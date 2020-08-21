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
        FeatureLock bufferLock = new FeatureLock();

        public InMemoryLogger(int bufferSize)
        {
            buffer = new CountingRingBuffer<LogMessage>(bufferSize);
        }

        public void Post<M>(in M message)
        {
            using(bufferLock.Lock())
            {
                if(message is LogMessage logMessage) buffer.Add(logMessage);
            }
        }

        public async Task PostAsync<M>(M message)
        {
            using(await bufferLock.LockAsync())
            {
                if(message is LogMessage logMessage) buffer.Add(logMessage);
            }
        }

        public LogMessage[] GetAllLogMessages()
        {
            using(bufferLock.LockReadOnly())
            {
                return buffer.GetAvailableSince(0, out _);
            }
        }
    }
}
