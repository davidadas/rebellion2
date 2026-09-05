using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Sectors
{
    [TestFixture]
    public class FogOfWarSystemTests
    {
        private GameRoot _game;
        private FogOfWarSystem _fogSystem;
        private Faction _alliance;
        private Faction _empire;
        private PlanetSector _coreSector;
        private PlanetSector _outerRim;
        private Planet _coruscant;
        private Planet _tatooine;
        private Planet _hoth;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = new GameConfig();
            _game = new GameRoot(config);
            _fogSystem = new FogOfWarSystem(_game);

            _alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            _empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            _game.GetFactions().Add(_alliance);
            _game.GetFactions().Add(_empire);

            _coreSector = new PlanetSector
            {
                InstanceID = "CORE_SECTOR",
                DisplayName = "Core Sector",
                SectorType = PlanetSectorType.Core,
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(_coreSector, _game.GetGalaxyMap());

            _outerRim = new PlanetSector
            {
                InstanceID = "OUTERRIM",
                DisplayName = "Outer Rim Sector",
                SectorType = PlanetSectorType.OuterRim,
                PositionX = 100,
                PositionY = 100,
            };
            _game.AttachNode(_outerRim, _game.GetGalaxyMap());

            _coruscant = new Planet
            {
                InstanceID = "CORUSCANT",
                DisplayName = "Coruscant",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
            };
            _game.AttachNode(_coruscant, _coreSector);

            _tatooine = new Planet
            {
                InstanceID = "TATOOINE",
                DisplayName = "Tatooine",
                OwnerInstanceID = null,
            };
            _game.AttachNode(_tatooine, _outerRim);

            _hoth = new Planet
            {
                InstanceID = "HOTH",
                DisplayName = "Hoth",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            _game.AttachNode(_hoth, _outerRim);
        }

        [Test]
        public void GetPlanetLastSeenTick_VisiblePlanet_ReturnsCurrentTick()
        {
            _game.CurrentTick = 123;

            int? tick = _fogSystem.GetPlanetLastSeenTick(_alliance, _hoth.InstanceID);

            Assert.AreEqual(123, tick);
        }

        [Test]
        public void GetPlanetLastSeenTick_SnapshotPlanet_ReturnsSnapshotTick()
        {
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 41);

            int? tick = _fogSystem.GetPlanetLastSeenTick(_alliance, _coruscant.InstanceID);

            Assert.AreEqual(41, tick);
        }

        [Test]
        public void GetPlanetLastSeenTick_UnexploredPlanet_ReturnsNull()
        {
            Assert.IsNull(_fogSystem.GetPlanetLastSeenTick(_alliance, _tatooine.InstanceID));
        }

        [Test]
        public void BuildFactionView_UnexploredPlanet_EmptySnapshot()
        {
            Assert.AreEqual(
                2,
                _game.Galaxy.GetChildren<PlanetSector>().Count,
                "Setup should have added 2 sectors to galaxy"
            );
            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
            Assert.AreEqual(
                2,
                view.GetChildren<PlanetSector>().Count,
                "BuildFactionView should return galaxy with 2 sectors"
            );

            Planet viewTatooine = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(0, viewTatooine.GetChildren<Officer>().Count);
            Assert.AreEqual(0, viewTatooine.GetChildren<Fleet>().Count);
            Assert.AreEqual(0, viewTatooine.GetChildren<Regiment>().Count);
        }

        [Test]
        public void BuildFactionView_UnexploredOwnedPlanet_HidesStatus()
        {
            _coruscant.EnergyCapacity = 7;
            _coruscant.NumRawResourceNodes = 5;

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.IsNull(
                viewCoruscant.OwnerInstanceID,
                "Unexplored planet must not reveal its owner"
            );
            Assert.AreEqual(
                0,
                viewCoruscant.EnergyCapacity,
                "Unexplored planet must not reveal capacity"
            );
            Assert.AreEqual(
                0,
                viewCoruscant.NumRawResourceNodes,
                "Unexplored planet must not reveal resources"
            );
            Assert.IsFalse(
                viewCoruscant.IsColonized,
                "Unexplored planet must not reveal colonization"
            );
            Assert.AreEqual(
                "Coruscant",
                viewCoruscant.GetDisplayName(),
                "Planet identity stays known"
            );
        }

        [Test]
        public void BuildFactionView_OwnFleetInTransitToUnexploredPlanet_ShowsFleetWithoutLivePlanetData()
        {
            _coruscant.EnergyCapacity = 7;

            Fleet ownFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(ownFleet, _coruscant);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs1", OwnerInstanceID = _alliance.InstanceID },
                ownFleet
            );
            ownFleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 0 };

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Fleet>().Count(f => f.InstanceID == "FLEET1"),
                "Own in-transit fleet must be visible heading to the planet"
            );
            Assert.IsNull(
                viewCoruscant.OwnerInstanceID,
                "An in-transit fleet must not reveal the unvisited destination's owner"
            );
            Assert.AreEqual(
                0,
                viewCoruscant.EnergyCapacity,
                "An in-transit fleet must not reveal the unvisited destination's capacity"
            );
        }

        [Test]
        public void BuildFactionView_UnexploredOuterRimAndCore_BothHiddenWithoutSnapshot()
        {
            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Assert.IsTrue(view.GetChildren<PlanetSector>().Any(s => s.InstanceID == "CORE_SECTOR"));
            Assert.IsTrue(view.GetChildren<PlanetSector>().Any(s => s.InstanceID == "OUTERRIM"));

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(0, viewCoruscant.GetChildren<Officer>().Count);
        }

        [Test]
        public void BuildFactionView_SnapshotPlanet_ReturnsVisitedViewPlanet()
        {
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == _coruscant.InstanceID);

            Assert.IsTrue(viewCoruscant.WasVisitedBy(_alliance.InstanceID));
            Assert.IsFalse(viewCoruscant.IsUnexploredView);
        }

        [Test]
        public void BuildFactionView_VisiblePlanet_UsesLiveData()
        {
            Officer leia = CreateOfficer("LEIA", _alliance);
            _game.AttachNode(leia, _hoth);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(1, viewHoth.GetChildren<Officer>().Count);
            Assert.AreEqual("LEIA", viewHoth.GetChildren<Officer>()[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_LivePlanet_ModifyingViewDoesNotAffectGame()
        {
            Officer leia = CreateOfficer("LEIA", _alliance);
            _game.AttachNode(leia, _hoth);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            viewHoth.RemoveChildren<Officer>(_ => true);

            Assert.AreEqual(1, _hoth.GetChildren<Officer>().Count);
        }

        [Test]
        public void BuildFactionView_LivePlanet_BuildingsPreserved()
        {
            Building groundFacility = CreateBuilding("BLDG1", _alliance);
            Building orbitStation = CreateBuilding("BLDG2", _alliance);
            _hoth.AddTestChild(groundFacility);
            _hoth.AddTestChild(orbitStation);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(2, viewHoth.GetChildren<Building>().Count);
        }

        [Test]
        public void BuildFactionView_NotVisibleWithSnapshot_UsesSnapshotData()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.GetChildren<Officer>().Count);
            Assert.AreEqual("VADER", viewCoruscant.GetChildren<Officer>()[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_Snapshot_ModifyingViewDoesNotAffectSnapshot()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            viewCoruscant.RemoveChildren<Officer>(_ => true);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
        }

        [Test]
        public void BuildFactionView_SnapshotBuildings_Visible()
        {
            Building facility = CreateBuilding("BLDG1", _empire);
            _coruscant.AddTestChild(facility);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.GetChildren<Building>().Count);
        }

        [Test]
        public void BuildFactionView_UnownedCorePlanet_UsesCurrentPopularSupport()
        {
            _coruscant.PopularSupport["FNALL1"] = 50;
            _coruscant.EnergyCapacity = 8;

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);
            _coruscant.PopularSupport["FNALL1"] = 25;
            _coruscant.EnergyCapacity = 3;

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(25, viewCoruscant.PopularSupport["FNALL1"]);
            Assert.AreEqual(8, viewCoruscant.EnergyCapacity);
        }

        [Test]
        public void BuildFactionView_UnownedCorePlanet_UsesCurrentUprisingState()
        {
            _coruscant.IsInUprising = false;
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);
            _coruscant.IsInUprising = true;

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.IsTrue(viewCoruscant.IsInUprising);
        }

        [Test]
        public void BuildFactionView_Snapshot_PreservesObservedResources()
        {
            _coruscant.NumRawResourceNodes = 5;

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(5, viewCoruscant.NumRawResourceNodes);
        }

        [Test]
        public void BuildFactionView_FleetLeaves_UsesSnapshot()
        {
            _coruscant.NumRawResourceNodes = 5;
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.MoveNode(allianceFleet, _hoth);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.GetChildren<Officer>().Count);
            Assert.AreEqual(5, viewCoruscant.NumRawResourceNodes);
        }

        [Test]
        public void BuildFactionView_FleetMoves_FleetNotDuplicated()
        {
            Fleet fleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(fleet, _hoth);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs1", OwnerInstanceID = _alliance.InstanceID },
                fleet
            );

            _game.MoveNode(fleet, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                0,
                viewHoth.GetChildren<Fleet>().Count(f => f.InstanceID == "FLEET1"),
                "Fleet must not remain at the origin planet"
            );
            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Fleet>().Count(f => f.InstanceID == "FLEET1"),
                "Fleet must appear at its destination"
            );

            int totalOccurrences = view.GetChildren<PlanetSector>()
                .SelectMany(s => s.GetChildren<Planet>())
                .Sum(p => p.GetChildren<Fleet>().Count(f => f.InstanceID == "FLEET1"));
            Assert.AreEqual(
                1,
                totalOccurrences,
                "Fleet must appear exactly once across the entire faction view"
            );
        }

        [Test]
        public void BuildFactionView_OwnFleetInTransit_DestinationUsesSnapshot()
        {
            _coruscant.NumRawResourceNodes = 5;
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Fleet ownFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(ownFleet, _coruscant);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs1", OwnerInstanceID = _alliance.InstanceID },
                ownFleet
            );
            ownFleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 0 };

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Fleet>().Count(f => f.InstanceID == "FLEET1"),
                "Own in-transit fleet must be visible at its destination"
            );
            Assert.AreEqual(
                5,
                viewCoruscant.NumRawResourceNodes,
                "In transit must not grant live vision -- destination stays on the observed snapshot"
            );
        }

        [Test]
        public void BuildFactionView_OwnFleetArrived_DestinationUsesLive()
        {
            _coruscant.NumRawResourceNodes = 5;
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Fleet ownFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(ownFleet, _coruscant);
            _game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "cs1",
                    OwnerInstanceID = _alliance.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                ownFleet
            );
            ownFleet.Movement = null;

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Fleet>().Count(f => f.InstanceID == "FLEET1"),
                "Own arrived fleet must be visible"
            );
            Assert.AreEqual(
                5,
                viewCoruscant.NumRawResourceNodes,
                "An arrived fleet grants live vision of the destination"
            );
        }

        [Test]
        public void BuildFactionView_LivePlanet_StaleSnapshotFriendlyFleet_NotShown()
        {
            // Fleet A is snapshotted at coruscant, then moves away.
            // Fleet B (a different friendly fleet) arrives and makes the planet live.
            // The view must show only Fleet B — the stale snapshot entry for Fleet A must not appear.
            Fleet fleetA = CreateFleet("FLEET_A", _alliance);
            _game.AttachNode(fleetA, _coruscant);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs_a", OwnerInstanceID = _alliance.InstanceID },
                fleetA
            );
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            _hoth.OwnerInstanceID = _alliance.InstanceID;
            _game.MoveNode(fleetA, _hoth);

            Fleet fleetB = CreateFleet("FLEET_B", _alliance);
            _game.AttachNode(fleetB, _coruscant);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs_b", OwnerInstanceID = _alliance.InstanceID },
                fleetB
            );

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Fleet>().Count,
                "Only the live fleet should appear"
            );
            Assert.AreEqual(
                "FLEET_B",
                viewCoruscant.GetChildren<Fleet>()[0].InstanceID,
                "Stale snapshot fleet must not bleed into live view"
            );
        }

        [Test]
        public void BuildFactionView_VaderMovesWithoutObservation_StaleIntelPersists()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Planet viewTatooine = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(1, viewCoruscant.GetChildren<Officer>().Count);
            Assert.AreEqual("VADER", viewCoruscant.GetChildren<Officer>()[0].InstanceID);

            Assert.AreEqual(0, viewTatooine.GetChildren<Officer>().Count);
        }

        [Test]
        public void BuildFactionView_PlanetWithNoEntities_HandledCorrectly()
        {
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewTatooine = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(0, viewTatooine.GetChildren<Officer>().Count);
            Assert.AreEqual(0, viewTatooine.GetChildren<Fleet>().Count);
        }

        [Test]
        public void BuildFactionView_SectorWithMultiplePlanets_MixedVisibilityHandledCorrectly()
        {
            MakeTatooineImperial();
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _tatooine);

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewTatooine = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "TATOOINE");

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(1, viewTatooine.GetChildren<Officer>().Count);
        }

        [Test]
        public void BuildFactionView_NoSnapshotsAnywhere_AllPlanetsEmptySnapshots()
        {
            GalaxyMap view = _fogSystem.BuildFactionView(_empire);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(0, viewHoth.GetChildren<Officer>().Count);
        }

        [Test]
        public void BuildFactionView_PlanetsWithSharedEntities_NoDuplicateEntitiesAcrossPlanets()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            List<Officer> allOfficers = view.GetChildren<PlanetSector>()
                .SelectMany(s => s.GetChildren<Planet>())
                .SelectMany(p => p.GetChildren<Officer>())
                .ToList();

            int vaderCount = allOfficers.Count(o => o.InstanceID == "VADER");

            Assert.AreEqual(1, vaderCount);
        }

        [Test]
        public void BuildFactionView_EntitiesOnMultiplePlanets_PreservesInstanceIDs()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            Fleet fleet = CreateFleet("DEATHSTAR", _empire);
            CapitalShip executor = new CapitalShip
            {
                InstanceID = "EX1",
                OwnerInstanceID = _empire.InstanceID,
            };
            Regiment regiment = CreateRegiment("STORMTROOPERS", _empire);
            _game.AttachNode(vader, _coruscant);
            _game.AttachNode(fleet, _coruscant);
            _game.AttachNode(executor, fleet);
            _game.AttachNode(regiment, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_empire);

            PlanetSector viewSector = view.GetChildren<PlanetSector>()
                .FirstOrDefault(s => s.InstanceID == "CORE_SECTOR");
            Assert.IsNotNull(viewSector, "CORE_SECTOR should exist in view");

            Planet viewPlanet = viewSector
                .GetChildren<Planet>()
                .FirstOrDefault(p => p.InstanceID == "CORUSCANT");
            Assert.IsNotNull(viewPlanet, "CORUSCANT should exist in view");

            Assert.AreEqual(
                "VADER",
                viewPlanet.GetChildren<Officer>()[0].InstanceID,
                "Officer InstanceID should be preserved"
            );
            Assert.AreEqual(
                "DEATHSTAR",
                viewPlanet.GetChildren<Fleet>()[0].InstanceID,
                "Fleet InstanceID should be preserved"
            );
            Assert.AreEqual(
                "STORMTROOPERS",
                viewPlanet.GetChildren<Regiment>()[0].InstanceID,
                "Regiment InstanceID should be preserved"
            );
        }

        [Test]
        public void BuildFactionView_CapturedFriendlyOfficerOnVisiblePlanet_ReturnsOfficer()
        {
            // The Alliance fleet supplies visibility independently of the captured officer.
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            Officer leia = CreateOfficer("LEIA", _alliance);
            leia.IsCaptured = true;
            _game.AttachNode(leia, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.IsTrue(
                viewCoruscant.GetChildren<Officer>().Any(o => o.InstanceID == "LEIA"),
                "Captured friendly officer must appear as live data on a visible planet"
            );
        }

        [Test]
        public void BuildFactionView_CapturedFriendlyOfficerOnSnapshotPlanet_DoesNotRevealOfficer()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Officer leia = CreateOfficer("LEIA", _alliance);
            leia.IsCaptured = true;
            _game.AttachNode(leia, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.IsFalse(viewCoruscant.GetChildren<Officer>().Any(o => o.InstanceID == "LEIA"));
        }

        [Test]
        public void BuildFactionView_CapturedFriendlyOfficerOnUnexploredPlanet_DoesNotRevealOfficer()
        {
            Officer leia = CreateOfficer("LEIA", _alliance);
            leia.IsCaptured = true;
            _game.AttachNode(leia, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.IsFalse(viewCoruscant.GetChildren<Officer>().Any(o => o.InstanceID == "LEIA"));
        }

        [Test]
        public void BuildFactionView_OwnPlanet_ManufacturingQueueVisible()
        {
            Building queuedBuilding = AddQueuedBuilding(_hoth, _alliance, "OWN_BUILDING", 25);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(sector => sector.InstanceID == _outerRim.InstanceID)
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == _hoth.InstanceID);

            Assert.AreSame(
                queuedBuilding,
                viewHoth.ManufacturingQueue[ManufacturingType.Building].Single()
            );
        }

        [Test]
        public void BuildFactionView_FleetAtEnemyPlanet_ManufacturingRemainsHidden()
        {
            AddQueuedBuilding(_coruscant, _empire, "HIDDEN_BUILDING", 25);
            Fleet fleet = CreateFleet("OBSERVING_FLEET", _alliance);
            _game.AttachNode(fleet, _coruscant);
            AddCapitalShip(fleet, _alliance, "OBSERVING_SHIP");

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == _coruscant.InstanceID);

            Assert.IsFalse(
                viewCoruscant
                    .GetChildren<Building>()
                    .Any(building => building.InstanceID == "HIDDEN_BUILDING")
            );
            Assert.IsEmpty(viewCoruscant.ManufacturingQueue);
        }

        [Test]
        public void BuildFactionView_OwnPlanet_EnemyMissionsNotVisible()
        {
            // Empire owns coruscant; alliance runs a mission there.
            // Empire's view should not expose the alliance mission.
            Mission allianceMission = CreateMission("M1", _alliance, _coruscant);
            _game.AttachNode(allianceMission, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_empire);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                0,
                viewCoruscant.GetChildren<Mission>().Count,
                "Enemy missions should not be visible on your own planet"
            );
        }

        [Test]
        public void BuildFactionView_OwnPlanet_OwnMissionsVisible()
        {
            // Empire owns coruscant and runs a mission there.
            // Empire's view SHOULD show their own mission.
            // NOTE: This test is RED until BuildFactionView exposes own-faction missions.
            _coruscant.PopularSupport["FNALL1"] = 50;
            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);
            _game.MoveNode(vader, empireMission);

            GalaxyMap view = _fogSystem.BuildFactionView(_empire);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Mission viewMission = viewCoruscant.GetChildren<Mission>().Single();
            Officer viewParticipant = viewMission
                .GetMainParticipants(includeDisabled: true)
                .Cast<Officer>()
                .Single();

            Assert.AreNotSame(empireMission, viewMission);
            Assert.AreNotSame(vader, viewParticipant);
            Assert.AreEqual(vader.InstanceID, viewParticipant.InstanceID);
        }

        [Test]
        public void BuildFactionView_LivePlanet_EspionageMissionIntelligenceRemainsVisible()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);

            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Officer>().Count,
                "Live officer (Vader) should be visible"
            );
            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Mission>().Count,
                "A mission revealed by espionage should remain visible with live planet intel"
            );
        }

        [Test]
        public void BuildFactionView_FleetAtEnemyPlanet_EnemyMissionsStillHidden()
        {
            // Alliance fleet sits at coruscant (empire planet).
            // Alliance should see units (live) but NOT empire missions running there.
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                0,
                viewCoruscant.GetChildren<Mission>().Count,
                "Enemy missions should remain hidden even when a friendly fleet is present"
            );
        }

        [Test]
        public void BuildFactionView_FleetAtEnemyPlanet_EnemyOfficerVisible()
        {
            // Alliance fleet orbits coruscant (empire's planet) -> live view for _alliance.
            // Empire officer is stationed there (valid — same owner as planet).
            // Alliance live view should include the enemy officer.
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            Officer tarkin = CreateOfficer("PALPATINE", _empire);
            _game.AttachNode(tarkin, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Officer>().Count,
                "Enemy officer should be visible when you have live intel on the planet"
            );
            Assert.AreEqual("PALPATINE", viewCoruscant.GetChildren<Officer>()[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_SnapshotPlanet_EntityAddedAfterSnapshot_NotVisible()
        {
            // Snapshot coruscant; then add a new officer after the snapshot is taken.
            // The new officer must NOT appear in the view.
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Officer lateArrival = CreateOfficer("MOFF1", _empire);
            _game.AttachNode(lateArrival, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                0,
                viewCoruscant.GetChildren<Officer>().Count,
                "Officer added after snapshot should not appear in the view"
            );
        }

        [Test]
        public void BuildFactionView_SnapshotPlanet_BuildingQueuedAfterSnapshot_NotVisible()
        {
            _coruscant.EnergyCapacity = 1;
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Building queuedBuilding = CreateBuilding(
                "BLDG_AFTER",
                _empire,
                ManufacturingStatus.Building
            );
            queuedBuilding.ConstructionCost = 100;
            queuedBuilding.BaseBuildSpeed = 1;
            queuedBuilding.BuildingType = BuildingType.Mine;

            ManufacturingSystem manufacturing = new ManufacturingSystem(
                _game,
                new FleetSystem(_game)
            );
            bool enqueued = manufacturing.Enqueue(
                _coruscant,
                queuedBuilding,
                _coruscant,
                ignoreCost: true
            );

            Assert.IsTrue(enqueued);
            Assert.AreEqual(1, _coruscant.GetChildren<Building>().Count);
            Assert.AreEqual(ManufacturingStatus.Building, queuedBuilding.ManufacturingStatus);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.IsFalse(
                viewCoruscant.GetChildren<Building>().Any(b => b.InstanceID == "BLDG_AFTER")
            );
        }

        [Test]
        public void BuildFactionView_SnapshotQueuedBuildingOnFullLivePlanet_SkipsGhostBuilding()
        {
            _coruscant.EnergyCapacity = 5;

            Building queuedBuilding = CreateBuilding(
                "BLDG_GHOST",
                _empire,
                ManufacturingStatus.Building
            );
            queuedBuilding.ConstructionCost = 100;
            queuedBuilding.BaseBuildSpeed = 1;
            queuedBuilding.BuildingType = BuildingType.Mine;
            ManufacturingSystem manufacturing = new ManufacturingSystem(
                _game,
                new FleetSystem(_game)
            );
            Assert.IsTrue(
                manufacturing.Enqueue(_coruscant, queuedBuilding, _coruscant, ignoreCost: true)
            );
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.DeleteNode(queuedBuilding);
            Building completedBuilding = CreateBuilding("BLDG_DONE", _empire);
            completedBuilding.BuildingType = BuildingType.Refinery;
            _game.AttachNode(completedBuilding, _coruscant);
            _coruscant.EnergyCapacity = 1;

            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            GalaxyMap view = null;
            Assert.DoesNotThrow(() => view = _fogSystem.BuildFactionView(_alliance));

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");
            Assert.IsTrue(
                viewCoruscant
                    .GetChildren<Building>(includeDisabled: true)
                    .All(b => b.InstanceID != "BLDG_GHOST"),
                "Ghost building intel should be skipped when the view planet has no energy capacity"
            );
        }

        [Test]
        public void BuildFactionView_LivePlanet_StaleOwnSnapshotUnits_NotVisible()
        {
            // Snapshot hoth while alliance has a fleet there.
            // Fleet moves away — now stale in the snapshot.
            // Live view should show only what is actually on hoth, not the stale snapshot fleet.
            Fleet staleFleet = CreateFleet("FLEET_STALE", _alliance);
            _game.AttachNode(staleFleet, _hoth);
            AddCapitalShip(staleFleet, _alliance, "CS1");
            _fogSystem.CaptureSnapshot(_alliance, _hoth, _outerRim, 10);
            _game.MoveNode(staleFleet, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(
                0,
                viewHoth.GetChildren<Fleet>().Count,
                "Stale own-faction fleet should not appear"
            );
        }

        [Test]
        public void BuildFactionView_LivePlanet_RemovesAbsentEnemyUnitsFromSnapshot()
        {
            Fleet enemyFleet = CreateFleet("ENEMY_FLEET", _empire);
            Officer enemyOfficer = CreateOfficer("ENEMY_OFFICER", _empire);
            Regiment enemyRegiment = CreateRegiment("ENEMY_REGIMENT", _empire);
            SpecialForces enemySpecialForces = new SpecialForces
            {
                InstanceID = "ENEMY_SPECIAL_FORCES",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            Starfighter enemyStarfighter = CreateStarfighter("ENEMY_STARFIGHTER", _empire);
            Building enemyBuilding = CreateBuilding("ENEMY_BUILDING", _empire);
            _coruscant.EnergyCapacity = 1;
            _game.AttachNode(enemyFleet, _coruscant);
            AddCapitalShip(enemyFleet, _empire, "ENEMY_SHIP");
            _game.AttachNode(enemyOfficer, _coruscant);
            _game.AttachNode(enemyRegiment, _coruscant);
            _game.AttachNode(enemySpecialForces, _coruscant);
            _game.AttachNode(enemyStarfighter, _coruscant);
            _game.AttachNode(enemyBuilding, _coruscant);
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.DetachNode(enemyFleet);
            _game.DetachNode(enemyOfficer);
            _game.DetachNode(enemyRegiment);
            _game.DetachNode(enemySpecialForces);
            _game.DetachNode(enemyStarfighter);
            _game.DetachNode(enemyBuilding);
            Fleet allianceFleet = CreateFleet("ALLIANCE_FLEET", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "ALLIANCE_SHIP");

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == _coruscant.InstanceID);

            CollectionAssert.AreEqual(
                new[] { allianceFleet.InstanceID },
                viewCoruscant.GetChildren<Fleet>().Select(fleet => fleet.InstanceID)
            );
            Assert.IsFalse(
                viewCoruscant
                    .GetChildren<Officer>()
                    .Any(officer => officer.InstanceID == enemyOfficer.InstanceID)
            );
            Assert.IsFalse(
                viewCoruscant
                    .GetChildren<Regiment>()
                    .Any(regiment => regiment.InstanceID == enemyRegiment.InstanceID)
            );
            Assert.IsFalse(
                viewCoruscant
                    .GetChildren<SpecialForces>()
                    .Any(specialForces => specialForces.InstanceID == enemySpecialForces.InstanceID)
            );
            Assert.IsFalse(
                viewCoruscant
                    .GetChildren<Starfighter>()
                    .Any(starfighter => starfighter.InstanceID == enemyStarfighter.InstanceID)
            );
            Assert.IsFalse(
                viewCoruscant
                    .GetChildren<Building>()
                    .Any(building => building.InstanceID == enemyBuilding.InstanceID)
            );

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);
            PlanetSnapshot refreshedSnapshot = _alliance
                .Fog
                .Snapshots[_coreSector.InstanceID]
                .Planets[_coruscant.InstanceID];
            Assert.IsEmpty(refreshedSnapshot.Fleets);
            Assert.IsEmpty(refreshedSnapshot.Officers);
            Assert.IsEmpty(refreshedSnapshot.Regiments);
            Assert.IsEmpty(refreshedSnapshot.SpecialForces);
            Assert.IsEmpty(refreshedSnapshot.Starfighters);
            Assert.IsEmpty(refreshedSnapshot.Buildings);
            Assert.IsEmpty(_alliance.Fog.EntityLastSeenAt);
        }

        [Test]
        public void BuildFactionView_PlanetCapturedFromEnemy_UsesOnlyLiveUnits()
        {
            // Coruscant was empire's. Alliance took a snapshot when empire owned it —
            // capturing an empire fleet. Alliance then takes ownership.
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", _empire);
            CapitalShip destroyer = new CapitalShip
            {
                InstanceID = "SD1",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(empireFleet, _coruscant);
            _game.AttachNode(destroyer, empireFleet);
            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            // Alliance takes ownership — empire units depart.
            _coruscant.OwnerInstanceID = _alliance.InstanceID;
            _game.MoveNode(empireFleet, _hoth);
            _game.DetachNode(empireMission);

            // Alliance officer now stationed on the captured planet.
            Officer leia = CreateOfficer("LEIA", _alliance);
            _game.AttachNode(leia, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Officer>().Count,
                "Live alliance officer should appear"
            );
            Assert.AreEqual("LEIA", viewCoruscant.GetChildren<Officer>()[0].InstanceID);
            Assert.IsEmpty(viewCoruscant.GetChildren<Fleet>());
            Assert.AreEqual(
                0,
                viewCoruscant.GetChildren<Mission>().Count,
                "An ordinary observation should not reveal enemy missions"
            );
        }

        [Test]
        public void BuildFactionView_OwnMission_OnEnemyPlanet_VisibleWithoutSnapshotOrFleet()
        {
            // Alliance runs a mission on coruscant (empire-owned).
            // No alliance fleet there, no prior snapshot.
            // Alliance should still see their own mission.
            Mission allianceMission = CreateMission("M1", _alliance, _coruscant);
            _game.AttachNode(allianceMission, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Mission>().Count,
                "Own mission on enemy planet should be visible without a snapshot or fleet"
            );
            Assert.AreEqual("M1", viewCoruscant.GetChildren<Mission>()[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_OwnMission_OnNeutralPlanet_VisibleWithoutSnapshotOrFleet()
        {
            // Alliance runs a mission on tatooine (neutral, uncolonized).
            // No fleet, no snapshot.
            Mission allianceMission = CreateMission("M1", _alliance, _tatooine);
            _game.AttachNode(allianceMission, _tatooine);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewTatooine = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(
                1,
                viewTatooine.GetChildren<Mission>().Count,
                "Own mission on neutral planet should be visible without a snapshot or fleet"
            );
            Assert.AreEqual("M1", viewTatooine.GetChildren<Mission>()[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_OwnFleet_AtEnemyPlanet_PlanetLiveWithoutSnapshot()
        {
            // Alliance fleet arrives at coruscant (empire-owned). No prior snapshot.
            // Planet should be live and fleet visible.
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs1", OwnerInstanceID = _alliance.InstanceID },
                allianceFleet
            );

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Fleet>().Count,
                "Own fleet should be visible"
            );
            Assert.AreEqual("FLEET1", viewCoruscant.GetChildren<Fleet>()[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_BlockadedOwnPlanet_StationaryEnemyFleet_IsVisible()
        {
            // Alliance owns Hoth; empire fleet is sitting at Hoth (not in transit).
            // Alliance should see the enemy fleet in their live view.
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", _empire);
            _game.AttachNode(empireFleet, _hoth);
            _game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "cs1",
                    OwnerInstanceID = _empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                empireFleet
            );

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(
                1,
                viewHoth.GetChildren<Fleet>().Count,
                "Stationary enemy fleet should be visible"
            );
            Assert.AreEqual("EMPIRE_FLEET", viewHoth.GetChildren<Fleet>()[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_BlockadedOwnPlanet_EnemyFleetInTransit_NotVisible()
        {
            // Alliance owns Hoth; empire fleet is en route to Hoth (in transit, Movement != null).
            // Fleet is parented to Hoth because RequestMove reparents immediately,
            // but it has not yet arrived. Alliance should NOT see it.
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", _empire);
            _game.AttachNode(empireFleet, _hoth);
            AddCapitalShip(empireFleet, _empire, "CS1");
            empireFleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(
                0,
                viewHoth.GetChildren<Fleet>().Count,
                "In-transit enemy fleet should not appear in the view"
            );
        }

        [Test]
        public void BuildFactionView_LiveEnemyPlanet_EnemyUnitsInTransit_NotVisible()
        {
            Fleet observerFleet = CreateFleet("ALLIANCE_FLEET", _alliance);
            _game.AttachNode(observerFleet, _coruscant);
            AddCapitalShip(observerFleet, _alliance, "ALLIANCE_SHIP");

            Officer officer = CreateOfficer("MOVING_OFFICER", _empire);
            officer.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(officer, _coruscant);

            Regiment regiment = CreateRegiment("MOVING_REGIMENT", _empire);
            regiment.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(regiment, _coruscant);

            SpecialForces specialForces = new SpecialForces
            {
                InstanceID = "MOVING_SPECIAL_FORCES",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 },
            };
            _game.AttachNode(specialForces, _coruscant);

            Starfighter starfighter = CreateStarfighter("MOVING_STARFIGHTER", _empire);
            starfighter.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(starfighter, _coruscant);

            _coruscant.EnergyCapacity = 1;
            Building building = CreateBuilding("MOVING_BUILDING", _empire);
            building.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(building, _coruscant);

            Planet viewCoruscant = _fogSystem
                .BuildFactionView(_alliance)
                .GetChildren<PlanetSector>()
                .Single(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .Single(planet => planet.InstanceID == _coruscant.InstanceID);

            Assert.IsEmpty(viewCoruscant.GetChildren<Officer>());
            Assert.IsEmpty(viewCoruscant.GetChildren<Regiment>());
            Assert.IsEmpty(viewCoruscant.GetChildren<SpecialForces>());
            Assert.IsEmpty(viewCoruscant.GetChildren<Starfighter>());
            Assert.IsEmpty(viewCoruscant.GetChildren<Building>());
        }

        [Test]
        public void BuildFactionView_LiveEnemyFleet_InTransitManifestNotVisible()
        {
            Fleet observerFleet = CreateFleet("ALLIANCE_FLEET", _alliance);
            _game.AttachNode(observerFleet, _coruscant);
            AddCapitalShip(observerFleet, _alliance, "ALLIANCE_SHIP");

            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", _empire);
            _game.AttachNode(empireFleet, _coruscant);
            CapitalShip stationaryShip = new CapitalShip
            {
                InstanceID = "STATIONARY_SHIP",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                RegimentCapacity = 1,
                StarfighterCapacity = 1,
            };
            _game.AttachNode(stationaryShip, empireFleet);

            CapitalShip movingShip = new CapitalShip
            {
                InstanceID = "MOVING_SHIP",
                OwnerInstanceID = _empire.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 },
            };
            _game.AttachNode(movingShip, empireFleet);
            _game.AttachNode(CreateOfficer("CARRIED_OFFICER", _empire), movingShip);

            Officer movingOfficer = CreateOfficer("MOVING_OFFICER", _empire);
            movingOfficer.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(movingOfficer, stationaryShip);

            Regiment movingRegiment = CreateRegiment("MOVING_REGIMENT", _empire);
            movingRegiment.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(movingRegiment, stationaryShip);

            Starfighter movingStarfighter = CreateStarfighter("MOVING_STARFIGHTER", _empire);
            movingStarfighter.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            _game.AttachNode(movingStarfighter, stationaryShip);

            Planet viewCoruscant = _fogSystem
                .BuildFactionView(_alliance)
                .GetChildren<PlanetSector>()
                .Single(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .Single(planet => planet.InstanceID == _coruscant.InstanceID);
            Fleet viewFleet = viewCoruscant
                .GetChildren<Fleet>()
                .Single(fleet => fleet.InstanceID == empireFleet.InstanceID);

            Assert.AreEqual(
                stationaryShip.InstanceID,
                viewFleet.GetChildren<CapitalShip>().Single().InstanceID
            );
            Assert.IsEmpty(viewFleet.GetChildren<CapitalShip>()[0].GetChildren<Officer>());
            Assert.IsEmpty(viewFleet.GetChildren<CapitalShip>()[0].GetChildren<Regiment>());
            Assert.IsEmpty(viewFleet.GetChildren<CapitalShip>()[0].GetChildren<Starfighter>());
        }

        [Test]
        public void BuildFactionView_OwnFleetInTransit_IsVisible()
        {
            // Alliance fleet is in transit to Hoth (alliance-owned). You should see your own fleet.
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _hoth);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs1", OwnerInstanceID = _alliance.InstanceID },
                allianceFleet
            );
            allianceFleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 4 };

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(
                1,
                viewHoth.GetChildren<Fleet>().Count,
                "Own in-transit fleet should be visible"
            );
            Assert.AreEqual("FLEET1", viewHoth.GetChildren<Fleet>()[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_LivePlanet_OrbingEnemyFleet_NotDuplicatedFromSnapshot()
        {
            // Empire fleet orbits Hoth (alliance-owned). Alliance takes a snapshot capturing it.
            // Fleet remains orbiting — still present live.
            // The fleet should appear exactly once in the faction view, not twice.
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", _empire);
            _game.AttachNode(empireFleet, _hoth);
            _game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "cs1",
                    OwnerInstanceID = _empire.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                empireFleet
            );

            _fogSystem.CaptureSnapshot(_alliance, _hoth, _outerRim, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(
                1,
                viewHoth.GetChildren<Fleet>().Count,
                "Orbiting enemy fleet already visible live should not be duplicated from snapshot"
            );
        }

        [Test]
        public void BuildFactionView_LivePlanet_EspionageSnapshotEnemyMission_IsSurfaced()
        {
            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);

            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.GetChildren<Mission>().Count,
                "Enemy missions captured by espionage should be surfaced"
            );
        }

        [Test]
        public void BuildFactionView_OuterRimSnapshot_PreservesObservedPopularSupport()
        {
            MakeTatooineImperial();
            _tatooine.PopularSupport["FNALL1"] = 40;

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 10);
            _tatooine.PopularSupport["FNALL1"] = 10;

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewTatooine = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(40, viewTatooine.PopularSupport["FNALL1"]);
        }

        [Test]
        public void BuildFactionView_OuterRimSnapshot_PreservesObservedUprisingState()
        {
            MakeTatooineImperial();
            _tatooine.IsInUprising = false;
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 10);
            _tatooine.IsInUprising = true;

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewTatooine = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "OUTERRIM")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "TATOOINE");

            Assert.IsFalse(viewTatooine.IsInUprising);
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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

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
            vader.SetBaseRating(OfficerRating.Diplomacy, 50);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            vader.SetBaseRating(OfficerRating.Diplomacy, 99);
            _coruscant.RemoveChild(vader);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
            Assert.AreEqual(50, snapshot.Officers[0].GetBaseRating(OfficerRating.Diplomacy));
        }

        [Test]
        public void CaptureSnapshot_SingleEntity_CopiesEntityWithSameInstanceID()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual("VADER", snapshot.Officers[0].InstanceID);
            Assert.AreNotSame(vader, snapshot.Officers[0]);
        }

        [Test]
        public void CaptureSnapshot_UnvisitedPlanet_MarksPlanetVisited()
        {
            Assert.IsFalse(_coruscant.WasVisitedBy(_alliance.InstanceID));

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Assert.IsTrue(_coruscant.WasVisitedBy(_alliance.InstanceID));
        }

        [Test]
        public void CaptureSnapshot_EntityMoves_RemovedFromOldPlanetSnapshot()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            _hoth.OwnerInstanceID = _empire.InstanceID; // Set owner so fleet can move here
            _game.MoveNode(fleet, _hoth);
            _fogSystem.CaptureSnapshot(_alliance, _hoth, _outerRim, 30);

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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
        }

        [Test]
        public void CaptureSnapshot_EntityMovesBackToOriginalPlanet_HandledCorrectly()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            _game.MoveNode(vader, _coruscant);
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 30);

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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

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
        public void CaptureSnapshot_EmptyPlanet_DoesNotCrash()
        {
            Assert.DoesNotThrow(() =>
            {
                _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 10);
            });

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
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(regiment, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Assert.AreEqual("CORUSCANT", _alliance.Fog.EntityLastSeenAt["VADER"]);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            Assert.AreEqual("TATOOINE", _alliance.Fog.EntityLastSeenAt["VADER"]);
        }

        [Test]
        public void CaptureSnapshot_PlanetInPlanetSector_MapsPlanetToSector()
        {
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            Assert.AreEqual("CORE_SECTOR", _alliance.Fog.PlanetToSector["CORUSCANT"]);

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

            Assert.AreEqual("OUTERRIM", _alliance.Fog.PlanetToSector["TATOOINE"]);
        }

        [Test]
        public void CaptureSnapshot_PlanetVisible_SnapshotNotOverwrittenWithoutExplicitCall()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.SetBaseRating(OfficerRating.Diplomacy, 50);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSectorSnapshot sectorSnapshot = _alliance.Fog.Snapshots["CORE_SECTOR"];
            PlanetSnapshot snapshot = sectorSnapshot.Planets["CORUSCANT"];
            int originalTickCaptured = snapshot.TickCaptured;

            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            vader.SetBaseRating(OfficerRating.Diplomacy, 99);

            Assert.AreEqual(
                originalTickCaptured,
                snapshot.TickCaptured,
                "Snapshot tick should not change"
            );
            Assert.AreEqual(
                50,
                snapshot.Officers[0].GetBaseRating(OfficerRating.Diplomacy),
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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            MakeTatooineImperial();
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsFalse(snapshot.HasManufacturingIntelligence);
            Assert.IsEmpty(snapshot.ManufacturingQueueItems);
            Assert.IsFalse(
                snapshot.Buildings.Any(building => building.InstanceID == "HIDDEN_BUILDING")
            );
        }

        [Test]
        public void CaptureSnapshot_ParticipantSeenElsewherePreservesRecordedMissionIdentity()
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
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRim, 20);

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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

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
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
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
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

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
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

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
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

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
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 20);

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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

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

            _fogSystem.CaptureSnapshot(_empire, _coruscant, _coreSector, _game.CurrentTick);

            GalaxyMap view = _fogSystem.BuildFactionView(_empire);
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

            _fogSystem.CaptureSnapshot(_empire, _coruscant, _coreSector, _game.CurrentTick);

            GalaxyMap view = _fogSystem.BuildFactionView(_empire);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(s => s.InstanceID == "CORE_SECTOR")
                .GetChildren<Planet>()
                .First(p => p.InstanceID == "CORUSCANT");

            Assert.IsTrue(
                viewCoruscant.GetChildren<Fleet>().Any(f => f.InstanceID == "armed_fleet"),
                "Fleet with capital ships should appear in snapshot"
            );
        }

        [Test]
        public void ProcessResults_SabotagedObject_RemovesObjectFromActorSnapshot()
        {
            _coruscant.EnergyCapacity = 1;
            Building mine = CreateBuilding("MINE1", _empire);
            _game.AttachNode(mine, _coruscant);

            Officer han = CreateOfficer("HAN", _alliance);
            _game.AttachNode(han, _hoth);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSector, 10);

            _game.DetachNode(mine);

            _fogSystem.ProcessResults(
                new List<GameObjectSabotagedResult>
                {
                    new GameObjectSabotagedResult
                    {
                        DestroyedObject = mine,
                        DestroyedBy = han,
                        Context = _coruscant,
                    },
                }
            );

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORE_SECTOR"].Planets["CORUSCANT"];
            Assert.IsFalse(snapshot.Buildings.Any(b => b.InstanceID == "MINE1"));
        }

        [Test]
        public void IsPlanetVisible_OwnedPlanet_ReturnsTrue()
        {
            bool visible = _fogSystem.IsPlanetVisible(_hoth, _alliance);

            Assert.IsTrue(visible);
        }

        [Test]
        public void IsPlanetVisible_FleetPresent_ReturnsTrue()
        {
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");

            bool visible = _fogSystem.IsPlanetVisible(_coruscant, _alliance);

            Assert.IsTrue(visible);
        }

        [Test]
        public void IsPlanetVisible_OwnFleetWithoutShips_ReturnsFalse()
        {
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);

            bool visible = _fogSystem.IsPlanetVisible(_coruscant, _alliance);

            Assert.IsFalse(visible);
        }

        [Test]
        public void IsPlanetVisible_NoOwnershipNoFleet_ReturnsFalse()
        {
            bool visible = _fogSystem.IsPlanetVisible(_tatooine, _alliance);

            Assert.IsFalse(visible);
        }

        [Test]
        public void IsPlanetVisible_CapturedFriendlyOfficerPresent_ReturnsFalse()
        {
            Officer captive = CreateOfficer("CAPTIVE", _alliance);
            captive.IsCaptured = true;
            captive.CaptorInstanceID = _empire.InstanceID;
            _game.AttachNode(captive, _coruscant);

            bool visible = _fogSystem.IsPlanetVisible(_coruscant, _alliance);

            Assert.IsFalse(visible);
        }

        [Test]
        public void IsPlanetVisible_MultipleFleetsDifferentFactions_OnlyOwnFactionCounts()
        {
            Fleet empireFleet = CreateFleet("FLEET1", _empire);
            _game.AttachNode(empireFleet, _tatooine);
            AddCapitalShip(empireFleet, _empire, "CS1");

            bool allianceVisible = _fogSystem.IsPlanetVisible(_tatooine, _alliance);
            bool empireVisible = _fogSystem.IsPlanetVisible(_tatooine, _empire);

            Assert.IsFalse(allianceVisible);
            Assert.IsTrue(empireVisible);
        }

        [Test]
        public void IsPlanetVisible_OwnFleetInTransit_DoesNotGrantVisibility()
        {
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            AddCapitalShip(allianceFleet, _alliance, "CS1");
            allianceFleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 3 };

            bool visible = _fogSystem.IsPlanetVisible(_coruscant, _alliance);

            Assert.IsFalse(
                visible,
                "An in-transit own fleet must not grant visibility of the destination"
            );
        }

        [Test]
        public void IsPlanetVisible_OwnCapitalShipInTransit_DoesNotGrantVisibility()
        {
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "CS1",
                OwnerInstanceID = _alliance.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState { TransitTicks = 10, TicksElapsed = 3 },
            };
            _game.AttachNode(capitalShip, allianceFleet);

            Assert.IsFalse(_fogSystem.IsPlanetVisible(_coruscant, _alliance));
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

            _fogSystem.HandleResults(
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

            _fogSystem.HandleResults(
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

            _fogSystem.HandleResults(
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
        public void HandleResults_SelectedManufacturingOrder_RevealsOnlySelectedOrder()
        {
            Building selected = AddQueuedBuilding(_coruscant, _empire, "SELECTED_ORDER", 25);
            AddQueuedBuilding(_coruscant, _empire, "HIDDEN_ORDER", 10);

            _fogSystem.HandleResults(
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
            Assert.AreEqual("TIE_SQUADRON", updatedSnapshot.Starfighters.Single().InstanceID);
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
        public void RecordEspionageSnapshot_EnemyManufacturing_RevealsManufacturing()
        {
            AddQueuedBuilding(_coruscant, _empire, "REVEALED_BUILDING", 25);
            FogOfWarRecorder recorder = new FogOfWarRecorder();

            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == _coruscant.InstanceID);

            Assert.IsTrue(
                viewCoruscant
                    .GetChildren<Building>()
                    .Any(building => building.InstanceID == "REVEALED_BUILDING")
            );
            Assert.AreEqual(
                "REVEALED_BUILDING",
                viewCoruscant.ManufacturingQueue[ManufacturingType.Building].Single().InstanceID
            );
        }

        [Test]
        public void RecordEspionageSnapshot_EnemyMissions_RevealsMissions()
        {
            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            FogOfWarRecorder recorder = new FogOfWarRecorder();

            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == _coruscant.InstanceID);

            Assert.AreEqual(1, viewCoruscant.GetChildren<Mission>().Count);
            Assert.AreEqual(
                empireMission.InstanceID,
                viewCoruscant.GetChildren<Mission>()[0].InstanceID
            );
        }

        [Test]
        public void RecordEspionageSnapshot_MissionCompletionPreservesParticipantIntelligence()
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
        public void RecordEspionageSnapshot_IncomingEnemyFleet_RevealsFleet()
        {
            Fleet empireFleet = CreateFleet("INCOMING_FLEET", _empire);
            _game.AttachNode(empireFleet, _coruscant);
            AddCapitalShip(empireFleet, _empire, "INCOMING_SHIP");
            empireFleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };
            FogOfWarRecorder recorder = new FogOfWarRecorder();

            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
            Planet viewCoruscant = view.GetChildren<PlanetSector>()
                .First(sector => sector.InstanceID == _coreSector.InstanceID)
                .GetChildren<Planet>()
                .First(planet => planet.InstanceID == _coruscant.InstanceID);

            Assert.AreEqual(1, viewCoruscant.GetChildren<Fleet>().Count);
            Assert.AreEqual(
                empireFleet.InstanceID,
                viewCoruscant.GetChildren<Fleet>()[0].InstanceID
            );
            Assert.IsNotNull(viewCoruscant.GetChildren<Fleet>()[0].Movement);
        }

        [Test]
        public void PlanetSnapshot_MissionParticipantIntelligenceSurvivesSerializationRoundTrip()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.DisplayName = "Darth Vader";
            vader.DisplayImagePath = "officers/vader";
            _game.AttachNode(vader, _coruscant);

            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);
            _game.MoveNode(vader, empireMission);
            FogOfWarRecorder recorder = new FogOfWarRecorder();
            recorder.RecordEspionageSnapshot(_alliance, _coruscant, _coreSector, 10);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots[_coreSector.InstanceID].Planets[
                _coruscant.InstanceID
            ];
            string xml = SerializationHelper.Serialize(snapshot);
            PlanetSnapshot restored = SerializationHelper.Deserialize<PlanetSnapshot>(xml);
            Officer restoredParticipant =
                restored.Missions.Single().GetMainParticipants(includeDisabled: true).Single()
                as Officer;

            Assert.IsNotNull(restoredParticipant);
            Assert.AreEqual(vader.InstanceID, restoredParticipant.InstanceID);
            Assert.AreEqual("Darth Vader", restoredParticipant.DisplayName);
            Assert.AreEqual("officers/vader", restoredParticipant.DisplayImagePath);
        }

        private Officer CreateOfficer(string id, Faction faction) =>
            EntityFactory.CreateOfficer(id, faction.InstanceID);

        private Building AddQueuedBuilding(Planet planet, Faction faction, string id, int progress)
        {
            Building building = CreateBuilding(id, faction, ManufacturingStatus.Building);
            building.ProducerOwnerID = faction.InstanceID;
            building.ProducerPlanetID = planet.InstanceID;
            building.ManufacturingProgress = progress;
            planet.EnergyCapacity = planet.GetChildren<Building>().Count + 1;
            _game.AttachNode(building, planet);
            planet.AddToManufacturingQueue(building);
            return building;
        }

        private void MakeTatooineImperial()
        {
            _tatooine.OwnerInstanceID = _empire.InstanceID;
            _tatooine.IsColonized = true;
        }

        private Fleet CreateFleet(string id, Faction faction) =>
            EntityFactory.CreateFleet(id, faction.InstanceID);

        private CapitalShip AddCapitalShip(Fleet fleet, Faction faction, string id)
        {
            CapitalShip ship = new CapitalShip
            {
                InstanceID = id,
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(ship, fleet);
            return ship;
        }

        private Regiment CreateRegiment(string id, Faction faction)
        {
            Regiment regiment = EntityFactory.CreateRegiment(id, faction.InstanceID);
            regiment.ManufacturingStatus = ManufacturingStatus.Complete;
            return regiment;
        }

        private Building CreateBuilding(
            string id,
            Faction faction,
            ManufacturingStatus status = ManufacturingStatus.Complete
        )
        {
            Building building = EntityFactory.CreateBuilding(id, faction.InstanceID);
            building.ManufacturingStatus = status;
            return building;
        }

        private Starfighter CreateStarfighter(string id, Faction faction)
        {
            Starfighter starfighter = EntityFactory.CreateStarfighter(id, faction.InstanceID);
            starfighter.ManufacturingStatus = ManufacturingStatus.Complete;
            return starfighter;
        }

        private StubMission CreateMission(string id, Faction owner, Planet target) =>
            EntityFactory.CreateMission(id, owner.InstanceID, target.InstanceID);
    }
}
