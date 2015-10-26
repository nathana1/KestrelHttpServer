// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Server.Kestrel.Infrastructure;
using Microsoft.AspNet.Server.Kestrel.Networking;

namespace Microsoft.AspNet.Server.Kestrel.Http
{
    public class SocketOutput : ISocketOutput
    {
        //private const int _maxPendingWrites = 3;
        //private const int _maxBytesPreCompleted = 65536;

        private readonly static int _stride = Vector<byte>.Count;
        private readonly static int _span = _stride - 1;

        private readonly KestrelThread _thread;
        private readonly UvStreamHandle _socket;
        private readonly long _connectionId;
        private readonly IKestrelTrace _log;

        // This locks access to to all of the below fields
        //private readonly object _lockObj = new object();
        //private readonly ConcurrentQueue<MemoryPoolBlock2> _writeQueue;
        private readonly MemoryPool2 _memory;

        private MemoryPoolBlock2 _currentMemoryBlock;

        private MemoryPoolBlock2[] _memoryBlocks;
        
        private int _currentBufferEnd;
        private int _preparedBuffers;

        private bool _socketShutdown;
        private bool _socketDisconnect;
        private long _bytesQueued;
        private long _bytesWritten;

        //private readonly SemaphoreSlim _wait;

        // The number of write operations that have been scheduled so far
        // but have not completed.
        private int _writesPending = 0;

        //private int _numBytesPreCompleted = 0;
        private Exception _lastWriteError;
        private int _lastWriteStatus;
        private int ShutdownSendStatus;
        //private WriteContext _nextWriteContext;
        private readonly Queue<CallbackContext> _callbacksPending;

        public SocketOutput(MemoryPool2 memory, KestrelThread thread, UvStreamHandle socket, long connectionId, IKestrelTrace log)
        {
            _thread = thread;
            _socket = socket;
            _connectionId = connectionId;
            _log = log;
            _callbacksPending = new Queue<CallbackContext>();
            _memory = memory;
            //_wait = new SemaphoreSlim(0);
            _memoryBlocks = new MemoryPoolBlock2[UvWriteReq.BUFFER_COUNT];
        }

        public void Write(
            ArraySegment<byte> buffer,
            Action<Exception, object, bool> callback,
            object state,
            bool immediate = true,
            bool socketShutdownSend = false,
            bool socketDisconnect = false)
        {
            var input = buffer.Array;
            var remaining = buffer.Count;
            _bytesQueued += remaining;

            _socketShutdown |= socketShutdownSend;
            _socketDisconnect |= socketDisconnect;

            if (remaining == 0 && _writesPending == 0)
            { 
                DoShutdownIfNeeded();
                // callback(error, state, calledInline)
                callback(null, state, true);
                return;
            }

            var inputOffset = buffer.Offset;
            var inputEnd = buffer.Offset + buffer.Count;
            byte[] output;


            while (remaining > 0)
            {
                if (_currentMemoryBlock == null)
                {
                    _currentMemoryBlock = _memory.Lease(MemoryPool2.NativeBlockSize);
                    _currentBufferEnd = _currentMemoryBlock.Start + _currentMemoryBlock.Data.Count;
                    _memoryBlocks[_preparedBuffers] = _currentMemoryBlock;
                }

                output = _currentMemoryBlock.Array;

                for (;
                    inputOffset + _span < inputEnd && _currentMemoryBlock.End + _span < _currentBufferEnd;
                    inputOffset += _stride, _currentMemoryBlock.End += _stride)
                {
                    (new Vector<byte>(input, inputOffset)).CopyTo(output, _currentMemoryBlock.End);
                    remaining -= _stride;
                }

                for (;
                    inputOffset < inputEnd && _currentMemoryBlock.End < _currentBufferEnd;
                    inputOffset++, _currentMemoryBlock.End++)
                {
                    output[_currentMemoryBlock.End] = input[inputOffset];
                    remaining--;
                }

                if (_currentMemoryBlock.End == _currentBufferEnd)
                {
                    _preparedBuffers++;
                    _currentMemoryBlock = null;
                }

                if (_preparedBuffers == UvWriteReq.BUFFER_COUNT)
                {
                    SendBufferedData();
                }
            }

            if (immediate)
            {
                if (_currentMemoryBlock != null)
                {
                    _preparedBuffers++;
                }

                if (_preparedBuffers > 0)
                {
                    SendBufferedData();
                }
            }

            if (_bytesWritten >= _bytesQueued)
            {
                callback(null, state, true);
            }
            else
            {
                _callbacksPending.Enqueue(new CallbackContext
                {
                    Callback = callback,
                    State = state,
                    BytesWrittenThreshold = _bytesQueued
                });
            }
        }

        private void SendBufferedData()
        {
            Interlocked.Increment(ref _writesPending);

            var mbs = new ArraySegment<MemoryPoolBlock2>(_memoryBlocks, 0, _preparedBuffers);

            var writeReq = new UvWriteReq(_log);
            writeReq.Init(_thread.Loop);
            writeReq.Write(_socket,
                mbs,
                (_writeReq, status, error, bytesWritten, socketOutput) =>
                {
                    _writeReq.Dispose();
                    var _this = (SocketOutput)socketOutput;

                    Interlocked.Add(ref _this._bytesWritten, bytesWritten);

                    _this._lastWriteStatus = status;
                    _this._lastWriteError = error;

                    var queuedWrites = Interlocked.Decrement(ref _this._writesPending);
                    if (queuedWrites == 0)
                    {
                        _this.DoShutdownIfNeeded();
                    }
                }, this);

            _memoryBlocks = new MemoryPoolBlock2[UvWriteReq.BUFFER_COUNT];
            _preparedBuffers = 0;
        }


        /// <summary>
        /// Second step: initiate async shutdown if needed, otherwise go to next step
        /// </summary>
        private void DoShutdownIfNeeded()
        {
            if (_socketShutdown == false || _socket.IsClosed)
            {
                DoDisconnectIfNeeded();
                return;
            }

            var shutdownReq = new UvShutdownReq(_log);
            shutdownReq.Init(_thread.Loop);
            shutdownReq.Shutdown(_socket, (_shutdownReq, status, state) =>
            {
                _shutdownReq.Dispose();
                var _this = (SocketOutput)state;
                _this.ShutdownSendStatus = status;

                _this._log.ConnectionWroteFin(_this._connectionId, status);

                _this.DoDisconnectIfNeeded();
            }, this);
        }

        /// <summary>
        /// Third step: disconnect socket if needed, otherwise this work item is complete
        /// </summary>
        private void DoDisconnectIfNeeded()
        {
            if (_socketDisconnect == false || _socket.IsClosed)
            {
                OnWriteCompleted();
                return;
            }

            _socket.Dispose();
            _log.ConnectionStop(_connectionId);
            OnWriteCompleted();
        }

        public void End(ProduceEndType endType)
        {
            switch (endType)
            {
                case ProduceEndType.SocketShutdownSend:
                    Write(default(ArraySegment<byte>), (error, state, calledInline) => { }, null,
                        immediate: true,
                        socketShutdownSend: true,
                        socketDisconnect: false);
                    break;
                case ProduceEndType.SocketDisconnect:
                    Write(default(ArraySegment<byte>), (error, state, calledInline) => { }, null,
                        immediate: true,
                        socketShutdownSend: false,
                        socketDisconnect: true);
                    break;
            }
        }

        // This is called on the libuv event loop
        private void OnWriteCompleted()
        {
            _log.ConnectionWriteCallback(_connectionId, _lastWriteStatus);

            while (_callbacksPending.Count > 0 &&
                   _callbacksPending.Peek().BytesWrittenThreshold <= _bytesWritten)
            {
                var callbackContext = _callbacksPending.Dequeue();

                // callback(error, state, calledInline)
                callbackContext.Callback(_lastWriteError, callbackContext.State, false);
            }
        }

        void ISocketOutput.Write(ArraySegment<byte> buffer, bool immediate)
        {
            if (!immediate)
            {
                // immediate==false calls always return complete tasks, because there is guaranteed
                // to be a subsequent immediate==true call which will go down the following code-path
                Write(
                    buffer,
                    (error, state, calledInline) => { },
                    null,
                    immediate: false);
                return;
            }

            // TODO: Optimize task being used, and remove callback model from the underlying Write
            var tcs = new TaskCompletionSource<int>();

            Write(
                buffer,
                (error, state, calledInline) =>
                {
                    var tcs2 = (TaskCompletionSource<int>)state;
                    if (error != null)
                    {
                        tcs2.SetException(error);
                    }
                    else
                    {
                        tcs2.SetResult(0);
                    }
                },
                tcs,
                immediate: true);

            if (tcs.Task.Status != TaskStatus.RanToCompletion)
            {
                tcs.Task.GetAwaiter().GetResult();
            }
        }

        async Task ISocketOutput.WriteAsync(ArraySegment<byte> buffer, bool immediate, CancellationToken cancellationToken)
        {
            if (!immediate)
            {
                // immediate==false calls always return complete tasks, because there is guaranteed
                // to be a subsequent immediate==true call which will go down the following code-path
                Write(
                    buffer,
                    (error, state, calledInline) => { },
                    null,
                    immediate: false);
                return;
            }

            // TODO: Optimize task being used, and remove callback model from the underlying Write
            var cs = new CompletionSource<int>();

            Write(
                buffer,
                (error, state, calledInline) =>
                {
                    var cs2 = (CompletionSource<int>)state;
                    if (error != null)
                    {
                        cs2.SetException(error);
                    }
                    else
                    {
                        cs2.SetResult(0);
                    }
                },
                cs,
                immediate: true);

            await cs;
            return;
        }

        private class CallbackContext
        {
            // callback(error, state, calledInline)
            public Action<Exception, object, bool> Callback;
            public object State;
            public long BytesWrittenThreshold;
        }
    }
}
