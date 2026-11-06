using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tests.Helpers;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Game.FogOfWar
{
    [TestFixture]
    public class FogOfWarRecorderTests : FogOfWarTestBase
    {
        [TestCase("officer", false)]
        [TestCase("regiment", false)]
        [TestCase("special forces", false)]
        [TestCase("starfighter", false)]
        [TestCase("officer", true)]
        [TestCase("regiment", true)]
        [TestCase("special forces", true)]
        [TestCase("starfighter", true)]
        public void RecordSelectedObservations_EmbarkingUnit_KeepsOneKnownLocation(
            string unitKind,
            bool changesPlanet
        )
        {
            ISceneNode unit = CreateUnit(unitKind, "UNIT");
            _game.AttachNode(unit, _coruscant);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordSelectedObservations(_game, _alliance, new[] { unit }, 10);
            Planet destination = changesPlanet ? _tatooine : _coruscant;
            Fleet fleet = CreateFleet("FLEET", _empire);
            _game.AttachNode(fleet, destination);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "SHIP");
            ship.RegimentCapacity = ship.StarfighterCapacity = 1;
            _game.MoveNode(unit, ship);

            recorder.RecordSelectedObservations(_game, _alliance, new[] { fleet }, 20);

            ISceneNode[] known = _alliance
                .Fog.Snapshots.Values.SelectMany(sector => sector.Planets.Values)
                .SelectMany(snapshot => FindSnapshotEntities(snapshot, unit.InstanceID))
                .ToArray();
            Assert.AreEqual(1, known.Length);
            Assert.AreEqual(
                ship.InstanceID,
                known.Single().GetParentOfType<CapitalShip>().InstanceID
            );
            Assert.AreEqual(
                destination.InstanceID,
                _alliance.Fog.EntityLastSeenAt[unit.InstanceID]
            );
        }

        [TestCase("officer")]
        [TestCase("regiment")]
        [TestCase("special forces")]
        [TestCase("starfighter")]
        public void RecordSelectedObservations_DisembarkingUnit_KeepsOneKnownLocation(
            string unitKind
        )
        {
            Fleet fleet = CreateFleet("FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "SHIP");
            ship.RegimentCapacity = ship.StarfighterCapacity = 1;
            ISceneNode unit = CreateUnit(unitKind, "UNIT");
            _game.AttachNode(unit, ship);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordSelectedObservations(_game, _alliance, new[] { fleet }, 10);
            _game.MoveNode(unit, _coruscant);

            recorder.RecordSelectedObservations(_game, _alliance, new[] { unit }, 20);

            ISceneNode[] known = FindSnapshotEntities(
                GetSnapshot(_coreSector, _coruscant),
                unit.InstanceID
            );
            Assert.AreEqual(1, known.Length);
            Assert.IsNull(known.Single().GetParentOfType<CapitalShip>());
            Assert.AreEqual(_coruscant.InstanceID, _alliance.Fog.EntityLastSeenAt[unit.InstanceID]);
        }

        [TestCase("officer")]
        [TestCase("special forces")]
        public void RecordSelectedObservations_LeavingMission_KeepsOneKnownLocation(string unitKind)
        {
            Mission mission = CreateMission("MISSION", _empire, _coruscant);
            _game.AttachNode(mission, _coruscant);
            ISceneNode unit = CreateUnit(unitKind, "UNIT");
            _game.AttachNode(unit, mission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordSelectedObservations(_game, _alliance, new[] { mission }, 10);
            _game.MoveNode(unit, _coruscant);

            recorder.RecordSelectedObservations(_game, _alliance, new[] { unit }, 20);

            ISceneNode[] known = FindSnapshotEntities(
                GetSnapshot(_coreSector, _coruscant),
                unit.InstanceID
            );
            Assert.AreEqual(1, known.Length);
            Assert.IsNull(known.Single().GetParentOfType<Mission>());
            Assert.AreEqual(_coruscant.InstanceID, _alliance.Fog.EntityLastSeenAt[unit.InstanceID]);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecordSelectedObservations_AbsentContainerParticipant_RemovesObsoleteLocation(
            bool isMission
        )
        {
            ContainerNode container;
            if (isMission)
            {
                Mission mission = CreateMission("CONTAINER", _empire, _coruscant);
                _game.AttachNode(mission, _coruscant);
                container = mission;
            }
            else
            {
                Fleet fleet = CreateFleet("CONTAINER", _empire);
                _game.AttachNode(fleet, _coruscant);
                container = AddCapitalShip(fleet, _empire, "SHIP");
            }
            Officer officer = CreateOfficer("OFFICER", _empire);
            _game.AttachNode(officer, container);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            _game.MoveNode(officer, _coruscant);
            ISceneNode observation = isMission ? container : container.GetParentOfType<Fleet>();

            recorder.RecordSelectedObservations(_game, _alliance, new[] { observation }, 20);

            Assert.IsEmpty(
                FindSnapshotEntities(GetSnapshot(_coreSector, _coruscant), officer.InstanceID)
            );
            Assert.IsFalse(_alliance.Fog.EntityLastSeenAt.ContainsKey(officer.InstanceID));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecordSelectedObservations_NoValidObservations_PreservesLocationIndex(
            bool detachedEntity
        )
        {
            _alliance.Fog.EntityLastSeenAt["REMEMBERED"] = "UNKNOWN_PLANET";
            ISceneNode[] observations = detachedEntity
                ? new ISceneNode[] { CreateOfficer("DETACHED", _empire) }
                : System.Array.Empty<ISceneNode>();

            new FogOfWarRecorder().RecordSelectedObservations(_game, _alliance, observations, 20);

            Assert.AreEqual("UNKNOWN_PLANET", _alliance.Fog.EntityLastSeenAt["REMEMBERED"]);
        }

        [TestCase(PlanetIntelligenceCategory.CapitalShips)]
        [TestCase(PlanetIntelligenceCategory.PlanetMilitaryProduction)]
        [TestCase(PlanetIntelligenceCategory.Starfighters)]
        [TestCase(PlanetIntelligenceCategory.Regiments)]
        public void RecordIntelligenceSnapshot_FleetChangesPlanet_PreservesUnreportedPersonnelAndLocations(
            PlanetIntelligenceCategory category
        )
        {
            Fleet fleet = CreateFleet("FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "SHIP");
            ship.RegimentCapacity = ship.StarfighterCapacity = 1;
            ISceneNode officer = CreateUnit("officer", "KNOWN_OFFICER");
            ISceneNode agent = CreateUnit("special forces", "KNOWN_AGENT");
            _game.AttachNode(officer, ship);
            _game.AttachNode(agent, ship);
            _game.AttachNode(CreateUnit("regiment", "REGIMENT"), ship);
            _game.AttachNode(CreateUnit("starfighter", "FIGHTER"), ship);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            _game.MoveNode(fleet, _tatooine);
            _game.MoveNode(officer, _coruscant);
            _game.MoveNode(agent, _coruscant);
            _game.AttachNode(CreateOfficer("UNKNOWN_OFFICER", _empire), ship);

            recorder.RecordIntelligenceSnapshot(_alliance, _tatooine, _outerRim, 20, category);

            PlanetSnapshot destination = GetSnapshot(_outerRim, _tatooine);
            foreach (string id in new[] { "KNOWN_OFFICER", "KNOWN_AGENT" })
            {
                Assert.AreEqual(1, FindSnapshotEntities(destination, id).Length);
                Assert.IsEmpty(FindSnapshotEntities(GetSnapshot(_coreSector, _coruscant), id));
                Assert.AreEqual(_tatooine.InstanceID, _alliance.Fog.EntityLastSeenAt[id]);
            }
            Assert.IsEmpty(FindSnapshotEntities(destination, "UNKNOWN_OFFICER"));
        }

        [Test]
        public void RecordIntelligenceSnapshot_SavedCompositeState_PreservesIndependentReportsAndLocations()
        {
            Fleet fleet = CreateFleet("FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "SHIP");
            _game.AttachNode(CreateOfficer("OFFICER", _empire), ship);
            _game.AttachNode(CreateUnit("special forces", "AGENT"), ship);
            Mission mission = CreateMission("MISSION", _empire, _coruscant);
            _game.AttachNode(mission, _coruscant);
            _game.AttachNode(CreateOfficer("PARTICIPANT", _empire), mission);
            AddQueuedBuilding(_coruscant, _empire, "ORDER", 25);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            _game.MoveNode(fleet, _tatooine);
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _tatooine,
                _outerRim,
                20,
                PlanetIntelligenceCategory.CapitalShips
            );
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                30,
                PlanetIntelligenceCategory.PlanetResources
            );

            FogState restored = SerializationHelper.Deserialize<FogState>(
                SerializationHelper.Serialize(_alliance.Fog)
            );
            _alliance.Fog.Snapshots = restored.Snapshots;
            _alliance.Fog.EntityLastSeenAt = restored.EntityLastSeenAt;
            _alliance.Fog.PlanetToSector = restored.PlanetToSector;
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                40,
                PlanetIntelligenceCategory.ProductionFacilities
            );

            PlanetSnapshot origin = GetSnapshot(_coreSector, _coruscant);
            PlanetSnapshot destination = GetSnapshot(_outerRim, _tatooine);
            Assert.AreEqual(10, origin.FullyObservedAtTick);
            Assert.AreEqual(10, origin.FleetsObservedAtTick);
            Assert.AreEqual(40, origin.TickCaptured);
            Assert.IsTrue(origin.HasIntelligence(PlanetIntelligenceCategory.Missions));
            Assert.AreEqual(
                "PARTICIPANT",
                origin.Missions.Single().GetChildren<Officer>().Single().InstanceID
            );
            AssertQueuedIntelligence(origin, "ORDER", 25);
            Assert.AreEqual(1, FindSnapshotEntities(destination, "OFFICER").Length);
            Assert.AreEqual(1, FindSnapshotEntities(destination, "AGENT").Length);
            Assert.AreEqual(_tatooine.InstanceID, _alliance.Fog.EntityLastSeenAt["OFFICER"]);
            Assert.AreEqual(_tatooine.InstanceID, _alliance.Fog.EntityLastSeenAt["AGENT"]);
        }

        /// <summary>
        /// Verifies ordered refreshes retain identical snapshots after movement and removal.
        /// </summary>
        /// <param name="reverseOrder">Whether to observe the previous location first.</param>
        [TestCase(PlanetIntelligenceCategory.Fleets | PlanetIntelligenceCategory.Starfighters, 20)]
        [TestCase(
            PlanetIntelligenceCategory.CapitalShips | PlanetIntelligenceCategory.Starfighters,
            20
        )]
        [TestCase(PlanetIntelligenceCategory.Fleets, 10)]
        [TestCase(PlanetIntelligenceCategory.CapitalShips, 10)]
        [TestCase(PlanetIntelligenceCategory.Starfighters, 10)]
        public void RecordIntelligenceSnapshot_OrbitalCategories_UpdatesCompleteFleetObservationTick(
            PlanetIntelligenceCategory categories,
            int expectedTick
        )
        {
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            recorder.RecordIntelligenceSnapshot(_alliance, _coruscant, _coreSector, 20, categories);

            Assert.AreEqual(
                expectedTick,
                GetSnapshot(_coreSector, _coruscant).FleetsObservedAtTick
            );
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecordPlanetSnapshots_ChangedLocations_MatchesIndividualObservations(
            bool reverseOrder
        )
        {
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            Faction batchedFaction = new Faction { InstanceID = _alliance.InstanceID };
            var planets = new[] { _coruscant, _tatooine, _hoth };
            if (reverseOrder)
                planets = planets.Reverse().ToArray();
            Regiment movingUnit = CreateRegiment("MOVING", _empire);
            _game.AttachNode(movingUnit, _tatooine);
            Regiment removedUnit = CreateRegiment("REMOVED", _empire);
            _game.AttachNode(removedUnit, _coruscant);
            Fleet movingFleet = CreateFleet("MOVING_FLEET", _empire);
            _game.AttachNode(movingFleet, _tatooine);
            CapitalShip movingShip = AddCapitalShip(movingFleet, _empire, "MOVING_SHIP");
            _game.AttachNode(CreateOfficer("CARRIED_OFFICER", _empire), movingShip);
            foreach (var planet in planets)
            {
                var sector = planet.GetParentOfType<Rebellion.Game.Galaxy.PlanetSector>();
                recorder.RecordEspionageSnapshot(_alliance, planet, sector, 10);
                recorder.RecordEspionageSnapshot(batchedFaction, planet, sector, 10);
            }
            _game.MoveNode(movingUnit, _coruscant);
            _game.MoveNode(movingFleet, _coruscant);
            _game.DeleteNode(removedUnit);
            _alliance.Fog.EntityLastSeenAt["ABSENT"] = _coruscant.InstanceID;
            batchedFaction.Fog.EntityLastSeenAt["ABSENT"] = _coruscant.InstanceID;

            foreach (var planet in planets)
                recorder.RecordPlanetSnapshot(
                    _alliance,
                    planet,
                    planet.GetParentOfType<Rebellion.Game.Galaxy.PlanetSector>(),
                    11
                );
            recorder.RecordPlanetSnapshots(batchedFaction, planets, 11);

            GameSerializer serializer = new GameSerializer(typeof(FogState));
            using StringWriter expected = new StringWriter();
            using StringWriter actual = new StringWriter();
            serializer.Serialize(expected, _alliance.Fog);
            serializer.Serialize(actual, batchedFaction.Fog);
            Assert.AreEqual(expected.ToString(), actual.ToString());
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RecordPlanetSnapshots_NoValidObservations_PreservesLocationIndex(
            bool includesDetachedPlanet
        )
        {
            _alliance.Fog.EntityLastSeenAt["REMEMBERED"] = "UNKNOWN_PLANET";
            Planet[] planets = includesDetachedPlanet
                ? new[] { new Planet { InstanceID = "DETACHED" } }
                : new Planet[0];

            new FogOfWarRecorder().RecordPlanetSnapshots(_alliance, planets, 20);

            Assert.AreEqual("UNKNOWN_PLANET", _alliance.Fog.EntityLastSeenAt["REMEMBERED"]);
        }

        [TestCase(PlanetIntelligenceCategory.Officers, false)]
        [TestCase(PlanetIntelligenceCategory.Regiments, false)]
        [TestCase(PlanetIntelligenceCategory.SpecialForces, false)]
        [TestCase(PlanetIntelligenceCategory.Starfighters, false)]
        [TestCase(PlanetIntelligenceCategory.Officers, true)]
        [TestCase(PlanetIntelligenceCategory.Regiments, true)]
        [TestCase(PlanetIntelligenceCategory.SpecialForces, true)]
        [TestCase(PlanetIntelligenceCategory.Starfighters, true)]
        public void RecordIntelligenceSnapshot_CarrierChangesFleet_KeepsOneCarrierPlacement(
            PlanetIntelligenceCategory category,
            bool changesPlanet
        )
        {
            ObserveCarrierAfterFleetChange(category, changesPlanet);

            string[] carrierFleets = _alliance
                .Fog.Snapshots.Values.SelectMany(sector => sector.Planets.Values)
                .SelectMany(snapshot => FindSnapshotEntities(snapshot, "CARRIER"))
                .Select(ship => ship.GetParent().InstanceID)
                .ToArray();

            CollectionAssert.AreEqual(new[] { "FLEET_B" }, carrierFleets);
        }

        [TestCase(PlanetIntelligenceCategory.Officers, false)]
        [TestCase(PlanetIntelligenceCategory.Regiments, false)]
        [TestCase(PlanetIntelligenceCategory.SpecialForces, false)]
        [TestCase(PlanetIntelligenceCategory.Starfighters, false)]
        [TestCase(PlanetIntelligenceCategory.Officers, true)]
        [TestCase(PlanetIntelligenceCategory.Regiments, true)]
        [TestCase(PlanetIntelligenceCategory.SpecialForces, true)]
        [TestCase(PlanetIntelligenceCategory.Starfighters, true)]
        public void RecordIntelligenceSnapshot_CarrierChangesFleet_PreservesUnreportedCargo(
            PlanetIntelligenceCategory category,
            bool changesPlanet
        )
        {
            (PlanetSnapshot snapshot, string[] rememberedIDs) = ObserveCarrierAfterFleetChange(
                category,
                changesPlanet
            );
            CapitalShip rememberedShip = snapshot
                .Fleets.SelectMany(fleet => fleet.GetChildren<CapitalShip>())
                .Single(ship => ship.InstanceID == "CARRIER");

            CollectionAssert.AreEquivalent(
                rememberedIDs,
                rememberedShip
                    .GetChildren<ISceneNode>()
                    .Where(unit => unit.InstanceID != "REPORTED")
                    .Select(unit => unit.InstanceID)
            );
        }

        [TestCase(PlanetIntelligenceCategory.Officers, false)]
        [TestCase(PlanetIntelligenceCategory.Regiments, false)]
        [TestCase(PlanetIntelligenceCategory.SpecialForces, false)]
        [TestCase(PlanetIntelligenceCategory.Starfighters, false)]
        [TestCase(PlanetIntelligenceCategory.Officers, true)]
        [TestCase(PlanetIntelligenceCategory.Regiments, true)]
        [TestCase(PlanetIntelligenceCategory.SpecialForces, true)]
        [TestCase(PlanetIntelligenceCategory.Starfighters, true)]
        public void RecordIntelligenceSnapshot_CarrierChangesFleet_ReplacesReportedCargo(
            PlanetIntelligenceCategory category,
            bool changesPlanet
        )
        {
            (PlanetSnapshot snapshot, _) = ObserveCarrierAfterFleetChange(category, changesPlanet);

            CollectionAssert.AreEqual(
                new[] { "REPORTED" },
                FindSnapshotEntities(snapshot, "REPLACED")
                    .Concat(FindSnapshotEntities(snapshot, "REPORTED"))
                    .Select(unit => unit.InstanceID)
            );
        }

        [Test]
        public void RecordSelectedObservations_LaterUnrelatedObservation_PreservesMovementObservationTime()
        {
            Officer officer = CreateOfficer("traveller", _empire);
            officer.Movement = new MovementState { TransitTicks = 20, TicksElapsed = 10 };
            _game.AttachNode(officer, _coruscant);
            Officer another = CreateOfficer("another", _empire);
            _game.AttachNode(another, _coruscant);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordSelectedObservations(_game, _alliance, new[] { officer }, 100);

            recorder.RecordSelectedObservations(_game, _alliance, new[] { another }, 110);

            MovementState observed = _alliance
                .Fog.Snapshots[_coreSector.InstanceID]
                .Planets[_coruscant.InstanceID]
                .Officers.Single(item => item.InstanceID == officer.InstanceID)
                .Movement;
            Assert.AreEqual(100, observed.ObservedAtTick);
            Assert.IsTrue(observed.IsObservation);
            Assert.IsNull(officer.Movement.ObservedAtTick);
            Assert.IsFalse(officer.Movement.IsObservation);
        }

        [Test]
        public void RecordIntelligenceSnapshot_CapitalShips_DoesNotLeakShipCargo()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            ship.StarfighterCapacity = 1;
            _game.AttachNode(CreateOfficer("VADER", _empire), ship);
            _game.AttachNode(
                new Starfighter
                {
                    InstanceID = "TIE_SQUADRON",
                    OwnerInstanceID = _empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                ship
            );

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.CapitalShips
            );

            CapitalShip knownShip = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single()
                .GetChildren<CapitalShip>()
                .Single();
            Assert.AreEqual("STAR_DESTROYER", knownShip.InstanceID);
            Assert.IsEmpty(knownShip.GetChildren<Officer>());
            Assert.IsEmpty(knownShip.GetChildren<Starfighter>());

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                43,
                PlanetIntelligenceCategory.Starfighters
            );
            PlanetSnapshot updatedSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets[
                "CORUSCANT"
            ];
            Assert.IsEmpty(updatedSnapshot.Starfighters);
            Assert.AreEqual(
                "TIE_SQUADRON",
                updatedSnapshot
                    .Fleets.Single()
                    .GetChildren<CapitalShip>()
                    .Single()
                    .GetChildren<Starfighter>()
                    .Single()
                    .InstanceID
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_EnemyFleet_DoesNotRetainWaypoints()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            fleet.Waypoints.Add(_tatooine.InstanceID);
            _game.AttachNode(fleet, _coruscant);
            AddCapitalShip(fleet, _empire, "STAR_DESTROYER");

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.CapitalShips
            );

            Fleet knownFleet = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single();
            Assert.IsEmpty(knownFleet.Waypoints);
        }

        [Test]
        public void RecordIntelligenceSnapshot_Fleets_RevealsFleetCargo()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            ship.StarfighterCapacity = 1;
            _game.AttachNode(CreateOfficer("VADER", _empire), ship);
            _game.AttachNode(CreateStarfighter("TIE_SQUADRON", _empire), ship);

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.Fleets
            );

            CapitalShip knownShip = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single()
                .GetChildren<CapitalShip>()
                .Single();
            Assert.AreEqual("VADER", knownShip.GetChildren<Officer>().Single().InstanceID);
            Assert.AreEqual(
                "TIE_SQUADRON",
                knownShip.GetChildren<Starfighter>().Single().InstanceID
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_AllWithoutDescendants_PreservesFleetHierarchy()
        {
            _game.AttachNode(CreateOfficer("STATIONED_OFFICER", _empire), _coruscant);
            _game.AttachNode(CreateRegiment("STATIONED_REGIMENT", _empire), _coruscant);
            _game.AttachNode(CreateStarfighter("STATIONED_FIGHTER", _empire), _coruscant);
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            ship.RegimentCapacity = 1;
            ship.StarfighterCapacity = 1;
            _game.AttachNode(CreateOfficer("EMBARKED_OFFICER", _empire), ship);
            _game.AttachNode(CreateRegiment("EMBARKED_REGIMENT", _empire), ship);
            _game.AttachNode(CreateStarfighter("EMBARKED_FIGHTER", _empire), ship);

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.All,
                includeDescendants: false
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            CollectionAssert.AreEqual(
                new[] { "STATIONED_OFFICER" },
                snapshot.Officers.Select(officer => officer.InstanceID)
            );
            CollectionAssert.AreEqual(
                new[] { "STATIONED_REGIMENT" },
                snapshot.Regiments.Select(regiment => regiment.InstanceID)
            );
            CollectionAssert.AreEqual(
                new[] { "STATIONED_FIGHTER" },
                snapshot.Starfighters.Select(starfighter => starfighter.InstanceID)
            );
            CapitalShip knownShip = snapshot.Fleets.Single().GetChildren<CapitalShip>().Single();
            Assert.AreEqual(
                "EMBARKED_OFFICER",
                knownShip.GetChildren<Officer>().Single().InstanceID
            );
            Assert.AreEqual(
                "EMBARKED_REGIMENT",
                knownShip.GetChildren<Regiment>().Single().InstanceID
            );
            Assert.AreEqual(
                "EMBARKED_FIGHTER",
                knownShip.GetChildren<Starfighter>().Single().InstanceID
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_CapitalShipsWithCombatCargo_ExcludesPersonnel()
        {
            Regiment stationedRegiment = CreateRegiment("STATIONED_REGIMENT", _empire);
            Starfighter stationedStarfighter = CreateStarfighter("STATIONED_FIGHTER", _empire);
            _game.AttachNode(stationedRegiment, _coruscant);
            _game.AttachNode(stationedStarfighter, _coruscant);
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            ship.RegimentCapacity = 1;
            ship.StarfighterCapacity = 1;
            _game.AttachNode(CreateRegiment("EMBARKED_REGIMENT", _empire), ship);
            _game.AttachNode(CreateStarfighter("EMBARKED_FIGHTER", _empire), ship);
            _game.AttachNode(CreateOfficer("VADER", _empire), ship);
            _game.AttachNode(
                new SpecialForces
                {
                    InstanceID = "IMPERIAL_AGENT",
                    OwnerInstanceID = _empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                ship
            );
            FogOfWarRecorder recorder = new FogOfWarRecorder();

            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.PlanetMilitaryProduction,
                includeDescendants: false
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            CapitalShip knownShip = snapshot.Fleets.Single().GetChildren<CapitalShip>().Single();
            Assert.AreEqual(
                "EMBARKED_REGIMENT",
                knownShip.GetChildren<Regiment>().Single().InstanceID
            );
            Assert.AreEqual(
                "EMBARKED_FIGHTER",
                knownShip.GetChildren<Starfighter>().Single().InstanceID
            );
            Assert.IsEmpty(knownShip.GetChildren<Officer>());
            Assert.IsEmpty(knownShip.GetChildren<SpecialForces>());
            CollectionAssert.AreEqual(
                new[] { "STATIONED_REGIMENT" },
                snapshot.Regiments.Select(regiment => regiment.InstanceID)
            );
            CollectionAssert.AreEqual(
                new[] { "STATIONED_FIGHTER" },
                snapshot.Starfighters.Select(starfighter => starfighter.InstanceID)
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_PartialFleetAfterFullReveal_PreservesUnrefreshedCargo()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            ship.RegimentCapacity = 2;
            ship.StarfighterCapacity = 2;
            _game.AttachNode(CreateOfficer("KNOWN_OFFICER", _empire), ship);
            _game.AttachNode(CreateRegiment("KNOWN_REGIMENT", _empire), ship);
            _game.AttachNode(CreateStarfighter("KNOWN_FIGHTER", _empire), ship);
            _game.AttachNode(
                new SpecialForces
                {
                    InstanceID = "KNOWN_AGENT",
                    OwnerInstanceID = _empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                ship
            );
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                41,
                PlanetIntelligenceCategory.All,
                includeDescendants: false
            );
            _game.AttachNode(CreateOfficer("NEW_OFFICER", _empire), ship);
            _game.AttachNode(CreateRegiment("NEW_REGIMENT", _empire), ship);
            _game.AttachNode(CreateStarfighter("NEW_FIGHTER", _empire), ship);
            _game.AttachNode(
                new SpecialForces
                {
                    InstanceID = "NEW_AGENT",
                    OwnerInstanceID = _empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                ship
            );

            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.PlanetMilitaryProduction,
                includeDescendants: false
            );

            CapitalShip knownShip = _alliance
                .Fog.Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .Fleets.Single()
                .GetChildren<CapitalShip>()
                .Single();
            CollectionAssert.AreEqual(
                new[] { "KNOWN_OFFICER" },
                knownShip.GetChildren<Officer>().Select(officer => officer.InstanceID)
            );
            CollectionAssert.AreEqual(
                new[] { "KNOWN_AGENT" },
                knownShip.GetChildren<SpecialForces>().Select(unit => unit.InstanceID)
            );
            CollectionAssert.AreEquivalent(
                new[] { "KNOWN_REGIMENT", "NEW_REGIMENT" },
                knownShip.GetChildren<Regiment>().Select(regiment => regiment.InstanceID)
            );
            CollectionAssert.AreEquivalent(
                new[] { "KNOWN_FIGHTER", "NEW_FIGHTER" },
                knownShip.GetChildren<Starfighter>().Select(starfighter => starfighter.InstanceID)
            );
        }

        [TestCase("officer", PlanetIntelligenceCategory.Officers)]
        [TestCase("regiment", PlanetIntelligenceCategory.Regiments)]
        [TestCase("special forces", PlanetIntelligenceCategory.SpecialForces)]
        [TestCase("starfighter", PlanetIntelligenceCategory.Starfighters)]
        public void RecordIntelligenceSnapshot_UnitMovesBetweenPlanetAndFleet_PreservesCanonicalPlacement(
            string unitKind,
            PlanetIntelligenceCategory category
        )
        {
            ISceneNode unit = CreateUnit(unitKind, "MOBILE_UNIT");
            _game.AttachNode(unit, _coruscant);
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "CARRIER");
            ship.RegimentCapacity = 1;
            ship.StarfighterCapacity = 1;
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.MoveNode(unit, ship);
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                category,
                includeDescendants: true
            );

            PlanetSnapshot embarked = GetSnapshot(_coreSector, _coruscant);
            ISceneNode[] embarkedCopies = FindSnapshotEntities(embarked, unit.InstanceID);
            Assert.AreEqual(1, embarkedCopies.Length);
            Assert.IsInstanceOf<CapitalShip>(embarkedCopies.Single().GetParent());

            _game.MoveNode(unit, _coruscant);
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                30,
                category,
                includeDescendants: true
            );

            PlanetSnapshot stationed = GetSnapshot(_coreSector, _coruscant);
            ISceneNode[] stationedCopies = FindSnapshotEntities(stationed, unit.InstanceID);
            Assert.AreEqual(1, stationedCopies.Length);
            Assert.IsNull(stationedCopies.Single().GetParent());
        }

        [Test]
        public void RecordIntelligenceSnapshot_RecursiveStarfighters_RecordsCanonicalFleetAncestry()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "CARRIER");
            ship.StarfighterCapacity = 1;
            Starfighter fighter = CreateStarfighter("EMBARKED_FIGHTER", _empire);
            _game.AttachNode(fighter, ship);

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                PlanetIntelligenceCategory.Starfighters,
                includeDescendants: true
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            ISceneNode[] copies = FindSnapshotEntities(snapshot, fighter.InstanceID);
            Assert.AreEqual(1, copies.Length);
            Assert.IsInstanceOf<CapitalShip>(copies.Single().GetParent());
            Assert.AreEqual("CARRIER", copies.Single().GetParent().InstanceID);
            Assert.IsEmpty(snapshot.Starfighters);
        }

        [Test]
        public void RecordIntelligenceSnapshot_RecursiveStarfighters_RemovesAbsentNestedUnit()
        {
            MakeTatooineImperial();
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "CARRIER");
            ship.StarfighterCapacity = 1;
            Starfighter fighter = CreateStarfighter("DEPARTED_FIGHTER", _empire);
            _game.AttachNode(fighter, ship);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.MoveNode(fighter, _tatooine);
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                PlanetIntelligenceCategory.Starfighters,
                includeDescendants: true
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.IsEmpty(FindSnapshotEntities(snapshot, fighter.InstanceID));
            Assert.IsFalse(_alliance.Fog.EntityLastSeenAt.ContainsKey(fighter.InstanceID));
        }

        [Test]
        public void RecordIntelligenceSnapshot_RecursiveStarfighters_PreservesDepartedCarrierCargo()
        {
            MakeTatooineImperial();
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "DEPARTED_CARRIER");
            ship.StarfighterCapacity = 1;
            Starfighter fighter = CreateStarfighter("REMEMBERED_FIGHTER", _empire);
            _game.AttachNode(fighter, ship);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.MoveNode(fleet, _tatooine);
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                PlanetIntelligenceCategory.Starfighters,
                includeDescendants: true
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            ISceneNode remembered = FindSnapshotEntities(snapshot, fighter.InstanceID).Single();
            Assert.IsInstanceOf<CapitalShip>(remembered.GetParent());
            Assert.AreEqual(ship.InstanceID, remembered.GetParent().InstanceID);
            Assert.AreEqual(
                _coruscant.InstanceID,
                _alliance.Fog.EntityLastSeenAt[fighter.InstanceID]
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_MissionParticipantReturnsToPlanet_PreservesMissionRemainder()
        {
            Officer returning = CreateOfficer("RETURNING", _empire);
            Officer remaining = CreateOfficer("REMAINING", _empire);
            Mission mission = CreateMission("MISSION", _empire, _coruscant);
            _game.AttachNode(mission, _coruscant);
            _game.AttachNode(returning, mission);
            _game.AttachNode(remaining, mission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.MoveNode(returning, _coruscant);
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                PlanetIntelligenceCategory.Officers,
                includeDescendants: true
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            CollectionAssert.AreEqual(
                new[] { returning.InstanceID },
                snapshot.Officers.Select(officer => officer.InstanceID)
            );
            Mission rememberedMission = snapshot.Missions.Single();
            CollectionAssert.AreEqual(
                new[] { remaining.InstanceID },
                rememberedMission
                    .GetMainParticipants(includeDisabled: true)
                    .Select(participant => participant.InstanceID)
            );
            Assert.AreEqual(1, FindSnapshotEntities(snapshot, returning.InstanceID).Length);
            Assert.AreEqual(1, FindSnapshotEntities(snapshot, remaining.InstanceID).Length);
        }

        [Test]
        public void RecordPlanetSnapshot_MissionParticipantReturnsToPlanet_PreservesMissionRemainder()
        {
            Officer returning = CreateOfficer("RETURNING", _empire);
            Officer remaining = CreateOfficer("REMAINING", _empire);
            Mission mission = CreateMission("MISSION", _empire, _coruscant);
            _game.AttachNode(mission, _coruscant);
            _game.AttachNode(returning, mission);
            _game.AttachNode(remaining, mission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.MoveNode(returning, _coruscant);
            recorder.RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            CollectionAssert.AreEqual(
                new[] { returning.InstanceID },
                snapshot.Officers.Select(officer => officer.InstanceID)
            );
            Mission rememberedMission = snapshot.Missions.Single();
            CollectionAssert.AreEqual(
                new[] { remaining.InstanceID },
                rememberedMission
                    .GetMainParticipants(includeDisabled: true)
                    .Select(participant => participant.InstanceID)
            );
            Assert.AreEqual(1, FindSnapshotEntities(snapshot, returning.InstanceID).Length);
            Assert.AreEqual(1, FindSnapshotEntities(snapshot, remaining.InstanceID).Length);
        }

        [Test]
        public void RecordPlanetSnapshot_PartialOfficerMissionIntelligence_PreservesOnlySelectedParticipant()
        {
            Officer officer = CreateOfficer("SELECTED_OFFICER", _empire);
            SpecialForces hiddenDecoy = new SpecialForces
            {
                InstanceID = "HIDDEN_DECOY",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Mission mission = CreateMission("PARTIAL_MISSION", _empire, _coruscant);
            _game.AttachNode(mission, _coruscant);
            _game.AttachNode(officer, mission);
            mission.AddDecoyParticipant(hiddenDecoy);
            _game.AttachNode(hiddenDecoy, mission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                10,
                PlanetIntelligenceCategory.Officers,
                includeDescendants: true
            );

            recorder.RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 20);

            Mission rememberedMission = GetSnapshot(_coreSector, _coruscant).Missions.Single();
            CollectionAssert.AreEqual(
                new[] { officer.InstanceID },
                rememberedMission
                    .GetMainParticipants(includeDisabled: true)
                    .Select(participant => participant.InstanceID)
            );
            Assert.IsEmpty(rememberedMission.GetDecoyParticipants(includeDisabled: true));
            Assert.IsEmpty(
                rememberedMission
                    .GetAllParticipants(includeDisabled: true)
                    .Where(participant => participant.InstanceID == hiddenDecoy.InstanceID)
            );
        }

        [Test]
        public void RecordPlanetLossSnapshot_PartialSpecialForcesMissionIntelligence_PreservesOnlySelectedDecoy()
        {
            Officer hiddenOfficer = CreateOfficer("HIDDEN_OFFICER", _empire);
            SpecialForces selectedDecoy = new SpecialForces
            {
                InstanceID = "SELECTED_DECOY",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Mission mission = CreateMission("PARTIAL_MISSION", _empire, _coruscant);
            _game.AttachNode(mission, _coruscant);
            _game.AttachNode(hiddenOfficer, mission);
            mission.AddDecoyParticipant(selectedDecoy);
            _game.AttachNode(selectedDecoy, mission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                10,
                PlanetIntelligenceCategory.SpecialForces,
                includeDescendants: true
            );

            recorder.RecordPlanetLossSnapshot(_alliance, _coruscant, _coreSector, 20);

            Mission rememberedMission = GetSnapshot(_coreSector, _coruscant).Missions.Single();
            Assert.IsEmpty(rememberedMission.GetMainParticipants(includeDisabled: true));
            CollectionAssert.AreEqual(
                new[] { selectedDecoy.InstanceID },
                rememberedMission
                    .GetDecoyParticipants(includeDisabled: true)
                    .Select(participant => participant.InstanceID)
            );
            Assert.IsEmpty(
                rememberedMission
                    .GetAllParticipants(includeDisabled: true)
                    .Where(participant => participant.InstanceID == hiddenOfficer.InstanceID)
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_MissionsOnly_ReparentsKnownParticipant()
        {
            Officer officer = CreateOfficer("PARTICIPANT", _empire);
            _game.AttachNode(officer, _coruscant);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            Mission mission = CreateMission("NEW_MISSION", _empire, _coruscant);
            _game.AttachNode(mission, _coruscant);
            _game.MoveNode(officer, mission);

            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                PlanetIntelligenceCategory.Missions
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.IsEmpty(snapshot.Officers);
            Mission rememberedMission = snapshot.Missions.Single();
            Assert.AreEqual(mission.InstanceID, rememberedMission.InstanceID);
            Assert.AreEqual(
                officer.InstanceID,
                rememberedMission.GetMainParticipants(includeDisabled: true).Single().InstanceID
            );
            Assert.AreEqual(1, FindSnapshotEntities(snapshot, officer.InstanceID).Length);
        }

        [Test]
        public void RecordIntelligenceSnapshot_CarrierMovesPlanets_PreservesUnrefreshedCargo()
        {
            MakeTatooineImperial();
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "CARRIER");
            ship.StarfighterCapacity = 1;
            Officer officer = CreateOfficer("EMBARKED_OFFICER", _empire);
            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "EMBARKED_SPECIAL_FORCES",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter fighter = CreateStarfighter("EMBARKED_FIGHTER", _empire);
            _game.AttachNode(officer, ship);
            _game.AttachNode(specialForces, ship);
            _game.AttachNode(fighter, ship);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.MoveNode(fleet, _tatooine);
            _game.MoveNode(officer, _tatooine);
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _tatooine,
                _outerRim,
                20,
                PlanetIntelligenceCategory.CapitalShips
            );

            PlanetSnapshot destination = GetSnapshot(_outerRim, _tatooine);
            CapitalShip rememberedShip = destination
                .Fleets.Single()
                .GetChildren<CapitalShip>()
                .Single();
            Assert.AreEqual(
                officer.InstanceID,
                rememberedShip.GetChildren<Officer>().Single().InstanceID
            );
            Assert.AreEqual(
                fighter.InstanceID,
                rememberedShip.GetChildren<Starfighter>().Single().InstanceID
            );
            Assert.AreEqual(
                specialForces.InstanceID,
                rememberedShip.GetChildren<SpecialForces>().Single().InstanceID
            );
            Assert.IsEmpty(destination.Officers);
            Assert.IsEmpty(
                FindSnapshotEntities(GetSnapshot(_coreSector, _coruscant), ship.InstanceID)
            );
            Assert.AreEqual(_tatooine.InstanceID, _alliance.Fog.EntityLastSeenAt[ship.InstanceID]);
            Assert.AreEqual(
                _tatooine.InstanceID,
                _alliance.Fog.EntityLastSeenAt[officer.InstanceID]
            );
            Assert.AreEqual(
                _tatooine.InstanceID,
                _alliance.Fog.EntityLastSeenAt[fighter.InstanceID]
            );
            Assert.AreEqual(
                _tatooine.InstanceID,
                _alliance.Fog.EntityLastSeenAt[specialForces.InstanceID]
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_RemovedCapitalShip_CleansSnapshotIndexes()
        {
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "REMOVED_SHIP");
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.DeleteNode(ship);
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                PlanetIntelligenceCategory.CapitalShips
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.IsEmpty(FindSnapshotEntities(snapshot, ship.InstanceID));
            Assert.IsFalse(_alliance.Fog.EntityLastSeenAt.ContainsKey(ship.InstanceID));
        }

        [Test]
        public void RecordIntelligenceSnapshot_NarrowCategory_RefreshesStatusWithoutChangingFullObservationAge()
        {
            Officer officer = CreateOfficer("MOVING_OFFICER", _empire);
            _game.AttachNode(officer, _coruscant);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            _coruscant.IsColonized = false;
            _coruscant.IsDestroyed = true;
            officer.Movement = new MovementState { TransitTicks = 20, TicksElapsed = 5 };

            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                PlanetIntelligenceCategory.Officers
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.AreEqual(10, snapshot.FullyObservedAtTick);
            Assert.IsFalse(snapshot.IsColonized);
            Assert.IsTrue(snapshot.IsDestroyed);
            MovementState movement = snapshot.Officers.Single().Movement;
            Assert.AreEqual(20, movement.ObservedAtTick);
            Assert.IsTrue(movement.IsObservation);
        }

        [TestCase(
            PlanetIntelligenceCategory.Starfighters
                | PlanetIntelligenceCategory.Regiments
                | PlanetIntelligenceCategory.PlanetaryDefenses
        )]
        [TestCase(
            PlanetIntelligenceCategory.Manufacturing
                | PlanetIntelligenceCategory.ProductionFacilities
        )]
        [TestCase(PlanetIntelligenceCategory.Fleets)]
        [TestCase(PlanetIntelligenceCategory.Officers | PlanetIntelligenceCategory.SpecialForces)]
        public void RecordIntelligenceSnapshot_AuthoredNarrowCategoryOnUnknownPlanet_RevealsOnlyBasicStatus(
            PlanetIntelligenceCategory categories
        )
        {
            _coruscant.OwnerInstanceID = _empire.InstanceID;
            _coruscant.IsColonized = true;
            _coruscant.IsDestroyed = true;
            _coruscant.IsInUprising = true;
            _coruscant.IsHeadquarters = true;
            _coruscant.EnergyCapacity = 12;
            _coruscant.AllocatedEnergy = 5;
            _coruscant.NumRawResourceNodes = 8;
            _coruscant.PopularSupport[_empire.InstanceID] = 75;

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                categories,
                includeDescendants: false
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.AreEqual(20, snapshot.TickCaptured);
            Assert.AreEqual(-1, snapshot.FullyObservedAtTick);
            Assert.AreEqual(_empire.InstanceID, snapshot.OwnerInstanceID);
            Assert.IsTrue(snapshot.IsColonized);
            Assert.IsTrue(snapshot.IsDestroyed);
            Assert.IsFalse(snapshot.IsInUprising);
            Assert.IsFalse(snapshot.IsHeadquarters);
            Assert.Zero(snapshot.EnergyCapacity);
            Assert.Zero(snapshot.AllocatedEnergy);
            Assert.Zero(snapshot.NumRawResourceNodes);
            Assert.IsEmpty(snapshot.PopularSupport);
        }

        [TestCase(
            PlanetIntelligenceCategory.Starfighters
                | PlanetIntelligenceCategory.Regiments
                | PlanetIntelligenceCategory.PlanetaryDefenses
        )]
        [TestCase(
            PlanetIntelligenceCategory.Manufacturing
                | PlanetIntelligenceCategory.ProductionFacilities
        )]
        [TestCase(PlanetIntelligenceCategory.Fleets)]
        [TestCase(PlanetIntelligenceCategory.Officers | PlanetIntelligenceCategory.SpecialForces)]
        public void RecordIntelligenceSnapshot_AuthoredNarrowCategoryOnKnownPlanet_RefreshesOnlyBasicStatus(
            PlanetIntelligenceCategory categories
        )
        {
            _coruscant.IsInUprising = true;
            _coruscant.IsHeadquarters = true;
            _coruscant.EnergyCapacity = 12;
            _coruscant.AllocatedEnergy = 5;
            _coruscant.NumRawResourceNodes = 8;
            _coruscant.PopularSupport[_empire.InstanceID] = 75;
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            _coruscant.OwnerInstanceID = _alliance.InstanceID;
            _coruscant.IsColonized = false;
            _coruscant.IsDestroyed = true;
            _coruscant.IsInUprising = false;
            _coruscant.IsHeadquarters = false;
            _coruscant.EnergyCapacity = 20;
            _coruscant.AllocatedEnergy = 9;
            _coruscant.NumRawResourceNodes = 15;
            _coruscant.PopularSupport[_empire.InstanceID] = 25;

            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                categories,
                includeDescendants: false
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.AreEqual(20, snapshot.TickCaptured);
            Assert.AreEqual(10, snapshot.FullyObservedAtTick);
            Assert.AreEqual(_alliance.InstanceID, snapshot.OwnerInstanceID);
            Assert.IsFalse(snapshot.IsColonized);
            Assert.IsTrue(snapshot.IsDestroyed);
            Assert.IsTrue(snapshot.IsInUprising);
            Assert.IsTrue(snapshot.IsHeadquarters);
            Assert.AreEqual(12, snapshot.EnergyCapacity);
            Assert.AreEqual(5, snapshot.AllocatedEnergy);
            Assert.AreEqual(8, snapshot.NumRawResourceNodes);
            Assert.AreEqual(75, snapshot.PopularSupport[_empire.InstanceID]);
        }

        [Test]
        public void RecordPlanetSnapshot_OrdinaryObservation_RefreshesFullyObservedAtTick()
        {
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            recorder.RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.AreEqual(20, snapshot.TickCaptured);
            Assert.AreEqual(20, snapshot.FullyObservedAtTick);
        }

        [Test]
        public void RecordPlanetLossSnapshot_CurrentObservation_RefreshesFullyObservedAtTick()
        {
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            recorder.RecordPlanetLossSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.AreEqual(20, snapshot.TickCaptured);
            Assert.AreEqual(20, snapshot.FullyObservedAtTick);
        }

        [Test]
        public void RecordIntelligenceSnapshot_ManufacturingNotRefreshed_PreservesQueuedIntelligence()
        {
            Building first = AddQueuedBuilding(_coruscant, _empire, "FIRST_ORDER", 25);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);
            first.ManufacturingStatus = ManufacturingStatus.Complete;
            _coruscant.ManufacturingQueue[ManufacturingType.Building].Remove(first);
            Building second = AddQueuedBuilding(_coruscant, _empire, "SECOND_ORDER", 10);

            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                PlanetIntelligenceCategory.Officers
            );
            AssertQueuedIntelligence(GetSnapshot(_coreSector, _coruscant), "FIRST_ORDER", 25);

            recorder.RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 30);
            AssertQueuedIntelligence(GetSnapshot(_coreSector, _coruscant), "FIRST_ORDER", 25);

            recorder.RecordPlanetLossSnapshot(_alliance, _coruscant, _coreSector, 40);
            AssertQueuedIntelligence(GetSnapshot(_coreSector, _coruscant), "FIRST_ORDER", 25);

            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                50,
                PlanetIntelligenceCategory.Manufacturing
            );
            PlanetSnapshot refreshed = GetSnapshot(_coreSector, _coruscant);
            CollectionAssert.AreEqual(
                new[] { second.InstanceID },
                refreshed.ManufacturingQueueItems.Select(item => item.InstanceID)
            );
            Assert.AreEqual(10, refreshed.ManufacturingQueueItems.Single().ManufacturingProgress);
        }

        [Test]
        public void RecordIntelligenceSnapshot_ExpandedCoverage_PreservesRequestedMaskAndUnknownFullObservation()
        {
            PlanetIntelligenceCategory requested =
                PlanetIntelligenceCategory.All & ~PlanetIntelligenceCategory.GroundForces;

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                20,
                requested
            );

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.IsTrue(snapshot.HasIntelligence(PlanetIntelligenceCategory.All));
            Assert.AreEqual(requested, snapshot.RevealedCategories);
            Assert.AreEqual(-1, snapshot.FullyObservedAtTick);
        }

        [Test]
        public void RecordIntelligenceSnapshot_AccumulatedCoverage_PreservesUnknownFullObservation()
        {
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            PlanetIntelligenceCategory[] reports =
            {
                PlanetIntelligenceCategory.Planet,
                PlanetIntelligenceCategory.CapitalShips,
                PlanetIntelligenceCategory.Starfighters,
                PlanetIntelligenceCategory.Regiments,
                PlanetIntelligenceCategory.SpecialForces,
                PlanetIntelligenceCategory.Buildings,
                PlanetIntelligenceCategory.Officers,
                PlanetIntelligenceCategory.Missions,
                PlanetIntelligenceCategory.Manufacturing,
                PlanetIntelligenceCategory.Fleets,
            };
            foreach (PlanetIntelligenceCategory report in reports)
                recorder.RecordIntelligenceSnapshot(_alliance, _coruscant, _coreSector, 20, report);

            PlanetSnapshot snapshot = GetSnapshot(_coreSector, _coruscant);
            Assert.IsTrue(snapshot.HasIntelligence(PlanetIntelligenceCategory.All));
            Assert.AreEqual(-1, snapshot.FullyObservedAtTick);
        }

        [Test]
        public void RecordPlanetLossSnapshot_NarrowGroundForceIntelligence_ClearsCategories()
        {
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                41,
                PlanetIntelligenceCategory.Regiments | PlanetIntelligenceCategory.SpecialForces
            );

            recorder.RecordPlanetLossSnapshot(_alliance, _coruscant, _coreSector, 42);

            PlanetIntelligenceCategory categories = _alliance
                .Fog
                .Snapshots["CORE_SECTOR"]
                .Planets["CORUSCANT"]
                .RevealedCategories;
            Assert.IsFalse(categories.HasFlag(PlanetIntelligenceCategory.Regiments));
            Assert.IsFalse(categories.HasFlag(PlanetIntelligenceCategory.SpecialForces));
        }

        [Test]
        public void RecordIntelligenceSnapshot_PlanetControl_DoesNotRevealHeadquarters()
        {
            _coruscant.IsHeadquarters = true;
            _coruscant.PopularSupport[_empire.InstanceID] = 75;

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.PlanetControl
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsFalse(snapshot.IsHeadquarters);
            Assert.AreEqual(75, snapshot.PopularSupport[_empire.InstanceID]);
        }

        [Test]
        public void RecordIntelligenceSnapshot_ControlReport_HidesUnknownUprising()
        {
            _coruscant.IsInUprising = true;

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.PlanetControl
            );

            Assert.IsFalse(GetSnapshot(_coreSector, _coruscant).IsInUprising);
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void RecordIntelligenceSnapshot_ControlReport_PreservesKnownUprising(
            bool rememberedUprising,
            bool currentUprising
        )
        {
            _coruscant.IsInUprising = rememberedUprising;
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);
            _coruscant.IsInUprising = currentUprising;

            recorder.RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.PlanetControl
            );

            Assert.AreEqual(rememberedUprising, GetSnapshot(_coreSector, _coruscant).IsInUprising);
        }

        [TestCase(PlanetIntelligenceCategory.Planet, false)]
        [TestCase(PlanetIntelligenceCategory.Planet, true)]
        [TestCase(PlanetIntelligenceCategory.PlanetMilitaryProduction, false)]
        [TestCase(PlanetIntelligenceCategory.PlanetMilitaryProduction, true)]
        [TestCase(PlanetIntelligenceCategory.All, false)]
        [TestCase(PlanetIntelligenceCategory.All, true)]
        public void RecordIntelligenceSnapshot_PlanetReport_RefreshesUprising(
            PlanetIntelligenceCategory category,
            bool rememberedUprising
        )
        {
            _coruscant.IsInUprising = rememberedUprising;
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordPlanetSnapshot(_alliance, _coruscant, _coreSector, 10);
            _coruscant.IsInUprising = !rememberedUprising;

            recorder.RecordIntelligenceSnapshot(_alliance, _coruscant, _coreSector, 42, category);

            Assert.AreEqual(!rememberedUprising, GetSnapshot(_coreSector, _coruscant).IsInUprising);
        }

        [Test]
        public void RecordIntelligenceSnapshot_PlanetResources_DoesNotRevealAllocatedEnergy()
        {
            _coruscant.EnergyCapacity = 12;
            _coruscant.NumRawResourceNodes = 8;
            _coruscant.AllocatedEnergy = 5;

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.PlanetResources
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.AreEqual(12, snapshot.EnergyCapacity);
            Assert.AreEqual(8, snapshot.NumRawResourceNodes);
            Assert.Zero(snapshot.AllocatedEnergy);
        }

        [Test]
        public void RecordIntelligenceSnapshot_Planet_RevealsHeadquartersAndAllocatedEnergy()
        {
            _coruscant.IsHeadquarters = true;
            _coruscant.AllocatedEnergy = 5;

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.Planet
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsTrue(snapshot.IsHeadquarters);
            Assert.AreEqual(5, snapshot.AllocatedEnergy);
        }

        [Test]
        public void RecordIntelligenceSnapshot_StationedRegiments_ExcludesFleetCargo()
        {
            Regiment stationed = CreateRegiment("STATIONED", _empire);
            _game.AttachNode(stationed, _coruscant);
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            ship.RegimentCapacity = 1;
            _game.AttachNode(CreateRegiment("EMBARKED", _empire), ship);

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.Regiments,
                includeDescendants: false
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            CollectionAssert.AreEqual(
                new[] { stationed.InstanceID },
                snapshot.Regiments.Select(regiment => regiment.InstanceID)
            );
            Assert.IsEmpty(snapshot.Fleets);
        }

        [Test]
        public void RecordIntelligenceSnapshot_StationedOfficers_ExcludesFleetPersonnel()
        {
            Officer stationed = CreateOfficer("STATIONED", _empire);
            _game.AttachNode(stationed, _coruscant);
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            _game.AttachNode(CreateOfficer("EMBARKED", _empire), ship);

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.Officers,
                includeDescendants: false
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            CollectionAssert.AreEqual(
                new[] { stationed.InstanceID },
                snapshot.Officers.Select(officer => officer.InstanceID)
            );
            Assert.IsEmpty(snapshot.Fleets);
        }

        [Test]
        public void RecordIntelligenceSnapshot_StationedStarfighters_ExcludesFleetCargo()
        {
            Starfighter stationed = CreateStarfighter("STATIONED", _empire);
            _game.AttachNode(stationed, _coruscant);
            Fleet fleet = CreateFleet("IMPERIAL_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            CapitalShip ship = AddCapitalShip(fleet, _empire, "STAR_DESTROYER");
            ship.StarfighterCapacity = 1;
            _game.AttachNode(CreateStarfighter("EMBARKED", _empire), ship);

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.Starfighters,
                includeDescendants: false
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            CollectionAssert.AreEqual(
                new[] { stationed.InstanceID },
                snapshot.Starfighters.Select(starfighter => starfighter.InstanceID)
            );
            Assert.IsEmpty(snapshot.Fleets);
        }

        [Test]
        public void RecordIntelligenceSnapshot_PlanetaryDefenses_ExcludesProductionFacilities()
        {
            _coruscant.EnergyCapacity = 2;
            Building shield = CreateBuilding("SHIELD", _empire);
            shield.BuildingType = BuildingType.Defense;
            Building mine = CreateBuilding("MINE", _empire);
            mine.BuildingType = BuildingType.Mine;
            _game.AttachNode(shield, _coruscant);
            _game.AttachNode(mine, _coruscant);

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.PlanetaryDefenses
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            CollectionAssert.AreEqual(
                new[] { "SHIELD" },
                snapshot.Buildings.Select(x => x.InstanceID)
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_ProductionFacilities_ExcludesPlanetaryDefenses()
        {
            _coruscant.EnergyCapacity = 2;
            Building shield = CreateBuilding("SHIELD", _empire);
            shield.BuildingType = BuildingType.Defense;
            Building mine = CreateBuilding("MINE", _empire);
            mine.BuildingType = BuildingType.Mine;
            _game.AttachNode(shield, _coruscant);
            _game.AttachNode(mine, _coruscant);

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.ProductionFacilities
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            CollectionAssert.AreEqual(
                new[] { "MINE" },
                snapshot.Buildings.Select(x => x.InstanceID)
            );
        }

        [Test]
        public void RecordIntelligenceSnapshot_Manufacturing_RevealsQueuedUnits()
        {
            Building queued = AddQueuedBuilding(_coruscant, _empire, "QUEUED_MINE", 25);

            new FogOfWarRecorder().RecordIntelligenceSnapshot(
                _alliance,
                _coruscant,
                _coreSector,
                42,
                PlanetIntelligenceCategory.Manufacturing
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsTrue(snapshot.HasManufacturingIntelligence);
            Assert.AreEqual(
                queued.InstanceID,
                snapshot.ManufacturingQueueItems.OfType<Building>().Single().InstanceID
            );
        }

        [Test]
        public void RecordEspionageSnapshot_MissionCompletion_PreservesParticipantIntelligence()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.DisplayName = "Darth Vader";
            vader.DisplayImagePath = "officers/vader";
            _game.AttachNode(vader, _coruscant);
            Officer tarkin = CreateOfficer("TARKIN", _empire);
            tarkin.DisplayName = "Grand Moff Tarkin";
            _game.AttachNode(tarkin, _coruscant);

            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            _game.MoveNode(vader, empireMission);
            empireMission.AddDecoyParticipant(tarkin);
            _game.MoveNode(tarkin, empireMission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.MoveNode(vader, _coruscant);
            _game.MoveNode(tarkin, _coruscant);
            vader.DisplayName = "Changed live officer";
            vader.DisplayImagePath = "officers/changed";
            tarkin.DisplayName = "Changed live decoy";

            Mission recordedMission = _alliance
                .Fog.Snapshots[_coreSector.InstanceID]
                .Planets[_coruscant.InstanceID]
                .Missions.Single();
            Officer recordedParticipant =
                recordedMission.GetMainParticipants(includeDisabled: true).Single() as Officer;
            Officer recordedDecoy =
                recordedMission.GetDecoyParticipants(includeDisabled: true).Single() as Officer;

            Assert.IsNotNull(recordedParticipant);
            Assert.AreNotSame(vader, recordedParticipant);
            Assert.AreEqual(vader.InstanceID, recordedParticipant.InstanceID);
            Assert.AreEqual("Darth Vader", recordedParticipant.DisplayName);
            Assert.AreEqual("officers/vader", recordedParticipant.DisplayImagePath);
            Assert.IsNotNull(recordedDecoy);
            Assert.AreNotSame(tarkin, recordedDecoy);
            Assert.AreEqual(tarkin.InstanceID, recordedDecoy.InstanceID);
            Assert.AreEqual("Grand Moff Tarkin", recordedDecoy.DisplayName);
        }

        [Test]
        public void RecordEspionageSnapshot_DisabledMissionParticipant_PreservesParticipant()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);
            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            _game.MoveNode(vader, empireMission);
            vader.IsEnabled = false;

            new FogOfWarRecorder().RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            Mission recordedMission = _alliance
                .Fog.Snapshots[_coreSector.InstanceID]
                .Planets[_coruscant.InstanceID]
                .Missions.Single();
            Officer recordedParticipant =
                recordedMission.GetMainParticipants(includeDisabled: true).Single() as Officer;

            Assert.IsNotNull(recordedParticipant);
            Assert.AreEqual(vader.InstanceID, recordedParticipant.InstanceID);
            Assert.IsFalse(recordedParticipant.IsEnabled);
        }

        [Test]
        public void RecordPlanetOwnershipSnapshot_TransferredBuildings_UpdatesKnownWithoutRevealingUnknown()
        {
            _tatooine.IsColonized = true;
            _tatooine.EnergyCapacity = 2;
            Building knownBuilding = new Building
            {
                InstanceID = "KNOWN_MINE",
                OwnerInstanceID = null,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(knownBuilding, _tatooine);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordPlanetSnapshot(_alliance, _tatooine, _outerRim, 10);

            Building unknownBuilding = new Building
            {
                InstanceID = "UNKNOWN_MINE",
                OwnerInstanceID = null,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(unknownBuilding, _tatooine);
            _tatooine.SetOwnerInstanceID(_empire.InstanceID);
            knownBuilding.SetOwnerInstanceID(_empire.InstanceID);
            unknownBuilding.SetOwnerInstanceID(_empire.InstanceID);

            recorder.RecordPlanetOwnershipSnapshot(_alliance, _tatooine, _outerRim, 20);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_outerRim.InstanceID].Planets[
                _tatooine.InstanceID
            ];
            Assert.AreEqual(_empire.InstanceID, snapshot.OwnerInstanceID);
            Assert.AreEqual(_empire.InstanceID, snapshot.Buildings.Single().OwnerInstanceID);
            Assert.AreEqual(knownBuilding.InstanceID, snapshot.Buildings.Single().InstanceID);
        }

        /// <summary>
        /// Observes one cargo family after its carrier changes fleets, retaining older knowledge of other cargo.
        /// </summary>
        /// <param name="category">The cargo family revealed by the later report.</param>
        /// <param name="changesPlanet">Whether the new fleet is on a different planet.</param>
        /// <returns>The destination snapshot and the identifiers of cargo outside the later report.</returns>
        private (PlanetSnapshot Snapshot, string[] RememberedIDs) ObserveCarrierAfterFleetChange(
            PlanetIntelligenceCategory category,
            bool changesPlanet
        )
        {
            string reportedKind = category switch
            {
                PlanetIntelligenceCategory.Officers => "officer",
                PlanetIntelligenceCategory.Regiments => "regiment",
                PlanetIntelligenceCategory.SpecialForces => "special forces",
                PlanetIntelligenceCategory.Starfighters => "starfighter",
                _ => throw new AssertionException($"Unexpected cargo category: {category}"),
            };
            Fleet origin = CreateFleet("FLEET_A", _empire);
            _game.AttachNode(origin, _coruscant);
            CapitalShip ship = AddCapitalShip(origin, _empire, "CARRIER");
            ship.RegimentCapacity = 4;
            ship.StarfighterCapacity = 4;
            ISceneNode[] knownCargo = new[]
            {
                "officer",
                "regiment",
                "special forces",
                "starfighter",
            }
                .Select(kind =>
                    CreateUnit(kind, kind == reportedKind ? "REPLACED" : "KNOWN_" + kind)
                )
                .ToArray();
            foreach (ISceneNode unit in knownCargo)
                _game.AttachNode(unit, ship);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            Planet destination = changesPlanet ? _tatooine : _coruscant;
            Fleet target = CreateFleet("FLEET_B", _empire);
            _game.AttachNode(target, destination);
            _game.MoveNode(ship, target);
            foreach (ISceneNode unit in knownCargo)
                _game.DeleteNode(unit);
            _game.AttachNode(CreateUnit(reportedKind, "REPORTED"), ship);
            _game.AttachNode(
                CreateUnit(reportedKind == "officer" ? "regiment" : "officer", "UNREPORTED"),
                ship
            );
            recorder.RecordIntelligenceSnapshot(
                _alliance,
                destination,
                destination.GetParentOfType<PlanetSector>(),
                20,
                category,
                includeDescendants: true
            );

            return (
                GetSnapshot(destination.GetParentOfType<PlanetSector>(), destination),
                knownCargo
                    .Where(unit => unit.InstanceID != "REPLACED")
                    .Select(unit => unit.InstanceID)
                    .ToArray()
            );
        }

        /// <summary>
        /// Creates one mobile unit of the requested kind for placement tests.
        /// </summary>
        /// <param name="unitKind">The requested unit kind.</param>
        /// <param name="instanceId">The unit instance ID.</param>
        /// <returns>The created unit.</returns>
        private ISceneNode CreateUnit(string unitKind, string instanceId)
        {
            return unitKind switch
            {
                "officer" => CreateOfficer(instanceId, _empire),
                "regiment" => CreateRegiment(instanceId, _empire),
                "special forces" => new SpecialForces
                {
                    InstanceID = instanceId,
                    OwnerInstanceID = _empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                "starfighter" => CreateStarfighter(instanceId, _empire),
                _ => throw new AssertionException($"Unknown unit kind: {unitKind}"),
            };
        }

        /// <summary>
        /// Returns the recorded snapshot for a planet.
        /// </summary>
        /// <param name="sector">The planet's sector.</param>
        /// <param name="planet">The recorded planet.</param>
        /// <returns>The recorded planet snapshot.</returns>
        private PlanetSnapshot GetSnapshot(PlanetSector sector, Planet planet) =>
            _alliance.Fog.Snapshots[sector.InstanceID].Planets[planet.InstanceID];

        /// <summary>
        /// Finds every physical representation of an entity in one planet snapshot.
        /// </summary>
        /// <param name="snapshot">The snapshot to search.</param>
        /// <param name="instanceId">The entity instance ID.</param>
        /// <returns>All matching physical representations.</returns>
        private static ISceneNode[] FindSnapshotEntities(PlanetSnapshot snapshot, string instanceId)
        {
            return snapshot
                .Officers.Cast<ISceneNode>()
                .Concat(snapshot.Regiments)
                .Concat(snapshot.SpecialForces)
                .Concat(snapshot.Starfighters)
                .Concat(snapshot.Buildings)
                .Concat(
                    snapshot.Fleets.SelectMany(fleet =>
                        fleet.GetChildren<ISceneNode>(recursive: true).Prepend(fleet)
                    )
                )
                .Concat(
                    snapshot.Missions.SelectMany(mission =>
                        mission.GetChildren<ISceneNode>(recursive: true).Prepend(mission)
                    )
                )
                .Where(entity => entity.InstanceID == instanceId)
                .ToArray();
        }

        /// <summary>
        /// Asserts that one remembered manufacturing order retains its recorded progress.
        /// </summary>
        /// <param name="snapshot">The snapshot containing manufacturing intelligence.</param>
        /// <param name="instanceId">The expected order instance ID.</param>
        /// <param name="progress">The expected remembered progress.</param>
        private static void AssertQueuedIntelligence(
            PlanetSnapshot snapshot,
            string instanceId,
            int progress
        )
        {
            Assert.AreEqual(1, snapshot.ManufacturingQueueItems.Count);
            Assert.AreEqual(instanceId, snapshot.ManufacturingQueueItems.Single().InstanceID);
            Assert.AreEqual(
                progress,
                snapshot.ManufacturingQueueItems.Single().ManufacturingProgress
            );
        }
    }
}
