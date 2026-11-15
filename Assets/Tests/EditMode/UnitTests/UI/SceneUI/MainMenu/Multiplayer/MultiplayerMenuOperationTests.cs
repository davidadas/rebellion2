using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.UI.SceneUI.MainMenu.Multiplayer;

namespace Rebellion.Tests.UI.SceneUI.MainMenu.Multiplayer
{
    public sealed class MultiplayerMenuOperationTests
    {
        [Test]
        public async Task Cancel_PendingConnection_ReleasesFormImmediatelyAsync()
        {
            using MultiplayerMenuOperation operation = new();
            TaskCompletionSource<bool> completion = new();
            Task pending = operation.RunAsync(MultiplayerMenuPage.Browser, _ => completion.Task);
            operation.Cancel();
            Assert.That(operation.IsBusy, Is.False);
            completion.SetResult(true);
            await pending;
        }

        [Test]
        public async Task Cancel_LateFailureFromDismissedForm_DoesNotAffectNewFormAsync()
        {
            using MultiplayerMenuOperation operation = new();
            TaskCompletionSource<bool> oldCompletion = new();
            Task old = operation.RunAsync(MultiplayerMenuPage.Create, _ => oldCompletion.Task);
            operation.Cancel();
            TaskCompletionSource<bool> newCompletion = new();
            Task current = operation.RunAsync(
                MultiplayerMenuPage.Password,
                _ => newCompletion.Task
            );
            oldCompletion.SetException(new InvalidOperationException("Dismissed content failure"));
            await old;
            Assert.That(operation.Page, Is.EqualTo(MultiplayerMenuPage.Password));
            Assert.That(operation.Error, Is.Null);
            Assert.That(operation.IsBusy, Is.True);
            newCompletion.SetResult(true);
            await current;
        }

        [Test]
        public async Task RunAsync_ContentFailure_AssociatesErrorWithCreateFormAsync()
        {
            using MultiplayerMenuOperation operation = new();
            InvalidOperationException failure = new("Content unavailable");
            await operation.RunAsync(MultiplayerMenuPage.Create, _ => Task.FromException(failure));
            Assert.That(operation.Error, Is.SameAs(failure));
            Assert.That(operation.Page, Is.EqualTo(MultiplayerMenuPage.Create));
        }

        [Test]
        public async Task Cancel_PendingOperation_CancelsItsTokenAsync()
        {
            using MultiplayerMenuOperation operation = new();
            TaskCompletionSource<bool> completion = new();
            System.Threading.CancellationToken captured = default;
            Task pending = operation.RunAsync(
                MultiplayerMenuPage.Save,
                token =>
                {
                    captured = token;
                    return completion.Task;
                }
            );
            operation.Cancel();
            Assert.That(captured.IsCancellationRequested, Is.True);
            completion.SetResult(true);
            await pending;
        }
    }
}
