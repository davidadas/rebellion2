using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class ResearchSystemTests
    {
        private GameRoot game;
        private Faction faction;
        private Planet planet;
        private ResearchSystem system;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = TestConfig.Create();
            game = new GameRoot(config);

            faction = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            game.Factions.Add(faction);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(sys, game.Galaxy);

            planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
                EnergyCapacity = 20,
            };
            game.AttachNode(planet, sys);

            system = new ResearchSystem(game);
        }

        private Building CreateShipyard(string id)
        {
            return new Building
            {
                InstanceID = id,
                OwnerInstanceID = "FNALL1",
                BuildingType = BuildingType.Shipyard,

                ProductionType = ManufacturingType.Ship,
                ProcessRate = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private void SetupShipResearchQueue(params (string name, int order, int difficulty)[] techs)
        {
            IManufacturable[] templates = techs
                .Select(t =>
                    (IManufacturable)
                        new CapitalShip
                        {
                            DisplayName = t.name,
                            ResearchOrder = t.order,
                            ResearchDifficulty = t.difficulty,
                            AllowedOwnerInstanceIDs = new List<string> { "FNALL1" },
                        }
                )
                .ToArray();
            faction.RebuildResearchQueues(templates);
        }

        // --- Capacity refresh from core-system facilities ---

        [Test]
        public void ProcessTick_PulseNotReached_DoesNotAddCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            game.AttachNode(shipyard, planet);

            system.ProcessTick();

            Assert.AreEqual(0, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
        }

        [Test]
        public void ProcessTick_TenthTickCoreSystemShipyard_AddsCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            game.AttachNode(shipyard, planet);

            game.CurrentTick = 10;

            int before = faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign);
            system.ProcessTick();
            int after = faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign);

            Assert.AreEqual(1, after - before, "One core-system shipyard should add 1 capacity");
        }

        [Test]
        public void ProcessTick_MultipleCoreSystemFacilities_AddsAll()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);
            game.AttachNode(CreateShipyard("SY2"), planet);
            game.AttachNode(CreateShipyard("SY3"), planet);
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(3, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
        }

        [Test]
        public void ProcessTick_BusyFacility_StillAddsCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            game.AttachNode(shipyard, planet);

            // Put something in the manufacturing queue to make the facility busy
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "SHIP1",
                OwnerInstanceID = "FNALL1",
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            planet.ManufacturingQueue[ManufacturingType.Ship] = new List<IManufacturable> { ship };
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(
                1,
                faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "Completed facilities should contribute even when production is queued"
            );
        }

        [Test]
        public void ProcessTick_FacilityUnderConstruction_DoesNotAddCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            shipyard.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(shipyard, planet);
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(
                0,
                faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "Facility under construction should not contribute to research capacity"
            );
        }

        [Test]
        public void ProcessTick_FacilityInTransit_DoesNotAddCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            shipyard.Movement = new MovementState();
            game.AttachNode(shipyard, planet);
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(
                0,
                faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "Facility in transit should not contribute to research capacity"
            );
        }

        [Test]
        public void ProcessTick_NoFacilities_NoCapacity()
        {
            game.CurrentTick = 10;
            system.ProcessTick();

            Assert.AreEqual(0, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(
                0,
                faction.GetResearchCapacityRemaining(ResearchDiscipline.FacilityDesign)
            );
            Assert.AreEqual(
                0,
                faction.GetResearchCapacityRemaining(ResearchDiscipline.TroopTraining)
            );
        }

        [Test]
        public void ProcessTick_OuterRimFacility_DoesNotAddCapacity()
        {
            PlanetSystem outerRimSystem = new PlanetSystem
            {
                InstanceID = "sys-outer",
                SystemType = PlanetSystemType.OuterRim,
            };
            game.AttachNode(outerRimSystem, game.Galaxy);

            Planet outerRimPlanet = new Planet
            {
                InstanceID = "p-outer",
                OwnerInstanceID = "FNALL1",
                IsColonized = true,
                EnergyCapacity = 20,
            };
            game.AttachNode(outerRimPlanet, outerRimSystem);
            game.AttachNode(CreateShipyard("SY-OUTER"), outerRimPlanet);
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(0, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
        }

        // --- Per-unit sequential unlocking ---

        [Test]
        public void ProcessTick_CapacityMeetsDifficulty_UnlocksUnit()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            faction.SetResearchCapacityRemaining(ResearchDiscipline.ShipDesign, 12);
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(1, faction.GetHighestUnlockedOrder(ManufacturingType.Ship));
            Assert.AreEqual(
                0,
                faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "Cost should be subtracted from capacity"
            );
        }

        [Test]
        public void ProcessTick_CapacityMeetsDifficulty_EmitsOrderAdvance()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            faction.SetResearchCapacityRemaining(ResearchDiscipline.ShipDesign, 12);
            game.CurrentTick = 10;

            List<GameResult> results = system.ProcessTick();

            ResearchOrderedResult result = results.OfType<ResearchOrderedResult>().FirstOrDefault();
            Assert.IsNotNull(result, "Should emit a ResearchOrderedResult");
            Assert.AreEqual(faction.InstanceID, result.Faction.InstanceID);
            Assert.AreEqual(ManufacturingType.Ship, result.FacilityType);
            Assert.AreEqual(1, result.ResearchOrder);
            Assert.AreEqual(0, results.OfType<TechnologyUnlockedResult>().Count());
        }

        [Test]
        public void ProcessTick_ExcessCapacity_CarriesOverAndUnlocksSingleAdvance()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12), ("Cruiser", 2, 24));
            faction.SetResearchCapacityRemaining(ResearchDiscipline.ShipDesign, 40);
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(1, faction.GetHighestUnlockedOrder(ManufacturingType.Ship));
            Assert.AreEqual(
                28,
                faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign),
                "One pulse should advance only one order and carry the remainder"
            );
        }

        [Test]
        public void ProcessTick_CapacityBelowDifficulty_NoUnlock()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            faction.SetResearchCapacityRemaining(ResearchDiscipline.ShipDesign, 5);
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(0, faction.GetHighestUnlockedOrder(ManufacturingType.Ship));
            Assert.AreEqual(5, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
        }

        [Test]
        public void ProcessTick_AllUnlocked_NoFurtherAdvancement()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            faction.SetHighestUnlockedOrder(ManufacturingType.Ship, 1);
            faction.SetResearchCapacityRemaining(ResearchDiscipline.ShipDesign, 9999);
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(
                1,
                faction.GetHighestUnlockedOrder(ManufacturingType.Ship),
                "Should not advance beyond last technology"
            );
        }

        [Test]
        public void ProcessTick_CoreSystemFacilityAcrossMultiplePulses_AccumulatesCapacity()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            game.AttachNode(CreateShipyard("SY1"), planet);

            for (int i = 1; i <= 120; i++)
            {
                game.CurrentTick = i;
                system.ProcessTick();
            }

            Assert.AreEqual(
                1,
                faction.GetHighestUnlockedOrder(ManufacturingType.Ship),
                "Twelve 10-tick pulses of +1 should reach Frigate's difficulty of 12"
            );
        }

        // --- Multi-faction isolation ---

        [Test]
        public void ProcessTick_MultipleFactions_IndependentCapacity()
        {
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.Factions.Add(empire);

            PlanetSystem sys2 = new PlanetSystem { InstanceID = "sys2" };
            game.AttachNode(sys2, game.Galaxy);
            Planet empirePlanet = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "FNEMP1",
                IsColonized = true,
                EnergyCapacity = 20,
            };
            game.AttachNode(empirePlanet, sys2);

            // Alliance gets 1 shipyard, Empire gets 3
            game.AttachNode(CreateShipyard("SY1"), planet);

            Building empSy1 = CreateShipyard("ESY1");
            empSy1.OwnerInstanceID = "FNEMP1";
            Building empSy2 = CreateShipyard("ESY2");
            empSy2.OwnerInstanceID = "FNEMP1";
            Building empSy3 = CreateShipyard("ESY3");
            empSy3.OwnerInstanceID = "FNEMP1";
            game.AttachNode(empSy1, empirePlanet);
            game.AttachNode(empSy2, empirePlanet);
            game.AttachNode(empSy3, empirePlanet);
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(1, faction.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
            Assert.AreEqual(3, empire.GetResearchCapacityRemaining(ResearchDiscipline.ShipDesign));
        }

        // --- Technology unlocking with real templates ---

        private IManufacturable[] LoadTemplates()
        {
            return ResourceManager
                .GetGameData<Building>()
                .Cast<IManufacturable>()
                .Concat(ResourceManager.GetGameData<CapitalShip>())
                .Concat(ResourceManager.GetGameData<Starfighter>())
                .Concat(ResourceManager.GetGameData<Regiment>())
                .ToArray();
        }

        [Test]
        public void ProcessTick_ResearchTargetReached_UnlocksNewTechnologies(
            [Values(ManufacturingType.Ship, ManufacturingType.Building, ManufacturingType.Troop)]
                ManufacturingType type
        )
        {
            IManufacturable[] templates = LoadTemplates();
            faction.RebuildResearchQueues(templates);

            // Start at order 0
            faction.SetHighestUnlockedOrder(type, 0);
            List<Technology> techsAtOrder0 = faction.GetUnlockedTechnologies(type);

            // Find the first technology with order > 0
            Technology target = faction.GetCurrentResearchTarget(type);
            if (target == null)
                Assert.Ignore($"No researchable {type} technologies for {faction.InstanceID}");

            // Set capacity to meet its difficulty
            faction.SetResearchCapacityRemaining(
                Faction.ToResearchDiscipline(type),
                target.GetResearchDifficulty()
            );
            game.CurrentTick = 10;

            system.ProcessTick();

            Assert.AreEqual(target.GetResearchOrder(), faction.GetHighestUnlockedOrder(type));

            List<Technology> techsAfter = faction.GetUnlockedTechnologies(type);
            Assert.Greater(
                techsAfter.Count,
                techsAtOrder0.Count,
                $"Unlocking {type} technology should increase available technologies"
            );
        }
    }
}
