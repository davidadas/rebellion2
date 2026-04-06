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

        private Building CreateTrainingFacility(string id)
        {
            return new Building
            {
                InstanceID = id,
                OwnerInstanceID = "FNALL1",
                BuildingType = BuildingType.TrainingFacility,

                ProductionType = ManufacturingType.Troop,
                ProcessRate = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private Officer CreateResearchOfficer(
            string id,
            int shipSkill = 0,
            int troopSkill = 0,
            int facilitySkill = 0
        )
        {
            return new Officer
            {
                InstanceID = id,
                OwnerInstanceID = "FNALL1",
                ShipResearch = shipSkill,
                TroopResearch = troopSkill,
                FacilityResearch = facilitySkill,
            };
        }

        // --- Phase 1: Passive capacity from idle facilities ---

        [Test]
        public void ProcessTick_IdleShipyard_AddsCapacity()
        {
            Building shipyard = CreateShipyard("SY1");
            game.AttachNode(shipyard, planet);

            int before = faction.ResearchCapacity[ManufacturingType.Ship];
            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));
            int after = faction.ResearchCapacity[ManufacturingType.Ship];

            Assert.AreEqual(1, after - before, "One idle shipyard should add 1 capacity per tick");
        }

        [Test]
        public void ProcessTick_MultipleIdleFacilities_AddsAll()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);
            game.AttachNode(CreateShipyard("SY2"), planet);
            game.AttachNode(CreateShipyard("SY3"), planet);

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.99 }));

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

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));

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

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));

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

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(
                0,
                faction.ResearchCapacity[ManufacturingType.Ship],
                "Facility in transit should not contribute to research capacity"
            );
        }

        [Test]
        public void ProcessTick_NoFacilities_NoCapacity()
        {
            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(0, faction.ResearchCapacity[ManufacturingType.Ship]);
            Assert.AreEqual(0, faction.ResearchCapacity[ManufacturingType.Building]);
            Assert.AreEqual(0, faction.ResearchCapacity[ManufacturingType.Troop]);
        }

        // --- Phase 2: Officer research contributions ---

        [Test]
        public void ProcessTick_OfficerSuccessfulRoll_AddsPoints()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);
            Officer officer = CreateResearchOfficer("OFF1", shipSkill: 100); // 100 = always succeeds
            game.AttachNode(officer, planet);

            // NextInt(1,101) needs to return <= 100 for success, NextInt(0,11) for dice roll
            // FixedRandomProvider: NextInt uses NextDouble * (max-min) + min
            // For roll: NextDouble * 100 + 1 <= 100 → any value works with skill 100
            // For dice: NextDouble * 11 + 0 → we want a known bonus
            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.0, 0.0 }));

            // With skill 100: roll = 0*100+1 = 1 <= 100 → success
            // Points = BaseResearchPoints(5) + dice(0*11+0 = 0) = 5
            // Plus 1 from idle facility = 6 total
            Assert.AreEqual(6, faction.ResearchCapacity[ManufacturingType.Ship]);
        }

        [Test]
        public void ProcessTick_OfficerFailedRoll_NoPoints()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);
            Officer officer = CreateResearchOfficer("OFF1", shipSkill: 10);
            game.AttachNode(officer, planet);

            // Roll: 0.99 * 100 + 1 = 100 > 10 → fail
            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.99 }));

            // Only the idle facility capacity (1), no officer contribution
            Assert.AreEqual(1, faction.ResearchCapacity[ManufacturingType.Ship]);
        }

        [Test]
        public void ProcessTick_OfficerSuccess_SkillIncrements()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);
            Officer officer = CreateResearchOfficer("OFF1", shipSkill: 100);
            game.AttachNode(officer, planet);

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.0, 0.0 }));

            Assert.AreEqual(
                101,
                officer.ShipResearch,
                "Research skill should increment by 1 on success"
            );
        }

        [Test]
        public void ProcessTick_OfficerOnMission_DoesNotContribute()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);
            Officer officer = CreateResearchOfficer("OFF1", shipSkill: 100);

            // Put officer on a mission instead of directly on the planet
            StubMission mission = new StubMission();
            officer.SetParent(mission);

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.0 }));

            // Only idle facility capacity, no officer contribution
            Assert.AreEqual(1, faction.ResearchCapacity[ManufacturingType.Ship]);
        }

        [Test]
        public void ProcessTick_OfficerInTransit_DoesNotContribute()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);
            Officer officer = CreateResearchOfficer("OFF1", shipSkill: 100);
            officer.Movement = new MovementState();
            game.AttachNode(officer, planet);

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.0 }));

            Assert.AreEqual(1, faction.ResearchCapacity[ManufacturingType.Ship]);
        }

        [Test]
        public void ProcessTick_OfficerNoMatchingFacility_DoesNotContribute()
        {
            // Troop facility on planet, but officer has ship research skill
            game.AttachNode(CreateTrainingFacility("TF1"), planet);
            Officer officer = CreateResearchOfficer("OFF1", shipSkill: 100);
            game.AttachNode(officer, planet);

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.0 }));

            Assert.AreEqual(
                0,
                faction.ResearchCapacity[ManufacturingType.Ship],
                "No ship facility means no ship research from officer"
            );
        }

        [Test]
        public void ProcessTick_OfficerZeroSkill_DoesNotRoll()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);
            Officer officer = CreateResearchOfficer("OFF1", shipSkill: 0);
            game.AttachNode(officer, planet);

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.0 }));

            // Only idle facility, officer with 0 skill is skipped
            Assert.AreEqual(1, faction.ResearchCapacity[ManufacturingType.Ship]);
        }

        // --- Phase 3: Level advancement ---

        [Test]
        public void ProcessTick_CapacityMeetsThreshold_AdvancesLevel()
        {
            // Level 0 → 1 costs 100 capacity
            faction.ResearchCapacity[ManufacturingType.Ship] = 100;

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(1, faction.GetResearchLevel(ManufacturingType.Ship));
            Assert.AreEqual(
                0,
                faction.ResearchCapacity[ManufacturingType.Ship],
                "Cost should be subtracted from capacity"
            );
        }

        [Test]
        public void ProcessTick_CapacityMeetsThreshold_EmitsResult()
        {
            faction.ResearchCapacity[ManufacturingType.Ship] = 100;

            List<GameResult> results = system.ProcessTick(
                game,
                new FixedRandomProvider(new[] { 0.5 })
            );

            ResearchLevelAdvancedResult result = results
                .OfType<ResearchLevelAdvancedResult>()
                .FirstOrDefault();
            Assert.IsNotNull(result, "Should emit a ResearchLevelAdvancedResult");
            Assert.AreEqual(faction.InstanceID, result.FactionInstanceID);
            Assert.AreEqual(ManufacturingType.Ship, result.ResearchType);
            Assert.AreEqual(1, result.NewLevel);
        }

        [Test]
        public void ProcessTick_CapacityExceedsThreshold_KeepsRemainder()
        {
            faction.ResearchCapacity[ManufacturingType.Ship] = 130;

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(1, faction.GetResearchLevel(ManufacturingType.Ship));
            Assert.AreEqual(
                30,
                faction.ResearchCapacity[ManufacturingType.Ship],
                "Remainder should carry over after level advancement"
            );
        }

        [Test]
        public void ProcessTick_CapacityBelowThreshold_NoAdvancement()
        {
            faction.ResearchCapacity[ManufacturingType.Ship] = 50;

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(0, faction.GetResearchLevel(ManufacturingType.Ship));
            Assert.AreEqual(50, faction.ResearchCapacity[ManufacturingType.Ship]);
        }

        [Test]
        public void ProcessTick_MaxLevel_NoFurtherAdvancement()
        {
            // Default config has levels 1-5, set to max
            faction.SetResearchLevel(ManufacturingType.Ship, 5);
            faction.ResearchCapacity[ManufacturingType.Ship] = 9999;

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(
                5,
                faction.GetResearchLevel(ManufacturingType.Ship),
                "Should not advance beyond max configured level"
            );
        }

        [Test]
        public void ProcessTick_AccumulatesAcrossMultipleTicks()
        {
            game.AttachNode(CreateShipyard("SY1"), planet);

            // Run 100 ticks with just the idle facility (+1 per tick = 100 total → level 1)
            for (int i = 0; i < 100; i++)
            {
                system.ProcessTick(game, new FixedRandomProvider(new[] { 0.99 }));
            }

            Assert.AreEqual(
                1,
                faction.GetResearchLevel(ManufacturingType.Ship),
                "100 ticks of +1 should reach level 1 threshold of 100"
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

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.99 }));

            Assert.AreEqual(1, faction.ResearchCapacity[ManufacturingType.Ship]);
            Assert.AreEqual(3, empire.ResearchCapacity[ManufacturingType.Ship]);
        }

        // --- Technology unlocking after level advancement ---

        private IManufacturable[] LoadTemplates()
        {
            IResourceManager resourceManager = ResourceManager.Instance;
            return resourceManager
                .GetGameData<Building>()
                .Cast<IManufacturable>()
                .Concat(resourceManager.GetGameData<CapitalShip>())
                .Concat(resourceManager.GetGameData<Starfighter>())
                .Concat(resourceManager.GetGameData<Regiment>())
                .ToArray();
        }

        [Test]
        public void ProcessTick_LevelAdvancement_UnlocksNewTechnologies(
            [Values(ManufacturingType.Ship, ManufacturingType.Building, ManufacturingType.Troop)]
                ManufacturingType type
        )
        {
            IManufacturable[] templates = LoadTemplates();
            faction.RebuildTechnologyLevels(templates);

            // Start at level 0
            faction.SetResearchLevel(type, 0);
            List<Technology> techsAtLevel0 = faction.GetResearchedTechnologies(type);

            // Verify there are level 1 techs to unlock
            bool hasLevel1Techs = templates.Any(t =>
                t.GetManufacturingType() == type
                && t.GetRequiredResearchLevel() == 1
                && t.AllowedOwnerInstanceIDs.Contains(faction.InstanceID)
            );
            if (!hasLevel1Techs)
                Assert.Ignore($"No level 1 {type} technologies for {faction.InstanceID}");

            // Set capacity to meet threshold, advance via ProcessTick
            GameConfig config = game.GetConfig();
            int cost = config.Research.LevelCosts[1];
            faction.ResearchCapacity[type] = cost;

            system.ProcessTick(game, new FixedRandomProvider(new[] { 0.5 }));

            Assert.AreEqual(1, faction.GetResearchLevel(type));

            List<Technology> techsAtLevel1 = faction.GetResearchedTechnologies(type);
            Assert.Greater(
                techsAtLevel1.Count,
                techsAtLevel0.Count,
                $"Advancing {type} research to level 1 should unlock additional technologies"
            );
        }
    }
}
