// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.AspNet.Server.Kestrel.Infrastructure;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Microsoft.AspNet.Server.Kestrel.Networking
{
    /// <summary>
    /// Summary description for UvWriteRequest
    /// </summary>
    public class UvWriteReq : UvRequest
    {
        private readonly static Libuv.uv_write_cb _uv_write_cb = (ptr, status) => UvWriteCb(ptr, status);

        private IntPtr _bufs;

        private Action<UvWriteReq, int, Exception, int, object> _callback;
        private object _state;
        public const int BUFFER_COUNT = 4;

        private ArraySegment<MemoryPoolBlock2> _buffers;
        private GCHandle _pin;

        public UvWriteReq(IKestrelTrace logger) : base(logger)
        {
        }

        public void Init(UvLoopHandle loop)
        {
            var requestSize = loop.Libuv.req_size(Libuv.RequestType.WRITE);
            var bufferSize = Marshal.SizeOf<Libuv.uv_buf_t>() * BUFFER_COUNT;
            CreateMemory(
                loop.Libuv,
                loop.ThreadId,
                requestSize + bufferSize);
            _bufs = handle + requestSize;
        }

        public unsafe void Write(
            UvStreamHandle handle,
            ArraySegment<MemoryPoolBlock2> bufs,
            Action<UvWriteReq, int, Exception, int, object> callback,
            object state)
        {
            try
            {
                _buffers = bufs;
                // add GCHandle to keeps this SafeHandle alive while request processing
                _pin = GCHandle.Alloc(this, GCHandleType.Normal);

                var pBuffers = (Libuv.uv_buf_t*)_bufs;
                var nBuffers = bufs.Count;

                for (var index = 0; index < nBuffers; index++)
                {
                    var buf = bufs.Array[bufs.Offset + index];
                    var len = buf.End - buf.Start;
                    // create and pin each segment being written
                    pBuffers[index] = Libuv.buf_init(
                        buf.Pin() - len,
                        buf.End - buf.Start);
                }

                _callback = callback;
                _state = state;
                _uv.write(this, handle, pBuffers, nBuffers, _uv_write_cb);
            }
            catch
            {
                _callback = null;
                _state = null;
                Unpin(this);
                ProcessBlocks(this);
                throw;
            }
        }

        public unsafe void Write2(
            UvStreamHandle handle,
            ArraySegment<MemoryPoolBlock2> bufs,
            UvStreamHandle sendHandle,
            Action<UvWriteReq, int, Exception, int, object> callback,
            object state)
        {
            try
            {
                _buffers = bufs;
                // add GCHandle to keeps this SafeHandle alive while request processing
                _pin = GCHandle.Alloc(this, GCHandleType.Normal);

                var pBuffers = (Libuv.uv_buf_t*)_bufs;
                var nBuffers = bufs.Count;

                for (var index = 0; index < nBuffers; index++)
                {
                    var buf = bufs.Array[bufs.Offset + index];
                    var len = buf.End - buf.Start;

                    pBuffers[index] = Libuv.buf_init(
                        buf.Pin() - len,
                        buf.End - buf.Start);
                }

                _callback = callback;
                _state = state;
                _uv.write2(this, handle, pBuffers, nBuffers, sendHandle, _uv_write_cb);
            }
            catch
            {
                _callback = null;
                _state = null;
                Unpin(this);
                ProcessBlocks(this);
                throw;
            }
        }

        private static void Unpin(UvWriteReq req)
        {
            req._pin.Free();
        }

        private static void UvWriteCbThreadPoolNoError(object state)
        {
            var req = (UvWriteReq)state;
            UvWriteCbThreadPool(req, 0);
        }

        private static void UvWriteCbThreadPool(UvWriteReq req, int status)
        {
            var bytesWritten = ProcessBlocks(req);

            var callback = req._callback;
            req._callback = null;

            var state = req._state;
            req._state = null;

            Exception error = null;
            if (status < 0)
            {
                req.Libuv.Check(status, out error);
            }

            try
            {
                callback(req, status, error, bytesWritten, state);
            }
            catch (Exception ex)
            {
                req._log.LogError("UvWriteCb", ex);
                throw;
            }
        }

        private static int ProcessBlocks(UvWriteReq req)
        {
            var bytesWritten = 0;
            var end = req._buffers.Offset + req._buffers.Count;
            for (var i = req._buffers.Offset; i < end; i++)
            {
                var block = req._buffers.Array[i];
                bytesWritten += block.End - block.Start;

                block.Unpin();

                if (block.Pool != null)
                {
                    block.Pool.Return(block);
                }
            }

            return bytesWritten;
        }

        private static void UvWriteCb(IntPtr ptr, int status)
        {
            var req = FromIntPtr<UvWriteReq>(ptr);
            if (req._pin != null)
            {
                Unpin(req);

                if (status >= 0)
                {
                    ThreadPool.QueueUserWorkItem((r) => UvWriteCbThreadPoolNoError(r), req);
                }
                else
                {
                    ThreadPool.QueueUserWorkItem((r) => UvWriteCbThreadPool((UvWriteReq)r, status), req);
                }
            }
        }

        public struct WriteBlock
        {
            public MemoryPoolBlock2 Block;
            public IntPtr Memory;
            public int Length;
        }
    }
}