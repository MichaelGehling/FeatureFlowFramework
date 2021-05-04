﻿using FeatureLoom.DataFlows;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Collections;
using FeatureLoom.Helpers.Synchronization;

namespace FeatureLoom.Services.Logging
{
    public class InMemoryLogger : IDataFlowSink
    {       
        CountingRingBuffer<LogMessage> buffer;
        MicroLock bufferLock = new MicroLock();

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
