using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Multiplayer;
using UnityEngine;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class MultiplayerPersistenceSessionTests
    {
        [Test]
        public void Receive_PreviousConnectionPrepare_DoesNotReserveRecoveredGuest()
        {
            using PeerPair previous = new();
            previous.Host.RequestAsync("checkpoint", "Checkpoint", false);
            previous.Host.Pump();
            MultiplayerPacket stale = previous.ToGuest.Dequeue();
            using PeerPair recovered = new();
            recovered.Guest.Receive(stale);
            recovered.Guest.Pump();
            Assert.IsFalse(recovered.Guest.BlocksGameplay);
            Assert.IsNull(recovered.Guest.Failure);
            Assert.Zero(recovered.GuestWrites);
        }

        [Test]
        public void RequestAsync_UnsettledHost_DrainsGameplayWithoutStartingCheckpoint()
        {
            using PeerPair peers = new();
            peers.HostStable = false;

            Task<bool> request = peers.Host.RequestAsync("checkpoint", "Checkpoint", false);
            peers.Flush();

            Assert.IsFalse(request.IsCompleted);
            Assert.IsTrue(peers.Host.BlocksNewTicks);
            Assert.IsFalse(peers.Host.BlocksGameplay);
            Assert.IsEmpty(peers.HostFiles);
            Assert.IsEmpty(peers.GuestFiles);
            Assert.IsFalse(peers.Guest.BlocksGameplay);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RequestAsync_ManualSave_WaitsForBothDurableParticipantCopiesAsync(
            bool guestRequest
        )
        {
            using PeerPair peers = new();
            peers.GuestWriteGate = new TaskCompletionSource<bool>();
            Task<bool> request = (guestRequest ? peers.Guest : peers.Host).RequestAsync(
                "save_20261027_010203",
                "Checkpoint",
                false
            );

            peers.Flush();

            Assert.AreEqual(1, peers.HostFiles.Count);
            Assert.IsEmpty(peers.GuestFiles);
            Assert.IsFalse(request.IsCompleted);
            Assert.IsTrue(peers.Host.BlocksGameplay);
            Assert.IsTrue(peers.Guest.BlocksGameplay);
            peers.GuestWriteGate.SetResult(true);
            await peers.GuestWriteGate.Task;
            await Task.Yield();
            peers.Flush();

            Assert.IsTrue(request.IsCompletedSuccessfully);
            Assert.IsTrue(await request);
            Assert.AreEqual(1, peers.GuestFiles.Count);
            Assert.IsFalse(peers.Host.BlocksGameplay);
            Assert.IsFalse(peers.Guest.BlocksGameplay);
            string hostName = SaveGameManager.GetMultiplayerSaveFileName(
                peers.HostMetadata.CampaignID,
                true,
                "save_20261027_010203"
            );
            string guestName = SaveGameManager.GetMultiplayerSaveFileName(
                peers.HostMetadata.CampaignID,
                false,
                "save_20261027_010203"
            );
            CollectionAssert.AreEqual(peers.HostFiles[hostName], peers.GuestFiles[guestName]);
            Assert.AreEqual(
                peers.HostMetadata.SharedSaveToken,
                peers.GuestMetadata.SharedSaveToken
            );
            Assert.AreEqual(peers.HostMetadata.LastSavedUtc, peers.GuestMetadata.LastSavedUtc);
        }

        [Test]
        public void RequestAsync_UnsettledGuest_DoesNotWriteHostCopyBeforeReservationReceipt()
        {
            using PeerPair peers = new();
            peers.GuestStable = false;
            peers.Host.RequestAsync("checkpoint", "Checkpoint", false);

            peers.Flush();

            Assert.IsTrue(peers.Host.BlocksGameplay);
            Assert.IsTrue(peers.Guest.BlocksGameplay);
            Assert.IsEmpty(peers.HostFiles);
            Assert.IsEmpty(peers.GuestFiles);
        }

        [Test]
        public void RequestAutosave_HostScheduled_WritesHostOnlyAndSharesExactMetadata()
        {
            using PeerPair peers = new();

            peers.Guest.RequestAutosave();
            peers.Flush();
            Assert.IsEmpty(peers.HostFiles);
            peers.Host.RequestAutosave();
            peers.Host.RequestAutosave();
            peers.Flush();

            Assert.AreEqual(1, peers.HostFiles.Count);
            Assert.IsEmpty(peers.GuestFiles);
            Assert.AreEqual(peers.HostMetadata.LastSavedUtc, peers.GuestMetadata.LastSavedUtc);
            Assert.AreEqual(
                peers.HostMetadata.SharedSaveToken,
                peers.GuestMetadata.SharedSaveToken
            );
            Assert.IsFalse(peers.Host.BlocksGameplay);
            Assert.IsFalse(peers.Guest.BlocksGameplay);
        }

        [Test]
        public void RequestAutosave_LoadWaitingForDay_PreservesOutgoingCheckpointBeforeTransfer()
        {
            using PeerPair peers = new();
            peers.HostStable = false;
            peers.Host.RequestAsync("load-source", null, true);
            peers.Host.RequestAutosave();
            peers.HostStable = true;

            peers.Flush();

            Assert.AreEqual(1, peers.HostFiles.Count);
            Assert.IsTrue(peers.Host.IsTransferringScene);
            Assert.AreEqual("save", peers.HostOperations[0]);
            Assert.AreEqual("load", peers.HostOperations[1]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RequestAsync_Load_WaitsForBothReplacementScenesToSynchronizeAsync(
            bool guestRequest
        )
        {
            using PeerPair peers = new();
            Task<bool> request = (guestRequest ? peers.Guest : peers.Host).RequestAsync(
                "load-source",
                null,
                true,
                guestRequest
                    ? new MultiplayerSnapshot(new byte[] { 1, 2, 3 }, "selected-save")
                    : null
            );

            peers.Flush();

            Assert.AreEqual(1, peers.HostTransfers);
            Assert.AreEqual(1, peers.GuestTransfers);
            Assert.IsTrue(peers.Host.IsTransferringScene);
            Assert.IsTrue(peers.Guest.IsTransferringScene);
            Assert.IsFalse(request.IsCompleted);
            peers.Guest.NotifySynchronized();
            Assert.IsFalse(request.IsCompleted);

            peers.Host.NotifySynchronized();
            peers.Flush();

            Assert.IsTrue(request.IsCompletedSuccessfully);
            Assert.IsTrue(await request);
            Assert.IsFalse(peers.Host.IsTransferringScene);
            Assert.IsFalse(peers.Guest.IsTransferringScene);
            Assert.IsFalse(peers.Host.BlocksGameplay);
            Assert.IsFalse(peers.Guest.BlocksGameplay);
        }

        [Test]
        public void RequestAsync_GuestWriteFails_ReportsPartialCheckpointWithoutClaimingSuccess()
        {
            using PeerPair peers = new();
            peers.GuestWriteFailure = new IOException("Peer disk is unavailable.");
            Task<bool> request = peers.Host.RequestAsync("checkpoint", "Checkpoint", false);

            peers.Flush();

            Assert.AreEqual(1, peers.HostFiles.Count);
            Assert.IsEmpty(peers.GuestFiles);
            Assert.IsInstanceOf<IOException>(peers.Host.Failure);
            Assert.IsInstanceOf<IOException>(peers.Guest.Failure);
            Assert.IsTrue(request.IsFaulted);
            Assert.ThrowsAsync<IOException>(async () => await request);
            Assert.IsTrue(peers.Host.BlocksGameplay);
            Assert.IsTrue(peers.Guest.BlocksGameplay);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Dispose_PendingOperation_CancelsOwningParticipantTask(bool guestRequest)
        {
            using PeerPair peers = new();
            peers.HostStable = false;
            MultiplayerPersistenceSession owner = guestRequest ? peers.Guest : peers.Host;
            Task<bool> request = owner.RequestAsync("checkpoint", "Checkpoint", false);

            owner.Dispose();

            Assert.IsTrue(request.IsCanceled);
            Assert.IsEmpty(peers.HostFiles);
            Assert.IsEmpty(peers.GuestFiles);
        }

        [Test]
        public void Receive_UnownedFilename_RejectsBeforeAnyStoreCallback()
        {
            using PeerPair peers = new();
            peers.Host.Receive(
                new MultiplayerPacket(
                    MultiplayerMessageType.PersistenceRequest,
                    JsonUtility.ToJson(
                        new MultiplayerPersistenceCommand
                        {
                            requestID = 1,
                            fileName = "../escape",
                            initializationID = peers.ConnectionID,
                        }
                    )
                )
            );

            peers.Host.Pump();

            Assert.IsInstanceOf<ArgumentException>(peers.Host.Failure);
            Assert.IsEmpty(peers.HostFiles);
            Assert.IsEmpty(peers.GuestFiles);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Dispose_TransferringScene_CancelsRequestAndReleasesTransferOwnership(
            bool guestRequest
        )
        {
            using PeerPair peers = new();
            MultiplayerPersistenceSession owner = guestRequest ? peers.Guest : peers.Host;
            Task<bool> loading = owner.RequestAsync(
                "load-source",
                null,
                true,
                guestRequest
                    ? new MultiplayerSnapshot(new byte[] { 1, 2, 3 }, "selected-save")
                    : null
            );
            peers.Flush();
            Assert.IsTrue(owner.IsTransferringScene);

            owner.Dispose();

            Assert.IsTrue(loading.IsCanceled);
            Assert.IsFalse(owner.IsTransferringScene);
            Assert.IsFalse(owner.BlocksGameplay);
        }

        [Test]
        public async Task Receive_ForgedAutomaticGuestRequest_StillWritesBothManualCopiesAsync()
        {
            using PeerPair peers = new();
            Task<bool> request = peers.Guest.RequestAsync("checkpoint", "Checkpoint", false);
            MultiplayerPacket packet = peers.ToHost.Dequeue();
            MultiplayerPersistenceCommand forged =
                JsonUtility.FromJson<MultiplayerPersistenceCommand>(packet.Metadata);
            forged.automatic = true;
            forged.guestRequest = false;
            peers.ToHost.Enqueue(new MultiplayerPacket(packet.Type, JsonUtility.ToJson(forged)));

            peers.Flush();

            Assert.IsTrue(request.IsCompletedSuccessfully);
            Assert.IsTrue(await request);
            Assert.AreEqual(1, peers.HostFiles.Count);
            Assert.AreEqual(1, peers.GuestFiles.Count);
        }

        [Test]
        public void Receive_DuplicateCommitAndReceipt_DoesNotRepeatWriteOrFailNextCheckpoint()
        {
            using PeerPair peers = new();
            peers.Host.RequestAsync("checkpoint", "Checkpoint", false);
            peers.Host.Pump();
            peers.DeliverToGuest();
            peers.Guest.Pump();
            peers.DeliverToHost();
            peers.Host.Pump();
            MultiplayerPacket commit = peers.ToGuest.Peek();
            peers.ToGuest.Enqueue(commit);

            peers.Flush();

            Assert.AreEqual(1, peers.HostFiles.Count);
            Assert.AreEqual(1, peers.GuestWrites);
            Assert.IsNull(peers.Host.Failure);
            Assert.IsNull(peers.Guest.Failure);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task RequestAsync_BothParticipantsSave_SerializesDurableCheckpointsAsync(
            bool guestFirst
        )
        {
            using PeerPair peers = new();
            peers.GuestWriteGate = new TaskCompletionSource<bool>();
            Task<bool> first = (guestFirst ? peers.Guest : peers.Host).RequestAsync(
                "first",
                "First checkpoint",
                false
            );
            peers.Flush();
            string firstToken = peers.HostMetadata.SharedSaveToken;

            Task<bool> second = (guestFirst ? peers.Host : peers.Guest).RequestAsync(
                "second",
                "Second checkpoint",
                false
            );
            peers.Flush();

            Assert.That(first.IsCompleted, Is.False);
            Assert.That(second.IsCompleted, Is.False);
            Assert.That(peers.HostFiles.Count, Is.EqualTo(1));
            Assert.That(peers.GuestFiles, Is.Empty);
            peers.GuestWriteGate.SetResult(true);
            await peers.GuestWriteGate.Task;
            await Task.Yield();
            peers.Flush();

            Assert.That(await first, Is.True);
            Assert.That(await second, Is.True);
            Assert.That(peers.HostFiles.Count, Is.EqualTo(2));
            Assert.That(peers.GuestFiles.Count, Is.EqualTo(2));
            foreach (string name in new[] { "first", "second" })
            {
                string hostName = SaveGameManager.GetMultiplayerSaveFileName(
                    peers.HostMetadata.CampaignID,
                    true,
                    name
                );
                string guestName = SaveGameManager.GetMultiplayerSaveFileName(
                    peers.HostMetadata.CampaignID,
                    false,
                    name
                );
                CollectionAssert.AreEqual(peers.HostFiles[hostName], peers.GuestFiles[guestName]);
            }
            Assert.That(peers.HostMetadata.SharedSaveToken, Is.Not.EqualTo(firstToken));
            Assert.That(
                peers.GuestMetadata.SharedSaveToken,
                Is.EqualTo(peers.HostMetadata.SharedSaveToken)
            );
            Assert.That(peers.Host.Failure, Is.Null);
            Assert.That(peers.Guest.Failure, Is.Null);
        }

        [Test]
        public void Dispose_BothParticipantsHaveQueuedSaves_CancelsBothWithoutWriting()
        {
            using PeerPair peers = new();
            peers.HostStable = false;
            Task<bool> host = peers.Host.RequestAsync("host-save", "Host save", false);
            Task<bool> guest = peers.Guest.RequestAsync("guest-save", "Guest save", false);
            peers.Flush();

            peers.Host.Dispose();
            peers.Guest.Dispose();

            Assert.That(host.IsCanceled, Is.True);
            Assert.That(guest.IsCanceled, Is.True);
            Assert.That(peers.HostFiles, Is.Empty);
            Assert.That(peers.GuestFiles, Is.Empty);
        }

        [Test]
        public async Task RequestAsync_GuestOnlyLoad_TransfersTheSelectedWorldAsync()
        {
            using PeerPair peers = new();
            MultiplayerSnapshot source = new(new byte[] { 4, 5, 6 }, "guest-selected-world");
            Task<bool> request = peers.Guest.RequestAsync("guest-only", null, true, source);
            peers.Flush();
            peers.Host.NotifySynchronized();
            peers.Flush();
            await request;

            CollectionAssert.AreEqual(source.Payload, peers.LoadSources[0]);
            Assert.That(peers.LoadSourceHashes[0], Is.EqualTo(source.Hash));
            Assert.That(peers.HostFiles, Is.Empty);
        }

        [Test]
        public void RequestAsync_GuestLoadCallerChangesPayload_PreservesRequestedSnapshot()
        {
            using PeerPair peers = new();
            byte[] bytes = { 4, 5, 6 };
            peers.Guest.RequestAsync(
                "guest-only",
                null,
                true,
                new MultiplayerSnapshot(bytes, "guest-selected-world")
            );
            bytes[0] = 9;
            peers.Flush();

            CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, peers.LoadSources[0]);
        }

        [Test]
        public void RequestAsync_GuestLoadWithoutSelectedWorld_RejectsBeforeSending()
        {
            using PeerPair peers = new();
            Assert.Throws<ArgumentException>(() =>
                peers.Guest.RequestAsync("guest-only", null, true)
            );
            Assert.That(peers.ToHost, Is.Empty);
        }

        [Test]
        public void Receive_GuestLoadWithoutSelectedWorld_RejectsBeforeStorage()
        {
            using PeerPair peers = new();
            peers.Host.Receive(
                new MultiplayerPacket(
                    MultiplayerMessageType.PersistenceRequest,
                    JsonUtility.ToJson(
                        new MultiplayerPersistenceCommand
                        {
                            initializationID = peers.ConnectionID,
                            requestID = 1,
                            guestRequest = true,
                            load = true,
                            fileName = "guest-only",
                        }
                    )
                )
            );
            peers.Host.Pump();

            Assert.That(peers.Host.Failure, Is.TypeOf<InvalidDataException>());
            Assert.That(peers.HostOperations, Is.Empty);
        }

        private sealed class PeerPair : IDisposable
        {
            internal readonly Queue<MultiplayerPacket> ToHost = new();
            internal readonly Queue<MultiplayerPacket> ToGuest = new();
            internal readonly Dictionary<string, byte[]> HostFiles = new();
            internal readonly Dictionary<string, byte[]> GuestFiles = new();
            internal readonly List<string> HostOperations = new();
            internal readonly MultiplayerPersistenceSession Host;
            internal readonly MultiplayerPersistenceSession Guest;
            internal readonly GameMetadata HostMetadata = new()
            {
                CampaignID = "11111111111111111111111111111111",
                CurrentTick = 40,
                MultiplayerCampaign = new MultiplayerCampaignIdentity
                {
                    HostUserID = "authenticated-host",
                    GuestUserID = "authenticated-guest",
                    HostFactionID = "FNALL1",
                    GuestFactionID = "FNEMP1",
                },
            };
            internal GameMetadata GuestMetadata;
            internal TaskCompletionSource<bool> GuestWriteGate;
            internal Exception GuestWriteFailure;
            internal bool HostStable = true;
            internal bool GuestStable = true;
            internal int HostTransfers;
            internal int GuestTransfers;
            internal int GuestWrites;
            internal readonly List<byte[]> LoadSources = new();
            internal readonly List<string> LoadSourceHashes = new();
            internal byte[] PreparedLoadPayload;
            internal string ConnectionID { get; } = Guid.NewGuid().ToString("N");

            /// <summary>
            /// Connects two independent stores through delayed ordered packet queues.
            /// </summary>
            internal PeerPair()
            {
                Host = new MultiplayerPersistenceSession(
                    MultiplayerRole.Host,
                    packet => ToGuest.Enqueue(packet),
                    () => HostStable,
                    PrepareHostAsync,
                    _ => Task.CompletedTask,
                    _ => HostTransfers++,
                    ConnectionID
                );
                Guest = new MultiplayerPersistenceSession(
                    MultiplayerRole.Guest,
                    packet => ToHost.Enqueue(packet),
                    () => GuestStable,
                    (_, _) =>
                        throw new InvalidOperationException("Guest cannot prepare host storage."),
                    PrepareGuestAsync,
                    _ => GuestTransfers++,
                    ConnectionID
                );
            }

            /// <summary>
            /// Drains this finite fixture exchange without synchronous transport reentrancy.
            /// </summary>
            internal void Flush()
            {
                for (int pass = 0; pass < 16; pass++)
                {
                    Host.Pump();
                    Guest.Pump();
                    DeliverToHost();
                    DeliverToGuest();
                }
            }

            /// <summary>
            /// Delivers queued guest packets without pumping domain or persistence callbacks.
            /// </summary>
            internal void DeliverToHost()
            {
                while (ToHost.Count > 0)
                    Host.Receive(ToHost.Dequeue());
            }

            /// <summary>
            /// Delivers queued host packets without pumping domain or persistence callbacks.
            /// </summary>
            internal void DeliverToGuest()
            {
                while (ToGuest.Count > 0)
                    Guest.Receive(ToGuest.Dequeue());
            }

            /// <summary>
            /// Cancels any unfinished requests held by either fixture participant.
            /// </summary>
            public void Dispose()
            {
                Host.Dispose();
                Guest.Dispose();
            }

            /// <summary>
            /// Publishes one host metadata checkpoint or returns a prepared fake saved-world payload.
            /// </summary>
            /// <param name="command">The reserved operation.</param>
            /// <param name="sourcePayload">The requesting guest's selected source, when present.</param>
            /// <returns>The prepared explicit-schema payload.</returns>
            private Task<MultiplayerPersistenceCheckpoint> PrepareHostAsync(
                MultiplayerPersistenceCommand command,
                byte[] sourcePayload
            )
            {
                HostOperations.Add(command.load ? "load" : "save");
                if (command.load)
                {
                    LoadSources.Add(sourcePayload);
                    LoadSourceHashes.Add(command.snapshotHash);
                    command.snapshotHash = "verified-saved-state";
                    PreparedLoadPayload = sourcePayload ?? new byte[] { 1, 2, 3 };
                    return Task.FromResult(
                        new MultiplayerPersistenceCheckpoint
                        {
                            Command = command,
                            Payload = PreparedLoadPayload,
                        }
                    );
                }
                HostMetadata.SharedSaveToken = command.token;
                HostMetadata.LastSavedUtc = new DateTime(2026, 10, 27, 1, 2, 3, DateTimeKind.Utc);
                byte[] payload = MultiplayerSnapshotSerializer.SerializeValue(
                    HostMetadata,
                    typeof(GameMetadata)
                );
                string logicalName = command.automatic ? "autosave_0000000040" : command.fileName;
                HostFiles[
                    SaveGameManager.GetMultiplayerSaveFileName(
                        HostMetadata.CampaignID,
                        true,
                        logicalName
                    )
                ] = payload;
                return Task.FromResult(
                    new MultiplayerPersistenceCheckpoint { Command = command, Payload = payload }
                );
            }

            /// <summary>
            /// Holds the peer write until allowed, then copies the exact host metadata bytes.
            /// </summary>
            /// <param name="checkpoint">The committed explicit-schema checkpoint.</param>
            /// <returns>The fixture-controlled durable-write or content-preparation completion.</returns>
            private async Task PrepareGuestAsync(MultiplayerPersistenceCheckpoint checkpoint)
            {
                if (GuestWriteGate != null)
                    await GuestWriteGate.Task;
                if (GuestWriteFailure != null)
                    throw GuestWriteFailure;
                if (checkpoint.Command.load)
                {
                    CollectionAssert.AreEqual(PreparedLoadPayload, checkpoint.Payload);
                    Assert.AreEqual("verified-saved-state", checkpoint.Command.snapshotHash);
                    return;
                }
                GuestMetadata = (GameMetadata)
                    MultiplayerSnapshotSerializer.DeserializeValue(
                        checkpoint.Payload,
                        typeof(GameMetadata)
                    );
                if (checkpoint.Command.automatic)
                    return;
                GuestWrites++;
                string fileName = SaveGameManager.GetMultiplayerSaveFileName(
                    GuestMetadata.CampaignID,
                    false,
                    checkpoint.Command.fileName
                );
                GuestFiles[fileName] = checkpoint.Payload;
            }
        }
    }
}
