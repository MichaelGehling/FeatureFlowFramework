﻿using FeatureLoom.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeatureLoom.Synchronization
{
    public static class TaskExtensions
    {
        public static SynchronizationContextRestorer Suspend(this SynchronizationContext context)
        {
            if (SynchronizationContext.Current == null) return new SynchronizationContextRestorer(null);
            else
            {
                SynchronizationContext.SetSynchronizationContext(null);
                return new SynchronizationContextRestorer(context);
            }
        }

        public readonly struct SynchronizationContextRestorer : IDisposable
        {
            private readonly SynchronizationContext context;

            public SynchronizationContextRestorer(SynchronizationContext context)
            {
                this.context = context;
            }

            public void Dispose()
            {
                if (SynchronizationContext.Current != context) SynchronizationContext.SetSynchronizationContext(context);
            }
        }

        public static void WaitFor(this Task task, bool unwrapExeption = true)
        {
            if (unwrapExeption) task.GetAwaiter().GetResult();
            else task.Wait();
        }

        public static T WaitFor<T>(this Task<T> task, bool unwrapExeption = true)
        {
            if (unwrapExeption) return task.GetAwaiter().GetResult();
            else return task.Result;
        }

        public static T WaitFor<T, OUT>(this Task<AsyncOut<T, OUT>> task, out OUT result, bool unwrapExeption = true)
        {
            if (unwrapExeption) return task.GetAwaiter().GetResult().Out(out result);
            else return task.Result.Out(out result);
        }

        public async static Task<bool> WaitAsync(this Task task)
        {
            if (task.IsCanceled || task.IsFaulted) return false;
            else if (task.IsCompleted) return true;

            await task.ConfigureAwait(false);

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }

        public async static Task<bool> WaitAsync(this Task task, TimeSpan timeout)
        {
            if (task.IsCanceled || task.IsFaulted) return false;
            else if (task.IsCompleted) return true;

            await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;

            /*
            var cts = new CancellationTokenSource();
            cts.CancelAfter(timeout);
            var cancellationToken = cts.Token;

            var tcs = new TaskCompletionSource<bool>();
            var registration = cancellationToken.Register(s =>
            {
                var source = (TaskCompletionSource<bool>)s;
                source.TrySetResult(false);
            }, tcs);

            _ = task.ContinueWith((t, s) =>
            {
                var tcsAndRegistration = ((TaskCompletionSource<bool> tcs, CancellationTokenRegistration ctr))s;

                if(t.IsFaulted && t.Exception != null)
                {
                    tcsAndRegistration.tcs.TrySetException(t.Exception.GetBaseException());
                }

                if(t.IsCanceled)
                {
                    tcsAndRegistration.tcs.TrySetCanceled();
                }

                if(t.IsCompleted)
                {
                    tcsAndRegistration.tcs.TrySetResult(true);
                }

                tcsAndRegistration.ctr.Dispose();
            },
            (tcs, registration),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

            await tcs.Task;

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
            */
        }

        public async static Task<bool> WaitAsync(this Task task, CancellationToken cancellationToken)
        {
            if (task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if (task.IsCompleted) return true;

            /*try
            {
                using(var cancelRegistration = cancellationToken.Register(() => throw new TaskCanceledException(task), true))
                {
                    await task;
                }
            }
            catch(TaskCanceledException)
            {
                return false;
            }

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
            */

            var tcs = new TaskCompletionSource<bool>();
            var registration = cancellationToken.Register(s =>
            {
                var source = (TaskCompletionSource<bool>)s;
                source.TrySetResult(false);
            }, tcs);

            _ = task.ContinueWith((t, s) =>
            {
                var tcsAndRegistration = ((TaskCompletionSource<bool> tcs, CancellationTokenRegistration ctr))s;

                if (t.IsFaulted && t.Exception != null)
                {
                    tcsAndRegistration.tcs.TrySetException(t.Exception.GetBaseException());
                }

                if (t.IsCanceled)
                {
                    tcsAndRegistration.tcs.TrySetCanceled();
                }

                if (t.IsCompleted)
                {
                    tcsAndRegistration.tcs.TrySetResult(true);
                }

                tcsAndRegistration.ctr.Dispose();
            },
            (tcs, registration),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

            await tcs.Task.ConfigureAwait(false);

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
        }

        public async static Task<bool> WaitAsync(this Task task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (task.IsCanceled || task.IsFaulted || cancellationToken.IsCancellationRequested) return false;
            else if (task.IsCompleted) return true;

            try
            {
                await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return false;
            }

            if (task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;

            /*
            var linkedCTS = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCTS.CancelAfter(timeout);
            var linkedToken = linkedCTS.Token;

            var tcs = new TaskCompletionSource<bool>();
            var registration = linkedToken.Register(s =>
            {
                var source = (TaskCompletionSource<bool>)s;
                source.TrySetResult(false);
            }, tcs);

            _ = task.ContinueWith((t, s) =>
            {
                var tcsAndRegistration = ((TaskCompletionSource<bool> tcs, CancellationTokenRegistration ctr))s;

                if(t.IsFaulted && t.Exception != null)
                {
                    tcsAndRegistration.tcs.TrySetException(t.Exception.GetBaseException());
                }

                if(t.IsCanceled)
                {
                    tcsAndRegistration.tcs.TrySetCanceled();
                }

                if(t.IsCompleted)
                {
                    tcsAndRegistration.tcs.TrySetResult(true);
                }

                tcsAndRegistration.ctr.Dispose();
            },
            (tcs, registration),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

            await tcs.Task;

            if(task.IsCanceled || task.IsFaulted || !task.IsCompleted) return false;
            else return true;
            */
        }
    }
}