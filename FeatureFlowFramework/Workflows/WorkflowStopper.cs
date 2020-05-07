﻿using FeatureFlowFramework.DataFlows;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureFlowFramework.Workflows
{
    public class WorkflowStopper
    {
        Workflow workflow;
        Predicate<Workflow.ExecutionInfo> predicate;
        ProcessingEndpoint<Workflow.ExecutionInfo> processor;

        public WorkflowStopper(Workflow workflow, Predicate<Workflow.ExecutionInfo> predicate, bool tryCancelWaitingState, bool deactivateWhenFired)
        {
            this.workflow = workflow;
            this.predicate = predicate;
            this.processor = new ProcessingEndpoint<Workflow.ExecutionInfo>(info =>
            {
                if(predicate(info))
                {
                    workflow.RequestPause(tryCancelWaitingState);
                    if(deactivateWhenFired) Deactivate();
                }
            });
            Activate();
        }

        public void Activate()
        {
            workflow.ExecutionInfoSource.ConnectTo(processor);
        }

        public void Deactivate()
        {
            workflow.ExecutionInfoSource.DisconnectFrom(processor);
        }
    }

}
