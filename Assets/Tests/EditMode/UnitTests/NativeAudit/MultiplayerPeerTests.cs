using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;
using UnityEngine;

namespace Rebellion.Tests.NativeAudit
{
    public sealed class MultiplayerPeerTests
    {
        [Test]
        public async Task HostAndGuest_Connected_ExchangeOrderedPacketsOverLoopbackAsync()
        {
            using MultiplayerPeer host = new MultiplayerPeer();
            using MultiplayerPeer guest = new MultiplayerPeer();
            host.StartHost(0);

            await guest.ConnectAsync("127.0.0.1", host.ListeningPort);
            await WaitForEventAsync(host, MultiplayerTransportEventType.Connected);
            await WaitForEventAsync(guest, MultiplayerTransportEventType.Connected);

            await guest.SendAsync(new MultiplayerPacket(MultiplayerMessageType.Hello, "first"));
            await guest.SendAsync(new MultiplayerPacket(MultiplayerMessageType.Ready, "second"));

            MultiplayerTransportEvent first = await WaitForEventAsync(
                host,
                MultiplayerTransportEventType.PacketReceived
            );
            MultiplayerTransportEvent second = await WaitForEventAsync(
                host,
                MultiplayerTransportEventType.PacketReceived
            );
            Assert.That(first.Packet.Type, Is.EqualTo(MultiplayerMessageType.Hello));
            Assert.That(first.Packet.Metadata, Is.EqualTo("first"));
            Assert.That(second.Packet.Type, Is.EqualTo(MultiplayerMessageType.Ready));
            Assert.That(second.Packet.Metadata, Is.EqualTo("second"));
        }

        [Test]
        public async Task SendAsync_SnapshotPayload_TransfersBothDirectionsAsync()
        {
            using MultiplayerPeer host = new MultiplayerPeer();
            using MultiplayerPeer guest = new MultiplayerPeer();
            host.StartHost(0);
            await guest.ConnectAsync("127.0.0.1", host.ListeningPort);
            await WaitForEventAsync(host, MultiplayerTransportEventType.Connected);
            await WaitForEventAsync(guest, MultiplayerTransportEventType.Connected);
            GameRoot hostGame = new GameRoot { CurrentTick = 7 };
            MultiplayerSnapshot hostSnapshot = MultiplayerSnapshotCodec.Capture(hostGame);
            string initializationID = Guid.NewGuid().ToString("N");

            await host.SendAsync(CreateSnapshotPacket(hostSnapshot, initializationID));
            MultiplayerTransportEvent committed = await WaitForEventAsync(
                guest,
                MultiplayerTransportEventType.PacketReceived
            );
            MultiplayerSnapshotMessage metadata = JsonUtility.FromJson<MultiplayerSnapshotMessage>(
                committed.Packet.Metadata
            );
            GameRoot guestGame = MultiplayerSnapshotCodec.Restore(
                committed.Packet.Payload,
                metadata.hash
            );
            guestGame.CurrentTick = 8;
            MultiplayerSnapshot guestSnapshot = MultiplayerSnapshotCodec.Capture(guestGame);

            await guest.SendAsync(CreateSnapshotPacket(guestSnapshot, initializationID));
            MultiplayerTransportEvent proposed = await WaitForEventAsync(
                host,
                MultiplayerTransportEventType.PacketReceived
            );
            metadata = JsonUtility.FromJson<MultiplayerSnapshotMessage>(proposed.Packet.Metadata);
            GameRoot restored = MultiplayerSnapshotCodec.Restore(
                proposed.Packet.Payload,
                metadata.hash
            );

            Assert.That(committed.Packet.Type, Is.EqualTo(MultiplayerMessageType.SnapshotCommit));
            Assert.That(restored.CurrentTick, Is.EqualTo(8));
            Assert.That(metadata.initializationID, Is.EqualTo(initializationID));
        }

        /// <summary>
        /// Creates one snapshot commit packet for transport integration coverage.
        /// </summary>
        /// <param name="snapshot">The captured state.</param>
        /// <param name="initializationID">The initialization identity carried by the metadata.</param>
        /// <returns>The encoded snapshot packet.</returns>
        private static MultiplayerPacket CreateSnapshotPacket(
            MultiplayerSnapshot snapshot,
            string initializationID
        )
        {
            return new MultiplayerPacket(
                MultiplayerMessageType.SnapshotCommit,
                JsonUtility.ToJson(
                    new MultiplayerSnapshotMessage
                    {
                        initializationID = initializationID,
                        hash = snapshot.Hash,
                    }
                ),
                snapshot.Payload
            );
        }

        /// <summary>
        /// Waits for one transport event while discarding unrelated events.
        /// </summary>
        /// <param name="peer">The peer to poll.</param>
        /// <param name="eventType">The required event type.</param>
        /// <returns>The matching event.</returns>
        private static async Task<MultiplayerTransportEvent> WaitForEventAsync(
            MultiplayerPeer peer,
            MultiplayerTransportEventType eventType
        )
        {
            Stopwatch timeout = Stopwatch.StartNew();
            while (timeout.Elapsed < TimeSpan.FromSeconds(5))
            {
                while (peer.TryDequeue(out MultiplayerTransportEvent transportEvent))
                {
                    if (transportEvent.Type == eventType)
                        return transportEvent;
                }
                await Task.Delay(10);
            }
            Assert.Fail($"Timed out waiting for {eventType}.");
            return null;
        }
    }
}
