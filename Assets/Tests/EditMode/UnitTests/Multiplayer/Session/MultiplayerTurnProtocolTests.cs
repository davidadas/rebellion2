using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Research;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Multiplayer;
using Rebellion.PlayerRequests;
using Rebellion.SceneGraph;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using AssetDatabase = UnityEditor.AssetDatabase;

namespace Rebellion.Tests.Multiplayer
{
    [TestFixture]
    public sealed class MultiplayerTurnProtocolTests
    {
        private const string _hostFactionID = "FNALL1";
        private const string _guestFactionID = "FNEMP1";

        /// <summary>
        /// Leaves Play Mode even when a UI assertion fails.
        /// </summary>
        /// <returns>The mode-exit sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDownPlayMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        [TestCaseSource(nameof(MissionStartCases))]
        public void Pump_MissionFamilyRequest_PreservesMatchingContinuationAfterReload(
            MultiplayerRole role,
            string missionType,
            ResearchDiscipline? discipline,
            bool repeating
        )
        {
            using PeerPair peers = new();
            string actor = role == MultiplayerRole.Host ? _hostFactionID : _guestFactionID;
            MissionContext hostContext = PrepareMission(
                peers.HostGame,
                actor,
                missionType,
                discipline
            );
            MissionContext guestContext = PrepareMission(
                peers.GuestGame,
                actor,
                missionType,
                discipline
            );
            bool host = role == MultiplayerRole.Host;
            GameSession session = host ? peers.HostSimulation : peers.GuestSimulation;
            MissionContext context = host ? hostContext : guestContext;
            context.IsRepeating = repeating;
            Assert.IsTrue(
                session.GetService<MissionQueries>().CanCreateMission(context),
                "Fixture eligibility"
            );

            Task<PlayerRequestOutcome> request = (host ? peers.Host : peers.Guest).ExecuteAsync(
                CreateMissionRequest(context)
            );
            peers.Flush();

            Assert.IsTrue(request.Result.Accepted, request.Result.RejectionReason);
            foreach (GameRoot game in new[] { peers.HostGame, peers.GuestGame })
            {
                Mission mission = game.GetSceneNodesByType<Mission>().Single();
                Assert.AreEqual(missionType, mission.ConfigKey);
                Assert.AreEqual(actor, mission.OwnerInstanceID);
                Assert.IsTrue(mission.HasInitiated);
                if (mission is EspionageMission espionage)
                    Assert.AreEqual(repeating, espionage.IsRepeating);
            }
            peers.AssertSharedState();
            MultiplayerSnapshot saved = MultiplayerSnapshotCodec.Capture(session);
            using PeerPair resumed = new(saved, TestGameData.Create(peers.HostGame.Config));
            for (int day = 0; day < 5; day++)
            {
                if (!peers.Host.CanBeginHostTick)
                {
                    Assert.IsFalse(resumed.Host.CanBeginHostTick);
                    break;
                }
                AdvanceMissionDay(peers);
                AdvanceMissionDay(resumed);
                Assert.AreEqual(
                    MultiplayerSnapshotCodec.ComputeStateFingerprint(peers.HostGame),
                    MultiplayerSnapshotCodec.ComputeStateFingerprint(resumed.HostGame),
                    $"{role} {missionType}: restored mission diverged on day {day + 1}"
                );
            }
        }

        [TestCaseSource(nameof(MissionStartCases))]
        public void Pump_FogOfWarMissionRequest_StartsMatchingMissionOnBothPeers(
            MultiplayerRole role,
            string missionType,
            ResearchDiscipline? discipline,
            bool repeating
        )
        {
            using PeerPair peers = new();
            string actor = role == MultiplayerRole.Host ? _hostFactionID : _guestFactionID;
            bool host = role == MultiplayerRole.Host;
            MissionContext hostContext = PrepareMission(
                peers.HostGame,
                actor,
                missionType,
                discipline
            );
            MissionContext guestContext = PrepareMission(
                peers.GuestGame,
                actor,
                missionType,
                discipline
            );
            foreach (
                var entry in new[]
                {
                    (peers.HostGame, hostContext),
                    (peers.GuestGame, guestContext),
                }
            )
            {
                Planet location = (Planet)entry.Item2.Location;
                new Rebellion.Game.FogOfWar.FogOfWarRecorder().RecordEspionageSnapshot(
                    entry.Item1.GetFactionByOwnerInstanceID(actor),
                    location,
                    location.GetParentOfType<PlanetSector>(),
                    entry.Item1.CurrentTick
                );
            }
            SettlePreparedState(peers);
            GameRoot issuer = host ? peers.HostGame : peers.GuestGame;
            GameSession session = host ? peers.HostSimulation : peers.GuestSimulation;
            MissionContext context = host ? hostContext : guestContext;
            Planet observed = session
                .GetService<FogOfWarQueries>()
                .BuildFactionView(issuer.GetFactionByOwnerInstanceID(actor))
                .GetChildren<PlanetSector>()
                .SelectMany(sector => sector.GetChildren<Planet>())
                .Single(planet => planet.InstanceID == context.Location.InstanceID);
            ISceneNode selected = null;
            observed.TraverseIncludingDisabled(node =>
            {
                if (node.InstanceID == context.SelectedTarget.InstanceID)
                    selected = node;
            });
            Assert.IsNotNull(selected, "The player must be able to see the selected target.");
            Assert.AreNotSame(context.Location, observed);
            context.Location = observed;
            context.SelectedTarget = selected;
            context.IsRepeating = repeating;
            Assert.IsTrue(
                session.GetService<MissionQueries>().CanCreateMission(context),
                "Observed fixture eligibility"
            );

            Task<PlayerRequestOutcome> request = (host ? peers.Host : peers.Guest).ExecuteAsync(
                CreateMissionRequest(context)
            );
            peers.Flush();

            Assert.IsTrue(Completed(request).Accepted, Completed(request).RejectionReason);
            Assert.AreEqual(
                missionType,
                peers.HostGame.GetSceneNodesByType<Mission>().Single().ConfigKey
            );
            peers.AssertSharedState();
        }

        [TestCaseSource(nameof(InvalidMissionCases))]
        public void Pump_InvalidMissionRequest_RejectsWithoutMutatingEitherPeer(
            MultiplayerRole role,
            string scenario
        )
        {
            using PeerPair peers = new();
            string actor = role == MultiplayerRole.Host ? _hostFactionID : _guestFactionID;
            bool host = role == MultiplayerRole.Host;
            MissionContext hostContext = PrepareMission(
                peers.HostGame,
                actor,
                SabotageMission.MissionTypeID,
                null
            );
            MissionContext guestContext = PrepareMission(
                peers.GuestGame,
                actor,
                SabotageMission.MissionTypeID,
                null
            );
            MissionContext context = host ? hostContext : guestContext;
            StartMissionRequest order = CreateMissionRequest(context);
            switch (scenario)
            {
                case "empty":
                    order.MainParticipantIDs = Array.Empty<string>();
                    break;
                case "missing":
                    order.MainParticipantIDs = new[] { "missing-agent" };
                    break;
                case "duplicate":
                    order.MainParticipantIDs = new[] { "matrix-agent", "matrix-agent" };
                    break;
                case "missing-decoy":
                    order.DecoyParticipantIDs = new[] { "missing-agent" };
                    break;
                case "overlap":
                    order.DecoyParticipantIDs = new[] { "matrix-agent" };
                    break;
                case "duplicate-decoy":
                    order.DecoyParticipantIDs = new[] { "matrix-decoy", "matrix-decoy" };
                    break;
                case "foreign":
                    order.MainParticipantIDs = new[] { "foreign-agent" };
                    break;
                case "foreign-decoy":
                    order.DecoyParticipantIDs = new[] { "foreign-agent" };
                    break;
                case "wrong-planet":
                    order.ObservedLocation = (Planet)
                        context.MainParticipants[0].GetParentOfType<Planet>();
                    break;
                case "unsupported-repeat":
                    order.IsRepeating = true;
                    break;
                case "unknown-mission":
                    order.MissionTypeID = "missing-mission";
                    break;
            }
            foreach (GameRoot game in new[] { peers.HostGame, peers.GuestGame })
            {
                Planet origin = game.GetSceneNodeByInstanceID<Planet>(
                    host ? "host-planet" : "guest-planet"
                );
                game.AttachNode(
                    new Officer { InstanceID = "matrix-decoy", OwnerInstanceID = actor },
                    origin
                );
                game.AttachNode(
                    new Officer
                    {
                        InstanceID = "foreign-agent",
                        OwnerInstanceID = host ? _guestFactionID : _hostFactionID,
                    },
                    game.GetSceneNodeByInstanceID<Planet>(host ? "guest-planet" : "host-planet")
                );
            }
            SettlePreparedState(peers);
            Task<PlayerRequestOutcome> request = (host ? peers.Host : peers.Guest).ExecuteAsync(
                order
            );
            foreach (GameRoot game in new[] { peers.HostGame, peers.GuestGame })
            {
                if (scenario == "injured")
                    game.GetSceneNodeByInstanceID<Officer>("matrix-agent").InjuryPoints = 1;
                if (scenario == "destroyed")
                    game.GetSceneNodeByInstanceID<Planet>(context.Location.InstanceID).IsDestroyed =
                        true;
            }
            string before = MultiplayerSnapshotCodec.ComputeStateFingerprint(peers.HostGame);

            peers.Flush();

            Assert.IsFalse(Completed(request).Accepted);
            Assert.IsNotEmpty(Completed(request).RejectionReason);
            Assert.IsEmpty(peers.HostGame.GetSceneNodesByType<Mission>());
            Assert.AreEqual(
                before,
                MultiplayerSnapshotCodec.ComputeStateFingerprint(peers.HostGame)
            );
            peers.AssertSharedState();
            Assert.IsTrue(peers.Host.CanBeginHostTick);
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void Receive_DuplicateMissionPackets_StartsOnlyOneMission(MultiplayerRole role)
        {
            using PeerPair peers = new();
            string actor = role == MultiplayerRole.Host ? _hostFactionID : _guestFactionID;
            MissionContext host = PrepareMission(
                peers.HostGame,
                actor,
                SabotageMission.MissionTypeID,
                null
            );
            MissionContext guest = PrepareMission(
                peers.GuestGame,
                actor,
                SabotageMission.MissionTypeID,
                null
            );
            Task<PlayerRequestOutcome> request = (
                role == MultiplayerRole.Host ? peers.Host : peers.Guest
            ).ExecuteAsync(CreateMissionRequest(role == MultiplayerRole.Host ? host : guest));
            if (role == MultiplayerRole.Guest)
                peers.ToHost.Enqueue(peers.ToHost.Peek());
            peers.DeliverToHost();
            peers.Host.Pump();
            peers.ToGuest.Enqueue(peers.ToGuest.Peek());

            peers.Flush();

            Assert.IsTrue(Completed(request).Accepted);
            Assert.AreEqual(1, peers.HostGame.GetSceneNodesByType<Mission>().Count);
            Assert.AreEqual(1, peers.GuestGame.GetSceneNodesByType<Mission>().Count);
            peers.AssertSharedState();
        }

        [TestCase(MultiplayerRole.Host, MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Host, MultiplayerRole.Guest)]
        [TestCase(MultiplayerRole.Guest, MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest, MultiplayerRole.Guest)]
        public void Pump_UnreceivedMissionCommitRecovery_RetainsSurvivorMissionAndAllowsResubmission(
            MultiplayerRole issuer,
            MultiplayerRole survivor
        )
        {
            using PeerPair previous = new();
            string actor = issuer == MultiplayerRole.Host ? _hostFactionID : _guestFactionID;
            MissionContext host = PrepareMission(
                previous.HostGame,
                actor,
                SabotageMission.MissionTypeID,
                null
            );
            MissionContext guest = PrepareMission(
                previous.GuestGame,
                actor,
                SabotageMission.MissionTypeID,
                null
            );
            SettlePreparedState(previous);
            (issuer == MultiplayerRole.Host ? previous.Host : previous.Guest).ExecuteAsync(
                CreateMissionRequest(issuer == MultiplayerRole.Host ? host : guest)
            );
            previous.DeliverToHost();
            previous.Host.Pump();
            MultiplayerPacket oldCommit = previous.ToGuest.Dequeue();
            MultiplayerSnapshot checkpoint = MultiplayerSnapshotCodec.Capture(
                survivor == MultiplayerRole.Host
                    ? previous.HostSimulation
                    : previous.GuestSimulation
            );
            using PeerPair resumed = new(checkpoint, TestGameData.Create(previous.HostGame.Config));

            resumed.Guest.Receive(oldCommit);
            resumed.Flush();

            Assert.AreEqual(
                survivor == MultiplayerRole.Host ? 1 : 0,
                resumed.HostGame.GetSceneNodesByType<Mission>().Count
            );
            resumed.AssertSharedState();
            if (survivor == MultiplayerRole.Guest)
            {
                GameRoot game =
                    issuer == MultiplayerRole.Host ? resumed.HostGame : resumed.GuestGame;
                Task<PlayerRequestOutcome> retry = (
                    issuer == MultiplayerRole.Host ? resumed.Host : resumed.Guest
                ).ExecuteAsync(
                    new StartMissionRequest
                    {
                        MissionTypeID = SabotageMission.MissionTypeID,
                        ObservedLocation = game.GetSceneNodeByInstanceID<Planet>(
                            host.Location.InstanceID
                        ),
                        ObservedTarget = game.GetSceneNodeByInstanceID<Regiment>("matrix-target"),
                        MainParticipantIDs = new[] { "matrix-agent" },
                    }
                );
                resumed.Flush();
                Assert.IsTrue(Completed(retry).Accepted);
            }
            Assert.AreEqual(1, resumed.HostGame.GetSceneNodesByType<Mission>().Count);
            for (int day = 0; day < 5; day++)
                AdvanceMissionDay(resumed);
        }

        [TestCase(MultiplayerRole.Host, 314159)]
        [TestCase(MultiplayerRole.Guest, 314159)]
        [TestCase(MultiplayerRole.Host, 271828)]
        [TestCase(MultiplayerRole.Guest, 271828)]
        public void Pump_MissionQueuedDuringTickWithJitter_StartsOnceAfterSafeBoundary(
            MultiplayerRole role,
            int seed
        )
        {
            using PeerPair peers = new();
            string actor = role == MultiplayerRole.Host ? _hostFactionID : _guestFactionID;
            MissionContext host = PrepareMission(
                peers.HostGame,
                actor,
                SabotageMission.MissionTypeID,
                null
            );
            MissionContext guest = PrepareMission(
                peers.GuestGame,
                actor,
                SabotageMission.MissionTypeID,
                null
            );
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
                Task<PlayerRequestOutcome> request = (
                    role == MultiplayerRole.Host ? peers.Host : peers.Guest
                ).ExecuteAsync(CreateMissionRequest(role == MultiplayerRole.Host ? host : guest));
                peers.Host.Pump();
                peers.Guest.Pump();
                Assert.IsEmpty(peers.HostGame.GetSceneNodesByType<Mission>());
                Assert.IsFalse(request.IsCompleted);
                peers.FlushWithJitter(
                    new[] { request },
                    new System.Random(seed),
                    hostTick,
                    guestTick
                );
                Assert.IsTrue(Completed(request).Accepted);
                Assert.AreEqual(1, peers.HostGame.GetSceneNodesByType<Mission>().Count);
                peers.AssertSharedState();
            }
            finally
            {
                (hostTick as IDisposable)?.Dispose();
                (guestTick as IDisposable)?.Dispose();
            }
            for (int day = 0; day < 5; day++)
                AdvanceMissionDay(peers);
        }

        [UnityTest]
        public IEnumerator ExecuteAsync_AllMissionFamiliesFromConfirmationButton_StartsMatchingMissionOnBothPeers()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return ConfirmMissionCases();
            yield return new ExitPlayMode();
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void Pump_PlanetMissionRequest_StartsMatchingMissionOnBothPeers(MultiplayerRole role)
        {
            using PeerPair peers = new();
            bool host = role == MultiplayerRole.Host;
            string actor = host ? _hostFactionID : _guestFactionID;
            string originID = host ? "host-planet" : "guest-planet";
            string targetID = host ? "guest-planet" : "host-planet";
            foreach (GameRoot game in new[] { peers.HostGame, peers.GuestGame })
            {
                game.AttachNode(
                    new SpecialForces
                    {
                        InstanceID = "mission-agent",
                        OwnerInstanceID = actor,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                        AllowedMissionTypeIDs = new List<string>
                        {
                            ReconnaissanceMission.MissionTypeID,
                        },
                    },
                    game.GetSceneNodeByInstanceID<Planet>(originID)
                );
            }
            GameRoot issuerGame = host ? peers.HostGame : peers.GuestGame;
            GameSession issuerSession = host ? peers.HostSimulation : peers.GuestSimulation;
            Planet target = issuerGame.GetSceneNodeByInstanceID<Planet>(targetID);
            SpecialForces agent = issuerGame.GetSceneNodeByInstanceID<SpecialForces>(
                "mission-agent"
            );
            Assert.IsTrue(
                issuerSession
                    .GetService<MissionQueries>()
                    .CanCreateMission(
                        new MissionContext
                        {
                            MissionTypeID = ReconnaissanceMission.MissionTypeID,
                            Location = target,
                            SelectedTarget = target,
                            MainParticipants = new List<IMissionParticipant> { agent },
                        }
                    ),
                "The mission must be eligible before crossing the multiplayer boundary."
            );

            Task<PlayerRequestOutcome> request = (host ? peers.Host : peers.Guest).ExecuteAsync(
                new StartMissionRequest
                {
                    MissionTypeID = ReconnaissanceMission.MissionTypeID,
                    ObservedLocation = target,
                    ObservedTarget = target,
                    MainParticipantIDs = new[] { agent.InstanceID },
                }
            );
            peers.Flush();

            Assert.IsTrue(request.Result.Accepted, request.Result.RejectionReason);
            foreach (GameRoot game in new[] { peers.HostGame, peers.GuestGame })
            {
                Mission mission = game.GetSceneNodesByType<Mission>().Single();
                Assert.IsInstanceOf<ReconnaissanceMission>(mission);
                Assert.IsTrue(mission.HasInitiated);
                Assert.AreEqual(targetID, mission.LocationInstanceID);
            }
            peers.AssertSharedState();
        }

        [TestCase(MultiplayerRole.Host, false)]
        [TestCase(MultiplayerRole.Guest, false)]
        [TestCase(MultiplayerRole.Host, true)]
        [TestCase(MultiplayerRole.Guest, true)]
        public void Pump_EntityMissionRequest_PreservesObservedTargetHierarchy(
            MultiplayerRole role,
            bool targetOfficerAboardShip
        )
        {
            using PeerPair peers = new();
            bool host = role == MultiplayerRole.Host;
            string actor = host ? _hostFactionID : _guestFactionID;
            string opponent = host ? _guestFactionID : _hostFactionID;
            string originID = host ? "host-planet" : "guest-planet";
            string targetID = host ? "guest-planet" : "host-planet";
            string targetShipID = host ? "guest-ship" : "host-ship";
            string missionType = targetOfficerAboardShip
                ? AssassinationMission.MissionTypeID
                : SabotageMission.MissionTypeID;
            foreach (GameRoot game in new[] { peers.HostGame, peers.GuestGame })
            {
                game.AttachNode(
                    new Officer
                    {
                        InstanceID = "mission-agent",
                        OwnerInstanceID = actor,
                        Ratings = new Dictionary<SkillRating, int> { { SkillRating.Combat, 50 } },
                    },
                    game.GetSceneNodeByInstanceID<Planet>(originID)
                );
                ISceneNode target = targetOfficerAboardShip
                    ? new Officer { InstanceID = "mission-target", OwnerInstanceID = opponent }
                    : new Regiment
                    {
                        InstanceID = "mission-target",
                        OwnerInstanceID = opponent,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    };
                game.AttachNode(
                    target,
                    targetOfficerAboardShip
                        ? game.GetSceneNodeByInstanceID<CapitalShip>(targetShipID)
                        : game.GetSceneNodeByInstanceID<Planet>(targetID)
                );
            }
            GameRoot issuerGame = host ? peers.HostGame : peers.GuestGame;
            GameSession issuerSession = host ? peers.HostSimulation : peers.GuestSimulation;
            Planet location = issuerGame.GetSceneNodeByInstanceID<Planet>(targetID);
            ISceneNode observedTarget = issuerGame.GetSceneNodeByInstanceID<ISceneNode>(
                "mission-target"
            );
            Officer agent = issuerGame.GetSceneNodeByInstanceID<Officer>("mission-agent");
            Assert.IsTrue(
                issuerSession
                    .GetService<MissionQueries>()
                    .CanCreateMission(
                        new MissionContext
                        {
                            MissionTypeID = missionType,
                            Location = location,
                            SelectedTarget = observedTarget,
                            MainParticipants = new List<IMissionParticipant> { agent },
                        }
                    ),
                "The entity-target mission must be eligible before serialization."
            );

            Task<PlayerRequestOutcome> request = (host ? peers.Host : peers.Guest).ExecuteAsync(
                new StartMissionRequest
                {
                    MissionTypeID = missionType,
                    ObservedLocation = location,
                    ObservedTarget = observedTarget,
                    MainParticipantIDs = new[] { agent.InstanceID },
                }
            );
            peers.Flush();

            Assert.IsTrue(request.Result.Accepted, request.Result.RejectionReason);
            Assert.AreEqual(1, peers.HostGame.GetSceneNodesByType<Mission>().Count);
            Assert.AreEqual(1, peers.GuestGame.GetSceneNodesByType<Mission>().Count);
            peers.AssertSharedState();
        }

        [Test]
        public void WaitForCampaignCompletionAsync_NoVictory_DoesNotEndRunningGame()
        {
            using PeerPair peers = new();

            Assert.IsFalse(peers.Host.WaitForCampaignCompletionAsync().Result);
            Assert.IsTrue(peers.Host.CanBeginHostTick);
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void WaitForCampaignCompletionAsync_WinningRequest_WaitsForFinalReplayAndHostClosure(
            MultiplayerRole winner
        )
        {
            using PeerPair peers = new(winningRole: winner);
            Task<bool> hostCompletion = null;
            Task<bool> guestCompletion = null;
            peers.HostSimulation.Pipeline.VictoryDeclared += _ =>
                hostCompletion = peers.Host.WaitForCampaignCompletionAsync();
            peers.GuestSimulation.Pipeline.VictoryDeclared += _ =>
                guestCompletion = peers.Guest.WaitForCampaignCompletionAsync();
            Task<PlayerRequestOutcome> winningRequest = ExecuteWinningRequestAsync(peers, winner);

            peers.DeliverToHost();
            peers.Host.Pump();

            Assert.IsNotNull(hostCompletion);
            Assert.IsFalse(hostCompletion.IsCompleted);
            Assert.That(
                peers.ToGuest.Peek().Type,
                Is.EqualTo(MultiplayerMessageType.PlayerActionCommit)
            );
            Assert.IsFalse(peers.Host.TryBeginTick(true));
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsNotNull(guestCompletion);
            Assert.IsFalse(guestCompletion.IsCompleted);
            Assert.IsFalse(peers.Guest.TryCompleteCampaignAfterPeerDeparture());
            peers.DeliverToHost();
            peers.Host.Pump();
            Assert.IsFalse(hostCompletion.IsCompleted);
            Assert.That(
                peers.ToGuest.Peek().Type,
                Is.EqualTo(MultiplayerMessageType.CampaignComplete)
            );
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsFalse(guestCompletion.IsCompleted);
            peers.DeliverToHost();

            Assert.IsTrue(hostCompletion.Result);
            Assert.IsTrue(winningRequest.Result.Accepted);
            Assert.IsTrue(peers.Guest.TryCompleteCampaignAfterPeerDeparture());
            Assert.IsTrue(guestCompletion.Result);
            peers.AssertSharedState();
        }

        [Test]
        public void WaitForCampaignCompletionAsync_GuestPresentationPending_KeepsHostConnected()
        {
            using PeerPair peers = new(winningRole: MultiplayerRole.Host);
            ExecuteWinningRequestAsync(peers, MultiplayerRole.Host);
            peers.Flush();
            Task<bool> hostCompletion = peers.Host.WaitForCampaignCompletionAsync();

            peers.Flush();

            Assert.IsFalse(hostCompletion.IsCompleted);
            Assert.IsFalse(peers.Guest.TryCompleteCampaignAfterPeerDeparture());
            peers.Guest.WaitForCampaignCompletionAsync();
            peers.Flush();
            Assert.IsTrue(hostCompletion.Result);
        }

        [Test]
        public void Pump_RequestQueuedBehindVictory_RejectsWithoutChangingTerminalWorld()
        {
            using PeerPair peers = new(winningRole: MultiplayerRole.Host);
            ExecuteWinningRequestAsync(peers, MultiplayerRole.Host);
            Task<PlayerRequestOutcome> trailing = peers.Host.ExecuteAsync(
                new RenameRequest { EntityID = "host-ship", Name = "Too late" }
            );

            peers.Flush();

            Assert.IsFalse(trailing.Result.Accepted);
            Assert.AreEqual(
                "host-ship",
                peers.HostGame.GetSceneNodeByInstanceID<CapitalShip>("host-ship").DisplayName
            );
            Assert.IsFalse(peers.Host.CanBeginHostTick);
            peers.AssertSharedState();
        }

        [Test]
        public void Receive_CampaignCompletionStateDiffers_FaultsBeforeClosing()
        {
            using PeerPair peers = new(winningRole: MultiplayerRole.Host);
            ExecuteWinningRequestAsync(peers, MultiplayerRole.Host);
            peers.Flush();
            Task<bool> hostCompletion = peers.Host.WaitForCampaignCompletionAsync();
            Task<bool> guestCompletion = peers.Guest.WaitForCampaignCompletionAsync();
            peers.Host.Pump();
            peers.GuestGame.GetSceneNodeByInstanceID<CapitalShip>("guest-ship").DamageControl++;

            peers.DeliverToGuest();
            peers.Guest.Pump();
            peers.DeliverToHost();

            Assert.IsNotNull(peers.Guest.Failure);
            Assert.IsFalse(guestCompletion.Result);
            Assert.IsFalse(hostCompletion.Result);
            Assert.IsFalse(peers.Guest.TryCompleteCampaignAfterPeerDeparture());
        }

        [Test]
        public void Dispose_CampaignCompletionPending_CompletesWithoutApprovingClosure()
        {
            using PeerPair peers = new(winningRole: MultiplayerRole.Host);
            ExecuteWinningRequestAsync(peers, MultiplayerRole.Host);
            peers.Flush();
            Task<bool> completion = peers.Host.WaitForCampaignCompletionAsync();

            peers.Host.Dispose();

            Assert.IsFalse(completion.Result);
        }

        [Test]
        public void Pump_TerminalOutcomeInsideTick_WaitsForBothDayReceipts()
        {
            using PeerPair peers = new();
            foreach (GameSession session in new[] { peers.HostSimulation, peers.GuestSimulation })
                session.Tick.TickCompleted += () =>
                {
                    VictoryResult result = session
                        .GetService<VictoryCommands>()
                        .TryDeclareVictory(
                            session.Game.GetFactionByOwnerInstanceID(_hostFactionID)
                        );
                    session.Pipeline.ProcessResults(new[] { result });
                };
            Assert.IsTrue(peers.Host.TryBeginTick(true));
            peers.DeliverToGuest();
            peers.Guest.Pump();
            Assert.IsTrue(peers.Guest.TryBeginTick(false));

            peers.HostSimulation.Tick.ProcessTick();
            Task<bool> hostCompletion = peers.Host.WaitForCampaignCompletionAsync();
            peers.Host.Pump();

            Assert.IsFalse(hostCompletion.IsCompleted);
            Assert.IsFalse(
                peers.ToGuest.Any(packet => packet.Type == MultiplayerMessageType.CampaignComplete)
            );
            peers.GuestSimulation.Tick.ProcessTick();
            Task<bool> guestCompletion = peers.Guest.WaitForCampaignCompletionAsync();
            peers.Flush();
            Assert.IsTrue(hostCompletion.Result);
            Assert.IsFalse(guestCompletion.IsCompleted);
            Assert.IsTrue(peers.Guest.TryCompleteCampaignAfterPeerDeparture());
            Assert.IsTrue(guestCompletion.Result);
            Assert.IsFalse(peers.Host.CanBeginHostTick);
            peers.AssertSharedState();
        }

        [Test]
        public void Receive_DuplicateCampaignCompletion_ClosesOnlyAfterAcknowledgment()
        {
            using PeerPair peers = new(winningRole: MultiplayerRole.Host);
            ExecuteWinningRequestAsync(peers, MultiplayerRole.Host);
            peers.Flush();
            Task<bool> hostCompletion = peers.Host.WaitForCampaignCompletionAsync();
            Task<bool> guestCompletion = peers.Guest.WaitForCampaignCompletionAsync();
            peers.Host.Pump();
            MultiplayerPacket offer = peers.ToGuest.Peek();
            peers.ToGuest.Enqueue(offer);

            peers.DeliverToGuest();
            peers.Guest.Pump();

            Assert.IsFalse(hostCompletion.IsCompleted);
            Assert.IsFalse(guestCompletion.IsCompleted);
            MultiplayerPacket receipt = peers.ToHost.Peek();
            peers.ToHost.Enqueue(receipt);
            peers.DeliverToHost();
            Assert.IsNull(peers.Host.Failure);
            Assert.IsNull(peers.Guest.Failure);
            Assert.IsTrue(hostCompletion.Result);
            Assert.IsTrue(peers.Guest.TryCompleteCampaignAfterPeerDeparture());
            Assert.IsTrue(guestCompletion.Result);
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void TryCompleteCampaignAfterPeerDeparture_NoTerminalReceipt_DoesNotApproveClosure(
            MultiplayerRole role
        )
        {
            using PeerPair peers = new(winningRole: MultiplayerRole.Host);
            ExecuteWinningRequestAsync(peers, MultiplayerRole.Host);
            peers.Flush();
            MultiplayerTurnProtocol subject =
                role == MultiplayerRole.Host ? peers.Host : peers.Guest;
            Task<bool> completion = subject.WaitForCampaignCompletionAsync();

            Assert.IsFalse(subject.TryCompleteCampaignAfterPeerDeparture());

            subject.Dispose();
            Assert.IsFalse(completion.Result);
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public void Pump_CampaignCompletionSendFails_DoesNotApproveClosure(MultiplayerRole role)
        {
            using PeerPair peers = new(winningRole: MultiplayerRole.Host);
            ExecuteWinningRequestAsync(peers, MultiplayerRole.Host);
            peers.Flush();
            Task<bool> hostCompletion = peers.Host.WaitForCampaignCompletionAsync();
            Task<bool> guestCompletion = peers.Guest.WaitForCampaignCompletionAsync();
            peers.RejectHostSends = role == MultiplayerRole.Host;
            peers.RejectGuestSends = role == MultiplayerRole.Guest;

            peers.Flush();

            MultiplayerTurnProtocol subject =
                role == MultiplayerRole.Host ? peers.Host : peers.Guest;
            Assert.IsInstanceOf<IOException>(subject.Failure);
            Assert.IsFalse(subject.TryCompleteCampaignAfterPeerDeparture());
            Assert.IsFalse(
                role == MultiplayerRole.Host ? hostCompletion.Result : guestCompletion.Result
            );
        }

        [TestCase(MultiplayerMessageType.CampaignComplete)]
        [TestCase(MultiplayerMessageType.CampaignCompleteReceipt)]
        public void Receive_PreviousInitializationCompletion_DoesNotEndCurrentGame(
            MultiplayerMessageType type
        )
        {
            using PeerPair peers = new();
            MultiplayerTurnProtocol subject =
                type == MultiplayerMessageType.CampaignComplete ? peers.Guest : peers.Host;

            subject.Receive(
                new MultiplayerPacket(
                    type,
                    JsonUtility.ToJson(
                        new PacketMetadata { initializationID = Guid.NewGuid().ToString("N") }
                    )
                )
            );
            subject.Pump();

            Assert.IsNull(subject.Failure);
            Assert.IsFalse(subject.TryCompleteCampaignAfterPeerDeparture());
            Assert.IsTrue(peers.Host.CanBeginHostTick);
        }

        [TestCase(MultiplayerRole.Host)]
        [TestCase(MultiplayerRole.Guest)]
        public async Task ExecuteAsync_RenamingOwnedShip_PublishesCommittedStateOnBothPeersAsync(
            MultiplayerRole role
        )
        {
            using PeerPair peers = new();
            string shipID = role == MultiplayerRole.Host ? "host-ship" : "guest-ship";
            string observedHostName = null;
            string observedGuestName = null;
            int hostChanges = 0;
            int guestChanges = 0;
            peers.Host.StateChanged += () =>
            {
                hostChanges++;
                observedHostName = peers
                    .HostGame.GetSceneNodeByInstanceID<CapitalShip>(shipID)
                    .DisplayName;
            };
            peers.Guest.StateChanged += () =>
            {
                guestChanges++;
                observedGuestName = peers
                    .GuestGame.GetSceneNodeByInstanceID<CapitalShip>(shipID)
                    .DisplayName;
            };

            Task<PlayerRequestOutcome> completion = (
                role == MultiplayerRole.Host ? peers.Host : peers.Guest
            ).ExecuteAsync(new RenameRequest { EntityID = shipID, Name = "Renamed once" });
            peers.Flush();

            Assert.IsTrue((await completion).Accepted);
            Assert.AreEqual(1, hostChanges);
            Assert.AreEqual(1, guestChanges);
            Assert.AreEqual("Renamed once", observedHostName);
            Assert.AreEqual("Renamed once", observedGuestName);
        }

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
            peers.GuestGame.UseDeterministicRuntimeIdentifiers = true;
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
            MultiplayerTurnProtocol proposer = hostProposes ? peers.Host : peers.Guest;
            MultiplayerTurnProtocol responder = hostProposes ? peers.Guest : peers.Host;
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
            Assert.AreEqual(1, peers.HostGame.IDCounter);
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
            MultiplayerTurnProtocol protocol =
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
                new MultiplayerTurnProtocol(
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
            MultiplayerTurnProtocol first = guestRespondsFirst ? previous.Guest : previous.Host;
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
            MultiplayerTurnProtocol firstAgain = guestRespondsFirst
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
            MultiplayerTurnProtocol second = guestRespondsFirst ? recovered.Host : recovered.Guest;
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
        /// Publishes fixture setup reactions before issuing network requests.
        /// </summary>
        /// <param name="peers">The matching prepared simulations.</param>
        private static void SettlePreparedState(PeerPair peers)
        {
            foreach (GameSession session in new[] { peers.HostSimulation, peers.GuestSimulation })
                Assert.IsTrue(session.ExecuteCommandAsync(() => true).GetAwaiter().GetResult());
            peers.AssertSharedState();
        }

        /// <summary>
        /// Covers ownership, selection, eligibility, and stale-state rejections for both peers.
        /// </summary>
        /// <returns>The invalid mission scenarios.</returns>
        private static IEnumerable<TestCaseData> InvalidMissionCases()
        {
            foreach (MultiplayerRole role in new[] { MultiplayerRole.Host, MultiplayerRole.Guest })
            foreach (
                string scenario in new[]
                {
                    "empty",
                    "missing",
                    "foreign",
                    "duplicate",
                    "missing-decoy",
                    "foreign-decoy",
                    "overlap",
                    "duplicate-decoy",
                    "injured",
                    "destroyed",
                    "wrong-planet",
                    "unsupported-repeat",
                    "unknown-mission",
                }
            )
                yield return new TestCaseData(role, scenario);
        }

        /// <summary>
        /// Exercises authored controls after the Play Mode domain transition.
        /// </summary>
        /// <returns>The confirmation and asynchronous UI completion sequence.</returns>
        private IEnumerator ConfirmMissionCases()
        {
            GameObject holder = new GameObject("Mission UI audit");
            holder.SetActive(false);
            GameObject root = UnityEngine.Object.Instantiate(
                AssetDatabase
                    .LoadAssetAtPath<GameObject>(
                        "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab"
                    )
                    .GetComponentInChildren<StrategyWindowLayerView>(true)
                    .gameObject,
                holder.transform
            );
            holder.SetActive(true);
            UIWindowManager manager = root.GetComponent<UIWindowManager>();
            StrategyWindowLayerView layer = root.GetComponent<StrategyWindowLayerView>();
            manager.SetContentSource(TestContent.Assets);
            try
            {
                foreach (TestCaseData scenario in MissionStartCases())
                {
                    MultiplayerRole role = (MultiplayerRole)scenario.Arguments[0];
                    string type = (string)scenario.Arguments[1];
                    ResearchDiscipline? discipline = (ResearchDiscipline?)scenario.Arguments[2];
                    bool repeating = (bool)scenario.Arguments[3];
                    using PeerPair peers = new();
                    string actor = role == MultiplayerRole.Host ? _hostFactionID : _guestFactionID;
                    MissionContext host = PrepareMission(peers.HostGame, actor, type, discipline);
                    MissionContext guest = PrepareMission(peers.GuestGame, actor, type, discipline);
                    bool isHost = role == MultiplayerRole.Host;
                    GameRoot game = isHost ? peers.HostGame : peers.GuestGame;
                    GameSession session = isHost ? peers.HostSimulation : peers.GuestSimulation;
                    MissionContext context = isHost ? host : guest;
                    context.IsRepeating = repeating;
                    MissionOption option = session
                        .GetService<MissionQueries>()
                        .GetAvailableMissionOptions(context)
                        .Single(candidate =>
                            candidate.MissionTypeID == type && candidate.Discipline == discipline
                        );
                    string label = option.DisplayName + (repeating ? " (Repeat)" : string.Empty);
                    UIContext ui = TestContent.CreateUIContext(
                        game,
                        TestContent.CreateThemeLibrary(),
                        new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
                    );
                    bool closed = false;
                    MissionCreateWindowController controller = new(
                        session,
                        isHost ? peers.Host : peers.Guest,
                        () => ui,
                        _ => { },
                        layer,
                        manager,
                        () => Vector2Int.zero,
                        window =>
                        {
                            closed = true;
                            manager.DestroyWindow(window);
                        },
                        () => { },
                        getShowMissionOdds: () => false
                    );
                    controller.Initialize(new MissionWindowActions());
                    Planet planet = (Planet)context.Location;
                    StrategyMissionTarget target = new(
                        new GalaxyMapPlanet(
                            planet.GetParentOfType<PlanetSector>(),
                            planet,
                            planet.PlanetIconPath
                        ),
                        context.SelectedTarget
                    );
                    Assert.IsTrue(
                        controller.Open(
                            target,
                            context.MainParticipants.Cast<ISceneNode>().ToArray()
                        ),
                        $"{role} {label}: open"
                    );
                    UIWindow window = manager.Windows.Single();
                    manager.TryGetWindowView(window, out MissionCreateWindowView view);
                    controller.RenderWindow(view, window);
                    view.GetComponentsInChildren<Button>(true)
                        .Single(button => button.name == "DropdownButtonImage")
                        .onClick.Invoke();
                    controller.RenderWindow(view, window);
                    StrategyDropdownItemView row =
                        view.GetComponentsInChildren<StrategyDropdownItemView>(true)
                            .Single(item =>
                                item.gameObject.activeSelf
                                && item.GetComponentsInChildren<TMPro.TMP_Text>(true)
                                    .Any(text => text.text == label)
                            );
                    row.GetComponentInChildren<Button>(true).onClick.Invoke();
                    controller.RenderWindow(view, window);
                    Button confirm = view.GetComponentsInChildren<Button>(true)
                        .Single(button => button.name == "OkButtonImage");
                    Assert.IsTrue(
                        confirm.IsInteractable(),
                        $"{role} {label}: confirmation enabled"
                    );
                    confirm.onClick.Invoke();
                    confirm.onClick.Invoke();
                    Assert.IsFalse(closed, $"{role} {label}: queued request is not acceptance");
                    peers.Flush();
                    for (int frame = 0; frame < 10 && !closed; frame++)
                        yield return null;
                    Assert.IsTrue(closed, $"{role} {label}: confirmation did not complete");
                    foreach (GameRoot peerGame in new[] { peers.HostGame, peers.GuestGame })
                    {
                        Mission mission = peerGame.GetSceneNodesByType<Mission>().Single();
                        Assert.AreEqual(type, mission.ConfigKey, $"{role} {label}");
                        if (mission is ResearchMission research)
                            Assert.AreEqual(discipline, research.Discipline);
                        if (mission is EspionageMission espionage)
                            Assert.AreEqual(repeating, espionage.IsRepeating);
                    }
                    peers.AssertSharedState();
                    TestContext.WriteLine($"Confirmed {role}: {label}");
                    yield return null;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
            }
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

        /// <summary>
        /// Submits the fixture's winning bombardment through its authenticated request owner.
        /// </summary>
        /// <param name="peers">The two independently simulated participants.</param>
        /// <param name="winner">The participant whose fleet destroys the opposing headquarters.</param>
        /// <returns>The ordered bombardment outcome.</returns>
        private static Task<PlayerRequestOutcome> ExecuteWinningRequestAsync(
            PeerPair peers,
            MultiplayerRole winner
        )
        {
            string prefix = winner == MultiplayerRole.Host ? "host" : "guest";
            string target = winner == MultiplayerRole.Host ? "guest-planet" : "host-planet";
            MultiplayerTurnProtocol issuer =
                winner == MultiplayerRole.Host ? peers.Host : peers.Guest;
            return issuer.ExecuteAsync(
                new BombardRequest
                {
                    ItemIDs = new[] { prefix + "-fleet" },
                    ObservedTarget = peers.HostGame.GetSceneNodeByInstanceID<Planet>(target),
                    Type = BombardmentType.DestroyPlanet,
                }
            );
        }

        /// <summary>
        /// Enumerates every mission family, research discipline, and repeat option for both peers.
        /// </summary>
        /// <returns>The supported mission scenarios.</returns>
        private static IEnumerable<TestCaseData> MissionStartCases()
        {
            string[] missions =
            {
                DiplomacyMission.MissionTypeID,
                EspionageMission.MissionTypeID,
                ReconnaissanceMission.MissionTypeID,
                RecruitmentMission.MissionTypeID,
                InciteUprisingMission.MissionTypeID,
                SubdueUprisingMission.MissionTypeID,
                JediTrainingMission.MissionTypeID,
                AbductionMission.MissionTypeID,
                AssassinationMission.MissionTypeID,
                RescueMission.MissionTypeID,
                SabotageMission.MissionTypeID,
            };
            foreach (MultiplayerRole role in new[] { MultiplayerRole.Host, MultiplayerRole.Guest })
            {
                foreach (string mission in missions)
                    yield return new TestCaseData(role, mission, null, false);
                foreach (
                    ResearchDiscipline discipline in Enum.GetValues(typeof(ResearchDiscipline))
                )
                {
                    if (discipline != ResearchDiscipline.None)
                        yield return new TestCaseData(
                            role,
                            ResearchMission.MissionTypeID,
                            discipline,
                            false
                        );
                }
                yield return new TestCaseData(role, EspionageMission.MissionTypeID, null, true);
            }
        }

        /// <summary>
        /// Prepares an eligible participant roster and objective for one mission family.
        /// </summary>
        /// <param name="game">The simulation receiving the fixture.</param>
        /// <param name="actor">The issuing faction.</param>
        /// <param name="missionType">The mission family.</param>
        /// <param name="discipline">The research discipline, when applicable.</param>
        /// <returns>The eligible local mission context.</returns>
        private static MissionContext PrepareMission(
            GameRoot game,
            string actor,
            string missionType,
            ResearchDiscipline? discipline
        )
        {
            bool friendly =
                missionType
                is DiplomacyMission.MissionTypeID
                    or RecruitmentMission.MissionTypeID
                    or ResearchMission.MissionTypeID
                    or JediTrainingMission.MissionTypeID
                    or SubdueUprisingMission.MissionTypeID;
            string originID = actor == _hostFactionID ? "host-planet" : "guest-planet";
            string opponent = actor == _hostFactionID ? _guestFactionID : _hostFactionID;
            string targetID =
                friendly ? originID
                : actor == _hostFactionID ? "guest-planet"
                : "host-planet";
            Planet origin = game.GetSceneNodeByInstanceID<Planet>(originID);
            Planet location = game.GetSceneNodeByInstanceID<Planet>(targetID);
            location.AddVisitor(actor);
            IMissionParticipant agent =
                missionType == ReconnaissanceMission.MissionTypeID
                    ? new SpecialForces
                    {
                        InstanceID = "matrix-agent",
                        OwnerInstanceID = actor,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                        AllowedMissionTypeIDs = new List<string>
                        {
                            ReconnaissanceMission.MissionTypeID,
                        },
                    }
                    : new Officer
                    {
                        InstanceID = "matrix-agent",
                        OwnerInstanceID = actor,
                        IsMain = true,
                        IsJediTrainer = missionType == JediTrainingMission.MissionTypeID,
                        ForceValue =
                            missionType == JediTrainingMission.MissionTypeID
                                ? game.Config.Jedi.ForceQualifiedThreshold + 1
                                : 0,
                        ShipResearch = 50,
                        TroopResearch = 50,
                        FacilityResearch = 50,
                        Ratings = new Dictionary<SkillRating, int>
                        {
                            { SkillRating.Combat, 50 },
                            { SkillRating.Diplomacy, 50 },
                            { SkillRating.Espionage, 50 },
                            { SkillRating.Leadership, 50 },
                        },
                    };
            game.AttachNode(agent, origin);
            List<IMissionParticipant> participants = new() { agent };
            ISceneNode selected = location;
            if (missionType == RecruitmentMission.MissionTypeID)
                game.GetUnrecruitedOfficers()
                    .Add(
                        new Officer
                        {
                            InstanceID = "matrix-recruit",
                            RecruitingFactionInstanceIDs = new List<string> { actor },
                        }
                    );
            if (missionType == SubdueUprisingMission.MissionTypeID)
                location.BeginUprising();
            if (missionType == ResearchMission.MissionTypeID)
            {
                location.EnergyCapacity = 10;
                ManufacturingType production = discipline switch
                {
                    ResearchDiscipline.ShipDesign => ManufacturingType.Ship,
                    ResearchDiscipline.TroopTraining => ManufacturingType.Troop,
                    _ => ManufacturingType.Building,
                };
                game.AttachNode(
                    new Building
                    {
                        InstanceID = "matrix-facility",
                        OwnerInstanceID = actor,
                        ProductionType = production,
                        ProcessRate = 1,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    location
                );
            }
            if (missionType == JediTrainingMission.MissionTypeID)
            {
                Officer student = new Officer
                {
                    InstanceID = "matrix-student",
                    OwnerInstanceID = actor,
                    ForceValue = 1,
                };
                game.AttachNode(student, origin);
                participants.Add(student);
            }
            if (
                missionType
                is AbductionMission.MissionTypeID
                    or AssassinationMission.MissionTypeID
                    or RescueMission.MissionTypeID
            )
            {
                Officer target = new Officer
                {
                    InstanceID = "matrix-target",
                    OwnerInstanceID = missionType == RescueMission.MissionTypeID ? actor : opponent,
                    IsCaptured = missionType == RescueMission.MissionTypeID,
                };
                game.AttachNode(target, location);
                selected = target;
            }
            if (missionType == SabotageMission.MissionTypeID)
            {
                Regiment target = new Regiment
                {
                    InstanceID = "matrix-target",
                    OwnerInstanceID = opponent,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                game.AttachNode(target, location);
                selected = target;
            }
            return new MissionContext
            {
                MissionTypeID = missionType,
                Location = location,
                SelectedTarget = selected,
                MainParticipants = participants,
                DecoyParticipants = new List<IMissionParticipant>(),
                Discipline = discipline,
            };
        }

        /// <summary>
        /// Copies a planned mission into the player request contract.
        /// </summary>
        /// <param name="context">The observed objective and live participants.</param>
        /// <returns>The network-serializable request.</returns>
        private static StartMissionRequest CreateMissionRequest(MissionContext context)
        {
            return new StartMissionRequest
            {
                MissionTypeID = context.MissionTypeID,
                ObservedLocation = (Planet)context.Location,
                ObservedTarget = context.SelectedTarget,
                MainParticipantIDs = context.MainParticipants.Select(p => p.InstanceID).ToArray(),
                DecoyParticipantIDs = context.DecoyParticipants.Select(p => p.InstanceID).ToArray(),
                Discipline = context.Discipline,
                IsRepeating = context.IsRepeating,
            };
        }

        /// <summary>
        /// Completes one authorized day and verifies both independent worlds.
        /// </summary>
        /// <param name="peers">The participating simulations.</param>
        private static void AdvanceMissionDay(PeerPair peers)
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

        private sealed class MissionWindowActions : IMissionCreateWindowActions
        {
            /// <summary>
            /// Leaves unrelated information navigation outside the confirmation fixture.
            /// </summary>
            public void OpenMissionCreateInfo() { }
        }

        /// <summary>
        /// Owns two independent simulations and delayed reliable ordered packet queues.
        /// </summary>
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
            public MultiplayerTurnProtocol Host { get; }
            public MultiplayerTurnProtocol Guest { get; }
            public bool RejectHostSends { get; set; }
            public bool RejectGuestSends { get; set; }
            public string InitializationID { get; } = Guid.NewGuid().ToString("N");

            /// <summary>
            /// Builds matching graphs from one in-memory snapshot and attaches independent services.
            /// </summary>
            /// <param name="contested">Whether opposing fleets initially share the host planet.</param>
            /// <param name="winningRole">The participant prepared to destroy the opposing headquarters, or null for normal play.</param>
            public PeerPair(bool contested = false, MultiplayerRole? winningRole = null)
            {
                GameRoot source = CreateSource(contested);
                if (winningRole.HasValue)
                {
                    source.Summary.VictoryCondition = GameVictoryCondition.Headquarters;
                    string prefix = winningRole == MultiplayerRole.Host ? "host" : "guest";
                    string target =
                        winningRole == MultiplayerRole.Host ? "guest-planet" : "host-planet";
                    source
                        .GetSceneNodeByInstanceID<CapitalShip>(prefix + "-ship")
                        .CanDestroyPlanets = true;
                    source.MoveNode(
                        source.GetSceneNodeByInstanceID<Fleet>(prefix + "-fleet"),
                        source.GetSceneNodeByInstanceID<Planet>(target)
                    );
                }
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
                Host = new MultiplayerTurnProtocol(
                    MultiplayerRole.Host,
                    _hostFactionID,
                    _guestFactionID,
                    HostSimulation,
                    HostClock,
                    SendHost,
                    InitializationID
                );
                Guest = new MultiplayerTurnProtocol(
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
                HostGame.UseDeterministicRuntimeIdentifiers = true;
                GuestGame.UseDeterministicRuntimeIdentifiers = true;
                HostGame.LocalPlayerFactionID = _hostFactionID;
                GuestGame.LocalPlayerFactionID = _guestFactionID;
                HostClock = new GameManager(() => HostGame, HostSimulation.Tick);
                GuestClock = new GameManager(() => GuestGame, GuestSimulation.Tick);
                Host = new MultiplayerTurnProtocol(
                    MultiplayerRole.Host,
                    _hostFactionID,
                    _guestFactionID,
                    HostSimulation,
                    HostClock,
                    SendHost,
                    InitializationID
                );
                Guest = new MultiplayerTurnProtocol(
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
