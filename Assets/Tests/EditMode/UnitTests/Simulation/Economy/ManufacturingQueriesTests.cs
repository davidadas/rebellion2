using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class ManufacturingQueriesTests
    {
        [Test]
        public void Constructor_NullGame_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new ManufacturingQueries(null)
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        [Test]
        public void EstimateQueueCompletionTicks_NoQueuedWork_ReturnsNull()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");

            int? estimate = ManufacturingQueries.EstimateQueueCompletionTicks(
                planet,
                ManufacturingType.Building
            );

            Assert.IsNull(estimate);
        }

        [Test]
        public void EstimateQueueCompletionTicks_PartiallyCompletedQueue_PreservesProductionState()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            Building facility = CreateOrderTestConstructionFacility("yard", "empire", 4);
            facility.ProductionCycleProgress = 1;
            game.AttachNode(facility, planet);
            Building first = CreateOrderTestBuildingTemplate("first");
            Building second = CreateOrderTestBuildingTemplate("second");
            foreach (Building item in new[] { first, second })
            {
                item.OwnerInstanceID = "empire";
                item.ManufacturingStatus = ManufacturingStatus.Building;
                game.AttachNode(item, planet);
                planet.AddToManufacturingQueue(item);
            }
            first.ManufacturingProgress = 4;
            second.ManufacturingProgress = 8;

            int? estimate = ManufacturingQueries.EstimateQueueCompletionTicks(
                planet,
                ManufacturingType.Building
            );

            Assert.AreEqual(31, estimate);
            CollectionAssert.AreEqual(
                new[] { first, second },
                planet.GetManufacturingQueue()[ManufacturingType.Building]
            );
            Assert.AreEqual(4, first.ManufacturingProgress);
            Assert.AreEqual(8, second.ManufacturingProgress);
            Assert.AreEqual(1, facility.ProductionCycleProgress);
        }

        [Test]
        public void CanStartManufacturing_DifferentProject_DoesNotCancelExistingWork()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            ManufacturingCommands manager = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game)
            );
            Assert.IsTrue(
                manager.StartManufacturing(
                    producer,
                    CreateOrderTestBuildingTemplate("mine"),
                    producer,
                    1,
                    "empire"
                )
            );
            IManufacturable original = producer
                .GetManufacturingQueue()[ManufacturingType.Building]
                .Single();

            bool eligible = new ManufacturingQueries(game).CanStartManufacturing(
                producer,
                CreateOrderTestBuildingTemplate("refinery"),
                producer,
                1,
                "empire"
            );

            Assert.IsTrue(eligible);
            Assert.AreSame(
                original,
                producer.GetManufacturingQueue()[ManufacturingType.Building].Single()
            );
            Assert.AreSame(producer, original.GetParent());
        }

        [Test]
        public void CanAcceptManufacturingOrder_WithoutMaintenanceHeadroom_ReturnsTrue()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");

            CapitalShip template = CreateOrderTestCapitalShipTemplate(
                "dreadnaught",
                "Dreadnaught",
                1
            );

            bool canAccept = ManufacturingQueries.CanAcceptManufacturingOrder(
                planet,
                template,
                planet,
                1,
                "empire"
            );

            Assert.IsTrue(canAccept);
            Assert.IsFalse(
                new ManufacturingQueries(game).CanStartManufacturing(
                    planet,
                    template,
                    planet,
                    1,
                    "empire"
                )
            );
        }

        [Test]
        public void CanAcceptManufacturingOrder_StarfighterForBlockadedPlanet_ReturnsFalse()
        {
            GameRoot game = CreateOrderTestGame();
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            Planet producer = CreateOrderTestShipyardPlanet(game, "producer", "empire");
            Planet destination = CreateOrderTestPlanet(game, "destination", "empire");
            Fleet blockadingFleet = EntityFactory.CreateFleet("blockading-fleet", "rebels");
            CapitalShip blockadingShip = new CapitalShip
            {
                InstanceID = "blockading-ship",
                OwnerInstanceID = "rebels",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
            };
            game.AttachNode(blockadingFleet, destination);
            game.AttachNode(blockadingShip, blockadingFleet);
            Starfighter template = CreateOrderTestStarfighterTemplate("fighter", "empire");

            bool canAccept = ManufacturingQueries.CanAcceptManufacturingOrder(
                producer,
                template,
                destination,
                1,
                "empire"
            );

            Assert.IsFalse(canAccept);
        }

        [Test]
        public void CanStartManufacturing_ResourceFacilityWithoutMaintenanceHeadroom_ReturnsTrue()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            Building mine = CreateOrderTestBuildingTemplate("mine");
            mine.MaintenanceCost = 1;

            bool canStart = new ManufacturingQueries(game).CanStartManufacturing(
                planet,
                mine,
                planet,
                1,
                "empire"
            );

            Assert.IsTrue(canStart);
        }

        [Test]
        public void CanStartManufacturing_HardAiMaintenanceBonus_AllowsBonusHeadroom()
        {
            GameRoot game = CreateOrderTestGame();
            Faction empire = game.GetFactions().Single();
            empire.Settings.ResourceProcessingPointsPerFacility = 50;
            game.Summary = new GameSummary { Difficulty = GameDifficulty.Hard };
            game.SetFactionController(empire.InstanceID, "EMPIRE-AI", PlayerControllerType.AI);
            game.Config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                MaintenanceCapacityPercent = 150,
            };
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", empire.InstanceID);
            game.AttachNode(
                new Building
                {
                    InstanceID = "mine",
                    OwnerInstanceID = empire.InstanceID,
                    BuildingType = BuildingType.Mine,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(
                new Building
                {
                    InstanceID = "refinery",
                    OwnerInstanceID = empire.InstanceID,
                    BuildingType = BuildingType.Refinery,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            bool canStart = new ManufacturingQueries(game).CanStartManufacturing(
                planet,
                CreateOrderTestCapitalShipTemplate("ship", "Ship", maintenanceCost: 60),
                planet,
                1,
                empire.InstanceID
            );

            Assert.IsTrue(canStart);
        }

        [Test]
        public void CanStartManufacturing_SpecialForcesDestinationFleetHasAvailableShip_ReturnsTrue()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestTrainingPlanet(game, "producer", "empire");
            Planet destination = CreateOrderTestPlanet(game, "destination", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(ship, fleet);

            bool canStart = new ManufacturingQueries(game).CanStartManufacturing(
                producer,
                CreateOrderTestSpecialForcesTemplate(),
                fleet,
                1,
                "empire"
            );

            Assert.IsTrue(canStart);
        }

        [Test]
        public void CanStartManufacturing_SpecialForcesDestinationIsAvailableShip_ReturnsTrue()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestTrainingPlanet(game, "producer", "empire");
            Planet destination = CreateOrderTestPlanet(game, "destination", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(ship, fleet);

            bool canStart = new ManufacturingQueries(game).CanStartManufacturing(
                producer,
                CreateOrderTestSpecialForcesTemplate(),
                ship,
                1,
                "empire"
            );

            Assert.IsTrue(canStart);
        }

        [Test]
        public void EstimateManufacturingTicks_MixedFacilityRates_UsesIntegerRateShares()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            game.AttachNode(CreateOrderTestConstructionFacility("yard1", "empire", 3), planet);
            game.AttachNode(CreateOrderTestConstructionFacility("yard2", "empire", 6), planet);
            Building template = CreateOrderTestBuildingTemplate("mine");
            template.ConstructionCost = 1;

            int? estimate = ManufacturingQueries.EstimateManufacturingTicks(planet, template, 1);

            Assert.AreEqual(3, estimate);
        }

        [Test]
        public void EstimateManufacturingTicks_InactiveFacilities_DoNotContribute()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            Building active = CreateOrderTestConstructionFacility("active", "empire", 4);
            Building unfinished = CreateOrderTestConstructionFacility("unfinished", "empire", 1);
            unfinished.ManufacturingStatus = ManufacturingStatus.Building;
            Building traveling = CreateOrderTestConstructionFacility("traveling", "empire", 1);
            traveling.Movement = new MovementState { TransitTicks = 5 };
            game.AttachNode(active, planet);
            game.AttachNode(unfinished, planet);
            game.AttachNode(traveling, planet);
            Building template = CreateOrderTestBuildingTemplate("mine");
            template.ConstructionCost = 1;

            int? estimate = ManufacturingQueries.EstimateManufacturingTicks(planet, template, 1);

            Assert.AreEqual(4, estimate);
        }

        [Test]
        public void EstimateCompletionTicks_Default_IncludesEarlierQueuedWorkAndCurrentProgress()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            game.AttachNode(CreateOrderTestConstructionFacility("yard", "empire", 2), planet);
            Building first = CreateOrderTestBuildingTemplate("first");
            first.ConstructionCost = 10;
            first.ManufacturingProgress = 4;
            first.ManufacturingStatus = ManufacturingStatus.Building;
            first.OwnerInstanceID = "empire";
            Building second = CreateOrderTestBuildingTemplate("second");
            second.ConstructionCost = 20;
            second.ManufacturingProgress = 5;
            second.ManufacturingStatus = ManufacturingStatus.Building;
            second.OwnerInstanceID = "empire";
            game.AttachNode(first, planet);
            game.AttachNode(second, planet);
            planet.AddToManufacturingQueue(first);
            planet.AddToManufacturingQueue(second);

            int? estimate = ManufacturingQueries.EstimateCompletionTicks(planet, second);

            Assert.AreEqual(42, estimate);
        }

        [Test]
        public void EstimateCompletionTicks_ActiveFacilityCycle_UsesRemainingCycleTime()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            Building yard = CreateOrderTestConstructionFacility("yard", "empire", 4);
            yard.ProductionCycleProgress = 1;
            game.AttachNode(yard, planet);
            Building item = CreateOrderTestBuildingTemplate("item");
            item.ConstructionCost = 3;
            item.ManufacturingProgress = 2;
            item.ManufacturingStatus = ManufacturingStatus.Building;
            item.OwnerInstanceID = "empire";
            game.AttachNode(item, planet);
            planet.AddToManufacturingQueue(item);

            int? estimate = ManufacturingQueries.EstimateCompletionTicks(planet, item);

            Assert.AreEqual(3, estimate);
        }

        /// <summary>
        /// Creates order test game.
        /// </summary>
        /// <returns>The created order test game.</returns>
        private static GameRoot CreateOrderTestGame()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            return game;
        }

        /// <summary>
        /// Creates order test shipyard planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planetId">The planet id.</param>
        /// <param name="factionId">The faction id.</param>
        /// <returns>The created order test shipyard planet.</returns>
        private static Planet CreateOrderTestShipyardPlanet(
            GameRoot game,
            string planetId,
            string factionId
        )
        {
            Planet planet = CreateOrderTestPlanet(game, planetId, factionId);
            game.AttachNode(
                new Building
                {
                    InstanceID = $"{planetId}_shipyard",
                    OwnerInstanceID = factionId,
                    BuildingType = BuildingType.Shipyard,
                    ProductionType = ManufacturingType.Ship,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            return planet;
        }

        /// <summary>
        /// Creates an order test training planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planetId">The planet id.</param>
        /// <param name="factionId">The faction id.</param>
        /// <returns>The created order test training planet.</returns>
        private static Planet CreateOrderTestTrainingPlanet(
            GameRoot game,
            string planetId,
            string factionId
        )
        {
            Planet planet = CreateOrderTestPlanet(game, planetId, factionId);
            game.AttachNode(
                new Building
                {
                    InstanceID = $"{planetId}_training",
                    OwnerInstanceID = factionId,
                    BuildingType = BuildingType.TrainingFacility,
                    ProductionType = ManufacturingType.Troop,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            return planet;
        }

        /// <summary>
        /// Creates order test construction planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planetId">The planet id.</param>
        /// <param name="factionId">The faction id.</param>
        /// <returns>The created order test construction planet.</returns>
        private static Planet CreateOrderTestConstructionPlanet(
            GameRoot game,
            string planetId,
            string factionId
        )
        {
            Planet planet = CreateOrderTestPlanet(game, planetId, factionId);
            game.AttachNode(
                CreateOrderTestConstructionFacility($"{planetId}_construction", factionId, 1),
                planet
            );
            return planet;
        }

        /// <summary>
        /// Creates order test construction facility.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="factionId">The faction id.</param>
        /// <param name="processRate">The process rate.</param>
        /// <returns>The created order test construction facility.</returns>
        private static Building CreateOrderTestConstructionFacility(
            string instanceId,
            string factionId,
            int processRate
        )
        {
            return new Building
            {
                InstanceID = instanceId,
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = processRate,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }

        /// <summary>
        /// Creates order test planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planetId">The planet id.</param>
        /// <param name="factionId">The faction id.</param>
        /// <returns>The created order test planet.</returns>
        private static Planet CreateOrderTestPlanet(
            GameRoot game,
            string planetId,
            string factionId
        )
        {
            PlanetSector planetSector = new PlanetSector { InstanceID = $"{planetId}_sector" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = planetId,
                OwnerInstanceID = factionId,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 10,
            };
            game.AttachNode(planet, planetSector);
            return planet;
        }

        /// <summary>
        /// Creates order test capital ship template.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="displayName">The display name.</param>
        /// <param name="maintenanceCost">The maintenance cost.</param>
        /// <returns>The created order test capital ship template.</returns>
        private static CapitalShip CreateOrderTestCapitalShipTemplate(
            string typeId,
            string displayName,
            int maintenanceCost
        )
        {
            return new CapitalShip
            {
                TypeID = typeId,
                DisplayName = displayName,
                ConstructionCost = 10,
                MaintenanceCost = maintenanceCost,
                BaseBuildSpeed = 1,
            };
        }

        /// <summary>
        /// Creates an order test starfighter template.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="factionId">The manufacturing faction id.</param>
        /// <returns>The created starfighter template.</returns>
        private static Starfighter CreateOrderTestStarfighterTemplate(
            string typeId,
            string factionId
        )
        {
            return new Starfighter
            {
                TypeID = typeId,
                DisplayName = typeId,
                ConstructionCost = 10,
                MaintenanceCost = 0,
                BaseBuildSpeed = 1,
                ManufacturingFactionInstanceIDs = new List<string> { factionId },
            };
        }

        /// <summary>
        /// Creates order test building template.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <returns>The created order test building template.</returns>
        private static Building CreateOrderTestBuildingTemplate(string typeId)
        {
            return new Building
            {
                TypeID = typeId,
                DisplayName = typeId,
                ConstructionCost = 10,
                MaintenanceCost = 0,
                BaseBuildSpeed = 1,
                BuildingType = BuildingType.Mine,
            };
        }

        /// <summary>
        /// Creates an order test special-forces template.
        /// </summary>
        /// <returns>The created special-forces template.</returns>
        private static SpecialForces CreateOrderTestSpecialForcesTemplate()
        {
            return new SpecialForces
            {
                TypeID = "special-forces",
                DisplayName = "Special forces",
                ConstructionCost = 10,
                MaintenanceCost = 0,
                BaseBuildSpeed = 1,
                ManufacturingFactionInstanceIDs = new List<string> { "empire" },
            };
        }
    }
}
