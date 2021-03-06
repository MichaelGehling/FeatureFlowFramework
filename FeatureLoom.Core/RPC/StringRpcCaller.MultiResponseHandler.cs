﻿using FeatureLoom.MessageFlow;
using FeatureLoom.Extensions;
using FeatureLoom.Time;
using System;

namespace FeatureLoom.RPC
{
    public partial class StringRpcCaller
    {
        private class MultiResponseHandler : IResponseHandler
        {
            private readonly long requestId;
            private readonly IMessageSink sink;
            public readonly TimeFrame lifeTime;

            public TimeFrame LifeTime => lifeTime;

            public MultiResponseHandler(long requestId, IMessageSink sink, TimeSpan timeout)
            {
                this.sink = sink;
                lifeTime = new TimeFrame(timeout);
                this.requestId = requestId;
            }

            public bool Handle<M>(in M message)
            {
                if (message is IRpcResponse myResponse && myResponse.RequestId == this.requestId)
                {
                    sink.Post(myResponse.ResultToJson().Trim('"'.ToSingleEntryArray()));
                }

                return false;
            }

            public void Cancel()
            {
                // do nothing
            }
        }
    }
}