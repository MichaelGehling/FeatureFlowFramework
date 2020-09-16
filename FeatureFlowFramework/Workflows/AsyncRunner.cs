﻿using System;
using System.Threading.Tasks;

namespace FeatureFlowFramework.Workflows
{
    public class AsyncRunner : AbstractRunner
    {
        public async Task RunAsync(Workflow workflow)
        {
            AddToRunningWorkflows(workflow);
            try
            {
                while(await workflow.ExecuteNextStepAsync(executionController)) ;
            }
            finally
            {
                RemoveFromRunningWorkflows(workflow);
            }
        }

        public override void Run(Workflow workflow)
        {
            _ = RunAsync(workflow);
        }
    }
}