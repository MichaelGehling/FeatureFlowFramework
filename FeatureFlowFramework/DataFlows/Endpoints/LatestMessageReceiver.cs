﻿using FeatureFlowFramework.Helper;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureFlowFramework.DataFlows
{
    public class LatestMessageReceiver<T> : IDataFlowQueue, IReceiver<T>, IAlternativeDataFlow, IAsyncWaitHandle
    {
        private AsyncManualResetEvent readerWakeEvent = new AsyncManualResetEvent(false);
        private T receivedMessage;
        private LazySlim<DataFlowSourceHelper> alternativeSendingHelper;
        public IDataFlowSource Else => alternativeSendingHelper.Obj;
        public bool IsEmpty => !readerWakeEvent.IsSet;
        public bool IsFull => false;
        public int Count => IsEmpty ? 0 : 1;
        public IAsyncWaitHandle WaitHandle => readerWakeEvent.AsyncWaitHandle;

        public void Post<M>(in M message)
        {
            if(message is T typedMessage)
            {
                receivedMessage = typedMessage;
                readerWakeEvent.Set();
            }
            else alternativeSendingHelper.ObjIfExists?.Forward(message);
        }

        public Task PostAsync<M>(M message)
        {
            if(message is T typedMessage)
            {
                receivedMessage = typedMessage;
                readerWakeEvent.Set();
                return Task.CompletedTask;
            }
            else return alternativeSendingHelper.ObjIfExists?.ForwardAsync(message);
        }

        public bool TryReceive(out T message)
        {
            message = default;
            lock(readerWakeEvent)
            {
                if(IsEmpty) return false;
                message = receivedMessage;
                receivedMessage = default;
                readerWakeEvent.Reset();
                return true;
            }
        }

        public async Task<AsyncOutResult<bool, T>> TryReceiveAsync(TimeSpan timeout = default)
        {
            T message = default;
            if(IsEmpty && timeout != default) await WaitHandle.WaitAsync(timeout);
            lock(readerWakeEvent)
            {
                if(IsEmpty) return new AsyncOutResult<bool, T>(false, message);
                message = receivedMessage;
                receivedMessage = default;
                readerWakeEvent.Reset();
                return new AsyncOutResult<bool, T>(true, message);
            }
        }

        public T[] ReceiveAll()
        {
            lock(readerWakeEvent)
            {
                if(IsEmpty) return Array.Empty<T>();
                T message = receivedMessage;
                readerWakeEvent.Reset();
                return message.ToSingleEntryArray();
            }
        }

        public bool TryPeek(out T nextItem)
        {
            nextItem = default;
            lock(readerWakeEvent)
            {
                if(IsEmpty) return false;
                nextItem = receivedMessage;
                return true;
            }
        }

        public T[] PeekAll()
        {
            lock(readerWakeEvent)
            {
                if(IsEmpty) return Array.Empty<T>();
                T message = receivedMessage;
                return message.ToSingleEntryArray();
            }
        }

        public void Clear()
        {
            lock(readerWakeEvent)
            {
                readerWakeEvent.Reset();
            }
        }

        public object[] GetQueuedMesssages()
        {
            lock(readerWakeEvent)
            {
                if(IsEmpty) return Array.Empty<object>();
                T message = receivedMessage;
                return message.ToSingleEntryArray<object>();
            }
        }

        public Task<bool> WaitAsync()
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync();
        }

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WaitAsync(timeout, cancellationToken);
        }

        public bool Wait()
        {
            return ((IAsyncWaitHandle)readerWakeEvent).Wait();
        }

        public bool Wait(TimeSpan timeout)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).Wait(timeout);
        }

        public bool Wait(CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).Wait(cancellationToken);
        }

        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).Wait(timeout, cancellationToken);
        }

        public bool WouldWait()
        {
            return ((IAsyncWaitHandle)readerWakeEvent).WouldWait();
        }

        public bool TryConvertToWaitHandle(out WaitHandle waitHandle)
        {
            return ((IAsyncWaitHandle)readerWakeEvent).TryConvertToWaitHandle(out waitHandle);
        }

        public int CountQueuedMessages => Count;

        public Task WaitingTask => ((IAsyncWaitHandle)readerWakeEvent).WaitingTask;
    }
}