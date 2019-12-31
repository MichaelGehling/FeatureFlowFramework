﻿using FeatureFlowFramework.Helper;
using Newtonsoft.Json;

namespace FeatureFlowFramework.DataFlows.RPC
{
    public struct RpcResponse<R> : IRpcResponse
    {
        public long requestId;
        public R result;

        public RpcResponse(long requestId, R result)
        {
            this.requestId = requestId;
            this.result = result;
        }
        [JsonIgnore]
        public long RequestId => requestId;
        [JsonIgnore]
        public R Result { get => result; }

        public string ResultToJson()
        {
            return result.ToJson();
        }
    }

}
