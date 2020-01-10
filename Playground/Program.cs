﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FeatureFlowFramework.Helper;

namespace Playground
{
    class Program
    {
        static AsyncLock myLock = new AsyncLock();

        static void Main(string[] args)
        {
            FunctionTest(2.Seconds(), 0, 0, 2, 2);
            Console.WriteLine("----");
            PerformanceTest();
            Console.WriteLine("----");
            PerformanceTestParallel();

            Console.ReadKey();
        }
        private static void PerformanceTestParallel()
        {
            var duration = 3.Seconds();
            double timeFactor = duration.TotalMilliseconds * 1_000_000;
            string name;
            long c = 0;
            int gcs = 0;
            int numReadLocks = 0;
            int numWriteLocks = 4;
            Action work = async () =>
            {
                TimeFrame tf = new TimeFrame(0.01.Milliseconds());
                //while(!tf.Elapsed) ;
                await Task.Delay(0.01.Milliseconds());
            };
            Action slack = () =>
            {
                TimeFrame tf = new TimeFrame(0.01.Milliseconds());
                while (!tf.Elapsed) ;
            };

            name = "Overhead";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, Overhead, numReadLocks, Overhead, numWriteLocks, work, slack).Sum();
            double time_overhead_ns = timeFactor / c;
            Console.WriteLine(time_overhead_ns + " " + (-1) + " " + name);

            name = "ClassicLock";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, ClassicLock, numReadLocks, ClassicLock, numWriteLocks, work, slack).Sum();
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL SyncLock";
            Prepare(out gcs);
            c = RunParallel(new AsyncLock(), duration, ASLReadLock, numReadLocks, ASLWriteLock, numWriteLocks, work, slack).Sum();
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL AsyncLock";
            Prepare(out gcs);
            c = RunParallelAsync(new AsyncLock(), duration, ASLReadLockAsync, numReadLocks, ASLWriteLockAsync, numWriteLocks, work, slack).Sum();
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL SyncLock Extension";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, ASLReadLockExtension, numReadLocks, ASLWriteLockExtension, numWriteLocks, work, slack).Sum();
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "SemaphoreSlim";
            Prepare(out gcs);
            c = RunParallel(new SemaphoreSlim(1,1), duration, SemaphoreLock, numReadLocks, SemaphoreLock, numWriteLocks, work, slack).Sum();
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ReaderWriterLockSlim";
            Prepare(out gcs);
            c = RunParallel(new ReaderWriterLockSlim(), duration, RwLockRead, numReadLocks, RwLockWrite, numWriteLocks, work, slack).Sum();
            Finish(timeFactor, name, c, gcs, time_overhead_ns);
        }

        private static List<long> RunParallelAsync<T>(T lockObj, TimeSpan duration, Func<T, TimeSpan, Action, Action, Task<long>> readLock, int numReadLockThreads, Func<T, TimeSpan, Action, Action, Task<long>> writeLock, int numWriteLockThreads, Action work, Action slack)
        {
            return RunParallel(lockObj, duration, (a, b, c, d) => readLock(a, b, c, d).Result, numReadLockThreads, (a, b, c, d) => writeLock(a, b, c, d).Result, numWriteLockThreads, work, slack);
        }

        private static List<long> RunParallel<T>(T lockObj, TimeSpan duration, Func<T, TimeSpan, Action, Action, long> readLock, int numReadLockThreads, Func<T, TimeSpan, Action, Action, long> writeLock, int numWriteLockThreads, Action work, Action slack)
        {
            List<long> counts = new List<long>();
            List<Task> tasks = new List<Task>();
            TaskCompletionSource<bool> starter = new TaskCompletionSource<bool>();
            for (int i= 0; i < numWriteLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = writeLock(lockObj, duration, work, slack);
                    lock(counts) counts.Add(c);
                }));
            }

            for(int i = 0; i < numReadLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = readLock(lockObj, duration, work, slack);
                    lock(counts) counts.Add(c);
                }));
            }

            Thread.Sleep(100);
            starter.SetResult(true);
            Task.WhenAll(tasks.ToArray()).Wait();
            return counts;
        }


         private static void PerformanceTest()
         {
            var duration = 0.5.Seconds();
            double timeFactor = duration.TotalMilliseconds * 1_000_000;
            string name;
            long c = 0;
            int gcs = 0;
            Action work = null;
            Action slack = null;

            name = "Overhead";
            Prepare(out gcs);
            c = Overhead(new object(), duration, work, slack);
            double time_overhead_ns = timeFactor / c;
            Console.WriteLine(time_overhead_ns + " " + -1 + " " + name);

            name = "ASL ReadLock";
            Prepare(out gcs);
            c = ASLReadLock(new AsyncLock(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL WriteLock";
            Prepare(out gcs);
            c = ASLWriteLock(new AsyncLock(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);


            name = "ASL WriteLockAsync";
            Prepare(out gcs);
            c = ASLWriteLockAsync(new AsyncLock(), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL ReadLockAsync";
            Prepare(out gcs);
            c = ASLReadLockAsync(new AsyncLock(), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL WriteLock Extension";
            Prepare(out gcs);
            c = ASLWriteLockExtension(new AsyncLock(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "ASL WriteLock Async Extension";
            Prepare(out gcs);
            c = ASLWriteLockExtensionAsync(new AsyncLock(), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Classic Lock";
            Prepare(out gcs);
            c = ClassicLock(new object(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Monitor";
            Prepare(out gcs);
            c = Monitor(new object(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Mutex";
            Prepare(out gcs);
            c = Mutex(new Mutex(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "SpinLock";
            Prepare(out gcs);
            c = SpinLock(new SpinLock(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Semaphore Lock";
            Prepare(out gcs);
            c = SemaphoreLock(new SemaphoreSlim(1,1), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Semaphore Lock Async";
            Prepare(out gcs);
            c = SemaphoreLockAsync(new SemaphoreSlim(1, 1), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RwLock Write (no recursion)";
            Prepare(out gcs);
            c = RwLockWrite(new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RwLock Write (with recursion)";
            Prepare(out gcs);
            c = RwLockWrite(new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

        }

        private static void Finish(double timeFactor, string name, long c, int gcs, double time_overhead_ns)
        {
            double time = timeFactor / c - time_overhead_ns;
            gcs = (GC.CollectionCount(0) - gcs);
            long iterationsPerGC = gcs > 0 ? c / gcs : -1;
            Console.WriteLine(time + " " + iterationsPerGC + " " + name);
        }

        private static void Prepare(out int gcs)
        {
            gcs = GC.CollectionCount(0);
        }

        private static long RwLockWrite(ReaderWriterLockSlim rwLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                rwLock.EnterWriteLock();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    rwLock.ExitWriteLock();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static long RwLockRead(ReaderWriterLockSlim rwLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                rwLock.EnterReadLock();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    rwLock.ExitReadLock();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static long Mutex(Mutex mutex, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                mutex.WaitOne();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static long SpinLock(SpinLock spinLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                bool taken = false;
                try
                {
                    spinLock.Enter(ref taken);
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    if (taken) spinLock.Exit();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static async Task<long> SemaphoreLockAsync(SemaphoreSlim sema, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                await sema.WaitAsync();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    sema.Release();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static long SemaphoreLock(SemaphoreSlim sema, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                sema.Wait();
                try
                {
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    sema.Release();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static long Monitor(object obj, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                bool lockWasTaken = false;
                try
                {
                    System.Threading.Monitor.Enter(obj, ref lockWasTaken);
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    if(lockWasTaken)
                        System.Threading.Monitor.Exit(obj);
                }
                slack?.Invoke();
            }

            return c;
        }

        private static long ClassicLock(object obj, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                lock(obj)
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static async Task<long> ASLWriteLockExtensionAsync(object obj, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(await obj.LockForWritingAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        private static long ASLReadLockExtension(object obj, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(obj.LockForReading())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static long ASLWriteLockExtension(object obj, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(obj.LockForWriting())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static async Task<long> ASLReadLockAsync(AsyncLock myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(await myLock.ForReadingAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }

            return c;
        }

        private static async Task<long> ASLWriteLockAsync(AsyncLock myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(await myLock.ForWritingAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        private static long ASLWriteLock(AsyncLock myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(myLock.ForWriting(true))
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        private static long ASLReadLock(AsyncLock myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(myLock.ForReading(true))
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        private static long Overhead(object dummy, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                c++;
                work?.Invoke();
                slack?.Invoke();
            }
            return c;
        }

        private static void FunctionTest(TimeSpan duration, int numWriting, int numReading, int numWritingAsync, int numReadingAsync)
        {
            myLock = new AsyncLock();

            var t1 = Task.Run(() => RunParallel(myLock, duration, ASLReadLock, numReading, ASLWriteLock, numWriting, null, null));
            var t2 = Task.Run(() => RunParallelAsync(myLock, duration, ASLReadLockAsync, numReadingAsync, ASLWriteLockAsync, numWritingAsync, null, null));
            
            Task.WhenAll(t1, t2).Wait();

            foreach(var c in t1.Result)
            {
                Console.WriteLine(c);
            }
            foreach(var c in t2.Result)
            {
                Console.WriteLine(c);
            }
        }
    }
}
