using System;
using NUnit.Framework;
using Rebellion.Multiplayer;

namespace Rebellion.Tests.Multiplayer
{
    public sealed class MultiplayerSnapshotBufferTests
    {
        [Test]
        public void Take_SeveralAuthoritativeSnapshots_ReturnsNewestCompleteState()
        {
            MultiplayerSnapshotBuffer buffer = new();
            MultiplayerPacket newest = new(
                MultiplayerMessageType.SnapshotCommit,
                payload: new byte[] { 3 }
            );
            buffer.Enqueue(new MultiplayerPacket(MultiplayerMessageType.SnapshotCommit), 1);
            buffer.Enqueue(new MultiplayerPacket(MultiplayerMessageType.SnapshotCommit), 2);
            buffer.Enqueue(newest, 3);

            Assert.That(buffer.Take(), Is.SameAs(newest));
        }

        [Test]
        public void Enqueue_OlderRevisionArrivesLater_PreservesNewestState()
        {
            MultiplayerSnapshotBuffer buffer = new();
            MultiplayerPacket newest = new(MultiplayerMessageType.SnapshotCommit);
            buffer.Enqueue(newest, 3);
            buffer.Enqueue(new MultiplayerPacket(MultiplayerMessageType.SnapshotCommit), 2);

            Assert.That(buffer.Take(), Is.SameAs(newest));
        }

        [Test]
        public void Enqueue_CorrectionAtSameRevision_RetainsLaterCorrection()
        {
            MultiplayerSnapshotBuffer buffer = new();
            MultiplayerPacket correction = new(MultiplayerMessageType.SnapshotCommit);
            buffer.Enqueue(new MultiplayerPacket(MultiplayerMessageType.SnapshotCommit), 3);
            buffer.Enqueue(correction, 3);

            Assert.That(buffer.Take(), Is.SameAs(correction));
        }

        [Test]
        public void Take_StateAlreadyConsumed_ReturnsNull()
        {
            MultiplayerSnapshotBuffer buffer = new();
            buffer.Enqueue(new MultiplayerPacket(MultiplayerMessageType.SnapshotCommit), 3);
            buffer.Take();

            Assert.That(buffer.Take(), Is.Null);
        }

        [Test]
        public void Clear_PreviousSessionHasPendingState_DiscardsIt()
        {
            MultiplayerSnapshotBuffer buffer = new();
            buffer.Enqueue(new MultiplayerPacket(MultiplayerMessageType.SnapshotCommit), 3);
            buffer.Clear();

            Assert.That(buffer.Take(), Is.Null);
        }

        [Test]
        public void Enqueue_NullPacket_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new MultiplayerSnapshotBuffer().Enqueue(null, 1)
            );
        }
    }
}
