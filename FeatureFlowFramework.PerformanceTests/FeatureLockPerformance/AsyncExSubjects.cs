﻿using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace FeatureLoom.PerformanceTests.FeatureLockPerformance
{
    public class AsyncExSubjects
    {
        private Nito.AsyncEx.AsyncLock myLock = new Nito.AsyncEx.AsyncLock();

        public void Init() => myLock = new Nito.AsyncEx.AsyncLock();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock(Action action)
        {
            using (myLock.Lock())
            {
                action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync(Func<Task> action)
        {
            using (await myLock.LockAsync())
            {
                await action();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Lock()
        {
            using (myLock.Lock())
            {
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public async Task LockAsync()
        {
            using (await myLock.LockAsync())
            {
            }
        }
    }
}