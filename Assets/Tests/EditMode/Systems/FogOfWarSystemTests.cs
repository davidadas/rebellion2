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
        private GameRoot game;
        private FogOfWarSystem fogSystem;
        private Faction alliance;
        private Faction empire;
        private PlanetSystem coreSystem;
        private PlanetSystem outerRimSystem;
        private Planet coruscant;
        private Planet tatooine;
        private Planet hoth;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();
            game = new GameRoot(config);
            fogSystem = new FogOfWarSystem(game);

            alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.Factions.Add(alliance);
            game.Factions.Add(empire);

            coreSystem = new PlanetSystem
            {
                InstanceID = "CORESYS",
                DisplayName = "Coruscant System",
                SystemType = PlanetSystemType.CoreSystem,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(coreSystem, game.GetGalaxyMap());

            outerRimSystem = new PlanetSystem
            {
                InstanceID = "OUTERRIM",
                DisplayName = "Outer Rim System",
                SystemType = PlanetSystemType.OuterRim,
                PositionX = 100,
                PositionY = 100,
            };
            game.AttachNode(outerRimSystem, game.GetGalaxyMap());

            coruscant = new Planet
            {
                InstanceID = "CORUSCANT",
                DisplayName = "Coruscant",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
            };
            game.AttachNode(coruscant, coreSystem);

            tatooine = new Planet
            {
                InstanceID = "TATOOINE",
                DisplayName = "Tatooine",
                OwnerInstanceID = null,
            };
            game.AttachNode(tatooine, outerRimSystem);

            hoth = new Planet
            {
                InstanceID = "HOTH",
                DisplayName = "Hoth",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
            };
            game.AttachNode(hoth, outerRimSystem);
        }

        [Test]
        public void BuildFactionView_UnexploredPlanet_EmptyShell()
        {
            Assert.AreEqual(
                2,
                game.Galaxy.PlanetSystems.Count,
                "Setup should have added 2 systems to galaxy"
            );
            GalaxyMap view = fogSystem.BuildFactionView(alliance);
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
            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Officer vader = CreateOfficer("VADER", empire);
            Fleet imperialFleet = CreateFleet("FLEET1", empire);
            CapitalShip destroyer = new CapitalShip { InstanceID = "SD1", OwnerInstanceID = empire.InstanceID };
            Regiment stormtroopers = CreateRegiment("REG1", empire);
            Building starport = CreateBuilding("BLDG1", empire);
            Starfighter tieFighter = CreateStarfighter("TIE1", empire);

            game.AttachNode(vader, coruscant);
            game.AttachNode(imperialFleet, coruscant);
            game.AttachNode(destroyer, imperialFleet);
            game.AttachNode(stormtroopers, coruscant);
            coruscant.Buildings[BuildingSlot.Ground].Add(starport);
            coruscant.Starfighters.Add(tieFighter);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            SystemSnapshot systemSnapshot = alliance.Fog.Snapshots["CORESYS"];
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
            Officer vader = CreateOfficer("VADER", empire);
            vader.SetSkillValue(MissionParticipantSkill.Diplomacy, 50);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            vader.SetSkillValue(MissionParticipantSkill.Diplomacy, 99);
            coruscant.Officers.Remove(vader);

            SystemSnapshot systemSnapshot = alliance.Fog.Snapshots["CORESYS"];
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
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            SystemSnapshot systemSnapshot = alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual("VADER", snapshot.Officers[0].InstanceID);
            Assert.AreNotSame(vader, snapshot.Officers[0]);
        }

        [Test]
        public void CaptureSnapshot_EntityMoves_RemovedFromOldPlanetSnapshot()
        {
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            tatooine.OwnerInstanceID = empire.InstanceID; // Set owner so vader can move here
            game.MoveNode(vader, tatooine);

            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 20);

            SystemSnapshot coreSnapshot = alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(0, coruscantSnapshot.Officers.Count);

            SystemSnapshot outerSnapshot = alliance.Fog.Snapshots["OUTERRIM"];
            PlanetSnapshot tatooineSnapshot = outerSnapshot.Planets["TATOOINE"];

            Assert.AreEqual(1, tatooineSnapshot.Officers.Count);
            Assert.AreEqual("VADER", tatooineSnapshot.Officers[0].InstanceID);
        }

        [Test]
        public void CaptureSnapshot_MultipleEntitiesMove_InvalidationIndependentPerEntity()
        {
            Officer vader = CreateOfficer("VADER", empire);
            Fleet fleet = CreateFleet("FLEET1", empire);
            game.AttachNode(vader, coruscant);
            game.AttachNode(fleet, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            tatooine.OwnerInstanceID = empire.InstanceID; // Set owner so vader can move here
            game.MoveNode(vader, tatooine);
            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 20);

            hoth.OwnerInstanceID = empire.InstanceID; // Set owner so fleet can move here
            game.MoveNode(fleet, hoth);
            fogSystem.CaptureSnapshot(alliance, hoth, outerRimSystem, 30);

            SystemSnapshot coreSnapshot = alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(0, coruscantSnapshot.Officers.Count);
            Assert.AreEqual(0, coruscantSnapshot.Fleets.Count);
        }

        [Test]
        public void CaptureSnapshot_EntitySeenTwiceSamePlanet_DoesNotDuplicate()
        {
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);
            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 20);

            SystemSnapshot systemSnapshot = alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
        }

        [Test]
        public void CaptureSnapshot_EntityMovesBackToOriginalPlanet_HandledCorrectly()
        {
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            tatooine.OwnerInstanceID = empire.InstanceID; // Set owner so vader can move here
            game.MoveNode(vader, tatooine);
            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 20);

            game.MoveNode(vader, coruscant);
            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 30);

            SystemSnapshot coreSnapshot = alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, coruscantSnapshot.Officers.Count);

            SystemSnapshot outerSnapshot = alliance.Fog.Snapshots["OUTERRIM"];
            PlanetSnapshot tatooineSnapshot = outerSnapshot.Planets["TATOOINE"];

            Assert.AreEqual(0, tatooineSnapshot.Officers.Count);
        }

        [Test]
        public void IsPlanetVisible_OwnedPlanet_ReturnsTrue()
        {
            bool visible = fogSystem.IsPlanetVisible(hoth, alliance);

            Assert.IsTrue(visible);
        }

        [Test]
        public void IsPlanetVisible_FleetPresent_ReturnsTrue()
        {
            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);

            bool visible = fogSystem.IsPlanetVisible(coruscant, alliance);

            Assert.IsTrue(visible);
        }

        [Test]
        public void IsPlanetVisible_NoOwnershipNoFleet_ReturnsFalse()
        {
            bool visible = fogSystem.IsPlanetVisible(tatooine, alliance);

            Assert.IsFalse(visible);
        }

        [Test]
        public void IsPlanetVisible_MultipleFleetsDifferentFactions_OnlyOwnFactionCounts()
        {
            Fleet empireFleet = CreateFleet("FLEET1", empire);
            game.AttachNode(empireFleet, tatooine);

            bool allianceVisible = fogSystem.IsPlanetVisible(tatooine, alliance);
            bool empireVisible = fogSystem.IsPlanetVisible(tatooine, empire);

            Assert.IsFalse(allianceVisible);
            Assert.IsTrue(empireVisible);
        }

        [Test]
        public void BuildFactionView_VisiblePlanet_UsesLiveData()
        {
            Officer leia = CreateOfficer("LEIA", alliance);
            game.AttachNode(leia, hoth);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(1, viewHoth.Officers.Count);
            Assert.AreEqual("LEIA", viewHoth.Officers[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_LivePlanet_ModifyingViewDoesNotAffectGame()
        {
            Officer leia = CreateOfficer("LEIA", alliance);
            game.AttachNode(leia, hoth);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            viewHoth.Officers.Clear();

            Assert.AreEqual(1, hoth.Officers.Count);
        }

        [Test]
        public void BuildFactionView_LivePlanet_BuildingsPreserveSlotStructure()
        {
            Building groundFacility = CreateBuilding("BLDG1", alliance);
            Building orbitStation = CreateBuilding("BLDG2", alliance);
            hoth.Buildings[BuildingSlot.Ground].Add(groundFacility);
            hoth.Buildings[BuildingSlot.Orbit].Add(orbitStation);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(1, viewHoth.Buildings[BuildingSlot.Ground].Count);
            Assert.AreEqual(1, viewHoth.Buildings[BuildingSlot.Orbit].Count);
        }

        [Test]
        public void BuildFactionView_NotVisibleWithSnapshot_UsesSnapshotData()
        {
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.Officers.Count);
            Assert.AreEqual("VADER", viewCoruscant.Officers[0].InstanceID);
        }

        [Test]
        public void BuildFactionView_Snapshot_ModifyingViewDoesNotAffectSnapshot()
        {
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            viewCoruscant.Officers.Clear();

            SystemSnapshot systemSnapshot = alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, snapshot.Officers.Count);
        }

        [Test]
        public void BuildFactionView_SnapshotBuildings_MappedToGroundSlot()
        {
            Building facility = CreateBuilding("BLDG1", empire);
            coruscant.Buildings[BuildingSlot.Orbit].Add(facility);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(1, viewCoruscant.Buildings[BuildingSlot.Ground].Count);
        }

        [Test]
        public void BuildFactionView_CoreSystemSnapshot_PopularSupportIsVisible()
        {
            coruscant.PopularSupport["FNALL1"] = 50;

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            coruscant.NumRawResourceNodes = 5;

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.AreEqual(0, viewCoruscant.NumRawResourceNodes);
        }

        [Test]
        public void BuildFactionView_FleetArrives_PlanetBecomesLive()
        {
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");
        }

        [Test]
        public void BuildFactionView_FleetLeaves_UsesSnapshot()
        {
            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);

            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            game.MoveNode(allianceFleet, hoth);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet fleetA = CreateFleet("FLEET_A", alliance);
            game.AttachNode(fleetA, coruscant);
            game.AttachNode(new CapitalShip { InstanceID = "cs_a", OwnerInstanceID = alliance.InstanceID }, fleetA);
            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            hoth.OwnerInstanceID = alliance.InstanceID;
            game.MoveNode(fleetA, hoth);

            Fleet fleetB = CreateFleet("FLEET_B", alliance);
            game.AttachNode(fleetB, coruscant);
            game.AttachNode(new CapitalShip { InstanceID = "cs_b", OwnerInstanceID = alliance.InstanceID }, fleetB);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            tatooine.OwnerInstanceID = empire.InstanceID; // Set owner so vader can move here
            game.MoveNode(vader, tatooine);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            tatooine.OwnerInstanceID = empire.InstanceID; // Set owner so vader can move here
            game.MoveNode(vader, tatooine);

            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 20);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewTatooine = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "TATOOINE");

            Assert.AreEqual(0, viewTatooine.Officers.Count);
            Assert.AreEqual(0, viewTatooine.Fleets.Count);
        }

        [Test]
        public void BuildFactionView_SystemWithMultiplePlanets_MixedVisibilityHandledCorrectly()
        {
            tatooine.OwnerInstanceID = empire.InstanceID; // Set owner so vader can be attached here
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, tatooine);

            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
                fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 10);
            });

            SystemSnapshot systemSnapshot = alliance.Fog.Snapshots["OUTERRIM"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["TATOOINE"];

            Assert.IsNotNull(snapshot);
        }

        [Test]
        public void BuildFactionView_NoSnapshotsAnywhere_AllPlanetsEmptyShells()
        {
            GalaxyMap view = fogSystem.BuildFactionView(empire);

            Planet viewHoth = view
                .PlanetSystems.First(s => s.InstanceID == "OUTERRIM")
                .Planets.First(p => p.InstanceID == "HOTH");

            Assert.AreEqual(0, viewHoth.Officers.Count);
        }

        [Test]
        public void BuildFactionView_NoDuplicateEntitiesAcrossPlanets()
        {
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            tatooine.OwnerInstanceID = empire.InstanceID; // Set owner so vader can move here
            game.MoveNode(vader, tatooine);
            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 20);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            Assert.AreEqual("CORUSCANT", alliance.Fog.EntityLastSeenAt["VADER"]);

            tatooine.OwnerInstanceID = empire.InstanceID; // Set owner so vader can move here
            game.MoveNode(vader, tatooine);
            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 20);

            Assert.AreEqual("TATOOINE", alliance.Fog.EntityLastSeenAt["VADER"]);
        }

        [Test]
        public void CaptureSnapshot_PlanetToSystemMappingCorrect()
        {
            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            Assert.AreEqual("CORESYS", alliance.Fog.PlanetToSystem["CORUSCANT"]);

            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 20);

            Assert.AreEqual("OUTERRIM", alliance.Fog.PlanetToSystem["TATOOINE"]);
        }

        [Test]
        public void CaptureSnapshot_PlanetVisible_SnapshotNotOverwrittenWithoutExplicitCall()
        {
            Officer vader = CreateOfficer("VADER", empire);
            vader.SetSkillValue(MissionParticipantSkill.Diplomacy, 50);
            game.AttachNode(vader, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            SystemSnapshot systemSnapshot = alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot snapshot = systemSnapshot.Planets["CORUSCANT"];
            int originalTickCaptured = snapshot.TickCaptured;

            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);

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
            Officer vader = CreateOfficer("VADER", empire);
            Officer tarkin = CreateOfficer("PALPATINE", empire);
            Fleet fleet = CreateFleet("FLEET1", empire);
            CapitalShip destroyer = new CapitalShip { InstanceID = "SD1", OwnerInstanceID = empire.InstanceID };
            game.AttachNode(vader, coruscant);
            game.AttachNode(tarkin, coruscant);
            game.AttachNode(fleet, coruscant);
            game.AttachNode(destroyer, fleet);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            tatooine.OwnerInstanceID = empire.InstanceID;
            game.MoveNode(vader, tatooine);
            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 20);

            SystemSnapshot coreSnapshot = alliance.Fog.Snapshots["CORESYS"];
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

            SystemSnapshot outerSnapshot = alliance.Fog.Snapshots["OUTERRIM"];
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
            Officer vader = CreateOfficer("VADER", empire);
            Fleet fleet = CreateFleet("DEATHSTAR", empire);
            CapitalShip executor = new CapitalShip { InstanceID = "EX1", OwnerInstanceID = empire.InstanceID };
            Regiment regiment = CreateRegiment("STORMTROOPERS", empire);
            game.AttachNode(vader, coruscant);
            game.AttachNode(fleet, coruscant);
            game.AttachNode(executor, fleet);
            game.AttachNode(regiment, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(empire);

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
            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);

            Officer leia = CreateOfficer("LEIA", alliance);
            leia.IsCaptured = true;
            game.AttachNode(leia, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);
            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            Officer leia = CreateOfficer("LEIA", alliance);
            leia.IsCaptured = true;
            game.AttachNode(leia, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Officer leia = CreateOfficer("LEIA", alliance);
            leia.IsCaptured = true;
            game.AttachNode(leia, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Officer leia = CreateOfficer("LEIA", alliance);
            leia.IsCaptured = true;
            game.AttachNode(leia, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            PlanetSnapshot snapshot = alliance.Fog.Snapshots["CORESYS"].Planets["CORUSCANT"];

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
            Mission allianceMission = CreateMission("M1", alliance, coruscant);
            game.AttachNode(allianceMission, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(empire);

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
            coruscant.PopularSupport["FNALL1"] = 50;
            Mission empireMission = CreateMission("M1", empire, coruscant);
            game.AttachNode(empireMission, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(empire);

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
            Officer vader = CreateOfficer("VADER", empire);
            game.AttachNode(vader, coruscant);

            Mission empireMission = CreateMission("M1", empire, coruscant);
            game.AttachNode(empireMission, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);

            Mission empireMission = CreateMission("M1", empire, coruscant);
            game.AttachNode(empireMission, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            // Alliance fleet orbits coruscant (empire's planet) → live view for alliance.
            // Empire officer is stationed there (valid — same owner as planet).
            // Alliance live view should include the enemy officer.
            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);

            Officer tarkin = CreateOfficer("PALPATINE", empire);
            game.AttachNode(tarkin, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            Officer lateArrival = CreateOfficer("MOFF1", empire);
            game.AttachNode(lateArrival, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet staleFleet = CreateFleet("FLEET_STALE", alliance);
            game.AttachNode(staleFleet, hoth);
            fogSystem.CaptureSnapshot(alliance, hoth, outerRimSystem, 10);
            game.MoveNode(staleFleet, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", empire);
            CapitalShip destroyer = new CapitalShip { InstanceID = "SD1", OwnerInstanceID = empire.InstanceID };
            game.AttachNode(empireFleet, coruscant);
            game.AttachNode(destroyer, empireFleet);
            Mission empireMission = CreateMission("M1", empire, coruscant);
            game.AttachNode(empireMission, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            // Alliance takes ownership — empire units depart.
            coruscant.OwnerInstanceID = alliance.InstanceID;
            game.MoveNode(empireFleet, hoth);
            game.DetachNode(empireMission);

            // Alliance officer now stationed on the captured planet.
            Officer leia = CreateOfficer("LEIA", alliance);
            game.AttachNode(leia, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Mission allianceMission = CreateMission("M1", alliance, coruscant);
            game.AttachNode(allianceMission, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Mission allianceMission = CreateMission("M1", alliance, tatooine);
            game.AttachNode(allianceMission, tatooine);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);
            game.AttachNode(new CapitalShip { InstanceID = "cs1", OwnerInstanceID = alliance.InstanceID }, allianceFleet);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", empire);
            game.AttachNode(empireFleet, hoth);
            game.AttachNode(new CapitalShip { InstanceID = "cs1", OwnerInstanceID = empire.InstanceID }, empireFleet);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", empire);
            game.AttachNode(empireFleet, hoth);
            empireFleet.Movement = new MovementState
            {
                DestinationInstanceID = "HOTH",
                TransitTicks = 10,
                TicksElapsed = 5,
            };

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);
            allianceFleet.Movement = new MovementState
            {
                DestinationInstanceID = "CORUSCANT",
                TransitTicks = 10,
                TicksElapsed = 3,
            };

            bool visible = fogSystem.IsPlanetVisible(coruscant, alliance);

            Assert.IsTrue(
                visible,
                "An in-transit own fleet should still grant visibility of the destination"
            );
        }

        [Test]
        public void BuildFactionView_OwnFleetInTransit_IsVisible()
        {
            // Alliance fleet is in transit to Hoth (alliance-owned). You should see your own fleet.
            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, hoth);
            game.AttachNode(new CapitalShip { InstanceID = "cs1", OwnerInstanceID = alliance.InstanceID }, allianceFleet);
            allianceFleet.Movement = new MovementState
            {
                DestinationInstanceID = "HOTH",
                TransitTicks = 10,
                TicksElapsed = 4,
            };

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet empireFleet = CreateFleet("EMPIRE_FLEET", empire);
            game.AttachNode(empireFleet, hoth);
            game.AttachNode(new CapitalShip { InstanceID = "cs1", OwnerInstanceID = empire.InstanceID }, empireFleet);

            fogSystem.CaptureSnapshot(alliance, hoth, outerRimSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Mission empireMission = CreateMission("M1", empire, coruscant);
            game.AttachNode(empireMission, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            Fleet allianceFleet = CreateFleet("FLEET1", alliance);
            game.AttachNode(allianceFleet, coruscant);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            tatooine.OwnerInstanceID = empire.InstanceID;
            tatooine.PopularSupport["FNALL1"] = 40;

            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

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
            Fleet emptyFleet = new Fleet { InstanceID = "empty_fleet", OwnerInstanceID = empire.InstanceID };
            game.AttachNode(emptyFleet, coruscant);

            fogSystem.CaptureSnapshot(empire, coruscant, coreSystem, game.CurrentTick);

            GalaxyMap view = fogSystem.BuildFactionView(empire);
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
            Fleet fleet = new Fleet { InstanceID = "armed_fleet", OwnerInstanceID = empire.InstanceID };
            game.AttachNode(fleet, coruscant);

            CapitalShip ship = new CapitalShip { InstanceID = "cs1", OwnerInstanceID = empire.InstanceID };
            game.AttachNode(ship, fleet);

            fogSystem.CaptureSnapshot(empire, coruscant, coreSystem, game.CurrentTick);

            GalaxyMap view = fogSystem.BuildFactionView(empire);
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
