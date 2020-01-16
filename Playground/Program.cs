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
            FunctionTestRWLock(2.Seconds(), 1, 2);
            FunctionTestSpinLock(2.Seconds(), 2, 2);
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
            int numWriteLocks = 2;


            List<DateTime> dummyList = new List<DateTime>();
            Random rnd = new Random();
            
            Action workWrite = () =>
            {
                //if(dummyList.Count > 100) dummyList.Clear();
                //dummyList.Add(AppTime.Now);
                //TimeFrame tf = new TimeFrame((0.1 * rnd.Next(0,20)).Milliseconds());
                //while(!tf.Elapsed) ;
                //Thread.Yield(); ;
            };
            Action workRead = () =>
            {
                //foreach(var d in dummyList) d.Add(1.Milliseconds());
                //TimeFrame tf = new TimeFrame((0.1 * rnd.Next(0, 20)).Milliseconds());
                //while(!tf.Elapsed) ;
                //Thread.Yield();
            };
            Action slack = () =>
            {
                /*TimeFrame tf = new TimeFrame(1.0.Milliseconds());
                while (!tf.Elapsed) ;*/
                //Thread.Sleep(1.Milliseconds());
                //Thread.Sleep(0);
            };

            name = "Overhead";
            Prepare(out gcs);
            c = RunParallel(new object(), duration, Overhead, numReadLocks, Overhead, numWriteLocks, workRead, workWrite, slack).Sum();
            double overhead = timeFactor / c;
            Console.WriteLine(overhead + " " + (-1) + " " + c + " " + name);

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

            name = "RWSpinLock";
            Prepare(out gcs);
            c = RunParallel(new RWSpinLock(), duration, RWSpinReadLock, numReadLocks, RWSpinWriteLock, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "RWLock";
            Prepare(out gcs);
            c = RunParallel(new RWLock(), duration, RWLockRead, numReadLocks, RWLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            
            name = "RWLock OnlySpinning";
            Prepare(out gcs);
            c = RunParallel(new RWLock(RWLock.SpinWaitBehaviour.OnlySpinning), duration, RWLockRead, numReadLocks, RWLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);
            
            name = "RWLock NoSpinning";
            Prepare(out gcs);
            c = RunParallel(new RWLock(RWLock.SpinWaitBehaviour.NoSpinning), duration, RWLockRead, numReadLocks, RWLockWrite, numWriteLocks, workRead, workWrite, slack).Sum();
            Finish(timeFactor, name, c, gcs, overhead);

            name = "SemaphoreSlim";
            Prepare(out gcs);
            c = RunParallel(new SemaphoreSlim(1,1), duration, SemaphoreLock, numReadLocks, SemaphoreLock, numWriteLocks, workRead, workWrite, slack).Sum();
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
            List<Task> tasks = new List<Task>();
            TaskCompletionSource<bool> starter = new TaskCompletionSource<bool>();
            for (int i= 0; i < numWriteLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = writeLock(lockObj, duration, workWrite, slack);
                    //Console.Write("W" + c + " ");
                    lock(counts) counts.Add(c);
                }));
            }

            for(int i = 0; i < numReadLockThreads; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    starter.Task.Wait();
                    var c = readLock(lockObj, duration, workRead, slack);
                    //Console.Write("R" + c + " ");
                    lock (counts) counts.Add(c);
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

            name = "RWSpinLock Read";
            Prepare(out gcs);
            c = RWSpinReadLock(new RWSpinLock(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWSpinLock Write";
            Prepare(out gcs);
            c = RWSpinWriteLock(new RWSpinLock(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);
            
            name = "RWLock Read";
            Prepare(out gcs);
            c = RWLockRead(new RWLock(), duration, work, slack);
            Finish(timeFactor, name, c, gcs, time_overhead_ns);

            name = "RWLock Write";
            Prepare(out gcs);
            c = RWLockWrite(new RWLock(), duration, work, slack);
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

        private static long RWSpinWriteLock(RWSpinLock myLock, TimeSpan duration, Action work, Action slack)
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

        private static long RWSpinReadLock(RWSpinLock myLock, TimeSpan duration, Action work, Action slack)
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

        private static void FunctionTestRWLock(TimeSpan duration, int numWriting, int numReading)
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

            var t1 = Task.Run(() => RunParallel(new RWLock(), duration, RWLockRead, numReading, RWLockWrite, numWriting, workRead, workWrite, null));
            t1.Wait();

            foreach(var c in t1.Result)
            {
                Console.WriteLine(c);
            }
        }

      
        private static void FunctionTestSpinLock(TimeSpan duration, int numWriting, int numReading)
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

            var t1 = Task.Run(() => RunParallel(new RWSpinLock(), duration, RWSpinReadLock, numReading, RWSpinWriteLock, numWriting, workRead, workWrite, null));
            t1.Wait();

            foreach(var c in t1.Result)
            {
                Console.WriteLine(c);
            }
        }
    }
}
