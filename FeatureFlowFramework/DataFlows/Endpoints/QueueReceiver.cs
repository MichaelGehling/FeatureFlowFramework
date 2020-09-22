﻿using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Helpers.Extensions;
using FeatureFlowFramework.Helpers.Synchronization;
using FeatureFlowFramework.Helpers.Time;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary>
    ///     An endpoint with a queue to receive messages asynchronously and process them in one or
    ///     multiple threads. It is thread-safe. When the maximum queue limit is exceeded, the
    ///     oldest elements are removed until the queue size is back to its limit. Optionally the
    ///     sender has to wait until a timeout exceeds or a consumer has dequeued an element.
    /// </summary>
    /// Uses a normal queue plus locking instead of a concurrent queue because of better performance
    /// in usual scenarios.
    /// <typeparam name="T"> The expected message type </typeparam>
    public class QueueReceiver<T> : IDataFlowQueue, IReceiver<T>, IAlternativeDataFlow, IAsyncWaitHandle, IDataFlowSink<T>
    {
        private Queue<T> queue = new Queue<T>();
        FeatureLock queueLock = new FeatureLock();

        public bool waitOnFullQueue = false;
        public TimeSpan timeoutOnFullQueue;
        public int maxQueueSize = int.MaxValue;
        public bool dropLatestMessageOnFullQueue = true;

        private AsyncManualResetEvent readerWakeEvent = new AsyncManualResetEvent(false);
        private AsyncManualResetEvent writerWakeEvent = new AsyncManualResetEvent(true);

        private LazySlim<DataFlowSourceHelper> alternativeSendingHelper;

        public QueueReceiver(int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default, bool dropLatestMessageOnFullQueue = true)
        {
            this.maxQueueSize = maxQueueSize;
            this.waitOnFullQueue = maxWaitOnFullQueue != default;
            this.timeoutOnFullQueue = maxWaitOnFullQueue;
            this.dropLatestMessageOnFullQueue = dropLatestMessageOnFullQueue;
        }

        public IDataFlowSource Else => alternativeSendingHelper.Obj;

        public bool IsEmpty => queue.Count == 0;
        public bool IsFull => queue.Count >= maxQueueSize;
        public int Count => queue.Count;
        public IAsyncWaitHandle WaitHandle => readerWakeEvent.AsyncWaitHandle;

        public void Post<M>(in M message)
        {
            if(message != null && message is T typedMessage)
            {
                if(waitOnFullQueue) writerWakeEvent.Wait(timeoutOnFullQueue);
                if(IsFull && dropLatestMessageOnFullQueue) alternativeSendingHelper.ObjIfExists?.Forward(message);
                else Enqueue(typedMessage);
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(message);
        }

        public async Task PostAsync<M>(M message)
        {
            if(message != null && message is T typedMessage)
            {
                if(waitOnFullQueue) await writerWakeEvent.WaitAsync(timeoutOnFullQueue);
                if(IsFull && dropLatestMessageOnFullQueue) await (alternativeSendingHelper.ObjIfExists?.ForwardAsync(message) ?? Task.CompletedTask);
                else Enqueue(typedMessage);
            }
            else await alternativeSendingHelper.ObjIfExists?.ForwardAsync(message);
        }

        private void Enqueue(T message)
        {
            using(queueLock.Lock())
            {
                queue.Enqueue(message);
                EnsureMaxSize();                
            }
            if(IsFull) writerWakeEvent.Reset();
            readerWakeEvent.Set();            
        }

        // ONLY USE IN LOCKED QUEUE!
        private void EnsureMaxSize()
        {
            while(queue.Count > maxQueueSize)
            {
                var element = queue.Dequeue();
                alternativeSendingHelper.ObjIfExists?.Forward(element);
            }
        }

        public bool TryReceive(out T message)
        {
            message = default;
            bool success = false;
            using (queueLock.Lock())
            {
                success = queue.TryDequeue(out message);                
            }
            if(IsEmpty) readerWakeEvent.Reset();
            if(!IsFull) writerWakeEvent.Set();
            return success;
        }

        public async Task<AsyncOut<bool, T>> TryReceiveAsync(TimeSpan timeout = default)
        {
            T message = default;
            bool success = false;

            if(IsEmpty && timeout != default) await WaitHandle.WaitAsync(timeout, CancellationToken.None);
            if(IsEmpty) return (false, default);
            using (await queueLock.LockAsync())
            {
                success = queue.TryDequeue(out message);                
            }
            if(IsEmpty) readerWakeEvent.Reset();
            if(!IsFull) writerWakeEvent.Set();
            return (success, message);
        }

        public T[] ReceiveAll()
        {
            if(IsEmpty)
            {
                return Array.Empty<T>();
            }

            T[] messages;

            using (queueLock.Lock())
            {
                messages = queue.ToArray();
                queue.Clear();                
            }
            if(IsEmpty) readerWakeEvent.Reset();
            if(!IsFull) writerWakeEvent.Set();
            return messages;
        }

        public bool TryPeek(out T nextItem)
        {
            nextItem = default;
            using (queueLock.Lock())
            {
                if(IsEmpty) return false;
                nextItem = queue.Peek();
                return true;
            }
        }

        public T[] PeekAll()
        {
            if(IsEmpty)
            {
                return new T[0];
            }

            T[] messages;

            using (queueLock.Lock())
            {
                messages = queue.ToArray();
            }
            return messages;
        }

        public void Clear()
        {
            using (queueLock.Lock())
            {
                queue.Clear();
            }
            if(IsEmpty) readerWakeEvent.Reset();
            if(!IsFull) writerWakeEvent.Set();
        }

        public int CountQueuedMessages => queue.Count;

        public Task WaitingTask => WaitHandle.WaitingTask;

        public object[] GetQueuedMesssages()
        {
            return Array.ConvertAll(queue.ToArray(), input => (object)input);
        }

        public Task<bool> WaitAsync()
        {
            return WaitHandle.WaitAsync();
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return WaitHandle.WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return WaitHandle.WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return WaitHandle.WaitAsync(timeout, cancellationToken);
        }

        public bool Wait()
        {
            return WaitHandle.Wait();
        }

        public bool Wait(TimeSpan timeout)
        {
            return WaitHandle.Wait(timeout);
        }

        public bool Wait(CancellationToken cancellationToken)
        {
            return WaitHandle.Wait(cancellationToken);
        }

        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return WaitHandle.Wait(timeout, cancellationToken);
        }

        public bool WouldWait()
        {
            return WaitHandle.WouldWait();
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            return WaitHandle.TryConvertToWaitHandle(out waitHandle);
        }
    }
}