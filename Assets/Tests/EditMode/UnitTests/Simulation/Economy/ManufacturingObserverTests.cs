using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class ManufacturingObserverTests
    {
        private GameRoot _game;
        private ManufacturingCommands _manager;
        private MovementCommands _movement;
        private Faction _empire;
        private Planet _coruscant;
        private Building _shipyard;

        private ManufacturingObserver _observer;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            // Create game with galaxy
            GameConfig config = TestContent.Data.GameConfig;
            _game = new GameRoot(config);
            GalaxyMap galaxy = _game.Galaxy;

            // Create faction
            _empire = new Faction { InstanceID = "EMPIRE" };
            _empire.RefinedMaterialStockpile = 1000;
            _game.GetFactions().Add(_empire);

            // Create planet sector
            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "SECTOR1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(planetSector, galaxy);

            // Create planet with resources
            _coruscant = new Planet
            {
                InstanceID = "CORUSCANT",
                OwnerInstanceID = "EMPIRE",
                PositionX = 0,
                PositionY = 0,
                NumRawResourceNodes = 100,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            _game.AttachNode(_coruscant, planetSector);

            // Create construction yard for production
            _shipyard = new Building
            {
                InstanceID = "SHIPYARD1",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_shipyard, _coruscant);

            _game.AttachNode(
                new Building
                {
                    InstanceID = "RESOURCE_MINE",
                    OwnerInstanceID = "EMPIRE",
                    BuildingType = BuildingType.Mine,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                _coruscant
            );
            _game.AttachNode(
                new Building
                {
                    InstanceID = "RESOURCE_REFINERY",
                    OwnerInstanceID = "EMPIRE",
                    BuildingType = BuildingType.Refinery,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                _coruscant
            );

            _movement = new MovementCommands(
                _game,
                new FogOfWarCommands(_game),
                new FleetCommands(_game),
                new FogOfWarQueries(_game),
                new MovementQueries(_game)
            );
            _manager = new ManufacturingCommands(
                _game,
                new FleetCommands(_game),
                new ManufacturingQueries(_game),
                _movement
            );
            _observer = new ManufacturingObserver(_manager);
        }

        [Test]
        public void HandleResults_AssaultDestroysLastProducer_CancelsWithoutNewResults()
        {
            Building item = CreateOrderTestBuildingTemplate("mine");
            item.OwnerInstanceID = "EMPIRE";
            Assert.IsTrue(_manager.Enqueue(_coruscant, item, _coruscant, ignoreCost: true));
            _game.DetachNode(_shipyard);

            List<GameResult> reactions = _observer.HandleResults(
                new PlanetaryAssaultResult[]
                {
                    null,
                    new PlanetaryAssaultResult
                    {
                        Planet = _coruscant,
                        CollateralDestroyedBuildings = new List<Building> { _shipyard },
                    },
                }
            );

            Assert.IsEmpty(reactions);
            Assert.IsNull(item.GetParent());
            Assert.IsFalse(
                _coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
        }

        [Test]
        public void HandleResults_LastProductionBuildingDestroyed_CancelsQueuedWork()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };
            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            _game.DetachNode(_shipyard);

            _observer.HandleResults(
                new List<GameObjectDestroyedResult>
                {
                    new GameObjectDestroyedResult
                    {
                        DestroyedObject = _shipyard,
                        Context = _coruscant,
                    },
                }
            );

            Assert.IsFalse(
                _coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
            Assert.IsNull(mine.GetParent());
        }

        [Test]
        public void HandleResults_LastProductionBuildingScrapped_CancelsQueuedWork()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };
            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            _game.DetachNode(_shipyard);

            _observer.HandleResults(
                new List<GameObjectScrappedResult>
                {
                    new GameObjectScrappedResult
                    {
                        ScrappedObject = _shipyard,
                        Context = _coruscant,
                    },
                }
            );

            Assert.IsFalse(
                _coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
            Assert.IsNull(mine.GetParent());
        }

        [Test]
        public void HandleResults_AnotherProductionBuildingSurvives_RetainsQueuedWork()
        {
            Building secondShipyard = new Building
            {
                InstanceID = "SHIPYARD2",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 4,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(secondShipyard, _coruscant);
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };
            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            _game.DetachNode(_shipyard);

            _observer.HandleResults(
                new List<GameObjectDestroyedResult>
                {
                    new GameObjectDestroyedResult
                    {
                        DestroyedObject = _shipyard,
                        Context = _coruscant,
                    },
                }
            );

            CollectionAssert.Contains(
                _coruscant.GetManufacturingQueue()[ManufacturingType.Building],
                mine
            );
            Assert.AreSame(_coruscant, mine.GetParent());
        }

        [Test]
        public void HandleResults_BombardmentDestroysLastProducer_CancelsQueuedWork()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };
            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            _game.DetachNode(_shipyard);

            _observer.HandleResults(
                new List<BombardmentResult>
                {
                    new BombardmentResult
                    {
                        Planet = _coruscant,
                        DestroyedBuildings = new List<Building> { _shipyard },
                    },
                }
            );

            Assert.IsFalse(
                _coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
            Assert.IsNull(mine.GetParent());
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
    }
}
