﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.FastPathTest
{
    [MaxIterationCount(40)]
    [MemoryDiagnoser]
    [CsvMeasurementsExporter]
    [RPlotExporter]
    [HtmlExporter]
    //[SimpleJob(RuntimeMoniker.Net472, baseline: true)]
    //[SimpleJob(RuntimeMoniker.NetCoreApp30)]
    //[SimpleJob(RuntimeMoniker.Mono)]
    public class FastPath_CompareReentrantAsyncLock
    {
        FeatureLockSubjects featureLockSubjects = new FeatureLockSubjects();
        NeoSmartSubjects neoSmartSubjects = new NeoSmartSubjects();

        [Benchmark(Baseline = true)]
        public void ReentrantFeatureLock_LockAsync_() => featureLockSubjects.ReentrantLockAsync().WaitFor();

        [Benchmark]
        public void ReentrantNeoSmart_LockAsync_() => neoSmartSubjects.LockAsync().WaitFor();

    }
}
