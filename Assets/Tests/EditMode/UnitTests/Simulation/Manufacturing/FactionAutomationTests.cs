using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation.ManufacturingTests
{
    [TestFixture]
    public class FactionAutomationTests
    {
        private const string _factionId = "faction";
        private const string _garrisonTypeId = "garrison";

        private GameRoot _game;
        private Faction _faction;
        private Planet _producer;
        private Planet _destination;
        private FactionAutomation _automation;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameConfig config = CreateGameConfig();
            GameDataCatalog gameData = CreateGameData(config);
            _game = new GameRoot(config);
            _faction = new Faction
            {
                InstanceID = _factionId,
                GarrisonTroopTypeID = _garrisonTypeId,
                ManageGarrisons = true,
                ManageProduction = true,
            };
            _faction.Settings.ResourceProcessingPointsPerFacility = 50;
            _game.GetFactions().Add(_faction);

            PlanetSector planetSector = new PlanetSector { InstanceID = "SECTOR" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _producer = CreatePlanet("PRODUCER", 100, 10);
            _destination = CreatePlanet("DESTINATION", 10, 10);
            _game.AttachNode(_producer, planetSector);
            _game.AttachNode(_destination, planetSector);

            AddProductionFacility(_producer, "TRAINING", ManufacturingType.Troop);
            AddProductionFacility(_producer, "CONSTRUCTION", ManufacturingType.Building);
            AddProductionFacility(_producer, "CONSTRUCTION_2", ManufacturingType.Building);
            AddResourcePairs(_producer, 10);

            Manufacturing manufacturing = new Manufacturing(_game, new Fleets(_game));
            _automation = new FactionAutomation(_game, gameData, manufacturing);
        }

        [Test]
        public void ProcessTick_ManageGarrisons_QueuesTroopForUnguardedPlanet()
        {
            _faction.ManageProduction = false;
            _destination.SetFullPopularSupport(_faction.InstanceID);
            AddCompletedRegiment(_producer, "GARRISON_1");
            AddCompletedRegiment(_producer, "GARRISON_2");

            _automation.ProcessTick();

            Assert.AreEqual(1, _destination.GetAllRegiments().Count);
            Assert.AreEqual(
                ManufacturingStatus.Building,
                _destination.GetAllRegiments().Single().ManufacturingStatus
            );
            Assert.AreEqual(_garrisonTypeId, _destination.GetAllRegiments().Single().TypeID);
        }

        [Test]
        public void ProcessTick_ManageGarrisons_PrioritizesUprising()
        {
            _faction.ManageProduction = false;
            AddCompletedRegiment(_producer, "GARRISON_1");
            AddCompletedRegiment(_producer, "GARRISON_2");
            Planet uprising = CreatePlanet("UPRISING", 10, 0);
            uprising.IsInUprising = true;
            _game.AttachNode(uprising, _destination.GetParent());

            _automation.ProcessTick();

            Assert.AreEqual(1, uprising.GetAllRegiments().Count);
            Assert.IsEmpty(_destination.GetAllRegiments());
        }

        [Test]
        public void ProcessTick_ManageGarrisons_FillsAvailableCapacityAcrossShortages()
        {
            _faction.ManageProduction = false;
            AddProductionFacility(_producer, "TRAINING_2", ManufacturingType.Troop);
            AddCompletedRegiment(_producer, "GARRISON_1");
            AddCompletedRegiment(_producer, "GARRISON_2");
            Planet secondDestination = CreatePlanet("SECOND_DESTINATION", 10, 0);
            _game.AttachNode(secondDestination, _destination.GetParent());

            _automation.ProcessTick();

            Assert.AreEqual(1, _destination.GetAllRegiments().Count);
            Assert.AreEqual(1, secondDestination.GetAllRegiments().Count);
        }

        [Test]
        public void ProcessTick_ManageGarrisonsWithReservedTrainingFacility_DoesNotQueueWork()
        {
            _faction.ManageProduction = false;
            _producer.SetManufacturingReserved(ManufacturingType.Troop, true);

            _automation.ProcessTick();

            Assert.IsEmpty(_destination.GetAllRegiments());
        }

        [Test]
        public void ProcessTick_ManageProduction_FillsLaneWithOneProject()
        {
            _faction.ManageGarrisons = false;

            _automation.ProcessTick();

            Assert.AreEqual(12, CountResourceFacilities(BuildingType.Mine));
            Assert.AreEqual(10, CountResourceFacilities(BuildingType.Refinery));
        }

        [Test]
        public void ProcessTick_ManageProduction_UsesClosestAvailableResourceSlot()
        {
            _faction.ManageGarrisons = false;
            _destination.PositionX = 1;
            Planet distant = CreatePlanet("DISTANT", 50, 50);
            distant.PositionX = 100;
            _game.AttachNode(distant, _destination.GetParent());

            _automation.ProcessTick();

            Assert.AreEqual(2, _destination.GetTotalBuildingTypeCount(BuildingType.Mine));
            Assert.AreEqual(0, distant.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        [Test]
        public void ProcessTick_ManageProductionWithReservedBuildingLane_DoesNotQueueWork()
        {
            _faction.ManageGarrisons = false;
            _producer.SetManufacturingReserved(ManufacturingType.Building, true);
            int mineCount = CountResourceFacilities(BuildingType.Mine);
            int refineryCount = CountResourceFacilities(BuildingType.Refinery);

            _automation.ProcessTick();

            Assert.AreEqual(mineCount, CountResourceFacilities(BuildingType.Mine));
            Assert.AreEqual(refineryCount, CountResourceFacilities(BuildingType.Refinery));
        }

        [Test]
        public void ProcessTick_ReservedDestination_RemainsAvailableForAutomatedDelivery()
        {
            _faction.ManageGarrisons = false;
            _destination.SetManufacturingReserved(ManufacturingType.Building, true);
            _destination.PositionX = 1;

            _automation.ProcessTick();

            Assert.AreEqual(2, _destination.GetTotalBuildingTypeCount(BuildingType.Mine));
        }

        [Test]
        public void ProcessTick_ManageProductionWithoutMineCapacity_DoesNotAddRefinery()
        {
            _faction.ManageGarrisons = false;
            _destination.NumRawResourceNodes = 0;
            int refineryCount = CountResourceFacilities(BuildingType.Refinery);

            _automation.ProcessTick();

            Assert.AreEqual(refineryCount, CountResourceFacilities(BuildingType.Refinery));
        }

        [Test]
        public void ProcessTick_DisabledAutomation_DoesNotQueueWork()
        {
            _faction.ManageGarrisons = false;
            _faction.ManageProduction = false;

            _automation.ProcessTick();

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
        /// Creates game config.
        /// </summary>
        /// <returns>The created game config.</returns>
        private static GameConfig CreateGameConfig()
        {
            GameConfig config = new GameConfig();
            config.AI.Garrison.SupportThreshold = 50;
            config.AI.Garrison.GarrisonDivisor = 10;
            config.AI.Garrison.UprisingMultiplier = 2;
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
            Regiment garrison = new Regiment
            {
                TypeID = _garrisonTypeId,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
                ManufacturingFactionInstanceIDs = manufacturingFactionIds,
            };
            return new GameDataCatalog(
                config,
                generationConfig,
                Array.Empty<Faction>(),
                Array.Empty<PlanetSector>(),
                new[] { mine, refinery },
                Array.Empty<CapitalShip>(),
                Array.Empty<Starfighter>(),
                new[] { garrison },
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
        private void AddCompletedRegiment(Planet planet, string instanceId)
        {
            _game.AttachNode(
                new Regiment
                {
                    InstanceID = instanceId,
                    OwnerInstanceID = _faction.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
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
    }
}
