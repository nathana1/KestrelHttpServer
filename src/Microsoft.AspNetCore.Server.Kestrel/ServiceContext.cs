// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Filter;
using Microsoft.AspNetCore.Server.Kestrel.Http;
using Microsoft.AspNetCore.Server.Kestrel.Infrastructure;

namespace Microsoft.AspNetCore.Server.Kestrel
{
    public class ServiceContext
    {
        public ServiceContext()
        {
        }

        public ServiceContext(ServiceContext context)
        {
            AppLifetime = context.AppLifetime;
            Log = context.Log;
            ThreadPool = context.ThreadPool;
            FrameFactory = context.FrameFactory;
            DateHeaderValueManager = context.DateHeaderValueManager;
            ConnectionFilter = context.ConnectionFilter;
            NoDelay = context.NoDelay;
            ReuseStreams = context.ReuseStreams;
        }

        public IApplicationLifetime AppLifetime { get; set; }

        public IKestrelTrace Log { get; set; }

        public IThreadPool ThreadPool { get; set; }

        public Func<ConnectionContext, IPEndPoint, IPEndPoint, Action<IFeatureCollection>, Frame> FrameFactory { get; set; }

        public DateHeaderValueManager DateHeaderValueManager { get; set; }

        public IConnectionFilter ConnectionFilter { get; set; }

        public bool NoDelay { get; set; }

        public bool ReuseStreams { get; set; }
    }
}
