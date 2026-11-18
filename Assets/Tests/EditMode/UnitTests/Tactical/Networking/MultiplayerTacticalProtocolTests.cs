using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using Rebellion.Tactical;
using Rebellion.Tactical.Networking;
using UnityEngine;
using Encoding = global::System.Text.Encoding;

namespace Rebellion.Tests.Tactical.Networking
{
    [TestFixture]
    public sealed class MultiplayerTacticalProtocolTests
    {
        [Test]
        public void PublishFrame_BothViewsReady_AdvancesIndependentlyOfProjectionReceipts()
        {
            using PeerPair peers = new();
            peers.Host.Ready();
            peers.Deliver();
            peers.Host.PublishFrame();
            Assert.Zero(peers.Captures);
            peers.Guest.Ready();
            peers.Deliver();

            peers.Host.PublishFrame();
            peers.Host.PublishFrame();

            Assert.AreEqual(2, peers.Captures);
            Assert.IsTrue(peers.Host.CanAdvance);
            Assert.Zero(peers.Applied.Count);
            peers.Deliver();
            Assert.AreEqual(2, peers.Applied.Last().Sequence);
            Assert.IsTrue(peers.Host.CanAdvance);
        }

        [Test]
        public void ExecuteAsync_PeerStillLoading_DefersCommandUntilBothViewsAreReady()
        {
            using PeerPair peers = new();
            peers.Host.Ready();
            var completion = peers.Host.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );

            peers.Deliver();

            Assert.IsFalse(completion.IsCompleted);
            Assert.IsEmpty(peers.Actors);
            peers.Guest.Ready();
            peers.Deliver();
            Assert.IsTrue(completion.Result.Accepted);
            CollectionAssert.AreEqual(new[] { CombatSide.Attacker }, peers.Actors);
        }

        [Test]
        public void ExecuteAsync_BothPlayers_AuthenticatesTheSideWithoutReplicaExecution()
        {
            using PeerPair peers = new();
            peers.Ready();
            var host = peers.Host.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Order,
                    Order = TacticalCombatOrder.Stop,
                }
            );
            var guest = peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );

            peers.Deliver();

            Assert.IsTrue(host.Result.Accepted);
            Assert.IsTrue(guest.Result.Accepted);
            CollectionAssert.AreEqual(
                new[] { CombatSide.Attacker, CombatSide.Defender },
                peers.Actors
            );
            Assert.Zero(peers.Applied.Count);
        }

        [Test]
        public void Receive_GuestClaimsHostRole_UsesConnectionRoleInstead()
        {
            using PeerPair peers = new();
            peers.Ready();
            var completion = peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Pause,
                    Paused = true,
                }
            );
            MultiplayerPacket request = peers.ToHost.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(request.Metadata);
            envelope.actor = MultiplayerRole.Host;
            peers.Host.Receive(
                new MultiplayerPacket(request.Type, JsonUtility.ToJson(envelope), request.Payload)
            );

            peers.Deliver();

            Assert.IsTrue(completion.Result.Accepted);
            CollectionAssert.AreEqual(new[] { CombatSide.Defender }, peers.Actors);
        }

        [Test]
        public void Receive_DuplicateCommand_RepliesWithoutApplyingAgain()
        {
            using PeerPair peers = new();
            peers.Ready();
            var completion = peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );
            MultiplayerPacket request = peers.ToHost.Peek();
            peers.Deliver();

            peers.Host.Receive(request);
            peers.Deliver();

            Assert.IsTrue(completion.Result.Accepted);
            Assert.AreEqual(1, peers.Actors.Count);
            Assert.IsNull(peers.Host.Failure);
        }

        [Test]
        public void Receive_DuplicateCommandChangesParameters_StopsBattle()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );
            MultiplayerPacket request = peers.ToHost.Peek();
            peers.Deliver();

            peers.Host.Receive(
                new MultiplayerPacket(
                    request.Type,
                    request.Metadata,
                    Encoding.UTF8.GetBytes(
                        JsonUtility.ToJson(
                            new TacticalCombatNetworkCommand
                            {
                                Kind = TacticalCombatNetworkCommandKind.ToggleAutomation,
                            }
                        )
                    )
                )
            );
            peers.Host.Pump();

            Assert.IsInstanceOf<InvalidDataException>(peers.Host.Failure);
            Assert.AreEqual(1, peers.Actors.Count);
        }

        [Test]
        public void Receive_DuplicateProjection_DoesNotReplayItsEffects()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            MultiplayerPacket frame = peers.ToGuest.Peek();
            peers.Deliver();

            peers.Guest.Receive(frame);
            peers.Deliver();

            Assert.AreEqual(1, peers.Applied.Count);
            Assert.IsTrue(peers.Host.CanAdvance);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Receive_StaleInitializationOrBattle_DropsBeforeReadingPayload(
            bool initialization
        )
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );
            MultiplayerPacket request = peers.ToHost.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(request.Metadata);
            if (initialization)
                envelope.initializationID = Guid.NewGuid().ToString("N");
            else
                envelope.battleID = Guid.NewGuid().ToString("N");

            peers.Host.Receive(
                new MultiplayerPacket(
                    request.Type,
                    JsonUtility.ToJson(envelope),
                    new byte[] { 0xff }
                )
            );
            peers.Host.Pump();

            Assert.IsNull(peers.Host.Failure);
            Assert.IsEmpty(peers.Actors);
        }

        [Test]
        public void Receive_GapInGuestCommandIDs_RejectsBeforeMutation()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );
            MultiplayerPacket request = peers.ToHost.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(request.Metadata);
            envelope.requestID++;

            peers.Host.Receive(
                new MultiplayerPacket(request.Type, JsonUtility.ToJson(envelope), request.Payload)
            );
            peers.Host.Pump();

            Assert.IsInstanceOf<InvalidDataException>(peers.Host.Failure);
            Assert.IsEmpty(peers.Actors);
        }

        [Test]
        public void Receive_ProjectionIdentityDisagreesWithPayload_RejectsBeforeApplyingEffects()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            MultiplayerPacket packet = peers.ToGuest.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(packet.Metadata);
            envelope.sequence++;

            peers.Guest.Receive(
                new MultiplayerPacket(packet.Type, JsonUtility.ToJson(envelope), packet.Payload)
            );
            peers.Guest.Pump();

            Assert.IsInstanceOf<InvalidDataException>(peers.Guest.Failure);
            Assert.IsEmpty(peers.Applied);
            Assert.IsTrue(peers.Host.CanAdvance);
        }

        [Test]
        public void Receive_ReceiptAcknowledgesFutureProjection_RejectsUnpublishedIdentity()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            peers.Guest.Receive(peers.ToGuest.Dequeue());
            peers.Guest.Pump();
            MultiplayerPacket packet = peers.ToHost.Dequeue();
            WireEnvelope envelope = JsonUtility.FromJson<WireEnvelope>(packet.Metadata);
            envelope.sequence++;

            peers.Host.Receive(new MultiplayerPacket(packet.Type, JsonUtility.ToJson(envelope)));

            Assert.IsInstanceOf<InvalidDataException>(peers.Host.Failure);
            Assert.IsFalse(peers.Host.CanAdvance);
            Assert.AreEqual(1, peers.Captures);
        }

        [Test]
        public void AcknowledgeMovie_OnlyOnePlayerFinished_RetainsSharedPause()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.HostMovieID = peers.GuestMovieID = 7;
            peers.Guest.AcknowledgeMovie(7);
            peers.Deliver();
            Assert.AreEqual(7, peers.HostMovieID);
            Assert.Zero(peers.CompletedMovies);

            peers.Host.AcknowledgeMovie(7);
            peers.Host.Pump();
            peers.Guest.AcknowledgeMovie(7);
            peers.Host.Pump();

            Assert.Zero(peers.HostMovieID);
            Assert.AreEqual(1, peers.CompletedMovies);
            peers.Host.PublishFrame();
            peers.Deliver();
            Assert.Zero(peers.GuestMovieID);
        }

        [Test]
        public void Dispose_UnconfirmedCommand_CompletesWithUnknownOutcome()
        {
            using PeerPair peers = new();
            peers.Ready();
            var completion = peers.Guest.ExecuteAsync(
                new TacticalCombatNetworkCommand
                {
                    Kind = TacticalCombatNetworkCommandKind.Withdrawal,
                }
            );

            peers.Guest.Dispose();

            Assert.IsFalse(completion.Result.OutcomeKnown);
            Assert.IsFalse(completion.Result.Accepted);
        }

        [Test]
        public void PublishFrame_TransportFailure_StopsWithoutAdvancingOrCapturingAgain()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.RejectHostSends = true;

            peers.Host.PublishFrame();
            peers.Host.PublishFrame();

            Assert.IsInstanceOf<IOException>(peers.Host.Failure);
            Assert.IsFalse(peers.Host.CanAdvance);
            Assert.AreEqual(1, peers.Captures);
        }

        [Test]
        public void Receive_LostAndReorderedReplaceableState_AppliesNewestCompleteState()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            peers.Deliver();
            peers.Host.PublishFrame();
            MultiplayerPacket dropped = peers.ToGuest.Dequeue();
            peers.Host.PublishFrame();
            MultiplayerPacket newest = peers.ToGuest.Dequeue();
            Assert.AreEqual(MultiplayerMessageType.TacticalFrame, dropped.Type);

            peers.Guest.Receive(newest);
            peers.Guest.Receive(dropped);
            peers.Guest.Receive(newest);
            peers.Deliver();

            CollectionAssert.AreEqual(
                new long[] { 1, 3 },
                peers.Applied.Select(frame => frame.Sequence)
            );
            Assert.IsNull(peers.Guest.Failure);
            Assert.IsTrue(peers.Host.CanAdvance);
        }

        [Test]
        public void Receive_ReplaceableStateOvertakesReady_RetainsNewestUntilAdmission()
        {
            using PeerPair peers = new();
            peers.Host.Ready();
            MultiplayerPacket hostReady = peers.ToGuest.Dequeue();
            peers.Guest.Ready();
            peers.Host.Receive(peers.ToHost.Dequeue());
            peers.Host.PublishFrame();
            MultiplayerPacket initial = peers.ToGuest.Dequeue();
            peers.Host.PublishFrame();
            peers.Guest.Receive(peers.ToGuest.Dequeue());

            peers.Guest.Pump();

            Assert.IsNull(peers.Guest.Failure);
            Assert.IsEmpty(peers.Applied);
            peers.Guest.Receive(hostReady);
            peers.Guest.Pump();
            Assert.AreEqual(2, peers.Applied.Single().Sequence);
            peers.Guest.Receive(initial);
            peers.Deliver();
            Assert.AreEqual(1, peers.Applied.Count);
            Assert.IsNull(peers.Host.Failure);
        }

        [Test]
        public void Receive_EffectsOutliveLostState_WaitsForNewerStateAndAppliesExactlyOnce()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            peers.Deliver();
            peers.NextEffects = Notices(11, 12);
            peers.Host.PublishFrame();
            Assert.AreEqual(MultiplayerMessageType.TacticalFrame, peers.ToGuest.Dequeue().Type);
            MultiplayerPacket effects = peers.ToGuest.Dequeue();

            peers.Guest.Receive(effects);
            peers.Guest.Pump();

            Assert.IsEmpty(peers.Effects);
            peers.Host.PublishFrame();
            peers.Deliver();
            peers.Guest.Receive(effects);
            peers.Deliver();
            CollectionAssert.AreEqual(
                new[] { 11, 12 },
                peers.Effects.Select(effect => effect.MessageID)
            );
            Assert.IsTrue(peers.Applied.All(frame => frame.Effects.Length == 0));
            CollectionAssert.AreEqual(
                new[] { "state:1", "state:3", "effect:11", "effect:12" },
                peers.PresentationOrder
            );
        }

        [Test]
        public void Receive_EffectSequenceGap_FailsBeforePresentation()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.NextEffects = Notices(11);
            peers.Host.PublishFrame();
            peers.Guest.Receive(peers.ToGuest.Dequeue());
            MultiplayerPacket packet = peers.ToGuest.Dequeue();
            WireEnvelope metadata = JsonUtility.FromJson<WireEnvelope>(packet.Metadata);
            metadata.effectSequence++;

            peers.Guest.Receive(
                new MultiplayerPacket(packet.Type, JsonUtility.ToJson(metadata), packet.Payload)
            );
            peers.Guest.Pump();

            Assert.IsInstanceOf<InvalidDataException>(peers.Guest.Failure);
            Assert.IsEmpty(peers.Effects);
        }

        [Test]
        public void Receive_DuplicateEffectsChangeContents_RejectsWithoutReplaying()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.NextEffects = Notices(11);
            peers.Host.PublishFrame();
            MultiplayerPacket packet = peers.ToGuest.Last();
            peers.Deliver();

            peers.Guest.Receive(
                new MultiplayerPacket(
                    packet.Type,
                    packet.Metadata,
                    MultiplayerTacticalFrameCodec.EncodeEffects(Notices(12))
                )
            );
            peers.Guest.Pump();

            Assert.IsInstanceOf<InvalidDataException>(peers.Guest.Failure);
            CollectionAssert.AreEqual(
                new[] { 11 },
                peers.Effects.Select(effect => effect.MessageID)
            );
        }

        [TestCase(0.1)]
        [TestCase(0.10000000000000002)]
        [TestCase(1234.5678901234567)]
        [TestCase(16777216.100000001)]
        [TestCase(1234567890.1234567)]
        public void Receive_FractionalTerminalTimestamp_CompletesAtTheFinalBinaryStateCursor(
            double timestamp
        )
        {
            using PeerPair peers = new(delayEffects: true);
            peers.Milliseconds = timestamp;
            peers.Ready();
            peers.Complete = true;
            peers.NextEffects = Notices(21);
            peers.Host.PublishFrame();
            peers.Deliver();

            peers.PresentationMilliseconds = peers.Applied.Single().TimestampMilliseconds;
            peers.Deliver();

            Assert.IsNull(peers.Host.Failure);
            Assert.IsNull(peers.Guest.Failure);
            Assert.AreEqual(1, peers.Effects.Count);
            Assert.IsTrue(peers.Host.TerminalFrameAcknowledged);
        }

        [Test]
        public void Receive_TerminalCheckpoint_WaitsForReliableEffectsAndPresentationCursor()
        {
            using PeerPair peers = new(delayEffects: true);
            peers.Ready();
            peers.Milliseconds = 100;
            peers.Complete = true;
            peers.NextEffects = Notices(21);
            peers.Host.PublishFrame();
            MultiplayerPacket checkpoint = peers.ToGuest.Dequeue();
            MultiplayerPacket effects = peers.ToGuest.Dequeue();
            Assert.AreEqual(MultiplayerMessageType.TacticalCheckpoint, checkpoint.Type);

            peers.Guest.Receive(checkpoint);
            peers.Guest.Pump();

            Assert.IsTrue(peers.Applied.Single().Complete);
            Assert.IsEmpty(peers.ToHost);
            Assert.IsFalse(peers.Host.TerminalFrameAcknowledged);
            peers.Guest.Receive(effects);
            peers.Deliver();
            Assert.IsEmpty(peers.Effects);
            Assert.IsFalse(peers.Host.TerminalFrameAcknowledged);
            peers.PresentationMilliseconds = 100;
            peers.Deliver();
            Assert.AreEqual(1, peers.Effects.Count);
            Assert.IsTrue(peers.Host.TerminalFrameAcknowledged);
            peers.Guest.Receive(checkpoint);
            peers.Guest.Receive(effects);
            peers.Deliver();
            Assert.AreEqual(1, peers.Applied.Count);
            Assert.AreEqual(1, peers.Effects.Count);
            Assert.IsTrue(peers.Host.TerminalFrameAcknowledged);
            peers.Host.PublishFrame();
            Assert.AreEqual(1, peers.Captures);
        }

        [Test]
        public void Receive_TerminalStateOvertakesCheckpoint_DoesNotAcknowledgeWithoutReliableFence()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Complete = true;
            peers.NextEffects = Notices(21);
            peers.Host.PublishFrame();
            MultiplayerPacket checkpoint = peers.ToGuest.Dequeue();
            MultiplayerPacket effects = peers.ToGuest.Dequeue();

            peers.Guest.Receive(
                new MultiplayerPacket(
                    MultiplayerMessageType.TacticalFrame,
                    checkpoint.Metadata,
                    checkpoint.Payload
                )
            );
            peers.Guest.Pump();
            peers.Guest.Receive(effects);
            peers.Deliver();

            Assert.IsTrue(peers.Applied.Single().Complete);
            Assert.AreEqual(1, peers.Effects.Count);
            Assert.IsFalse(peers.Host.TerminalFrameAcknowledged);
            peers.Guest.Receive(checkpoint);
            peers.Deliver();
            Assert.IsTrue(peers.Host.TerminalFrameAcknowledged);
            Assert.AreEqual(1, peers.Applied.Count);
        }

        [Test]
        public void PublishFrame_MovieTransition_SendsReliableStateBeforeMovieEffect()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            peers.Deliver();
            peers.HostMovieID = 7;
            peers.NextEffects = new[]
            {
                new TacticalCombatNetworkEffect
                {
                    Kind = TacticalCombatNetworkEffectKind.Movie,
                    MovieID = 7,
                },
            };

            peers.Host.PublishFrame();

            CollectionAssert.AreEqual(
                new[]
                {
                    MultiplayerMessageType.TacticalCheckpoint,
                    MultiplayerMessageType.TacticalEffects,
                },
                peers.ToGuest.Select(packet => packet.Type)
            );
            peers.Deliver();
            Assert.AreEqual(7, peers.Applied.Last().MovieID);
            Assert.AreEqual(7, peers.Effects.Single().MovieID);
        }

        [Test]
        public void Pump_PeriodicRecovery_ReusesCapturedStateWithoutConsumingPendingEffects()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            peers.Deliver();
            peers.NextEffects = Notices(31);
            peers.Milliseconds = 1000;

            peers.Host.Pump();

            Assert.AreEqual(1, peers.Captures);
            Assert.AreEqual(MultiplayerMessageType.TacticalCheckpoint, peers.ToGuest.Single().Type);
            peers.Deliver();
            Assert.IsEmpty(peers.Effects);
            peers.Host.PublishFrame();
            peers.Deliver();
            Assert.AreEqual(2, peers.Captures);
            Assert.AreEqual(31, peers.Effects.Single().MessageID);
        }

        [Test]
        public void Receive_RepeatedResyncRequests_RateLimitsCachedRecoveryWithoutRecapture()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            string metadata = peers.ToGuest.Peek().Metadata;
            peers.Deliver();
            peers.NextEffects = Notices(31);
            peers.Milliseconds = 1000;

            for (int attempt = 0; attempt < 100; attempt++)
                peers.Host.Receive(
                    new MultiplayerPacket(MultiplayerMessageType.TacticalResync, metadata)
                );

            Assert.AreEqual(1, peers.Captures);
            Assert.AreEqual(1, peers.ToGuest.Count);
            peers.Deliver();
            peers.Host.PublishFrame();
            peers.Deliver();
            Assert.AreEqual(31, peers.Effects.Single().MessageID);
        }

        [Test]
        public void Pump_MissingState_RequestsResyncAtBoundedIntervals()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Milliseconds = 2000;

            for (int attempt = 0; attempt < 100; attempt++)
                peers.Guest.Pump();

            Assert.AreEqual(MultiplayerMessageType.TacticalResync, peers.ToHost.Single().Type);
            peers.Milliseconds = 4000;
            peers.Guest.Pump();
            Assert.AreEqual(2, peers.ToHost.Count);
        }

        [Test]
        public void ExecuteAsync_TooManyPendingCommands_FailsAllWaitersWithoutMutation()
        {
            using PeerPair peers = new();
            peers.Ready();
            var first = peers.Guest.ExecuteAsync(new TacticalCombatNetworkCommand());
            for (
                int index = 1;
                index <= MultiplayerTacticalProtocol.MaximumPendingCommands;
                index++
            )
                peers.Guest.ExecuteAsync(new TacticalCombatNetworkCommand());

            Assert.IsInstanceOf<InvalidDataException>(peers.Guest.Failure);
            Assert.IsFalse(first.Result.OutcomeKnown);
            Assert.IsEmpty(peers.Actors);
        }

        [Test]
        public void Receive_TooManyQueuedReliableEvents_FailsBeforeCommandMutation()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Guest.ExecuteAsync(new TacticalCombatNetworkCommand());
            MultiplayerPacket packet = peers.ToHost.Dequeue();

            for (int index = 0; index <= MultiplayerTacticalProtocol.MaximumQueuedEvents; index++)
                peers.Host.Receive(packet);

            Assert.IsInstanceOf<InvalidDataException>(peers.Host.Failure);
            Assert.IsEmpty(peers.Actors);
        }

        [Test]
        public void Receive_TooManyEffectsWaitingForLostState_FailsBoundedRetention()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            peers.Deliver();

            for (
                int index = 0;
                index <= MultiplayerTacticalProtocol.MaximumPendingEffectBatches;
                index++
            )
            {
                peers.NextEffects = Notices(index);
                peers.Host.PublishFrame();
                peers.ToGuest.Dequeue();
                peers.Guest.Receive(peers.ToGuest.Dequeue());
                peers.Guest.Pump();
            }

            Assert.IsInstanceOf<InvalidDataException>(peers.Guest.Failure);
            Assert.IsEmpty(peers.Effects);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Pump_ReadinessDeadline_FailsWithUnknownPendingOutcome(bool locallyReady)
        {
            using PeerPair peers = new();
            if (locallyReady)
                peers.Host.Ready();
            var pending = peers.Host.ExecuteAsync(new TacticalCombatNetworkCommand());
            peers.Milliseconds = MultiplayerTacticalProtocol.ReadinessTimeoutMilliseconds;

            peers.Host.Pump();

            Assert.IsInstanceOf<TimeoutException>(peers.Host.Failure);
            Assert.IsFalse(pending.Result.OutcomeKnown);
            Assert.IsEmpty(peers.Actors);
        }

        [Test]
        public void Pump_PeerInactivityDeadline_StopsAuthorityThroughFaultedEventOnce()
        {
            using PeerPair peers = new();
            peers.Ready();
            int faults = 0;
            peers.Host.Faulted += _ => faults++;
            peers.Milliseconds = MultiplayerTacticalProtocol.InactivityTimeoutMilliseconds;

            peers.Host.Pump();
            peers.Host.Pump();

            Assert.IsInstanceOf<TimeoutException>(peers.Host.Failure);
            Assert.IsFalse(peers.Host.CanAdvance);
            Assert.AreEqual(1, faults);
        }

        [Test]
        public void Pump_TerminalReceiptDeadline_FailsDespiteOtherPeerTraffic()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Complete = true;
            peers.Host.PublishFrame();
            string metadata = peers.ToGuest.Peek().Metadata;
            peers.Milliseconds = MultiplayerTacticalProtocol.TerminalTimeoutMilliseconds;
            peers.Host.Receive(
                new MultiplayerPacket(MultiplayerMessageType.TacticalReady, metadata)
            );

            peers.Host.Pump();

            Assert.IsInstanceOf<TimeoutException>(peers.Host.Failure);
            Assert.IsFalse(peers.Host.TerminalFrameAcknowledged);
        }

        [Test]
        public void Pump_CommandConfirmationDeadline_FailsDespiteOtherPeerTraffic()
        {
            using PeerPair peers = new();
            peers.Ready();
            var pending = peers.Guest.ExecuteAsync(new TacticalCombatNetworkCommand());
            peers.Host.PublishFrame();
            string metadata = peers.ToGuest.Dequeue().Metadata;
            peers.Milliseconds = MultiplayerTacticalProtocol.InactivityTimeoutMilliseconds;
            peers.Guest.Receive(
                new MultiplayerPacket(MultiplayerMessageType.TacticalReady, metadata)
            );

            peers.Guest.Pump();

            Assert.IsInstanceOf<TimeoutException>(peers.Guest.Failure);
            Assert.IsFalse(pending.Result.OutcomeKnown);
        }

        [Test]
        public void Receive_OversizedCommandPayload_FailsBeforeDecodingOrMutation()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Guest.ExecuteAsync(new TacticalCombatNetworkCommand());
            MultiplayerPacket packet = peers.ToHost.Dequeue();

            peers.Host.Receive(
                new MultiplayerPacket(
                    packet.Type,
                    packet.Metadata,
                    new byte[MultiplayerTacticalProtocol.MaximumCommandBytes + 1]
                )
            );

            Assert.IsInstanceOf<InvalidDataException>(peers.Host.Failure);
            Assert.IsEmpty(peers.Actors);
        }

        [Test]
        public void Receive_StateContainsUnreliableEffects_RejectsBeforeApplication()
        {
            using PeerPair peers = new();
            peers.Ready();
            peers.Host.PublishFrame();
            MultiplayerPacket packet = peers.ToGuest.Dequeue();
            TacticalCombatNetworkFrame frame = MultiplayerTacticalFrameCodec.Decode(packet.Payload);
            frame.Effects = Notices(41);

            peers.Guest.Receive(
                new MultiplayerPacket(
                    packet.Type,
                    packet.Metadata,
                    MultiplayerTacticalFrameCodec.Encode(frame)
                )
            );
            peers.Guest.Pump();

            Assert.IsInstanceOf<InvalidDataException>(peers.Guest.Failure);
            Assert.IsEmpty(peers.Applied);
            Assert.IsEmpty(peers.Effects);
        }

        /// <summary>
        /// Creates ordered notification effects whose identifiers make loss or replay observable.
        /// </summary>
        /// <param name="messages">The notification identifiers in publication order.</param>
        /// <returns>A detached typed presentation batch.</returns>
        private static TacticalCombatNetworkEffect[] Notices(params int[] messages) =>
            messages
                .Select(message => new TacticalCombatNetworkEffect
                {
                    Kind = TacticalCombatNetworkEffectKind.Notification,
                    MessageID = message,
                })
                .ToArray();

        [Serializable]
        private sealed class WireEnvelope
        {
            public string initializationID;
            public string battleID;
            public long requestID;
            public long sequence;
            public long effectSequence;
            public double timestampMilliseconds;
            public MultiplayerRole actor;
            public bool accepted;
            public int movieID;
        }

        private sealed class PeerPair : IDisposable
        {
            public Queue<MultiplayerPacket> ToHost { get; } = new();
            public Queue<MultiplayerPacket> ToGuest { get; } = new();
            public List<CombatSide> Actors { get; } = new();
            public List<TacticalCombatNetworkFrame> Applied { get; } = new();
            public List<TacticalCombatNetworkEffect> Effects { get; } = new();
            public List<string> PresentationOrder { get; } = new();
            public TacticalCombatNetworkEffect[] NextEffects { get; set; } =
                Array.Empty<TacticalCombatNetworkEffect>();
            public bool Complete { get; set; }
            public double Milliseconds { get; set; }
            public double PresentationMilliseconds { get; set; }
            public MultiplayerTacticalProtocol Host { get; }
            public MultiplayerTacticalProtocol Guest { get; }
            public int Captures { get; private set; }
            public int HostMovieID { get; set; }
            public int GuestMovieID { get; set; }
            public int CompletedMovies { get; private set; }
            public bool RejectHostSends { get; set; }

            /// <summary>
            /// Creates authenticated peers sharing one battle identity and queued transport.
            /// </summary>
            /// <param name="delayEffects">Whether reliable effects wait for the controlled presentation cursor.</param>
            public PeerPair(bool delayEffects = false)
            {
                string initialization = Guid.NewGuid().ToString("N");
                string battle = Guid.NewGuid().ToString("N");
                Host = new MultiplayerTacticalProtocol(
                    MultiplayerRole.Host,
                    initialization,
                    battle,
                    CombatSide.Attacker,
                    (actor, _) =>
                    {
                        Actors.Add(actor);
                        return true;
                    },
                    sequence =>
                    {
                        Captures++;
                        TacticalCombatNetworkFrame frame = new()
                        {
                            Sequence = sequence,
                            MovieID = HostMovieID,
                            Complete = Complete,
                            Effects = NextEffects,
                        };
                        NextEffects = Array.Empty<TacticalCombatNetworkEffect>();
                        return frame;
                    },
                    _ => Assert.Fail("The host must not import a replica frame."),
                    _ => Assert.Fail("The host must not import replica effects."),
                    () => HostMovieID,
                    id =>
                    {
                        Assert.AreEqual(HostMovieID, id);
                        HostMovieID = 0;
                        CompletedMovies++;
                        return true;
                    },
                    packet =>
                    {
                        if (RejectHostSends)
                            throw new IOException("Transport failed.");
                        ToGuest.Enqueue(packet);
                    },
                    () => Milliseconds
                );
                Guest = new MultiplayerTacticalProtocol(
                    MultiplayerRole.Guest,
                    initialization,
                    battle,
                    CombatSide.Attacker,
                    (_, _) =>
                        throw new AssertionException("The guest must not execute combat commands."),
                    _ =>
                        throw new AssertionException(
                            "The guest must not capture authoritative combat."
                        ),
                    frame =>
                    {
                        Applied.Add(frame);
                        GuestMovieID = frame.MovieID;
                        PresentationOrder.Add($"state:{frame.Sequence}");
                    },
                    effects =>
                    {
                        Effects.AddRange(effects);
                        foreach (TacticalCombatNetworkEffect effect in effects)
                            PresentationOrder.Add($"effect:{effect.MessageID}");
                    },
                    () => GuestMovieID,
                    _ =>
                        throw new AssertionException(
                            "The guest must not release the authoritative movie."
                        ),
                    ToHost.Enqueue,
                    () => Milliseconds,
                    delayEffects ? () => PresentationMilliseconds : null
                );
            }

            /// <summary>
            /// Announces both local views as ready and delivers their readiness exchange.
            /// </summary>
            public void Ready()
            {
                Host.Ready();
                Guest.Ready();
                Deliver();
            }

            /// <summary>
            /// Drains the bounded ordered packet exchange without advancing combat.
            /// </summary>
            public void Deliver()
            {
                for (int round = 0; round < 8; round++)
                {
                    while (ToHost.Count != 0)
                        Host.Receive(ToHost.Dequeue());
                    Host.Pump();
                    while (ToGuest.Count != 0)
                        Guest.Receive(ToGuest.Dequeue());
                    Guest.Pump();
                    if (ToHost.Count == 0 && ToGuest.Count == 0)
                        return;
                }
                Assert.Fail("The battle packet exchange did not settle.");
            }

            /// <summary>
            /// Releases both protocol endpoints and their pending requests.
            /// </summary>
            public void Dispose()
            {
                Host.Dispose();
                Guest.Dispose();
            }
        }
    }
}
