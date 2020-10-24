﻿using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.PerformanceTests.FeatureLockPerformance.QueueTest
{

    public class QueuePerformanceTest
    {
        public int numProducers = 1;
        public int numConsumers = 1;
        public int numOverallMessages = 1_000_000;

        public void Run(Action init, Action<Action> producerLock, Action<Action> consumerLock = null)
        {
            init();

            if(consumerLock == null) consumerLock = producerLock;
            Queue<int> queue = new Queue<int>();
            AsyncManualResetEvent starter = new AsyncManualResetEvent(false);
            bool producersDone = false;
            int messagesPerProducer = numOverallMessages / numProducers;
            List<Task> producerTasks = new List<Task>();
            List<Task> consumerTasks = new List<Task>();
            for(int i=0; i < numProducers; i++)
            {
                producerTasks.Add(Task.Run(() =>
                {
                    starter.Wait();
                    int count = 0;
                    while(count < messagesPerProducer)
                    {
                        producerLock(() =>
                        {
                            queue.Enqueue(count++);
                        });
                    }
                    //var timer = AppTime.TimeKeeper;
                    //while(timer.Elapsed < 0.01.Milliseconds()) ;
                }));
            }
            for(int i = 0; i < numConsumers; i++)
            {
                consumerTasks.Add(Task.Run(() =>
                {
                    starter.Wait();                    
                    bool empty = false;
                    while(!empty || !producersDone)
                    {
                        consumerLock(() =>
                        {                            
                            if (!queue.TryDequeue(out _))
                            {
                                empty = true;
                            }
                        });
                        if(empty) Thread.Yield();
                        //var timer = AppTime.TimeKeeper;
                        //while(timer.Elapsed < 0.01.Milliseconds()) ;
                    }
                }));
            }
            starter.Set();
            if (!Task.WhenAll(producerTasks.ToArray()).Wait(10000)) Console.Write("! TIMEOUT !");
            producersDone = true;
            if(!Task.WhenAll(consumerTasks.ToArray()).Wait(10000)) Console.Write("! TIMEOUT !");
        }

        public void AsyncRun(Action init, Func<Action, Task> producerLock, Func<Action, Task> consumerLock = null)
        {
            init();
            if(consumerLock == null) consumerLock = producerLock;
            Queue<int> queue = new Queue<int>();
            AsyncManualResetEvent starter = new AsyncManualResetEvent();
            bool producersDone = false;
            int messagesPerProducer = numOverallMessages / numProducers;
            List<Task> producerTasks = new List<Task>();
            List<Task> consumerTasks = new List<Task>();
            for(int i = 0; i < numProducers; i++)
            {
                producerTasks.Add(new Func<Task>(async () =>
                {
                    await starter.WaitAsync();
                    int count = 0;
                    while(count < messagesPerProducer)
                    {
                        await producerLock(() =>
                        {
                            queue.Enqueue(count++);
                        });
                    }
                }).Invoke());
            }
            for(int i = 0; i < numConsumers; i++)
            {
                consumerTasks.Add(new Func<Task>(async () =>
                {
                    await starter.WaitAsync();
                    bool empty = false;
                    while(!empty || !producersDone)
                    {
                        await consumerLock(() =>
                        {
                            if(!queue.TryDequeue(out _))
                            {
                                empty = true;
                            }
                        });
                        if(empty) await Task.Yield();
                    }
                }).Invoke());
            }
            starter.Set();
            if(!Task.WhenAll(producerTasks.ToArray()).Wait(10000)) Console.Write("! TIMEOUT !");
            producersDone = true;
            if(!Task.WhenAll(consumerTasks.ToArray()).Wait(10000)) Console.Write("! TIMEOUT !");
        }
    }
}
