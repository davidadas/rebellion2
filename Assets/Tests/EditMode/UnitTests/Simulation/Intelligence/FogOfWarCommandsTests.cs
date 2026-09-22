using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Tests.Helpers;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class FogOfWarCommandsTests : FogOfWarTestBase
    {
        private FogOfWarCommands _commands;
        private FogOfWarQueries _queries;

        /// <summary>
        /// Creates the subject for the arranged faction scene.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _commands = new FogOfWarCommands(_game);
            _queries = new FogOfWarQueries(_game);
        }

        [Test]
        public void CaptureSnapshot_PlanetWithAllEntities_CreatesAccurateSnapshot()
        {
            _coruscant.NumRawResourceNodes = 5;
            _coruscant.EnergyCapacity = 1;
            Officer vader = CreateOfficer("VADER", _empire);
            Fleet imperialFleet = CreateFleet("FLEET1", _empire);
            CapitalShip destroyer = new CapitalShip
            {
                InstanceID = "SD1",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Regiment stormtroopers = CreateRegiment("REG1", _empire);
            Building starport = CreateBuilding("BLDG1", _empire);
            Starfighter tieFighter = CreateStarfighter("TIE1", _empire);

            _game.AttachNode(vader, _coruscant);
            _game.AttachNode(imperialFleet, _coruscant);
            _game.AttachNode(destroyer, imperialFleet);
            _game.AttachNode(stormtroopers, _coruscant);
            _coruscant.AddChild(starport);
            _coruscant.AddChild(tieFighter);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
            Assert.AreEqual(1, snapshot.Fleets.Count);
            Assert.AreEqual(1, snapshot.Regiments.Count);
            Assert.AreEqual(1, snapshot.Buildings.Count);
            Assert.AreEqual(1, snapshot.Starfighters.Count);
            Assert.AreEqual("FNEMP1", snapshot.OwnerInstanceID);
            Assert.AreEqual(5, snapshot.NumRawResourceNodes);
        }

        [Test]
        public void CaptureSnapshot_DeepCopy_ModifyingGameDoesNotAffectSnapshot()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.SetBaseRating(SkillRating.Diplomacy, 50);
            _game.AttachNode(vader, _coruscant);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            vader.SetBaseRating(SkillRating.Diplomacy, 99);
            _coruscant.RemoveChild(vader);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
            Assert.AreEqual(50, snapshot.Officers[0].GetBaseRating(SkillRating.Diplomacy));
        }

        [Test]
        public void CaptureSnapshot_SingleEntity_CopiesEntityWithSameInstanceID()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual("VADER", snapshot.Officers[0].InstanceID);
            Assert.AreNotSame(vader, snapshot.Officers[0]);
        }

        [Test]
        public void CaptureSnapshot_UnvisitedPlanet_MarksPlanetVisited()
        {
            Assert.IsFalse(_coruscant.WasVisitedBy(_alliance.InstanceID));

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Assert.IsTrue(_coruscant.WasVisitedBy(_alliance.InstanceID));
        }

        [Test]
        public void CaptureSnapshot_EntityMoves_RemovedFromOldPlanetSnapshot()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);

            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            PlanetSectorSnapshot coreSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(0, coruscantSnapshot.Officers.Count);

            PlanetSectorSnapshot outerSnapshot = _alliance.Fog.Snapshots["OUTERRIM"];
            PlanetSnapshot tatooineSnapshot = outerSnapshot.Planets["TATOOINE"];

            Assert.AreEqual(1, tatooineSnapshot.Officers.Count);
            Assert.AreEqual("VADER", tatooineSnapshot.Officers[0].InstanceID);
        }

        [Test]
        public void CaptureSnapshot_MultipleEntitiesMove_InvalidationIndependentPerEntity()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            Fleet fleet = CreateFleet("FLEET1", _empire);
            _game.AttachNode(vader, _coruscant);
            _game.AttachNode(fleet, _coruscant);
            AddCapitalShip(fleet, _empire, "CS1");

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            _hoth.OwnerInstanceID = _empire.InstanceID; // Set owner so fleet can move here
            _game.MoveNode(fleet, _hoth);
            _commands.CaptureSnapshot(_alliance, _hoth, _outerRim, 30);

            PlanetSectorSnapshot coreSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(0, coruscantSnapshot.Officers.Count);
            Assert.AreEqual(0, coruscantSnapshot.Fleets.Count);
        }

        [Test]
        public void CaptureSnapshot_EntitySeenTwiceSamePlanet_DoesNotDuplicate()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);
            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
        }

        [Test]
        public void CaptureSnapshot_EntityMovesBackToOriginalPlanet_HandledCorrectly()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            _game.MoveNode(vader, _coruscant);
            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 30);

            PlanetSectorSnapshot coreSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, coruscantSnapshot.Officers.Count);

            PlanetSectorSnapshot outerSnapshot = _alliance.Fog.Snapshots["OUTERRIM"];
            PlanetSnapshot tatooineSnapshot = outerSnapshot.Planets["TATOOINE"];

            Assert.AreEqual(0, tatooineSnapshot.Officers.Count);
        }

        [Test]
        public void CaptureSnapshot_VaderRediscovered_RemovesFromOldPlanet()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);

            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            GalaxyMap view = _queries.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Planet viewTatooine = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(0, viewCoruscant.GetChildren<Officer>().Count);
            Assert.AreEqual(1, viewTatooine.GetChildren<Officer>().Count);
        }

        [Test]
        public void CaptureSnapshot_EmptyPlanet_CreatesPlanetSnapshot()
        {
            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 10);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["OUTERRIM"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["TATOOINE"];

            Assert.IsNotNull(snapshot);
        }

        [Test]
        public void CaptureSnapshot_NestedEntityObservedElsewhere_RemovesOldFleetManifestEntry()
        {
            Fleet fleet = CreateFleet("FLEET", _empire);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
            };
            Regiment regiment = CreateRegiment("REGIMENT", _empire);
            _game.AttachNode(fleet, _coruscant);
            _game.AttachNode(ship, fleet);
            _game.AttachNode(regiment, ship);
            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(regiment, _tatooine);
            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            PlanetSnapshot oldSnapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            Assert.IsEmpty(
                oldSnapshot
                    .Fleets.Single()
                    .GetChildren<CapitalShip>()
                    .Single()
                    .GetChildren<Regiment>()
            );
            Assert.AreEqual(
                _tatooine.InstanceID,
                _alliance.Fog.EntityLastSeenAt[regiment.InstanceID]
            );
        }

        [Test]
        public void CaptureSnapshot_EntityOnPlanet_UpdatesLastSeenIndex()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Assert.AreEqual("CORUSCANT", _alliance.Fog.EntityLastSeenAt["VADER"]);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            Assert.AreEqual("TATOOINE", _alliance.Fog.EntityLastSeenAt["VADER"]);
        }

        [Test]
        public void CaptureSnapshot_PlanetInPlanetSector_MapsPlanetToSector()
        {
            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Assert.AreEqual("CORE_SECTOR", _alliance.Fog.PlanetToSector["CORUSCANT"]);

            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            Assert.AreEqual("OUTERRIM", _alliance.Fog.PlanetToSector["TATOOINE"]);
        }

        [Test]
        public void CaptureSnapshot_PlanetVisible_SnapshotNotOverwrittenWithoutExplicitCall()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.SetBaseRating(SkillRating.Diplomacy, 50);
            _game.AttachNode(vader, _coruscant);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];
            int originalTickCaptured = snapshot.TickCaptured;

            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            vader.SetBaseRating(SkillRating.Diplomacy, 99);

            Assert.AreEqual(
                originalTickCaptured,
                snapshot.TickCaptured,
                "Snapshot tick should not change"
            );
            Assert.AreEqual(
                50,
                snapshot.Officers[0].GetBaseRating(SkillRating.Diplomacy),
                "Snapshot should preserve old skill value"
            );
            Assert.AreEqual(1, snapshot.Officers.Count, "Snapshot should not include new entities");
        }

        [Test]
        public void CaptureSnapshot_Invalidation_RemovesOnlyTargetEntity()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            Officer tarkin = CreateOfficer("PALPATINE", _empire);
            Fleet fleet = CreateFleet("FLEET1", _empire);
            CapitalShip destroyer = new CapitalShip
            {
                InstanceID = "SD1",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(vader, _coruscant);
            _game.AttachNode(tarkin, _coruscant);
            _game.AttachNode(fleet, _coruscant);
            _game.AttachNode(destroyer, fleet);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            PlanetSectorSnapshot coreSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(
                1,
                coruscantSnapshot.Officers.Count,
                "Should have 1 officer (Palpatine)"
            );
            Assert.AreEqual(
                "PALPATINE",
                coruscantSnapshot.Officers[0].InstanceID,
                "Palpatine should remain"
            );
            Assert.AreEqual(1, coruscantSnapshot.Fleets.Count, "Fleet should remain");

            PlanetSectorSnapshot outerSnapshot = _alliance.Fog.Snapshots["OUTERRIM"];
            PlanetSnapshot tatooineSnapshot = outerSnapshot.Planets["TATOOINE"];
            Assert.AreEqual(
                1,
                tatooineSnapshot.Officers.Count,
                "Tatooine should have 1 officer (Vader)"
            );
            Assert.AreEqual(
                "VADER",
                tatooineSnapshot.Officers[0].InstanceID,
                "Vader should be at new location"
            );
        }

        [Test]
        public void CaptureSnapshot_CapturedFriendlyOfficer_IncludesDetachedOfficer()
        {
            Officer leia = CreateOfficer("LEIA", _alliance);
            leia.IsCaptured = true;
            leia.CaptorInstanceID = _empire.InstanceID;
            _game.AttachNode(leia, _coruscant);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];

            Officer observed = snapshot.Officers.Single(officer => officer.InstanceID == "LEIA");
            Assert.AreNotSame(leia, observed);
            Assert.IsTrue(observed.IsCaptured);
            Assert.AreEqual(_empire.InstanceID, observed.CaptorInstanceID);
        }

        [Test]
        public void CaptureSnapshot_OrdinaryObservation_ManufacturingRemainsHidden()
        {
            AddQueuedBuilding(_coruscant, _empire, "HIDDEN_BUILDING", 25);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsFalse(snapshot.HasManufacturingIntelligence);
            Assert.IsEmpty(snapshot.ManufacturingQueueItems);
            Assert.IsFalse(
                snapshot.Buildings.Any(building => building.InstanceID == "HIDDEN_BUILDING")
            );
        }

        [Test]
        public void CaptureSnapshot_ParticipantSeenElsewhere_PreservesRecordedMissionIdentity()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.DisplayName = "Darth Vader";
            _game.AttachNode(vader, _coruscant);

            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            _game.MoveNode(vader, empireMission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            vader.DisplayName = "Vader observed elsewhere";
            _commands.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            PlanetSnapshot coruscantSnapshot = _alliance
                .Fog
                .Snapshots[_coreSector.InstanceID]
                .Planets[_coruscant.InstanceID];
            Officer recordedParticipant =
                coruscantSnapshot
                    .Missions.Single()
                    .GetMainParticipants(includeDisabled: true)
                    .Single() as Officer;

            Assert.IsEmpty(coruscantSnapshot.Officers);
            Assert.IsNotNull(recordedParticipant);
            Assert.AreNotSame(vader, recordedParticipant);
            Assert.AreEqual(vader.InstanceID, recordedParticipant.InstanceID);
            Assert.AreEqual("Darth Vader", recordedParticipant.DisplayName);
        }

        [Test]
        public void CaptureSnapshot_AfterEspionage_PreservesIncomingEnemyFleet()
        {
            Fleet empireFleet = CreateFleet("INCOMING_FLEET", _empire);
            _game.AttachNode(empireFleet, _coruscant);
            AddCapitalShip(empireFleet, _empire, "INCOMING_SHIP");
            empireFleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            GalaxyMap view = _queries.BuildFactionView(_alliance);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == _coruscant.InstanceID);
            Fleet viewFleet = viewCoruscant
                .GetChildren<Fleet>()
                .Single(fleet => fleet.InstanceID == empireFleet.InstanceID);
            Assert.IsNotNull(viewFleet.Movement);
        }

        [Test]
        public void CaptureSnapshot_AfterEspionage_PreservesMissionIntelligence()
        {
            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            Assert.AreEqual(1, snapshot.Missions.Count);
            Assert.AreEqual(empireMission.InstanceID, snapshot.Missions[0].InstanceID);
        }

        [Test]
        public void CaptureSnapshot_AfterEspionage_PreservesStaleManufacturingIntel()
        {
            Building knownBuilding = AddQueuedBuilding(_coruscant, _empire, "KNOWN_BUILDING", 25);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            knownBuilding.ManufacturingProgress = 75;
            AddQueuedBuilding(_coruscant, _empire, "UNKNOWN_BUILDING", 10);
            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            GalaxyMap view = _queries.BuildFactionView(_alliance);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == _coruscant.InstanceID);
            List<IManufacturable> queue = viewCoruscant.ManufacturingQueue[
                ManufacturingType.Building
            ];

            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual("KNOWN_BUILDING", queue[0].InstanceID);
            Assert.AreEqual(25, queue[0].ManufacturingProgress);
        }

        [Test]
        public void CaptureSnapshot_AfterEspionage_RemovesAbsentManufacturingIntel()
        {
            Building knownBuilding = AddQueuedBuilding(_coruscant, _empire, "KNOWN_BUILDING", 25);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _coruscant.ManufacturingQueue[ManufacturingType.Building].Remove(knownBuilding);
            _game.DetachNode(knownBuilding);
            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            Assert.IsFalse(
                snapshot.Buildings.Any(building => building.InstanceID == knownBuilding.InstanceID)
            );
            Assert.IsFalse(
                snapshot.ManufacturingQueueItems.Any(item =>
                    item.InstanceID == knownBuilding.InstanceID
                )
            );
        }

        [Test]
        public void CaptureSnapshot_AfterEspionage_RemovesAbsentCargoFromPreservedShip()
        {
            Fleet fleet = CreateFleet("KNOWN_FLEET", _empire);
            _game.AttachNode(fleet, _coruscant);
            AddCapitalShip(fleet, _empire, "VISIBLE_SHIP");
            CapitalShip knownShip = new CapitalShip
            {
                InstanceID = "KNOWN_SHIP",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Building,
                RegimentCapacity = 1,
            };
            Regiment departedRegiment = CreateRegiment("DEPARTED_REGIMENT", _empire);
            _game.AttachNode(knownShip, fleet);
            _game.AttachNode(departedRegiment, knownShip);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.DetachNode(departedRegiment);
            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            CapitalShip preservedShip = snapshot
                .Fleets.Single(snapshotFleet => snapshotFleet.InstanceID == fleet.InstanceID)
                .GetChildren<CapitalShip>()
                .Single(ship => ship.InstanceID == knownShip.InstanceID);
            Assert.IsEmpty(preservedShip.GetChildren<Regiment>());
        }

        [Test]
        public void CaptureSnapshot_AfterEspionage_PreservesFleetContainingOnlyManufacturingShip()
        {
            Fleet fleet = CreateFleet("KNOWN_FLEET", _empire);
            CapitalShip knownShip = new CapitalShip
            {
                InstanceID = "KNOWN_SHIP",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 25,
            };
            _game.AttachNode(fleet, _coruscant);
            _game.AttachNode(knownShip, fleet);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            knownShip.ManufacturingProgress = 75;
            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            Fleet preservedFleet = snapshot.Fleets.Single(snapshotFleet =>
                snapshotFleet.InstanceID == fleet.InstanceID
            );
            CapitalShip preservedShip = preservedFleet.GetChildren<CapitalShip>().Single();
            Assert.AreEqual(knownShip.InstanceID, preservedShip.InstanceID);
            Assert.AreEqual(25, preservedShip.ManufacturingProgress);
            Assert.AreEqual(
                _coruscant.InstanceID,
                _alliance.Fog.EntityLastSeenAt[fleet.InstanceID]
            );
            Assert.AreEqual(
                _coruscant.InstanceID,
                _alliance.Fog.EntityLastSeenAt[knownShip.InstanceID]
            );
        }

        [Test]
        public void CaptureSnapshot_AfterEspionage_RemovesAbsentFleetContainingOnlyManufacturingShip()
        {
            Fleet fleet = CreateFleet("KNOWN_FLEET", _empire);
            CapitalShip knownShip = new CapitalShip
            {
                InstanceID = "KNOWN_SHIP",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            _game.AttachNode(fleet, _coruscant);
            _game.AttachNode(knownShip, fleet);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.DetachNode(knownShip);
            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            Assert.IsFalse(
                snapshot.Fleets.Any(snapshotFleet => snapshotFleet.InstanceID == fleet.InstanceID)
            );
            Assert.IsFalse(_alliance.Fog.EntityLastSeenAt.ContainsKey(fleet.InstanceID));
            Assert.IsFalse(_alliance.Fog.EntityLastSeenAt.ContainsKey(knownShip.InstanceID));
        }

        [Test]
        public void CaptureSnapshot_EnemyUnitsInTransit_NotRecorded()
        {
            Officer officer = CreateOfficer("MOVING_OFFICER", _empire);
            officer.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(officer, _coruscant);

            Regiment regiment = CreateRegiment("MOVING_REGIMENT", _empire);
            regiment.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(regiment, _coruscant);

            Starfighter starfighter = CreateStarfighter("MOVING_STARFIGHTER", _empire);
            starfighter.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(starfighter, _coruscant);

            Fleet fleet = CreateFleet("MOVING_FLEET", _empire);
            fleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(fleet, _coruscant);
            AddCapitalShip(fleet, _empire, "MOVING_FLEET_SHIP");

            _commands.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            Assert.IsEmpty(snapshot.Officers);
            Assert.IsEmpty(snapshot.Regiments);
            Assert.IsEmpty(snapshot.Starfighters);
            Assert.IsEmpty(snapshot.Fleets);
        }

        [Test]
        public void CaptureSnapshot_EmptyFleet_ExcludedFromSnapshot()
        {
            // An empty fleet (no capital ships) should not appear in snapshots
            Fleet emptyFleet = new Fleet
            {
                InstanceID = "empty_fleet",
                OwnerInstanceID = _empire.InstanceID,
            };
            _game.AttachNode(emptyFleet, _coruscant);

            _commands.CaptureSnapshot(_empire, _coruscant, _coreSector, _game.CurrentTick);

            GalaxyMap view = _queries.BuildFactionView(_empire);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.IsFalse(
                viewCoruscant.GetChildren<Fleet>().Any(f => f.InstanceID == "empty_fleet"),
                "Empty fleet should not appear in snapshot"
            );
        }

        [Test]
        public void CaptureSnapshot_FleetWithShips_IncludedInSnapshot()
        {
            Fleet fleet = new Fleet
            {
                InstanceID = "armed_fleet",
                OwnerInstanceID = _empire.InstanceID,
            };
            _game.AttachNode(fleet, _coruscant);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = _empire.InstanceID,
            };
            _game.AttachNode(ship, fleet);

            _commands.CaptureSnapshot(_empire, _coruscant, _coreSector, _game.CurrentTick);

            GalaxyMap view = _queries.BuildFactionView(_empire);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.IsTrue(
                viewCoruscant.GetChildren<Fleet>().Any(f => f.InstanceID == "armed_fleet"),
                "Fleet with capital ships should appear in snapshot"
            );
        }
    }
}
