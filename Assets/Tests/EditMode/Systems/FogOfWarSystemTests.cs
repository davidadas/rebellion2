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
            Assert.IsFalse(viewTatooine.IsLive);
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
            Regiment stormtroopers = CreateRegiment("REG1", empire);
            Building starport = CreateBuilding("BLDG1", empire);
            Starfighter tieFighter = CreateStarfighter("TIE1", empire);

            game.AttachNode(vader, coruscant);
            game.AttachNode(imperialFleet, coruscant);
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

            Assert.IsTrue(viewHoth.IsLive);
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

            Assert.IsFalse(viewCoruscant.IsLive);
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
        public void BuildFactionView_Snapshot_PopularSupportIsEmpty()
        {
            coruscant.PopularSupport["FNALL1"] = 50;

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            GalaxyMap view = fogSystem.BuildFactionView(alliance);

            Planet viewCoruscant = view
                .PlanetSystems.First(s => s.InstanceID == "CORESYS")
                .Planets.First(p => p.InstanceID == "CORUSCANT");

            Assert.IsNotNull(viewCoruscant.PopularSupport);
            Assert.IsEmpty(viewCoruscant.PopularSupport);
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

            Assert.IsTrue(viewCoruscant.IsLive);
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

            Assert.IsFalse(viewCoruscant.IsLive);
            Assert.AreEqual(1, viewCoruscant.Officers.Count);
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

            Assert.IsFalse(viewTatooine.IsLive);
            Assert.AreEqual(1, viewTatooine.Officers.Count);

            Assert.IsTrue(viewHoth.IsLive);
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

            Assert.IsFalse(viewHoth.IsLive);
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
            Officer tarkin = CreateOfficer("TARKIN", empire);
            Fleet fleet = CreateFleet("FLEET1", empire);
            game.AttachNode(vader, coruscant);
            game.AttachNode(tarkin, coruscant);
            game.AttachNode(fleet, coruscant);

            fogSystem.CaptureSnapshot(alliance, coruscant, coreSystem, 10);

            tatooine.OwnerInstanceID = empire.InstanceID;
            game.MoveNode(vader, tatooine);
            fogSystem.CaptureSnapshot(alliance, tatooine, outerRimSystem, 20);

            SystemSnapshot coreSnapshot = alliance.Fog.Snapshots["CORESYS"];
            PlanetSnapshot coruscantSnapshot = coreSnapshot.Planets["CORUSCANT"];

            Assert.AreEqual(1, coruscantSnapshot.Officers.Count, "Should have 1 officer (Tarkin)");
            Assert.AreEqual(
                "TARKIN",
                coruscantSnapshot.Officers[0].InstanceID,
                "Tarkin should remain"
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
            Regiment regiment = CreateRegiment("STORMTROOPERS", empire);
            game.AttachNode(vader, coruscant);
            game.AttachNode(fleet, coruscant);
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

        private Officer CreateOfficer(string id, Faction faction)
        {
            return new Officer
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = faction.InstanceID,
                Skills = new Dictionary<MissionParticipantSkill, int>
                {
                    { MissionParticipantSkill.Diplomacy, 50 },
                    { MissionParticipantSkill.Espionage, 50 },
                    { MissionParticipantSkill.Combat, 50 },
                    { MissionParticipantSkill.Leadership, 50 },
                },
            };
        }

        private Fleet CreateFleet(string id, Faction faction)
        {
            return new Fleet
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = faction.InstanceID,
            };
        }

        private Regiment CreateRegiment(string id, Faction faction)
        {
            return new Regiment
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = faction.InstanceID,
            };
        }

        private Building CreateBuilding(string id, Faction faction)
        {
            return new Building
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = faction.InstanceID,
            };
        }

        private Starfighter CreateStarfighter(string id, Faction faction)
        {
            return new Starfighter
            {
                InstanceID = id,
                DisplayName = id,
                OwnerInstanceID = faction.InstanceID,
            };
        }
    }
}
