using System;
using System.Threading;
using System.Threading.Tasks;

namespace AlienProject.WebWrapper
{

    public static class SdkTaskWaiter
    {
        public static async Task WaitAsync(Task task, CancellationToken cancellationToken)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            ObserveFailure(task);
            cancellationToken.ThrowIfCancellationRequested();
            if (!task.IsCompleted && cancellationToken.CanBeCanceled)
            {
                var cancelled = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
                {
                    await Task.WhenAny(task, cancelled.Task);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            await task;
        }

        public static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            await WaitAsync((Task)task, cancellationToken);
            return await task;
        }

        private static void ObserveFailure(Task task)
        {
            task.ContinueWith(
                completed => { var observed = completed.Exception; },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
