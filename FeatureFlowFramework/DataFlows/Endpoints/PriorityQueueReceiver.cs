﻿using FeatureFlowFramework.Helper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    /// <summary>
    ///     An endpoint with a queue to receive messages asynchronously ordered by priority and
    ///     process them in one or multiple threads. It is thread-safe. When the maximum queue limit
    ///     is exceeded, the elements with lowest priority are removed until the queue size is back
    ///     to its limit. Optionally the sender has to wait until a timeout exceeds or a consumer
    ///     has dequeued an element.
    /// </summary>
    /// Uses a normal queue plus locking instead of a concurrent queue because of better performance
    /// in usual scenarios.
    /// <typeparam name="T"> The expected message type </typeparam>
    public class PriorityQueueReceiver<T> : IDataFlowQueue, IReceiver<T>, IAsyncWaitHandle
    {
        private PriorityQueue<T> queue;

        public bool waitOnFullQueue = false;
        public TimeSpan timeoutOnFullQueue;
        public int maxQueueSize = int.MaxValue;

        private AsyncManualResetEvent readerWakeEvent = new AsyncManualResetEvent(false);
        private AsyncManualResetEvent writerWakeEvent = new AsyncManualResetEvent(true);

        private LazySlim<DataFlowSourceHelper> alternativeSendingHelper;

        public PriorityQueueReceiver(Comparer<T> priorityComparer, int maxQueueSize = int.MaxValue, TimeSpan maxWaitOnFullQueue = default)
        {
            this.queue = new PriorityQueue<T>(priorityComparer);
            this.maxQueueSize = maxQueueSize;
            this.waitOnFullQueue = maxWaitOnFullQueue != default;
            this.timeoutOnFullQueue = maxWaitOnFullQueue;
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
                Enqueue(typedMessage);
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(message);
        }

        public async Task PostAsync<M>(M message)
        {
            if(message != null && message is T typedMessage)
            {
                if(waitOnFullQueue) await writerWakeEvent.WaitAsync(timeoutOnFullQueue);
                Enqueue(typedMessage);
            }
            else await alternativeSendingHelper.ObjIfExists?.ForwardAsync(message);
        }

        private void Enqueue(T message)
        {
            lock(queue)
            {
                queue.Enqueue(message);
                EnsureMaxSize();
                readerWakeEvent.Set();
                if(IsFull) writerWakeEvent.Reset();
            }
        }

        // ONLY USE IN LOCKED QUEUE!
        private void EnsureMaxSize()
        {
            while(queue.Count > maxQueueSize)
            {
                var element = queue.Dequeue(false);
                alternativeSendingHelper.ObjIfExists?.Forward(element);
            }
        }

        public bool TryReceive(out T message)
        {
            message = default;
            bool success = false;

            if(IsEmpty) return false;
            lock(queue)
            {
                success = queue.TryDequeue(out message);
                if(IsEmpty) readerWakeEvent.Reset();
                if(!IsFull) writerWakeEvent.Set();
            }
            return success;
        }

        public async Task<AsyncOutResult<bool, T>> TryReceiveAsync(TimeSpan timeout = default)
        {
            T message = default;
            bool success = false;

            if(IsEmpty && timeout != default) await WaitHandle.WaitAsync(timeout, CancellationToken.None);
            if(IsEmpty) return new AsyncOutResult<bool, T>(false, default);
            lock(queue)
            {
                success = queue.TryDequeue(out message);
                if(IsEmpty) readerWakeEvent.Reset();
                if(!IsFull) writerWakeEvent.Set();
            }
            return new AsyncOutResult<bool, T>(success, message);
        }

        public T[] ReceiveAll()
        {
            if(IsEmpty)
            {
                return Array.Empty<T>();
            }

            T[] messages;

            lock(queue)
            {
                messages = queue.ToArray();
                queue.Clear();

                if(IsEmpty) readerWakeEvent.Reset();
                if(!IsFull) writerWakeEvent.Set();
            }
            return messages;
        }

        public bool TryPeek(out T nextItem)
        {
            nextItem = default;
            lock(queue)
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

            lock(queue)
            {
                messages = queue.ToArray();
            }
            return messages;
        }

        public void Clear()
        {
            lock(queue)
            {
                queue.Clear();
            }
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