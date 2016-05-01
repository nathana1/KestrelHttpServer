// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel;
using Microsoft.AspNetCore.Server.Kestrel.Http;
using Microsoft.AspNetCore.Server.Kestrel.Infrastructure;
using Microsoft.AspNetCore.Server.Kestrel.TestCommon;

namespace Microsoft.AspNetCore.Server.KestrelTests
{
    /// <summary>
    /// Summary description for TestServer
    /// </summary>
    public class TestServer : IDisposable
    {
        public HeaderFactory HeaderFactory;
        public StreamFactory StreamFactory;

        private KestrelEngine _engine;
        private IDisposable _server;
        ServerAddress _address;

        public TestServer(RequestDelegate app)
            : this(app, new TestServiceContext())
        {
        }

        public TestServer(RequestDelegate app, ServiceContext context)
            : this(app, context, $"http://localhost:{GetNextPort()}/")
        {
        }

        public int Port => _address.Port;

        public TestServer(RequestDelegate app, ServiceContext context, string serverAddress)
        {
            var dateHeaderValueManager = new DateHeaderValueManager();
            HeaderFactory = new HeaderFactory();
            StreamFactory = new StreamFactory();
            context.FrameFactory = connectionContext =>
            {
                return new Frame<HttpContext>(new DummyApplication(app), connectionContext)
                {
                    DateHeaderValueManager = dateHeaderValueManager,
                    HeaderFactory = HeaderFactory,
                    StreamFactory = StreamFactory
                };
            };

            try
            {
                _engine = new KestrelEngine(context);
                _engine.Start(1);
                _address = ServerAddress.FromUrl(serverAddress);
                _server = _engine.CreateServer(_address);
            }
            catch
            {
                _server?.Dispose();
                _engine?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            _server.Dispose();
            _engine.Dispose();
        }

        public static int GetNextPort()
        {
            return PortManager.GetNextPort();
        }
    }
}