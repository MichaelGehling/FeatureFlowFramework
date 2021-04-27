﻿using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Misc;
using FeatureFlowFramework.Services.Logging;
using FeatureFlowFramework.Services.MetaData;
using FeatureFlowFramework.Services.Serialization;
using System;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public partial class RpcCallee
    {
        private class RpcRequestHandler<P1, R> : IRpcRequestHandler
        {
            private Func<P1, R> method;
            private readonly string name;
            private ISender target;

            public RpcRequestHandler(string name, Func<P1, R> method)
            {
                this.method = method;
                this.name = name;
            }

            public void SetTarget(ISender target)
            {
                this.target = target;
            }

            public bool Handle<M>(in M message)
            {
                if(message is RpcRequest<P1, R> myRequest && myRequest.method == this.name)
                {
                    HandleRpcRequest(myRequest);
                    return true;
                }
                else if(message is string stringMessage)
                {
                    if(stringMessage.TryParseJson(out RpcRequest<P1, R> rpcRequest) && rpcRequest.method == this.name)
                    {
                        HandleRpcRequest(rpcRequest);
                        return true;
                    }
                }
                return false;
            }

            private void HandleRpcRequest(RpcRequest<P1, R> myRequest)
            {
                R result = default;
                try
                {
                    result = method.Invoke(myRequest.parameterSet);
                    if(!myRequest.noResponse)
                    {
                        var response = new RpcResponse<R>(myRequest.requestId, result);
                        target.Send(response);
                    }
                }
                catch(Exception e)
                {
                    if(myRequest.noResponse)
                    {
                        Log.ERROR(this.GetHandle(), $"Failed executing RPC call {myRequest.method}", e.ToString());
                    }
                    else
                    {
                        string errorMessage = e.ToString();
                        var response = new RpcErrorResponse(myRequest.requestId, errorMessage);
                        target.Send(response);
                    }
                }
            }
        }
    }
}