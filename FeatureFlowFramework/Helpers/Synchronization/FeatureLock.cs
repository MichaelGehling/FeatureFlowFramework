﻿using FeatureFlowFramework.Helpers.Time;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Helpers.Synchronization
{
    public class FeatureLock
    {
        const int NO_LOCK = 0;
        const int WRITE_LOCK = -1;
        const int FIRST_READ_LOCK = 1;

        const int NOT_ENTERED = 0;
        const int FIRST_WRITE_ENTERED = -1;
        const int FIRST_READ_ENTERED = 1;

        const int INTERNAL_MAX_PRIORITY = int.MaxValue;        
        const int INTERNAL_MIN_PRIORITY = int.MinValue;
        public const int DEFAULT_PRIORITY = 0;
        public const int MIN_PRIORITY = INTERNAL_MIN_PRIORITY + 10_000;
        public const int MAX_PRIORITY = INTERNAL_MAX_PRIORITY - 10_000;
        public readonly TimeSpan sleepTime = 100.Milliseconds();

        const int FALSE = 0;
        const int TRUE = 1;

        readonly bool reentranceSupported;
        /// <summary>
        /// Multiple read-locks are allowed in parallel while write-locks are always exclusive.
        /// A lockIndicator of -1 implies a write-lock, while a lockIndicator greater than 0 implies a read-lock.
        /// When entering a read-lock, the lockIndicator is increased and decreased when leaving a read-lock.
        /// When entering a write-lock, a WRITE_LOCK(-1) is set and set back to NO_LOCK(0) when the write-lock is left.
        /// </summary>
        volatile int lockIndicator = NO_LOCK;        
        volatile int highestPriority = INTERNAL_MIN_PRIORITY;
        volatile int waitingForUpgrade = FALSE;
        volatile int numWaiting = 0;

        AsyncLocal<int> reentranceIndicator;

        AsyncManualResetEvent mre = new AsyncManualResetEvent(true);

        Task<AcquiredLock> readLockTask;
        Task<AcquiredLock> writeLockTask;

        public FeatureLock(bool supportReentrance = false)
        {
            this.reentranceSupported = supportReentrance;
            if (supportReentrance) reentranceIndicator = new AsyncLocal<int>();
            readLockTask = Task.FromResult(new AcquiredLock(this, false));
            writeLockTask = Task.FromResult(new AcquiredLock(this, true));
        }

        public void ResetReentranceContext()
        {
            if (reentranceSupported) reentranceIndicator.Value = NOT_ENTERED;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLockReadOnly(out AcquiredLock readLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            var timer = new TimeFrame(timeout);
            int waitCycles = 0;
            ApplyWaitOrder(ref priority);
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForReading();
                if (reentered)
                {
                    readLock = acquiredLock;
                    return true;
                }
            }
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                bool timedOut = TryLockReadOnlyWaitingLoop(ref priority, ref timer, ref waitCycles);

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = INTERNAL_MIN_PRIORITY;
                    readLock = new AcquiredLock();
                    return false;
                }
                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(waitCycles, priority);
            if (reentranceSupported) reentranceIndicator.Value = FIRST_READ_ENTERED;

            readLock = new AcquiredLock(this, false);
            return true;
        }

        private bool TryLockReadOnlyWaitingLoop(ref int priority, ref TimeFrame timer, ref int waitCycles)
        {
            bool timedOut = false;
            if (timer.Elapsed) timedOut = true;
            else
            {
                bool nextInQueue = UpdatePriority(ref priority, ref waitCycles);

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining)) timedOut = true;
                    }
                    else if (didReset) mre.Set();
                    if (!timedOut && priority > highestPriority) highestPriority = priority;
                }
                else Thread.Yield();
            }

            return timedOut;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryLock(out AcquiredLock writeLock, TimeSpan timeout = default, int priority = DEFAULT_PRIORITY)
        {
            var timer = new TimeFrame(timeout);
            int waitCycles = 0;
            ApplyWaitOrder(ref priority);

            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, timedOut , acquiredLock) = TryReenterForWritingWithTimeout(timer);
                writeLock = acquiredLock;
                if (reentered) return true;                
                else if (timedOut) return false;                
            }
            while (WriterMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool timedOut = TryLockWaitingLoop(ref priority, ref timer, ref waitCycles);

                if (timedOut)
                {
                    if (priority >= highestPriority) highestPriority = INTERNAL_MIN_PRIORITY;
                    writeLock = new AcquiredLock();
                    return false;
                }
                currentLockIndicator = lockIndicator;
            }
            UpdateAfterEnter(waitCycles, priority);
            if (reentranceSupported) reentranceIndicator.Value = FIRST_WRITE_ENTERED;

            writeLock = new AcquiredLock(this, true);
            return true;
        }

        private bool TryLockWaitingLoop(ref int priority, ref TimeFrame timer, ref int waitCycles)
        {
            bool timedOut = false;
            if (timer.Elapsed) timedOut = true;
            else
            {
                bool nextInQueue = UpdatePriority(ref priority, ref waitCycles);

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        if (!mre.Wait(timer.Remaining)) timedOut = true;
                    }
                    else if (didReset) mre.Set();
                    if (!timedOut && priority > highestPriority) highestPriority = priority;
                }
                else Thread.Yield();
            }

            return timedOut;
        }

        private (bool reentered, bool timedOut, AcquiredLock acquiredLock) TryReenterForWritingWithTimeout(TimeFrame timer)
        {
            var currentReentranceIndicator = reentranceIndicator.Value;
            if (currentReentranceIndicator <= FIRST_WRITE_ENTERED)
            {
                reentranceIndicator.Value = currentReentranceIndicator - 1;
                return (true, false, new AcquiredLock(this, false, true));
            }
            else if (currentReentranceIndicator >= FIRST_READ_ENTERED)
            {
                // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
                if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
                // Waiting for upgrade to writeLock
                while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
                {
                    if (timer.Elapsed) return (false, true, new AcquiredLock());
                    Thread.Yield(); // Could be more optimized, but it's such a rare case...
                }
                waitingForUpgrade = FALSE;
                reentranceIndicator.Value = -currentReentranceIndicator - 1;
                return (true, false, new AcquiredLock(this, true, true)); // ...with downgrade flag set!
            }
            else return (false, false, new AcquiredLock());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock LockReadOnly(int priority = DEFAULT_PRIORITY)
        {
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForReading();
                if (reentered) return acquiredLock;
            }

            int waitCycles = 0;
            ApplyWaitOrder(ref priority);

            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                priority = LockReadOnlyWaitingLoop(priority, out currentLockIndicator, out newLockIndicator, ref waitCycles);
            }
            UpdateAfterEnter(waitCycles, priority);

            if (reentranceSupported) reentranceIndicator.Value = FIRST_READ_ENTERED;

            return new AcquiredLock(this, false);
        }

        private int LockReadOnlyWaitingLoop(int priority, out int currentLockIndicator, out int newLockIndicator, ref int waitCycles)
        {
            bool nextInQueue = UpdatePriority(ref priority, ref waitCycles);

            if (!nextInQueue)
            {
                bool didReset = mre.Reset();
                if (lockIndicator != NO_LOCK)
                {
                    mre.Wait(sleepTime);
                }
                else if (didReset) mre.Set();
                if (priority > highestPriority) highestPriority = priority;
            }
            else Thread.Yield();

            currentLockIndicator = lockIndicator;
            newLockIndicator = currentLockIndicator + 1;
            return priority;
        }

        private (bool reentered, AcquiredLock acquiredLock) TryReenterForReading()
        {
            var currentReentranceIndicator = reentranceIndicator.Value;
            if (currentReentranceIndicator <= FIRST_WRITE_ENTERED)
            {
                // Already a writeLock in place in this flow, so reenter just reenter as a writeLock
                reentranceIndicator.Value = currentReentranceIndicator - 1;
                return (true, new AcquiredLock(this, true));
            }
            else if (currentReentranceIndicator >= FIRST_READ_ENTERED)
            {
                // Already a readlock in place in this flow, so reenter
                reentranceIndicator.Value = currentReentranceIndicator + 1;
                return (true, new AcquiredLock(this, false));
            }
            else return (false, new AcquiredLock());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockReadOnlyAsync(int priority = DEFAULT_PRIORITY)
        {
            if(TryLockReadOnly(out _, default, priority)) return readLockTask;
            else return LockForReadingAsync(priority);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private async Task<AcquiredLock> LockForReadingAsync(int priority = DEFAULT_PRIORITY)
        {
            int waitCycles = 0;
            ApplyWaitOrder(ref priority);

            var currentLockIndicator = lockIndicator;
            var newLockIndicator = currentLockIndicator + 1;
            while (ReaderMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, newLockIndicator, currentLockIndicator))
            {
                bool nextInQueue = UpdatePriority(ref priority, ref waitCycles);

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK) await mre.WaitAsync();
                    else if (didReset) mre.Set();
                    if (priority > highestPriority) highestPriority = priority;
                }
                else Thread.Yield();

                currentLockIndicator = lockIndicator;
                newLockIndicator = currentLockIndicator + 1;
            }
            UpdateAfterEnter(waitCycles, priority);
            if (reentranceSupported) reentranceIndicator.Value = FIRST_READ_ENTERED;

            return new AcquiredLock(this, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public AcquiredLock Lock(int priority = DEFAULT_PRIORITY)
        {
            var currentLockIndicator = lockIndicator;
            if (reentranceSupported && currentLockIndicator != NO_LOCK)
            {
                var (reentered, acquiredLock) = TryReenterForWriting();
                if (reentered) return acquiredLock;
            }

            int waitCycles = 0;
            ApplyWaitOrder(ref priority);

            while (WriterMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                currentLockIndicator = LockWaitingLoop(ref priority, ref waitCycles);
            }
            UpdateAfterEnter(waitCycles, priority);
            
            if (reentranceSupported) reentranceIndicator.Value = FIRST_WRITE_ENTERED;

            return new AcquiredLock(this, true);
        }

        

        private int LockWaitingLoop(ref int priority, ref int waitCycles)
        {
            int currentLockIndicator;
            bool nextInQueue = UpdatePriority(ref priority, ref waitCycles);

            if (!nextInQueue)
            {
                bool didReset = mre.Reset();
                if (lockIndicator != NO_LOCK)
                {
                    mre.Wait(sleepTime);
                }
                else if (didReset) mre.Set();
                if (priority > highestPriority) highestPriority = priority;
            }
            else Thread.Yield();

            currentLockIndicator = lockIndicator;
            return currentLockIndicator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyWaitOrder(ref int priority)
        {                
            priority -= numWaiting;
        }

        private bool UpdatePriority(ref int priority, ref int waitCycles)
        {
            if (waitCycles == 0)
            {
                numWaiting++;
            }
            waitCycles++;

            bool nextInQueue = false;
            if (priority > highestPriority) highestPriority = priority;
            if (priority >= highestPriority) nextInQueue = true;

            if (priority < INTERNAL_MAX_PRIORITY)
            {
                if (!nextInQueue || waitCycles % 1000 == 0) priority++;
            }
            return nextInQueue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateAfterEnter(int waitCycles, int priority)
        {
            if (highestPriority == INTERNAL_MIN_PRIORITY)
            {
                numWaiting = 0;
            }
            else
            {
                if (waitCycles != 0) numWaiting--;
                highestPriority = INTERNAL_MIN_PRIORITY;
            }            
        }

        private (bool reentered, AcquiredLock acquiredLock) TryReenterForWriting()
        {
            var currentReentranceIndicator = reentranceIndicator.Value;
            if (currentReentranceIndicator <= FIRST_WRITE_ENTERED)
            {
                reentranceIndicator.Value = currentReentranceIndicator - 1;
                return (true, new AcquiredLock(this, true));
            }
            else if (currentReentranceIndicator >= FIRST_READ_ENTERED)
            {
                // set flag that we are trying to upgrade... if anybody else already had set it: DEADLOCK, buhuuu!
                if (TRUE == Interlocked.CompareExchange(ref waitingForUpgrade, TRUE, FALSE)) throw new Exception("Deadlock! Two threads trying to upgrade a shared readLock in parallel!");
                // Waiting for upgrade to writeLock
                while (FIRST_READ_LOCK != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, FIRST_READ_LOCK))
                {
                    Thread.Yield();
                }
                waitingForUpgrade = FALSE;
                reentranceIndicator.Value = -currentReentranceIndicator - 1;
                return (true, new AcquiredLock(this, true, true)); // ...with downgrade flag set!
            }
            else return (false, new AcquiredLock());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<AcquiredLock> LockAsync(int priority = DEFAULT_PRIORITY)
        {
            if(TryLock(out _, default, priority)) return writeLockTask;
            else return LockForWritingAsync(priority);
        }

        private async Task<AcquiredLock> LockForWritingAsync(int priority = DEFAULT_PRIORITY)
        {
            int waitCycles = 0;
            ApplyWaitOrder(ref priority);

            var currentLockIndicator = lockIndicator;
            while (WriterMustWait(currentLockIndicator, priority) || currentLockIndicator != Interlocked.CompareExchange(ref lockIndicator, WRITE_LOCK, NO_LOCK))
            {
                bool nextInQueue = UpdatePriority(ref priority, ref waitCycles);

                if (!nextInQueue)
                {
                    bool didReset = mre.Reset();
                    if (lockIndicator != NO_LOCK)
                    {
                        await mre.WaitAsync(sleepTime);
                    }
                    else if (didReset) mre.Set();
                    if (priority > highestPriority) highestPriority = priority;
                }
                else Thread.Yield();

                currentLockIndicator = lockIndicator;
            }
            UpdateAfterEnter(waitCycles, priority);
            if (reentranceSupported) reentranceIndicator.Value = FIRST_WRITE_ENTERED;

            return new AcquiredLock(this, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ReaderMustWait(int currentLockIndicator, int priority)
        {
            return currentLockIndicator == WRITE_LOCK || priority < highestPriority || (reentranceSupported && waitingForUpgrade == TRUE);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool WriterMustWait(int currentLockIndicator, int priority)
        {
            return currentLockIndicator != NO_LOCK || priority < highestPriority;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitReadLock()
        {
            if (!reentranceSupported || !HandleReentranceForReadExit())
            {
                var newLockIndicator = Interlocked.Decrement(ref lockIndicator);
                if (NO_LOCK == newLockIndicator)
                {
                    mre.Set();
                }
            }
        }

        private bool HandleReentranceForReadExit()
        {
            var currentReentranceIndicator = reentranceIndicator.Value;
            currentReentranceIndicator--;

            if (currentReentranceIndicator == NOT_ENTERED)
            {
                reentranceIndicator.Value = NOT_ENTERED;
                return false; // go on, unlock...
            }
            else
            {
                reentranceIndicator.Value = currentReentranceIndicator;
                return true; // still a writelock in place
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExitWriteLock(bool downgrade)
        {
            if (!reentranceSupported || !HandleReentranceForWriteExit(downgrade))
            {
                lockIndicator = NO_LOCK;
                mre.Set();
            }
        }

        private bool HandleReentranceForWriteExit(bool downgrade)
        {
            bool done = false;
            var currentReentranceIndicator = reentranceIndicator.Value;
            currentReentranceIndicator++;
            if (currentReentranceIndicator == NOT_ENTERED)
            {
                reentranceIndicator.Value = NOT_ENTERED;
                done = false; // go on, unlock...
            }
            else if (downgrade)
            {
                reentranceIndicator.Value = -currentReentranceIndicator;
                lockIndicator = FIRST_READ_LOCK;
                done = true; // now it's a readlock again
            }
            else
            {
                reentranceIndicator.Value = currentReentranceIndicator;
                done = true; // still a writelock in place
            }

            return done;
        }

        public struct AcquiredLock : IDisposable
        {
            FeatureLock parentLock;
            readonly bool isWriteLock;
            readonly bool downgrade;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public AcquiredLock(FeatureLock parentLock, bool isWriteLock, bool downgrade = false)
            {
                this.parentLock = parentLock;
                this.isWriteLock = isWriteLock;
                this.downgrade = downgrade;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                Exit();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Exit()
            {
                if (isWriteLock) parentLock?.ExitWriteLock(downgrade);
                else parentLock?.ExitReadLock();
                parentLock = null;
            }
        }

    }
}
