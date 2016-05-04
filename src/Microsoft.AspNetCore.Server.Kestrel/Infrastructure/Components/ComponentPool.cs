// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.AspNetCore.Server.Kestrel.Infrastructure
{
    public class ComponentPool<T> : ComponentPool where T : class
    {
        private readonly T[] _objects;

        private CacheLinePadded<int> _index;
        private McsStackLock _mcsLock;

        /// <summary>
        /// Creates the pool with maxPooled objects.
        /// </summary>
        public ComponentPool(int maxPooled)
        {
            if (maxPooled <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPooled));
            }

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
                var ticket = new McsStackLockTicket();
                _mcsLock.Lock(ref ticket);

                var removeIndex = _index.Value;
                if (removeIndex >= 0)
                {
                    obj = objects[removeIndex];
                    objects[removeIndex] = null;
                    _index.Value = removeIndex - 1;
                }

                _mcsLock.Unlock(ref ticket);
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
                var ticket = new McsStackLockTicket();
                _mcsLock.Lock(ref ticket);

                var insertIndex = _index.Value + 1;
                if (insertIndex < _objects.Length)
                {
                    _objects[insertIndex] = obj;
                    _index.Value = insertIndex;
                }

                _mcsLock.Unlock(ref ticket);
            }
        }
    }

    public class ComponentPool
    {
        /// <summary>
        /// McsStackLock is a specialized MCS Lock where its lock and unlock can only be used within the same stack scope.
        /// Also both must be used within a finally block to prevent Thread.Abort Exceptions
        /// </summary>
        protected unsafe struct McsStackLock
        {
            private CacheLinePadded<McsStackLockTicket> _queue;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Lock(ref McsStackLockTicket newTicket)
            {
                if (newTicket.IsLocked) throw new ArgumentOutOfRangeException(nameof(newTicket));

                newTicket.IsLocked = true;

                fixed (McsStackLockTicket* ticket = &newTicket)
                {
                    var previous = Interlocked.Exchange(ref _queue.Value.Next, (IntPtr)ticket);
                    if (previous == IntPtr.Zero)
                    {
                        // Have lock
                        return;
                    }

                    Interlocked.Exchange(ref (*(McsStackLockTicket*)previous).Next, (IntPtr)ticket);

                    var sw = new SpinWait();
                    while (Volatile.Read(ref newTicket.IsLocked))
                    {
                        sw.SpinOnce();
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Unlock(ref McsStackLockTicket lockedTicket)
            {
                if (!lockedTicket.IsLocked) throw new ArgumentOutOfRangeException(nameof(lockedTicket));

                if (lockedTicket.Next == IntPtr.Zero)
                {
                    fixed (McsStackLockTicket* ticket = &lockedTicket)
                    {
                        if (Interlocked.CompareExchange(ref _queue.Value.Next, IntPtr.Zero, (IntPtr)ticket) == (IntPtr)ticket)
                        {
                            // Unlocked, nothing waiting
                            return;
                        };

                        var next = Volatile.Read(ref lockedTicket.Next);
                        while (Volatile.Read(ref next) == IntPtr.Zero)
                        {
                            next = Volatile.Read(ref lockedTicket.Next);
                        }

                        Volatile.Write(ref (*((McsStackLockTicket*)next)).IsLocked, false);
                    }
                }
                else
                {
                    Volatile.Write(ref (*((McsStackLockTicket*)lockedTicket.Next)).IsLocked, false);
                    // Unlocked, next waiting notified
                }
            }
        }

        protected struct McsStackLockTicket
        {
            public bool IsLocked;
            public IntPtr Next;
        }
    }
}
