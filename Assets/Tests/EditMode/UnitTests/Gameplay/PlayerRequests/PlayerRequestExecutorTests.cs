using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.PlayerRequests;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.PlayerRequests;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Gameplay.PlayerRequests
{
    [TestFixture]
    public sealed class PlayerRequestExecutorTests
    {
        private const string _ownerID = "FNALL1";
        private const string _opponentID = "FNEMP1";

        private Faction _actor;
        private Planet _destination;
        private PlayerRequestExecutor _executor;
        private GameRoot _game;
        private GameManager _gameManager;
        private Faction _opponent;
        private Planet _opponentPlanet;
        private Planet _origin;
        private GameSession _session;

        /// <summary>
        /// Creates an isolated two-human-player session without files or runtime singletons.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _game = TestGame.Create(TestConfig.Create());
            _game.Summary.Seed = 481;
            _game.Config.ProbabilityTables.Mission.Foil = new Dictionary<int, int> { { -1000, 0 } };
            _actor = new Faction { InstanceID = _ownerID };
            _opponent = new Faction { InstanceID = _opponentID };
            _game.GetFactions().AddRange(new[] { _actor, _opponent });
            _game.SetFactionController(_ownerID, "first", PlayerControllerType.Human);
            _game.SetFactionController(_opponentID, "second", PlayerControllerType.Human);
            _game.LocalPlayerFactionID = _ownerID;
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _origin = CreatePlanet("origin", _ownerID, sector, 0);
            _destination = CreatePlanet("destination", _ownerID, sector, 100);
            _opponentPlanet = CreatePlanet("opponent", _opponentID, sector, 200);
            _session = TestContent.CreateGameSession(_game);
            _gameManager = new GameManager(() => _session.Game, _session.Tick);
            _executor = new PlayerRequestExecutor(_session, _gameManager);
        }

        /// <summary>
        /// Disconnects the isolated session's result observers.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            _session?.Dispose();
        }

        [Test]
        public async Task ExecuteAsync_SuspendedTick_DefersLocalMutationUntilBoundaryAsync()
        {
            _game.SetGameSpeed(TickSpeed.Fast);
            Fleet fleet = CreateFleet("queued-fleet", _ownerID, _origin, out _);
            string originalName = fleet.DisplayName;
            IEnumerator tick = _session.Tick.ProcessTickIncrementally();
            try
            {
                Assert.IsTrue(tick.MoveNext());
                LocalPlayerRequestDispatcher dispatcher = new(_executor, () => _ownerID);
                Task<PlayerRequestOutcome> action = dispatcher.ExecuteAsync(
                    new RenameRequest { EntityID = fleet.InstanceID, Name = "Queued name" }
                );
                Assert.IsFalse(action.IsCompleted);
                Assert.AreEqual(originalName, fleet.DisplayName);
                while (tick.MoveNext()) { }
                _session.PumpCommands();
                Assert.IsTrue((await action).Accepted);
                Assert.AreEqual("Queued name", fleet.DisplayName);
            }
            finally
            {
                (tick as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void Execute_SuspendedTick_RejectsDirectMutation()
        {
            _game.SetGameSpeed(TickSpeed.Fast);
            Fleet fleet = CreateFleet("queued-fleet", _ownerID, _origin, out _);
            string originalName = fleet.DisplayName;
            IEnumerator tick = _session.Tick.ProcessTickIncrementally();
            try
            {
                Assert.IsTrue(tick.MoveNext());
                PlayerRequestOutcome result = _executor.Execute(
                    _ownerID,
                    new RenameRequest { EntityID = fleet.InstanceID, Name = "Forbidden" }
                );
                Assert.IsFalse(result.Accepted);
                Assert.AreEqual(originalName, fleet.DisplayName);
            }
            finally
            {
                (tick as IDisposable)?.Dispose();
            }
        }

        [Test]
        public void Constructor_NullSession_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new PlayerRequestExecutor(null, _gameManager)
            );
        }

        [Test]
        public void Execute_UnknownActor_DoesNotMutateAnotherFaction()
        {
            Fleet fleet = CreateFleet("fleet", _ownerID, _origin, out _);

            PlayerRequestOutcome result = _executor.Execute(
                "unbound",
                new RenameRequest { EntityID = fleet.InstanceID, Name = "Changed" }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsNotEmpty(result.RejectionReason);
            Assert.AreEqual("fleet", fleet.DisplayName);
        }

        [Test]
        public void Execute_NullAction_ReturnsExplicitRejection()
        {
            PlayerRequestOutcome result = _executor.Execute(_ownerID, null);

            Assert.IsFalse(result.Accepted);
            Assert.IsNotEmpty(result.RejectionReason);
        }

        [Test]
        public void Execute_OpponentActor_UsesActorWithoutChangingLocalPerspective()
        {
            Fleet fleet = CreateFleet("opponent fleet", _opponentID, _opponentPlanet, out _);

            PlayerRequestOutcome result = _executor.Execute(
                _opponentID,
                new RenameRequest { EntityID = fleet.InstanceID, Name = "Guest fleet" }
            );

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual("Guest fleet", fleet.DisplayName);
            Assert.AreEqual(_ownerID, _game.LocalPlayerFactionID);
            Assert.AreSame(_game, _session.Game);
        }

        [Test]
        public void Execute_RenameOtherFactionEntity_PreservesItsName()
        {
            Fleet fleet = CreateFleet("opponent fleet", _opponentID, _opponentPlanet, out _);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new RenameRequest { EntityID = fleet.InstanceID, Name = "Changed" }
            );

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("opponent fleet", fleet.DisplayName);
        }

        [Test]
        public void Execute_RenameCapitalShip_PreservesManualNameAgainstAutomation()
        {
            CreateFleet("fleet", _ownerID, _origin, out CapitalShip ship);
            _actor.ManageNaming = true;
            ship.ShipNamePoolID = "names";
            _actor.ShipNamePools.Add(
                new FactionNamePool
                {
                    NamePoolID = "names",
                    Names = new List<string> { "Automatic" },
                }
            );

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new RenameRequest { EntityID = ship.InstanceID, Name = "Manual" }
            );
            _session.GetService<NamingCommands>().ProcessFaction(_actor);

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(ship.HasAssignedName);
            Assert.AreEqual("Manual", ship.DisplayName);
        }

        [Test]
        public void Execute_MoveWrongFactionOfficer_DoesNotReparentOrPublishResults()
        {
            Officer officer = CreateOfficer("opponent officer", _opponentID, _opponentPlanet);
            int published = 0;
            _session.Pipeline.ResultsResolved += results => published += results.Count;

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new MoveRequest
                {
                    ItemIDs = new[] { officer.InstanceID },
                    DestinationID = _destination.InstanceID,
                }
            );

            Assert.IsFalse(result.Accepted);
            Assert.AreSame(_opponentPlanet, officer.GetParent());
            Assert.AreEqual(0, published);
        }

        [Test]
        public void Execute_MoveOwnedOfficer_UsesExistingImmediateResultsOnce()
        {
            Officer officer = CreateOfficer("officer", _ownerID, _origin);
            List<GameResult> published = new List<GameResult>();
            _session.Pipeline.ResultsResolved += published.AddRange;

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new MoveRequest
                {
                    ItemIDs = new[] { officer.InstanceID },
                    DestinationID = _destination.InstanceID,
                }
            );

            Assert.IsTrue(result.Accepted);
            Assert.AreSame(_destination, officer.GetParent());
            Assert.AreEqual(1, published.OfType<GameObjectEnrouteResult>().Count());
        }

        [Test]
        public void Execute_MoveSelectionContainsMissingIdentity_PreservesCompleteSelection()
        {
            Officer officer = CreateOfficer("officer", _ownerID, _origin);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new MoveRequest
                {
                    ItemIDs = new[] { officer.InstanceID, "missing" },
                    DestinationID = _destination.InstanceID,
                }
            );

            Assert.IsFalse(result.Accepted);
            Assert.AreSame(_origin, officer.GetParent());
        }

        [Test]
        public void Execute_MoveHeadquarters_ClearsDepartingHeadquartersMarkers()
        {
            _actor.Settings.Headquarters.IsMobile = true;
            _actor.HQInstanceID = _origin.InstanceID;
            _origin.IsHeadquarters = true;
            Building headquarters = new Building
            {
                InstanceID = "headquarters",
                OwnerInstanceID = _ownerID,
                BuildingType = BuildingType.Headquarters,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(headquarters, _origin);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new MoveRequest
                {
                    ItemIDs = new[] { headquarters.InstanceID },
                    DestinationID = _destination.InstanceID,
                }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsFalse(_origin.IsHeadquarters);
            Assert.IsNull(_actor.HQInstanceID);
        }

        [Test]
        public void Execute_CreateFleet_ReturnsAttachedFleetIdentity()
        {
            Fleet source = CreateFleet("source", _ownerID, _origin, out CapitalShip ship);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new CreateFleetRequest { ItemIDs = new[] { ship.InstanceID } }
            );

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(result.CreatedEntityID, ship.GetParent().InstanceID);
            Assert.AreSame(_origin, ship.GetParent().GetParent());
            Assert.IsNull(source.GetParent());
        }

        [Test]
        public void Execute_ScrapIncludesOtherFactionItem_PreservesBothItems()
        {
            Regiment first = CreateRegiment("owned", _ownerID, _origin);
            Regiment second = CreateRegiment("other", _opponentID, _opponentPlanet);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ScrapRequest { ItemIDs = new[] { first.InstanceID, second.InstanceID } }
            );

            Assert.IsFalse(result.Accepted);
            Assert.AreSame(_origin, first.GetParent());
            Assert.AreSame(_opponentPlanet, second.GetParent());
        }

        [Test]
        public void Execute_CancelPartiallyAvailableSelection_CancelsEligibleItems()
        {
            Regiment owned = CreateQueuedRegiment("owned", _ownerID, _origin);
            Regiment other = CreateQueuedRegiment("other", _opponentID, _opponentPlanet);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new CancelManufacturingRequest
                {
                    ItemIDs = new[] { owned.InstanceID, "missing", other.InstanceID },
                }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsNull(owned.GetParent());
            Assert.AreSame(_opponentPlanet, other.GetParent());
            Assert.IsFalse(_origin.GetManufacturingQueue().ContainsKey(ManufacturingType.Troop));
        }

        [Test]
        public void Execute_RetireContainsOtherFactionPerson_PreservesCompleteSelection()
        {
            Officer owned = CreateOfficer("owned", _ownerID, _origin);
            Officer other = CreateOfficer("other", _opponentID, _opponentPlanet);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new RetirePersonnelRequest
                {
                    ItemIDs = new[] { owned.InstanceID, other.InstanceID },
                }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsFalse(owned.IsRetired);
            Assert.IsFalse(other.IsRetired);
        }

        [Test]
        public void Execute_StartMissionWithOtherFactionAgent_DoesNotCreateMission()
        {
            Officer other = CreateOfficer("other", _opponentID, _opponentPlanet);
            Regiment target = CreateRegiment("target", _opponentID, _opponentPlanet);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new StartMissionRequest
                {
                    MissionTypeID = SabotageMission.MissionTypeID,
                    ObservedLocation = _opponentPlanet,
                    ObservedTarget = target,
                    MainParticipantIDs = new[] { other.InstanceID },
                }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(_game.GetSceneNodesByType<Mission>());
            Assert.AreSame(_opponentPlanet, other.GetParent());
        }

        [Test]
        public void Execute_StartMissionWithStaleObservedTarget_PreservesObservedEligibility()
        {
            Officer participant = CreateOfficer("participant", _ownerID, _origin);
            Regiment liveTarget = CreateRegiment("target", _opponentID, _opponentPlanet);
            liveTarget.ManufacturingStatus = ManufacturingStatus.Building;
            Planet observedPlanet = new Planet { InstanceID = _opponentPlanet.InstanceID };
            Regiment observedTarget = new Regiment
            {
                InstanceID = liveTarget.InstanceID,
                OwnerInstanceID = _opponentID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            observedTarget.SetParent(observedPlanet);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new StartMissionRequest
                {
                    MissionTypeID = SabotageMission.MissionTypeID,
                    ObservedLocation = observedPlanet,
                    ObservedTarget = observedTarget,
                    MainParticipantIDs = new[] { participant.InstanceID },
                }
            );

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, _game.GetSceneNodesByType<Mission>().Count);
            Assert.AreEqual(ManufacturingStatus.Building, liveTarget.ManufacturingStatus);
        }

        [Test]
        public void Execute_AbortOtherFactionMission_DoesNotInterruptMission()
        {
            SabotageMission mission = new SabotageMission
            {
                InstanceID = "other mission",
                OwnerInstanceID = _opponentID,
                LocationInstanceID = _origin.InstanceID,
                HasInitiated = true,
            };
            _game.AttachNode(mission, _origin);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new AbortMissionsRequest { ItemIDs = new[] { mission.InstanceID } }
            );

            Assert.IsFalse(result.Accepted);
            Assert.AreSame(_origin, mission.GetParent());
        }

        [Test]
        public void Execute_SetOtherFactionOfficerRank_PreservesCurrentCommand()
        {
            Officer officer = CreateOfficer("officer", _opponentID, _opponentPlanet);
            officer.CurrentRank = OfficerRank.General;

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new SetOfficerRankRequest
                {
                    OfficerID = officer.InstanceID,
                    Rank = OfficerRank.None,
                }
            );

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(OfficerRank.General, officer.CurrentRank);
        }

        [Test]
        public void Execute_EnableProductionAutomation_AlsoRunsAlreadyEnabledNaming()
        {
            CreateFleet("fleet", _ownerID, _origin, out CapitalShip ship);
            _actor.ManageNaming = true;
            ship.ShipNamePoolID = "names";
            _actor.ShipNamePools.Add(
                new FactionNamePool
                {
                    NamePoolID = "names",
                    Names = new List<string> { "Named" },
                }
            );

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ToggleAdvisorOptionRequest { Option = PlayerAdvisorOption.ManageProduction }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(_actor.ManageProduction);
            Assert.AreEqual("Named", ship.DisplayName);
        }

        [Test]
        public void Execute_ToggleOtherActorAdvisorOption_ChangesOnlyBoundActor()
        {
            PlayerRequestOutcome result = _executor.Execute(
                _opponentID,
                new ToggleAdvisorOptionRequest { Option = PlayerAdvisorOption.AgentAdvice }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(_actor.AgentAdvice);
            Assert.IsFalse(_opponent.AgentAdvice);
        }

        [Test]
        public void Execute_ReadOtherFactionMessage_DoesNotChangeReadState()
        {
            Message message = AddMessage(_opponent, "other message");

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ReadMessagesRequest { MessageIDs = new[] { message.InstanceID } }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsFalse(message.Read);
        }

        [Test]
        public void Execute_ReadOwnMessage_AcknowledgesNotificationThroughReadState()
        {
            Message message = AddMessage(_actor, "message");

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ReadMessagesRequest { MessageIDs = new[] { message.InstanceID } }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(message.Read);
            Assert.IsTrue(message.NotificationAcknowledged);
        }

        [Test]
        public void Execute_AcknowledgeOwnMessage_DoesNotMarkItRead()
        {
            Message message = AddMessage(_actor, "message");

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new AcknowledgeMessagesRequest { MessageIDs = new[] { message.InstanceID } }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(message.NotificationAcknowledged);
            Assert.IsFalse(message.Read);
        }

        [Test]
        public void Execute_MarkAllMessagesRead_LeavesOtherFactionUnread()
        {
            Message first = AddMessage(_actor, "first");
            Message second = AddMessage(_actor, "second");
            Message other = AddMessage(_opponent, "other");

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new MarkAllMessagesReadRequest()
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(first.Read);
            Assert.IsTrue(second.Read);
            Assert.IsFalse(other.Read);
        }

        [Test]
        public void Execute_RemoveMixedFactionMessages_RemovesOnlyActorMessages()
        {
            Message own = AddMessage(_actor, "own");
            Message other = AddMessage(_opponent, "other");

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new RemoveMessagesRequest
                {
                    MessageIDs = new[] { own.InstanceID, other.InstanceID },
                }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsFalse(_actor.Messages.Values.Any(bucket => bucket.Contains(own)));
            Assert.IsTrue(_opponent.Messages.Values.Any(bucket => bucket.Contains(other)));
        }

        [Test]
        public void Execute_ToggleAllMessageNotifications_ClearsCategoryOverrides()
        {
            _actor.AdvisorMessageNotifications[MessageType.Fleet] = false;

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ToggleMessageNotificationRequest { MessageType = null }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsFalse(_actor.AdvisorMessageNotificationsEnabled);
            Assert.IsEmpty(_actor.AdvisorMessageNotifications);
            Assert.IsTrue(_opponent.AdvisorMessageNotificationsEnabled);
        }

        [Test]
        public void Execute_ConsumeMissionContinuation_ChangesOnlySelectedReport()
        {
            Message selected = AddMessage(_actor, "selected");
            Message other = AddMessage(_actor, "other");
            selected.MissionContinuationPending = true;
            other.MissionContinuationPending = true;

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ConsumeMissionContinuationRequest { MessageID = selected.InstanceID }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsFalse(selected.MissionContinuationPending);
            Assert.IsTrue(other.MissionContinuationPending);
        }

        [Test]
        public void Execute_SetSpeed_UpdatesSharedGraphAndSessionClock()
        {
            int speedChanges = 0;
            _gameManager.GameSpeedChanged += () => speedChanges++;

            PlayerRequestOutcome result = _executor.Execute(
                _opponentID,
                new SetSpeedRequest { Speed = TickSpeed.Fast }
            );

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(TickSpeed.Fast, _game.GetGameSpeed());
            Assert.AreEqual(1, speedChanges);
            Assert.IsTrue(
                _gameManager.TryAdvanceTickTimer(_game.Config.GameSpeed.FastTickIntervalSeconds)
            );
        }

        [Test]
        public void Execute_UnknownSpeed_PreservesClockState()
        {
            TickSpeed before = _gameManager.GetGameSpeed();

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new SetSpeedRequest { Speed = (TickSpeed)999 }
            );

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(before, _gameManager.GetGameSpeed());
        }

        [Test]
        public void Execute_UnknownManufacturingTemplate_DoesNotCreateProductionItems()
        {
            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new SetManufacturingOrderRequest
                {
                    ProducerPlanetID = _origin.InstanceID,
                    DestinationID = _origin.InstanceID,
                    ManufacturingType = ManufacturingType.Ship,
                    TemplateTypeID = "not-authored",
                    Quantity = 1,
                }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(_origin.GetManufacturingQueue());
        }

        [Test]
        public void Execute_ReserveOtherFactionManufacturing_DoesNotChangeReservation()
        {
            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ToggleManufacturingReservationRequest
                {
                    ProducerPlanetID = _opponentPlanet.InstanceID,
                    ManufacturingType = ManufacturingType.Ship,
                }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsFalse(_opponentPlanet.IsManufacturingReserved(ManufacturingType.Ship));
        }

        [Test]
        public void Execute_NoPendingCombat_RejectsWithoutThrowing()
        {
            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ResolveCombatRequest { PlanetID = _origin.InstanceID, Tick = 0 }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsTrue(_session.Tick.IsSettled);
        }

        [Test]
        public void Execute_ClearCommittedFleetRoute_PreservesCurrentTransit()
        {
            Fleet fleet = CreateFleet("fleet", _ownerID, _origin, out _);
            PlayerRequestOutcome set = _executor.Execute(
                _ownerID,
                new SetFleetRouteRequest
                {
                    ItemIDs = new[] { fleet.InstanceID },
                    WaypointPlanetIDs = new[] { _destination.InstanceID, _origin.InstanceID },
                }
            );
            MovementState currentLeg = fleet.Movement;

            PlayerRequestOutcome clear = _executor.Execute(
                _ownerID,
                new ClearFleetRouteRequest { ItemIDs = new[] { fleet.InstanceID } }
            );

            Assert.IsTrue(set.Accepted);
            Assert.IsTrue(clear.Accepted);
            Assert.IsNotNull(currentLeg);
            Assert.AreSame(currentLeg, fleet.Movement);
            Assert.IsEmpty(fleet.Waypoints);
        }

        [Test]
        public void Execute_SetOwnedOfficerRank_ReplacesExistingCommandThroughDomainResults()
        {
            Officer incumbent = CreateOfficer("incumbent", _ownerID, _origin);
            Officer replacement = CreateOfficer("replacement", _ownerID, _origin);
            incumbent.CurrentRank = OfficerRank.General;
            List<GameResult> results = new List<GameResult>();
            _session.Pipeline.ResultsResolved += results.AddRange;

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new SetOfficerRankRequest
                {
                    OfficerID = replacement.InstanceID,
                    Rank = OfficerRank.General,
                }
            );

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(OfficerRank.None, incumbent.CurrentRank);
            Assert.AreEqual(OfficerRank.General, replacement.CurrentRank);
            Assert.AreEqual(2, results.OfType<CommandKindChangedResult>().Count());
        }

        [Test]
        public void Execute_ReserveOwnManufacturingLane_ChangesOnlySelectedLane()
        {
            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ToggleManufacturingReservationRequest
                {
                    ProducerPlanetID = _origin.InstanceID,
                    ManufacturingType = ManufacturingType.Ship,
                }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(_origin.IsManufacturingReserved(ManufacturingType.Ship));
            Assert.IsFalse(_origin.IsManufacturingReserved(ManufacturingType.Troop));
        }

        [Test]
        public void Execute_RetargetEmptyProductionLane_AcceptsExistingDomainNoOp()
        {
            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new RetargetManufacturingRequest
                {
                    ProducerPlanetID = _origin.InstanceID,
                    DestinationID = _destination.InstanceID,
                    ManufacturingType = ManufacturingType.Ship,
                }
            );

            Assert.IsTrue(result.Accepted);
            Assert.IsEmpty(_origin.GetManufacturingQueue());
        }

        [Test]
        public void Execute_ResolveStalePendingBattle_PreservesCurrentDecision()
        {
            CreateFleet("first", _ownerID, _origin, out _);
            CreateFleet("second", _opponentID, _origin, out _);
            _session.Tick.ProcessTick();
            Assert.IsTrue(
                _session
                    .GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            );

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick - 1,
                }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsTrue(_session.GetService<SpaceCombatCommands>().HasPendingDecision);
        }

        [Test]
        public void Execute_ResolveBattleByNonparticipant_PreservesCurrentDecision()
        {
            CreateFleet("first", _ownerID, _origin, out _);
            CreateFleet("second", _opponentID, _origin, out _);
            _game.GetFactions().Add(new Faction { InstanceID = "observer" });
            _game.SetFactionController("observer", "third", PlayerControllerType.Human);
            _session.Tick.ProcessTick();
            Assert.IsTrue(
                _session
                    .GetService<SpaceCombatCommands>()
                    .TryGetPendingCombat(out PendingCombatResult pending)
            );

            PlayerRequestOutcome result = _executor.Execute(
                "observer",
                new ResolveCombatRequest
                {
                    PlanetID = pending.PlanetInstanceID,
                    Tick = pending.Tick,
                }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsTrue(_session.GetService<SpaceCombatCommands>().HasPendingDecision);
        }

        [TestCase("first line\r\nsecond line\n  final line  ")]
        [TestCase(" \t\r\n ")]
        public void Execute_SendPeerChatRawText_PreservesSubmittedBody(string text)
        {
            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new SendPeerChatRequest { Text = text }
            );

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(text, _opponent.Messages[MessageType.Chat].Single().Body);
        }

        [TestCase(null)]
        [TestCase("")]
        public void Execute_SendPeerChatEmptyText_RejectsWithoutDelivery(string text)
        {
            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new SendPeerChatRequest { Text = text }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(_actor.Messages[MessageType.Chat]);
            Assert.IsEmpty(_opponent.Messages[MessageType.Chat]);
        }

        [TestCase(_ownerID, _opponentID)]
        [TestCase(_opponentID, _ownerID)]
        public void Execute_SendPeerChat_RecordsOnlyOppositeHumanHistory(
            string senderID,
            string recipientID
        )
        {
            _game.CurrentTick = 37;

            PlayerRequestOutcome result = _executor.Execute(
                senderID,
                new SendPeerChatRequest { Text = "Peer message" }
            );

            Faction sender = _game.GetFactionByOwnerInstanceID(senderID);
            Faction recipient = _game.GetFactionByOwnerInstanceID(recipientID);
            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(sender.Messages.Values.All(bucket => bucket.Count == 0));
            Message received = recipient.Messages[MessageType.Chat].Single();
            Assert.AreEqual("Peer message", received.Body);
            Assert.AreEqual(MessageType.Chat, received.Type);
            Assert.AreEqual(37, received.CreatedTick);
        }

        [TestCase("Player handle", "Faction handle", "Message from Player handle")]
        [TestCase(null, "Faction handle", "Message from Faction handle")]
        [TestCase(null, null, "Message from FNALL1")]
        public void Execute_SendPeerChatSenderName_UsesBoundPlayerThenFactionIdentity(
            string playerName,
            string factionName,
            string expectedTitle
        )
        {
            _game.GetFactionPlayer(_ownerID).DisplayName = playerName;
            _actor.DisplayName = factionName;

            _executor.Execute(_ownerID, new SendPeerChatRequest { Text = "Message" });

            Assert.AreEqual(expectedTitle, _opponent.Messages[MessageType.Chat].Single().Title);
        }

        [Test]
        public void Execute_SendPeerChatWithoutOtherHuman_RejectsWithoutHistory()
        {
            _game.SetFactionController(_opponentID, "computer", PlayerControllerType.AI);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new SendPeerChatRequest { Text = "Message" }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(_actor.Messages[MessageType.Chat]);
            Assert.IsEmpty(_opponent.Messages[MessageType.Chat]);
        }

        [Test]
        public void Execute_SendPeerChatFromComputerFaction_RejectsWithoutHistory()
        {
            _game.SetFactionController(_ownerID, "computer", PlayerControllerType.AI);

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new SendPeerChatRequest { Text = "Message" }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(_actor.Messages[MessageType.Chat]);
            Assert.IsEmpty(_opponent.Messages[MessageType.Chat]);
        }

        [Test]
        public void Execute_SendPeerChatWithAmbiguousRecipients_RejectsWithoutDelivery()
        {
            Faction third = new Faction { InstanceID = "third" };
            _game.GetFactions().Add(third);
            _game.SetFactionController(
                third.InstanceID,
                "third player",
                PlayerControllerType.Human
            );

            PlayerRequestOutcome result = _executor.Execute(
                _ownerID,
                new SendPeerChatRequest { Text = "Message" }
            );

            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(_opponent.Messages[MessageType.Chat]);
            Assert.IsEmpty(third.Messages[MessageType.Chat]);
        }

        [Test]
        public void Execute_SendPeerChat_PublishesRecipientDeliveryOnce()
        {
            List<MessageDeliveredResult> delivered = new List<MessageDeliveredResult>();
            _session.Pipeline.MessageDelivered += delivered.Add;

            _executor.Execute(_ownerID, new SendPeerChatRequest { Text = "Message" });

            Assert.AreEqual(1, delivered.Count);
            Assert.AreSame(_opponent, delivered[0].Recipient);
            Assert.AreSame(_opponent.Messages[MessageType.Chat].Single(), delivered[0].Message);
        }

        [Test]
        public void Execute_SendPeerChatWithDeterministicIdentities_ConsumesOneRuntimeIdentity()
        {
            _game.UseDeterministicRuntimeIdentifiers = true;
            long identityBefore = _game.RuntimeIdentifierIndex;
            long randomBefore = _game.RandomIndex;

            _executor.Execute(_ownerID, new SendPeerChatRequest { Text = "Message" });

            Message message = _opponent.Messages[MessageType.Chat].Single();
            Assert.AreEqual(identityBefore + 1, _game.RuntimeIdentifierIndex);
            Assert.IsTrue(Guid.TryParseExact(message.InstanceID, "N", out _));
            Assert.AreEqual(identityBefore + 1, _game.RuntimeIdentifierIndex);
            Assert.AreEqual(randomBefore, _game.RandomIndex);
        }

        [Test]
        public void Execute_SendPeerChatOnEquivalentSessions_ProducesSameMessageIdentity()
        {
            _game.UseDeterministicRuntimeIdentifiers = true;
            GameRoot copy = SerializationHelper.Deserialize<GameRoot>(
                SerializationHelper.Serialize(_game)
            );
            copy.UseDeterministicRuntimeIdentifiers = true;
            using GameSession otherSession = TestContent.CreateGameSession(copy);
            PlayerRequestExecutor other = new PlayerRequestExecutor(
                otherSession,
                new GameManager(() => otherSession.Game, otherSession.Tick)
            );
            SendPeerChatRequest action = new SendPeerChatRequest { Text = "Same message" };

            PlayerRequestOutcome first = _executor.Execute(_ownerID, action);
            PlayerRequestOutcome second = other.Execute(_ownerID, action);

            Assert.IsTrue(first.Accepted);
            Assert.IsTrue(second.Accepted);
            Assert.AreEqual(
                _opponent.Messages[MessageType.Chat].Single().InstanceID,
                copy.GetFactionByOwnerInstanceID(_opponentID)
                    .Messages[MessageType.Chat]
                    .Single()
                    .InstanceID
            );
            Assert.AreEqual(_game.RuntimeIdentifierIndex, copy.RuntimeIdentifierIndex);
        }

        /// <summary>
        /// Attaches one colonized planet to the fixture's galaxy.
        /// </summary>
        /// <param name="id">The planet identity.</param>
        /// <param name="ownerID">The owning faction identity.</param>
        /// <param name="sector">The containing sector.</param>
        /// <param name="x">The authored horizontal coordinate.</param>
        /// <returns>The attached planet.</returns>
        private Planet CreatePlanet(string id, string ownerID, PlanetSector sector, int x)
        {
            Planet planet = new Planet
            {
                InstanceID = id,
                OwnerInstanceID = ownerID,
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = x,
                PositionY = 0,
            };
            _game.AttachNode(planet, sector);
            return planet;
        }

        /// <summary>
        /// Attaches one deployed officer with sufficient mission ratings.
        /// </summary>
        /// <param name="id">The officer identity.</param>
        /// <param name="ownerID">The owning faction identity.</param>
        /// <param name="parent">The containing planet or vessel.</param>
        /// <returns>The attached officer.</returns>
        private Officer CreateOfficer(string id, string ownerID, ISceneNode parent)
        {
            Officer officer = new Officer
            {
                InstanceID = id,
                OwnerInstanceID = ownerID,
                AllowedRanks = new[]
                {
                    OfficerRank.Commander,
                    OfficerRank.Admiral,
                    OfficerRank.General,
                },
                Ratings = new Dictionary<SkillRating, int>
                {
                    { SkillRating.Combat, 50 },
                    { SkillRating.Diplomacy, 50 },
                    { SkillRating.Espionage, 50 },
                    { SkillRating.Leadership, 50 },
                },
            };
            _game.AttachNode(officer, parent);
            return officer;
        }

        /// <summary>
        /// Attaches a fleet containing one completed capital ship.
        /// </summary>
        /// <param name="id">The fleet identity and display name.</param>
        /// <param name="ownerID">The owning faction identity.</param>
        /// <param name="planet">The containing planet.</param>
        /// <param name="ship">Receives the attached capital ship.</param>
        /// <returns>The attached fleet.</returns>
        private Fleet CreateFleet(string id, string ownerID, Planet planet, out CapitalShip ship)
        {
            Fleet fleet = new Fleet
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = ownerID,
            };
            ship = new CapitalShip
            {
                InstanceID = id + " ship",
                OwnerInstanceID = ownerID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
                Hyperdrive = 1,
            };
            _game.AttachNode(fleet, planet);
            _game.AttachNode(ship, fleet);
            return fleet;
        }

        /// <summary>
        /// Attaches one completed surface regiment.
        /// </summary>
        /// <param name="id">The regiment identity.</param>
        /// <param name="ownerID">The owning faction identity.</param>
        /// <param name="planet">The containing planet.</param>
        /// <returns>The attached regiment.</returns>
        private Regiment CreateRegiment(string id, string ownerID, Planet planet)
        {
            Regiment regiment = new Regiment
            {
                InstanceID = id,
                OwnerInstanceID = ownerID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(regiment, planet);
            return regiment;
        }

        /// <summary>
        /// Adds an unfinished regiment to its producer's existing production queue contract.
        /// </summary>
        /// <param name="id">The regiment identity.</param>
        /// <param name="ownerID">The owning faction identity.</param>
        /// <param name="producer">The producing planet.</param>
        /// <returns>The attached queued regiment.</returns>
        private Regiment CreateQueuedRegiment(string id, string ownerID, Planet producer)
        {
            Regiment regiment = CreateRegiment(id, ownerID, producer);
            regiment.ManufacturingStatus = ManufacturingStatus.Building;
            regiment.ProducerPlanetID = producer.InstanceID;
            producer.GetManufacturingQueue()[ManufacturingType.Troop] = new List<IManufacturable>
            {
                regiment,
            };
            return regiment;
        }

        /// <summary>
        /// Adds a uniquely identified unread message to a faction's delivered messages.
        /// </summary>
        /// <param name="faction">The receiving faction.</param>
        /// <param name="id">The message identity.</param>
        /// <returns>The delivered message.</returns>
        private static Message AddMessage(Faction faction, string id)
        {
            Message message = new StatusMessage(MessageType.Fleet, id, id) { InstanceID = id };
            faction.AddMessage(message);
            return message;
        }
    }
}
