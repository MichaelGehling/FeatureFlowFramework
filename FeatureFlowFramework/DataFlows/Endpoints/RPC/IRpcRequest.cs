﻿namespace FeatureLoom.DataFlows.RPC
{
    public interface IRpcRequest
    {
        long RequestId { get; }
        string Method { get; }
    }
}