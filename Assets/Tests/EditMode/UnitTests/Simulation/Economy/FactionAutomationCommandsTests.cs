using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class FactionAutomationCommandsTests
    {
        private const string _factionId = "faction";
        private const string _strongDefenderTypeId = "strong-defender";
        private const string _cheapDefenderTypeId = "cheap-defender";
        private const string _offensiveRegimentTypeId = "offensive-regiment";

        private GameRoot _game;
        private GameDataCatalog _gameData;
        private Faction _faction;
        private ManufacturingCommands _manufacturing;
        private MovementCommands _movement;
        private Planet _producer;
        private Planet _destination;
        private FactionAutomationCommands _automation;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameConfig config = CreateGameConfig();
            _gameData = CreateGameData(config);
            _game = TestGame.Create(config);
            _faction = new Faction
            {
                InstanceID = _factionId,
                ManageGarrisons = true,
                ManageProduction = true,
            };
            _faction.Settings.ResourceProcessingPointsPerFacility = 50;
            _game.GetFactions().Add(_faction);
            _game.GetFactions().Add(new Faction { InstanceID = "enemy" });

            PlanetSector planetSector = new PlanetSector { InstanceID = "SECTOR" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _producer = CreatePlanet("PRODUCER", 100, 10);
            _destination = CreatePlanet("DESTINATION", 10, 10);
            _game.AttachNode(_producer, planetSector);
            _game.AttachNode(_destination, planetSector);

            AddProductionFacility(_producer, "TRAINING", ManufacturingType.Troop);
            AddResourcePairs(_producer, 1);
            SatisfyGarrison(_producer);

            _movement = new MovementCommands(
                _game,
                new FogOfWarCommands(_game),
                new FleetCommands(_game),
                new FogOfWarQueries(_game),
                new MovementQueries(_game)
            );
            _manufacturing = new ManufacturingCommands(
                _game,
                new FleetCommands(_game),
                new ManufacturingQueries(_game),
                _movement
            );
            _automation = new FactionAutomationCommands(
                _game,
                _gameData,
                _manufacturing,
                new GarrisonAutomationCommands(_game, _gameData, _manufacturing)
            );
        }

        [Test]
        public void ProcessFaction_EnabledAutomation_QueuesWorkImmediately()
        {
            AddProductionInfrastructure();
            _game.CurrentTick = 42;

            _automation.ProcessFaction(_faction);

            Assert.Greater(GetQueueCount(_producer, ManufacturingType.Troop), 0);
            Assert.IsNotEmpty(_producer.GetManufacturingQueue()[ManufacturingType.Building]);
            Assert.AreEqual(42, _game.CurrentTick);
        }

        [Test]
        public void ProcessFaction_FullLanes_DoesNotReplaceExistingOrders()
        {
            AddProductionInfrastructure();
            _automation.ProcessFaction(_faction);
            IManufacturable[] orders = _producer
                .GetManufacturingQueue()
                .Values.SelectMany(queue => queue)
                .ToArray();

            _automation.ProcessFaction(_faction);

            Assert.IsNotEmpty(orders);
            CollectionAssert.AreEqual(
                orders,
                _producer.GetManufacturingQueue().Values.SelectMany(queue => queue)
            );
        }

        [Test]
        public void ProcessFaction_NullFaction_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                _automation.ProcessFaction(null)
            );

            Assert.AreEqual("faction", exception.ParamName);
        }

        [Test]
        public void ProcessFaction_SupportBelowConfiguredThreshold_UsesConfiguredGarrisonTarget()
        {
            _faction.ManageProduction = false;
            _destination.SetPopularSupport(
                _faction.InstanceID,
                _game.Config.AI.Garrison.SupportThreshold - 1
            );
            AddCompletedRegiment(_destination, "DESTINATION_DEFENSE");

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(2, _destination.GetAllRegiments().Count);
            Assert.AreEqual(
                ManufacturingStatus.Building,
                _destination
                    .GetAllRegiments()
                    .Single(regiment =>
                        regiment.ManufacturingStatus == ManufacturingStatus.Building
                    )
                    .ManufacturingStatus
            );
        }

        [Test]
        public void ProcessFaction_SupportAtConfiguredThreshold_UsesOneRegimentGarrisonTarget()
        {
            _faction.ManageProduction = false;
            _destination.SetPopularSupport(
                _faction.InstanceID,
                _game.Config.AI.Garrison.SupportThreshold
            );
            AddCompletedRegiment(_destination, "DESTINATION_DEFENSE");

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, _destination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_Uprising_DoublesSupportGarrisonTarget()
        {
            _faction.ManageProduction = false;
            _destination.IsInUprising = true;
            AddCompletedRegiment(_destination, "DESTINATION_DEFENSE");

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(2, _destination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_ConfiguredGarrisonDivisor_UsesConfiguredValue()
        {
            _faction.ManageProduction = false;
            _game.Config.AI.Garrison.GarrisonDivisor = 5;
            _destination.SetPopularSupport(_faction.InstanceID, 40);
            AddCompletedRegiments(_destination, "DESTINATION_DEFENSE", 2);

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(3, _destination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_ConfiguredUprisingMultiplier_UsesConfiguredValue()
        {
            _faction.ManageProduction = false;
            _game.Config.AI.Garrison.UprisingMultiplier = 3;
            _destination.IsInUprising = true;
            AddCompletedRegiments(_destination, "DESTINATION_DEFENSE", 2);

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(3, _destination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_ConfiguredOrderQuantity_QueuesConfiguredQuantity()
        {
            _faction.ManageProduction = false;
            _game.Config.AI.Garrison.AutomatedOrderQuantity = 2;

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(2, _destination.GetAllRegiments().Count);
            Assert.AreEqual(2, GetQueueCount(_producer, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_ConfiguredMinimumGarrisonTarget_UsesConfiguredValue()
        {
            _faction.ManageProduction = false;
            _game.Config.AI.Garrison.MinimumGarrisonTarget = 2;
            AddCompletedRegiment(_destination, "DESTINATION_DEFENSE");

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(2, _destination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_ConfiguredResourceFacilitiesPerGarrison_UsesConfiguredValue()
        {
            _faction.ManageProduction = false;
            _game.Config.AI.Garrison.ResourceFacilitiesPerGarrison = 1;
            AddResourceFacility(_destination, "DESTINATION_MINE", BuildingType.Mine);
            AddResourceFacility(_destination, "DESTINATION_REFINERY", BuildingType.Refinery);
            AddCompletedRegiment(_destination, "DESTINATION_DEFENSE");

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(2, _destination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_FacilityTargetExceedsSupportTarget_UsesFacilityTarget()
        {
            _faction.ManageProduction = false;
            AddResourceFacility(_destination, "DESTINATION_MINE_1", BuildingType.Mine);
            AddResourceFacility(_destination, "DESTINATION_MINE_2", BuildingType.Mine);
            AddResourceFacility(_destination, "DESTINATION_REFINERY", BuildingType.Refinery);
            AddStrategicFacility(_destination, "DESTINATION_SHIPYARD", BuildingType.Shipyard);
            AddStrategicFacility(
                _destination,
                "DESTINATION_TRAINING",
                BuildingType.TrainingFacility
            );
            AddStrategicFacility(
                _destination,
                "DESTINATION_CONSTRUCTION",
                BuildingType.ConstructionFacility
            );
            AddCompletedRegiments(_destination, "DESTINATION_DEFENSE", 3);

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(4, _destination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_UnfinishedFacilitiesIncreaseTarget_CountsPendingFacilities()
        {
            _faction.ManageProduction = false;
            AddStrategicFacility(
                _destination,
                "PENDING_MINE",
                BuildingType.Mine,
                ManufacturingStatus.Building
            );
            AddStrategicFacility(
                _destination,
                "PENDING_REFINERY",
                BuildingType.Refinery,
                ManufacturingStatus.Building
            );
            AddStrategicFacility(
                _destination,
                "PENDING_CONSTRUCTION",
                BuildingType.ConstructionFacility,
                ManufacturingStatus.Building
            );
            AddCompletedRegiment(_destination, "DESTINATION_DEFENSE");

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(2, _destination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_PendingRegimentMeetsTarget_DoesNotQueueAnotherRegiment()
        {
            _faction.ManageProduction = false;
            AddPendingRegiment(_destination, "PENDING_DEFENSE");

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, _destination.GetAllRegiments().Count);
            Assert.AreEqual(0, GetQueueCount(_producer, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_TravelingRegimentDoesNotMeetTarget_QueuesRegiment()
        {
            _faction.ManageProduction = false;
            Regiment traveling = AddCompletedRegiment(_destination, "TRAVELING_DEFENSE");
            traveling.Movement = new MovementState();

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(2, _destination.GetAllRegiments().Count);
            Assert.AreEqual(
                1,
                _destination
                    .GetAllRegiments()
                    .Count(regiment => regiment.ManufacturingStatus == ManufacturingStatus.Building)
            );
        }

        [Test]
        public void ProcessFaction_BlockadedShortage_DoesNotQueueRegiment()
        {
            _faction.ManageProduction = false;
            AddBlockadingFleet(_destination);

            _automation.ProcessFaction(_faction);

            Assert.IsEmpty(_destination.GetAllRegiments());
            Assert.AreEqual(0, GetQueueCount(_producer, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_UprisingShortageExists_PrioritizesUprising()
        {
            _faction.ManageProduction = false;
            Planet uprising = CreatePlanet("UPRISING", 10, 0);
            uprising.IsInUprising = true;
            _game.AttachNode(uprising, _destination.GetParent());

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, uprising.GetAllRegiments().Count);
            Assert.IsEmpty(_destination.GetAllRegiments());
        }

        [Test]
        public void ProcessFaction_UprisingSectorsDiffer_PrioritizesGreatestAggregateShortage()
        {
            _faction.ManageProduction = false;
            SatisfyGarrison(_destination);
            PlanetSector largerShortageSector = CreateSector("LARGER_SHORTAGE", 20, 0);
            Planet first = CreateUprisingPlanet("FIRST", largerShortageSector, 50);
            Planet second = CreateUprisingPlanet("SECOND", largerShortageSector, 40);
            PlanetSector smallerShortageSector = CreateSector("SMALLER_SHORTAGE", 10, 0);
            Planet third = CreateUprisingPlanet("THIRD", smallerShortageSector, 40);
            AddCompletedRegiments(second, "SECOND_DEFENSE", 1);

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, first.GetAllRegiments().Count);
            Assert.AreEqual(1, second.GetAllRegiments().Count);
            Assert.IsEmpty(third.GetAllRegiments());
        }

        [Test]
        public void ProcessFaction_UprisingPlanetsDiffer_PrioritizesSmallestIndividualShortage()
        {
            _faction.ManageProduction = false;
            SatisfyGarrison(_destination);
            PlanetSector sector = CreateSector("UPRISING_SECTOR", 20, 0);
            Planet smallerShortage = CreateUprisingPlanet("SMALLER_SHORTAGE", sector, 60);
            Planet largerShortage = CreateUprisingPlanet("LARGER_SHORTAGE", sector, 30);

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, smallerShortage.GetAllRegiments().Count);
            Assert.IsEmpty(largerShortage.GetAllRegiments());
        }

        [Test]
        public void ProcessFaction_NormalSectorsDiffer_PrioritizesSectorWithFewestControlledPlanets()
        {
            _faction.ManageProduction = false;
            SatisfyGarrison(_destination);
            PlanetSector smallerSector = CreateSector("SMALLER_SECTOR", 20, 0);
            Planet smallerSectorShortage = CreatePlanet("SMALLER_SECTOR_SHORTAGE", 10, 0);
            _game.AttachNode(smallerSectorShortage, smallerSector);
            PlanetSector largerSector = CreateSector("LARGER_SECTOR", 10, 0);
            Planet largerSectorShortage = CreatePlanet("LARGER_SECTOR_SHORTAGE", 10, 0);
            Planet largerSectorSatisfied = CreatePlanet("LARGER_SECTOR_SATISFIED", 10, 0);
            _game.AttachNode(largerSectorShortage, largerSector);
            _game.AttachNode(largerSectorSatisfied, largerSector);
            AddCompletedRegiment(largerSectorSatisfied, "LARGER_SECTOR_DEFENSE");

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, smallerSectorShortage.GetAllRegiments().Count);
            Assert.IsEmpty(largerSectorShortage.GetAllRegiments());
        }

        [Test]
        public void ProcessFaction_NormalPlanetsDiffer_PrioritizesGreatestIndividualShortage()
        {
            _faction.ManageProduction = false;
            SatisfyGarrison(_destination);
            Planet smallerShortage = CreatePlanet("SMALLER_SHORTAGE", 10, 0);
            Planet largerShortage = CreatePlanet("LARGER_SHORTAGE", 10, 0);
            largerShortage.SetPopularSupport(_faction.InstanceID, 30);
            _game.AttachNode(smallerShortage, _destination.GetParent());
            _game.AttachNode(largerShortage, _destination.GetParent());

            _automation.ProcessFaction(_faction);

            Assert.IsEmpty(smallerShortage.GetAllRegiments());
            Assert.AreEqual(1, largerShortage.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_EqualPriorityDestinations_UsesGameRandom()
        {
            _faction.ManageProduction = false;
            _game.Random = new MaximumRNG();
            Planet second = CreatePlanet("SECOND", 10, 0);
            _game.AttachNode(second, _destination.GetParent());

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, second.GetAllRegiments().Count);
            Assert.IsEmpty(_destination.GetAllRegiments());
        }

        [Test]
        public void ProcessFaction_ConfiguredTieBreakRollRange_UsesConfiguredBounds()
        {
            _faction.ManageProduction = false;
            RecordingMinimumRNG random = new RecordingMinimumRNG();
            _game.Random = random;
            _game.Config.AI.Garrison.TieBreakRollMinimum = 7;
            _game.Config.AI.Garrison.TieBreakRollMaximum = 8;
            _game.Config.AI.Garrison.TieBreakReplacementThreshold = 7;
            Planet second = CreatePlanet("SECOND", 10, 0);
            _game.AttachNode(second, _destination.GetParent());

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, second.GetAllRegiments().Count);
            Assert.AreEqual(7, random.LastMinimum);
            Assert.AreEqual(8, random.LastMaximum);
        }

        [Test]
        public void ProcessFaction_ConfiguredTieBreakReplacementThreshold_UsesConfiguredValue()
        {
            _faction.ManageProduction = false;
            _game.Random = new MaximumRNG();
            _game.Config.AI.Garrison.TieBreakReplacementThreshold = 10;
            Planet second = CreatePlanet("SECOND", 10, 0);
            _game.AttachNode(second, _destination.GetParent());

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, _destination.GetAllRegiments().Count);
            Assert.IsEmpty(second.GetAllRegiments());
        }

        [Test]
        public void ProcessFaction_ProducersInDifferentSectors_UsesClosestEligibleSector()
        {
            _faction.ManageProduction = false;
            RemoveProductionFacility(_producer, "TRAINING");
            PlanetSector destinationSector = (PlanetSector)_destination.GetParent();
            destinationSector.PositionX = 0;
            PlanetSector nearbySector = CreateSector("NEARBY", 10, 0);
            PlanetSector distantSector = CreateSector("DISTANT", 100, 0);
            Planet nearbyProducer = CreateProducer("NEARBY_PRODUCER", nearbySector, 100);
            Planet distantProducer = CreateProducer("DISTANT_PRODUCER", distantSector, 100);

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, GetQueueCount(nearbyProducer, ManufacturingType.Troop));
            Assert.AreEqual(0, GetQueueCount(distantProducer, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_ProducersShareClosestSector_UsesLowestSupportProducer()
        {
            _faction.ManageProduction = false;
            RemoveProductionFacility(_producer, "TRAINING");
            PlanetSector sector = CreateSector("PRODUCERS", 10, 0);
            Planet highSupport = CreateProducer("HIGH_SUPPORT", sector, 100);
            Planet lowSupport = CreateProducer("LOW_SUPPORT", sector, 70);

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(0, GetQueueCount(highSupport, ManufacturingType.Troop));
            Assert.AreEqual(1, GetQueueCount(lowSupport, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_CloserProducersAreIneligible_UsesEligibleProducer()
        {
            _faction.ManageProduction = false;
            RemoveProductionFacility(_producer, "TRAINING");
            PlanetSector closeSector = CreateSector("CLOSE", 1, 0);
            Planet blockaded = CreateProducer("BLOCKADED", closeSector, 100);
            AddBlockadingFleet(blockaded);
            Planet uprising = CreateProducer("UPRISING", closeSector, 100);
            uprising.IsInUprising = true;
            Planet reserved = CreateProducer("RESERVED", closeSector, 100);
            reserved.SetManufacturingReserved(ManufacturingType.Troop, true);
            Planet busy = CreateProducer("BUSY", closeSector, 100);
            Regiment activeOrder = AddPendingRegiment(busy, "BUSY_ORDER");
            busy.AddToManufacturingQueue(activeOrder);
            PlanetSector distantSector = CreateSector("DISTANT", 50, 0);
            Planet eligible = CreateProducer("ELIGIBLE", distantSector, 100);

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, GetQueueCount(eligible, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_NormalDestination_SelectsHighestDefenseRegiment()
        {
            _faction.ManageProduction = false;

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(_strongDefenderTypeId, _destination.GetAllRegiments().Single().TypeID);
        }

        [Test]
        public void ProcessFaction_UprisingDestination_SelectsLowestMaintenanceRegiment()
        {
            _faction.ManageProduction = false;
            _destination.IsInUprising = true;

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(_cheapDefenderTypeId, _destination.GetAllRegiments().Single().TypeID);
        }

        [Test]
        public void ProcessFaction_OffensiveAndLockedRegiments_IgnoresIneligibleRegiments()
        {
            _faction.ManageProduction = false;

            _automation.ProcessFaction(_faction);

            Assert.AreNotEqual(
                _offensiveRegimentTypeId,
                _destination.GetAllRegiments().Single().TypeID
            );
            Assert.AreNotEqual("locked-defender", _destination.GetAllRegiments().Single().TypeID);
        }

        [Test]
        public void ProcessFaction_SelectedRegimentExceedsHeadroom_DoesNotUseCheaperFallback()
        {
            _faction.ManageProduction = false;
            GetRegimentTemplate(_strongDefenderTypeId).MaintenanceCost = 100;

            _automation.ProcessFaction(_faction);

            Assert.IsEmpty(_destination.GetAllRegiments());
            Assert.AreEqual(0, GetQueueCount(_producer, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_NoMaintenanceHeadroom_DoesNotQueueRegiment()
        {
            _faction.ManageProduction = false;
            _faction.Settings.ResourceProcessingPointsPerFacility = 0;

            _automation.ProcessFaction(_faction);

            Assert.IsEmpty(_destination.GetAllRegiments());
            Assert.AreEqual(0, GetQueueCount(_producer, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_ZeroCostRegimentAtZeroHeadroom_QueuesRegiment()
        {
            _faction.ManageProduction = false;
            _faction.Settings.ResourceProcessingPointsPerFacility = 0;
            GetRegimentTemplate(_strongDefenderTypeId).MaintenanceCost = 0;

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(_strongDefenderTypeId, _destination.GetAllRegiments().Single().TypeID);
            Assert.AreEqual(1, GetQueueCount(_producer, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_NoManufacturableDefensiveRegiment_DoesNotQueueRegiment()
        {
            _faction.ManageProduction = false;
            foreach (Regiment regiment in _gameData.Regiments)
                regiment.ManufacturingFactionInstanceIDs.Clear();

            _automation.ProcessFaction(_faction);

            Assert.IsEmpty(_destination.GetAllRegiments());
            Assert.AreEqual(0, GetQueueCount(_producer, ManufacturingType.Troop));
        }

        [Test]
        public void ProcessFaction_MultipleIdleTrainingFacilities_QueuesOneRegiment()
        {
            _faction.ManageProduction = false;
            AddProductionFacility(_producer, "TRAINING_2", ManufacturingType.Troop);
            AddCompletedRegiment(_producer, "PRODUCER_DEFENSE_EXTRA");

            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, GetQueueCount(_producer, ManufacturingType.Troop));
            Assert.AreEqual(1, _destination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessFaction_CompletedAutomatedRegiments_RefillsEveryLaneImmediately()
        {
            _faction.ManageProduction = false;
            _destination.SetPopularSupport(_faction.InstanceID, 10);
            Planet secondProducer = CreateProducer(
                "PRODUCER_2",
                _producer.GetParentOfType<PlanetSector>(),
                100
            );

            _automation.ProcessFaction(_faction);
            new ManufacturingTickProcessor(_manufacturing, _movement).ProcessTick(_game);
            _automation.ProcessFaction(_faction);

            Assert.AreEqual(1, GetQueueCount(_producer, ManufacturingType.Troop));
            Assert.AreEqual(1, GetQueueCount(secondProducer, ManufacturingType.Troop));
            Assert.AreEqual(0, _producer.GetIdleManufacturingFacilities(ManufacturingType.Troop));
            Assert.AreEqual(
                0,
                secondProducer.GetIdleManufacturingFacilities(ManufacturingType.Troop)
            );
        }

        [Test]
        public void ProcessFaction_CompletedAutomatedBuildings_RefillsEveryLaneImmediately()
        {
            _faction.ManageGarrisons = false;
            AddProductionInfrastructure();

            _automation.ProcessFaction(_faction);
            new ManufacturingTickProcessor(_manufacturing, _movement).ProcessTick(_game);
            _automation.ProcessFaction(_faction);

            Assert.AreEqual(2, GetQueueCount(_producer, ManufacturingType.Building));
            Assert.AreEqual(
                0,
                _producer.GetIdleManufacturingFacilities(ManufacturingType.Building)
            );
        }

        [Test]
        public void ProcessFaction_ReservedTrainingFacility_DoesNotQueueWork()
        {
            _faction.ManageProduction = false;
            _producer.SetManufacturingReserved(ManufacturingType.Troop, true);

            _automation.ProcessFaction(_faction);

            Assert.IsEmpty(_destination.GetAllRegiments());
        }

        [Test]
        public void ProcessTick_ManageProduction_FillsLaneWithOneProject()
        {
            _faction.ManageGarrisons = false;
            AddProductionInfrastructure();

            new FactionAutomationTickProcessor(_automation).ProcessTick(_game);

            Assert.AreEqual(12, CountResourceFacilities(BuildingType.Mine));
            Assert.AreEqual(10, CountResourceFacilities(BuildingType.Refinery));
        }

        [Test]
        public void ProcessTick_ManageProduction_UsesClosestAvailableResourceSlot()
        {
            _faction.ManageGarrisons = false;
            AddProductionInfrastructure();
            _destination.PositionX = 1;
            Planet distant = CreatePlanet("DISTANT", 50, 50);
            distant.PositionX = 100;
            _game.AttachNode(distant, _destination.GetParent());

            new FactionAutomationTickProcessor(_automation).ProcessTick(_game);

            Assert.AreEqual(2, _destination.GetTotalBuildingTypeCount(BuildingType.Mine));
            Assert.AreEqual(0, distant.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        [Test]
        public void ProcessTick_ManageProduction_UsesOwnedUncolonizedResourceDestination()
        {
            _faction.ManageGarrisons = false;
            AddProductionInfrastructure();
            _destination.IsColonized = false;
            AddCompletedRegiment(_destination, "DESTINATION_GARRISON");

            new FactionAutomationTickProcessor(_automation).ProcessTick(_game);

            Assert.AreEqual(2, _destination.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        [Test]
        public void ProcessTick_ManageProductionWithReservedBuildingLane_DoesNotQueueWork()
        {
            _faction.ManageGarrisons = false;
            AddProductionInfrastructure();
            _producer.SetManufacturingReserved(ManufacturingType.Building, true);
            int mineCount = CountResourceFacilities(BuildingType.Mine);
            int refineryCount = CountResourceFacilities(BuildingType.Refinery);

            new FactionAutomationTickProcessor(_automation).ProcessTick(_game);

            Assert.AreEqual(mineCount, CountResourceFacilities(BuildingType.Mine));
            Assert.AreEqual(refineryCount, CountResourceFacilities(BuildingType.Refinery));
        }

        [Test]
        public void ProcessTick_ReservedDestination_RemainsAvailableForAutomatedDelivery()
        {
            _faction.ManageGarrisons = false;
            AddProductionInfrastructure();
            _destination.SetManufacturingReserved(ManufacturingType.Building, true);
            _destination.PositionX = 1;

            new FactionAutomationTickProcessor(_automation).ProcessTick(_game);

            Assert.AreEqual(2, _destination.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        [Test]
        public void ProcessTick_ManageProductionWithoutMineCapacity_DoesNotAddRefinery()
        {
            _faction.ManageGarrisons = false;
            AddProductionInfrastructure();
            _destination.NumRawResourceNodes = 0;
            int refineryCount = CountResourceFacilities(BuildingType.Refinery);

            new FactionAutomationTickProcessor(_automation).ProcessTick(_game);

            Assert.AreEqual(refineryCount, CountResourceFacilities(BuildingType.Refinery));
        }

        [Test]
        public void ProcessTick_DisabledAutomation_DoesNotQueueWork()
        {
            _faction.ManageGarrisons = false;
            _faction.ManageProduction = false;

            new FactionAutomationTickProcessor(_automation).ProcessTick(_game);

            Assert.IsEmpty(_destination.GetAllRegiments());
            Assert.AreEqual(0, _destination.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        /// <summary>
        /// Creates planet.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="energy">The energy.</param>
        /// <param name="resources">The resources.</param>
        /// <returns>The created planet.</returns>
        private Planet CreatePlanet(string instanceId, int energy, int resources)
        {
            Planet planet = new Planet
            {
                InstanceID = instanceId,
                OwnerInstanceID = _faction.InstanceID,
                IsColonized = true,
                EnergyCapacity = energy,
                NumRawResourceNodes = resources,
            };
            planet.SetFullPopularSupport(_faction.InstanceID);
            return planet;
        }

        /// <summary>
        /// Creates and attaches a planet sector at the supplied galactic coordinates.
        /// </summary>
        /// <param name="instanceId">The sector identifier.</param>
        /// <param name="positionX">The horizontal coordinate.</param>
        /// <param name="positionY">The vertical coordinate.</param>
        /// <returns>The attached sector.</returns>
        private PlanetSector CreateSector(string instanceId, int positionX, int positionY)
        {
            PlanetSector sector = new PlanetSector
            {
                InstanceID = instanceId,
                PositionX = positionX,
                PositionY = positionY,
            };
            _game.AttachNode(sector, _game.Galaxy);
            return sector;
        }

        /// <summary>
        /// Creates an owned uprising planet in a sector.
        /// </summary>
        /// <param name="instanceId">The planet identifier.</param>
        /// <param name="sector">The containing sector.</param>
        /// <param name="support">The controlling faction's popular support.</param>
        /// <returns>The attached uprising planet.</returns>
        private Planet CreateUprisingPlanet(string instanceId, PlanetSector sector, int support)
        {
            Planet planet = CreatePlanet(instanceId, 10, 0);
            planet.SetPopularSupport(_faction.InstanceID, support);
            planet.IsInUprising = true;
            _game.AttachNode(planet, sector);
            return planet;
        }

        /// <summary>
        /// Creates an eligible troop producer and satisfies its own garrison requirement.
        /// </summary>
        /// <param name="instanceId">The planet identifier.</param>
        /// <param name="sector">The containing sector.</param>
        /// <param name="support">The controlling faction's popular support.</param>
        /// <returns>The attached producer.</returns>
        private Planet CreateProducer(string instanceId, PlanetSector sector, int support)
        {
            Planet planet = CreatePlanet(instanceId, 10, 0);
            planet.SetPopularSupport(_faction.InstanceID, support);
            _game.AttachNode(planet, sector);
            AddProductionFacility(planet, $"{instanceId}_TRAINING", ManufacturingType.Troop);
            SatisfyGarrison(planet);
            return planet;
        }

        /// <summary>
        /// Adds the construction capacity and resource facilities used by production tests.
        /// </summary>
        private void AddProductionInfrastructure()
        {
            AddProductionFacility(_producer, "CONSTRUCTION", ManufacturingType.Building);
            AddProductionFacility(_producer, "CONSTRUCTION_2", ManufacturingType.Building);
            for (int index = 1; index < 10; index++)
            {
                AddResourceFacility(_producer, $"MINE_{index}", BuildingType.Mine);
                AddResourceFacility(_producer, $"REFINERY_{index}", BuildingType.Refinery);
            }
        }

        /// <summary>
        /// Adds ample stationary regiments so a setup planet cannot be selected as a shortage.
        /// </summary>
        /// <param name="planet">The planet to satisfy.</param>
        private void SatisfyGarrison(Planet planet)
        {
            AddCompletedRegiments(planet, $"{planet.InstanceID}_DEFENSE", 20);
        }

        /// <summary>
        /// Creates game config.
        /// </summary>
        /// <returns>The created game config.</returns>
        private static GameConfig CreateGameConfig()
        {
            GameConfig config = new GameConfig();
            config.Movement.DistanceDivisor = 1;
            config.AI.Garrison.SupportThreshold = 50;
            config.AI.Garrison.GarrisonDivisor = 10;
            config.AI.Garrison.UprisingMultiplier = 2;
            config.AI.Garrison.AutomatedOrderQuantity = 1;
            config.AI.Garrison.MinimumGarrisonTarget = 1;
            config.AI.Garrison.ResourceFacilitiesPerGarrison = 2;
            config.AI.Garrison.TieBreakRollMinimum = 0;
            config.AI.Garrison.TieBreakRollMaximum = 10;
            config.AI.Garrison.TieBreakReplacementThreshold = 5;
            return config;
        }

        /// <summary>
        /// Creates game data.
        /// </summary>
        /// <param name="config">The config.</param>
        /// <returns>The created game data.</returns>
        private static GameDataCatalog CreateGameData(GameConfig config)
        {
            GameGenerationConfig generationConfig = new GameGenerationConfig();
            List<string> manufacturingFactionIds = new List<string> { _factionId };
            Building mine = new Building
            {
                TypeID = "mine",
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
                ManufacturingFactionInstanceIDs = manufacturingFactionIds,
            };
            Building refinery = new Building
            {
                TypeID = "refinery",
                BuildingType = BuildingType.Refinery,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
                ManufacturingFactionInstanceIDs = manufacturingFactionIds,
            };
            Regiment strongDefender = new Regiment
            {
                TypeID = _strongDefenderTypeId,
                ConstructionCost = 1,
                MaintenanceCost = 4,
                BaseBuildSpeed = 1,
                AttackRating = 4,
                DefenseRating = 8,
                ManufacturingFactionInstanceIDs = manufacturingFactionIds,
            };
            Regiment cheapDefender = new Regiment
            {
                TypeID = _cheapDefenderTypeId,
                ConstructionCost = 1,
                MaintenanceCost = 1,
                BaseBuildSpeed = 1,
                AttackRating = 2,
                DefenseRating = 4,
                ManufacturingFactionInstanceIDs = manufacturingFactionIds,
            };
            Regiment offensiveRegiment = new Regiment
            {
                TypeID = _offensiveRegimentTypeId,
                ConstructionCost = 1,
                MaintenanceCost = 1,
                BaseBuildSpeed = 1,
                AttackRating = 10,
                DefenseRating = 9,
                ManufacturingFactionInstanceIDs = manufacturingFactionIds,
            };
            Regiment lockedDefender = new Regiment
            {
                TypeID = "locked-defender",
                ConstructionCost = 1,
                MaintenanceCost = 1,
                BaseBuildSpeed = 1,
                AttackRating = 1,
                DefenseRating = 20,
                ResearchOrder = 1,
                ManufacturingFactionInstanceIDs = manufacturingFactionIds,
            };
            return new GameDataCatalog(
                config,
                generationConfig,
                new[]
                {
                    new Faction
                    {
                        InstanceID = _factionId,
                        GarrisonTroopTypeID = _strongDefenderTypeId,
                    },
                },
                Array.Empty<PlanetSector>(),
                Array.Empty<BattleMap>(),
                new[] { mine, refinery },
                Array.Empty<CapitalShip>(),
                Array.Empty<Starfighter>(),
                new[] { strongDefender, cheapDefender, offensiveRegiment, lockedDefender },
                Array.Empty<SpecialForces>(),
                Array.Empty<Officer>(),
                Array.Empty<GameEvent>(),
                Array.Empty<MessageDefinition>(),
                new EncyclopediaEntries(),
                new FactionThemes()
            );
        }

        /// <summary>
        /// Adds production facility.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="type">The type.</param>
        private void AddProductionFacility(Planet planet, string instanceId, ManufacturingType type)
        {
            _game.AttachNode(
                new Building
                {
                    InstanceID = instanceId,
                    OwnerInstanceID = _faction.InstanceID,
                    BuildingType =
                        type == ManufacturingType.Troop
                            ? BuildingType.TrainingFacility
                            : BuildingType.ConstructionFacility,
                    ProductionType = type,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
        }

        /// <summary>
        /// Adds resource pairs.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="count">The count.</param>
        private void AddResourcePairs(Planet planet, int count)
        {
            for (int index = 0; index < count; index++)
            {
                AddResourceFacility(planet, $"MINE_{index}", BuildingType.Mine);
                AddResourceFacility(planet, $"REFINERY_{index}", BuildingType.Refinery);
            }
        }

        /// <summary>
        /// Adds completed regiment.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <returns>The attached regiment.</returns>
        private Regiment AddCompletedRegiment(Planet planet, string instanceId)
        {
            Regiment regiment = new Regiment
            {
                InstanceID = instanceId,
                OwnerInstanceID = _faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(regiment, planet);
            return regiment;
        }

        /// <summary>
        /// Adds stationary completed regiments to a planet.
        /// </summary>
        /// <param name="planet">The planet receiving the regiments.</param>
        /// <param name="instanceIdPrefix">The identifier prefix.</param>
        /// <param name="count">The number of regiments to add.</param>
        private void AddCompletedRegiments(Planet planet, string instanceIdPrefix, int count)
        {
            for (int index = 0; index < count; index++)
                AddCompletedRegiment(planet, $"{instanceIdPrefix}_{index}");
        }

        /// <summary>
        /// Adds an unfinished regiment order directly to its destination.
        /// </summary>
        /// <param name="planet">The destination planet.</param>
        /// <param name="instanceId">The regiment identifier.</param>
        /// <returns>The attached regiment.</returns>
        private Regiment AddPendingRegiment(Planet planet, string instanceId)
        {
            Regiment regiment = new Regiment
            {
                InstanceID = instanceId,
                OwnerInstanceID = _faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            _game.AttachNode(regiment, planet);
            return regiment;
        }

        /// <summary>
        /// Adds a completed strategic facility to a planet.
        /// </summary>
        /// <param name="planet">The planet receiving the facility.</param>
        /// <param name="instanceId">The facility identifier.</param>
        /// <param name="buildingType">The facility type.</param>
        /// <param name="status">The facility's manufacturing status.</param>
        private void AddStrategicFacility(
            Planet planet,
            string instanceId,
            BuildingType buildingType,
            ManufacturingStatus status = ManufacturingStatus.Complete
        )
        {
            _game.AttachNode(
                new Building
                {
                    InstanceID = instanceId,
                    OwnerInstanceID = _faction.InstanceID,
                    BuildingType = buildingType,
                    ManufacturingStatus = status,
                },
                planet
            );
        }

        /// <summary>
        /// Removes a named production facility from a planet.
        /// </summary>
        /// <param name="planet">The planet containing the facility.</param>
        /// <param name="instanceId">The facility identifier.</param>
        private void RemoveProductionFacility(Planet planet, string instanceId)
        {
            Building facility = planet
                .GetChildren<Building>()
                .Single(building => building.InstanceID == instanceId);
            _game.DetachNode(facility);
        }

        /// <summary>
        /// Adds a stationary hostile fleet with an operational capital ship.
        /// </summary>
        /// <param name="planet">The planet to blockade.</param>
        private void AddBlockadingFleet(Planet planet)
        {
            string owner = "enemy";
            Fleet fleet = new Fleet
            {
                InstanceID = $"{planet.InstanceID}_BLOCKADE",
                OwnerInstanceID = owner,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = $"{planet.InstanceID}_BLOCKADE_SHIP",
                OwnerInstanceID = owner,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(fleet, planet);
            _game.AttachNode(ship, fleet);
        }

        /// <summary>
        /// Returns a regiment template by identifier.
        /// </summary>
        /// <param name="typeId">The regiment type identifier.</param>
        /// <returns>The matching template.</returns>
        private Regiment GetRegimentTemplate(string typeId)
        {
            return _gameData.Regiments.Single(regiment => regiment.TypeID == typeId);
        }

        /// <summary>
        /// Returns the number of active orders in one manufacturing lane.
        /// </summary>
        /// <param name="planet">The producer planet.</param>
        /// <param name="manufacturingType">The manufacturing lane.</param>
        /// <returns>The active order count.</returns>
        private static int GetQueueCount(Planet planet, ManufacturingType manufacturingType)
        {
            return planet
                .GetManufacturingQueue()
                .TryGetValue(manufacturingType, out List<IManufacturable> queue)
                ? queue.Count
                : 0;
        }

        /// <summary>
        /// Adds resource facility.
        /// </summary>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="buildingType">The building type.</param>
        private void AddResourceFacility(
            Planet planet,
            string instanceId,
            BuildingType buildingType
        )
        {
            _game.AttachNode(
                new Building
                {
                    InstanceID = instanceId,
                    OwnerInstanceID = _faction.InstanceID,
                    BuildingType = buildingType,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
        }

        /// <summary>
        /// Executes count resource facilities.
        /// </summary>
        /// <param name="buildingType">The building type.</param>
        /// <returns>The result of count resource facilities.</returns>
        private int CountResourceFacilities(BuildingType buildingType)
        {
            return new[] { _producer, _destination }.Sum(planet =>
                planet.GetTotalBuildingTypeCount(buildingType)
            );
        }

        private sealed class RecordingMinimumRNG : IRandomNumberProvider
        {
            public int LastMinimum { get; private set; }

            public int LastMaximum { get; private set; }

            /// <summary>
            /// Rejects unexpected floating-point random requests.
            /// </summary>
            /// <returns>This method does not return.</returns>
            public double NextDouble()
            {
                throw new InvalidOperationException("Unexpected floating-point random request.");
            }

            /// <summary>
            /// Records the requested range and returns its minimum value.
            /// </summary>
            /// <param name="min">The inclusive minimum.</param>
            /// <param name="max">The exclusive maximum.</param>
            /// <returns>The requested minimum.</returns>
            public int NextInt(int min, int max)
            {
                LastMinimum = min;
                LastMaximum = max;
                return min;
            }
        }
    }
}
