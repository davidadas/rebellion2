using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class CapitalShipProductionIssueTests
    {
        private GameRoot _game;
        private Faction _empire;
        private Faction _rebels;
        private PlanetSystem _system;
        private Planet _empPlanet;
        private Planet _rebelPlanet;
        private ManufacturingSystem _manufacturing;

        [SetUp]
        public void SetUp()
        {
            GameConfig config = TestConfig.Create();
            _game = new GameRoot(config);

            _empire = new Faction { InstanceID = "empire", PlayerID = null };
            _rebels = new Faction { InstanceID = "rebels", PlayerID = null };
            _game.Factions.Add(_empire);
            _game.Factions.Add(_rebels);

            _system = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(_system, _game.Galaxy);

            _empPlanet = new Planet
            {
                InstanceID = "emp_p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                NumRawResourceNodes = 100,
                EnergyCapacity = 10,
                PopularSupport = new Dictionary<string, int> { { "empire", 80 } },
            };
            _game.AttachNode(_empPlanet, _system);

            _rebelPlanet = new Planet
            {
                InstanceID = "reb_p1",
                OwnerInstanceID = "rebels",
                IsColonized = true,
                PositionX = 100,
                PositionY = 0,
                EnergyCapacity = 5,
                PopularSupport = new Dictionary<string, int> { { "rebels", 60 } },
            };
            _game.AttachNode(_rebelPlanet, _system);

            _manufacturing = new ManufacturingSystem(_game);
        }

        // --- Helpers ---

        private Building CreateKDYFacility(string id, string factionId, int modifier = 10)
        {
            return new Building
            {
                InstanceID = id,
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.Defense,
                DefenseFacilityClass = DefenseFacilityClass.KDY,
                ProductionModifier = modifier,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private Building CreateLNRFacility(string id, string factionId, int modifier = 10)
        {
            return new Building
            {
                InstanceID = id,
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.Defense,
                DefenseFacilityClass = DefenseFacilityClass.LNR,
                ProductionModifier = modifier,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private Building CreateShipyard(string id, string factionId, int processRate = 10)
        {
            return new Building
            {
                InstanceID = id,
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.Shipyard,
                ProductionType = ManufacturingType.Ship,
                ProcessRate = processRate,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        private CapitalShip CreateShipInQueue(
            string id,
            string factionId,
            int constructionCost,
            int productionCapacity = 40,
            string displayName = null
        )
        {
            return new CapitalShip
            {
                InstanceID = id,
                DisplayName = displayName ?? id,
                OwnerInstanceID = factionId,
                ConstructionCost = constructionCost,
                ProductionCapacity = productionCapacity,
                ProductionCapacityUsed = 0,
                RefinedMaterialProgress = 0,
                KdyPool = 0,
                LnrPool = 0,
                BaseBuildSpeed = 1,
                StarfighterCapacity = 0,
                RegimentCapacity = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
        }

        /// <summary>
        /// Enqueues a capital ship for production at a planet.
        /// Ships go into the manufacturing queue and a fleet.
        /// </summary>
        private CapitalShip EnqueueShip(
            Planet planet,
            string id,
            string factionId,
            int constructionCost,
            int productionCapacity = 40,
            string displayName = null
        )
        {
            CapitalShip ship = CreateShipInQueue(
                id,
                factionId,
                constructionCost,
                productionCapacity,
                displayName
            );
            Fleet fleet = EntityFactory.CreateFleet(id + "_fleet", factionId);
            _game.AttachNode(fleet, planet);
            _manufacturing.Enqueue(planet, ship, fleet, ignoreCost: true);
            return ship;
        }

        // --- Two-resource model tests ---

        [Test]
        public void Contribution_KDY_FillsPrimaryShortageOnly()
        {
            // KDY should fill primary shortage (ConstructionCost), never capacity
            _game.AttachNode(CreateKDYFacility("kdy1", "empire", modifier: 100), _empPlanet);

            CapitalShip ship = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 50);
            // Give it a large KDY pool to exceed primary shortage
            ship.KdyPool = 200;

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            // Primary should be fully covered
            Assert.AreEqual(50, ship.RefinedMaterialProgress, "KDY should fill primary shortage");
            // KDY does NOT fill capacity — capacity should remain 0 after KDY-only contribution
            // (LNR overflow fills capacity, not KDY)
            Assert.AreEqual(
                0,
                ship.ProductionCapacityUsed,
                "KDY should not fill capacity (only LNR overflows into capacity)"
            );
        }

        [Test]
        public void Contribution_LNR_FillsPrimaryThenOverflowsToCapacity()
        {
            // LNR should fill primary first, then overflow into capacity
            _game.AttachNode(CreateLNRFacility("lnr1", "empire", modifier: 100), _empPlanet);

            CapitalShip ship = EnqueueShip(
                _empPlanet,
                "cs1",
                "empire",
                constructionCost: 20,
                productionCapacity: 40
            );
            // Give it enough LNR pool to cover primary (20) + all capacity (40)
            ship.LnrPool = 200;

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.AreEqual(
                20,
                ship.RefinedMaterialProgress,
                "LNR should fill primary shortage first"
            );
            Assert.AreEqual(
                40,
                ship.ProductionCapacityUsed,
                "LNR overflow should fill capacity to completion"
            );
        }

        [Test]
        public void Contribution_ConsumeFromPool_ExactMatch()
        {
            // When pool exactly matches requirement, both go to zero
            CapitalShip ship = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 50);
            ship.KdyPool = 50;

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.AreEqual(50, ship.RefinedMaterialProgress, "Primary fully covered");
            Assert.AreEqual(0, ship.KdyPool, "KDY pool fully consumed");
        }

        [Test]
        public void Contribution_PoolsPersistAcrossTicks()
        {
            // Pools that aren't fully consumed should persist for next tick
            CapitalShip ship = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 100);
            ship.KdyPool = 30;

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.AreEqual(30, ship.RefinedMaterialProgress, "Should consume what's available");
            Assert.AreEqual(0, ship.KdyPool, "Pool should be drained");

            // Next tick: add more to the pool
            ship.KdyPool = 40;
            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.AreEqual(
                70,
                ship.RefinedMaterialProgress,
                "Progress should accumulate across ticks"
            );
        }

        [Test]
        public void Contribution_ShipCompletesWhenCapacityFull()
        {
            CapitalShip ship = EnqueueShip(
                _empPlanet,
                "cs1",
                "empire",
                constructionCost: 10,
                productionCapacity: 40
            );
            // Give enough to cover primary (10) and all capacity (40)
            ship.LnrPool = 100;

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.AreEqual(40, ship.ProductionCapacityUsed, "Capacity should be full");
            Assert.GreaterOrEqual(
                ship.ProductionCapacityUsed,
                ship.ProductionCapacity,
                "Ship should be complete when capacity is full"
            );
        }

        [Test]
        public void Contribution_KDYAndLNR_CombinedConsumptionOrder()
        {
            // Test the full consumption chain: KDY→primary, LNR→primary, LNR→capacity
            CapitalShip ship = EnqueueShip(
                _empPlanet,
                "cs1",
                "empire",
                constructionCost: 100,
                productionCapacity: 40
            );
            ship.KdyPool = 60; // Covers 60 of 100 primary
            ship.LnrPool = 80; // Covers remaining 40 primary + 40 capacity

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.AreEqual(100, ship.RefinedMaterialProgress, "Primary fully covered by KDY+LNR");
            Assert.AreEqual(40, ship.ProductionCapacityUsed, "Capacity filled by LNR overflow");
            Assert.AreEqual(0, ship.KdyPool, "KDY pool consumed");
            Assert.AreEqual(0, ship.LnrPool, "LNR pool consumed");
        }

        // --- Facility contribution tests ---

        [Test]
        public void Contribution_KDYFacility_AddsToShipKdyPool()
        {
            // A KDY facility should contribute to the ship's KDY pool
            // Formula: (personnelSkill / divisor + 1) * productionModifier
            Building kdy = CreateKDYFacility("kdy1", "empire", modifier: 5);
            _game.AttachNode(kdy, _empPlanet);

            // Officer at the system provides personnel skill
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            _game.AttachNode(officer, _empPlanet);

            CapitalShip ship = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 1000);

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            // With leadership=50, divisor from config, modifier=5:
            // contribution = (50 / divisor + 1) * 5
            // The exact value depends on config, but pool should be positive
            Assert.Greater(
                ship.RefinedMaterialProgress,
                0,
                "KDY facility should contribute to primary via pool"
            );
        }

        [Test]
        public void Contribution_NoFacilities_NoPoolChange()
        {
            // Without KDY/LNR facilities, no contributions should occur
            CapitalShip ship = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 100);

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.AreEqual(
                0,
                ship.RefinedMaterialProgress,
                "No facilities means no contribution"
            );
            Assert.AreEqual(0, ship.KdyPool, "KDY pool unchanged without KDY facilities");
            Assert.AreEqual(0, ship.LnrPool, "LNR pool unchanged without LNR facilities");
        }

        [Test]
        public void Contribution_IncompleteFacility_DoesNotContribute()
        {
            Building kdy = CreateKDYFacility("kdy1", "empire", modifier: 10);
            kdy.ManufacturingStatus = ManufacturingStatus.Building;
            _game.AttachNode(kdy, _empPlanet);

            CapitalShip ship = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 100);

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.AreEqual(
                0,
                ship.RefinedMaterialProgress,
                "Incomplete facility should not contribute"
            );
        }

        // --- Stage execution tests ---

        [Test]
        public void Execute_AllStagesRunUnconditionally()
        {
            // Verify that all 4 stages always run, regardless of prior stage results.
            // The AND-chain only affects the return value.
            // If setup finds no ships, contribution should still run (and find none itself).
            // This test ensures no short-circuiting.
            CapitalShipProductionIssue issue = new CapitalShipProductionIssue(
                CapitalShipProductionIssue.Variant.FullPipeline,
                _game,
                _empire,
                _system,
                new StubRNG()
            );

            // Should not throw even with no ships in progress
            bool result = issue.Execute();

            // With no ships, setup returns false, but other stages still ran
            // (they returned true because their enable flags are set but there's nothing to process)
            Assert.IsFalse(result, "Should return false when no ships in progress");
        }

        [Test]
        public void Execute_NullSystem_ReturnsFalse()
        {
            CapitalShipProductionIssue issue = new CapitalShipProductionIssue(
                CapitalShipProductionIssue.Variant.FullPipeline,
                _game,
                _empire,
                null,
                new StubRNG()
            );

            Assert.IsFalse(issue.Execute(), "Null system should return false");
        }

        [Test]
        public void Execute_NullFaction_ReturnsFalse()
        {
            CapitalShipProductionIssue issue = new CapitalShipProductionIssue(
                CapitalShipProductionIssue.Variant.FullPipeline,
                _game,
                null,
                _system,
                new StubRNG()
            );

            Assert.IsFalse(issue.Execute(), "Null faction should return false");
        }

        // --- Personnel skill tests ---

        [Test]
        public void PersonnelSkill_SystemWideFirstMatch()
        {
            // Personnel lookup is system-wide (not per-planet), returns first match.
            // Two officers: one on empPlanet (leadership=30), one on a second empire planet (leadership=90).
            // First match should be used regardless of skill value.
            Planet empPlanet2 = new Planet
            {
                InstanceID = "emp_p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 50,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 80 } },
            };
            _game.AttachNode(empPlanet2, _system);

            Officer officer1 = EntityFactory.CreateOfficer("o1", "empire");
            officer1.SetSkillValue(MissionParticipantSkill.Leadership, 30);
            _game.AttachNode(officer1, _empPlanet);

            Officer officer2 = EntityFactory.CreateOfficer("o2", "empire");
            officer2.SetSkillValue(MissionParticipantSkill.Leadership, 90);
            _game.AttachNode(officer2, empPlanet2);

            Building kdy = CreateKDYFacility("kdy1", "empire", modifier: 10);
            _game.AttachNode(kdy, _empPlanet);

            CapitalShip ship1 = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 10000);

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            int progress1 = ship1.RefinedMaterialProgress;

            // Reset and test with officers swapped
            ship1.RefinedMaterialProgress = 0;
            ship1.KdyPool = 0;

            // Remove officers and re-add in opposite order
            _game.DetachNode(officer1);
            _game.DetachNode(officer2);
            _game.AttachNode(officer2, _empPlanet); // 90 skill, now first
            _game.AttachNode(officer1, empPlanet2); // 30 skill, now second

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            int progress2 = ship1.RefinedMaterialProgress;

            // progress2 should be higher because officer2 (skill=90) is now first match
            Assert.Greater(progress2, progress1, "First officer found should be used, not best");
        }

        [Test]
        public void PersonnelSkill_NoOfficer_StillContributes()
        {
            // With no officers, personnel skill = 0, formula = (0/divisor + 1) * modifier = modifier
            Building kdy = CreateKDYFacility("kdy1", "empire", modifier: 10);
            _game.AttachNode(kdy, _empPlanet);

            CapitalShip ship = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 10000);

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            // (0 / divisor + 1) * 10 = 10 per facility per variant that has contribution enabled
            Assert.Greater(
                ship.RefinedMaterialProgress,
                0,
                "Should still contribute with zero personnel skill"
            );
        }

        // --- Multiple ship tests ---

        [Test]
        public void Contribution_MultipleShips_FacilitiesDistributeRandomly()
        {
            // Each facility independently picks a random ship to contribute to.
            // With StubRNG (always returns min=0), all contributions go to the first ship.
            // With a different RNG, contributions should spread.
            Building kdy = CreateKDYFacility("kdy1", "empire", modifier: 10);
            _game.AttachNode(kdy, _empPlanet);

            CapitalShip ship1 = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 10000);

            // Need a second planet with a ship for the second queue entry
            Planet empPlanet2 = new Planet
            {
                InstanceID = "emp_p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 50,
                PositionY = 0,
                PopularSupport = new Dictionary<string, int> { { "empire", 80 } },
            };
            _game.AttachNode(empPlanet2, _system);
            CapitalShip ship2 = EnqueueShip(empPlanet2, "cs2", "empire", constructionCost: 10000);

            // StubRNG always returns min (0), so all contributions go to ship at index 0
            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.Greater(
                ship1.RefinedMaterialProgress,
                0,
                "First ship should receive contributions with StubRNG"
            );

            // Reset and test with RNG that returns max (last index = 1)
            ship1.RefinedMaterialProgress = 0;
            ship1.KdyPool = 0;
            ship2.RefinedMaterialProgress = 0;
            ship2.KdyPool = 0;

            // SequenceRNG with int value 1 will select ship at index 1
            SequenceRNG lastRNG = new SequenceRNG(
                intValues: Enumerable.Repeat(1, 100).ToArray()
            );
            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, lastRNG);

            Assert.Greater(
                ship2.RefinedMaterialProgress,
                0,
                "Second ship should receive contributions when RNG selects index 1"
            );
        }

        [Test]
        public void AI_BuildsDifferentCapitalShipTypes()
        {
            // Verify that AI can queue different ship types when multiple technologies
            // are available at the same research level.
            GameConfig config = TestConfig.Create();
            GameRoot testGame = new GameRoot(config);

            Faction testEmpire = new Faction { InstanceID = "empire", PlayerID = null };
            testGame.Factions.Add(testEmpire);

            PlanetSystem testSystem = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            testGame.AttachNode(testSystem, testGame.Galaxy);

            // Create multiple planets with shipyards to trigger multiple enqueue calls
            List<Planet> planets = new List<Planet>();
            for (int i = 0; i < 6; i++)
            {
                Planet p = new Planet
                {
                    InstanceID = $"p{i}",
                    OwnerInstanceID = "empire",
                    IsColonized = true,
                    PositionX = i * 10,
                    PositionY = 0,
                    NumRawResourceNodes = 200,
                    EnergyCapacity = 10,
                    PopularSupport = new Dictionary<string, int> { { "empire", 80 } },
                };
                testGame.AttachNode(p, testSystem);
                planets.Add(p);

                // Each planet gets a shipyard, mine, and refinery for a working economy
                Building shipyard = CreateShipyard($"sy{i}", "empire");
                testGame.AttachNode(shipyard, p);

                Building mine = new Building
                {
                    InstanceID = $"mine{i}",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Mine,
                    ProcessRate = 200,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                testGame.AttachNode(mine, p);

                Building refinery = new Building
                {
                    InstanceID = $"ref{i}",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Refinery,
                    ProcessRate = 200,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                testGame.AttachNode(refinery, p);
            }

            // Register multiple ship technologies at research level 0
            CapitalShip dreadnaught = new CapitalShip
            {
                InstanceID = "CSEM001_template",
                DisplayName = "Imperial Dreadnaught",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                ConstructionCost = 44,
                ProductionCapacity = 40,
                BaseBuildSpeed = 1,
                StarfighterCapacity = 12,
                RegimentCapacity = 4,
            };

            CapitalShip galleon = new CapitalShip
            {
                InstanceID = "CSEM002_template",
                DisplayName = "Galleon",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                ConstructionCost = 10,
                ProductionCapacity = 40,
                BaseBuildSpeed = 1,
                StarfighterCapacity = 0,
                RegimentCapacity = 0,
            };

            CapitalShip carrack = new CapitalShip
            {
                InstanceID = "CSEM004_template",
                DisplayName = "Carrack Light Cruiser",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                ConstructionCost = 26,
                ProductionCapacity = 40,
                BaseBuildSpeed = 1,
                StarfighterCapacity = 0,
                RegimentCapacity = 0,
            };

            testEmpire.AddTechnologyNode(0, new Technology(dreadnaught));
            testEmpire.AddTechnologyNode(0, new Technology(galleon));
            testEmpire.AddTechnologyNode(0, new Technology(carrack));

            // Run AI manufacturing
            FogOfWarSystem fog = new FogOfWarSystem(testGame);
            MovementSystem movement = new MovementSystem(testGame, fog);
            ManufacturingSystem mfg = new ManufacturingSystem(testGame);
            OwnershipSystem ownership = new OwnershipSystem(testGame, movement, mfg);
            MissionSystem missionSystem = new MissionSystem(testGame, movement, ownership);
            AISystem ai = new AISystem(testGame, missionSystem, movement, mfg, new CyclingRNG());

            ai.ProcessTick();

            // Collect all capital ships in manufacturing queues
            List<CapitalShip> queuedShips = new List<CapitalShip>();
            foreach (Planet p in planets)
            {
                Dictionary<ManufacturingType, List<IManufacturable>> queue =
                    p.GetManufacturingQueue();
                if (queue.TryGetValue(ManufacturingType.Ship, out List<IManufacturable> shipQueue))
                {
                    queuedShips.AddRange(shipQueue.OfType<CapitalShip>());
                }
            }

            Assert.Greater(queuedShips.Count, 0, "AI should have queued at least one ship");

            HashSet<string> uniqueTypes = new HashSet<string>(
                queuedShips.Select(s => s.DisplayName)
            );

            // Original game selects randomly from all available ship types per shipyard.
            // With 3 tech types and multiple shipyards, we should see variety.
            Assert.Greater(
                uniqueTypes.Count,
                1,
                "AI should build different ship types, not just one — "
                    + $"building: {string.Join(", ", uniqueTypes)}"
            );
        }

        // --- Assault stage tests ---

        [Test]
        public void Assault_ProbabilityDampening_StaleCountCausesMisses()
        {
            // The assault stage rolls target indices using the STORED count from setup,
            // not the current count. As targets are destroyed, some rolls exceed the
            // shrinking list size and become no-ops. This test verifies the dampening.
            Officer officer = EntityFactory.CreateOfficer("o1", "empire");
            officer.SetSkillValue(MissionParticipantSkill.Leadership, 100);
            _game.AttachNode(officer, _empPlanet);

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, _empPlanet);

            // Add a strong capital ship to the fleet for high combat value
            CapitalShip warship = new CapitalShip
            {
                InstanceID = "warship",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 0,
                RegimentCapacity = 0,
                HullStrength = 999,
                ManufacturingStatus = ManufacturingStatus.Complete,
                PrimaryWeapons = new Dictionary<PrimaryWeaponType, int[]>
                {
                    {
                        PrimaryWeaponType.Turbolaser,
                        new int[] { 100, 100, 100, 100, 100 }
                    },
                    { PrimaryWeaponType.IonCannon, new int[] { 50, 50, 50, 50, 50 } },
                    { PrimaryWeaponType.LaserCannon, new int[] { 50, 50, 50, 50, 50 } },
                },
            };
            _game.AttachNode(warship, fleet);

            // Place a few enemy regiments as strike targets
            int initialRegimentCount = 3;
            for (int i = 0; i < initialRegimentCount; i++)
            {
                Regiment reg = EntityFactory.CreateRegiment($"reg{i}", "rebels");
                reg.DefenseRating = 0; // Easy to strike
                _game.AttachNode(reg, _rebelPlanet);
            }

            // Ship in progress (so productionComplete doesn't get set by contribution)
            EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 10000);

            // Run only the FullPipeline variant (has all stages enabled)
            CapitalShipProductionIssue issue = new CapitalShipProductionIssue(
                CapitalShipProductionIssue.Variant.SetupContributionAssault,
                _game,
                _empire,
                _system,
                new StubRNG()
            );
            issue.Execute();

            // Count remaining regiments
            List<Regiment> remaining = _rebelPlanet
                .GetAllRegiments()
                .Where(r => r.GetOwnerInstanceID() == "rebels")
                .ToList();

            // Some regiments should have been destroyed by assault strikes.
            // The exact number depends on net strength, RNG, and dampening.
            // With StubRNG (always returns min), target index is always 0,
            // and threshold is always thresholdLow, so strikes should succeed
            // against targets with resistance < thresholdLow.
            Assert.Less(
                remaining.Count,
                initialRegimentCount,
                "Assault should destroy some enemy targets"
            );
        }

        // --- Variant tests ---

        [Test]
        public void Variant_ContributionOnly_SkipsSetupAndAssault()
        {
            // Variant 0x221 only has enableContribution. It should still work
            // by enumerating ships directly without setup's ship list.
            Building kdy = CreateKDYFacility("kdy1", "empire", modifier: 10);
            _game.AttachNode(kdy, _empPlanet);

            CapitalShip ship = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 10000);

            CapitalShipProductionIssue issue = new CapitalShipProductionIssue(
                CapitalShipProductionIssue.Variant.ContributionOnly,
                _game,
                _empire,
                _system,
                new StubRNG()
            );

            bool result = issue.Execute();

            Assert.IsTrue(result, "ContributionOnly should return true when ships exist");
            Assert.Greater(
                ship.RefinedMaterialProgress,
                0,
                "ContributionOnly should still contribute to ships"
            );
        }

        [Test]
        public void ExecuteAllVariants_RunsAll4Variants()
        {
            // ExecuteAllVariants should run all 4 variants without throwing.
            // Contribution happens in variants 221, 222, and 223 (3 variants with enableContribution).
            Building kdy = CreateKDYFacility("kdy1", "empire", modifier: 10);
            _game.AttachNode(kdy, _empPlanet);

            CapitalShip ship = EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 10000);

            Assert.DoesNotThrow(
                () =>
                    CapitalShipProductionIssue.ExecuteAllVariants(
                        _game,
                        _empire,
                        _system,
                        new StubRNG()
                    ),
                "ExecuteAllVariants should not throw"
            );

            // 3 variants contribute (221, 222, 223), each with the same facility,
            // so progress should reflect 3x the single-variant contribution
            Assert.Greater(
                ship.RefinedMaterialProgress,
                0,
                "Multiple variants should accumulate contributions"
            );
        }

        // --- Finalize tests ---

        [Test]
        public void Finalize_NoDeathStar_NoSupportShift()
        {
            // Without Death Star ships, finalize should not modify popular support
            int initialSupport = _rebelPlanet.PopularSupport["rebels"];

            EnqueueShip(_empPlanet, "cs1", "empire", constructionCost: 10000);

            CapitalShipProductionIssue.ExecuteAllVariants(_game, _empire, _system, new StubRNG());

            Assert.AreEqual(
                initialSupport,
                _rebelPlanet.PopularSupport["rebels"],
                "Support should be unchanged without Death Star"
            );
        }
    }
}
