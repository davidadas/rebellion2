using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSendSchedulerTests
    {
        [Test]
        public async Task SendAsync_BlockedReliableWriter_BoundsRetainedWorkBeforeWaitingAsync()
        {
            TaskCompletionSource<bool> unblock = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            MultiplayerSendBudget budget = new(maximumTacticalPackets: 2);
            int writes = 0;
            MultiplayerSendScheduler scheduler = new(
                async (_, _) =>
                {
                    writes++;
                    await unblock.Task;
                },
                _ => true,
                budget
            );
            MultiplayerPacket packet = new(
                MultiplayerMessageType.TacticalEffects,
                payload: new byte[1024]
            );
            Task first = scheduler.SendAsync(packet, CancellationToken.None);
            Task second = scheduler.SendAsync(packet, CancellationToken.None);

            for (int index = 0; index < 1000; index++)
                Assert.Throws<InvalidDataException>(() =>
                    scheduler.SendAsync(packet, CancellationToken.None)
                );

            Assert.AreEqual(1, writes);
            Assert.AreEqual(2, budget.RetainedPackets);
            Assert.AreEqual(
                2 * MultiplayerPacketCodec.GetEncodedLength(packet),
                budget.RetainedBytes
            );
            unblock.SetResult(true);
            await Task.WhenAll(first, second);
            Assert.AreEqual(2, writes);
            Assert.Zero(budget.RetainedPackets);
            Assert.Zero(budget.RetainedBytes);
        }

        [Test]
        public async Task SendAsync_QueuedAndActiveCancellation_ReleasesEachReservationAndWriterGateAsync()
        {
            MultiplayerSendBudget budget = new();
            bool blocked = true;
            MultiplayerSendScheduler scheduler = new(
                (_, token) => blocked ? Task.Delay(Timeout.Infinite, token) : Task.CompletedTask,
                _ => true,
                budget
            );
            using CancellationTokenSource active = new();
            using CancellationTokenSource queued = new();
            MultiplayerPacket packet = new(MultiplayerMessageType.TacticalReady);
            Task first = scheduler.SendAsync(packet, active.Token);
            Task second = scheduler.SendAsync(packet, queued.Token);
            queued.Cancel();
            queued.Cancel();

            await RequireCancellationAsync(second);

            Assert.AreEqual(1, budget.RetainedPackets);
            active.Cancel();
            active.Cancel();
            await RequireCancellationAsync(first);
            Assert.Zero(budget.RetainedPackets);
            Assert.Zero(budget.RetainedBytes);
            blocked = false;
            await scheduler.SendAsync(packet, CancellationToken.None);
            Assert.Zero(budget.RetainedPackets);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task SendAsync_WriterFailure_ReleasesOwnershipBeforeSubsequentSendAsync(
            bool synchronousFailure
        )
        {
            MultiplayerSendBudget budget = new();
            int attempts = 0;
            MultiplayerSendScheduler scheduler = new(
                (_, _) =>
                {
                    if (++attempts > 1)
                        return Task.CompletedTask;
                    if (synchronousFailure)
                        throw new IOException("blocked provider failed");
                    return Task.FromException(new IOException("asynchronous provider failed"));
                },
                _ => true,
                budget
            );
            MultiplayerPacket packet = new(MultiplayerMessageType.TacticalCheckpoint);

            Assert.ThrowsAsync<IOException>(async () =>
                await scheduler.SendAsync(packet, CancellationToken.None)
            );

            Assert.Zero(budget.RetainedPackets);
            Assert.Zero(budget.RetainedBytes);
            await scheduler.SendAsync(packet, CancellationToken.None);
            Assert.AreEqual(2, attempts);
            Assert.Zero(budget.RetainedPackets);
        }

        [Test]
        public async Task SendAsync_ReliableOverflow_StillAttemptsAndDropsReplaceableSnapshotWithoutWaitingAsync()
        {
            TaskCompletionSource<bool> unblock = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            MultiplayerSendBudget budget = new(maximumTacticalPackets: 1);
            int snapshotAttempts = 0;
            int reliableWrites = 0;
            MultiplayerSendScheduler scheduler = new(
                async (_, _) =>
                {
                    reliableWrites++;
                    await unblock.Task;
                },
                _ =>
                {
                    snapshotAttempts++;
                    return false;
                },
                budget
            );
            Task reliable = scheduler.SendAsync(
                new(MultiplayerMessageType.TacticalEffects),
                CancellationToken.None
            );
            Assert.Throws<InvalidDataException>(() =>
                scheduler.SendAsync(
                    new(MultiplayerMessageType.TacticalEffects),
                    CancellationToken.None
                )
            );

            Task snapshot = scheduler.SendAsync(
                new(MultiplayerMessageType.TacticalFrame),
                CancellationToken.None
            );

            Assert.IsTrue(snapshot.IsCompletedSuccessfully);
            Assert.AreEqual(1, snapshotAttempts);
            Assert.AreEqual(1, reliableWrites);
            Assert.AreEqual(1, budget.RetainedPackets);
            unblock.SetResult(true);
            await reliable;
            Assert.Zero(budget.RetainedPackets);
            Assert.Zero(budget.RetainedBytes);
        }

        [Test]
        public void SendAsync_CancelledOrInvalidPacket_ReservesNoOwnership()
        {
            MultiplayerSendBudget budget = new();
            MultiplayerSendScheduler scheduler = new(
                (_, _) => Task.CompletedTask,
                _ => true,
                budget
            );
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                scheduler.SendAsync(new(MultiplayerMessageType.TacticalReady), cancellation.Token)
            );
            Assert.Throws<InvalidDataException>(() =>
                scheduler.SendAsync(
                    new(
                        MultiplayerMessageType.TacticalCommand,
                        payload: new byte[MultiplayerPacketCodec.MaximumTacticalCommandBytes + 1]
                    ),
                    CancellationToken.None
                )
            );

            Assert.Zero(budget.RetainedPackets);
            Assert.Zero(budget.RetainedBytes);
        }

        /// <summary>Awaits cancellation without accepting ordinary completion as cleanup evidence.</summary>
        /// <param name="pending">The cancelled transport operation.</param>
        /// <returns>The observed cancellation boundary.</returns>
        private static async Task RequireCancellationAsync(Task pending)
        {
            try
            {
                await pending;
                Assert.Fail("The transport operation should have been cancelled.");
            }
            catch (OperationCanceledException) { }
        }
    }
}
