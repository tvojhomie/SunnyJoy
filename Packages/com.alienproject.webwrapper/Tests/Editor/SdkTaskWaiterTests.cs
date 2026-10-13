using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace AlienProject.WebWrapper.Tests
{
    public sealed class SdkTaskWaiterTests
    {
        [Test]
        public async Task CompletedSdkTask_ReturnsValue()
        {
            var result = await SdkTaskWaiter.WaitAsync(Task.FromResult("token"), CancellationToken.None);
            Assert.That(result, Is.EqualTo("token"));
        }

        [Test]
        public async Task HungSdkTask_CallerCancellationEndsWaitWithoutCancellingNativeTask()
        {
            var nativeOperation = new TaskCompletionSource<string>();
            using (var cancellation = new CancellationTokenSource())
            {
                var wait = SdkTaskWaiter.WaitAsync(nativeOperation.Task, cancellation.Token);
                cancellation.Cancel();
                await ExpectCancellation(wait);
                Assert.That(nativeOperation.Task.IsCompleted, Is.False);

                nativeOperation.SetResult("late-token");
                Assert.That(await nativeOperation.Task, Is.EqualTo("late-token"));
            }
        }

        [Test]
        public async Task SharedDeadline_AlreadySpentDuringInitializationDoesNotWaitForCallback()
        {
            using (var deadline = new CancellationTokenSource())
            {
                await SdkTaskWaiter.WaitAsync(Task.CompletedTask, deadline.Token);
                deadline.Cancel();
                var callback = new TaskCompletionSource<bool>();
                await ExpectCancellation(SdkTaskWaiter.WaitAsync(callback.Task, deadline.Token));
                Assert.That(callback.Task.IsCompleted, Is.False);
            }
        }

        [Test]
        public async Task SdkFailure_IsPropagatedToAdaptersFallbackHandler()
        {
            var failure = new InvalidOperationException("SDK failed");
            try
            {
                await SdkTaskWaiter.WaitAsync(Task.FromException(failure), CancellationToken.None);
                Assert.Fail("Expected the SDK failure.");
            }
            catch (InvalidOperationException actual)
            {
                Assert.That(actual, Is.SameAs(failure));
            }
        }

        private static async Task ExpectCancellation(Task task)
        {
            try
            {
                await task;
                Assert.Fail("Expected cancellation.");
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
