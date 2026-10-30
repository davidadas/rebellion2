using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.PlayerRequests;
using Rebellion.Multiplayer;
using UnityEngine;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class MultiplayerGameSessionTests
    {
        private const string _hostFactionID = "FNALL1";
        private const string _guestFactionID = "FNEMP1";

        [Test]
        public void Receive_PreviousInitializationCommit_DoesNotMutateCurrentGuest()
        {
            using PeerPair previous = new();
            previous.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Previous initialization" }
            );
            previous.Host.Pump();
            MultiplayerPacket delayed = previous.ToGuest.Dequeue();
            using PeerPair current = new();
            string name = current
                .GuestGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship")
                .DisplayName;

            current.Guest.Receive(delayed);
            current.Guest.Pump();

            Assert.IsNull(current.Guest.Failure);
            Assert.AreEqual(
                name,
                current.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
        }

        [Test]
        public void Receive_PreviousInitializationDay_DoesNotStartCurrentGuestDay()
        {
            using PeerPair previous = new();
            Assert.IsTrue(previous.Host.TryBeginTick(true));
            MultiplayerPacket delayed = previous.ToGuest.Dequeue();
            using PeerPair current = new();

            current.Guest.Receive(delayed);
            current.Guest.Pump();

            Assert.IsFalse(current.Guest.TryBeginTick(false));
            Assert.IsNull(current.Guest.Failure);
            Assert.Zero(current.GuestGame.CurrentTick);
        }

        [Test]
        public void Receive_PreviousInitializationFailure_DoesNotFaultCurrentSession()
        {
            using PeerPair previous = new();
            previous.Host.ReportFailure(new InvalidOperationException("Previous initialization"));
            MultiplayerPacket delayed = previous.ToGuest.Dequeue();
            using PeerPair current = new();

            current.Guest.Receive(delayed);
            current.Guest.Pump();

            Assert.IsNull(current.Guest.Failure);
        }

        [Test]
        public void Receive_UnadmittedFutureDayReceipt_FaultsBeforeAdvancing()
        {
            using PeerPair peers = new();
            MultiplayerPacket invalid = new(
                MultiplayerMessageType.DayReceipt,
                JsonUtility.ToJson(
                    new PacketMetadata { initializationID = peers.InitializationID, tick = 10 }
                )
            );

            peers.Host.Receive(invalid);

            Assert.IsNotNull(peers.Host.Failure);
            Assert.IsFalse(peers.Host.TryBeginTick(true));
            Assert.Zero(peers.HostGame.CurrentTick);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Pump_DivergentUnitState_StopsBeforeAnotherDay(bool changeCombatStatistic)
        {
            using PeerPair peers = new();
            CapitalShip ship = peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship");
            if (changeCombatStatistic)
                ship.DamageControl++;
            else
                ship.AssignName("Different peer state");
            Assert.AreNotEqual(
                MultiplayerSnapshotCodec.ComputeStateFingerprint(peers.HostGame),
                MultiplayerSnapshotCodec.ComputeStateFingerprint(peers.GuestGame)
            );
            Assert.IsTrue(peers.Host.TryBeginTick(true));
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsTrue(peers.Guest.TryBeginTick(false));
            peers.HostSimulation.Tick.ProcessTick();
            peers.GuestSimulation.Tick.ProcessTick();
            peers.Flush();

            Assert.IsTrue(peers.Host.Failure != null || peers.Guest.Failure != null);
            Assert.IsFalse(peers.Host.CanBeginHostTick);
        }

        [Test]
        public void ReplaceGame_RemovedUnitThenSameAddition_PreservesVerification()
        {
            using PeerPair peers = new();
            Planet planet = peers.HostGame.GetSceneNodeByInstanceID<Planet>("host-planet");
            peers.HostGame.AttachNode(
                new Regiment { InstanceID = "removed", OwnerInstanceID = _hostFactionID },
                planet
            );
            peers.HostGame.AttachNode(
                new Regiment { InstanceID = "retained", OwnerInstanceID = _hostFactionID },
                planet
            );
            peers.HostGame.DeleteNode(peers.HostGame.GetSceneNodeByInstanceID<Regiment>("removed"));
            peers
                .HostSimulation.ExecuteCommandAsync(() =>
                {
                    peers.HostSimulation.Pipeline.ProcessResults(
                        Array.Empty<GameResult>(),
                        processMessages: false
                    );
                    return true;
                })
                .GetAwaiter()
                .GetResult();
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(peers.HostSimulation);
            peers.GuestSimulation.ReplaceGame(
                MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash)
            );
            foreach (GameRoot game in new[] { peers.HostGame, peers.GuestGame })
                game.AttachNode(
                    new Regiment { InstanceID = "added", OwnerInstanceID = _hostFactionID },
                    game.GetSceneNodeByInstanceID<Planet>("host-planet")
                );
            foreach (GameSession session in new[] { peers.HostSimulation, peers.GuestSimulation })
                session
                    .ExecuteCommandAsync(() =>
                    {
                        session.Pipeline.ProcessResults(
                            Array.Empty<GameResult>(),
                            processMessages: false
                        );
                        return true;
                    })
                    .GetAwaiter()
                    .GetResult();
            peers.AssertSharedState();
            Assert.DoesNotThrow(() =>
                MultiplayerStateFingerprint.VerifyLoadedGame(
                    peers.GuestSimulation,
                    MultiplayerStateFingerprint.ComputeSimulation(peers.HostSimulation)
                )
            );
        }

        [Test]
        public async Task Pump_ExistingWindowDispatchers_ReplicatesBothActorsAcrossTwentyDaysAsync()
        {
            using PeerPair peers = new();
            SessionPlayerRequestDispatcher hostBinding = new(peers.Host, () => true);
            SessionPlayerRequestDispatcher guestBinding = new(peers.Guest, () => true);
            IPlayerRequestDispatcher hostWindow = hostBinding;
            IPlayerRequestDispatcher guestWindow = guestBinding;

            Task<PlayerRequestOutcome> hostOrder = hostWindow.ExecuteAsync(
                new CreateFleetRequest { ItemIDs = new[] { "host-ship" } }
            );
            Task<PlayerRequestOutcome> guestOrder = guestWindow.ExecuteAsync(
                new CreateFleetRequest { ItemIDs = new[] { "guest-ship" } }
            );
            peers.Flush();
            Assert.IsTrue((await hostOrder).Accepted);
            Assert.IsTrue((await guestOrder).Accepted);
            peers.AssertSharedState();

            Task<PlayerRequestOutcome> hostChat = hostWindow.ExecuteAsync(
                new SendPeerChatRequest { Text = "Host window message" }
            );
            Task<PlayerRequestOutcome> guestChat = guestWindow.ExecuteAsync(
                new SendPeerChatRequest { Text = "Guest window message" }
            );
            peers.Flush();
            Assert.IsTrue((await hostChat).Accepted);
            Assert.IsTrue((await guestChat).Accepted);
            peers.AssertSharedState();

            for (int day = 0; day < 20; day++)
            {
                Assert.IsTrue(peers.Host.TryBeginTick(true));
                peers.DeliverToGuest();
                peers.Guest.Pump();
                Assert.IsTrue(peers.Guest.TryBeginTick(false));
                peers.HostSimulation.Tick.ProcessTick();
                peers.GuestSimulation.Tick.ProcessTick();
                peers.Flush();
                peers.AssertSharedState();
            }

            Assert.AreEqual(20, peers.HostGame.CurrentTick);
            Assert.AreEqual(20, peers.GuestGame.CurrentTick);
        }

        [Test]
        public void ExecuteAsync_HostRequestBeforePump_DoesNotMutateOrComplete()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> request = peers.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Host order" }
            );

            Assert.IsFalse(request.IsCompleted);
            Assert.AreEqual(
                "host-ship",
                peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
            Assert.IsEmpty(peers.ToGuest);
        }

        [Test]
        public void ExecuteAsync_DelayedGuestRequest_CompletesOnlyAfterGuestExecutesCommit()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new RenameRequest { EntityID = "guest-ship", Name = "Guest order" }
            );
            Assert.IsFalse(request.IsCompleted);
            peers.DeliverToHost();
            peers.Host.Pump();

            Assert.IsFalse(request.IsCompleted);
            Assert.AreEqual(
                "Guest order",
                peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship").DisplayName
            );
            Assert.AreEqual(
                "guest-ship",
                peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship").DisplayName
            );
            peers.DeliverToGuest();
            Assert.IsFalse(request.IsCompleted);
            peers.Guest.Pump();

            Assert.IsTrue(request.IsCompletedSuccessfully);
            Assert.IsTrue(Completed(request).Accepted);
            Assert.AreEqual(
                "Guest order",
                peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship").DisplayName
            );
        }

        [Test]
        public void Pump_ConcurrentActors_UpdatesExistingGraphsInHostOrder()
        {
            using PeerPair peers = new();
            GameRoot hostGraph = peers.HostGame;
            GameRoot guestGraph = peers.GuestGame;
            CapitalShip retainedGuestShip = guestGraph.GetSceneNodeByInstanceID<CapitalShip>(
                "guest-ship"
            );
            Task<PlayerRequestOutcome> guestRequest = peers.Guest.ExecuteAsync(
                new RenameRequest { EntityID = "guest-ship", Name = "Guest name" }
            );
            Task<PlayerRequestOutcome> hostRequest = peers.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Host name" }
            );

            peers.Flush();

            Assert.IsTrue(Completed(hostRequest).Accepted);
            Assert.IsTrue(Completed(guestRequest).Accepted);
            Assert.AreSame(hostGraph, peers.HostSimulation.Game);
            Assert.AreSame(guestGraph, peers.GuestSimulation.Game);
            Assert.AreSame(
                retainedGuestShip,
                peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship")
            );
            peers.AssertSharedState();
        }

        [Test]
        public void Pump_RequestDuringSuspendedTick_WaitsForBothSimulationBoundaries()
        {
            using PeerPair peers = new();
            Assert.IsTrue(peers.Host.TryBeginTick(true));
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsTrue(peers.Guest.TryBeginTick(false));
            IEnumerator hostTick = peers.HostSimulation.Tick.ProcessTickIncrementally();
            IEnumerator guestTick = peers.GuestSimulation.Tick.ProcessTickIncrementally();
            try
            {
                Assert.IsTrue(hostTick.MoveNext());
                Assert.IsTrue(guestTick.MoveNext());
                Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                    new RenameRequest { EntityID = "guest-ship", Name = "After tick" }
                );
                peers.Flush();

                Assert.IsFalse(request.IsCompleted);
                Assert.AreEqual(
                    "guest-ship",
                    peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship").DisplayName
                );
                while (hostTick.MoveNext()) { }
                peers.Flush();
                Assert.IsFalse(request.IsCompleted);
                Assert.AreEqual(
                    "guest-ship",
                    peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship").DisplayName
                );

                while (guestTick.MoveNext()) { }
                peers.Flush();

                Assert.IsTrue(Completed(request).Accepted);
                Assert.AreEqual(
                    "After tick",
                    peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship").DisplayName
                );
                Assert.IsTrue(peers.Host.CanBeginHostTick);
                peers.AssertSharedState();
            }
            finally
            {
                (hostTick as IDisposable)?.Dispose();
                (guestTick as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void Pump_GuestTargetsHostEntity_RejectsWithoutStoppingSession()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Unauthorized" }
            );

            peers.Flush();

            Assert.IsFalse(Completed(request).Accepted);
            Assert.AreEqual(
                "host-ship",
                peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
            Assert.IsNull(peers.Host.Failure);
            Assert.IsNull(peers.Guest.Failure);
            peers.AssertSharedState();
        }

        [Test]
        public void Pump_RejectedDomainOutcome_CommitsAndWaitsForReplayReceipt()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Unauthorized" }
            );
            peers.DeliverToHost();
            peers.Host.Pump();

            Assert.IsFalse(request.IsCompleted);
            Assert.AreEqual(MultiplayerMessageType.PlayerActionCommit, peers.ToGuest.Single().Type);
            Assert.IsFalse(peers.Host.CanBeginHostTick);
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsFalse(Completed(request).Accepted);
            Assert.IsFalse(peers.Host.CanBeginHostTick);
            peers.DeliverToHost();
            peers.Host.Pump();

            Assert.IsTrue(peers.Host.CanBeginHostTick);
            peers.AssertSharedState();
        }

        [Test]
        public void Receive_GuestClaimsHostRole_UsesAuthenticatedGuestOwnership()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Unauthorized" }
            );
            MultiplayerPacket packet = peers.ToHost.Dequeue();
            PacketMetadata metadata = JsonUtility.FromJson<PacketMetadata>(packet.Metadata);
            metadata.actor = MultiplayerRole.Host;

            peers.Host.Receive(
                new MultiplayerPacket(packet.Type, JsonUtility.ToJson(metadata), packet.Payload)
            );
            peers.Flush();

            Assert.IsFalse(Completed(request).Accepted);
            Assert.AreEqual(
                "host-ship",
                peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
            Assert.IsNull(peers.Host.Failure);
        }

        [Test]
        public void Pump_CreateFleet_ReplaysSameIdentityAndParentRelationships()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new CreateFleetRequest { ItemIDs = new[] { "guest-ship" } }
            );

            peers.Flush();

            PlayerRequestOutcome result = Completed(request);
            Assert.IsTrue(result.Accepted);
            string fleetID = result.CreatedEntityID;
            Assert.IsNotEmpty(fleetID);
            Assert.AreSame(
                peers.HostGame.GetSceneNodeByInstanceID<Fleet>(fleetID),
                peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship").GetParent()
            );
            Assert.AreSame(
                peers.GuestGame.GetSceneNodeByInstanceID<Fleet>(fleetID),
                peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship").GetParent()
            );
            peers.AssertSharedState();
        }

        [Test]
        public void Pump_ReadMessages_PreservesGuestMessageObject()
        {
            using PeerPair peers = new();
            Message retainedMessage = peers
                .GuestGame.GetFactionByOwnerInstanceID(_guestFactionID)
                .Messages[MessageType.Advice]
                .Single();
            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new ReadMessagesRequest { MessageIDs = new[] { "guest-message" } }
            );

            peers.Flush();

            Assert.IsTrue(Completed(request).Accepted);
            Assert.IsTrue(retainedMessage.Read);
            Assert.AreSame(
                retainedMessage,
                peers
                    .GuestGame.GetFactionByOwnerInstanceID(_guestFactionID)
                    .Messages[MessageType.Advice]
                    .Single()
            );
            Assert.IsFalse(
                peers
                    .GuestGame.GetFactionByOwnerInstanceID(_hostFactionID)
                    .Messages[MessageType.Advice]
                    .Single()
                    .Read
            );
            peers.AssertSharedState();
        }

        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public void Pump_SpeedProposalResponse_OrdersBothParticipantsIdentically(
            bool hostProposes,
            bool accept
        )
        {
            using PeerPair peers = new();
            peers.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Paused });
            peers.Guest.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Paused });
            peers.Flush();
            MultiplayerGameSession proposer = hostProposes ? peers.Host : peers.Guest;
            MultiplayerGameSession responder = hostProposes ? peers.Guest : peers.Host;
            string faction = hostProposes ? _hostFactionID : _guestFactionID;
            proposer.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast });
            peers.Flush();
            var response = responder.ExecuteAsync(
                new RespondToSpeedRequest
                {
                    RequestingFactionID = faction,
                    Speed = TickSpeed.Fast,
                    Revision = peers.HostGame.GetFactionPlayer(faction).GameSpeedSelectionRevision,
                    Accept = accept,
                }
            );
            peers.Flush();

            Assert.IsTrue(Completed(response).Accepted);
            Assert.AreEqual(
                accept ? TickSpeed.Fast : TickSpeed.Paused,
                peers.HostClock.GetGameSpeed()
            );
            Assert.AreEqual(peers.HostClock.GetGameSpeed(), peers.GuestClock.GetGameSpeed());
            peers.AssertSharedState();
        }

        [Test]
        public void Pump_ReplacedSpeedProposal_RejectsOldAcceptanceOnBothPeers()
        {
            using PeerPair peers = new();
            peers.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Paused });
            peers.Guest.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Paused });
            peers.Flush();
            peers.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast });
            peers.Flush();
            long revision = peers
                .HostGame.GetFactionPlayer(_hostFactionID)
                .GameSpeedSelectionRevision;
            peers.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Slow });
            peers.Flush();
            var response = peers.Guest.ExecuteAsync(
                new RespondToSpeedRequest
                {
                    RequestingFactionID = _hostFactionID,
                    Speed = TickSpeed.Fast,
                    Revision = revision,
                    Accept = true,
                }
            );
            peers.Flush();

            Assert.IsFalse(Completed(response).Accepted);
            Assert.AreEqual(TickSpeed.Paused, peers.HostClock.GetGameSpeed());
            peers.AssertSharedState();
        }

        [Test]
        public void Pump_SlowerGuestSelection_AppliesToBothPeersWithoutMatchingHostVote()
        {
            using PeerPair peers = new();
            peers.Guest.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.VerySlow });
            peers.Flush();
            Assert.AreEqual(TickSpeed.VerySlow, peers.HostClock.GetGameSpeed());
            Assert.AreEqual(TickSpeed.VerySlow, peers.GuestClock.GetGameSpeed());
            peers.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.VerySlow });
            peers.Flush();

            Assert.AreEqual(TickSpeed.VerySlow, peers.HostClock.GetGameSpeed());
            Assert.AreEqual(TickSpeed.VerySlow, peers.GuestClock.GetGameSpeed());
            peers.AssertSharedState();
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void Pump_OneParticipantPauses_StopsBothPeers(MultiplayerRole role)
        {
            using PeerPair peers = new();
            (role == MultiplayerRole.Host ? peers.Host : peers.Guest).ExecuteAsync(
                new SetSpeedRequest { Speed = TickSpeed.Paused }
            );

            peers.Flush();

            Assert.AreEqual(TickSpeed.Paused, peers.HostClock.GetGameSpeed());
            Assert.AreEqual(TickSpeed.Paused, peers.GuestClock.GetGameSpeed());
            peers.AssertSharedState();
        }

        [Test]
        public void Pump_OtherParticipantRequestsFast_DoesNotReleasePlayerPause()
        {
            using PeerPair peers = new();
            peers.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Paused });
            peers.Flush();
            peers.Guest.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Paused });
            peers.Flush();
            peers.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast });
            peers.Flush();

            Assert.AreEqual(TickSpeed.Paused, peers.HostClock.GetGameSpeed());
            Assert.AreEqual(TickSpeed.Paused, peers.GuestClock.GetGameSpeed());
            peers.AssertSharedState();
        }

        [Test]
        public void TryBeginTick_HostClockHasNotElapsed_DoesNotPublishDay()
        {
            using PeerPair peers = new();

            Assert.IsFalse(peers.Host.TryBeginTick(false));

            Assert.IsEmpty(peers.ToGuest);
            Assert.Zero(peers.HostGame.CurrentTick);
        }

        [Test]
        public void TryBeginTick_GuestHasNoHostBarrier_DoesNotAdvance()
        {
            using PeerPair peers = new();

            Assert.IsFalse(peers.Guest.TryBeginTick(true));

            Assert.Zero(peers.GuestGame.CurrentTick);
        }

        [Test]
        public void TryBeginTick_UnacknowledgedAction_BlocksNextDay()
        {
            using PeerPair peers = new();
            peers.Host.ExecuteAsync(new RenameRequest { EntityID = "host-ship", Name = "Ordered" });
            peers.Host.Pump();

            Assert.IsFalse(peers.Host.TryBeginTick(true));
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsFalse(peers.Host.TryBeginTick(true));
            peers.DeliverToHost();
            peers.Host.Pump();

            Assert.IsTrue(peers.Host.CanBeginHostTick);
        }

        [Test]
        public void Receive_HostDayAndReceiptArriveTogether_WaitsForGuestDayBeforeContinuing()
        {
            using PeerPair peers = new();
            Assert.IsTrue(peers.Host.TryBeginTick(true));
            peers.HostSimulation.Tick.ProcessTick();
            Assert.AreEqual(1, peers.HostGame.CurrentTick);
            Assert.IsFalse(peers.Host.TryBeginTick(true));

            peers.DeliverToGuest();
            peers.Guest.Pump();

            Assert.IsNull(peers.Guest.Failure);
            Assert.IsTrue(peers.Guest.TryBeginTick(false));
            peers.GuestSimulation.Tick.ProcessTick();
            peers.Flush();

            Assert.AreEqual(1, peers.GuestGame.CurrentTick);
            Assert.IsTrue(peers.Host.CanBeginHostTick);
            peers.AssertSharedState();
        }

        [Test]
        public void TryBeginTick_ThreeAcknowledgedDays_PreservesIndependentSimulationState()
        {
            using PeerPair peers = new();
            for (int index = 0; index < 3; index++)
            {
                Assert.IsTrue(peers.Host.TryBeginTick(true));
                peers.DeliverToGuest();
                peers.Guest.Pump();
                Assert.IsTrue(peers.Guest.TryBeginTick(false));
                peers.GuestSimulation.Tick.ProcessTick();
                peers.DeliverToHost();
                peers.Host.Pump();
                Assert.IsFalse(peers.Host.CanBeginHostTick);
                peers.HostSimulation.Tick.ProcessTick();
                peers.Flush();
                Assert.IsTrue(peers.Host.CanBeginHostTick);
                peers.AssertSharedState();
            }
            Assert.AreEqual(3, peers.HostGame.CurrentTick);
        }

        [Test]
        public void Receive_DuplicateGuestRequest_CreatesFleetOnce()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new CreateFleetRequest { ItemIDs = new[] { "guest-ship" } }
            );
            MultiplayerPacket packet = peers.ToHost.Dequeue();
            peers.Host.Receive(packet);
            peers.Host.Receive(packet);

            peers.Flush();

            Assert.IsTrue(Completed(request).Accepted);
            Assert.AreEqual(1, peers.HostGame.RuntimeIdentifierIndex);
            peers.AssertSharedState();
        }

        [Test]
        public void Receive_DuplicateCommit_AcknowledgesWithoutRepeatingToggle()
        {
            using PeerPair peers = new();
            peers.Host.ExecuteAsync(
                new ToggleAdvisorOptionRequest { Option = PlayerAdvisorOption.AgentAdvice }
            );
            peers.Host.Pump();
            MultiplayerPacket commit = peers.ToGuest.Dequeue();
            peers.Guest.Receive(commit);
            peers.Guest.Pump();
            bool appliedValue = peers
                .GuestGame.GetFactionByOwnerInstanceID(_hostFactionID)
                .AgentAdvice;
            peers.ToHost.Clear();

            peers.Guest.Receive(commit);
            peers.Guest.Pump();
            peers.Flush();

            Assert.AreEqual(
                appliedValue,
                peers.GuestGame.GetFactionByOwnerInstanceID(_hostFactionID).AgentAdvice
            );
            Assert.IsTrue(peers.Host.CanBeginHostTick);
            peers.AssertSharedState();
        }

        [Test]
        public void Receive_CommitSkipsSequence_FailsBeforeApplyingMutation()
        {
            using PeerPair peers = new();
            peers.Host.ExecuteAsync(new RenameRequest { EntityID = "host-ship", Name = "Skipped" });
            peers.Host.Pump();
            MultiplayerPacket commit = peers.ToGuest.Dequeue();
            PacketMetadata metadata = JsonUtility.FromJson<PacketMetadata>(commit.Metadata);
            metadata.sequence++;

            peers.Guest.Receive(
                new MultiplayerPacket(commit.Type, JsonUtility.ToJson(metadata), commit.Payload)
            );
            peers.Guest.Pump();

            Assert.IsNotNull(peers.Guest.Failure);
            StringAssert.Contains("sequence", peers.Guest.Failure.Message);
            Assert.AreEqual(
                "host-ship",
                peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
        }

        [Test]
        public void Receive_DuplicateDayBarrier_DoesNotStartAnotherDay()
        {
            using PeerPair peers = new();
            Assert.IsTrue(peers.Host.TryBeginTick(true));
            MultiplayerPacket barrier = peers.ToGuest.Dequeue();
            peers.Guest.Receive(barrier);
            peers.Guest.Receive(barrier);
            peers.Guest.Pump();
            Assert.IsTrue(peers.Guest.TryBeginTick(false));
            peers.GuestSimulation.Tick.ProcessTick();
            peers.HostSimulation.Tick.ProcessTick();
            peers.Flush();

            Assert.IsFalse(peers.Guest.TryBeginTick(false));
            Assert.AreEqual(1, peers.GuestGame.CurrentTick);
            Assert.IsNull(peers.Guest.Failure);
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void Dispose_PendingLocalRequest_CompletesWithUnknownOutcome(MultiplayerRole role)
        {
            using PeerPair peers = new();
            MultiplayerGameSession protocol =
                role == MultiplayerRole.Host ? peers.Host : peers.Guest;
            Task<PlayerRequestOutcome> request = protocol.ExecuteAsync(
                new MarkAllMessagesReadRequest()
            );

            protocol.Dispose();

            Assert.IsFalse(Completed(request).OutcomeKnown);
            Assert.Throws<ObjectDisposedException>(() =>
                protocol.ExecuteAsync(new MarkAllMessagesReadRequest())
            );
        }

        [Test]
        public void Pump_DomainObserverThrowsAfterMutation_FaultsWithoutRetrying()
        {
            using PeerPair peers = new();
            int observerCalls = 0;
            peers.HostClock.GameSpeedChanged += () =>
            {
                observerCalls++;
                throw new InvalidOperationException("Observer failed after mutation.");
            };
            Task<PlayerRequestOutcome> request = peers.Host.ExecuteAsync(
                new SetSpeedRequest { Speed = TickSpeed.Paused }
            );

            peers.Host.Pump();
            peers.Host.Pump();
            peers.Flush();

            Assert.IsNotNull(peers.Host.Failure);
            Assert.IsNotNull(peers.Guest.Failure);
            Assert.IsFalse(Completed(request).OutcomeKnown);
            Assert.AreEqual(TickSpeed.Paused, peers.HostGame.GetGameSpeed());
            Assert.AreEqual(1, observerCalls);
        }

        [Test]
        public void Pump_TransportAlsoRejectsFailureNotification_FaultsWithUnknownOutcome()
        {
            using PeerPair peers = new();
            peers.RejectHostSends = true;
            int failures = 0;
            peers.Host.Faulted += _ => failures++;
            Task<PlayerRequestOutcome> request = peers.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Applied before disconnect" }
            );

            Assert.DoesNotThrow(peers.Host.Pump);

            Assert.IsNotNull(peers.Host.Failure);
            Assert.IsFalse(Completed(request).OutcomeKnown);
            Assert.AreEqual(1, failures);
            Assert.AreEqual(
                "Applied before disconnect",
                peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
        }

        [Test]
        public void ExecuteAsync_GuestTransportFails_CompletesUnconfirmedAndFaults()
        {
            using PeerPair peers = new();
            peers.RejectGuestSends = true;

            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new MarkAllMessagesReadRequest()
            );

            Assert.IsFalse(Completed(request).OutcomeKnown);
            Assert.IsInstanceOf<IOException>(peers.Guest.Failure);
            Assert.IsEmpty(peers.ToHost);
        }

        [Test]
        public void Pump_HostCommitSendFails_DoesNotExecuteFollowingQueuedAction()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> first = peers.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "First mutation" }
            );
            Task<PlayerRequestOutcome> second = peers.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Must remain queued" }
            );
            peers.RejectHostSends = true;

            peers.Host.Pump();

            Assert.IsNotNull(peers.Host.Failure);
            Assert.IsFalse(Completed(first).OutcomeKnown);
            Assert.IsFalse(Completed(second).OutcomeKnown);
            Assert.AreEqual(
                "First mutation",
                peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
        }

        [Test]
        public void Pump_GuestReceiptSendFails_DoesNotReplayFollowingQueuedAction()
        {
            using PeerPair peers = new();
            peers.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "First mutation" }
            );
            peers.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Must remain queued" }
            );
            peers.Host.Pump();
            peers.DeliverToGuest();
            peers.RejectGuestSends = true;

            peers.Guest.Pump();

            Assert.IsNotNull(peers.Guest.Failure);
            Assert.AreEqual(
                "First mutation",
                peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
        }

        [Test]
        public void TryBeginTick_TransportFails_DoesNotAuthorizeSimulationAdvance()
        {
            using PeerPair peers = new();
            peers.RejectHostSends = true;

            Assert.IsFalse(peers.Host.TryBeginTick(true));

            Assert.IsInstanceOf<IOException>(peers.Host.Failure);
            Assert.Zero(peers.HostGame.CurrentTick);
        }

        [Test]
        public void ReportFailure_PendingRemoteRequest_LeavesOutcomeUnknownWithoutRetry()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new MarkAllMessagesReadRequest()
            );
            InvalidOperationException failure = new("The tick could not complete.");

            peers.Host.ReportFailure(failure);
            peers.Flush();

            Assert.AreSame(failure, peers.Host.Failure);
            Assert.IsNotNull(peers.Guest.Failure);
            Assert.IsFalse(Completed(request).OutcomeKnown);
            Assert.IsFalse(peers.Host.CanBeginHostTick);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Receive_StreamPositionDiverges_RejectsNextDayBeforeAdvancing(bool identifiers)
        {
            using PeerPair peers = new();
            if (identifiers)
                peers.GuestGame.GenerateRuntimeIdentifier();
            else
                peers.GuestGame.Random.NextDouble();
            Assert.IsTrue(peers.Host.TryBeginTick(true));

            peers.DeliverToGuest();
            peers.Guest.Pump();

            Assert.IsNotNull(peers.Guest.Failure);
            Assert.IsFalse(peers.Guest.TryBeginTick(false));
            Assert.Zero(peers.GuestGame.CurrentTick);
        }

        [Test]
        public void Pump_ActionQueuedInsideTickCompletion_WaitsUntilIteratorReleasesGuard()
        {
            using PeerPair peers = new();
            Task<PlayerRequestOutcome> request = null;
            peers.HostSimulation.Tick.TickCompleted += () =>
            {
                request = peers.Host.ExecuteAsync(
                    new RenameRequest { EntityID = "host-ship", Name = "After day" }
                );
                peers.Host.Pump();
                Assert.IsFalse(request.IsCompleted);
            };
            Assert.IsTrue(peers.Host.TryBeginTick(true));

            peers.HostSimulation.Tick.ProcessTick();

            Assert.IsNotNull(request);
            Assert.IsFalse(request.IsCompleted);
            peers.Host.Pump();
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsTrue(peers.Guest.TryBeginTick(false));
            peers.GuestSimulation.Tick.ProcessTick();
            peers.Flush();
            Assert.IsTrue(Completed(request).Accepted);
            peers.AssertSharedState();
        }

        [Test]
        public void Pump_PendingCombatDecision_ReplaysRetreatWhileDayIsUnsaveable()
        {
            using PeerPair peers = new(contested: true);
            Assert.IsTrue(peers.Host.TryBeginTick(true));
            peers.HostSimulation.Tick.ProcessTick();
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsTrue(peers.Guest.TryBeginTick(false));
            peers.GuestSimulation.Tick.ProcessTick();
            Assert.IsFalse(peers.HostSimulation.Tick.IsSettled);
            Assert.IsFalse(peers.GuestSimulation.Tick.IsSettled);
            Assert.IsTrue(
                peers
                    .GuestSimulation.GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            );
            Task<PlayerRequestOutcome> request = peers.Guest.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = true,
                }
            );

            peers.Flush();

            Assert.IsTrue(request.IsCompletedSuccessfully);
            Assert.IsTrue(Completed(request).Accepted);
            Assert.IsFalse(peers.HostSimulation.Tick.IsSettled);
            peers.Host.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = false,
                }
            );
            peers.Flush();
            Assert.IsTrue(peers.HostSimulation.Tick.IsSettled);
            Assert.IsTrue(peers.GuestSimulation.Tick.IsSettled);
            Assert.IsNull(peers.Host.Failure, peers.Host.Failure?.ToString());
            Assert.IsNull(peers.Guest.Failure, peers.Guest.Failure?.ToString());
            Assert.IsTrue(peers.Host.CanBeginHostTick);
            peers.AssertSharedState();
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Pump_TwoHumanCombatResponses_WaitsForBothAndAppliesTheirChoices(
            bool hostRetreat,
            bool guestRetreat
        )
        {
            using PeerPair peers = new(contested: true);
            BeginPendingBattle(peers);
            Assert.IsTrue(
                peers
                    .HostSimulation.GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            );
            long before = peers.HostGame.RandomIndex;
            Task<PlayerRequestOutcome> first = peers.Host.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = hostRetreat,
                }
            );
            peers.Flush();

            Assert.IsTrue(first.Result.Accepted);
            Assert.IsNull(first.Result.SpaceCombatResult);
            Assert.IsFalse(peers.HostSimulation.Tick.IsSettled);
            Assert.IsFalse(peers.GuestSimulation.Tick.IsSettled);
            Assert.AreEqual(before, peers.HostGame.RandomIndex);
            Task<PlayerRequestOutcome> second = peers.Guest.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = guestRetreat,
                }
            );
            peers.Flush();

            Assert.IsTrue(second.Result.Accepted);
            Assert.IsNotNull(second.Result.SpaceCombatResult);
            Assert.IsTrue(peers.HostSimulation.Tick.IsSettled);
            Assert.IsTrue(peers.GuestSimulation.Tick.IsSettled);
            if (hostRetreat && guestRetreat)
            {
                Assert.AreEqual(CombatSide.Draw, second.Result.SpaceCombatResult.Winner);
                Assert.AreEqual(
                    SpaceCombatSideOutcome.Withdrawn,
                    second.Result.SpaceCombatResult.AttackerOutcome
                );
                Assert.AreEqual(
                    SpaceCombatSideOutcome.Withdrawn,
                    second.Result.SpaceCombatResult.DefenderOutcome
                );
                Assert.IsNotEmpty(second.Result.SpaceCombatResult.AttackerRetreatPlanetInstanceID);
                Assert.IsNotEmpty(second.Result.SpaceCombatResult.DefenderRetreatPlanetInstanceID);
            }
            peers.AssertSharedState();
        }

        [Test]
        public void Pump_UnavailableRetreat_DoesNotRecordAResponse()
        {
            using PeerPair peers = new(contested: true);
            peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").Hyperdrive = 0;
            peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").Hyperdrive = 0;
            BeginPendingBattle(peers);
            Assert.IsTrue(
                peers
                    .HostSimulation.GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            );
            Task<PlayerRequestOutcome> request = peers.Host.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = true,
                }
            );
            peers.Flush();

            Assert.IsFalse(Completed(request).Accepted);
            Assert.IsTrue(
                peers
                    .HostSimulation.GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult waiting)
            );
            Assert.IsFalse(waiting.AttackerHasResponded);
            Assert.IsFalse(waiting.DefenderHasResponded);
            peers.AssertSharedState();
        }

        [Test]
        public void Pump_OnlyOneHumanCombatParticipant_ResolvesAfterThatResponse()
        {
            using PeerPair peers = new(contested: true);
            peers.HostGame.SetFactionController(
                _guestFactionID,
                "guest-ai",
                PlayerControllerType.AI
            );
            peers.GuestGame.SetFactionController(
                _guestFactionID,
                "guest-ai",
                PlayerControllerType.AI
            );
            BeginPendingBattle(peers);
            Assert.IsTrue(
                peers
                    .HostSimulation.GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            );
            Task<PlayerRequestOutcome> request = peers.Host.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = false,
                }
            );
            peers.Flush();

            Assert.IsTrue(Completed(request).Accepted);
            Assert.IsNotNull(Completed(request).SpaceCombatResult);
            Assert.IsTrue(peers.HostSimulation.Tick.IsSettled);
            Assert.IsTrue(peers.GuestSimulation.Tick.IsSettled);
            peers.AssertSharedState();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Pump_OneCombatResponsePending_RejectsRecoveryCheckpoint(bool retreat)
        {
            using PeerPair peers = new(contested: true);
            BeginPendingBattle(peers);
            Assert.IsTrue(
                peers
                    .HostSimulation.GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            );
            peers.Host.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = retreat,
                }
            );
            peers.Flush();
            Assert.Throws<InvalidOperationException>(() =>
                MultiplayerSnapshotCodec.Capture(peers.HostSimulation)
            );
            Assert.Throws<InvalidOperationException>(() =>
                MultiplayerSnapshotCodec.Capture(peers.GuestSimulation)
            );
            Task<PlayerRequestOutcome> second = peers.Guest.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = false,
                }
            );
            peers.Flush();

            Assert.IsTrue(second.Result.Accepted);
            peers.AssertSharedState();
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void Constructor_PendingBattle_RejectsNewOrderingOwner(MultiplayerRole role)
        {
            using PeerPair peers = new(contested: true);
            BeginPendingBattle(peers);
            GameSession session =
                role == MultiplayerRole.Host ? peers.HostSimulation : peers.GuestSimulation;
            GameManager clock = role == MultiplayerRole.Host ? peers.HostClock : peers.GuestClock;
            Assert.Throws<InvalidOperationException>(() =>
                new MultiplayerGameSession(
                    role,
                    _hostFactionID,
                    _guestFactionID,
                    session,
                    clock,
                    _ => { },
                    Guid.NewGuid().ToString("N")
                )
            );
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void Pump_SurvivorRecovery_ContinuesTwentyDaysWithFreshOrdering(
            MultiplayerRole survivor
        )
        {
            using PeerPair previous = new();
            previous.HostGame.SetGameSpeed(TickSpeed.Paused);
            previous.GuestGame.SetGameSpeed(TickSpeed.Paused);
            foreach (GameRoot game in new[] { previous.HostGame, previous.GuestGame })
            foreach (Player player in game.GetPlayers())
                player.RequestedGameSpeed = TickSpeed.Paused;
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(
                survivor == MultiplayerRole.Host
                    ? previous.HostSimulation
                    : previous.GuestSimulation
            );
            previous.Host.Dispose();
            previous.Guest.Dispose();
            using PeerPair recovered = new(snapshot, TestGameData.Create(previous.HostGame.Config));
            recovered.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast });
            recovered.Flush();
            Assert.AreEqual(TickSpeed.Paused, recovered.HostClock.GetGameSpeed());
            recovered.Guest.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast });
            recovered.Flush();
            Assert.AreEqual(TickSpeed.Fast, recovered.HostClock.GetGameSpeed());
            for (int day = 0; day < 20; day++)
            {
                Assert.IsTrue(recovered.Host.TryBeginTick(true));
                recovered.DeliverToGuest();
                recovered.Guest.Pump();
                Assert.IsTrue(recovered.Guest.TryBeginTick(false));
                recovered.HostSimulation.Tick.ProcessTick();
                recovered.GuestSimulation.Tick.ProcessTick();
                recovered.Flush();
                recovered.AssertSharedState();
            }
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void Pump_ThirtyAlternatingRecoveries_PreservesOneHundredFiftyDays(
            MultiplayerRole firstSurvivor
        )
        {
            PeerPair peers = new();
            try
            {
                for (int cycle = 0; cycle < 30; cycle++)
                {
                    for (int day = 0; day < 5; day++)
                    {
                        Assert.IsTrue(peers.Host.TryBeginTick(true));
                        peers.DeliverToGuest();
                        peers.Guest.Pump();
                        Assert.IsTrue(peers.Guest.TryBeginTick(false));
                        peers.HostSimulation.Tick.ProcessTick();
                        peers.GuestSimulation.Tick.ProcessTick();
                        peers.Flush();
                        peers.AssertSharedState();
                    }
                    MultiplayerRole survivor =
                        cycle % 2 == 0 ? firstSurvivor
                        : firstSurvivor == MultiplayerRole.Host ? MultiplayerRole.Guest
                        : MultiplayerRole.Host;
                    MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(
                        survivor == MultiplayerRole.Host
                            ? peers.HostSimulation
                            : peers.GuestSimulation
                    );
                    GameDataCatalog content = TestGameData.Create(peers.HostGame.Config);
                    peers.Dispose();
                    peers = new PeerPair(snapshot, content);
                    peers.Host.ExecuteAsync(
                        new RenameRequest
                        {
                            EntityID = "host-ship",
                            Name = "Host recovery " + cycle,
                        }
                    );
                    peers.Guest.ExecuteAsync(
                        new RenameRequest
                        {
                            EntityID = "guest-ship",
                            Name = "Guest recovery " + cycle,
                        }
                    );
                    peers.Flush();
                    Assert.AreEqual(
                        MultiplayerStateFingerprint.ComputeSimulation(peers.HostSimulation),
                        MultiplayerStateFingerprint.ComputeSimulation(peers.GuestSimulation)
                    );
                    Assert.AreEqual((cycle + 1) * 5, peers.HostGame.CurrentTick);
                }
            }
            finally
            {
                peers.Dispose();
            }
        }

        [Test]
        public void Pump_RecoveryDuringBattle_ReturnsToCompletedCheckpointAndRequiresBothNewResponses(
            [Values(MultiplayerRole.Host, MultiplayerRole.Guest)] MultiplayerRole survivor,
            [Values] bool guestRespondsFirst,
            [Values] bool hostRetreat,
            [Values] bool guestRetreat
        )
        {
            using PeerPair previous = new(contested: true);
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(
                survivor == MultiplayerRole.Host
                    ? previous.HostSimulation
                    : previous.GuestSimulation
            );
            int checkpointTick = previous.HostGame.CurrentTick;
            BeginPendingBattle(previous);
            Assert.IsTrue(
                previous
                    .HostSimulation.GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            );
            Assert.IsTrue(pending.AttackerCanRetreat);
            Assert.IsTrue(pending.DefenderCanRetreat);
            MultiplayerGameSession first = guestRespondsFirst ? previous.Guest : previous.Host;
            Task<PlayerRequestOutcome> initialResponse = first.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                    Retreat = !(guestRespondsFirst ? guestRetreat : hostRetreat),
                }
            );
            previous.Flush();
            Assert.IsTrue(Completed(initialResponse).Accepted);
            Assert.IsNull(Completed(initialResponse).SpaceCombatResult);
            Assert.Throws<InvalidOperationException>(() =>
                MultiplayerSnapshotCodec.Capture(previous.HostSimulation)
            );
            previous.Host.Dispose();
            previous.Guest.Dispose();
            using PeerPair recovered = new(snapshot, TestGameData.Create(previous.HostGame.Config));
            Assert.That(recovered.HostGame.CurrentTick, Is.EqualTo(checkpointTick));
            Assert.That(recovered.GuestGame.CurrentTick, Is.EqualTo(checkpointTick));
            Assert.That(
                recovered.HostSimulation.Tick.IsSettled && recovered.GuestSimulation.Tick.IsSettled,
                Is.True
            );
            BeginPendingBattle(recovered);
            Assert.IsTrue(
                recovered
                    .HostSimulation.GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult restored)
            );
            Assert.That(restored.AttackerHasResponded || restored.DefenderHasResponded, Is.False);
            MultiplayerGameSession firstAgain = guestRespondsFirst
                ? recovered.Guest
                : recovered.Host;
            Task<PlayerRequestOutcome> newResponse = firstAgain.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = restored.PlanetInstanceID,
                    Tick = restored.Tick,
                    Retreat = guestRespondsFirst ? guestRetreat : hostRetreat,
                }
            );
            recovered.Flush();
            Assert.That(Completed(newResponse).Accepted, Is.True);
            Assert.That(Completed(newResponse).SpaceCombatResult, Is.Null);
            Assert.That(recovered.HostSimulation.Tick.IsSettled, Is.False);
            MultiplayerGameSession second = guestRespondsFirst ? recovered.Host : recovered.Guest;
            Task<PlayerRequestOutcome> finalResponse = second.ExecuteAsync(
                new ResolveCombatRequest
                {
                    PlanetID = restored.PlanetInstanceID,
                    Tick = restored.Tick,
                    Retreat = guestRespondsFirst ? hostRetreat : guestRetreat,
                }
            );
            recovered.Flush();

            PlayerRequestOutcome completed = Completed(finalResponse);
            Assert.IsTrue(completed.Accepted);
            Assert.IsNotNull(completed.SpaceCombatResult);
            Assert.IsTrue(recovered.HostSimulation.Tick.IsSettled);
            Assert.IsTrue(recovered.GuestSimulation.Tick.IsSettled);
            if (hostRetreat && guestRetreat)
            {
                Assert.AreEqual(
                    SpaceCombatSideOutcome.Withdrawn,
                    completed.SpaceCombatResult.AttackerOutcome
                );
                Assert.AreEqual(
                    SpaceCombatSideOutcome.Withdrawn,
                    completed.SpaceCombatResult.DefenderOutcome
                );
            }
            recovered.AssertSharedState();
        }

        [TestCase(11)]
        [TestCase(42)]
        [TestCase(314159)]
        [TestCase(20261028)]
        public void Pump_DelayedRequestsDuringTicksAndRepeatedRecovery_PreservesBothPlayersSelections(
            int seed
        )
        {
            System.Random network = new(seed);
            TickSpeed[] speeds =
            {
                TickSpeed.Paused,
                TickSpeed.VerySlow,
                TickSpeed.Slow,
                TickSpeed.Medium,
                TickSpeed.Fast,
            };
            PeerPair peers = new();
            try
            {
                for (int cycle = 0; cycle < 12; cycle++)
                {
                    List<Task<PlayerRequestOutcome>> start = new()
                    {
                        peers.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast }),
                        peers.Guest.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast }),
                    };
                    peers.FlushWithJitter(start, network);
                    Assert.IsTrue(peers.Host.TryBeginTick(true));
                    peers.DeliverToGuest();
                    peers.Guest.Pump();
                    Assert.IsTrue(peers.Guest.TryBeginTick(false));
                    IEnumerator hostTick = peers.HostSimulation.Tick.ProcessTickIncrementally();
                    IEnumerator guestTick = peers.GuestSimulation.Tick.ProcessTickIncrementally();
                    try
                    {
                        Assert.IsTrue(hostTick.MoveNext());
                        Assert.IsTrue(guestTick.MoveNext());
                        TickSpeed? expectedHost = peers
                            .HostGame.GetFactionPlayer(_hostFactionID)
                            .RequestedGameSpeed;
                        TickSpeed? expectedGuest = peers
                            .HostGame.GetFactionPlayer(_guestFactionID)
                            .RequestedGameSpeed;
                        List<Task<PlayerRequestOutcome>> requests = new();
                        for (int request = 0; request < 24; request++)
                        {
                            TickSpeed speed = speeds[network.Next(speeds.Length)];
                            bool guest = network.Next(2) == 0;
                            if (guest)
                                expectedGuest = speed;
                            else
                                expectedHost = speed;
                            requests.Add(
                                (guest ? peers.Guest : peers.Host).ExecuteAsync(
                                    new SetSpeedRequest { Speed = speed }
                                )
                            );
                        }
                        peers.FlushWithJitter(requests, network, hostTick, guestTick);
                        foreach (Task<PlayerRequestOutcome> request in requests)
                            Assert.IsTrue(Completed(request).Accepted);
                        Assert.AreEqual(
                            expectedHost,
                            peers.HostGame.GetFactionPlayer(_hostFactionID).RequestedGameSpeed
                        );
                        Assert.AreEqual(
                            expectedGuest,
                            peers.HostGame.GetFactionPlayer(_guestFactionID).RequestedGameSpeed
                        );
                        peers.AssertSharedState();
                    }
                    finally
                    {
                        (hostTick as IDisposable)?.Dispose();
                        (guestTick as IDisposable)?.Dispose();
                    }
                    List<Task<PlayerRequestOutcome>> resume = new()
                    {
                        peers.Host.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast }),
                        peers.Guest.ExecuteAsync(new SetSpeedRequest { Speed = TickSpeed.Fast }),
                    };
                    peers.FlushWithJitter(resume, network);
                    Assert.AreEqual(TickSpeed.Fast, peers.HostClock.GetGameSpeed());
                    for (int day = 0; day < 2; day++)
                    {
                        Assert.IsTrue(peers.Host.TryBeginTick(true));
                        peers.DeliverToGuest();
                        peers.Guest.Pump();
                        Assert.IsTrue(peers.Guest.TryBeginTick(false));
                        peers.HostSimulation.Tick.ProcessTick();
                        peers.GuestSimulation.Tick.ProcessTick();
                        peers.FlushWithJitter(Array.Empty<Task<PlayerRequestOutcome>>(), network);
                        peers.AssertSharedState();
                    }
                    if (cycle % 3 == 2)
                    {
                        GameSession survivor =
                            network.Next(2) == 0 ? peers.HostSimulation : peers.GuestSimulation;
                        MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(survivor);
                        var gameData = TestGameData.Create(peers.HostGame.Config);
                        peers.Dispose();
                        peers = new PeerPair(snapshot, gameData);
                        peers.AssertSharedState();
                    }
                }
                Assert.AreEqual(36, peers.HostGame.CurrentTick);
                Assert.AreEqual(36, peers.GuestGame.CurrentTick);
            }
            finally
            {
                peers.Dispose();
            }
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void Pump_RecoveryWithUndeliveredCommit_UsesSurvivorAndRejectsOldConnectionTraffic(
            MultiplayerRole survivor
        )
        {
            using PeerPair previous = new();
            previous.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Unreceived commit" }
            );
            previous.Host.Pump();
            MultiplayerPacket oldCommit = previous.ToGuest.Dequeue();
            MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(
                survivor == MultiplayerRole.Host
                    ? previous.HostSimulation
                    : previous.GuestSimulation
            );
            using PeerPair recovered = new(snapshot, TestGameData.Create(previous.HostGame.Config));

            recovered.Guest.Receive(oldCommit);
            recovered.Flush();
            recovered.AssertSharedState();

            Assert.AreEqual(
                survivor == MultiplayerRole.Host ? "Unreceived commit" : "host-ship",
                recovered.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
        }

        /// <summary>
        /// Reads a completed request without allowing a lost protocol completion to block the test runner.
        /// </summary>
        /// <param name="request">The request whose completion is required.</param>
        /// <returns>The completed domain outcome.</returns>
        private static PlayerRequestOutcome Completed(Task<PlayerRequestOutcome> request)
        {
            Assert.AreEqual(TaskStatus.RanToCompletion, request.Status);
            return request.Result;
        }

        /// <summary>
        /// Contains only public wire fields used to exercise malformed or repeated packets.
        /// </summary>
        [Serializable]
        private sealed class PacketMetadata
        {
            public string initializationID;
            public string stateFingerprint;
            public long requestID;
            public long sequence;
            public PlayerRequestKind kind;
            public MultiplayerRole actor;
            public bool accepted;
            public string reason;
            public string createdEntityID;
            public int tick;
            public long randomIndex;
            public long identifierIndex;
        }

        /// <summary>
        /// Owns two independent simulations and delayed reliable ordered packet queues.
        /// </summary>
        /// <summary>
        /// Admits and independently processes the shared day until both peers await combat input.
        /// </summary>
        /// <param name="peers">The independently simulated participants.</param>
        private static void BeginPendingBattle(PeerPair peers)
        {
            Assert.IsTrue(peers.Host.TryBeginTick(true));
            peers.HostSimulation.Tick.ProcessTick();
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsTrue(peers.Guest.TryBeginTick(false));
            peers.GuestSimulation.Tick.ProcessTick();
        }

        private sealed class PeerPair : IDisposable
        {
            public Queue<MultiplayerPacket> ToHost { get; } = new();
            public Queue<MultiplayerPacket> ToGuest { get; } = new();
            public GameSession HostSimulation { get; }
            public GameSession GuestSimulation { get; }
            public GameRoot HostGame => HostSimulation.Game;
            public GameRoot GuestGame => GuestSimulation.Game;
            public GameManager HostClock { get; }
            public GameManager GuestClock { get; }
            public MultiplayerGameSession Host { get; }
            public MultiplayerGameSession Guest { get; }
            public bool RejectHostSends { get; set; }
            public bool RejectGuestSends { get; set; }
            public string InitializationID { get; } = Guid.NewGuid().ToString("N");

            /// <summary>
            /// Builds matching graphs from one in-memory snapshot and attaches independent services.
            /// </summary>
            /// <param name="contested">Whether opposing fleets initially share the host planet.</param>
            public PeerPair(bool contested = false)
            {
                GameRoot source = CreateSource(contested);
                MultiplayerSnapshot snapshot = MultiplayerSnapshotCodec.Capture(source);
                GameRoot hostGame = MultiplayerSnapshotCodec.Restore(
                    snapshot.Payload,
                    snapshot.Hash
                );
                GameRoot guestGame = MultiplayerSnapshotCodec.Restore(
                    snapshot.Payload,
                    snapshot.Hash
                );
                hostGame.UseDeterministicRuntimeIdentifiers = true;
                guestGame.UseDeterministicRuntimeIdentifiers = true;
                hostGame.LocalPlayerFactionID = _hostFactionID;
                guestGame.LocalPlayerFactionID = _guestFactionID;
                GameDataCatalog content = TestGameData.Create(source.Config);
                HostSimulation = new GameSession(hostGame, content);
                GuestSimulation = new GameSession(guestGame, content);
                HostClock = new GameManager(() => HostGame, HostSimulation.Tick);
                GuestClock = new GameManager(() => GuestGame, GuestSimulation.Tick);
                Host = new MultiplayerGameSession(
                    MultiplayerRole.Host,
                    _hostFactionID,
                    _guestFactionID,
                    HostSimulation,
                    HostClock,
                    SendHost,
                    InitializationID
                );
                Guest = new MultiplayerGameSession(
                    MultiplayerRole.Guest,
                    _hostFactionID,
                    _guestFactionID,
                    GuestSimulation,
                    GuestClock,
                    SendGuest,
                    InitializationID
                );
            }

            /// <summary>
            /// Restarts independent ordering owners from either survivor's complete continuation.
            /// </summary>
            /// <param name="snapshot">The survivor's captured runtime boundary.</param>
            /// <param name="content">The verified shared gameplay content.</param>
            public PeerPair(MultiplayerSnapshot snapshot, GameDataCatalog content)
            {
                HostSimulation = new GameSession(
                    MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash),
                    content
                );
                GuestSimulation = new GameSession(
                    MultiplayerSnapshotCodec.Restore(snapshot.Payload, snapshot.Hash),
                    content
                );
                HostGame.LocalPlayerFactionID = _hostFactionID;
                GuestGame.LocalPlayerFactionID = _guestFactionID;
                HostClock = new GameManager(() => HostGame, HostSimulation.Tick);
                GuestClock = new GameManager(() => GuestGame, GuestSimulation.Tick);
                Host = new MultiplayerGameSession(
                    MultiplayerRole.Host,
                    _hostFactionID,
                    _guestFactionID,
                    HostSimulation,
                    HostClock,
                    SendHost,
                    InitializationID
                );
                Guest = new MultiplayerGameSession(
                    MultiplayerRole.Guest,
                    _hostFactionID,
                    _guestFactionID,
                    GuestSimulation,
                    GuestClock,
                    SendGuest,
                    InitializationID
                );
            }

            /// <summary>
            /// Delivers a whole transport poll without executing the receiving simulation.
            /// </summary>
            public void DeliverToHost()
            {
                while (ToHost.Count > 0)
                    Host.Receive(ToHost.Dequeue());
            }

            /// <summary>
            /// Delivers a whole transport poll without reentrant domain execution.
            /// </summary>
            public void DeliverToGuest()
            {
                while (ToGuest.Count > 0)
                    Guest.Receive(ToGuest.Dequeue());
            }

            /// <summary>
            /// Drains a bounded exchange of command and receipt packets without advancing time.
            /// </summary>
            public void Flush()
            {
                for (int pass = 0; pass < 32; pass++)
                {
                    DeliverToHost();
                    Host.Pump();
                    DeliverToGuest();
                    Guest.Pump();
                    if (ToHost.Count == 0 && ToGuest.Count == 0)
                        return;
                }
                Assert.Fail("The in-memory command exchange did not settle.");
            }

            /// <summary>
            /// Delays each ordered direction independently while optionally completing suspended ticks.
            /// </summary>
            /// <param name="requests">The commands that must finish before the exchange is settled.</param>
            /// <param name="network">The fixture's independent deterministic delivery scheduler.</param>
            /// <param name="hostTick">The optional already-started host tick iterator.</param>
            /// <param name="guestTick">The optional already-started guest tick iterator.</param>
            public void FlushWithJitter(
                IReadOnlyCollection<Task<PlayerRequestOutcome>> requests,
                System.Random network,
                IEnumerator hostTick = null,
                IEnumerator guestTick = null
            )
            {
                bool hostComplete = hostTick == null;
                bool guestComplete = guestTick == null;
                for (int pulse = 0; pulse < requests.Count * 32 + 256; pulse++)
                {
                    if (pulse >= 128 || network.Next(8) == 0)
                        DeliverToHost();
                    if (pulse >= 128 || network.Next(8) == 0)
                        DeliverToGuest();
                    Host.Pump();
                    Guest.Pump();
                    if (!hostComplete)
                        hostComplete = !hostTick.MoveNext();
                    if (!guestComplete)
                        guestComplete = !guestTick.MoveNext();
                    if (
                        hostComplete
                        && guestComplete
                        && requests.All(request => request.IsCompleted)
                        && Host.CanBeginHostTick
                        && ToHost.Count == 0
                        && ToGuest.Count == 0
                    )
                        return;
                }
                Assert.Fail("The delayed commands and day receipts did not settle.");
            }

            /// <summary>
            /// Compares complete persisted model state after both independent peers settle.
            /// </summary>
            public void AssertSharedState()
            {
                Assert.IsNull(Host.Failure, Host.Failure?.ToString());
                Assert.IsNull(Guest.Failure, Guest.Failure?.ToString());
                Assert.AreEqual(
                    MultiplayerSnapshotCodec.ComputeStateFingerprint(HostGame),
                    MultiplayerSnapshotCodec.ComputeStateFingerprint(GuestGame)
                );
            }

            /// <summary>
            /// Releases protocol subscriptions before disposing the underlying simulation services.
            /// </summary>
            public void Dispose()
            {
                if (Host.Failure != null)
                    TestContext.WriteLine("Host synchronization failure: " + Host.Failure);
                if (Guest.Failure != null)
                    TestContext.WriteLine("Guest synchronization failure: " + Guest.Failure);
                Host.Dispose();
                Guest.Dispose();
                HostSimulation.Dispose();
                GuestSimulation.Dispose();
            }

            /// <summary>
            /// Queues host output or models a broken transport without immediate peer callbacks.
            /// </summary>
            /// <param name="packet">The complete outgoing packet.</param>
            private void SendHost(MultiplayerPacket packet)
            {
                if (RejectHostSends)
                    throw new IOException("The peer connection is closed.");
                ToGuest.Enqueue(packet);
            }

            /// <summary>
            /// Queues guest output or models a disconnected sender without invoking host code.
            /// </summary>
            /// <param name="packet">The complete outgoing packet.</param>
            private void SendGuest(MultiplayerPacket packet)
            {
                if (RejectGuestSends)
                    throw new IOException("The peer connection is closed.");
                ToHost.Enqueue(packet);
            }

            /// <summary>
            /// Creates a small two-player campaign without touching persistence or runtime singletons.
            /// </summary>
            /// <param name="contested">Whether the guest fleet starts at the host planet.</param>
            /// <returns>The initial shared model.</returns>
            private static GameRoot CreateSource(bool contested)
            {
                GameRoot game = TestGame.Create(
                    new GameSummary
                    {
                        Seed = 314159,
                        PlayerFactionID = _hostFactionID,
                        VictoryCondition = GameVictoryCondition.Conquest,
                    },
                    TestConfig.Create()
                );
                game.SetGameSpeed(TickSpeed.Fast);
                Faction host = new Faction
                {
                    InstanceID = _hostFactionID,
                    HQInstanceID = "host-planet",
                    Settings = new FactionSettings { BattleLossLoyaltyDivisor = 80 },
                };
                Faction guest = new Faction
                {
                    InstanceID = _guestFactionID,
                    HQInstanceID = "guest-planet",
                    Settings = new FactionSettings { BattleLossLoyaltyDivisor = 80 },
                };
                game.GetFactions().AddRange(new[] { host, guest });
                game.SetFactionController(
                    _hostFactionID,
                    "host-player",
                    PlayerControllerType.Human
                );
                game.SetFactionController(
                    _guestFactionID,
                    "guest-player",
                    PlayerControllerType.Human
                );
                PlanetSector sector = new PlanetSector { InstanceID = "sector" };
                game.AttachNode(sector, game.Galaxy);
                Planet hostPlanet = new Planet
                {
                    InstanceID = "host-planet",
                    DisplayName = "Host planet",
                    OwnerInstanceID = _hostFactionID,
                    IsColonized = true,
                    IsHeadquarters = true,
                };
                Planet guestPlanet = new Planet
                {
                    InstanceID = "guest-planet",
                    DisplayName = "Guest planet",
                    OwnerInstanceID = _guestFactionID,
                    IsColonized = true,
                    IsHeadquarters = true,
                    PositionX = 100,
                };
                hostPlanet.SetPopularSupport(_hostFactionID, 80);
                guestPlanet.SetPopularSupport(_guestFactionID, 80);
                game.AttachNode(hostPlanet, sector);
                game.AttachNode(guestPlanet, sector);
                if (contested)
                {
                    game.AttachNode(
                        new Planet
                        {
                            InstanceID = "host-safe-planet",
                            OwnerInstanceID = _hostFactionID,
                            IsColonized = true,
                            PositionX = -100,
                        },
                        sector
                    );
                }
                AddFleet(game, hostPlanet, _hostFactionID, "host");
                AddFleet(game, contested ? hostPlanet : guestPlanet, _guestFactionID, "guest");
                host.AddMessage(
                    new StatusMessage(MessageType.Advice, "Host report", "Host report")
                    {
                        InstanceID = "host-message",
                    }
                );
                guest.AddMessage(
                    new StatusMessage(MessageType.Advice, "Guest report", "Guest report")
                    {
                        InstanceID = "guest-message",
                    }
                );
                return game;
            }

            /// <summary>
            /// Attaches one explicitly identified fleet and mobile capital ship to the supplied planet.
            /// </summary>
            /// <param name="game">The campaign receiving the force.</param>
            /// <param name="planet">The force's current location.</param>
            /// <param name="owner">The controlling faction.</param>
            /// <param name="prefix">The stable fleet and ship identity prefix.</param>
            private static void AddFleet(GameRoot game, Planet planet, string owner, string prefix)
            {
                Fleet fleet = new Fleet
                {
                    InstanceID = prefix + "-fleet",
                    DisplayName = prefix + "-fleet",
                    OwnerInstanceID = owner,
                };
                game.AttachNode(fleet, planet);
                game.AttachNode(
                    new CapitalShip
                    {
                        InstanceID = prefix + "-ship",
                        DisplayName = prefix + "-ship",
                        OwnerInstanceID = owner,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                        CurrentHullStrength = 100,
                        MaxHullStrength = 100,
                        Hyperdrive = 1,
                    },
                    fleet
                );
            }
        }
    }
}
