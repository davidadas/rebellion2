using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Extensions;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class FogOfWarSystemTests
    {
        private GameRoot _game;
        private FogOfWarSystem _fogSystem;
        private Faction _alliance;
        private Faction _empire;
        private PlanetSystem _coreSystem;
        private PlanetSystem _outerRimSystem;
        private Planet _coruscant;
        private Planet _tatooine;
        private Planet _hoth;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();
            _game = new GameRoot(config);
            _fogSystem = new FogOfWarSystem(_game);

            _alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            _empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            _game.Factions.Add(_alliance);
            _game.Factions.Add(_empire);

            _coreSystem = new PlanetSystem
            {
                InstanceID = "CORESYS",
                DisplayName = "Coruscant System",
                SystemType = PlanetSystemType.CoreSystem,
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(_coreSystem, _game.GetGalaxyMap());

            _outerRimSystem = new PlanetSystem
            {
                InstanceID = "OUTERRIM",
                DisplayName = "Outer Rim System",
                SystemType = PlanetSystemType.OuterRim,
                PositionX = 100,
                PositionY = 100,
            };
            _game.AttachNode(_outerRimSystem, _game.GetGalaxyMap());

            _coruscant = new Planet
            {
                InstanceID = "CORUSCANT",
                DisplayName = "Coruscant",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
            };
            _game.AttachNode(_coruscant, _coreSystem);

            _tatooine = new Planet
            {
                InstanceID = "TATOOINE",
                DisplayName = "Tatooine",
                OwnerInstanceID = null,
            };
            _game.AttachNode(_tatooine, _outerRimSystem);

            _hoth = new Planet
            {
                InstanceID = "HOTH",
                DisplayName = "Hoth",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            _game.AttachNode(_hoth, _outerRimSystem);
        }

        [Test]
        public void BuildFactionView_UnexploredPlanet_EmptyShell()
        {
            Assert.AreEqual(
                2,
                _game.Galaxy.PlanetSystems.Count,
                "Setup should have added 2 systems to galaxy"
            );
            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);
            Assert.AreEqual(
                2,
                view.PlanetSystems.Count,
                "BuildFactionView should return galaxy with 2 systems"
            );

            Planet viewTatooine = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(0, viewTatooine.Officers.Count);
            Assert.AreEqual(0, viewTatooine.Fleets.Count);
            Assert.AreEqual(0, viewTatooine.Regiments.Count);
        }

        [Test]
        public void BuildFactionView_UnexploredOuterRimAndCore_BothHiddenWithoutSnapshot()
        {
            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Assert.IsTrue(view.PlanetSystems.Any(s => s.InstanceID == "CORESYS"));
            Assert.IsTrue(view.PlanetSystems.Any(s => s.InstanceID == "OUTERRIM"));

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(0, viewCoruscant.Officers.Count);
        }

        [Test]
        public void CaptureSnapshot_PlanetWithAllEntities_CreatesAccurateSnapshot()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            Fleet imperialFleet = CreateFleet("FLEET1", _empire);
            CapitalShip destroyer = new CapitalShip
            {
                InstanceID = "SD1",
                OwnerInstanceID = _empire.InstanceID,
            };
            Regiment stormtroopers = CreateRegiment("REG1", _empire);
            Building starport = CreateBuilding("BLDG1", _empire);
            Starfighter tieFighter = CreateStarfighter("TIE1", _empire);

            _game.AttachNode(vader, _coruscant);
            _game.AttachNode(imperialFleet, _coruscant);
            _game.AttachNode(destroyer, imperialFleet);
            _game.AttachNode(stormtroopers, _coruscant);
            _coruscant.Buildings.Add(starport);
            _coruscant.Starfighters.Add(tieFighter);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            SystemSnapshot systemSnapshot = _alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
            Assert.AreEqual(1, snapshot.Fleets.Count);
            Assert.AreEqual(1, snapshot.Regiments.Count);
            Assert.AreEqual(1, snapshot.Buildings.Count);
            Assert.AreEqual(1, snapshot.Starfighters.Count);
            Assert.AreEqual("FNEMP1", snapshot.OwnerInstanceID);
        }

        [Test]
        public void CaptureSnapshot_DeepCopy_ModifyingGameDoesNotAffectSnapshot()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.SetSkillValue(MissionParticipantSkill.Diplomacy, 50);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            vader.SetSkillValue(MissionParticipantSkill.Diplomacy, 99);
            _coruscant.Officers.Remove(vader);

            SystemSnapshot systemSnapshot = _alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
            Assert.AreEqual(
                50,
                snapshot.Officers[0].GetSkillValue(MissionParticipantSkill.Diplomacy)
            );
        }

        [Test]
        public void CaptureSnapshot_EntityCopiedWithSameInstanceID()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            SystemSnapshot systemSnapshot = _alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual("VADER", snapshot.Officers[0].InstanceID);
            Assert.AreNotSame(vader, snapshot.Officers[0]);
        }

        [Test]
        public void CaptureSnapshot_EntityMoves_RemovedFromOldPlanetSnapshot()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            _tatooine.OwnerInstanceID = _empire.InstanceID; // Set owner so vader can move here
            _game.MoveNode(vader, _tatooine);

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 20);

            SystemSnapshot coreSnapshot = _alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(0, coruscantSnapshot.Officers.Count);

            SystemSnapshot outerSnapshot = _alliance.Fog.Snapshots["OUTERRIM"];
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

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            _tatooine.OwnerInstanceID = _empire.InstanceID; // Set owner so vader can move here
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 20);

            _hoth.OwnerInstanceID = _empire.InstanceID; // Set owner so fleet can move here
            _game.MoveNode(fleet, _hoth);
            _fogSystem.CaptureSnapshot(_alliance, _hoth, _outerRimSystem, 30);

            SystemSnapshot coreSnapshot = _alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(0, coruscantSnapshot.Officers.Count);
            Assert.AreEqual(0, coruscantSnapshot.Fleets.Count);
        }

        [Test]
        public void CaptureSnapshot_EntitySeenTwiceSamePlanet_DoesNotDuplicate()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 20);

            SystemSnapshot systemSnapshot = _alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
        }

        [Test]
        public void CaptureSnapshot_EntityMovesBackToOriginalPlanet_HandledCorrectly()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            _tatooine.OwnerInstanceID = _empire.InstanceID; // Set owner so vader can move here
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 20);

            _game.MoveNode(vader, _coruscant);
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 30);

            SystemSnapshot coreSnapshot = _alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, coruscantSnapshot.Officers.Count);

            SystemSnapshot outerSnapshot = _alliance.Fog.Snapshots["OUTERRIM"];
            PlanetSnapshot tatooineSnapshot = outerSnapshot.Planets["TATOOINE"];

            Assert.AreEqual(0, tatooineSnapshot.Officers.Count);
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

            bool visible = _fogSystem.IsPlanetVisible(_coruscant, _alliance);

            Assert.IsTrue(visible);
        }

        [Test]
        public void IsPlanetVisible_NoOwnershipNoFleet_ReturnsFalse()
        {
            bool visible = _fogSystem.IsPlanetVisible(_tatooine, _alliance);

            Assert.IsFalse(visible);
        }

        [Test]
        public void IsPlanetVisible_MultipleFleetsDifferentFactions_OnlyOwnFactionCounts()
        {
            Fleet empireFleet = CreateFleet("FLEET1", _empire);
            _game.AttachNode(empireFleet, _tatooine);

            bool allianceVisible = _fogSystem.IsPlanetVisible(_tatooine, _alliance);
            bool empireVisible = _fogSystem.IsPlanetVisible(_tatooine, _empire);

            Assert.IsFalse(allianceVisible);
            Assert.IsTrue(empireVisible);
        }

        [Test]
        public void BuildFactionView_VisiblePlanet_UsesLiveData()
        {
            Officer leia = CreateOfficer("LEIA", _alliance);
            _game.AttachNode(leia, _hoth);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(1, viewHoth.Officers.Count);
            Assert.AreEqual("LEIA", viewHoth.Officers[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_LivePlanet_ModifyingViewDoesNotAffectGame()
        {
            Officer leia = CreateOfficer("LEIA", _alliance);
            _game.AttachNode(leia, _hoth);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            viewHoth.Officers.Clear();

            Assert.AreEqual(1, _hoth.Officers.Count);
        }

        [Test]
        public void BuildFactionView_LivePlanet_BuildingsPreserved()
        {
            Building groundFacility = CreateBuilding("BLDG1", _alliance);
            Building orbitStation = CreateBuilding("BLDG2", _alliance);
            _hoth.Buildings.Add(groundFacility);
            _hoth.Buildings.Add(orbitStation);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(2, viewHoth.Buildings.Count);
        }

        [Test]
        public void BuildFactionView_NotVisibleWithSnapshot_UsesSnapshotData()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.Officers.Count);
            Assert.AreEqual("VADER", viewCoruscant.Officers[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_Snapshot_ModifyingViewDoesNotAffectSnapshot()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            viewCoruscant.Officers.Clear();

            SystemSnapshot systemSnapshot = _alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
        }

        [Test]
        public void BuildFactionView_SnapshotBuildings_Visible()
        {
            Building facility = CreateBuilding("BLDG1", _empire);
            _coruscant.Buildings.Add(facility);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.Buildings.Count);
        }

        [Test]
        public void BuildFactionView_CoreSystemSnapshot_PopularSupportIsVisible()
        {
            _coruscant.PopularSupport["FNALL1"] = 50;

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.IsNotNull(viewCoruscant.PopularSupport);
            Assert.IsNotEmpty(
                viewCoruscant.PopularSupport,
                "Core system popular support should always be visible"
            );
            Assert.AreEqual(
                50,
                viewCoruscant.PopularSupport["FNALL1"],
                "Popular support value should match live data"
            );
        }

        [Test]
        public void BuildFactionView_Snapshot_NumResourcesIsZero()
        {
            _coruscant.NumRawResourceNodes = 5;

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(0, viewCoruscant.NumRawResourceNodes);
        }

        [Test]
        public void BuildFactionView_FleetArrives_PlanetBecomesLive()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");
        }

        [Test]
        public void BuildFactionView_FleetLeaves_UsesSnapshot()
        {
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);

            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            _game.MoveNode(allianceFleet, _hoth);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.Officers.Count);
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
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            _hoth.OwnerInstanceID = _alliance.InstanceID;
            _game.MoveNode(fleetA, _hoth);

            Fleet fleetB = CreateFleet("FLEET_B", _alliance);
            _game.AttachNode(fleetB, _coruscant);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs_b", OwnerInstanceID = _alliance.InstanceID },
                fleetB
            );

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.Fleets.Count, "Only the live fleet should appear");
            Assert.AreEqual(
                "FLEET_B",
                viewCoruscant.Fleets[0].InstanceID,
                "Stale snapshot fleet must not bleed into live view"
            );
        }

        [Test]
        public void BuildFactionView_VaderMovesWithoutObservation_StaleIntelPersists()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            _tatooine.OwnerInstanceID = _empire.InstanceID; // Set owner so vader can move here
            _game.MoveNode(vader, _tatooine);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Planet viewTatooine = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(1, viewCoruscant.Officers.Count);
            Assert.AreEqual("VADER", viewCoruscant.Officers[0].InstanceID);

            Assert.AreEqual(0, viewTatooine.Officers.Count);
        }

        [Test]
        public void CaptureSnapshot_VaderRediscovered_RemovesFromOldPlanet()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            _tatooine.OwnerInstanceID = _empire.InstanceID; // Set owner so vader can move here
            _game.MoveNode(vader, _tatooine);

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 20);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Planet viewTatooine = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(0, viewCoruscant.Officers.Count);
            Assert.AreEqual(1, viewTatooine.Officers.Count);
        }

        [Test]
        public void BuildFactionView_PlanetWithNoEntities_HandledCorrectly()
        {
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewTatooine = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(0, viewTatooine.Officers.Count);
            Assert.AreEqual(0, viewTatooine.Fleets.Count);
        }

        [Test]
        public void BuildFactionView_SystemWithMultiplePlanets_MixedVisibilityHandledCorrectly()
        {
            _tatooine.OwnerInstanceID = _empire.InstanceID; // Set owner so vader can be attached here
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _tatooine);

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewTatooine = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "TATOOINE");

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(1, viewTatooine.Officers.Count);
        }

        [Test]
        public void CaptureSnapshot_EmptyPlanet_DoesNotCrash()
        {
            Assert.DoesNotThrow(() =>
            {
                _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 10);
            });

            SystemSnapshot systemSnapshot = _alliance.Fog.Snapshots["OUTERRIM"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["TATOOINE"];

            Assert.IsNotNull(snapshot);
        }

        [Test]
        public void BuildFactionView_NoSnapshotsAnywhere_AllPlanetsEmptyShells()
        {
            GalaxyMap view = _fogSystem.BuildFactionView(_empire);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(0, viewHoth.Officers.Count);
        }

        [Test]
        public void BuildFactionView_NoDuplicateEntitiesAcrossPlanets()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            _tatooine.OwnerInstanceID = _empire.InstanceID; // Set owner so vader can move here
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 20);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            List<Officer> allOfficers = view
                .PlanetSystems.SelectMany(s => s.Planets)
                .SelectMany(p => p.Officers)
                .ToList();

            int vaderCount = allOfficers.Count(o => o.InstanceID == "VADER");

            Assert.AreEqual(1, vaderCount);
        }

        [Test]
        public void CaptureSnapshot_EntityLastSeenIndexUpdatedCorrectly()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            Assert.AreEqual("CORUSCANT", _alliance.Fog.EntityLastSeenAt["VADER"]);

            _tatooine.OwnerInstanceID = _empire.InstanceID; // Set owner so vader can move here
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 20);

            Assert.AreEqual("TATOOINE", _alliance.Fog.EntityLastSeenAt["VADER"]);
        }

        [Test]
        public void CaptureSnapshot_PlanetToSystemMappingCorrect()
        {
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            Assert.AreEqual("CORESYS", _alliance.Fog.PlanetToSystem["CORUSCANT"]);

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 20);

            Assert.AreEqual("OUTERRIM", _alliance.Fog.PlanetToSystem["TATOOINE"]);
        }

        [Test]
        public void CaptureSnapshot_PlanetVisible_SnapshotNotOverwrittenWithoutExplicitCall()
        {
            Officer vader = CreateOfficer("VADER", _empire);
            vader.SetSkillValue(MissionParticipantSkill.Diplomacy, 50);
            _game.AttachNode(vader, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            SystemSnapshot systemSnapshot = _alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];
            int originalTickCaptured = snapshot.TickCaptured;

            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);

            vader.SetSkillValue(MissionParticipantSkill.Diplomacy, 99);

            Assert.AreEqual(
                originalTickCaptured,
                snapshot.TickCaptured,
                "Snapshot tick should not change"
            );
            Assert.AreEqual(
                50,
                snapshot.Officers[0].GetSkillValue(MissionParticipantSkill.Diplomacy),
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
            };
            _game.AttachNode(vader, _coruscant);
            _game.AttachNode(tarkin, _coruscant);
            _game.AttachNode(fleet, _coruscant);
            _game.AttachNode(destroyer, fleet);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            _tatooine.OwnerInstanceID = _empire.InstanceID;
            _game.MoveNode(vader, _tatooine);
            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 20);

            SystemSnapshot coreSnapshot = _alliance.Fog.Snapshots["CORESYS"];
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

            SystemSnapshot outerSnapshot = _alliance.Fog.Snapshots["OUTERRIM"];
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
        public void BuildFactionView_EntitiesPreserveInstanceIDInView()
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

            PlanetSystem viewSystem = view.PlanetSystems.FirstOrDefault(s =>
                s.InstanceID == "CORESYS"
            );
            Assert.IsNotNull(viewSystem, "CORESYS should exist in view");

            Planet viewPlanet = viewSystem.Planets.FirstOrDefault(p => p.InstanceID == "CORUSCANT");
            Assert.IsNotNull(viewPlanet, "CORUSCANT should exist in view");

            Assert.AreEqual(
                "VADER",
                viewPlanet.Officers[0].InstanceID,
                "Officer InstanceID should be preserved"
            );
            Assert.AreEqual(
                "DEATHSTAR",
                viewPlanet.Fleets[0].InstanceID,
                "Fleet InstanceID should be preserved"
            );
            Assert.AreEqual(
                "STORMTROOPERS",
                viewPlanet.Regiments[0].InstanceID,
                "Regiment InstanceID should be preserved"
            );
        }

        [Test]
        public void BuildFactionView_CapturedFriendlyOfficer_VisibleOnLivePlanet()
        {
            // Leia is captured on Coruscant. Alliance sends a fleet (real-time visibility).
            // Captured friendly officers are always live data — must appear regardless.
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);

            Officer leia = CreateOfficer("LEIA", _alliance);
            leia.IsCaptured = true;
            _game.AttachNode(leia, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.IsTrue(
                viewCoruscant.Officers.Any(o => o.InstanceID == "LEIA"),
                "Captured friendly officer must appear as live data on a visible planet"
            );
        }

        [Test]
        public void BuildFactionView_CapturedFriendlyOfficer_VisibleOnSnapshotPlanet()
        {
            // Leia is captured on Coruscant. Alliance has a snapshot but no current visibility.
            // Captured friendly officers are always live data — must appear even via snapshot path.
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            Officer leia = CreateOfficer("LEIA", _alliance);
            leia.IsCaptured = true;
            _game.AttachNode(leia, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.IsTrue(
                viewCoruscant.Officers.Any(o => o.InstanceID == "LEIA"),
                "Captured friendly officer must appear as live data even when planet is only known via snapshot"
            );
        }

        [Test]
        public void BuildFactionView_CapturedFriendlyOfficer_VisibleOnUnexploredPlanet()
        {
            // Leia is captured on Coruscant. Alliance has never observed the planet.
            // Captured friendly officers are always live data — must appear even on unexplored planets.
            Officer leia = CreateOfficer("LEIA", _alliance);
            leia.IsCaptured = true;
            _game.AttachNode(leia, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.IsTrue(
                viewCoruscant.Officers.Any(o => o.InstanceID == "LEIA"),
                "Captured friendly officer must appear as live data even on a completely unexplored planet"
            );
        }

        [Test]
        public void CaptureSnapshot_CapturedFriendlyOfficer_NotIncludedInSnapshot()
        {
            // Leia is captured on Coruscant. Snapshots must not include captured friendly officers
            // because they are always surfaced as live data — snapshotting them would be redundant
            // and could produce stale copies that conflict with live position.
            Officer leia = CreateOfficer("LEIA", _alliance);
            leia.IsCaptured = true;
            _game.AttachNode(leia, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            PlanetSnapshot snapshot = _alliance.Fog.Snapshots["CORESYS"].Planets["CORUSCANT"];

            Assert.IsFalse(
                snapshot.Officers.Any(o => o.InstanceID == "LEIA"),
                "Captured friendly officer must not be included in snapshots — they are live data"
            );
        }

        private Officer CreateOfficer(string id, Faction faction) =>
            EntityFactory.CreateOfficer(id, faction.InstanceID);

        private Fleet CreateFleet(string id, Faction faction) =>
            EntityFactory.CreateFleet(id, faction.InstanceID);

        private Regiment CreateRegiment(string id, Faction faction) =>
            EntityFactory.CreateRegiment(id, faction.InstanceID);

        private Building CreateBuilding(string id, Faction faction) =>
            EntityFactory.CreateBuilding(id, faction.InstanceID);

        private Starfighter CreateStarfighter(string id, Faction faction) =>
            EntityFactory.CreateStarfighter(id, faction.InstanceID);

        private StubMission CreateMission(string id, Faction owner, Planet target) =>
            EntityFactory.CreateMission(id, owner.InstanceID, target.InstanceID);

        // --- Partial visibility: missions ---

        [Test]
        public void BuildFactionView_OwnPlanet_EnemyMissionsNotVisible()
        {
            // Empire owns coruscant; alliance runs a mission there.
            // Empire's view should not expose the alliance mission.
            Mission allianceMission = CreateMission("M1", _alliance, _coruscant);
            _game.AttachNode(allianceMission, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_empire);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                0,
                viewCoruscant.Missions.Count,
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

            GalaxyMap view = _fogSystem.BuildFactionView(_empire);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.Missions.Count,
                "Own missions should be visible on your own planet"
            );
        }

        [Test]
        public void BuildFactionView_LivePlanet_EnemyMissionsNeverVisible()
        {
            // Alliance takes a snapshot of coruscant (e.g. via espionage) then gets live intel.
            // Enemy missions must never be visible — not from snapshot, not from live sight.
            Officer vader = CreateOfficer("VADER", _empire);
            _game.AttachNode(vader, _coruscant);

            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.Officers.Count,
                "Live officer (Vader) should be visible"
            );
            Assert.AreEqual(
                0,
                viewCoruscant.Missions.Count,
                "Enemy missions must never be visible"
            );
        }

        [Test]
        public void BuildFactionView_FleetAtEnemyPlanet_EnemyMissionsStillHidden()
        {
            // Alliance fleet sits at coruscant (empire planet).
            // Alliance should see units (live) but NOT empire missions running there.
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);

            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                0,
                viewCoruscant.Missions.Count,
                "Enemy missions should remain hidden even when a friendly fleet is present"
            );
        }

        // --- Partial visibility: enemy units visible when you have live intel ---

        [Test]
        public void BuildFactionView_FleetAtEnemyPlanet_EnemyOfficerVisible()
        {
            // Alliance fleet orbits coruscant (empire's planet) → live view for _alliance.
            // Empire officer is stationed there (valid — same owner as planet).
            // Alliance live view should include the enemy officer.
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);

            Officer tarkin = CreateOfficer("PALPATINE", _empire);
            _game.AttachNode(tarkin, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.Officers.Count,
                "Enemy officer should be visible when you have live intel on the planet"
            );
            Assert.AreEqual("PALPATINE", viewCoruscant.Officers[0].InstanceID);
        }

        // --- Snapshot does not reveal post-snapshot changes ---

        [Test]
        public void BuildFactionView_SnapshotPlanet_EntityAddedAfterSnapshot_NotVisible()
        {
            // Snapshot coruscant; then add a new officer after the snapshot is taken.
            // The new officer must NOT appear in the view.
            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            Officer lateArrival = CreateOfficer("MOFF1", _empire);
            _game.AttachNode(lateArrival, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                0,
                viewCoruscant.Officers.Count,
                "Officer added after snapshot should not appear in the view"
            );
        }

        // --- Live planet + snapshot: own stale data ignored, enemy snapshot data surfaced ---

        [Test]
        public void BuildFactionView_LivePlanet_StaleOwnSnapshotUnits_NotVisible()
        {
            // Snapshot hoth while alliance has a fleet there.
            // Fleet moves away — now stale in the snapshot.
            // Live view should show only what is actually on hoth, not the stale snapshot fleet.
            Fleet staleFleet = CreateFleet("FLEET_STALE", _alliance);
            _game.AttachNode(staleFleet, _hoth);
            _fogSystem.CaptureSnapshot(_alliance, _hoth, _outerRimSystem, 10);
            _game.MoveNode(staleFleet, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(0, viewHoth.Fleets.Count, "Stale own-faction fleet should not appear");
        }

        [Test]
        public void BuildFactionView_PlanetCapturedFromEnemy_LiveOwnData_PlusSnapshotEnemyFleet()
        {
            // Coruscant was empire's. Alliance took a snapshot when empire owned it —
            // capturing an empire fleet. Alliance then takes ownership.
            // View should show live own data alongside the snapshot enemy fleet.
            // Enemy missions are never visible regardless of snapshot.
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", _empire);
            CapitalShip destroyer = new CapitalShip
            {
                InstanceID = "SD1",
                OwnerInstanceID = _empire.InstanceID,
            };
            _game.AttachNode(empireFleet, _coruscant);
            _game.AttachNode(destroyer, empireFleet);
            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            // Alliance takes ownership — empire units depart.
            _coruscant.OwnerInstanceID = _alliance.InstanceID;
            _game.MoveNode(empireFleet, _hoth);
            _game.DetachNode(empireMission);

            // Alliance officer now stationed on the captured planet.
            Officer leia = CreateOfficer("LEIA", _alliance);
            _game.AttachNode(leia, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.Officers.Count, "Live alliance officer should appear");
            Assert.AreEqual("LEIA", viewCoruscant.Officers[0].InstanceID);
            Assert.AreEqual(1, viewCoruscant.Fleets.Count, "Snapshot empire fleet should appear");
            Assert.AreEqual("EMPIRE_FLEET", viewCoruscant.Fleets[0].InstanceID);
            Assert.AreEqual(
                0,
                viewCoruscant.Missions.Count,
                "Enemy missions must never be visible"
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

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                1,
                viewCoruscant.Missions.Count,
                "Own mission on enemy planet should be visible without a snapshot or fleet"
            );
            Assert.AreEqual("M1", viewCoruscant.Missions[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_OwnMission_OnNeutralPlanet_VisibleWithoutSnapshotOrFleet()
        {
            // Alliance runs a mission on tatooine (neutral, uncolonized).
            // No fleet, no snapshot.
            Mission allianceMission = CreateMission("M1", _alliance, _tatooine);
            _game.AttachNode(allianceMission, _tatooine);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewTatooine = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(
                1,
                viewTatooine.Missions.Count,
                "Own mission on neutral planet should be visible without a snapshot or fleet"
            );
            Assert.AreEqual("M1", viewTatooine.Missions[0].InstanceID);
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

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.Fleets.Count, "Own fleet should be visible");
            Assert.AreEqual("FLEET1", viewCoruscant.Fleets[0].InstanceID);
        }

        // --- Blockade visibility ---

        [Test]
        public void BuildFactionView_BlockadedOwnPlanet_StationaryEnemyFleet_IsVisible()
        {
            // Alliance owns Hoth; empire fleet is sitting at Hoth (not in transit).
            // Alliance should see the enemy fleet in their live view.
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", _empire);
            _game.AttachNode(empireFleet, _hoth);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs1", OwnerInstanceID = _empire.InstanceID },
                empireFleet
            );

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(1, viewHoth.Fleets.Count, "Stationary enemy fleet should be visible");
            Assert.AreEqual("EMPIRE_FLEET", viewHoth.Fleets[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_BlockadedOwnPlanet_EnemyFleetInTransit_NotVisible()
        {
            // Alliance owns Hoth; empire fleet is en route to Hoth (in transit, Movement != null).
            // Fleet is parented to Hoth because RequestMove reparents immediately,
            // but it has not yet arrived. Alliance should NOT see it.
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", _empire);
            _game.AttachNode(empireFleet, _hoth);
            empireFleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 5 };

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(
                0,
                viewHoth.Fleets.Count,
                "In-transit enemy fleet should not appear in the view"
            );
        }

        [Test]
        public void IsPlanetVisible_OwnFleetInTransit_GrantsVisibility()
        {
            // Alliance fleet is en route to Coruscant. Even though it hasn't arrived,
            // you dispatched it and know it's heading there — it grants live visibility.
            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);
            allianceFleet.Movement = new MovementState { TransitTicks = 10, TicksElapsed = 3 };

            bool visible = _fogSystem.IsPlanetVisible(_coruscant, _alliance);

            Assert.IsTrue(
                visible,
                "An in-transit own fleet should still grant visibility of the destination"
            );
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

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(1, viewHoth.Fleets.Count, "Own in-transit fleet should be visible");
            Assert.AreEqual("FLEET1", viewHoth.Fleets[0].InstanceID);
        }

        // --- Snapshot fleet de-duplication ---

        [Test]
        public void BuildFactionView_LivePlanet_OrbingEnemyFleet_NotDuplicatedFromSnapshot()
        {
            // Empire fleet orbits Hoth (alliance-owned). Alliance takes a snapshot capturing it.
            // Fleet remains orbiting — still present live.
            // The fleet should appear exactly once in the faction view, not twice.
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", _empire);
            _game.AttachNode(empireFleet, _hoth);
            _game.AttachNode(
                new CapitalShip { InstanceID = "cs1", OwnerInstanceID = _empire.InstanceID },
                empireFleet
            );

            _fogSystem.CaptureSnapshot(_alliance, _hoth, _outerRimSystem, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(
                1,
                viewHoth.Fleets.Count,
                "Orbiting enemy fleet already visible live should not be duplicated from snapshot"
            );
        }

        [Test]
        public void BuildFactionView_LivePlanet_SnapshotEnemyMission_NeverSurfaced()
        {
            // Even if a snapshot captured an enemy mission, and the planet is later live,
            // enemy missions must never appear in the faction view.
            Mission empireMission = CreateMission("M1", _empire, _coruscant);
            _game.AttachNode(empireMission, _coruscant);

            _fogSystem.CaptureSnapshot(_alliance, _coruscant, _coreSystem, 10);

            Fleet allianceFleet = CreateFleet("FLEET1", _alliance);
            _game.AttachNode(allianceFleet, _coruscant);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(
                0,
                viewCoruscant.Missions.Count,
                "Enemy missions must never be surfaced"
            );
        }

        // --- Outer rim: support hidden in snapshots ---

        [Test]
        public void BuildFactionView_OuterRimSnapshot_PopularSupportHidden()
        {
            // Outer rim planet: popular support is NOT universally visible.
            // Only core system support is always shown.
            _tatooine.OwnerInstanceID = _empire.InstanceID;
            _tatooine.PopularSupport["FNALL1"] = 40;

            _fogSystem.CaptureSnapshot(_alliance, _tatooine, _outerRimSystem, 10);

            GalaxyMap view = _fogSystem.BuildFactionView(_alliance);

            Planet viewTatooine = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "TATOOINE");

            Assert.IsEmpty(
                viewTatooine.PopularSupport,
                "Popular support on outer rim snapshots should be hidden"
            );
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

            _fogSystem.CaptureSnapshot(_empire, _coruscant, _coreSystem, _game.CurrentTick);

            GalaxyMap view = _fogSystem.BuildFactionView(_empire);
            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.IsFalse(
                viewCoruscant.Fleets.Any(f => f.InstanceID == "empty_fleet"),
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

            _fogSystem.CaptureSnapshot(_empire, _coruscant, _coreSystem, _game.CurrentTick);

            GalaxyMap view = _fogSystem.BuildFactionView(_empire);
            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.IsTrue(
                viewCoruscant.Fleets.Any(f => f.InstanceID == "armed_fleet"),
                "Fleet with capital ships should appear in snapshot"
            );
        }
    }
}
