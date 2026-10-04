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
            _game = TestGame.Create(config);
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
            _observer = new ManufacturingObserver(_game, _manager);
        }

        [Test]
        public void HandleResults_AssaultDestroysLastProducer_CancelsWithoutNewResults()
        {
            Building item = CreateOrderTestBuildingTemplate("mine");
            item.OwnerInstanceID = "EMPIRE";
            Assert.IsTrue(_manager.Enqueue(_coruscant, item, _coruscant));
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
            _manager.Enqueue(_coruscant, mine, _coruscant);
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
            _manager.Enqueue(_coruscant, mine, _coruscant);
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
            _manager.Enqueue(_coruscant, mine, _coruscant);
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
            _manager.Enqueue(_coruscant, mine, _coruscant);
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

        [Test]
        public void Connect_OwnershipChangePublished_ClearsDestinationPlanetQueues()
        {
            Building mine = CreateOrderTestBuildingTemplate("captured-mine");
            mine.OwnerInstanceID = _empire.InstanceID;
            Assert.IsTrue(_manager.Enqueue(_coruscant, mine, _coruscant));
            Faction alliance = CreateFaction("ALLIANCE");
            GameResultBus results = new GameResultBus();
            _observer.Connect(results);

            results.Publish(
                new PlanetOwnershipChangedResult
                {
                    Planet = _coruscant,
                    PreviousOwner = _empire,
                    NewOwner = alliance,
                }
            );

            Assert.IsEmpty(_coruscant.GetManufacturingQueue());
            Assert.IsNull(mine.GetParent());
        }

        [Test]
        public void HandleResults_IncompatibleRemoteOrder_CancelsOnlyCapturedDestinationOrder()
        {
            Planet destination = CreatePlanet("CAPTURED", _empire.InstanceID);
            Building remoteMine = CreateOrderTestBuildingTemplate("remote-mine");
            remoteMine.OwnerInstanceID = _empire.InstanceID;
            Building localMine = CreateOrderTestBuildingTemplate("local-mine");
            localMine.OwnerInstanceID = _empire.InstanceID;
            Assert.IsTrue(_manager.Enqueue(_coruscant, remoteMine, destination));
            Assert.IsTrue(_manager.Enqueue(_coruscant, localMine, _coruscant));

            _observer.HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = destination,
                        PreviousOwner = _empire,
                        NewOwner = CreateFaction("ALLIANCE"),
                    },
                }
            );

            List<IManufacturable> queue = _coruscant.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            CollectionAssert.AreEqual(new[] { localMine }, queue);
            Assert.IsNull(remoteMine.GetParent());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>(remoteMine.InstanceID));
        }

        [Test]
        public void HandleResults_CompatibleRemoteOrder_PreservesOrder()
        {
            Planet destination = CreatePlanet("CLAIMED", _empire.InstanceID);
            Building remoteMine = CreateOrderTestBuildingTemplate("friendly-mine");
            remoteMine.OwnerInstanceID = _empire.InstanceID;
            Assert.IsTrue(_manager.Enqueue(_coruscant, remoteMine, destination));

            _observer.HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = destination,
                        PreviousOwner = null,
                        NewOwner = _empire,
                    },
                }
            );

            CollectionAssert.Contains(
                _coruscant.GetManufacturingQueue()[ManufacturingType.Building],
                remoteMine
            );
            Assert.AreSame(destination, remoteMine.GetParent());
        }

        [Test]
        public void HandleResults_FleetDestinationAtCapturedPlanet_PreservesOrder()
        {
            Planet destination = CreatePlanet("CAPTURED", _empire.InstanceID);
            Building troopFacility = new Building
            {
                InstanceID = "TROOP_FACILITY",
                OwnerInstanceID = _empire.InstanceID,
                BuildingType = BuildingType.TrainingFacility,
                ProductionType = ManufacturingType.Troop,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(troopFacility, _coruscant);
            Fleet fleet = new Fleet(_empire.InstanceID, "Empire Fleet");
            CapitalShip transport = new CapitalShip
            {
                InstanceID = "TRANSPORT",
                OwnerInstanceID = _empire.InstanceID,
                RegimentCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(fleet, destination);
            _game.AttachNode(transport, fleet);
            Regiment regiment = new Regiment
            {
                InstanceID = "FLEET_REGIMENT",
                OwnerInstanceID = _empire.InstanceID,
                ConstructionCost = 100,
            };
            Assert.IsTrue(_manager.Enqueue(_coruscant, regiment, fleet));

            _observer.HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = destination,
                        PreviousOwner = _empire,
                        NewOwner = CreateFaction("ALLIANCE"),
                    },
                }
            );

            CollectionAssert.Contains(
                _coruscant.GetManufacturingQueue()[ManufacturingType.Troop],
                regiment
            );
            Assert.AreSame(fleet, regiment.GetParentOfType<Fleet>());
        }

        [Test]
        public void HandleResults_QueuedRegimentEvacuatedBeforeObservation_CancelsOrder()
        {
            Planet destination = CreatePlanet("CAPTURED", _empire.InstanceID);
            Building troopFacility = new Building
            {
                InstanceID = "TROOP_FACILITY",
                OwnerInstanceID = _empire.InstanceID,
                BuildingType = BuildingType.TrainingFacility,
                ProductionType = ManufacturingType.Troop,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(troopFacility, _coruscant);
            Regiment regiment = new Regiment
            {
                InstanceID = "REMOTE_REGIMENT",
                OwnerInstanceID = _empire.InstanceID,
                ConstructionCost = 100,
            };
            Assert.IsTrue(_manager.Enqueue(_coruscant, regiment, destination));
            Faction alliance = CreateFaction("ALLIANCE");
            PlanetaryControlCommands control = new PlanetaryControlCommands(
                _game,
                _movement,
                new FogOfWarCommands(_game),
                new PlanetaryControlQueries(_game),
                new FogOfWarQueries(_game)
            );

            PlanetOwnershipChangedResult result = control.TransferPlanet(destination, alliance);
            _observer.HandleResults(new[] { result });

            Assert.IsFalse(_coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Troop));
            Assert.IsNull(regiment.GetParent());
            Assert.IsNull(regiment.Movement);
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

        /// <summary>Creates and attaches a faction for an ownership-change test.</summary>
        /// <param name="instanceId">The faction instance identifier.</param>
        /// <returns>The attached faction.</returns>
        private Faction CreateFaction(string instanceId)
        {
            Faction faction = new Faction { InstanceID = instanceId };
            _game.GetFactions().Add(faction);
            return faction;
        }

        /// <summary>Creates a populated planet in the test sector.</summary>
        /// <param name="instanceId">The planet instance identifier.</param>
        /// <param name="ownerInstanceId">The optional owning faction identifier.</param>
        /// <returns>The attached planet.</returns>
        private Planet CreatePlanet(string instanceId, string ownerInstanceId)
        {
            Planet planet = new Planet
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerInstanceId,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            _game.AttachNode(planet, _coruscant.GetParent());
            return planet;
        }
    }
}
