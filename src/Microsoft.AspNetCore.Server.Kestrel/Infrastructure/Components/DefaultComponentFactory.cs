// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Infrastructure
{
    public abstract class DefaultComponentFactory<T> : IComponentFactory<T> where T : class, IComponent
    {
        private static readonly int _poolCount = CalcuatePoolCount();
        private static readonly int _poolMask = _poolCount - 1;

        private CacheLinePadded<int> _poolIndex = new CacheLinePadded<int>();
        private ComponentPool<T>[] _pools = new ComponentPool<T>[_poolCount];

        private int _maxPooled;
        private int _maxPerPool;

        public int MaxPooled
        {
            get { return _maxPooled; }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MaxPooled));
                if (value != _maxPooled)
                {
                    var maxPerPool = (int)Math.Ceiling(value / (double)_poolCount);
                    if (maxPerPool == 0 && value > 0)
                    {
                        maxPerPool = 1;
                    }

                    Interlocked.Exchange(ref _pools, CreatePools(_poolCount, maxPerPool));

                    _maxPooled = value;
                    _maxPerPool = maxPerPool;
                }
            }
        }

        public DefaultComponentFactory()
            : this(0)
        {
        }

        public DefaultComponentFactory(int maxPooled)
        {
            MaxPooled = maxPooled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ComponentPool<T> GetPool(int poolIndex)
        {
            return Volatile.Read(ref _pools[poolIndex]);
        }

        private int NextPoolIndex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Interlocked.Increment(ref _poolIndex.Value) & _poolMask;
            }
        }

        protected abstract T CreateNew();

        public T Create()
        {
            if (_maxPooled == 0)
            {
                return CreateNew();
            }

            int poolIndex = NextPoolIndex;
            T component = null;
            if (!GetPool(poolIndex).TryRent(out component))
            {
                component = CreateNew();
                component.CorrelationId = poolIndex;
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

            if (_maxPooled > 0)
            {
                GetPool(component.CorrelationId).Return(component);
            }
        }

        private static ComponentPool<T>[] CreatePools(int poolCount, int maxPerPool)
        {
            var pools = new ComponentPool<T>[poolCount];
            for (var i = 0; i < pools.Length; i++)
            {
                pools[i] = new ComponentPool<T>(maxPerPool);
            }

            return pools;
        }

        private static int CalcuatePoolCount()
        {
            var processors = Environment.ProcessorCount;

            if (processors > 64) return 128;
            if (processors > 32) return 64;
            if (processors > 16) return 32;
            if (processors > 8) return 16;
            if (processors > 4) return 8;
            if (processors > 2) return 4;
            if (processors > 1) return 2;
            return 1;
        }
    }
}
