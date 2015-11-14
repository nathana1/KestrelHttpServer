// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.AspNet.Server.Kestrel.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNet.Server.Kestrel.Networking
{
    /// <summary>
    /// Summary description for UvWriteRequest
    /// </summary>
    public class UvWriteReq : UvRequest
    {
        private readonly static Libuv.uv_write_cb _uv_write_cb = (IntPtr ptr, int status) => UvWriteCb(ptr, status);

        private readonly ArraySegment<ArraySegment<byte>> _dummyMessage = new ArraySegment<ArraySegment<byte>>(new[] { new ArraySegment<byte>(new byte[] { 1, 2, 3, 4 }) });

        private IntPtr _bufs;

        private Action<UvWriteReq, int, Exception, object> _callback;
        private object _state;
        internal const int BUFFER_COUNT = 16;

        private GCHandle _pin;
        private MemoryPoolBlock2[] _blocks;
        private int _blockCount;

        public UvWriteReq(IKestrelTrace logger) : base(logger)
        {
        }

        public void Init(UvLoopHandle loop, MemoryPoolBlock2[] blocks)
        {
            var requestSize = loop.Libuv.req_size(Libuv.RequestType.WRITE);
            var bufferSize = Marshal.SizeOf<Libuv.uv_buf_t>() * BUFFER_COUNT;
            CreateMemory(
                loop.Libuv,
                loop.ThreadId,
                requestSize + bufferSize);
            _bufs = handle + requestSize;
            _blocks = blocks;
        }

        public unsafe void Write(
            UvStreamHandle handle,
            int bufferCount,
            Action<UvWriteReq, int, Exception, object> callback,
            object state)
        {
            try
            {
                // add GCHandle to keeps this SafeHandle alive while request processing
                _pin = GCHandle.Alloc(this, GCHandleType.Normal);
                var pBuffers = (Libuv.uv_buf_t*)_bufs;

                _blockCount = bufferCount;
                for (var index = 0; index < bufferCount; index++)
                {
                    // create and pin each segment being written
                    var block = _blocks[index];
                    var length = block.End - block.Start;
                    var address = block.Pin() - length;

                    pBuffers[index] = Libuv.buf_init(
                        address,
                        length);
                }

                _callback = callback;
                _state = state;
                _uv.write(this, handle, pBuffers, bufferCount, _uv_write_cb);
            }
            catch
            {
                _callback = null;
                _state = null;
                UnpinLocal();
                throw;
            }
        }

        private void UnpinLocal()
        {
            _pin.Free();
            for (var index = 0; index < _blockCount; index++)
            {
                // create and pin each segment being written
                _blocks[index].Unpin();
            }
        }

        private static void UvWriteCb(IntPtr ptr, int status)
        {
            var req = FromIntPtr<UvWriteReq>(ptr);
            req.UnpinLocal();

            var callback = req._callback;
            req._callback = null;

            var state = req._state;
            req._state = null;

            req._blockCount = 0;

            Exception error = null;
            if (status < 0)
            {
                req.Libuv.Check(status, out error);
            }

            try
            {
                callback(req, status, error, state);
            }
            catch (Exception ex)
            {
                req._log.LogError("UvWriteCb", ex);
                throw;
            }
        }
    }
}