﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FeatureFlowFramework.Helper;

namespace Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            //FunctionTestRWLock(new RWLock(RWLock.SpinWaitBehaviour.NoSpinning), 3.Seconds(), 4, 4, 0, 0);
            Console.WriteLine("--2,2,2,2--");
            for (int i= 0; i< 5; i++) FunctionTestRWLock(new RWLock3(0), 1.Seconds(), 2, 2, 2, 2);
            /*Console.WriteLine("--0,0,1,1--");
            for(int i = 0; i < 5; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 1, 1);
            Console.WriteLine("--0,0,0,4--");
            for(int i = 0; i < 5; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 0, 4);
            Console.WriteLine("--0,0,4,0--");
            for(int i = 0; i < 5; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 4, 0);
            Console.WriteLine("--0,0,3,1--");
            for(int i = 0; i < 5; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 3, 1);
            Console.WriteLine("--0,0,1,2--");
            for(int i = 0; i < 15; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 1, 2);
            Console.WriteLine("--0,0,2,2--");
            for(int i = 0; i < 15; i++) FunctionTestRWLock(new RWLock3(RWLock3.SpinWaitBehaviour.NoSpinning), 1.Seconds(), 0, 0, 2, 2);*/
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
            int numReadLocks = 3;
            int numWriteLocks = 3;

            List<DateTime> dummyList = new List<DateTime>();
            Random rnd = new Random();

            Action workWrite = () =>
            {
                //if(dummyList.Count > 1000) dummyList.Clear();
                //dummyList.Add(AppTime.Now);
                TimeFrame tf = new TimeFrame(0.1.Milliseconds());
                while(!tf.Elapsed) ;
                //Thread.Sleep(1);
                Thread.Yield();
            };
            Action workRead = () =>
            {
                //DateTime x = AppTime.Now;
                //TimeSpan y;
                //foreach (var d in dummyList) y = x.Subtract(d);
                TimeFrame tf = new TimeFrame(0.1.Milliseconds());
                while(!tf.Elapsed) ;
                //Thread.Sleep(1);
                Thread.Yield();
            };
            Action slack = () =>
            {
                /*TimeFrame tf = new TimeFrame(1.0.Milliseconds());
                while (!tf.Elapsed) ;*/
                //Thread.Sleep(1.Milliseconds());
                //Thread.Sleep(1);
                TimeFrame tf = new TimeFrame(0.2.Milliseconds());
                while(!tf.Elapsed) ;
                Thread.Yield();
            };

            name = "Overhead";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, Overhead, numReadLocks, Overhead, numWriteLocks, workRead, workWrite, slack).Sum();
            double overhead = timeFactor / c;
            Console.WriteLine(overhead + " " + (-1) + " " + c + " " + name);

           /* name = "RWLock";
            Prepare(out gcs);
            c = RunParallel(new RWLock(), duration, RWLockRead, numReadLocks, RWLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock Async";
            Prepare(out gcs);
            c = RunParallelAsync(new RWLock(), duration, RWLockReadAsync, numReadLocks, RWLockWriteAsync, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock NoSpinning";
            Prepare(out gcs);
            c = RunParallel(new RWLock(RWLock.SpinWaitBehaviour.NoSpinning), duration, RWLockRead, numReadLocks, RWLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock NoSpinning Async";
            Prepare(out gcs);
            c = RunParallelAsync(new RWLock(RWLock.SpinWaitBehaviour.NoSpinning), duration, RWLockReadAsync, numReadLocks, RWLockWriteAsync, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock OnlySpinning";
            Prepare(out gcs);
            c = RunParallel(new RWLock(RWLock.SpinWaitBehaviour.OnlySpinning), duration, RWLockRead, numReadLocks, RWLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);


            name = "RWLock2";
            Prepare(out gcs);
            c = RunParallel(new RWLock2(), duration, RWLockRead2, numReadLocks, RWLockWrite2, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock2 Async";
            Prepare(out gcs);
            c = RunParallelAsync(new RWLock2(), duration, RWLockReadAsync2, numReadLocks, RWLockWriteAsync2, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock2 NoSpinning";
            Prepare(out gcs);
            c = RunParallel(new RWLock2(RWLock2.SpinWaitBehaviour.NoSpinning), duration, RWLockRead2, numReadLocks, RWLockWrite2, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock2 NoSpinning Async";
            Prepare(out gcs);
            c = RunParallelAsync(new RWLock2(RWLock2.SpinWaitBehaviour.NoSpinning), duration, RWLockReadAsync2, numReadLocks, RWLockWriteAsync2, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock2 OnlySpinning";
            Prepare(out gcs);
            c = RunParallel(new RWLock2(RWLock2.SpinWaitBehaviour.OnlySpinning), duration, RWLockRead2, numReadLocks, RWLockWrite2, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            */

            name = "RWLock3";
            Prepare(out gcs);
            c = RunParallel(new RWLock3(), duration, RWLockRead3, numReadLocks, RWLockWrite3, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock3 Async";
            Prepare(out gcs);
            c = RunParallelAsync(new RWLock3(), duration, RWLockReadAsync3, numReadLocks, RWLockWriteAsync3, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock3 NoSpinning";
            Prepare(out gcs);
            c = RunParallel(new RWLock3(0), duration, RWLockRead3, numReadLocks, RWLockWrite3, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock3 NoSpinning Async";
            Prepare(out gcs);
            c = RunParallelAsync(new RWLock3(0), duration, RWLockReadAsync3, numReadLocks, RWLockWriteAsync3, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock3 OnlySpinning";
            Prepare(out gcs);
            c = RunParallel(new RWLock3(int.MaxValue), duration, RWLockRead3, numReadLocks, RWLockWrite3, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);


            name = "ClassicLock";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, ClassicLock, numReadLocks, ClassicLock, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            /*
            name = "SpinLock";
            Prepare(out gcs);
            c = RunParallel(new SpinLock(), duration, SpinLock, numReadLocks, SpinLock, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            */

            name = "SemaphoreSlim";
            Prepare(out gcs);
            c = RunParallel(new SemaphoreSlim(1,1), duration, SemaphoreLock, numReadLocks, SemaphoreLock, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "SemaphoreSlim Async";
            Prepare(out gcs);
            c = RunParallelAsync(new SemaphoreSlim(1, 1), duration, SemaphoreLockAsync, numReadLocks, SemaphoreLockAsync, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "ReaderWriterLockSlim";
            Prepare(out gcs);
            c = RunParallel(new ReaderWriterLockSlim(), duration, ReaderWriterLockRead, numReadLocks, ReaderWriterLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
        }

        private static List<long> RunParallelAsync<T>(T lockObj, TimeSpan duration, Func<T, TimeSpan, Action, Action, Task<long>> readLock, int numReadLockThreads, Func<T, TimeSpan, Action, Action, Task<long>> writeLock, int numWriteLockThreads, Action workRead, Action workWrite, Action slack)
        {
            return RunParallel(lockObj, duration, (a, b, c, d) => readLock(a, b, c, d).Result, numReadLockThreads, (a, b, c, d) => writeLock(a, b, c, d).Result, numWriteLockThreads, workRead, workWrite, slack);
        }

        private static List<long> RunParallel<T>(T lockObj, TimeSpan duration, Func<T, TimeSpan, Action, Action, long> readLock, int numReadLockThreads, Func<T, TimeSpan, Action, Action, long> writeLock, int numWriteLockThreads, Action workRead, Action workWrite, Action slack)
        {
            List<long> counts = new List<long>();
            List<long> countsW = new List<long>();
            List<long> countsR = new List<long>();
            List<Task> tasks = new List<Task>();
            TaskCompletionSource<bool> starter = new TaskCompletionSource<bool>();
            for (int i= 0; i < numWriteLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = writeLock(lockObj, duration, workWrite, slack);
                    Console.WriteLine("W" + c);
                    lock(counts) counts.Add(c);
                    lock (countsW) countsW.Add(c);
                }));
            }

            for(int i = 0; i < numReadLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = readLock(lockObj, duration, workRead, slack);
                    Console.WriteLine("R" + c);
                    lock (counts) counts.Add(c);
                    lock (countsR) countsR.Add(c);
                }));
            }

            Thread.Sleep(100);
            starter.SetResult(true);
            Task.WhenAll(tasks.ToArray()).Wait();
            //Console.WriteLine("W*R " + (countsR.Min()* countsR.Max() * countsW.Min()*countsW.Max()) / (counts.Sum() * counts.Sum()));
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
            
            name = "RWLock Read";
            Prepare(out gcs);
            c = RWLockRead(new RWLock(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock Write";
            Prepare(out gcs);
            c = RWLockWrite(new RWLock(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock Read Async";
            Prepare(out gcs);
            c = RWLockReadAsync(new RWLock(), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock Write Async";
            Prepare(out gcs);
            c = RWLockWriteAsync(new RWLock(), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock2 Read";
            Prepare(out gcs);
            c = RWLockRead2(new RWLock2(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock2 Write";
            Prepare(out gcs);
            c = RWLockWrite2(new RWLock2(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock2 Read Async";
            Prepare(out gcs);
            c = RWLockReadAsync2(new RWLock2(), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock2 Write Async";
            Prepare(out gcs);
            c = RWLockWriteAsync2(new RWLock2(), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock3 Read";
            Prepare(out gcs);
            c = RWLockRead3(new RWLock3(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock3 Write";
            Prepare(out gcs);
            c = RWLockWrite3(new RWLock3(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock3 Read Async";
            Prepare(out gcs);
            c = RWLockReadAsync3(new RWLock3(), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock3 Write Async";
            Prepare(out gcs);
            c = RWLockWriteAsync3(new RWLock3(), duration, work, slack).Result;
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Classic Lock";
            Prepare(out gcs);
            c = ClassicLock(new object(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "Monitor";
            Prepare(out gcs);
            c = Monitor(new object(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            /*name = "Mutex";
            Prepare(out gcs);
            c = Mutex(new Mutex(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);
            */
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
            c = ReaderWriterLockWrite(new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RwLock Write (with recursion)";
            Prepare(out gcs);
            c = ReaderWriterLockWrite(new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

        }

        private static void Finish(double timeFactor, string name, long c, int gcs, double time_overhead_ns)
        {
            double time = timeFactor / c - time_overhead_ns;
            gcs = (GC.CollectionCount(0) - gcs);
            long iterationsPerGC = gcs > 0 ? c / gcs : -1;
            Console.WriteLine(time + " " + iterationsPerGC + " " + c +" " + name);
        }

        private static void Prepare(out int gcs)
        {
            gcs = GC.CollectionCount(0);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long ReaderWriterLockWrite(ReaderWriterLockSlim rwLock, TimeSpan duration, Action work, Action slack)
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long ReaderWriterLockRead(ReaderWriterLockSlim rwLock, TimeSpan duration, Action work, Action slack)
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long SpinLock(SpinLock spinLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                bool spinLockTaken = false;
                try
                {
                    spinLock.Enter(ref spinLockTaken);
                    if(spinLockTaken)
                    {
                        c++;
                        work?.Invoke();
                    }
                }
                finally
                {
                    if (spinLockTaken) spinLock.Exit();
                }
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long Monitor(object obj, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                bool monitorLockTaken = false;
                try
                {
                    System.Threading.Monitor.Enter(obj, ref monitorLockTaken);
                    c++;
                    work?.Invoke();
                }
                finally
                {
                    if(monitorLockTaken)
                        System.Threading.Monitor.Exit(obj);
                }
                slack?.Invoke();
            }

            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long RWLockWrite(RWLock myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(myLock.ForWriting())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long RWLockRead(RWLock myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while(!tf.Elapsed)
            {
                using(myLock.ForReading())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async static Task<long> RWLockWriteAsync(RWLock myLock, TimeSpan duration, Action work, Action slack)
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async static Task<long> RWLockReadAsync(RWLock myLock, TimeSpan duration, Action work, Action slack)
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

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long RWLockWrite2(RWLock2 myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while (!tf.Elapsed)
            {
                using (myLock.ForWriting())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long RWLockRead2(RWLock2 myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while (!tf.Elapsed)
            {
                using (myLock.ForReading())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async static Task<long> RWLockWriteAsync2(RWLock2 myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while (!tf.Elapsed)
            {
                using (await myLock.ForWritingAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async static Task<long> RWLockReadAsync2(RWLock2 myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while (!tf.Elapsed)
            {
                using (await myLock.ForReadingAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long RWLockWrite3(RWLock3 myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while (!tf.Elapsed)
            {
                using (myLock.ForWriting())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private static long RWLockRead3(RWLock3 myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while (!tf.Elapsed)
            {
                using (myLock.ForReading())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async static Task<long> RWLockWriteAsync3(RWLock3 myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while (!tf.Elapsed)
            {
                using (await myLock.ForWritingAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        private async static Task<long> RWLockReadAsync3(RWLock3 myLock, TimeSpan duration, Action work, Action slack)
        {
            long c = 0;
            TimeFrame tf = new TimeFrame(duration);
            while (!tf.Elapsed)
            {
                using (await myLock.ForReadingAsync())
                {
                    c++;
                    work?.Invoke();
                }
                slack?.Invoke();
            }
            return c;
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
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

        private static void FunctionTestRWLock(RWLock myLock, TimeSpan duration, int numReading, int numWriting, int numReadingAsync, int numWritingAsync)
        {
            List<DateTime> dummyList = new List<DateTime>();
            Action workWrite = () =>
            {
                if (dummyList.Count > 100) dummyList.Clear();
                dummyList.Add(AppTime.Now);
            };
            Action workRead = () =>
            {
                foreach (var d in dummyList) d.Add(1.Milliseconds());
            };

            var t1 = Task.Run(() => RunParallel(myLock, duration, RWLockRead, numReading, RWLockWrite, numWriting, workRead, workWrite, null));
            var t2 = Task.Run(() => RunParallelAsync(myLock, duration, RWLockReadAsync, numReadingAsync, RWLockWriteAsync, numWritingAsync, workRead, workWrite, null));
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

        private static void FunctionTestRWLock(RWLock3 myLock, TimeSpan duration, int numReading, int numWriting, int numReadingAsync, int numWritingAsync)
        {
            List<DateTime> dummyList = new List<DateTime>();
            Action workWrite = () =>
            {
                if (dummyList.Count > 100) dummyList.Clear();
                dummyList.Add(AppTime.Now);
            };
            Action workRead = () =>
            {
                foreach (var d in dummyList) d.Add(1.Milliseconds());
            };

            var t1 = Task.Run(() => RunParallel(myLock, duration, RWLockRead3, numReading, RWLockWrite3, numWriting, workRead, workWrite, null));
            var t2 = Task.Run(() => RunParallelAsync(myLock, duration, RWLockReadAsync3, numReadingAsync, RWLockWriteAsync3, numWritingAsync, workRead, workWrite, null));
            Task.WhenAll(t1, t2).Wait();

            foreach (var c in t1.Result)
            {
                Console.WriteLine(c);
            }
            foreach (var c in t2.Result)
            {
                Console.WriteLine(c);
            }
        }

    }
}
