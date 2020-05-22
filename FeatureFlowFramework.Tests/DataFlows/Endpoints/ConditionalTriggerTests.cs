﻿using FeatureFlowFramework.DataFlows;
using FeatureFlowFramework.DataFlows.Test;
using FeatureFlowFramework.Helpers;
using FeatureFlowFramework.Helpers.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace FeatureFlowFramework.DataFlows
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
