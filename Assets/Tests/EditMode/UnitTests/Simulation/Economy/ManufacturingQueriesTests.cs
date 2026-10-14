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
        [TestCase(true, ManufacturingStatus.Building, true)]
        [TestCase(false, ManufacturingStatus.Building, false)]
        [TestCase(true, ManufacturingStatus.Complete, false)]
        public void GetInterruptibleManufacturingOrder_SingleBuilding_ReturnsOnlyUnfinishedMarkedOrder(
            bool marked,
            ManufacturingStatus status,
            bool expected
        )
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            Building building = CreateOrderTestBuildingTemplate("shield");
            building.OwnerInstanceID = "empire";
            building.ProducerPlanetID = planet.InstanceID;
            building.ManufacturingStatus = status;
            building.IsAIProductionFiller = marked;
            game.AttachNode(building, planet);
            planet.AddToManufacturingQueue(building);

            Assert.AreEqual(
                expected ? building : null,
                ManufacturingQueries.GetInterruptibleManufacturingOrder(
                    planet,
                    ManufacturingType.Building
                )
            );
        }

        [Test]
        public void GetInterruptibleManufacturingOrder_MixedQueue_DoesNotInterruptWork()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            foreach (string typeId in new[] { "shield", "required" })
            {
                Building building = CreateOrderTestBuildingTemplate(typeId);
                building.OwnerInstanceID = "empire";
                building.ProducerPlanetID = planet.InstanceID;
                building.ManufacturingStatus = ManufacturingStatus.Building;
                building.IsAIProductionFiller = typeId == "shield";
                game.AttachNode(building, planet);
                planet.AddToManufacturingQueue(building);
            }

            Assert.IsNull(
                ManufacturingQueries.GetInterruptibleManufacturingOrder(
                    planet,
                    ManufacturingType.Building
                )
            );
        }

        [TestCase(true, 1)]
        [TestCase(false, 0)]
        public void GetAvailableBuildingSpace_FullPlanet_IncludesOnlyReplaceableProject(
            bool marked,
            int expected
        )
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            Planet destination = CreateOrderTestPlanet(game, "p2", "empire");
            destination.EnergyCapacity = 1;
            Building building = CreateOrderTestBuildingTemplate("shield");
            building.OwnerInstanceID = "empire";
            building.ProducerPlanetID = producer.InstanceID;
            building.ManufacturingStatus = ManufacturingStatus.Building;
            building.IsAIProductionFiller = marked;
            game.AttachNode(building, destination);
            producer.AddToManufacturingQueue(building);

            Assert.AreEqual(0, ManufacturingQueries.GetAvailableBuildingSpace(game, destination));
            Assert.AreEqual(
                expected,
                ManufacturingQueries.GetAvailableBuildingSpace(game, destination, true)
            );
        }

        [Test]
        public void Constructor_NullGame_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new ManufacturingQueries(null)
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        [Test]
        public void GetManufacturingOrder_PopulatedLane_ReturnsProductAndQuantity()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            Building template = CreateOrderTestBuildingTemplate("mine");
            ManufacturingCommands manager = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game)
            );
            Assert.IsTrue(manager.StartManufacturing(planet, template, planet, 3, "empire"));

            ManufacturingOrder order = ManufacturingQueries.GetManufacturingOrder(
                planet,
                ManufacturingType.Building
            );

            Assert.IsNotNull(order);
            Assert.AreEqual(ManufacturingType.Building, order.ManufacturingType);
            Assert.AreEqual(template.TypeID, order.ProductTypeID);
            Assert.AreEqual(3, order.Quantity);
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

        [TestCase(1, 6)]
        [TestCase(3, 26)]
        public void EstimateManufacturingOrderTicks_SameProduct_PreservesRetainedProgress(
            int requestedCount,
            int expectedTicks
        )
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            game.AttachNode(CreateOrderTestConstructionFacility("yard", "empire", 1), planet);
            Building template = CreateOrderTestBuildingTemplate("mine");
            ManufacturingCommands manager = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game)
            );
            Assert.IsTrue(manager.StartManufacturing(planet, template, planet, 2, "empire"));
            planet.GetManufacturingQueue()[ManufacturingType.Building][0].ManufacturingProgress = 4;

            int? estimate = ManufacturingQueries.EstimateManufacturingOrderTicks(
                planet,
                template,
                requestedCount
            );

            Assert.AreEqual(expectedTicks, estimate);
        }

        [Test]
        public void CanSetManufacturingOrder_DifferentBuildingAtDestinationCapacity_ReleasesReplacementCapacity()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            planet.EnergyCapacity = 2;
            planet.NumRawResourceNodes = 1;
            Building activeTemplate = CreateOrderTestBuildingTemplate("mine");
            Building replacementTemplate = CreateOrderTestBuildingTemplate("refinery");
            replacementTemplate.BuildingType = BuildingType.Refinery;
            ManufacturingCommands manager = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game)
            );
            Assert.IsTrue(manager.StartManufacturing(planet, activeTemplate, planet, 1, "empire"));

            bool canSet = new ManufacturingQueries(game).CanSetManufacturingOrder(
                planet,
                replacementTemplate,
                planet,
                1,
                "empire"
            );

            Assert.IsTrue(canSet);
        }

        [Test]
        public void CanSetManufacturingOrder_ExistingOrderConsumesMaintenance_ReleasesReservation()
        {
            GameRoot game = CreateOrderTestGame();
            Faction faction = game.GetFactions().Single();
            faction.Settings.ResourceProcessingPointsPerFacility = 10;
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            game.AttachNode(
                new Building
                {
                    InstanceID = "mine",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Mine,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(
                new Building
                {
                    InstanceID = "refinery",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Refinery,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            CapitalShip activeTemplate = CreateOrderTestCapitalShipTemplate(
                "old-ship",
                "Old Ship",
                10
            );
            CapitalShip replacementTemplate = CreateOrderTestCapitalShipTemplate(
                "new-ship",
                "New Ship",
                10
            );
            ManufacturingCommands manager = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game)
            );
            Assert.IsTrue(manager.StartManufacturing(planet, activeTemplate, planet, 1, "empire"));

            bool canSet = new ManufacturingQueries(game).CanSetManufacturingOrder(
                planet,
                replacementTemplate,
                planet,
                1,
                "empire"
            );

            Assert.IsTrue(canSet);
        }

        [Test]
        public void CanSetManufacturingOrder_ExistingFighterFillsCarrier_ReleasesCarrierCapacity()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestShipyardPlanet(game, "producer", "empire");
            Planet destination = CreateOrderTestPlanet(game, "destination", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(carrier, fleet);
            Starfighter activeTemplate = CreateOrderTestStarfighterTemplate(
                "old-fighter",
                "empire"
            );
            Starfighter replacementTemplate = CreateOrderTestStarfighterTemplate(
                "new-fighter",
                "empire"
            );
            ManufacturingCommands manager = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game)
            );
            Assert.IsTrue(manager.StartManufacturing(producer, activeTemplate, fleet, 1, "empire"));

            bool canSet = new ManufacturingQueries(game).CanSetManufacturingOrder(
                producer,
                replacementTemplate,
                fleet,
                1,
                "empire"
            );

            Assert.IsTrue(canSet);
        }

        [Test]
        public void CanSetManufacturingOrder_ReplacementFighterOnTravelingCarrier_DoesNotReleaseCapacity()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestShipyardPlanet(game, "producer", "empire");
            Planet destination = CreateOrderTestPlanet(game, "destination", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(carrier, fleet);
            Starfighter activeTemplate = CreateOrderTestStarfighterTemplate(
                "old-fighter",
                "empire"
            );
            Starfighter replacementTemplate = CreateOrderTestStarfighterTemplate(
                "new-fighter",
                "empire"
            );
            ManufacturingCommands manager = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game)
            );
            Assert.IsTrue(manager.StartManufacturing(producer, activeTemplate, fleet, 1, "empire"));
            carrier.Movement = new MovementState { TransitTicks = 10 };

            bool canSet = new ManufacturingQueries(game).CanSetManufacturingOrder(
                producer,
                replacementTemplate,
                fleet,
                1,
                "empire"
            );

            Assert.IsFalse(canSet);
        }

        [Test]
        public void CanSetManufacturingOrder_ReplacementRegimentOnTravelingCarrier_DoesNotReleaseCapacity()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestTrainingPlanet(game, "producer", "empire");
            Planet destination = CreateOrderTestPlanet(game, "destination", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "carrier",
                OwnerInstanceID = "empire",
                RegimentCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(fleet, destination);
            game.AttachNode(carrier, fleet);
            Regiment activeTemplate = CreateOrderTestRegimentTemplate("old-regiment", "empire");
            Regiment replacementTemplate = CreateOrderTestRegimentTemplate(
                "new-regiment",
                "empire"
            );
            ManufacturingCommands manager = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game)
            );
            Assert.IsTrue(manager.StartManufacturing(producer, activeTemplate, fleet, 1, "empire"));
            carrier.Movement = new MovementState { TransitTicks = 10 };

            bool canSet = new ManufacturingQueries(game).CanSetManufacturingOrder(
                producer,
                replacementTemplate,
                fleet,
                1,
                "empire"
            );

            Assert.IsFalse(canSet);
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
        public void CanStartManufacturing_MineAtRawResourceNodeCapacity_ReturnsFalse()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            planet.NumRawResourceNodes = 2;
            game.AttachNode(
                new Building
                {
                    InstanceID = "mine1",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Mine,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            game.AttachNode(
                new Building
                {
                    InstanceID = "mine2",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Mine,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            bool canStart = new ManufacturingQueries(game).CanStartManufacturing(
                planet,
                CreateOrderTestBuildingTemplate("mine"),
                planet,
                1,
                "empire"
            );

            Assert.IsFalse(canStart);
        }

        [Test]
        public void CanStartManufacturing_MineWithAvailableResourceNodes_ReturnsTrue()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            planet.NumRawResourceNodes = 3;
            game.AttachNode(
                new Building
                {
                    InstanceID = "mine1",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Mine,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            bool canStart = new ManufacturingQueries(game).CanStartManufacturing(
                planet,
                CreateOrderTestBuildingTemplate("mine"),
                planet,
                1,
                "empire"
            );

            Assert.IsTrue(canStart);
        }

        [Test]
        public void CanStartManufacturing_NonMineBuildingAtRawResourceNodeCapacity_ReturnsTrue()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            planet.NumRawResourceNodes = 1;
            game.AttachNode(
                new Building
                {
                    InstanceID = "mine1",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.Mine,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            Building refinery = CreateOrderTestBuildingTemplate("refinery");
            refinery.BuildingType = BuildingType.Refinery;

            bool canStart = new ManufacturingQueries(game).CanStartManufacturing(
                planet,
                refinery,
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

        [Test]
        public void EstimateScheduledCompletionTicks_ShipyardUnderConstruction_IncludesFutureCapacity()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            game.AttachNode(
                CreateOrderTestConstructionFacility("construction", "empire", 1),
                planet
            );
            Building pending = AddProjectedShipyard(game, planet, ManufacturingStatus.Building);
            pending.ConstructionCost = 5;
            pending.ProducerPlanetID = planet.InstanceID;
            planet.AddToManufacturingQueue(pending);
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);
            ship.ConstructionCost = 20;

            int? estimate = new ManufacturingQueries(game).EstimateScheduledCompletionTicks(
                planet,
                ship,
                20
            );

            Assert.AreEqual(13, estimate);
            Assert.AreEqual(0, pending.ManufacturingProgress);
            CollectionAssert.AreEqual(
                new[] { pending },
                planet.GetManufacturingQueue()[ManufacturingType.Building]
            );
        }

        [Test]
        public void EstimateScheduledCompletionTicks_FirstShipyardInTransit_WaitsForDecisionAfterArrival()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            Building pending = AddProjectedShipyard(game, planet, ManufacturingStatus.Delivering);
            pending.Movement = new MovementState { TransitTicks = 5 };
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);

            int? estimate = new ManufacturingQueries(game).EstimateScheduledCompletionTicks(
                planet,
                ship,
                20
            );

            Assert.AreEqual(30, estimate);
            Assert.AreEqual(5, pending.Movement.TicksRemaining());
        }

        [Test]
        public void EstimateScheduledCompletionTicks_UnqueuedFacility_IgnoresFutureCapacity()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            AddProjectedShipyard(game, planet, ManufacturingStatus.Building);
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);

            Assert.AreEqual(
                10,
                new ManufacturingQueries(game).EstimateScheduledCompletionTicks(planet, ship, 20)
            );
        }

        [Test]
        public void EstimateScheduledCompletionTicks_BusyLane_IncludesDecisionDelay()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            CapitalShip current = CreateOrderTestCapitalShipTemplate("current", "current", 0);
            current.ManufacturingStatus = ManufacturingStatus.Building;
            current.ManufacturingProgress = 9;
            current.OwnerInstanceID = "empire";
            Fleet fleet = EntityFactory.CreateFleet("current-fleet", "empire");
            game.AttachNode(fleet, planet);
            game.AttachNode(current, fleet);
            planet.AddToManufacturingQueue(current);
            CapitalShip next = CreateOrderTestCapitalShipTemplate("next", "next", 0);

            int? estimate = new ManufacturingQueries(game).EstimateScheduledCompletionTicks(
                planet,
                next,
                20
            );

            Assert.AreEqual(30, estimate);
            Assert.AreEqual(9, current.ManufacturingProgress);
            Assert.AreEqual(1, planet.GetManufacturingQueue()[ManufacturingType.Ship].Count);
        }

        [Test]
        public void EstimateScheduledCompletionTicks_ReadyProductionPoint_PreservesCyclePhase()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            Building yard = planet.GetProductionFacilities(ManufacturingType.Ship).Single();
            yard.ProcessRate = 4;
            yard.ProductionCycleProgress = 1;
            yard.ProductionPointReady = true;
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);

            Assert.AreEqual(
                36,
                new ManufacturingQueries(game).EstimateScheduledCompletionTicks(planet, ship, 20)
            );
            Assert.AreEqual(1, yard.ProductionCycleProgress);
            Assert.IsTrue(yard.ProductionPointReady);
        }

        [Test]
        public void EstimateScheduledCompletionTicks_Uprising_ReturnsNull()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            planet.IsInUprising = true;
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);

            Assert.IsNull(
                new ManufacturingQueries(game).EstimateScheduledCompletionTicks(planet, ship, 20)
            );
        }

        [Test]
        public void EstimateScheduledCompletionTicks_TwoPrecedingOrders_ForecastsWithoutQueueing()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);
            Assert.AreEqual(
                50,
                new ManufacturingQueries(game).EstimateScheduledCompletionTicks(
                    planet,
                    ship,
                    20,
                    new IManufacturable[] { ship, ship }
                )
            );
            Assert.IsNull(
                ManufacturingQueries.GetManufacturingOrder(planet, ManufacturingType.Ship)
            );
        }

        [Test]
        public void EstimateScheduledCompletionTicks_DestinationReadyAtTurn35_StartsAtTurn40()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);
            int? completion = new ManufacturingQueries(game).EstimateScheduledCompletionTicks(
                planet,
                ship,
                20,
                earliestStartTicks: 35
            );
            Assert.AreEqual(50, completion);
        }

        [Test]
        public void EstimateScheduledCompletionTicks_PrecedingOrderWaitsForCapacity_PreservesItsDelay()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);
            int? completion = new ManufacturingQueries(game).EstimateScheduledCompletionTicks(
                planet,
                ship,
                20,
                new[] { ship },
                precedingStartTicks: new[] { 35 }
            );
            Assert.AreEqual(70, completion);
        }

        [Test]
        public void EstimateScheduledCompletionTicks_MultipleOrders_ReturnsEveryCompletionWithoutQueueing()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);
            CollectionAssert.AreEqual(
                new[] { 10, 50, 70 },
                new ManufacturingQueries(game).EstimateScheduledCompletionTicks(
                    planet,
                    new IManufacturable[] { ship, ship, ship },
                    20,
                    new[] { 0, 35, 0 }
                )
            );
            Assert.IsNull(
                ManufacturingQueries.GetManufacturingOrder(planet, ManufacturingType.Ship)
            );
        }

        [TestCase(1, 3, 3)]
        [TestCase(17, 3, 51)]
        [TestCase(int.MaxValue, 1, int.MaxValue)]
        [TestCase(int.MaxValue, 3, int.MaxValue)]
        public void EstimateScheduledCompletionTicks_ConstructionCostAndFacilityRate_ReturnsFirstCompletionTick(
            int constructionCost,
            int processRate,
            int expectedTicks
        )
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "producer", "empire");
            planet.GetProductionFacilities(ManufacturingType.Ship)[0].ProcessRate = processRate;
            CapitalShip ship = CreateOrderTestCapitalShipTemplate("ship", "ship", 0);
            ship.ConstructionCost = constructionCost;

            Assert.AreEqual(
                expectedTicks,
                new ManufacturingQueries(game).EstimateScheduledCompletionTicks(planet, ship, 1)
            );
        }

        /// <summary>Adds a shipyard in the requested manufacturing state.</summary>
        /// <param name="game">The game containing the planet.</param>
        /// <param name="planet">The shipyard's destination planet.</param>
        /// <param name="status">The shipyard's manufacturing status.</param>
        /// <returns>The attached shipyard.</returns>
        private static Building AddProjectedShipyard(
            GameRoot game,
            Planet planet,
            ManufacturingStatus status
        )
        {
            Building facility = new Building
            {
                InstanceID = "future-yard",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Shipyard,
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 1,
                ManufacturingStatus = status,
            };
            game.AttachNode(facility, planet);
            return facility;
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
        /// Creates an order test regiment template.
        /// </summary>
        /// <param name="typeId">The type id.</param>
        /// <param name="factionId">The manufacturing faction id.</param>
        /// <returns>The created regiment template.</returns>
        private static Regiment CreateOrderTestRegimentTemplate(string typeId, string factionId)
        {
            return new Regiment
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
