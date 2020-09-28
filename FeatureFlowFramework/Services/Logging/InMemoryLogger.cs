﻿using FeatureFlowFramework.DataFlows;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Collections;
using FeatureFlowFramework.Helpers.Synchronization;

namespace FeatureFlowFramework.Services.Logging
{
    public class InMemoryLogger : IDataFlowSink
    {       
        CountingRingBuffer<LogMessage> buffer;
        FastSpinLock bufferLock = new FastSpinLock();

        public InMemoryLogger(int bufferSize)
        {
            buffer = new CountingRingBuffer<LogMessage>(bufferSize);
        }

        public void Post<M>(in M message)
        {
            if(message is LogMessage logMessage) using(bufferLock.Lock()) buffer.Add(logMessage);
        }

        public Task PostAsync<M>(M message)
        {
            if(message is LogMessage logMessage) using(bufferLock.Lock()) buffer.Add(logMessage);
            return Task.CompletedTask;
        }

        public LogMessage[] GetAllLogMessages()
        {
            using(bufferLock.Lock())
            {
                return buffer.GetAvailableSince(0, out _);
            }
        }
    }
}
