// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNetCore.Server.Kestrel.Infrastructure
{
    public interface IComponentFactory<T> where T : class
    {
        int MaxPooled { get; set; }
        T Create();
        void Reset(T streams);
        void Dispose(T streams);
    }
}
