using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorOperationTests
    {
        [Test]
        public async Task RunAsync_AnotherOperationIsRunning_DoesNotStartItAsync()
        {
            using var operation = new ContentEditorOperation(() => Assert.Fail());
            var finished = new TaskCompletionSource<bool>();
            Task running = operation.RunAsync(
                _ => finished.Task,
                _ => { },
                exception => Assert.Fail(exception.ToString())
            );
            int starts = 0;

            await operation.RunAsync(
                _ =>
                {
                    starts++;
                    return Task.CompletedTask;
                },
                _ => { },
                exception => Assert.Fail(exception.ToString())
            );

            Assert.That(starts, Is.Zero);
            Assert.That(operation.IsRunning, Is.True);
            finished.SetResult(true);
            await running;
            Assert.That(operation.IsRunning, Is.False);
        }

        [Test]
        public async Task CanQuit_OperationHasNotUnwound_DefersQuitUntilCleanupAsync()
        {
            var events = new List<string>();
            using var operation = new ContentEditorOperation(() => events.Add("quit"));
            var finished = new TaskCompletionSource<bool>();
            CancellationToken token = default;
            Task running = operation.RunAsync(
                value =>
                {
                    token = value;
                    return finished.Task;
                },
                busy => events.Add(busy ? "busy" : "idle"),
                exception => Assert.Fail(exception.ToString()),
                completed: () => events.Add("refresh")
            );

            Assert.That(operation.CanQuit(), Is.False);
            Assert.That(token.IsCancellationRequested, Is.True);
            Assert.That(operation.IsRunning, Is.True);
            CollectionAssert.AreEqual(new[] { "busy" }, events);
            finished.SetResult(true);
            await running;

            CollectionAssert.AreEqual(new[] { "busy", "idle", "refresh", "quit" }, events);
            Assert.That(operation.CanQuit(), Is.True);
        }

        [Test]
        public async Task RunAsync_RequestedCancellation_ReportsCancellationAndRestoresInputAsync()
        {
            using var operation = new ContentEditorOperation(() => Assert.Fail());
            var finished = new TaskCompletionSource<bool>();
            bool busy = false;
            int cancellations = 0;
            Task running = operation.RunAsync(
                async token =>
                {
                    await finished.Task;
                    token.ThrowIfCancellationRequested();
                },
                value => busy = value,
                exception => Assert.Fail(exception.ToString()),
                () => cancellations++
            );

            operation.Cancel();
            finished.SetResult(true);
            await running;

            Assert.That(cancellations, Is.EqualTo(1));
            Assert.That(busy, Is.False);
            Assert.That(operation.IsRunning, Is.False);
        }

        [Test]
        public async Task RunAsync_WorkFails_ReportsFailureAndRestoresInputAsync()
        {
            using var operation = new ContentEditorOperation(() => Assert.Fail());
            var expected = new InvalidOperationException("Fixture failure");
            Exception reported = null;
            bool busy = false;

            await operation.RunAsync(
                _ => Task.FromException(expected),
                value => busy = value,
                exception => reported = exception
            );

            Assert.That(reported, Is.SameAs(expected));
            Assert.That(busy, Is.False);
            Assert.That(operation.IsRunning, Is.False);
        }

        [Test]
        public async Task Dispose_OperationFinishesAfterHostIsDestroyed_SuppressesHostCallbacksAsync()
        {
            var operation = new ContentEditorOperation(() => Assert.Fail());
            var finished = new TaskCompletionSource<bool>();
            int updates = 0;
            CancellationToken token = default;
            Task running = operation.RunAsync(
                value =>
                {
                    token = value;
                    return finished.Task;
                },
                _ => updates++,
                exception => Assert.Fail(exception.ToString()),
                completed: () => Assert.Fail()
            );

            operation.Dispose();
            Assert.That(token.IsCancellationRequested, Is.True);
            finished.SetResult(true);
            await running;

            Assert.That(updates, Is.EqualTo(1));
            Assert.That(operation.IsRunning, Is.False);
        }
    }
}
