// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Infrastructure
{
    public class ComponentPool<T> where T : class
    {
        private readonly T[][] _buckets;
        private const int _bucketCount = 32;

        private object[] _locks;
        private int[] _indices;

        /// <summary>
        /// Creates the pool with maxPooled objects.
        /// </summary>
        public ComponentPool(int maxPooled)
        {
            if (maxPooled <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPooled));
            }

            var objectsPerBucket = maxPooled > _bucketCount ? maxPooled / _bucketCount : 1;
            _locks = new object[_bucketCount];
            _buckets = new T[_bucketCount][];
            _indices = new int[_bucketCount];
            for(var i = 0; i < _bucketCount; i++)
            {
                _locks[i] = new object();
                _buckets[i] = new T[objectsPerBucket]; 
                _indices[i] = -1;
            }
        }

        /// <summary>Tries to take an object from the pool, returns true if sucessful.</summary>
        public bool TryRent(out T obj)
        {
            var bucketIndex = (Thread.CurrentThread.ManagedThreadId >> 1) % _bucketCount;
            T[][] buckets = _buckets;
            obj = null;
            // While holding the lock, grab whatever is at the next available index and
            // update the index.  We do as little work as possible while holding the spin
            // lock to minimize contention with other threads.  The try/finally is
            // necessary to properly handle thread aborts on platforms which have them.
            bool lockTaken = false;
            try
            {
                Monitor.Enter(_locks[bucketIndex], ref lockTaken);
                var removeIndex = _indices[bucketIndex];
                if (removeIndex >= 0)
                {
                    obj = buckets[bucketIndex][removeIndex];
                    buckets[bucketIndex][removeIndex] = null;
                    _indices[bucketIndex] = removeIndex - 1;
                }
            }
            finally
            {
                if (lockTaken) 
                {
                    Monitor.Exit(_locks[bucketIndex]);
                }
            }
            return obj != null;
        }

        /// <summary>
        /// Attempts to return the object to the pool.  If successful, the object will be stored
        /// in the pool; otherwise, the buffer won't be stored.
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null)
            {
                return;
            }

            // While holding the spin lock, if there's room available in the array,
            // put the object into the next available slot.  Otherwise, we just drop it.
            // The try/finally is necessary to properly handle thread aborts on platforms
            // which have them.
            var bucketIndex = (Thread.CurrentThread.ManagedThreadId >> 1) % _bucketCount;
            bool lockTaken = false;
            try
            {
                Monitor.Enter(_locks[bucketIndex], ref lockTaken);
                var insertIndex = _indices[bucketIndex] + 1;
                if (insertIndex < _buckets[bucketIndex].Length)
                {
                    _buckets[bucketIndex][insertIndex] = obj;
                    _indices[bucketIndex] = insertIndex;
                }
            }
            finally
            {
                if (lockTaken) 
                {
                    Monitor.Exit(_locks[bucketIndex]);
                }
            }
        }
    }
}
