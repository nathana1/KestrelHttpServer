// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Infrastructure
{
    public class ComponentPool<T> where T : class
    {
        private readonly T[] _objects;

        private CacheLinePadded<int> _ticket;
        private CacheLinePadded<int> _lock;
        private CacheLinePadded<int> _index;

        /// <summary>
        /// Creates the pool with maxPooled objects.
        /// </summary>
        public ComponentPool(int maxPooled)
        {
            if (maxPooled <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPooled));
            }
            _ticket = new CacheLinePadded<int>();
            _lock = new CacheLinePadded<int>();
            _index = new CacheLinePadded<int>() { Value = -1 };
            _objects = new T[maxPooled];
        }

        /// <summary>Tries to take an object from the pool, returns true if sucessful.</summary>
        public bool TryRent(out T obj)
        {
            T[] objects = _objects;
            obj = null;

            try
            {
                // Protect lock+unlock from Thread.Abort
            }
            finally
            {
                Lock();

                var removeIndex = _index.Value;
                if (removeIndex >= 0)
                {
                    obj = objects[removeIndex];
                    objects[removeIndex] = null;
                    _index.Value = removeIndex - 1;
                }

                Unlock();
            }

            return obj != null;
        }

        /// <summary>
        /// Attempts to return the object to the pool.  If successful, the object will be stored
        /// in the pool; otherwise, the object won't be stored.
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null)
            {
                return;
            }

            try
            {
                // Protect lock+unlock from Thread.Abort
            }
            finally
            {
                Lock();

                var insertIndex = _index.Value + 1;
                if (insertIndex < _objects.Length)
                {
                    _objects[insertIndex] = obj;
                    _index.Value = insertIndex;
                }

                Unlock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Lock()
        {
            var slot = Interlocked.Increment(ref _ticket.Value);
            if (slot == 0)
            {
                slot = Interlocked.Increment(ref _ticket.Value);
            }

            var lockTaken = Interlocked.CompareExchange(ref _lock.Value, slot, 0) == 0;
            if (lockTaken)
            {
                return;
            }

            var sw = new SpinWait();
            while (!lockTaken)
            {
                sw.SpinOnce();
                lockTaken = Interlocked.CompareExchange(ref _lock.Value, slot, 0) == 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Unlock()
        {
            Interlocked.Exchange(ref _lock.Value, 0);
        }
    }
}
