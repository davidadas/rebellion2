using System.Linq;
using NUnit.Framework;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Tests.Helpers;

namespace Rebellion.Tests.Game.FogOfWar
{
    [TestFixture]
    public class FogOfWarRecorderTests : FogOfWarTestBase
    {
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
        public void RecordIntelligenceSnapshot_NarrowCategory_PreservesPlanetAgeAndStatus()
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
            Assert.AreEqual(10, snapshot.FullObservationTick);
            Assert.IsTrue(snapshot.IsColonized);
            Assert.IsFalse(snapshot.IsDestroyed);
            MovementState movement = snapshot.Officers.Single().Movement;
            Assert.AreEqual(20, movement.ObservedAtTick);
            Assert.IsTrue(movement.IsObservation);
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
