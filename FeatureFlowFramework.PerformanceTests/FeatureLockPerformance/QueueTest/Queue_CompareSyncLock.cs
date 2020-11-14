﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.QueueTest
{
    [MaxIterationCount(20)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    //[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    //[SimpleJob(RuntimeMoniker.Mono)]
    //[HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions)]
    public class Queue_CompareSyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        MonitorSubjects monitorSubjects = new MonitorSubjects();
        SemaphoreSlimSubjects semaphoreSlimSubjects = new SemaphoreSlimSubjects();
        ReaderWriterLockSlimSubjects readerWriterLockSlimSubjects = new ReaderWriterLockSlimSubjects();
        AsyncExSubjects asyncExSubjects = new AsyncExSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();
        FastSpinLockSubjects fastSpinLockSubjects = new FastSpinLockSubjects();
        SpinLockSubjects spinLockSubjects = new SpinLockSubjects();

        QueuePerformanceTest queueTest = new QueuePerformanceTest();

        [Params(1, 10)]
        public int numProducers
        {
            set => queueTest.numProducers = value;
        }

        [Params( 10)]
        public int numConsumers
        {
            set => queueTest.numConsumers = value;
        }

        [Params(100_000)]
        public int numMessages
        {
            set => queueTest.numOverallMessages = value;
        }

        [Benchmark(Baseline = true)]
        public void FeatureLock_Lock() => queueTest.Run(featureLockSubjects.Init, featureLockSubjects.Lock);

        //[Benchmark]
        public void FastSpinLock_Lock() => queueTest.Run(fastSpinLockSubjects.Init, fastSpinLockSubjects.Lock);

        [Benchmark]
        public void Monitor_Lock() => queueTest.Run(monitorSubjects.Init, monitorSubjects.Lock);

        //[Benchmark]
        public void SemaphoreSlim_Lock() => queueTest.Run(semaphoreSlimSubjects.Init, semaphoreSlimSubjects.Lock);

        //[Benchmark]
        public void ReaderWriterLockSlim_Lock() => queueTest.Run(readerWriterLockSlimSubjects.Init, readerWriterLockSlimSubjects.Lock);

        //[Benchmark]
        public void SpinLock_Lock() => queueTest.Run(spinLockSubjects.Init, spinLockSubjects.Lock);

        //[Benchmark]
        public void AsyncEx_Lock() => queueTest.Run(asyncExSubjects.Init, asyncExSubjects.Lock);

        //[Benchmark]
        public void NeoSmart_Lock() => queueTest.Run(neoSmartSubjects.Init, neoSmartSubjects.Lock);


    }
}
