// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Server.Kestrel.Infrastructure;
using Microsoft.AspNet.Server.Kestrel.Networking;

namespace Microsoft.AspNet.Server.Kestrel.Http
{
    public class SocketOutput : ISocketOutput
    {
        private const int _maxPendingWrites = 3;
        private const int _maxBytesPreCompleted = 64512;
        private const int _maxPooledWriteContexts = 16;

        private readonly KestrelThread _thread;
        private readonly UvStreamHandle _socket;
        private readonly long _connectionId;
        private readonly IKestrelTrace _log;

        // This locks access to to all of the below fields
        private readonly object _lockObj = new object();
        private bool _isDisposed; 

        // The number of write operations that have been scheduled so far
        // but have not completed.
        private int _writesPending = 0;

        private int _numBytesPreCompleted = 0;
        private Exception _lastWriteError;
        private WriteContext _nextWriteContext;
        private readonly Queue<TaskCompletionSource<object>> _tasksPending;
        private readonly Queue<WriteContext> _writeContextsPending;
        private readonly Queue<WriteContext> _writeContextPool;

        public SocketOutput(KestrelThread thread, UvStreamHandle socket, long connectionId, IKestrelTrace log)
        {
            _thread = thread;
            _socket = socket;
            _connectionId = connectionId;
            _log = log;
            _tasksPending = new Queue<TaskCompletionSource<object>>(16);
            _writeContextsPending = new Queue<WriteContext>(16);
            _writeContextPool = new Queue<WriteContext>(_maxPooledWriteContexts);
        }

        public Task WriteAsync(
            ArraySegment<byte> buffer,
            bool immediate = true,
            bool socketShutdownSend = false,
            bool socketDisconnect = false)
        {
            MemoryPoolBlock2 memoryBlock = null;
            if (buffer.Array != null && buffer.Count > 0)
            {
                var transferred = 0;
                var remaining = buffer.Count;

                if (_nextWriteContext != null)
                {
                    lock (_lockObj)
                    {
                        if (_nextWriteContext != null && _nextWriteContext.BufferCount > 0)
                        {
                            // Data ready but not sent yet, append to available buffer
                            memoryBlock = _nextWriteContext.Buffers[_nextWriteContext.BufferCount - 1];
                            var remainingBlockSize = memoryBlock.Data.Count - (memoryBlock.End - memoryBlock.Start);
                            var copyAmount = remainingBlockSize >= remaining ? remaining : remainingBlockSize;

                            if (copyAmount > 0)
                            {

                                Buffer.BlockCopy(buffer.Array, buffer.Offset, memoryBlock.Array, memoryBlock.End, copyAmount);
                                remaining -= copyAmount;
                                memoryBlock.End += copyAmount;
                                transferred += copyAmount;
                            }

                            if (remaining == 0)
                            {
                                return ScheduleWriteAsync(immediate, socketShutdownSend, socketDisconnect, copyAmount);
                            }
                        }
                    }
                }

                while (remaining > 0)
                {
                    memoryBlock = _thread.Memory2.Lease();
                    var blockSize = memoryBlock.Data.Count;
                    var copyAmount = blockSize >= remaining ? remaining : blockSize;

                    Buffer.BlockCopy(buffer.Array, buffer.Offset + transferred, memoryBlock.Array, memoryBlock.End, copyAmount);
                    remaining -= copyAmount;
                    memoryBlock.End += copyAmount;

                    if (remaining > 0)
                    {
                        WriteAsync(memoryBlock, false, false, false);
                        transferred += copyAmount;
                        memoryBlock = null;
                    }
                }
            }

            return WriteAsync(memoryBlock, immediate, socketShutdownSend, socketDisconnect);
        }
        
        public Task WriteAsync(
            MemoryPoolBlock2 memoryBlock,
            bool immediate = true,
            bool socketShutdownSend = false,
            bool socketDisconnect = false)
        {
            var transferBytes = 0;
            var hasData = memoryBlock != null;
            if (hasData && memoryBlock.Start == memoryBlock.End)
            {
                hasData = false;
                _thread.Memory2.Return(memoryBlock);
            }
            else if (hasData)
            {
                transferBytes = memoryBlock.End - memoryBlock.Start;
            }

            lock (_lockObj)
            {
                if (_nextWriteContext == null || _nextWriteContext.BufferCount == UvWriteReq.BUFFER_COUNT)
                {
                    if (_writeContextPool.Count > 0)
                    {
                        _nextWriteContext = _writeContextPool.Dequeue();
                    }
                    else
                    {
                        _nextWriteContext = new WriteContext(this);
                    }
                    _writeContextsPending.Enqueue(_nextWriteContext);
                }

                if (hasData)
                {
                    _nextWriteContext.ByteCount += transferBytes;
                    _nextWriteContext.Buffers[_nextWriteContext.BufferCount] = memoryBlock;
                    _nextWriteContext.BufferCount += 1;
                }

                return ScheduleWriteAsync(immediate, socketShutdownSend, socketDisconnect, transferBytes);
            }
        }

        private Task ScheduleWriteAsync(bool immediate, bool socketShutdownSend, bool socketDisconnect, int transferBytes)
        {
            TaskCompletionSource<object> tcs = null;
            if (socketShutdownSend)
            {
                _nextWriteContext.SocketShutdownSend = true;
            }
            if (socketDisconnect)
            {
                _nextWriteContext.SocketDisconnect = true;
            }

            if (!immediate)
            {
                // immediate==false calls always return complete tasks, because there is guaranteed
                // to be a subsequent immediate==true call which will go down one of the previous code-paths
                _numBytesPreCompleted += transferBytes;
            }
            else if (_lastWriteError == null &&
                    _tasksPending.Count == 0 &&
                    _numBytesPreCompleted + transferBytes <= _maxBytesPreCompleted)
            {
                // Complete the write task immediately if all previous write tasks have been completed,
                // the buffers haven't grown too large, and the last write to the socket succeeded.
                _numBytesPreCompleted += transferBytes;
            }
            else
            {
                // immediate write, which is not eligable for instant completion above
                tcs = new TaskCompletionSource<object>(transferBytes);
                _tasksPending.Enqueue(tcs);
            }

            if (_writesPending < _maxPendingWrites && immediate)
            {
                ScheduleWrite();
                _writesPending++;
            }

            // Return TaskCompletionSource's Task if set, otherwise completed Task 
            return tcs?.Task ?? TaskUtilities.CompletedTask;
        }

        public void End(ProduceEndType endType)
        {
            switch (endType)
            {
                case ProduceEndType.SocketShutdownSend:
                    WriteAsync(default(ArraySegment<byte>),
                        immediate: true,
                        socketShutdownSend: true,
                        socketDisconnect: false);
                    break;
                case ProduceEndType.SocketDisconnect:
                    WriteAsync(default(ArraySegment<byte>),
                        immediate: true,
                        socketShutdownSend: false,
                        socketDisconnect: true);
                    break;
            }
        }

        private void ScheduleWrite()
        {
            _thread.Post(_this => _this.WriteAllPending(), this);
        }

        // This is called on the libuv event loop
        private void WriteAllPending()
        {
            WriteContext writingContext;
            var moreData = true;
            do
            {
                lock (_lockObj)
                {
                    var count = _writeContextsPending.Count;
                    if (count > 0)
                    {
                        writingContext = _writeContextsPending.Dequeue();
                        if (writingContext == _nextWriteContext)
                        {
                            _nextWriteContext = null;
                        }
                    }
                    else
                    {
                        _writesPending--;
                        return;
                    }
                    moreData = count > 1;
                }

                try
                { 
                    writingContext.DoWriteIfNeeded();
                }
                catch
                {
                    lock (_lockObj)
                    {
                        // Lock instead of using Interlocked.Decrement so _writesSending
                        // doesn't change in the middle of executing other synchronized code.
                        _writesPending--;
                    }

                    throw;
                }
            } while (moreData);
        }

        // This is called on the libuv event loop
        private void OnWriteCompleted(WriteContext writeContext)
        {
            var status = writeContext.WriteStatus;

            lock (_lockObj)
            {
                _lastWriteError = writeContext.WriteError;

                if (_nextWriteContext != null)
                {
                    ScheduleWrite();
                }
                else
                {
                    _writesPending--;
                }
                
                // _numBytesPreCompleted can temporarily go negative in the event there are
                // completed writes that we haven't triggered callbacks for yet.
                _numBytesPreCompleted -= writeContext.ByteCount;

                // bytesLeftToBuffer can be greater than _maxBytesPreCompleted
                // This allows large writes to complete once they've actually finished.
                var bytesLeftToBuffer = _maxBytesPreCompleted - _numBytesPreCompleted;
                while (_tasksPending.Count > 0 &&
                       (int)(_tasksPending.Peek().Task.AsyncState) <= bytesLeftToBuffer)
                {
                    var tcs = _tasksPending.Dequeue();
                    var bytesToWrite = (int)tcs.Task.AsyncState;

                    _numBytesPreCompleted += bytesToWrite;
                    bytesLeftToBuffer -= bytesToWrite;

                    if (writeContext.WriteError == null)
                    {
                        ThreadPool.QueueUserWorkItem(
                            (o) => ((TaskCompletionSource<object>)o).SetResult(null), 
                            tcs);
                    }
                    else
                    {
                        var error = writeContext.WriteError;
                        // error is closure captured 
                        ThreadPool.QueueUserWorkItem(
                            (o) => ((TaskCompletionSource<object>)o).SetException(error), 
                            tcs);
                    }
                }

                if (_writeContextPool.Count < _maxPooledWriteContexts 
                    && !_isDisposed)
                {
                    writeContext.Reset();
                    _writeContextPool.Enqueue(writeContext);
                }
                else
                {
                    writeContext.Dispose();
                }

                // Now that the while loop has completed the following invariants should hold true:
                Debug.Assert(_numBytesPreCompleted >= 0);
            }

            _log.ConnectionWriteCallback(_connectionId, status);
        }

        public void Write(ArraySegment<byte> buffer, bool immediate)
        {
            var task = WriteAsync(buffer, immediate);

            if (task.Status == TaskStatus.RanToCompletion)
            {
                return;
            }
            else
            {
                task.GetAwaiter().GetResult();
            }
        }

        public Task WriteAsync(ArraySegment<byte> buffer, bool immediate, CancellationToken cancellationToken)
        {
            return WriteAsync(buffer, immediate);
        }

        private void Dispose()
        {
            lock (_lockObj)
            {
                _isDisposed = true;

                while (_writeContextPool.Count > 0)
                {
                    _writeContextPool.Dequeue().Dispose();
                }
            }

        }

        private class WriteContext : IDisposable
        {
            public SocketOutput Self;
            public MemoryPoolBlock2[] Buffers;
            public int BufferCount;
            public int ByteCount;
            
            public bool SocketShutdownSend;
            public bool SocketDisconnect;

            public int WriteStatus;
            public Exception WriteError;

            private UvWriteReq _writeReq;

            public int ShutdownSendStatus;

            public WriteContext(SocketOutput self)
            {
                Self = self;
                Buffers = new MemoryPoolBlock2[UvWriteReq.BUFFER_COUNT];
                _writeReq = new UvWriteReq(Self._log);
                _writeReq.Init(Self._thread.Loop, Buffers);
            }

            /// <summary>
            /// First step: initiate async write if needed, otherwise go to next step
            /// </summary>
            public void DoWriteIfNeeded()
            {
                if (BufferCount == 0 || Self._socket.IsClosed)
                {
                    DoShutdownIfNeeded();
                    return;
                }

                _writeReq.Write(
                    Self._socket, 
                    BufferCount, 
                    (writeReq, status, error, state) =>
                        {
                            ((WriteContext)state).WriteCallback(writeReq, status, error);
                        }, 
                    this);
            }

            public void WriteCallback(UvWriteReq writeReq, int status, Exception error)
            {
                var buffers = Buffers;
                for (var i = 0; i < buffers.Length; i++)
                {
                    var buffer = buffers[i];
                    if (buffer != null)
                    {
                        var pool = buffer.Pool;
                        if (pool != null)
                        {
                            pool.Return(buffer);
                        }
                        buffers[i] = null;
                    }
                }
                WriteStatus = status;
                WriteError = error;
                DoShutdownIfNeeded();
            }

            /// <summary>
            /// Second step: initiate async shutdown if needed, otherwise go to next step
            /// </summary>
            public void DoShutdownIfNeeded()
            {
                if (SocketShutdownSend == false || Self._socket.IsClosed)
                {
                    DoDisconnectIfNeeded();
                    return;
                }

                var shutdownReq = new UvShutdownReq(Self._log);
                shutdownReq.Init(Self._thread.Loop);
                shutdownReq.Shutdown(Self._socket, (_shutdownReq, status, state) =>
                {
                    _shutdownReq.Dispose();
                    var _this = (WriteContext)state;
                    _this.ShutdownSendStatus = status;

                    _this.Self._log.ConnectionWroteFin(_this.Self._connectionId, status);

                    _this.DoDisconnectIfNeeded();
                }, this);
            }

            /// <summary>
            /// Third step: disconnect socket if needed, otherwise this work item is complete
            /// </summary>
            public void DoDisconnectIfNeeded()
            {
                if (SocketDisconnect == false)
                {
                    Complete();
                    return;
                }
                else if (Self._socket.IsClosed)
                {
                    Self.Dispose();
                    Complete();
                    return;
                }

                Self._socket.Dispose();
                Self._log.ConnectionStop(Self._connectionId);
                Complete();
            }

            public void Complete()
            {
                Self.OnWriteCompleted(this);
            }

            public void Reset()
            {
                BufferCount = 0;
                ByteCount = 0;
                SocketDisconnect = false;
                SocketShutdownSend = false;
                WriteStatus = 0;
                WriteError = null;
                ShutdownSendStatus = 0;
            }

            public void Dispose()
            {
                _writeReq.Dispose();
            }
        }
    }
}
