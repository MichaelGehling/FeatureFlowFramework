﻿using FeatureLoom.DataFlows;
using FeatureLoom.DataFlows.Test;
using FeatureLoom.Helpers;
using FeatureLoom.Helpers.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace FeatureLoom.DataFlows
{
    public class ConditionalTriggerTests
    {
        [Fact]
        public void IsOnlySetWhenReceivedValidMessage()
        {
            TestHelper.PrepareTestContext();

            MessageTrigger trigger = new ConditionalTrigger<int, int>(m => m >= 42, null);
            Assert.False(trigger.IsTriggered());
            trigger.Post(41);
            Assert.False(trigger.IsTriggered());
            trigger.Post(42);
            Assert.True(trigger.IsTriggered());
            Assert.True(trigger.IsTriggered(true));
            Assert.False(trigger.IsTriggered());
            trigger.Post(43);
            Assert.True(trigger.IsTriggered());
        }

        [Fact]
        public void IsResetWhenReceivedValidMessage()
        {
            TestHelper.PrepareTestContext();

            MessageTrigger trigger = new ConditionalTrigger<int, int>(m => m > 42, m => m < 42);
            Assert.False(trigger.IsTriggered());
            trigger.Post(43);
            Assert.True(trigger.IsTriggered());
            trigger.Post(42);
            Assert.True(trigger.IsTriggered());
            trigger.Post(41);
            Assert.False(trigger.IsTriggered());
            trigger.Post(42);
            Assert.False(trigger.IsTriggered());
        }
    }
}
