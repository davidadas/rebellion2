using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Systems;

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

            system = new ResearchSystem();
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

        // --- Passive capacity from idle facilities ---

        [Test]
        public void ProcessTick_IdleShipyard_AddsCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            game.AttachNode(shipyard, planet);

            int before = faction.ResearchCapacity[ManufacturingType.Ship];
            system.ProcessTick(game);
            int after = faction.ResearchCapacity[ManufacturingType.Ship];

            Assert.AreEqual(1, after - before, "One idle shipyard should add 1 capacity per tick");
        }

        [Test]
        public void ProcessTick_MultipleIdleFacilities_AddsAll()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);
            game.AttachNode(CreateShipyard("SY2"), planet);
            game.AttachNode(CreateShipyard("SY3"), planet);

            system.ProcessTick(game);

            Assert.AreEqual(3, faction.ResearchCapacity[ManufacturingType.Ship]);
        }

        [Test]
        public void ProcessTick_BusyFacility_DoesNotAddCapacity()
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

            system.ProcessTick(game);

            Assert.AreEqual(
                0,
                faction.ResearchCapacity[ManufacturingType.Ship],
                "Busy facility should not contribute to research capacity"
            );
        }

        [Test]
        public void ProcessTick_FacilityUnderConstruction_DoesNotAddCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            shipyard.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(shipyard, planet);

            system.ProcessTick(game);

            Assert.AreEqual(
                0,
                faction.ResearchCapacity[ManufacturingType.Ship],
                "Facility under construction should not contribute to research capacity"
            );
        }

        [Test]
        public void ProcessTick_FacilityInTransit_DoesNotAddCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            shipyard.Movement = new MovementState();
            game.AttachNode(shipyard, planet);

            system.ProcessTick(game);

            Assert.AreEqual(
                0,
                faction.ResearchCapacity[ManufacturingType.Ship],
                "Facility in transit should not contribute to research capacity"
            );
        }

        [Test]
        public void ProcessTick_NoFacilities_NoCapacity()
        {
            system.ProcessTick(game);

            Assert.AreEqual(0, faction.ResearchCapacity[ManufacturingType.Ship]);
            Assert.AreEqual(0, faction.ResearchCapacity[ManufacturingType.Building]);
            Assert.AreEqual(0, faction.ResearchCapacity[ManufacturingType.Troop]);
        }

        // --- Per-unit sequential unlocking ---

        [Test]
        public void ProcessTick_CapacityMeetsDifficulty_UnlocksUnit()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            faction.ResearchCapacity[ManufacturingType.Ship] = 12;

            system.ProcessTick(game);

            Assert.AreEqual(1, faction.GetHighestUnlockedOrder(ManufacturingType.Ship));
            Assert.AreEqual(
                0,
                faction.ResearchCapacity[ManufacturingType.Ship],
                "Cost should be subtracted from capacity"
            );
        }

        [Test]
        public void ProcessTick_CapacityMeetsDifficulty_EmitsResult()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            faction.ResearchCapacity[ManufacturingType.Ship] = 12;

            List<GameResult> results = system.ProcessTick(game);

            TechnologyUnlockedResult result = results
                .OfType<TechnologyUnlockedResult>()
                .FirstOrDefault();
            Assert.IsNotNull(result, "Should emit a TechnologyUnlockedResult");
            Assert.AreEqual(faction.InstanceID, result.Faction.InstanceID);
            Assert.AreEqual(ManufacturingType.Ship, result.ResearchType);
            Assert.AreEqual("Frigate", result.TechnologyName);
            Assert.AreEqual(1, result.ResearchOrder);
        }

        [Test]
        public void ProcessTick_ExcessCapacity_CarriesOverAndUnlocksMultiple()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12), ("Cruiser", 2, 24));
            // Enough to unlock both: 12 + 24 = 36
            faction.ResearchCapacity[ManufacturingType.Ship] = 40;

            system.ProcessTick(game);

            Assert.AreEqual(2, faction.GetHighestUnlockedOrder(ManufacturingType.Ship));
            Assert.AreEqual(
                4,
                faction.ResearchCapacity[ManufacturingType.Ship],
                "Remainder should carry over after unlocking"
            );
        }

        [Test]
        public void ProcessTick_CapacityBelowDifficulty_NoUnlock()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            faction.ResearchCapacity[ManufacturingType.Ship] = 5;

            system.ProcessTick(game);

            Assert.AreEqual(0, faction.GetHighestUnlockedOrder(ManufacturingType.Ship));
            Assert.AreEqual(5, faction.ResearchCapacity[ManufacturingType.Ship]);
        }

        [Test]
        public void ProcessTick_AllUnlocked_NoFurtherAdvancement()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            faction.SetHighestUnlockedOrder(ManufacturingType.Ship, 1);
            faction.ResearchCapacity[ManufacturingType.Ship] = 9999;

            system.ProcessTick(game);

            Assert.AreEqual(
                1,
                faction.GetHighestUnlockedOrder(ManufacturingType.Ship),
                "Should not advance beyond last technology"
            );
        }

        [Test]
        public void ProcessTick_IdleFacilityAcrossMultipleTicks_AccumulatesCapacity()
        {
            SetupShipResearchQueue(("Dreadnaught", 0, 0), ("Frigate", 1, 12));
            game.AttachNode(CreateShipyard("SY1"), planet);

            // Run 12 ticks with just the idle facility (+1 per tick = 12 total → unlock Frigate)
            for (int i = 0; i < 12; i++)
            {
                system.ProcessTick(game);
            }

            Assert.AreEqual(
                1,
                faction.GetHighestUnlockedOrder(ManufacturingType.Ship),
                "12 ticks of +1 should reach Frigate's difficulty of 12"
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

            system.ProcessTick(game);

            Assert.AreEqual(1, faction.ResearchCapacity[ManufacturingType.Ship]);
            Assert.AreEqual(3, empire.ResearchCapacity[ManufacturingType.Ship]);
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
        public void ProcessTick_UnlockUnit_UnlocksNewTechnologies(
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
            faction.ResearchCapacity[type] = target.GetResearchDifficulty();

            system.ProcessTick(game);

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
