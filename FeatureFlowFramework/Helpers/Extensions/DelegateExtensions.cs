﻿using FeatureFlowFramework.Services.Logging;
using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FeatureFlowFramework.Helpers.Extensions
{
    public static class DelegateExtensions
    {
        public static Action<T> WrapInTryCatchIfAsync<T>(this Action<T> action, bool logOnException = true)
        {
            Action<T> result = action;
            if(action.Method.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null)
            {
                result = t =>
                {
                    try
                    {
                        action(t);
                    }
                    catch(Exception e)
                    {
                        if(logOnException) Log.ERROR(null, $"Async function failed with an exception that was caught! ", e.ToString());
                    }
                };
            }
            return result;
        }
    }
}