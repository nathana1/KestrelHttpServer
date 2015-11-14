// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.AspNet.Server.Kestrel.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNet.Server.Kestrel.Networking
{
    /// <summary>
    /// Summary description for UvWrite2Request
    /// </summary>
    public class UvWrite2Req : UvRequest
    {
        private readonly static Libuv.uv_write_cb _uv_write_cb = (IntPtr ptr, int status) => UvWriteCb(ptr, status);

        // this message is passed to write2 because it must be non-zero-length, 
        // but it has no other functional significance
        private readonly ArraySegment<byte> _dummyMessage = new ArraySegment<byte>(new byte[] { 1, 2, 3, 4 });

        private IntPtr _bufs;

        private Action<UvWrite2Req, int, Exception, object> _callback;
        private object _state;
        private const int BUFFER_COUNT = 1;

        private GCHandle _pinUvWrite2Req;
        private GCHandle _pinBuffer;

        public UvWrite2Req(IKestrelTrace logger) : base(logger)
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

        public unsafe void Write2(
            UvStreamHandle handle,
            UvStreamHandle sendHandle,
            Action<UvWrite2Req, int, Exception, object> callback,
            object state)
        {
            try
            {
                // add GCHandle to keeps this SafeHandle alive while request processing
                _pinUvWrite2Req = GCHandle.Alloc(this, GCHandleType.Normal);

                var pBuffers = (Libuv.uv_buf_t*)_bufs;

                _pinBuffer = GCHandle.Alloc(_dummyMessage.Array, GCHandleType.Pinned);

                pBuffers[0] = Libuv.buf_init(
                    _pinBuffer.AddrOfPinnedObject(),
                    _dummyMessage.Count);

                _callback = callback;
                _state = state;
                _uv.write2(this, handle, pBuffers, 1, sendHandle, _uv_write_cb);
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
            _pinUvWrite2Req.Free();
            _pinBuffer.Free();
        }

        private static void UvWriteCb(IntPtr ptr, int status)
        {
            var req = FromIntPtr<UvWrite2Req>(ptr);
            req.UnpinLocal();

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