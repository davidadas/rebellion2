using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Tests.Helpers;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class FogOfWarObserverTests : FogOfWarTestBase
    {
        public enum SabotageTargetKind
        {
            Building,
            CapitalShip,
            Regiment,
            SpecialForces,
            Starfighter,
        }

        private FogOfWarObserver _observer;
        private GameResultBus _results;

        /// <summary>
        /// Creates the subject for the arranged faction scene.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _results = new GameResultBus();
            FogOfWarQueries queries = new FogOfWarQueries(_game);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            _observer = new FogOfWarObserver(
                _game,
                new FogOfWarCommands(_game, recorder),
                queries,
                recorder
            );
        }

        [Test]
        public void Connect_SabotagedObject_RegistersSnapshotInvalidation()
        {
            _observer.Connect(_results);
            _coruscant.EnergyCapacity = 1;
            Building mine = CreateBuilding("MINE1", _empire);
            _game.AttachNode(mine, _coruscant);

            Officer han = CreateOfficer("HAN", _alliance);
            _game.AttachNode(han, _hoth);

            new FogOfWarRecorder().RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.DetachNode(mine);

            _results.Publish(
                new GameObjectSabotagedResult
                {
                    DestroyedObject = mine,
                    DestroyedBy = han,
                    Context = _coruscant,
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsFalse(snapshot.Buildings.Any(b => b.InstanceID == "MINE1"));
        }

        [Test]
        public void Dispose_IntelligenceResult_StopsRecordingObservations()
        {
            _observer.Connect(_results);
            _observer.Dispose();

            _results.Publish(
                new IntelligenceRevealedResult
                {
                    Recipient = _alliance,
                    Observations = new List<ISceneNode> { _coruscant },
                    Tick = 12,
                }
            );

            Assert.IsEmpty(_alliance.Fog.Snapshots);
        }

        [Test]
        public void HandleResults_ObservationForOneFaction_UpdatesOnlyRecipientAtResultTick()
        {
            _game.CurrentTick = 90;

            List<GameResult> reactions = _observer.HandleResults(
                new[]
                {
                    new IntelligenceRevealedResult
                    {
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { _coruscant },
                        Tick = 12,
                    },
                }
            );

            Assert.IsEmpty(reactions);
            Assert.IsEmpty(_empire.Fog.Snapshots);
            Assert.AreEqual(
                12,
                _alliance
                    .Fog
                    .Snapshots[_coreSector.InstanceID]
                    .Planets[_coruscant.InstanceID]
                    .TickCaptured
            );
        }

        [Test]
        public void ProcessResults_ReleasedOfficer_RemovesOwnerAndCaptorObservations()
        {
            Officer captive = CreateOfficer("CAPTIVE", _alliance);
            captive.IsCaptured = true;
            captive.CaptorInstanceID = _empire.InstanceID;
            _game.AttachNode(captive, _coruscant);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);
            recorder.RecordPlanetSnapshot(_empire, _coruscant, _coreSector, 10);
            captive.IsCaptured = false;
            captive.CaptorInstanceID = null;

            _observer.ProcessResults(
                new GameResult[]
                {
                    new OfficerCaptureStateResult
                    {
                        TargetOfficer = captive,
                        IsCaptured = false,
                        CaptorInstanceID = _empire.InstanceID,
                        Tick = 12,
                    },
                }
            );

            Assert.IsFalse(_alliance.Fog.EntityLastSeenAt.ContainsKey(captive.InstanceID));
            Assert.IsFalse(_empire.Fog.EntityLastSeenAt.ContainsKey(captive.InstanceID));
        }

        [Test]
        public void ProcessResults_AbductionTargetUnavailable_RemovesOwnersStaleLocation()
        {
            Faction observer = new Faction { InstanceID = "OBSERVER" };
            _game.GetFactions().Add(observer);
            Officer participant = CreateOfficer("PARTICIPANT", _alliance);
            Officer target = CreateOfficer("TARGET", _empire);
            _game.AttachNode(participant, _hoth);
            _game.AttachNode(target, _coruscant);
            AbductionMission mission = AbductionMission.TryCreate(
                new MissionContext
                {
                    Game = _game,
                    OwnerInstanceId = _alliance.InstanceID,
                    Location = _coruscant,
                    SelectedTarget = target,
                    MainParticipants = new List<IMissionParticipant> { participant },
                    DecoyParticipants = new List<IMissionParticipant>(),
                }
            );
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);
            recorder.RecordPlanetSnapshot(observer, _coruscant, _coreSector, 10);
            MakeTatooineImperial();
            _game.MoveNode(target, _tatooine);

            _observer.ProcessResults(
                new GameResult[]
                {
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        Location = _coruscant,
                        Outcome = MissionOutcome.Failed,
                        CompletionReason = MissionCompletionReason.TargetUnavailable,
                    },
                }
            );

            Assert.IsFalse(_alliance.Fog.EntityLastSeenAt.ContainsKey(target.InstanceID));
            Assert.IsFalse(
                _alliance
                    .Fog.Snapshots[_coreSector.InstanceID]
                    .Planets[_coruscant.InstanceID]
                    .Officers.Any(officer => officer.InstanceID == target.InstanceID)
            );
            Assert.IsTrue(observer.Fog.EntityLastSeenAt.ContainsKey(target.InstanceID));
        }

        [Test]
        public void ProcessResults_AbductionTargetUnavailableAtLocation_PreservesOwnersSighting()
        {
            Officer participant = CreateOfficer("PARTICIPANT", _alliance);
            Officer target = CreateOfficer("TARGET", _empire);
            _game.AttachNode(participant, _hoth);
            _game.AttachNode(target, _coruscant);
            AbductionMission mission = AbductionMission.TryCreate(
                new MissionContext
                {
                    Game = _game,
                    OwnerInstanceId = _alliance.InstanceID,
                    Location = _coruscant,
                    SelectedTarget = target,
                    MainParticipants = new List<IMissionParticipant> { participant },
                    DecoyParticipants = new List<IMissionParticipant>(),
                }
            );
            new FogOfWarRecorder().RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);
            target.IsCaptured = true;

            _observer.ProcessResults(
                new GameResult[]
                {
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        Location = _coruscant,
                        Outcome = MissionOutcome.Failed,
                        CompletionReason = MissionCompletionReason.TargetUnavailable,
                    },
                }
            );

            Assert.AreEqual(
                _coruscant.InstanceID,
                _alliance.Fog.EntityLastSeenAt[target.InstanceID]
            );
            Assert.IsTrue(
                _alliance
                    .Fog.Snapshots[_coreSector.InstanceID]
                    .Planets[_coruscant.InstanceID]
                    .Officers.Any(officer => officer.InstanceID == target.InstanceID)
            );
        }

        [Test]
        public void ProcessResults_AssassinationTargetUnavailable_RemovesOwnersStaleLocation()
        {
            Officer target = CreateOfficer("TARGET", _empire);
            _game.AttachNode(target, _coruscant);
            AssassinationMission mission = AssassinationMission.TryCreate(
                CreateTargetingMissionContext(target)
            );

            RecordTargetThenRemoveFromPlanet(target);
            ProcessUnavailableTarget(mission);

            AssertTargetForgotten(target.InstanceID);
        }

        [Test]
        public void ProcessResults_RescueTargetUnavailable_RemovesOwnersStaleLocation()
        {
            Officer target = CreateOfficer("TARGET", _alliance);
            target.IsCaptured = true;
            target.CaptorInstanceID = _empire.InstanceID;
            _game.AttachNode(target, _coruscant);
            RescueMission mission = RescueMission.TryCreate(CreateTargetingMissionContext(target));

            RecordTargetThenRemoveFromPlanet(target);
            ProcessUnavailableTarget(mission);

            AssertTargetForgotten(target.InstanceID);
        }

        [TestCase(SabotageTargetKind.Building)]
        [TestCase(SabotageTargetKind.Regiment)]
        [TestCase(SabotageTargetKind.SpecialForces)]
        [TestCase(SabotageTargetKind.Starfighter)]
        public void ProcessResults_SabotagePlanetTargetUnavailable_RemovesOwnersStaleLocation(
            SabotageTargetKind targetKind
        )
        {
            ISceneNode target = CreatePlanetSabotageTarget(targetKind);
            if (target is Building)
                _coruscant.EnergyCapacity = 1;
            _game.AttachNode(target, _coruscant);
            SabotageMission mission = SabotageMission.TryCreate(
                CreateTargetingMissionContext(target)
            );

            RecordTargetThenRemoveFromPlanet(target);
            ProcessUnavailableTarget(mission);

            AssertTargetForgotten(target.GetInstanceID());
        }

        [TestCase(SabotageTargetKind.CapitalShip)]
        [TestCase(SabotageTargetKind.Regiment)]
        [TestCase(SabotageTargetKind.Starfighter)]
        public void ProcessResults_SabotageFleetTargetUnavailable_RemovesOwnersStaleLocation(
            SabotageTargetKind targetKind
        )
        {
            ISceneNode target = CreateFleetSabotageTarget(targetKind);
            SabotageMission mission = SabotageMission.TryCreate(
                CreateTargetingMissionContext(target)
            );

            RecordTargetThenRemoveFromPlanet(target);
            ProcessUnavailableTarget(mission);

            AssertTargetForgotten(target.GetInstanceID());
        }

        [Test]
        public void ProcessResults_SabotageTargetUnavailableAtLocation_PreservesOwnersSighting()
        {
            Starfighter target = CreateStarfighter("TARGET", _empire);
            _game.AttachNode(target, _coruscant);
            SabotageMission mission = SabotageMission.TryCreate(
                CreateTargetingMissionContext(target)
            );
            new FogOfWarRecorder().RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            target.ManufacturingStatus = ManufacturingStatus.Building;

            ProcessUnavailableTarget(mission);

            Assert.AreEqual(
                _coruscant.InstanceID,
                _alliance.Fog.EntityLastSeenAt[target.InstanceID]
            );
            Assert.IsTrue(
                _alliance
                    .Fog.Snapshots[_coreSector.InstanceID]
                    .Planets[_coruscant.InstanceID]
                    .Starfighters.Any(starfighter => starfighter.InstanceID == target.InstanceID)
            );
        }

        [Test]
        public void ProcessResults_SabotageTargetAboardInTransitFleet_RemovesOwnersStaleLocation()
        {
            Starfighter target = (Starfighter)CreateFleetSabotageTarget(
                SabotageTargetKind.Starfighter
            );
            SabotageMission mission = SabotageMission.TryCreate(
                CreateTargetingMissionContext(target)
            );
            new FogOfWarRecorder().RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            target.GetParentOfType<Fleet>().Movement = new MovementState();

            ProcessUnavailableTarget(mission);

            AssertTargetForgotten(target.InstanceID);
        }

        [Test]
        public void HandleResults_NullResultAfterValidObservation_KeepsEarlierSnapshotAndThrows()
        {
            Assert.Throws<System.NullReferenceException>(() =>
                _observer.HandleResults(
                    new IntelligenceRevealedResult[]
                    {
                        new IntelligenceRevealedResult
                        {
                            Recipient = _alliance,
                            Observations = new List<ISceneNode> { _coruscant },
                            Tick = 12,
                        },
                        null,
                        new IntelligenceRevealedResult
                        {
                            Recipient = _alliance,
                            Observations = new List<ISceneNode> { _tatooine },
                            Tick = 13,
                        },
                    }
                )
            );

            Assert.IsTrue(
                _alliance
                    .Fog.Snapshots[_coreSector.InstanceID]
                    .Planets.ContainsKey(_coruscant.InstanceID)
            );
            Assert.IsFalse(_alliance.Fog.Snapshots.ContainsKey(_outerRim.InstanceID));
        }

        [Test]
        public void ProcessResults_SabotageObservedByBothFactions_PreservesOtherFactionSnapshot()
        {
            Faction observer = new Faction { InstanceID = "OBSERVER" };
            _game.GetFactions().Add(observer);
            _coruscant.EnergyCapacity = 1;
            Building mine = CreateBuilding("MINE1", _empire);
            _game.AttachNode(mine, _coruscant);
            new FogOfWarRecorder().RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);
            new FogOfWarRecorder().RecordPlanetSnapshot(observer, _coruscant, _coreSector, 10);

            _observer.ProcessResults(
                new[]
                {
                    new GameObjectSabotagedResult
                    {
                        DestroyedObject = mine,
                        DestroyedBy = CreateOfficer("SABOTEUR", _alliance),
                    },
                }
            );

            Assert.IsEmpty(
                _alliance
                    .Fog
                    .Snapshots[_coreSector.InstanceID]
                    .Planets[_coruscant.InstanceID]
                    .Buildings
            );
            Assert.AreEqual(
                "MINE1",
                observer
                    .Fog.Snapshots[_coreSector.InstanceID]
                    .Planets[_coruscant.InstanceID]
                    .Buildings.Single()
                    .InstanceID
            );
            Assert.AreSame(_coruscant, mine.GetParent());
        }

        [Test]
        public void HandleResults_SelectedObservation_RevealsOnlySelectedObject()
        {
            _coruscant.EnergyCapacity = 1;
            Building building = new Building
            {
                InstanceID = "IMPERIAL_FACILITY",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Officer officer = CreateOfficer("VADER", _empire);
            _game.AttachNode(building, _coruscant);
            _game.AttachNode(officer, _coruscant);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { building },
                    },
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.AreEqual(42, snapshot.TickCaptured);
            Assert.AreEqual(PlanetIntelligenceCategory.None, snapshot.RevealedCategories);
            Assert.AreEqual("IMPERIAL_FACILITY", snapshot.Buildings.Single().InstanceID);
            Assert.IsEmpty(snapshot.Officers);
            Assert.AreNotEqual(PlanetIntelligenceCategory.All, snapshot.RevealedCategories);
        }

        [Test]
        public void HandleResults_SelectedCapitalShip_RevealsPartialFleetWithoutSiblingsOrCargo()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip selectedShip = AddCapitalShip(fleet, _empire, "SELECTED_SHIP");
            AddCapitalShip(fleet, _empire, "HIDDEN_SHIP");
            _game.AttachNode(CreateOfficer("HIDDEN_OFFICER", _empire), selectedShip);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { selectedShip },
                    },
                }
            );

            Fleet knownFleet = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single();
            CapitalShip knownShip = knownFleet.GetChildren<CapitalShip>().Single();
            Assert.AreEqual("IMPERIAL_FLEET", knownFleet.InstanceID);
            Assert.AreEqual("SELECTED_SHIP", knownShip.InstanceID);
            Assert.IsEmpty(knownShip.GetChildren<Officer>());
        }

        [Test]
        public void HandleResults_SelectedNestedOfficer_RevealsAncestryWithoutSiblings()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            Officer selectedOfficer = CreateOfficer("SELECTED_OFFICER", _empire);
            _game.AttachNode(selectedOfficer, ship);
            _game.AttachNode(CreateOfficer("HIDDEN_OFFICER", _empire), ship);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { selectedOfficer },
                    },
                }
            );

            Fleet knownFleet = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single();
            CapitalShip knownShip = knownFleet.GetChildren<CapitalShip>().Single();
            Assert.AreEqual("IMPERIAL_FLEET", knownFleet.InstanceID);
            Assert.AreEqual("STAR_DESTROYER", knownShip.InstanceID);
            Assert.AreEqual(
                "SELECTED_OFFICER",
                knownShip.GetChildren<Officer>().Single().InstanceID
            );
        }

        [Test]
        public void HandleResults_SelectedPlanetRegiment_PreservesPreviouslyObservedRegiment()
        {
            Regiment first = CreateRegiment("FIRST", _empire);
            Regiment second = CreateRegiment("SECOND", _empire);
            _game.AttachNode(first, _coruscant);
            _game.AttachNode(second, _coruscant);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 41,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { first },
                    },
                }
            );
            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { second },
                    },
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            CollectionAssert.AreEquivalent(
                new[] { "FIRST", "SECOND" },
                snapshot.Regiments.Select(regiment => regiment.InstanceID)
            );
        }

        [Test]
        public void HandleResults_SelectedPlanetStarfighter_PreservesPreviouslyObservedStarfighter()
        {
            Starfighter first = CreateStarfighter("FIRST", _empire);
            Starfighter second = CreateStarfighter("SECOND", _empire);
            _game.AttachNode(first, _coruscant);
            _game.AttachNode(second, _coruscant);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 41,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { first },
                    },
                }
            );
            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { second },
                    },
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            CollectionAssert.AreEquivalent(
                new[] { "FIRST", "SECOND" },
                snapshot.Starfighters.Select(starfighter => starfighter.InstanceID)
            );
        }

        [Test]
        public void HandleResults_SelectedNestedRegimentWithCapacity_PreservesRememberedRegiment()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "CARRIER");
            ship.RegimentCapacity = 2;
            Regiment first = CreateRegiment("FIRST", _empire);
            Regiment second = CreateRegiment("SECOND", _empire);
            _game.AttachNode(first, ship);
            _game.AttachNode(second, ship);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 41,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { first },
                    },
                }
            );
            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { second },
                    },
                }
            );

            CapitalShip knownShip = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single()
                .GetChildren<CapitalShip>()
                .Single();
            CollectionAssert.AreEquivalent(
                new[] { "FIRST", "SECOND" },
                knownShip.GetChildren<Regiment>().Select(regiment => regiment.InstanceID)
            );
        }

        [Test]
        public void HandleResults_SelectedNestedStarfighterWithCapacity_PreservesRememberedStarfighter()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "CARRIER");
            ship.StarfighterCapacity = 2;
            Starfighter first = CreateStarfighter("FIRST", _empire);
            Starfighter second = CreateStarfighter("SECOND", _empire);
            _game.AttachNode(first, ship);
            _game.AttachNode(second, ship);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 41,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { first },
                    },
                }
            );
            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { second },
                    },
                }
            );

            CapitalShip knownShip = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single()
                .GetChildren<CapitalShip>()
                .Single();
            CollectionAssert.AreEquivalent(
                new[] { "FIRST", "SECOND" },
                knownShip.GetChildren<Starfighter>().Select(starfighter => starfighter.InstanceID)
            );
        }

        [Test]
        public void HandleResults_SelectedNestedRegiment_ReplacesOneAbsentRememberedRegiment()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "CARRIER");
            ship.RegimentCapacity = 2;
            Regiment firstStale = CreateRegiment("FIRST_STALE", _empire);
            Regiment secondStale = CreateRegiment("SECOND_STALE", _empire);
            _game.AttachNode(firstStale, ship);
            _game.AttachNode(secondStale, ship);
            new FogOfWarRecorder().RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 41);
            _game.DetachNode(firstStale);
            Regiment current = CreateRegiment("CURRENT", _empire);
            _game.AttachNode(current, ship);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { current },
                    },
                }
            );

            CapitalShip knownShip = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single()
                .GetChildren<CapitalShip>()
                .Single();
            CollectionAssert.AreEquivalent(
                new[] { "SECOND_STALE", "CURRENT" },
                knownShip.GetChildren<Regiment>().Select(regiment => regiment.InstanceID)
            );
        }

        [Test]
        public void HandleResults_SelectedNestedStarfighters_ReplacesOnlyAbsentRememberedStarfighters()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "CARRIER");
            ship.StarfighterCapacity = 3;
            Starfighter firstStale = CreateStarfighter("FIRST_STALE", _empire);
            Starfighter secondStale = CreateStarfighter("SECOND_STALE", _empire);
            Starfighter retained = CreateStarfighter("RETAINED", _empire);
            _game.AttachNode(firstStale, ship);
            _game.AttachNode(secondStale, ship);
            _game.AttachNode(retained, ship);
            new FogOfWarRecorder().RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 41);
            _game.DetachNode(firstStale);
            _game.DetachNode(secondStale);
            Starfighter firstCurrent = CreateStarfighter("FIRST_CURRENT", _empire);
            Starfighter secondCurrent = CreateStarfighter("SECOND_CURRENT", _empire);
            _game.AttachNode(firstCurrent, ship);
            _game.AttachNode(secondCurrent, ship);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { firstCurrent, secondCurrent },
                    },
                }
            );

            CapitalShip knownShip = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single()
                .GetChildren<CapitalShip>()
                .Single();
            CollectionAssert.AreEquivalent(
                new[] { "RETAINED", "FIRST_CURRENT", "SECOND_CURRENT" },
                knownShip.GetChildren<Starfighter>().Select(starfighter => starfighter.InstanceID)
            );
        }

        [Test]
        public void HandleResults_SelectedManufacturingOrder_RevealsOnlySelectedOrder()
        {
            Building selected = AddQueuedBuilding(_coruscant, _empire, "SELECTED_ORDER", 25);
            AddQueuedBuilding(_coruscant, _empire, "HIDDEN_ORDER", 10);

            _observer.HandleResults(
                new List<IntelligenceRevealedResult>
                {
                    new IntelligenceRevealedResult
                    {
                        Tick = 42,
                        Recipient = _alliance,
                        Observations = new List<ISceneNode> { selected },
                    },
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsTrue(snapshot.HasManufacturingIntelligence);
            Assert.AreEqual("SELECTED_ORDER", snapshot.ManufacturingQueueItems.Single().InstanceID);
            Assert.IsFalse(
                snapshot.ManufacturingQueueItems.Any(item => item.InstanceID == "HIDDEN_ORDER")
            );
        }

        [Test]
        public void ObserveOwnershipChange_PreviousOwnerLosesVisibility_PreservesPlanetAndBuildingsWithoutOpposingUnits()
        {
            _coruscant.EnergyCapacity = 4;
            _coruscant.AllocatedEnergy = 1;
            _coruscant.NumRawResourceNodes = 3;
            Building facility = CreateBuilding("KNOWN_FACILITY", _empire);
            _game.AttachNode(facility, _coruscant);

            Fleet fleet = CreateFleet("OPPOSING_FLEET", _alliance);
            _game.AttachNode(fleet, _coruscant);
            AddCapitalShip(fleet, _alliance, "OPPOSING_SHIP");
            Mission mission = CreateMission("OPPOSING_MISSION", _alliance, _coruscant);
            _game.AttachNode(mission, _coruscant);
            new FogOfWarRecorder().RecordEspionageSnapshot(_empire, _coruscant, _coreSector, 10);
            _coruscant.SetOwnerInstanceID(null);

            _observer.ObserveOwnershipChange(
                new PlanetOwnershipChangedResult
                {
                    Planet = _coruscant,
                    PreviousOwner = _empire,
                    NewOwner = null,
                    Tick = 42,
                }
            );

            PlanetSnapshot snapshot = _empire.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            Assert.AreEqual(42, snapshot.TickCaptured);
            Assert.IsNull(snapshot.OwnerInstanceID);
            Assert.AreEqual(4, snapshot.EnergyCapacity);
            Assert.AreEqual(1, snapshot.AllocatedEnergy);
            Assert.AreEqual(3, snapshot.NumRawResourceNodes);
            Assert.AreEqual(facility.InstanceID, snapshot.Buildings.Single().InstanceID);
            Assert.IsEmpty(snapshot.Officers);
            Assert.IsEmpty(snapshot.Fleets);
            Assert.IsEmpty(snapshot.Regiments);
            Assert.IsEmpty(snapshot.SpecialForces);
            Assert.IsEmpty(snapshot.Starfighters);
            PlanetIntelligenceCategory defenseCategories =
                PlanetIntelligenceCategory.CapitalShips
                | PlanetIntelligenceCategory.Starfighters
                | PlanetIntelligenceCategory.GroundForces
                | PlanetIntelligenceCategory.Officers;
            Assert.AreEqual(
                PlanetIntelligenceCategory.None,
                snapshot.RevealedCategories & defenseCategories
            );
            Assert.IsTrue(snapshot.RevealedCategories.HasFlag(PlanetIntelligenceCategory.Missions));
            Assert.AreEqual(mission.InstanceID, snapshot.Missions.Single().InstanceID);

            Planet viewPlanet = new FogOfWarQueries(_game)
                .BuildFactionView(_empire)
                .GetChildren<PlanetSector>()
                .Single(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .Single(planet => planet.InstanceID == _coruscant.InstanceID);
            Assert.AreEqual(4, viewPlanet.EnergyCapacity);
            Assert.AreEqual(1, viewPlanet.AllocatedEnergy);
            Assert.AreEqual(3, viewPlanet.NumRawResourceNodes);
            Assert.AreEqual(
                facility.InstanceID,
                viewPlanet.GetChildren<Building>().Single().InstanceID
            );
            Assert.IsEmpty(viewPlanet.GetChildren<Fleet>());
            Assert.AreEqual(
                mission.InstanceID,
                viewPlanet.GetChildren<Mission>().Single().InstanceID
            );
        }

        [Test]
        public void RefreshVisibleKnowledge_NeutralPlanetWithFormerOwnerFleet_PreservesRetainedBuildingsAfterFleetDeparts()
        {
            _coruscant.EnergyCapacity = 1;
            Building facility = CreateBuilding("KNOWN_FACILITY", _empire);
            _game.AttachNode(facility, _coruscant);
            Fleet fleet = CreateFleet("FORMER_OWNER_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            AddCapitalShip(fleet, _empire, "FORMER_OWNER_SHIP");
            _coruscant.SetOwnerInstanceID(null);
            _observer.ObserveOwnershipChange(
                new PlanetOwnershipChangedResult
                {
                    Planet = _coruscant,
                    PreviousOwner = _empire,
                    NewOwner = null,
                    Tick = 42,
                }
            );

            _observer.RefreshVisibleKnowledge();
            _game.DetachNode(fleet);

            Planet viewPlanet = new FogOfWarQueries(_game)
                .BuildFactionView(_empire)
                .GetChildren<PlanetSector>()
                .Single(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .Single(planet => planet.InstanceID == _coruscant.InstanceID);
            Assert.AreEqual(
                facility.InstanceID,
                viewPlanet.GetChildren<Building>().Single().InstanceID
            );
        }

        [Test]
        public void ObserveOwnershipChange_OpposingCarrier_ForgetsFleetAndCargo()
        {
            Fleet fleet = CreateFleet("ATTACKERS", _alliance);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _alliance, "TRANSPORT");
            ship.RegimentCapacity = 3;
            Regiment first = CreateRegiment("FIRST", _alliance);
            Regiment retained = CreateRegiment("RETAINED", _alliance);
            Regiment second = CreateRegiment("SECOND", _alliance);
            _game.AttachNode(first, ship);
            _game.AttachNode(retained, ship);
            _game.AttachNode(second, ship);
            _game.ChangeOwnership(_coruscant, _alliance.InstanceID);
            PlanetOwnershipChangedResult ownershipChange = new PlanetOwnershipChangedResult
            {
                Planet = _coruscant,
                PreviousOwner = _empire,
                NewOwner = _alliance,
                Tick = 42,
            };

            _observer.ObserveOwnershipChange(ownershipChange);
            _game.MoveNode(first, _coruscant);
            _game.MoveNode(second, _coruscant);
            _observer.ProcessResults(new GameResult[] { ownershipChange });

            PlanetSnapshot snapshot = _empire.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            Assert.IsEmpty(snapshot.Regiments);
            Assert.IsEmpty(snapshot.Fleets);
        }

        [Test]
        public void ProcessResults_ShipDestroyedInCombat_RemovesShipForBothParticipants()
        {
            Fleet fleet = CreateFleet("FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "SHIP");
            new FogOfWarRecorder().RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);
            CombatUnitSnapshot destroyedShip = new CombatUnitSnapshot(ship) { Destroyed = true };
            _game.DeleteNode(ship);

            _observer.ProcessResults(
                new GameResult[]
                {
                    new SpaceCombatResult
                    {
                        AttackerOwnerInstanceID = _alliance.InstanceID,
                        DefenderOwnerInstanceID = _empire.InstanceID,
                        Planet = _coruscant,
                        DefendingUnits = new List<CombatUnitSnapshot> { destroyedShip },
                        Tick = 20,
                    },
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            Assert.IsFalse(
                snapshot.Fleets.Any(snapshotFleet =>
                    snapshotFleet
                        .GetChildren<CapitalShip>()
                        .Any(candidate => candidate.InstanceID == ship.InstanceID)
                )
            );
            Assert.IsFalse(_alliance.Fog.EntityLastSeenAt.ContainsKey(ship.InstanceID));
        }

        /// <summary>
        /// Creates a mission context for an Alliance participant targeting an entity at Coruscant.
        /// </summary>
        /// <param name="target">The selected mission target.</param>
        /// <returns>The configured mission context.</returns>
        private MissionContext CreateTargetingMissionContext(ISceneNode target)
        {
            Officer participant = CreateOfficer("PARTICIPANT", _alliance);
            _game.AttachNode(participant, _hoth);
            return new MissionContext
            {
                Game = _game,
                OwnerInstanceId = _alliance.InstanceID,
                Location = _coruscant,
                SelectedTarget = target,
                MainParticipants = new List<IMissionParticipant> { participant },
                DecoyParticipants = new List<IMissionParticipant>(),
            };
        }

        /// <summary>
        /// Creates an operational sabotage target that is stationed directly on a planet.
        /// </summary>
        /// <param name="targetKind">The kind of sabotage target to create.</param>
        /// <returns>The created target.</returns>
        private ISceneNode CreatePlanetSabotageTarget(SabotageTargetKind targetKind)
        {
            return targetKind switch
            {
                SabotageTargetKind.Building => CreateBuilding("TARGET", _empire),
                SabotageTargetKind.Regiment => CreateRegiment("TARGET", _empire),
                SabotageTargetKind.SpecialForces => new SpecialForces
                {
                    InstanceID = "TARGET",
                    OwnerInstanceID = _empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                SabotageTargetKind.Starfighter => CreateStarfighter("TARGET", _empire),
                _ => throw new System.ArgumentOutOfRangeException(nameof(targetKind)),
            };
        }

        /// <summary>
        /// Creates an operational sabotage target aboard an enemy fleet.
        /// </summary>
        /// <param name="targetKind">The kind of sabotage target to create.</param>
        /// <returns>The created target.</returns>
        private ISceneNode CreateFleetSabotageTarget(SabotageTargetKind targetKind)
        {
            Fleet fleet = CreateFleet("FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "SHIP");
            if (targetKind == SabotageTargetKind.CapitalShip)
                return ship;

            ISceneNode target;
            if (targetKind == SabotageTargetKind.Regiment)
            {
                ship.RegimentCapacity = 1;
                target = CreateRegiment("TARGET", _empire);
            }
            else if (targetKind == SabotageTargetKind.Starfighter)
            {
                ship.StarfighterCapacity = 1;
                target = CreateStarfighter("TARGET", _empire);
            }
            else
            {
                throw new System.ArgumentOutOfRangeException(nameof(targetKind));
            }

            _game.AttachNode(target, ship);
            return target;
        }

        /// <summary>
        /// Records complete intelligence at Coruscant, then removes the target from that planet.
        /// </summary>
        /// <param name="target">The target whose stale observation is being arranged.</param>
        private void RecordTargetThenRemoveFromPlanet(ISceneNode target)
        {
            new FogOfWarRecorder().RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            _game.DetachNode(target);
        }

        /// <summary>
        /// Processes a target-unavailable completion for the supplied mission.
        /// </summary>
        /// <param name="mission">The mission whose target was unavailable.</param>
        private void ProcessUnavailableTarget(Mission mission)
        {
            Assert.IsNotNull(mission);
            _observer.ProcessResults(
                new GameResult[]
                {
                    new MissionCompletedResult
                    {
                        Mission = mission,
                        Location = _coruscant,
                        Outcome = MissionOutcome.Failed,
                        CompletionReason = MissionCompletionReason.TargetUnavailable,
                    },
                }
            );
        }

        /// <summary>
        /// Asserts that an entity was removed from both the location index and Coruscant snapshot.
        /// </summary>
        /// <param name="targetInstanceId">The target entity instance ID.</param>
        private void AssertTargetForgotten(string targetInstanceId)
        {
            Assert.IsFalse(_alliance.Fog.EntityLastSeenAt.ContainsKey(targetInstanceId));
            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            IEnumerable<ISceneNode> entities = snapshot
                .Buildings.Cast<ISceneNode>()
                .Concat(snapshot.Officers)
                .Concat(snapshot.Regiments)
                .Concat(snapshot.SpecialForces)
                .Concat(snapshot.Starfighters)
                .Concat(
                    snapshot.Fleets.SelectMany(fleet =>
                        fleet.GetChildren<ISceneNode>(recursive: true).Prepend(fleet)
                    )
                );
            Assert.IsFalse(entities.Any(entity => entity.GetInstanceID() == targetInstanceId));
        }
    }
}
