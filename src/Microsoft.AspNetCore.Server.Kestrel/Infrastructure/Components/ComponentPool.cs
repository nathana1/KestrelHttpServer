// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Infrastructure
{
    public class ComponentPool<T> where T : class
    {
        private readonly T[] _objects;

        private SpinLock _lock; // do not make this readonly; it's a mutable struct
        private int _index = -1;

        /// <summary>
        /// Creates the pool with maxPooled objects.
        /// </summary>
        public ComponentPool(int maxPooled)
        {
            if (maxPooled <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPooled));
            }

            _lock = new SpinLock(Debugger.IsAttached); // only enable thread tracking if debugger is attached; it adds non-trivial overheads to Enter/Exit
            _objects = new T[maxPooled];
        }

        /// <summary>Tries to take an object from the pool, returns true if sucessful.</summary>
        public bool TryRent(out T obj)
        {
            T[] objects = _objects;
            obj = null;
            // While holding the lock, grab whatever is at the next available index and
            // update the index.  We do as little work as possible while holding the spin
            // lock to minimize contention with other threads.  The try/finally is
            // necessary to properly handle thread aborts on platforms which have them.
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                var removeIndex = _index;
                if (removeIndex >= 0)
                {
                    obj = objects[removeIndex];
                    objects[removeIndex] = null;
                    _index = removeIndex - 1;
                }
            }
            finally
            {
                if (lockTaken) _lock.Exit(false);
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
            bool lockTaken = false;
            try
            {
                _lock.Enter(ref lockTaken);

                var insertIndex = _index + 1;
                if (insertIndex < _objects.Length)
                {
                    _objects[insertIndex] = obj;
                    _index = insertIndex;
                }
            }
            finally
            {
                if (lockTaken) _lock.Exit(false);
            }
        }
    }
}
