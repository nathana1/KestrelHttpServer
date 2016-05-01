// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Infrastructure
{
    public abstract class DefaultComponentFactory<T> : IComponentFactory<T> where T : class, IComponent
    {
        private ComponentPool<T> _pool;

        private int _maxPooled = 0;
        public int MaxPooled
        {
            get { return _maxPooled; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MaxPooled));
                if (value != _maxPooled)
                {
                    _maxPooled = value;
                    Interlocked.Exchange(ref _pool, null);
                }
            }
        }

        private ComponentPool<T> Pool
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return (MaxPooled == 0) ?
                    null : Volatile.Read(ref _pool) ?? EnsurePoolCreated(ref _pool, MaxPooled);
            }
        }

        protected abstract T CreateNew();

        public T Create()
        {
            T component = null;

            if (!(Pool?.TryRent(out component) ?? false))
            {
                component = CreateNew();
            }
            return component;
        }

        public void Reset(T component)
        {
            component.Reset();
        }

        public void Dispose(T component)
        {
            component.Uninitialize();
            Pool?.Return(component);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ComponentPool<T> EnsurePoolCreated(ref ComponentPool<T> pool, int maxPooled)
        {
            Interlocked.CompareExchange(ref pool, CreatePool(maxPooled), null);
            return pool;
        }

        private static ComponentPool<T> CreatePool(int maxPooled)
        {
            return new ComponentPool<T>(maxPooled);
        }
    }
}
