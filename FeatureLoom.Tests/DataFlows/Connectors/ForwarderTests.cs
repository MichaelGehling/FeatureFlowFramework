﻿using FeatureLoom.DataFlows.Test;
using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Diagnostics;
using Xunit;

namespace FeatureLoom.DataFlows
{
    public class ForwarderTests
    {
        [Theory]
        [InlineData(42)]
        [InlineData("test string")]
        public void CanForwardObjectsAndValues<T>(T message)
        {
            TestHelper.PrepareTestContext();

            var sender = new Sender<T>();
            var forwarder = new Forwarder();
            var sink = new SingleMessageTestSink<T>();
            sender.ConnectTo(forwarder).ConnectTo(sink);
            sender.Send(message);
            Assert.True(sink.received);
            Assert.Equal(message, sink.receivedMessage);
        }
    }
}