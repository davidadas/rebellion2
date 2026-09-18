using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class ManufacturingSystemTests
    {
        private GameRoot _game;
        private ManufacturingSystem _manager;
        private MovementSystem _movement;
        private Faction _empire;
        private Planet _coruscant;
        private Building _shipyard;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            // Create game with galaxy
            GameConfig config = TestConfig.Create();
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

            _movement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            _manager = new ManufacturingSystem(_game, new FleetSystem(_game), _movement);
        }

        /// <summary>
        /// Verifies constructor with null game throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            GameRoot dependencyGame = new GameRoot(TestConfig.Create());

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new ManufacturingSystem(null, new FleetSystem(dependencyGame))
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        /// <summary>
        /// Verifies constructor with null fleet system throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_WithNullFleetSystem_ThrowsArgumentNullException()
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new ManufacturingSystem(_game, null)
            );

            Assert.AreEqual("fleetSystem", exception.ParamName);
        }

        /// <summary>
        /// Verifies process tick empty game returns no results.
        /// </summary>
        [Test]
        public void ProcessTick_EmptyGame_ReturnsNoResults()
        {
            GameConfig config = TestConfig.Create();
            GameRoot emptyGame = new GameRoot(config);
            ManufacturingSystem emptyManager = new ManufacturingSystem(
                emptyGame,
                new FleetSystem(emptyGame),
                new MovementSystem(
                    emptyGame,
                    new FogOfWarSystem(emptyGame),
                    new FleetSystem(emptyGame)
                )
            );

            List<GameResult> results = emptyManager.ProcessTick();

            Assert.IsEmpty(results);
        }

        /// <summary>
        /// Verifies process tick single item advances progress.
        /// </summary>
        [Test]
        public void ProcessTick_SingleItem_AdvancesProgress()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            Assert.Greater(mine.ManufacturingProgress, 0);
            Assert.AreEqual(ManufacturingStatus.Building, mine.ManufacturingStatus);
            ManufacturingPointsCompletedResult progress = results
                .OfType<ManufacturingPointsCompletedResult>()
                .FirstOrDefault();
            Assert.IsNotNull(progress);
            Assert.AreEqual(_empire, progress.Faction);
            Assert.Greater(progress.Points, 0);
            Assert.AreEqual(_coruscant, progress.Context);
        }

        /// <summary>
        /// Verifies process tick building complete removes from queue.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_RemovesFromQueue()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1, // Minimal cost to ensure completion in one tick
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.AreEqual(0, queue[ManufacturingType.Building].Count);
            Assert.AreEqual(ManufacturingStatus.Complete, mine.ManufacturingStatus);
            Assert.AreEqual(0, mine.ManufacturingQueueSequence);
            ManufacturingDeployedResult deployed = results
                .OfType<ManufacturingDeployedResult>()
                .FirstOrDefault();
            Assert.IsNotNull(deployed);
            Assert.AreEqual(mine, deployed.DeployedObject);
            GameObjectCreatedResult created = results
                .OfType<GameObjectCreatedResult>()
                .FirstOrDefault();
            Assert.IsNotNull(created);
            Assert.AreEqual(mine, created.GameObject);
        }

        /// <summary>
        /// Verifies process tick building complete does not change popular support.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_DoesNotChangePopularSupport()
        {
            _coruscant.SetPopularSupport(_empire.InstanceID, 47);
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = _empire.InstanceID,
                ConstructionCost = 1,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            _manager.ProcessTick();

            Assert.AreEqual(47, _coruscant.GetPopularSupport(_empire.InstanceID));
        }

        /// <summary>
        /// Verifies process tick building complete sets status complete.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_SetsStatusComplete()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            Assert.AreEqual(ManufacturingStatus.Building, mine.ManufacturingStatus);

            _manager.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, mine.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies process tick building complete remains attached to parent.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_RemainsAttachedToParent()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            _manager.ProcessTick();

            // Building should still be attached to planet after completion
            Assert.AreEqual(_coruscant, mine.GetParent());
            Assert.IsTrue(_coruscant.GetAllBuildings().Contains(mine));
        }

        /// <summary>
        /// Verifies process tick overflow progress carries to next item.
        /// </summary>
        [Test]
        public void ProcessTick_OverflowProgress_CarriesToNextItem()
        {
            _shipyard.ProcessRate = 1;

            Building _shipyard2 = new Building
            {
                InstanceID = "SHIPYARD2",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_shipyard2, _coruscant);

            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 10,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);

            _manager.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(1, mine2.ManufacturingProgress);
            Assert.AreEqual(ManufacturingStatus.Building, mine2.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies process tick exact completion does not advance next item.
        /// </summary>
        [Test]
        public void ProcessTick_ExactCompletion_DoesNotAdvanceNextItem()
        {
            // Test exact boundary: progress == required, should not over-advance
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);

            _manager.ProcessTick();

            // mine1 completes exactly (1 == 1)
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(1, mine1.ManufacturingProgress);

            // mine2 should have NO progress yet
            Assert.AreEqual(0, mine2.ManufacturingProgress);
            Assert.AreEqual(ManufacturingStatus.Building, mine2.ManufacturingStatus);

            // Next tick advances mine2
            _manager.ProcessTick();
            Assert.Greater(mine2.ManufacturingProgress, 0);
        }

        /// <summary>
        /// Verifies process tick overflow progress starts next item.
        /// </summary>
        [Test]
        public void ProcessTick_OverflowProgress_StartsNextItem()
        {
            // Create two buildings with minimal cost
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);

            _manager.ProcessTick();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(ManufacturingStatus.Building, mine2.ManufacturingStatus);
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count); // Only mine2 remains
        }

        /// <summary>
        /// Verifies process tick multiple completions same tick.
        /// </summary>
        [Test]
        public void ProcessTick_MultipleCompletions_SameTick()
        {
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            Building mine3 = new Building
            {
                InstanceID = "MINE3",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine3, _coruscant, ignoreCost: true);

            // Tick 1: mine1 completes
            _manager.ProcessTick();
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(
                2,
                _coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count
            );

            // Tick 2: mine2 completes
            _manager.ProcessTick();
            Assert.AreEqual(ManufacturingStatus.Complete, mine2.ManufacturingStatus);
            Assert.AreEqual(
                1,
                _coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count
            );

            // Tick 3: mine3 completes
            _manager.ProcessTick();
            Assert.AreEqual(ManufacturingStatus.Complete, mine3.ManufacturingStatus);
            Assert.AreEqual(
                0,
                _coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count
            );
        }

        /// <summary>
        /// Verifies process tick queue mutation does not skip items.
        /// </summary>
        [Test]
        public void ProcessTick_QueueMutation_DoesNotSkipItems()
        {
            // Test that removing items during iteration doesn't skip subsequent items
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            Building mine3 = new Building
            {
                InstanceID = "MINE3",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine3, _coruscant, ignoreCost: true);

            // Tick 1: mine1 completes and is removed - mine2 should still process next tick
            _manager.ProcessTick();
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);

            // Tick 2: mine2 should be active (not skipped)
            List<IManufacturable> queueBefore = _coruscant.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(mine2, queueBefore[0]); // mine2 is now first

            _manager.ProcessTick();
            Assert.AreEqual(ManufacturingStatus.Complete, mine2.ManufacturingStatus);

            // Tick 3: mine3 should complete
            _manager.ProcessTick();
            Assert.AreEqual(ManufacturingStatus.Complete, mine3.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies process tick production building removed cancels queued work.
        /// </summary>
        [Test]
        public void ProcessTick_ProductionBuildingRemoved_CancelsQueuedWork()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);

            // First tick advances progress
            _manager.ProcessTick();
            int progressAfterTick1 = mine.ManufacturingProgress;
            Assert.Greater(progressAfterTick1, 0);

            // Remove production building
            _game.DetachNode(_shipyard);

            List<GameResult> results = _manager.ProcessTick();

            Assert.IsFalse(
                _coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
            Assert.IsNull(mine.GetParent());
            Assert.AreEqual(1, results.OfType<ManufacturingIdleResult>().Count());
        }

        /// <summary>
        /// Verifies handle results last production building destroyed cancels queued work.
        /// </summary>
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

            _manager.HandleResults(
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

        /// <summary>
        /// Verifies handle results last production building scrapped cancels queued work.
        /// </summary>
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

            _manager.HandleResults(
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

        /// <summary>
        /// Verifies handle results another production building survives retains queued work.
        /// </summary>
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

            _manager.HandleResults(
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

        /// <summary>
        /// Verifies handle results bombardment destroys last producer cancels queued work.
        /// </summary>
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

            _manager.HandleResults(
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
        /// Verifies process tick multiple production sources stack correctly.
        /// </summary>
        [Test]
        public void ProcessTick_MultipleProductionSources_StackCorrectly()
        {
            _shipyard.ProcessRate = 4;

            // Add second construction facility
            Building _shipyard2 = new Building
            {
                InstanceID = "SHIPYARD2",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 4,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_shipyard2, _coruscant);

            Assert.AreEqual(2, _coruscant.GetProductionFacilityCount(ManufacturingType.Building));

            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);

            _manager.ProcessTick();
            Assert.AreEqual(0, mine.ManufacturingProgress);

            _manager.ProcessTick();
            Assert.AreEqual(0, mine.ManufacturingProgress);

            _manager.ProcessTick();
            Assert.AreEqual(0, mine.ManufacturingProgress);

            _manager.ProcessTick();
            Assert.IsFalse(_shipyard.ProductionPointReady);
            Assert.IsFalse(_shipyard2.ProductionPointReady);
            Assert.AreEqual(2, mine.ManufacturingProgress);

            double productionRate = _coruscant.GetProductionRate(ManufacturingType.Building);
            Assert.AreEqual(0.5, productionRate);
        }

        /// <summary>
        /// Verifies process tick faster production source completes cycle first.
        /// </summary>
        [Test]
        public void ProcessTick_FasterProductionSource_CompletesCycleFirst()
        {
            _shipyard.ProcessRate = 4;

            Building fasterFacility = new Building
            {
                InstanceID = "SHIPYARD2",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(fasterFacility, _coruscant);

            Assert.AreEqual(2, _coruscant.GetProductionFacilityCount(ManufacturingType.Building));

            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);

            _manager.ProcessTick();
            Assert.AreEqual(0, mine.ManufacturingProgress);

            _manager.ProcessTick();
            Assert.AreEqual(1, mine.ManufacturingProgress);

            _manager.ProcessTick();
            Assert.AreEqual(1, mine.ManufacturingProgress);

            _manager.ProcessTick();
            Assert.IsFalse(_shipyard.ProductionPointReady);
            Assert.IsFalse(fasterFacility.ProductionPointReady);
            Assert.AreEqual(3, mine.ManufacturingProgress);
        }

        /// <summary>
        /// Verifies process tick with no refined materials continues production.
        /// </summary>
        [Test]
        public void ProcessTick_WithNoRefinedMaterials_ContinuesProduction()
        {
            _empire.RefinedMaterialStockpile = 0;
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 2,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);

            _manager.ProcessTick();

            Assert.AreEqual(1, mine.ManufacturingProgress);
            Assert.IsFalse(_shipyard.ProductionPointReady);
            Assert.IsFalse(_shipyard.ProductionInputReserved);
            CollectionAssert.IsEmpty(_empire.PendingRefinedMaterialFacilityIDs);
        }

        /// <summary>
        /// Verifies process tick at former ai refined material reserve continues production.
        /// </summary>
        [Test]
        public void ProcessTick_AtFormerAIRefinedMaterialReserve_ContinuesProduction()
        {
            Building defense = new Building
            {
                InstanceID = "DEFENSE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 2,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Defense,
            };
            int reserve =
                _empire.RefinedMaterialSupply
                * _game.Config.AI.Selection.RefinedMaterialReservePercent
                / 100;
            _empire.RefinedMaterialStockpile = reserve;
            _manager.Enqueue(_coruscant, defense, _coruscant, ignoreCost: true);

            _manager.ProcessTick();

            Assert.AreEqual(1, defense.ManufacturingProgress);
            Assert.AreEqual(reserve, _empire.RefinedMaterialStockpile);
            Assert.IsFalse(_shipyard.ProductionInputReserved);
            CollectionAssert.IsEmpty(_empire.PendingRefinedMaterialFacilityIDs);
        }

        /// <summary>
        /// Verifies process tick at player refined material reserve continues production.
        /// </summary>
        [Test]
        public void ProcessTick_AtPlayerRefinedMaterialReserve_ContinuesProduction()
        {
            _game
                .GetPlayers()
                .Add(
                    new Player
                    {
                        PlayerID = "PLAYER1",
                        FactionID = _empire.InstanceID,
                        ControllerType = PlayerControllerType.Human,
                    }
                );
            Building defense = new Building
            {
                InstanceID = "DEFENSE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 2,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Defense,
            };
            int reserve =
                _empire.RefinedMaterialSupply
                * _game.Config.AI.Selection.RefinedMaterialReservePercent
                / 100;
            _empire.RefinedMaterialStockpile = reserve;
            _manager.Enqueue(_coruscant, defense, _coruscant, ignoreCost: true);

            _manager.ProcessTick();

            Assert.AreEqual(1, defense.ManufacturingProgress);
            Assert.AreEqual(reserve, _empire.RefinedMaterialStockpile);
        }

        /// <summary>
        /// Verifies process tick available refined material is not consumed by manufacturing.
        /// </summary>
        [Test]
        public void ProcessTick_AvailableRefinedMaterial_IsNotConsumedByManufacturing()
        {
            _empire.RefinedMaterialStockpile = 1;
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 2,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);

            _manager.ProcessTick();

            Assert.AreEqual(1, mine.ManufacturingProgress);
            Assert.AreEqual(1, _empire.RefinedMaterialStockpile);
            Assert.IsFalse(_shipyard.ProductionPointReady);
            Assert.IsFalse(_shipyard.ProductionInputReserved);
            CollectionAssert.IsEmpty(_empire.PendingRefinedMaterialFacilityIDs);
        }

        /// <summary>
        /// Verifies process tick building complete no duplicate nodes.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_NoDuplicateNodes()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            _manager.ProcessTick();

            // Verify only one instance in scene graph
            List<Building> allBuildings = _game.GetSceneNodesByType<Building>();
            List<Building> matchingBuildings = allBuildings
                .Where(b => b.InstanceID == "MINE1")
                .ToList();
            Assert.AreEqual(1, matchingBuildings.Count);

            // Verify only one instance in planet's building list
            List<Building> planetBuildings = _coruscant
                .GetAllBuildings()
                .Where(b => b.InstanceID == "MINE1")
                .ToList();
            Assert.AreEqual(1, planetBuildings.Count);
        }

        /// <summary>
        /// Verifies process tick building complete bidirectional relationship valid.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_BidirectionalRelationshipValid()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            _manager.ProcessTick();

            // Verify bidirectional relationship
            Assert.AreEqual(_coruscant, mine.GetParent()); // child -> parent
            Assert.IsTrue(_coruscant.GetAllBuildings().Contains(mine)); // parent -> child
        }

        /// <summary>
        /// Verifies process tick owner change mid build retains original owner.
        /// </summary>
        [Test]
        public void ProcessTick_OwnerChangeMidBuild_RetainsOriginalOwner()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100, // Takes multiple ticks
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            _manager.ProcessTick();

            // Planet captured mid-construction
            _game.GetFactions().Add(new Faction { InstanceID = "REBELLION" });
            _coruscant.OwnerInstanceID = "REBELLION";

            _manager.ProcessTick();

            // Building should still belong to original producer (EMPIRE)
            Assert.AreEqual("EMPIRE", mine.OwnerInstanceID);
            Assert.AreEqual("EMPIRE", mine.ProducerOwnerID);
        }

        /// <summary>
        /// Verifies process tick zero production rate no progress.
        /// </summary>
        [Test]
        public void ProcessTick_ZeroProductionRate_NoProgress()
        {
            // Remove the _shipyard to have zero production
            _game.DetachNode(_shipyard);

            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            int initialProgress = mine.ManufacturingProgress;

            _manager.ProcessTick();

            // No production facilities = no progress
            Assert.AreEqual(initialProgress, mine.ManufacturingProgress);
        }

        /// <summary>
        /// Verifies process tick capital ship building remains in fleet with progress.
        /// </summary>
        [Test]
        public void ProcessTick_CapitalShipBuilding_RemainsInFleetWithProgress()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, planet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, fleet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick();
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Building, ship.ManufacturingStatus);
            Assert.Greater(ship.ManufacturingProgress, 0, "Progress should advance.");
            Assert.IsTrue(
                ship.GetParent() is Fleet,
                "Ship should be in a fleet during production."
            );
            Assert.AreEqual(planet, fleet.GetParent(), "Fleet should be at production planet.");
        }

        /// <summary>
        /// Verifies process tick capital ship uses every ready facility without consuming material.
        /// </summary>
        [Test]
        public void ProcessTick_CapitalShip_UsesEveryReadyFacilityWithoutConsumingMaterial()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction faction = new Faction { InstanceID = "empire", RefinedMaterialStockpile = 2 };
            game.GetFactions().Add(faction);
            Planet planet = BuildShipyardPlanet(game, "p1", faction.InstanceID);
            faction.RefinedMaterialStockpile = 2;
            Building secondShipyard = new Building
            {
                InstanceID = "p1_second_shipyard",
                OwnerInstanceID = faction.InstanceID,
                BuildingType = BuildingType.Shipyard,
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(secondShipyard, planet);
            Fleet fleet = EntityFactory.CreateFleet("f1", faction.InstanceID);
            game.AttachNode(fleet, planet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "anchor",
                OwnerInstanceID = faction.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(anchor, fleet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = faction.InstanceID,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };
            FleetSystem fleetSystem = new FleetSystem(game);
            ManufacturingSystem manufacturing = new ManufacturingSystem(
                game,
                fleetSystem,
                new MovementSystem(game, new FogOfWarSystem(game), fleetSystem)
            );
            manufacturing.Enqueue(planet, ship, fleet, ignoreCost: true);

            manufacturing.ProcessTick();

            Assert.AreEqual(2, faction.RefinedMaterialStockpile);
            Assert.AreEqual(2, ship.ManufacturingProgress);
            CollectionAssert.IsEmpty(faction.PendingRefinedMaterialFacilityIDs);
        }

        /// <summary>
        /// Verifies process tick capital ship complete removed from queue.
        /// </summary>
        [Test]
        public void ProcessTick_CapitalShipComplete_RemovedFromQueue()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, planet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, fleet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, ship.ManufacturingStatus);
            bool inQueue = planet
                .GetManufacturingQueue()
                .Values.Any(list => list.Any(i => i.InstanceID == "cs1"));
            Assert.IsFalse(inQueue, "Completed ship should be removed from queue.");
        }

        /// <summary>
        /// Verifies process tick capital ship complete on same planet no movement.
        /// </summary>
        [Test]
        public void ProcessTick_CapitalShipCompleteOnSamePlanet_NoMovement()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, planet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, fleet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.IsNotNull(ship.GetParentOfType<Fleet>(), "Ship should be in a fleet.");
            Assert.IsNull(ship.Movement, "No _movement needed for same-planet destination.");
        }

        /// <summary>
        /// Verifies process tick capital ship complete on different planet ships fleet.
        /// </summary>
        [Test]
        public void ProcessTick_CapitalShipCompleteOnDifferentPlanet_ShipsFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet originPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, destPlanet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, fleet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(originPlanet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Delivering, ship.ManufacturingStatus);
            Assert.IsNotNull(ship.Movement, "Ship should have _movement state for transit.");
            Assert.Greater(ship.Movement.TransitTicks, 0, "Should have travel time.");
            Assert.IsNull(fleet.Movement, "Fleet should not move — the ship travels to it.");
        }

        /// <summary>
        /// Verifies process tick capital ship complete fleet over hostile planet ship travels to fleet.
        /// </summary>
        [Test]
        public void ProcessTick_CapitalShipCompleteFleetOverHostilePlanet_ShipTravelsToFleet()
        {
            // Ship queued into fleet at destPlanet. destPlanet captured mid-production.
            // Planet doesn't accept CapitalShips directly, so HandleArrivalRejection finds
            // no valid fallback — ship stays in fleet, no transit state.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            Faction rebels = new Faction { InstanceID = "rebels" };
            _game.GetFactions().Add(empire);
            _game.GetFactions().Add(rebels);
            Planet originPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, destPlanet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, fleet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                localMovement
            );
            mfg.Enqueue(originPlanet, ship, fleet, ignoreCost: true);

            destPlanet.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Delivering, ship.ManufacturingStatus);
            Assert.IsNotNull(
                ship.Movement,
                "Ship should travel to its assigned fleet even when the fleet is over a hostile planet."
            );
            Assert.AreEqual(
                fleet,
                ship.GetParentOfType<Fleet>(),
                "Ship stays in its assigned fleet."
            );
        }

        /// <summary>
        /// Verifies process tick starfighter complete ships to destination.
        /// </summary>
        [Test]
        public void ProcessTick_StarfighterComplete_ShipsToDestination()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet originPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip destShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Delivering, fighter.ManufacturingStatus);
            Assert.IsNotNull(fighter.Movement, "Should have _movement state for shipping.");
            Assert.Greater(fighter.Movement.TransitTicks, 0, "Should have travel time.");
        }

        /// <summary>
        /// Verifies process tick starfighter complete remains inside destination fleet.
        /// </summary>
        [Test]
        public void ProcessTick_StarfighterComplete_RemainsInsideDestinationFleet()
        {
            // When a starfighter is enqueued into an existing fleet on a different planet,
            // completing manufacturing must ship it to the fleet's capital ship.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet originPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip destShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(
                destShip,
                fighter.GetParent(),
                "Completed starfighter must be parented to the destination capital ship."
            );
        }

        /// <summary>
        /// Verifies process tick regiment complete ships to destination.
        /// </summary>
        [Test]
        public void ProcessTick_RegimentComplete_ShipsToDestination()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet originPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(originPlanet, regiment, destPlanet, ignoreCost: true);
            List<GameResult> results = mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Delivering, regiment.ManufacturingStatus);
            Assert.IsNotNull(regiment.Movement, "Should have _movement state for shipping.");
            Assert.Greater(regiment.Movement.TransitTicks, 0, "Should have travel time.");
            Assert.IsFalse(
                results
                    .OfType<GameObjectDeployedResult>()
                    .Any(result => ReferenceEquals(result.GameObject, regiment))
            );
        }

        /// <summary>
        /// Verifies process tick regiment complete on same planet attached immediately.
        /// </summary>
        [Test]
        public void ProcessTick_RegimentCompleteOnSamePlanet_AttachedImmediately()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, regiment, planet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, regiment.ManufacturingStatus);
            Assert.AreEqual(
                planet,
                regiment.GetParent(),
                "Regiment should be on the production planet."
            );
            Assert.IsNull(regiment.Movement, "No _movement needed for same-planet destination.");
            Assert.IsTrue(
                planet.GetChildren<Regiment>().Contains(regiment),
                "Regiment should be in planet's regiment list."
            );
        }

        /// <summary>
        /// Verifies process tick destination destroyed unit is also destroyed.
        /// </summary>
        [Test]
        public void ProcessTick_DestinationDestroyed_UnitIsAlsoDestroyed()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip destShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, fighter, destFleet, ignoreCost: true);

            // Destroy the destination fleet mid-production — fighter is a child so it is deregistered too
            _game.DetachNode(destFleet);

            Starfighter found = _game.GetSceneNodeByInstanceID<Starfighter>("sf1");
            Assert.IsNull(
                found,
                "Fighter should be deregistered when its destination fleet is destroyed."
            );
        }

        /// <summary>
        /// Verifies process tick building complete on different planet ships to destination.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingCompleteOnDifferentPlanet_ShipsToDestination()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet originPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(constructionYard, originPlanet);

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(originPlanet, mine, destPlanet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Delivering, mine.ManufacturingStatus);
            Assert.IsNotNull(mine.Movement, "Should have _movement state for shipping.");
            Assert.Greater(mine.Movement.TransitTicks, 0, "Should have travel time.");
        }

        /// <summary>
        /// Verifies process tick building for owned uncolonized planet colonizes on arrival.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingForOwnedUncolonizedPlanet_ColonizesOnArrival()
        {
            PlanetSector planetSector = _coruscant.GetParentOfType<PlanetSector>();
            Planet destination = new Planet
            {
                InstanceID = "OUTER_RIM",
                OwnerInstanceID = "EMPIRE",
                IsColonized = false,
                EnergyCapacity = 5,
            };
            _game.AttachNode(destination, planetSector);
            _game.AttachNode(
                new Regiment { InstanceID = "GARRISON", OwnerInstanceID = "EMPIRE" },
                destination
            );

            Building mine = new Building
            {
                InstanceID = "OUTER_RIM_MINE",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BuildingType = BuildingType.Mine,
            };

            bool enqueued = _manager.Enqueue(_coruscant, mine, destination, ignoreCost: true);
            _manager.ProcessTick();

            Assert.IsTrue(enqueued);
            Assert.IsFalse(destination.IsColonized);
            Assert.IsNotNull(mine.Movement);

            int transitTicks = mine.Movement.TransitTicks;
            for (int i = 0; i < transitTicks; i++)
            {
                _movement.ProcessTick();
            }

            Assert.IsNull(mine.Movement);
            Assert.IsTrue(destination.IsColonized);
            Assert.AreSame(destination, mine.GetParent());
        }

        /// <summary>
        /// Verifies process tick building destination changed sides cancels order.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingDestinationChangedSides_CancelsOrder()
        {
            // Mine queued from planetA to planetB. planetB captured before completion.
            // The order should be cancelled instead of redirecting to planetA.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", RefinedMaterialStockpile = 1 };
            _game.GetFactions().Add(empire);
            _game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            AddResourceSupplyPlanet(_game, "resource_supply_changed_sides", "empire");

            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 0,
                PositionY = 0,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(planetA, planetSector);

            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(constructionYard, planetA);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 500,
                PositionY = 0,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(planetB, planetSector);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                ManufacturingFactionInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                localMovement
            );
            mfg.Enqueue(planetA, mine, planetB, ignoreCost: true);

            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.IsNull(mine.GetParent());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>(mine.InstanceID));
            Assert.IsEmpty(planetA.GetManufacturingQueue());
        }

        /// <summary>
        /// Verifies process tick building destination changed sides cancels regardless of producer capacity.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingDestinationChangedSides_CancelsRegardlessOfProducerCapacity()
        {
            // Mine queued from full planetA to planetB. planetB captured before completion.
            // Cancellation should not depend on planetA having fallback capacity.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", RefinedMaterialStockpile = 1 };
            _game.GetFactions().Add(empire);
            _game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            AddResourceSupplyPlanet(_game, "resource_supply_no_capacity", "empire");

            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 2,
                PositionX = 0,
                PositionY = 0,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(planetA, planetSector);

            // Fill planetA to capacity.
            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(constructionYard, planetA);
            Building filler = new Building
            {
                InstanceID = "fill1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(filler, planetA);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 500,
                PositionY = 0,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(planetB, planetSector);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                ManufacturingFactionInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                localMovement
            );
            mfg.Enqueue(planetA, mine, planetB, ignoreCost: true);

            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.IsNull(mine.GetParent());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>(mine.InstanceID));
            Assert.IsEmpty(planetA.GetManufacturingQueue());
        }

        /// <summary>
        /// Verifies process tick blockade applies graduated rate and kdy restores full rate.
        /// </summary>
        [Test]
        public void ProcessTick_Blockade_AppliesGraduatedRateAndKdyRestoresFullRate()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            Faction rebels = new Faction { InstanceID = "rebels" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(rebels);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");
            empire.RefinedMaterialStockpile = 1;
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };
            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(constructionYard, planet);
            Fleet hostileFleet = EntityFactory.CreateFleet("hf1", "rebels");
            game.AttachNode(hostileFleet, planet);
            CapitalShip hostileShip = new CapitalShip
            {
                InstanceID = "hostile_ship",
                OwnerInstanceID = rebels.InstanceID,
                StarfighterCapacity = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(hostileShip, hostileFleet);
            game.AttachNode(
                new Starfighter
                {
                    InstanceID = "hostile_fighter",
                    OwnerInstanceID = rebels.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                hostileShip
            );
            FleetSystem fleetSystem = new FleetSystem(game);
            ManufacturingSystem manufacturing = new ManufacturingSystem(
                game,
                fleetSystem,
                new MovementSystem(game, new FogOfWarSystem(game), fleetSystem)
            );
            manufacturing.Enqueue(planet, mine, planet, ignoreCost: true);

            manufacturing.ProcessTick();

            double expectedBlockadeProgress =
                1.0
                - (
                    config.Blockade.CapitalShipProductionPenaltyPercent
                    + config.Blockade.FighterProductionPenaltyPercent
                ) / 100.0;
            Assert.AreEqual(
                expectedBlockadeProgress,
                constructionYard.ProductionCycleProgress,
                0.0001
            );
            Assert.AreEqual(1, empire.RefinedMaterialStockpile);
            Assert.IsFalse(constructionYard.ProductionInputReserved);

            game.AttachNode(
                new Building
                {
                    InstanceID = "kdy1",
                    OwnerInstanceID = empire.InstanceID,
                    BuildingType = BuildingType.Weapon,
                    DefenseWeaponEffect = DefenseWeaponEffect.ShieldDamage,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );
            manufacturing.ProcessTick();

            Assert.AreEqual(
                expectedBlockadeProgress + 1,
                constructionYard.ProductionCycleProgress,
                0.0001
            );
        }

        /// <summary>
        /// Verifies process tick full blockade halts production without reserving input.
        /// </summary>
        [Test]
        public void ProcessTick_ManufacturingSpeedModifier_PreservesFractionalThroughput()
        {
            GameConfig config = TestConfig.Create();
            config.DifficultyModifiers[GameDifficulty.Hard] = new DifficultyModifiers
            {
                ManufacturingSpeedPercent = 150,
            };
            GameRoot game = new GameRoot(config);
            game.Summary.Difficulty = GameDifficulty.Hard;
            game.Summary.PlayerFactionID = "player";
            Faction empire = new Faction { InstanceID = "empire" };
            game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", empire.InstanceID);
            empire.RefinedMaterialStockpile = 3;
            Building yard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = empire.InstanceID,
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(yard, planet);
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = empire.InstanceID,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };
            FleetSystem fleetSystem = new FleetSystem(game);
            ManufacturingSystem manufacturing = new ManufacturingSystem(game, fleetSystem);
            manufacturing.Enqueue(planet, mine, planet, ignoreCost: true);

            manufacturing.ProcessTick();

            Assert.AreEqual(1.5, yard.ProductionCycleProgress, 0.0001);
            Assert.AreEqual(0, mine.ManufacturingProgress);

            manufacturing.ProcessTick();

            Assert.AreEqual(1, yard.ProductionCycleProgress, 0.0001);
            Assert.AreEqual(1, mine.ManufacturingProgress);

            manufacturing.ProcessTick();

            Assert.AreEqual(0.5, yard.ProductionCycleProgress, 0.0001);
            Assert.AreEqual(2, mine.ManufacturingProgress);
        }

        /// <summary>
        /// Verifies a full blockade halts production without reserving its next input.
        /// </summary>
        [Test]
        public void ProcessTick_FullBlockade_HaltsProductionWithoutReservingInput()
        {
            GameConfig config = TestConfig.Create();
            config.Blockade.CapitalShipProductionPenaltyPercent = 100;
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            Faction rebels = new Faction { InstanceID = "rebels" };
            game.GetFactions().Add(empire);
            game.GetFactions().Add(rebels);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");
            empire.RefinedMaterialStockpile = 1;
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };
            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(constructionYard, planet);
            Fleet hostileFleet = EntityFactory.CreateFleet("hf1", "rebels");
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(
                new CapitalShip
                {
                    InstanceID = "hostile_ship",
                    OwnerInstanceID = rebels.InstanceID,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                hostileFleet
            );
            FleetSystem fleetSystem = new FleetSystem(game);
            ManufacturingSystem manufacturing = new ManufacturingSystem(
                game,
                fleetSystem,
                new MovementSystem(game, new FogOfWarSystem(game), fleetSystem)
            );
            manufacturing.Enqueue(planet, mine, planet, ignoreCost: true);

            manufacturing.ProcessTick();

            Assert.AreEqual(0, mine.ManufacturingProgress);
            Assert.AreEqual(1, empire.RefinedMaterialStockpile);
            Assert.IsFalse(constructionYard.ProductionInputReserved);
            Assert.IsEmpty(empire.PendingRefinedMaterialFacilityIDs);
        }

        /// <summary>
        /// Verifies process tick uprising halts production without reserving input.
        /// </summary>
        [Test]
        public void ProcessTick_Uprising_HaltsProductionWithoutReservingInput()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", RefinedMaterialStockpile = 1 };
            game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", empire.InstanceID);
            empire.RefinedMaterialStockpile = 1;
            planet.IsInUprising = true;
            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(constructionYard, planet);
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ProductionType = ManufacturingType.Building,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };
            FleetSystem fleetSystem = new FleetSystem(game);
            ManufacturingSystem manufacturing = new ManufacturingSystem(
                game,
                fleetSystem,
                new MovementSystem(game, new FogOfWarSystem(game), fleetSystem)
            );
            manufacturing.Enqueue(planet, mine, planet, ignoreCost: true);

            manufacturing.ProcessTick();

            Assert.AreEqual(0, mine.ManufacturingProgress);
            Assert.AreEqual(1, empire.RefinedMaterialStockpile);
            Assert.IsFalse(constructionYard.ProductionInputReserved);
            Assert.IsEmpty(empire.PendingRefinedMaterialFacilityIDs);
        }

        /// <summary>
        /// Verifies process tick three manufacturing types all advance.
        /// </summary>
        [Test]
        public void ProcessTick_ThreeManufacturingTypes_AllAdvance()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            empire.Settings.RefinementMultiplier = 4;
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            // BuildShipyardPlanet already adds a _shipyard (Ship) and training facility (Troop).
            // Add a construction yard for Building production.
            Building constructionYard = new Building
            {
                InstanceID = "p1_construction",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(constructionYard, planet);

            // Enqueue one item per manufacturing type
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ConstructionCost = 50,
                BaseBuildSpeed = 1,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 50,
                BaseBuildSpeed = 1,
            };
            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 50,
                BaseBuildSpeed = 1,
            };

            // Create a fleet with a capital ship so ship and regiment have a valid destination.
            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, planet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "anchor_cs",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 10,
                RegimentCapacity = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(anchor, fleet);

            MovementSystem localMovement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                localMovement
            );
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.Enqueue(planet, regiment, fleet, ignoreCost: true);

            mfg.ProcessTick();

            Assert.Greater(
                mine.ManufacturingProgress,
                0,
                "Building should advance in parallel with ships and troops"
            );
            Assert.Greater(
                ship.ManufacturingProgress,
                0,
                "Ship should advance in parallel with buildings and troops"
            );
            Assert.Greater(
                regiment.ManufacturingProgress,
                0,
                "Troop should advance in parallel with buildings and ships"
            );
        }

        /// <summary>
        /// Verifies process tick no shipyard ship makes no progress.
        /// </summary>
        [Test]
        public void ProcessTick_NoShipyard_ShipMakesNoProgress()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(planetSector, _game.Galaxy);

            // Planet with NO facilities at all
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                EnergyCapacity = 10,
                NumRawResourceNodes = 100,
            };
            _game.AttachNode(planet, planetSector);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 10,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, ship, planet, ignoreCost: true);

            // Tick many times — should never advance
            for (int i = 0; i < 20; i++)
                mfg.ProcessTick();

            Assert.AreEqual(
                0,
                ship.ManufacturingProgress,
                "Ship should make no progress without a _shipyard"
            );
        }

        /// <summary>
        /// Verifies process tick no training facility regiment makes no progress.
        /// </summary>
        [Test]
        public void ProcessTick_NoTrainingFacility_RegimentMakesNoProgress()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(planetSector, _game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                EnergyCapacity = 10,
            };
            _game.AttachNode(planet, planetSector);

            // Add a _shipyard but NOT a training facility
            Building _shipyard = new Building
            {
                InstanceID = "sy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Shipyard,
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_shipyard, planet);

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 10,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, regiment, planet, ignoreCost: true);

            for (int i = 0; i < 20; i++)
                mfg.ProcessTick();

            Assert.AreEqual(
                0,
                regiment.ManufacturingProgress,
                "Regiment should make no progress without a training facility"
            );
        }

        /// <summary>
        /// Verifies process tick no construction yard building makes no progress.
        /// </summary>
        [Test]
        public void ProcessTick_NoConstructionYard_BuildingMakesNoProgress()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);

            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = "sector1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(planetSector, _game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                EnergyCapacity = 10,
            };
            _game.AttachNode(planet, planetSector);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ProductionType = ManufacturingType.Building,
                ConstructionCost = 10,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            for (int i = 0; i < 20; i++)
                mfg.ProcessTick();

            Assert.AreEqual(
                0,
                mine.ManufacturingProgress,
                "Building should make no progress without a construction yard"
            );
        }

        /// <summary>
        /// Verifies process tick capital ship complete destination fleet destroyed ship is lost.
        /// </summary>
        [Test]
        public void ProcessTick_CapitalShipCompleteDestinationFleetDestroyed_ShipIsLost()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet productionPlanet = BuildShipyardPlanet(_game, "p1", "empire");
            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");

            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, destFleet);

            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                localMovement
            );
            mfg.Enqueue(productionPlanet, capitalShip, destFleet, ignoreCost: true);

            Assert.IsNotNull(
                capitalShip.GetParentOfType<Fleet>(),
                "CS should be in fleet after enqueue."
            );

            // Destroy the fleet mid-production by detaching all ships and the fleet itself.
            _game.DetachNode(anchor);
            _game.DetachNode(capitalShip);
            _game.DetachNode(destFleet);

            // One tick completes manufacturing and triggers the rescue.
            mfg.ProcessTick();

            Assert.IsNull(
                _game.GetSceneNodeByInstanceID<CapitalShip>("cs1"),
                "Ship should be lost when its destination fleet was destroyed before completion."
            );
        }

        /// <summary>
        /// Verifies process tick starfighter complete fleet over hostile planet travels to carrier.
        /// </summary>
        [Test]
        public void ProcessTick_StarfighterCompleteFleetOverHostilePlanet_TravelsToCarrier()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            _game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            Planet productionPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(carrier, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                localMovement
            );
            mfg.Enqueue(productionPlanet, fighter, destFleet, ignoreCost: true);

            destPlanet.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Delivering, fighter.ManufacturingStatus);
            Assert.AreEqual(
                carrier,
                fighter.GetParent(),
                "Fighter should stay assigned to its carrier when the fleet is over a hostile planet."
            );
            Assert.IsNotNull(
                fighter.Movement,
                "Fighter should be in visual transit toward its carrier."
            );
        }

        /// <summary>
        /// Verifies process tick regiment complete fleet over hostile planet travels to transport.
        /// </summary>
        [Test]
        public void ProcessTick_RegimentCompleteFleetOverHostilePlanet_TravelsToTransport()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            _game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            Planet productionPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                RegimentCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(carrier, destFleet);

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                localMovement
            );
            mfg.Enqueue(productionPlanet, regiment, destFleet, ignoreCost: true);

            destPlanet.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Delivering, regiment.ManufacturingStatus);
            Assert.AreEqual(
                carrier,
                regiment.GetParent(),
                "Regiment should stay assigned to its transport when the fleet is over a hostile planet."
            );
            Assert.IsNotNull(
                regiment.Movement,
                "Regiment should be in visual transit toward its transport."
            );
        }

        /// <summary>
        /// Verifies process tick regiment destination changed sides cancels order.
        /// </summary>
        [Test]
        public void ProcessTick_RegimentDestinationChangedSides_CancelsOrder()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "REBELS" });
            Planet destination = new Planet
            {
                InstanceID = "DESTINATION",
                OwnerInstanceID = _empire.InstanceID,
                IsColonized = true,
                EnergyCapacity = 10,
            };
            _game.AttachNode(destination, _coruscant.GetParent());
            Regiment regiment = new Regiment
            {
                InstanceID = "REGIMENT",
                OwnerInstanceID = _empire.InstanceID,
                ConstructionCost = 100,
            };

            Assert.IsTrue(_manager.Enqueue(_coruscant, regiment, destination, ignoreCost: true));
            destination.OwnerInstanceID = "REBELS";

            _manager.ProcessTick();

            Assert.IsNull(regiment.GetParent());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            Assert.IsFalse(_coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Troop));
        }

        /// <summary>
        /// Verifies process tick local building order producer changed sides cancels order.
        /// </summary>
        [Test]
        public void ProcessTick_LocalBuildingOrderProducerChangedSides_CancelsOrder()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "REBELS" });
            Building mine = CreateOrderTestBuildingTemplate("LOCAL_MINE");
            mine.InstanceID = "LOCAL_MINE";
            mine.OwnerInstanceID = _empire.InstanceID;
            mine.ConstructionCost = 100;
            Assert.IsTrue(_manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true));

            _coruscant.OwnerInstanceID = "REBELS";

            _manager.ProcessTick();

            Assert.IsNull(mine.GetParent());
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>(mine.InstanceID));
            Assert.IsFalse(
                _coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
        }

        /// <summary>
        /// Verifies process tick six invalid orders before valid order cancels invalid and advances valid.
        /// </summary>
        [Test]
        public void ProcessTick_SixInvalidOrdersBeforeValidOrder_CancelsInvalidAndAdvancesValid()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "REBELS" });
            Planet captured = CreateOrderTestPlanet(_game, "CAPTURED", _empire.InstanceID);
            List<Building> invalidOrders = Enumerable
                .Range(1, 6)
                .Select(index =>
                {
                    Building building = CreateOrderTestBuildingTemplate($"INVALID_{index}");
                    building.InstanceID = $"INVALID_{index}";
                    building.OwnerInstanceID = _empire.InstanceID;
                    building.ConstructionCost = 100;
                    return building;
                })
                .ToList();
            foreach (Building invalidOrder in invalidOrders)
                Assert.IsTrue(
                    _manager.Enqueue(_coruscant, invalidOrder, captured, ignoreCost: true)
                );

            Building validOrder = CreateOrderTestBuildingTemplate("VALID");
            validOrder.InstanceID = "VALID";
            validOrder.OwnerInstanceID = _empire.InstanceID;
            validOrder.ConstructionCost = 100;
            Assert.IsTrue(_manager.Enqueue(_coruscant, validOrder, _coruscant, ignoreCost: true));
            captured.OwnerInstanceID = "REBELS";

            _manager.ProcessTick();

            Assert.IsTrue(invalidOrders.All(order => order.GetParent() == null));
            List<IManufacturable> queue = _coruscant.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            CollectionAssert.AreEqual(new[] { validOrder }, queue);
            Assert.Greater(validOrder.ManufacturingProgress, 0);
        }

        /// <summary>
        /// Verifies process tick orders for two captured planets cancels entire lane.
        /// </summary>
        [Test]
        public void ProcessTick_OrdersForTwoCapturedPlanets_CancelsEntireLane()
        {
            _game.GetFactions().Add(new Faction { InstanceID = "REBELS" });
            Planet firstCaptured = CreateOrderTestPlanet(
                _game,
                "FIRST_CAPTURED",
                _empire.InstanceID
            );
            Planet secondCaptured = CreateOrderTestPlanet(
                _game,
                "SECOND_CAPTURED",
                _empire.InstanceID
            );
            Planet[] destinations = { firstCaptured, secondCaptured, firstCaptured };
            List<Building> orders = new List<Building>();
            for (int index = 0; index < destinations.Length; index++)
            {
                Building order = CreateOrderTestBuildingTemplate($"ORDER_{index}");
                order.InstanceID = $"ORDER_{index}";
                order.OwnerInstanceID = _empire.InstanceID;
                order.ConstructionCost = 100;
                Assert.IsTrue(
                    _manager.Enqueue(_coruscant, order, destinations[index], ignoreCost: true)
                );
                orders.Add(order);
            }

            firstCaptured.OwnerInstanceID = "REBELS";
            secondCaptured.OwnerInstanceID = "REBELS";

            _manager.ProcessTick();

            Assert.IsTrue(orders.All(order => order.GetParent() == null));
            Assert.IsFalse(
                _coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
        }

        /// <summary>
        /// Verifies process tick building batch destination changed sides cancels all targeted orders.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingBatchDestinationChangedSides_CancelsAllTargetedOrders()
        {
            // 3 mines queued from production planet A to destination planet B.
            // B changes sides. A has enough ground slots for all 3.
            // Every order assigned to B should be cancelled.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", RefinedMaterialStockpile = 3 };
            _game.GetFactions().Add(empire);
            _game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            AddResourceSupplyPlanet(_game, "resource_supply_batch_capacity", "empire");

            // Production planet A: EnergyCapacity 10, with 3 construction yards using 3 slots,
            // leaving 7 available for the 3 mines being built.
            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 0,
                PositionY = 0,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(planetA, planetSector);

            for (int i = 1; i <= 3; i++)
            {
                Building constructionYard = new Building
                {
                    InstanceID = $"cy{i}",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.ConstructionFacility,
                    ProductionType = ManufacturingType.Building,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _game.AttachNode(constructionYard, planetA);
            }

            // Destination planet B: plenty of ground slots.
            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 100,
                PositionY = 0,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(planetB, planetSector);

            Building mine1 = new Building
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                ManufacturingFactionInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            Building mine2 = new Building
            {
                InstanceID = "m2",
                OwnerInstanceID = "empire",
                ManufacturingFactionInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            Building mine3 = new Building
            {
                InstanceID = "m3",
                OwnerInstanceID = "empire",
                ManufacturingFactionInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                localMovement
            );

            mfg.Enqueue(planetA, mine1, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine2, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine3, planetB, ignoreCost: true);

            // Destination captured before mines complete.
            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.IsNull(mine1.GetParent());
            Assert.IsNull(mine2.GetParent());
            Assert.IsNull(mine3.GetParent());
            Assert.IsEmpty(planetA.GetManufacturingQueue());
        }

        /// <summary>
        /// Verifies process tick building batch destination changed sides cancels without fallback capacity.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingBatchDestinationChangedSides_CancelsWithoutFallbackCapacity()
        {
            // 3 mines queued from production planet A to destination planet B.
            // B changes sides. Cancellation does not depend on fallback capacity at A.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire", RefinedMaterialStockpile = 3 };
            _game.GetFactions().Add(empire);
            _game.GetFactions().Add(new Faction { InstanceID = "rebels" });

            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            AddResourceSupplyPlanet(_game, "resource_supply_batch_no_capacity", "empire");

            // Production planet A: EnergyCapacity 5, fully occupied by 3 yards + 2 dummies (0 available).
            Planet planetA = new Planet
            {
                InstanceID = "pA",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 5,
                PositionX = 0,
                PositionY = 0,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(planetA, planetSector);

            for (int i = 1; i <= 3; i++)
            {
                Building constructionYard = new Building
                {
                    InstanceID = $"cy{i}",
                    OwnerInstanceID = "empire",
                    BuildingType = BuildingType.ConstructionFacility,
                    ProductionType = ManufacturingType.Building,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _game.AttachNode(constructionYard, planetA);
            }

            // Fill ground slots so production planet has no capacity for redirected mines.
            Building dummy1 = new Building
            {
                InstanceID = "dummy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(dummy1, planetA);
            Building dummy2 = new Building
            {
                InstanceID = "dummy2",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(dummy2, planetA);

            Planet planetB = new Planet
            {
                InstanceID = "pB",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
                PositionX = 100,
                PositionY = 0,
                NumRawResourceNodes = 10,
            };
            _game.AttachNode(planetB, planetSector);

            Building mine1 = new Building
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                ManufacturingFactionInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            Building mine2 = new Building
            {
                InstanceID = "m2",
                OwnerInstanceID = "empire",
                ManufacturingFactionInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            Building mine3 = new Building
            {
                InstanceID = "m3",
                OwnerInstanceID = "empire",
                ManufacturingFactionInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(
                _game,
                new FogOfWarSystem(_game),
                new FleetSystem(_game)
            );
            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                localMovement
            );

            mfg.Enqueue(planetA, mine1, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine2, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine3, planetB, ignoreCost: true);

            // Destination captured before mines complete.
            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.IsNull(mine1.GetParent());
            Assert.IsNull(mine2.GetParent());
            Assert.IsNull(mine3.GetParent());
            Assert.IsEmpty(planetA.GetManufacturingQueue());
        }

        /// <summary>
        /// Verifies process tick building complete emits deployed result.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_EmitsDeployedResult()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            Assert.IsTrue(results.OfType<ManufacturingDeployedResult>().Any());
        }

        /// <summary>
        /// Verifies process tick building complete deployed result has correct faction and object.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_DeployedResultHasCorrectFactionAndObject()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            ManufacturingDeployedResult deployed = results
                .OfType<ManufacturingDeployedResult>()
                .First();
            Assert.AreEqual(_empire, deployed.Faction);
            Assert.AreEqual(mine, deployed.DeployedObject);
        }

        /// <summary>
        /// Verifies process tick last item completes emits idle result.
        /// </summary>
        [Test]
        public void ProcessTick_LastItemCompletes_EmitsIdleResult()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            ManufacturingIdleResult idle = results.OfType<ManufacturingIdleResult>().Single();
            Assert.AreEqual(_empire, idle.Faction);
            Assert.AreEqual(_coruscant, idle.ProductionPlanet);
            Assert.AreEqual(ManufacturingType.Building, idle.ManufacturingType);
        }

        /// <summary>
        /// Verifies process tick first of two items completes does not emit idle result.
        /// </summary>
        [Test]
        public void ProcessTick_FirstOfTwoItemsCompletes_DoesNotEmitIdleResult()
        {
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };
            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1000,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            Assert.IsTrue(
                results.OfType<ManufacturingDeployedResult>().Any(),
                "deployed should fire for completed item"
            );
            Assert.IsFalse(
                results.OfType<ManufacturingIdleResult>().Any(),
                "idle should not fire while queue still has items"
            );
        }

        /// <summary>
        /// Verifies process tick building complete emits remaining result.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_EmitsRemainingResult()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            Assert.IsTrue(results.OfType<ManufacturingRemainingResult>().Any());
        }

        /// <summary>
        /// Verifies process tick first of two items completes remaining count is one.
        /// </summary>
        [Test]
        public void ProcessTick_FirstOfTwoItemsCompletes_RemainingCountIsOne()
        {
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };
            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1000,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            ManufacturingRemainingResult remaining = results
                .OfType<ManufacturingRemainingResult>()
                .First();
            Assert.AreEqual(1, remaining.RemainingCount);
        }

        /// <summary>
        /// Verifies process tick building complete emits points required result.
        /// </summary>
        [Test]
        public void ProcessTick_BuildingComplete_EmitsPointsRequiredResult()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            Assert.IsTrue(results.OfType<ManufacturingPointsRequiredResult>().Any());
        }

        /// <summary>
        /// Verifies process tick first of two items completes points required matches remaining item.
        /// </summary>
        [Test]
        public void ProcessTick_FirstOfTwoItemsCompletes_PointsRequiredMatchesRemainingItem()
        {
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };
            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 50,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);
            List<GameResult> results = _manager.ProcessTick();

            ManufacturingPointsRequiredResult pointsResult = results
                .OfType<ManufacturingPointsRequiredResult>()
                .First();
            Assert.AreEqual(50, pointsResult.RequiredPoints);
        }

        /// <summary>
        /// Verifies enqueue valid building adds to queue.
        /// </summary>
        [Test]
        public void Enqueue_ValidBuilding_AddsToQueue()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };

            bool result = _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);

            Assert.IsTrue(result);
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.IsTrue(queue.ContainsKey(ManufacturingType.Building));
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count);
            Assert.AreEqual("MINE1", queue[ManufacturingType.Building][0].InstanceID);
        }

        /// <summary>
        /// Verifies enqueue regiment to uncolonized planet returns false.
        /// </summary>
        [Test]
        public void Enqueue_RegimentToUncolonizedPlanet_ReturnsFalse()
        {
            PlanetSector planetSector = _coruscant.GetParentOfType<PlanetSector>();
            Planet destination = new Planet
            {
                InstanceID = "UNCHARTED",
                OwnerInstanceID = null,
                IsColonized = false,
            };
            _game.AttachNode(destination, planetSector);

            Regiment regiment = new Regiment
            {
                InstanceID = "REGIMENT1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1,
            };

            bool result = _manager.Enqueue(_coruscant, regiment, destination, ignoreCost: true);

            Assert.IsFalse(result);
            Assert.IsNull(regiment.GetParent());
            Assert.IsFalse(_coruscant.GetManufacturingQueue().ContainsKey(ManufacturingType.Troop));
        }

        /// <summary>
        /// Verifies enqueue multiple buildings maintains order.
        /// </summary>
        [Test]
        public void Enqueue_MultipleBuildings_MaintainsOrder()
        {
            Building building1 = new Building
            {
                InstanceID = "B1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };
            Building building2 = new Building
            {
                InstanceID = "B2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 200,
                BaseBuildSpeed = 20,
                BuildingType = BuildingType.Refinery,
            };
            Building building3 = new Building
            {
                InstanceID = "B3",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 150,
                BaseBuildSpeed = 15,
                BuildingType = BuildingType.Defense,
            };

            _manager.Enqueue(_coruscant, building1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, building2, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, building3, _coruscant, ignoreCost: true);

            List<IManufacturable> queue = _coruscant.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual("B1", queue[0].InstanceID);
            Assert.AreEqual("B2", queue[1].InstanceID);
            Assert.AreEqual("B3", queue[2].InstanceID);
        }

        /// <summary>
        /// Verifies enqueue order using remaining maintenance capacity succeeds.
        /// </summary>
        [Test]
        public void Enqueue_OrderUsingRemainingMaintenanceCapacity_Succeeds()
        {
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = _empire.InstanceID,
                ConstructionCost = 100,
                MaintenanceCost = _empire.ProjectedMaintenanceHeadroom,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            bool enqueued = _manager.Enqueue(_coruscant, mine, _coruscant);

            Assert.IsTrue(enqueued);
            Assert.AreEqual(0, _empire.ProjectedMaintenanceHeadroom);
        }

        /// <summary>
        /// Verifies enqueue valid building attaches to scene graph.
        /// </summary>
        [Test]
        public void Enqueue_ValidBuilding_AttachesToSceneGraph()
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

            // Building should be in the _game's node registry
            Building retrievedBuilding = _game.GetSceneNodeByInstanceID<Building>("MINE1");
            Assert.IsNotNull(retrievedBuilding);
            Assert.AreEqual("MINE1", retrievedBuilding.InstanceID);
        }

        /// <summary>
        /// Verifies enqueue building in building state succeeds.
        /// </summary>
        [Test]
        public void Enqueue_BuildingInBuildingState_Succeeds()
        {
            // Items can be enqueued while in Building state - the state just tracks
            // that they're under construction. Multiple items can be Building simultaneously.
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                BuildingType = BuildingType.Mine,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                BuildingType = BuildingType.Mine,
            };

            // Both should enqueue successfully
            bool firstResult = _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            bool secondResult = _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);

            Assert.IsTrue(firstResult);
            Assert.IsTrue(secondResult);

            List<IManufacturable> queue = _coruscant.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(2, queue.Count);
        }

        /// <summary>
        /// Verifies enqueue attached to different parent throws exception.
        /// </summary>
        [Test]
        public void Enqueue_AttachedToDifferentParent_ThrowsException()
        {
            // Create second planet
            Planet tatooine = new Planet
            {
                InstanceID = "TATOOINE",
                OwnerInstanceID = "EMPIRE",
                PositionX = 100,
                PositionY = 100,
                NumRawResourceNodes = 50,
                IsColonized = true,
                EnergyCapacity = 5,
            };
            _game.AttachNode(tatooine, _game.GetSceneNodesByType<PlanetSector>()[0]);

            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            // Attach to first planet (complete, so goes into Buildings list)
            _game.AttachNode(mine, _coruscant);

            // Attempt to enqueue on different planet should throw
            Assert.Throws<InvalidOperationException>(() =>
            {
                _manager.Enqueue(tatooine, mine, tatooine, ignoreCost: true);
            });
        }

        /// <summary>
        /// Verifies enqueue duplicate instance throws exception.
        /// </summary>
        [Test]
        public void Enqueue_DuplicateInstance_ThrowsException()
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

            // Second enqueue should throw - same instance already has a parent
            Assert.Throws<InvalidOperationException>(() =>
            {
                _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            });
        }

        /// <summary>
        /// Verifies enqueue two instances same type both added.
        /// </summary>
        [Test]
        public void Enqueue_TwoInstancesSameType_BothAdded()
        {
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.AreEqual(2, queue[ManufacturingType.Building].Count);
            Assert.AreEqual("MINE1", queue[ManufacturingType.Building][0].InstanceID);
            Assert.AreEqual("MINE2", queue[ManufacturingType.Building][1].InstanceID);
        }

        /// <summary>
        /// Verifies enqueue different faction returns false.
        /// </summary>
        [Test]
        public void Enqueue_DifferentFaction_ReturnsFalse()
        {
            // CanAcceptChild rejects the building before AttachNode is called.
            Building rebelBuilding = new Building
            {
                InstanceID = "REBEL_BUILDING",
                OwnerInstanceID = "REBELS",
                BuildingType = BuildingType.Mine,
                ManufacturingFactionInstanceIDs = new List<string> { "REBELS" },
            };

            bool result = _manager.Enqueue(_coruscant, rebelBuilding, _coruscant, ignoreCost: true);
            Assert.IsFalse(
                result,
                "Enqueueing a building owned by a different faction must return false"
            );
        }

        /// <summary>
        /// Verifies enqueue insufficient refined materials still queues.
        /// </summary>
        [Test]
        public void Enqueue_InsufficientRefinedMaterials_StillQueues()
        {
            // With default setup (no mines/refineries), faction has no materials
            Building expensive = new Building
            {
                InstanceID = "EXPENSIVE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 9999, // Very high cost
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };

            // Verify we have insufficient materials
            int available = _empire.RefinedMaterials;
            Assert.Less(available, 9999);

            bool result = _manager.Enqueue(_coruscant, expensive, _coruscant, ignoreCost: false);

            Assert.IsTrue(result);
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count);
        }

        /// <summary>
        /// Verifies enqueue ignore cost flag bypasses funds.
        /// </summary>
        [Test]
        public void Enqueue_IgnoreCostFlag_BypassesFunds()
        {
            // With default setup, faction has no materials
            Building expensive = new Building
            {
                InstanceID = "EXPENSIVE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 9999, // Very high cost
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };

            // Verify we have insufficient materials
            int available = _empire.RefinedMaterials;
            Assert.Less(available, 9999);

            bool result = _manager.Enqueue(_coruscant, expensive, _coruscant, ignoreCost: true);

            Assert.IsTrue(result); // Should succeed despite insufficient funds when ignoreCost=true
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count);
        }

        /// <summary>
        /// Verifies enqueue with sufficient stockpile does not deduct construction cost.
        /// </summary>
        [Test]
        public void Enqueue_WithSufficientStockpile_DoesNotDeductConstructionCost()
        {
            _empire.RefinedMaterialStockpile = 500;
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 200,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };

            bool result = _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: false);

            Assert.IsTrue(result);
            Assert.AreEqual(500, _empire.RefinedMaterialStockpile);
        }

        /// <summary>
        /// Verifies enqueue with ignore cost true does not deduct stockpile.
        /// </summary>
        [Test]
        public void Enqueue_WithIgnoreCostTrue_DoesNotDeductStockpile()
        {
            _empire.RefinedMaterialStockpile = 500;
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 200,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };

            bool result = _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);

            Assert.IsTrue(result);
            Assert.AreEqual(500, _empire.RefinedMaterialStockpile);
        }

        /// <summary>
        /// Verifies enqueue zero cost item completes immediately.
        /// </summary>
        [Test]
        public void Enqueue_ZeroCostItem_CompletesImmediately()
        {
            Building free = new Building
            {
                InstanceID = "FREE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 0, // Zero cost
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };

            bool result = _manager.Enqueue(_coruscant, free, _coruscant, ignoreCost: true);
            Assert.IsTrue(result);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count);

            // Process one tick - zero cost should complete immediately
            _manager.ProcessTick();

            // Verify completion behavior
            Assert.AreEqual(ManufacturingStatus.Complete, free.ManufacturingStatus);
            Assert.AreEqual(
                0,
                _coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count
            );
        }

        /// <summary>
        /// Verifies enqueue fleet destination owned by different faction returns false.
        /// </summary>
        [Test]
        public void Enqueue_FleetDestinationOwnedByDifferentFaction_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            game.GetFactions().Add(new Faction { InstanceID = "rebels" });
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "rebels");
            game.AttachNode(fleet, planet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "rebels",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(carrier, fleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                game,
                new FleetSystem(game),
                _movement
            );

            Assert.IsFalse(mfg.Enqueue(planet, fighter, fleet, ignoreCost: true));
            Assert.IsNull(fighter.GetParent());
        }

        /// <summary>
        /// Verifies enqueue capital ship destination available queues passenger on ship.
        /// </summary>
        [Test]
        public void Enqueue_CapitalShipDestinationAvailable_QueuesPassengerOnShip()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(carrier, fleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                game,
                new FleetSystem(game),
                _movement
            );

            Assert.IsTrue(mfg.Enqueue(planet, fighter, carrier, ignoreCost: true));
            Assert.AreEqual(carrier, fighter.GetParent());
            Assert.Contains(fighter, carrier.GetChildren<Starfighter>().ToList());
        }

        /// <summary>
        /// Verifies enqueue capital ship destination in transit returns false.
        /// </summary>
        [Test]
        public void Enqueue_CapitalShipDestinationInTransit_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState(),
            };
            game.AttachNode(carrier, fleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                game,
                new FleetSystem(game),
                _movement
            );

            Assert.IsFalse(mfg.Enqueue(planet, fighter, carrier, ignoreCost: true));
            Assert.IsNull(fighter.GetParent());
        }

        /// <summary>
        /// Verifies enqueue capital ship destination under construction returns false.
        /// </summary>
        [Test]
        public void Enqueue_CapitalShipDestinationUnderConstruction_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(carrier, fleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                game,
                new FleetSystem(game),
                _movement
            );

            Assert.IsFalse(mfg.Enqueue(planet, fighter, carrier, ignoreCost: true));
            Assert.IsNull(fighter.GetParent());
        }

        /// <summary>
        /// Verifies enqueue fleet destination with only unfinished carrier returns false.
        /// </summary>
        [Test]
        public void Enqueue_FleetDestinationWithOnlyUnfinishedCarrier_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Building,
            };
            game.AttachNode(carrier, fleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem manufacturing = new ManufacturingSystem(
                game,
                new FleetSystem(game),
                _movement
            );

            Assert.IsFalse(manufacturing.Enqueue(planet, fighter, fleet, ignoreCost: true));
            Assert.IsNull(fighter.GetParent());
        }

        /// <summary>
        /// Verifies clear queue queued building removes item and queue bucket.
        /// </summary>
        [Test]
        public void ClearQueue_QueuedBuilding_RemovesItemAndQueueBucket()
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

            bool cleared = _manager.ClearQueue(_coruscant, ManufacturingType.Building);
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();

            Assert.IsTrue(cleared);
            Assert.IsFalse(queue.ContainsKey(ManufacturingType.Building));
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>("MINE1"));
        }

        /// <summary>
        /// Verifies clear queue building queue clears queue and queued destination buildings.
        /// </summary>
        [Test]
        public void ClearQueue_BuildingQueue_ClearsQueueAndQueuedDestinationBuildings()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            Planet destination = CreateOrderTestPlanet(game, "p2", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            Building template = CreateOrderTestBuildingTemplate("mine");

            bool started = manager.StartManufacturing(producer, template, destination, 2, "empire");
            List<IManufacturable> queued = new List<IManufacturable>(
                producer.GetManufacturingQueue()[ManufacturingType.Building]
            );

            bool stopped = manager.ClearQueue(producer, ManufacturingType.Building);

            Assert.IsTrue(started);
            Assert.IsTrue(stopped);
            Assert.IsFalse(
                producer.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
            Assert.AreEqual(0, destination.GetChildren<Building>().Count);
            foreach (IManufacturable item in queued)
                Assert.IsNull(((ISceneNode)item).GetParent());
        }

        /// <summary>
        /// Verifies clear queue empty queue returns false.
        /// </summary>
        [Test]
        public void ClearQueue_EmptyQueue_ReturnsFalse()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));

            bool stopped = manager.ClearQueue(planet, ManufacturingType.Building);

            Assert.IsFalse(stopped);
        }

        /// <summary>
        /// Verifies attach node different owner throws exception.
        /// </summary>
        [Test]
        public void AttachNode_DifferentOwner_ThrowsException()
        {
            // Scene graph must reject a building whose owner doesn't match the planet's owner.
            Building rebelBuilding = new Building
            {
                InstanceID = "REBEL_BUILDING",
                OwnerInstanceID = "REBELS",
                BuildingType = BuildingType.Mine,
                ManufacturingFactionInstanceIDs = new List<string> { "REBELS" },
            };

            Assert.Throws<SceneAccessException>(
                () => _game.AttachNode(rebelBuilding, _coruscant),
                "Attaching a building to a planet owned by a different faction must throw SceneAccessException"
            );
        }

        /// <summary>
        /// Verifies cancel manufacturing reserved input completes and discards facility cycle.
        /// </summary>
        [Test]
        public void CancelManufacturing_ReservedInput_CompletesAndDiscardsFacilityCycle()
        {
            _empire.RefinedMaterialStockpile = 1;
            _shipyard.ProcessRate = 2;
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 2,
                BaseBuildSpeed = 10,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
            };
            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);

            _manager.ProcessTick();
            bool cancelled = _manager.CancelManufacturing(mine, _empire.InstanceID);
            _manager.ProcessTick();

            Assert.IsTrue(cancelled);
            Assert.AreEqual(1, _empire.RefinedMaterialStockpile);
            Assert.IsFalse(_shipyard.ProductionInputReserved);
            Assert.IsFalse(_shipyard.ProductionPointReady);
            Assert.IsNull(_game.GetSceneNodeByInstanceID<Building>(mine.InstanceID));
        }

        /// <summary>
        /// Verifies cancel manufacturing queued item restores maintenance headroom.
        /// </summary>
        [Test]
        public void CancelManufacturing_QueuedItem_RestoresMaintenanceHeadroom()
        {
            const int maintenanceCost = 12;
            int initialHeadroom = _empire.ProjectedMaintenanceHeadroom;
            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                MaintenanceCost = maintenanceCost,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };
            _manager.Enqueue(_coruscant, mine, _coruscant, ignoreCost: true);
            Assert.AreEqual(
                initialHeadroom - maintenanceCost,
                _empire.ProjectedMaintenanceHeadroom
            );

            bool cancelled = _manager.CancelManufacturing(mine, _empire.InstanceID);

            Assert.IsTrue(cancelled);
            Assert.AreEqual(initialHeadroom, _empire.ProjectedMaintenanceHeadroom);
        }

        /// <summary>
        /// Verifies cancel manufacturing queued item removes only selected item.
        /// </summary>
        [Test]
        public void CancelManufacturing_QueuedItem_RemovesOnlySelectedItem()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            Planet destination = CreateOrderTestPlanet(game, "p2", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            Building template = CreateOrderTestBuildingTemplate("mine");
            Assert.IsTrue(manager.StartManufacturing(producer, template, destination, 2, "empire"));
            List<IManufacturable> queue = producer.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            IManufacturable cancelled = queue[0];
            IManufacturable retained = queue[1];

            bool result = manager.CancelManufacturing(cancelled, "empire");

            Assert.IsTrue(result);
            Assert.AreEqual(1, queue.Count);
            Assert.AreSame(retained, queue[0]);
            Assert.AreEqual(0, cancelled.ManufacturingQueueSequence);
            Assert.AreEqual(2, retained.ManufacturingQueueSequence);
            Assert.IsNull(((ISceneNode)cancelled).GetParent());
            Assert.AreSame(destination, ((ISceneNode)retained).GetParent());
        }

        /// <summary>
        /// Verifies cancel manufacturing queued items removes complete selection.
        /// </summary>
        [Test]
        public void CancelManufacturing_QueuedItems_RemovesCompleteSelection()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            Planet destination = CreateOrderTestPlanet(game, "p2", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            Building template = CreateOrderTestBuildingTemplate("mine");
            Assert.IsTrue(manager.StartManufacturing(producer, template, destination, 2, "empire"));
            List<IManufacturable> queued = producer
                .GetManufacturingQueue()[ManufacturingType.Building]
                .ToList();

            bool result = manager.CancelManufacturing(queued, "empire");

            Assert.IsTrue(result);
            Assert.IsFalse(
                producer.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
            Assert.IsTrue(queued.All(item => ((ISceneNode)item).GetParent() == null));
        }

        /// <summary>
        /// Verifies cancel manufacturing last capital ship removes destination fleet.
        /// </summary>
        [Test]
        public void CancelManufacturing_LastCapitalShip_RemovesDestinationFleet()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            CapitalShip template = CreateOrderTestCapitalShipTemplate(
                "dreadnaught",
                "Dreadnaught",
                0
            );
            Assert.IsTrue(manager.StartManufacturing(planet, template, planet, 1, "empire"));
            IManufacturable queued = planet.GetManufacturingQueue()[ManufacturingType.Ship][0];

            bool result = manager.CancelManufacturing(queued, "empire");

            Assert.IsTrue(result);
            Assert.IsEmpty(planet.GetChildren<Fleet>());
            Assert.IsNull(((ISceneNode)queued).GetParent());
        }

        /// <summary>
        /// Verifies cancel manufacturing other faction does not remove item.
        /// </summary>
        [Test]
        public void CancelManufacturing_OtherFaction_DoesNotRemoveItem()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            Planet destination = CreateOrderTestPlanet(game, "p2", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            Building template = CreateOrderTestBuildingTemplate("mine");
            Assert.IsTrue(manager.StartManufacturing(producer, template, destination, 1, "empire"));
            IManufacturable queued = producer.GetManufacturingQueue()[ManufacturingType.Building][
                0
            ];

            bool result = manager.CancelManufacturing(queued, "other");

            Assert.IsFalse(result);
            Assert.AreSame(destination, ((ISceneNode)queued).GetParent());
            Assert.AreSame(queued, producer.GetManufacturingQueue()[ManufacturingType.Building][0]);
        }

        /// <summary>
        /// Verifies get manufacturing queue no items returns empty dictionary.
        /// </summary>
        [Test]
        public void GetManufacturingQueue_NoItems_ReturnsEmptyDictionary()
        {
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();

            Assert.IsNotNull(queue);
            Assert.IsEmpty(queue);
        }

        /// <summary>
        /// Verifies get manufacturing queue two items returns correct state.
        /// </summary>
        [Test]
        public void GetManufacturingQueue_TwoItems_ReturnsCorrectState()
        {
            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };

            _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();

            Assert.AreEqual(2, queue[ManufacturingType.Building].Count);
            Assert.AreEqual(mine1, queue[ManufacturingType.Building][0]);
            Assert.AreEqual(mine2, queue[ManufacturingType.Building][1]);
        }

        /// <summary>
        /// Verifies rebuild queues empty game no queues.
        /// </summary>
        [Test]
        public void RebuildQueues_EmptyGame_NoQueues()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.GetFactions().Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _game.AttachNode(planet, planetSector);

            ManufacturingSystem _manager = new ManufacturingSystem(_game, new FleetSystem(_game));
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(0, queue.Count, "Empty game should have no queues");
        }

        /// <summary>
        /// Verifies rebuild queues persisted order restores original queue order.
        /// </summary>
        [Test]
        public void RebuildQueues_PersistedOrder_RestoresOriginalQueueOrder()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            Building first = CreateOrderTestBuildingTemplate("first");
            Building second = CreateOrderTestBuildingTemplate("second");
            first.OwnerInstanceID = "empire";
            second.OwnerInstanceID = "empire";
            first.ProducerPlanetID = planet.InstanceID;
            second.ProducerPlanetID = planet.InstanceID;
            first.ManufacturingQueueSequence = 2;
            second.ManufacturingQueueSequence = 1;
            first.ManufacturingStatus = ManufacturingStatus.Building;
            second.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(first, planet);
            game.AttachNode(second, planet);

            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            manager.RebuildQueues();

            CollectionAssert.AreEqual(
                new IManufacturable[] { second, first },
                planet.GetManufacturingQueue()[ManufacturingType.Building]
            );
        }

        /// <summary>
        /// Verifies rebuild queues multiple planets correct grouping.
        /// </summary>
        [Test]
        public void RebuildQueues_MultiplePlanets_CorrectGrouping()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.GetFactions().Add(new Faction { InstanceID = "empire" });

            Planet planet1 = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            Planet planet2 = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _game.AttachNode(planet1, planetSector);
            _game.AttachNode(planet2, planetSector);

            Building item1 = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            Regiment item2 = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ConstructionCost = 50,
            };
            Building item3 = new Building
            {
                InstanceID = "b2",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p2",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };

            _game.AttachNode(item1, planet1);
            _game.AttachNode(item2, planet1);
            _game.AttachNode(item3, planet2);

            ManufacturingSystem _manager = new ManufacturingSystem(_game, new FleetSystem(_game));
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue1 =
                planet1.GetManufacturingQueue();
            Assert.AreEqual(2, queue1.Count, "Planet 1 should have 2 types");
            Assert.AreEqual(1, queue1[ManufacturingType.Building].Count);
            Assert.AreEqual(1, queue1[ManufacturingType.Troop].Count);
            Assert.AreEqual(1, item1.ManufacturingQueueSequence);
            Assert.AreEqual(1, item2.ManufacturingQueueSequence);

            Dictionary<ManufacturingType, List<IManufacturable>> queue2 =
                planet2.GetManufacturingQueue();
            Assert.AreEqual(1, queue2.Count, "Planet 2 should have 1 type");
            Assert.AreEqual(1, queue2[ManufacturingType.Building].Count);
        }

        /// <summary>
        /// Verifies rebuild queues called twice no duplication.
        /// </summary>
        [Test]
        public void RebuildQueues_CalledTwice_NoDuplication()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.GetFactions().Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _game.AttachNode(planet, planetSector);

            Building item = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            _game.AttachNode(item, planet);

            ManufacturingSystem _manager = new ManufacturingSystem(_game, new FleetSystem(_game));
            _manager.RebuildQueues();
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count, "No duplication");
        }

        /// <summary>
        /// Verifies rebuild queues only building ignores complete.
        /// </summary>
        [Test]
        public void RebuildQueues_OnlyBuilding_IgnoresComplete()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.GetFactions().Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _game.AttachNode(planet, planetSector);

            Building itemBuilding = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            Building itemComplete = new Building
            {
                InstanceID = "b2",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Complete,
                ManufacturingProgress = 100,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };

            _game.AttachNode(itemBuilding, planet);
            _game.AttachNode(itemComplete, planet);

            ManufacturingSystem _manager = new ManufacturingSystem(_game, new FleetSystem(_game));
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count, "Only Building status");
            Assert.AreEqual("b1", queue[ManufacturingType.Building][0].InstanceID);
        }

        /// <summary>
        /// Verifies rebuild queues no producer planet id skips item.
        /// </summary>
        [Test]
        public void RebuildQueues_NoProducerPlanetID_SkipsItem()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.GetFactions().Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _game.AttachNode(planet, planetSector);

            Building itemNoProducer = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = null,
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };

            _game.AttachNode(itemNoProducer, planet);

            ManufacturingSystem _manager = new ManufacturingSystem(_game, new FleetSystem(_game));
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(0, queue.Count, "Should skip null ProducerPlanetID");
        }

        /// <summary>
        /// Verifies rebuild queues invalid producer planet id skips item.
        /// </summary>
        [Test]
        public void RebuildQueues_InvalidProducerPlanetID_SkipsItem()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.GetFactions().Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSector planetSector = new PlanetSector { InstanceID = "sector1" };
            _game.AttachNode(planetSector, _game.Galaxy);
            _game.AttachNode(planet, planetSector);

            Building itemOrphan = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p999",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };

            _game.AttachNode(itemOrphan, planet);

            ManufacturingSystem _manager = new ManufacturingSystem(_game, new FleetSystem(_game));
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(0, queue.Count, "Should skip invalid ProducerPlanetID");
        }

        /// <summary>
        /// Verifies enqueue capital ship valid ship attaches to fleet at planet.
        /// </summary>
        [Test]
        public void EnqueueCapitalShip_ValidShip_AttachesToFleetAtPlanet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, planet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, fleet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);

            Assert.AreEqual(2, fleet.GetChildren<CapitalShip>().Count, "Ship should be in fleet.");
            Assert.AreEqual("cs1", fleet.GetChildren<CapitalShip>()[1].InstanceID);
            Assert.AreEqual(ManufacturingStatus.Building, ship.ManufacturingStatus);

            CapitalShip found = _game.GetSceneNodeByInstanceID<CapitalShip>("cs1");
            Assert.IsNotNull(found, "Ship should be in the scene graph during production.");
        }

        /// <summary>
        /// Verifies enqueue capital ship planet destination returns false.
        /// </summary>
        [Test]
        public void EnqueueCapitalShip_PlanetDestination_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            bool result = mfg.Enqueue(planet, ship, planet, ignoreCost: true);

            Assert.IsFalse(
                result,
                "Capital ship production requires an existing fleet destination."
            );
        }

        /// <summary>
        /// Verifies enqueue capital ship with fleet destination joins fleet.
        /// </summary>
        [Test]
        public void EnqueueCapitalShip_WithFleetDestination_JoinsFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Fleet existingFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(existingFleet, planet);
            CapitalShip existingShip = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(existingShip, existingFleet);

            CapitalShip newShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, newShip, existingFleet, ignoreCost: true);

            List<Fleet> fleets = planet.GetChildren<Fleet>().ToList();
            Assert.AreEqual(1, fleets.Count, "Ship should join the explicitly specified fleet.");
            Assert.AreEqual(2, fleets[0].GetChildren<CapitalShip>().Count);
            Assert.AreEqual(ManufacturingStatus.Building, newShip.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies enqueue capital ship planet destination with fleet present still returns false.
        /// </summary>
        [Test]
        public void EnqueueCapitalShip_PlanetDestinationWithFleetPresent_StillReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Fleet existingFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(existingFleet, planet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, existingFleet);

            CapitalShip newShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            bool result = mfg.Enqueue(planet, newShip, planet, ignoreCost: true);

            Assert.IsFalse(
                result,
                "Capital ships must use the fleet destination overload, not the planet overload."
            );
            Assert.AreEqual(
                1,
                planet.GetChildren<Fleet>().Count,
                "No new fleet should be created."
            );
        }

        /// <summary>
        /// Verifies enqueue capital ship no owner returns false.
        /// </summary>
        [Test]
        public void EnqueueCapitalShip_NoOwner_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, planet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, fleet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = null,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            bool result = mfg.Enqueue(planet, ship, fleet, ignoreCost: true);

            Assert.IsFalse(result, "Enqueueing a capital ship with no owner should fail.");
        }

        /// <summary>
        /// Verifies enqueue two capital ships same fleet both join.
        /// </summary>
        [Test]
        public void EnqueueTwoCapitalShips_SameFleet_BothJoin()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(fleet, planet);
            CapitalShip anchor = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            _game.AttachNode(anchor, fleet);

            CapitalShip ship1 = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            CapitalShip ship2 = new CapitalShip
            {
                InstanceID = "cs2",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, ship1, fleet, ignoreCost: true);
            mfg.Enqueue(planet, ship2, fleet, ignoreCost: true);

            Assert.AreEqual(
                3,
                fleet.GetChildren<CapitalShip>().Count,
                "Both ships should join the fleet."
            );
        }

        /// <summary>
        /// Verifies enqueue two capital ships same explicit fleet join same fleet.
        /// </summary>
        [Test]
        public void EnqueueTwoCapitalShips_SameExplicitFleet_JoinSameFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Fleet targetFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(targetFleet, planet);

            CapitalShip ship1 = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            CapitalShip ship2 = new CapitalShip
            {
                InstanceID = "cs2",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, ship1, targetFleet, ignoreCost: true);
            mfg.Enqueue(planet, ship2, targetFleet, ignoreCost: true);

            List<Fleet> fleets = planet.GetChildren<Fleet>().ToList();
            Assert.AreEqual(1, fleets.Count, "Both ships explicitly target the same fleet.");
            Assert.AreEqual(2, fleets[0].GetChildren<CapitalShip>().Count);
        }

        /// <summary>
        /// Verifies enqueue building valid building parent is destination planet.
        /// </summary>
        [Test]
        public void EnqueueBuilding_ValidBuilding_ParentIsDestinationPlanet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(constructionYard, planet);

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, mine, destPlanet, ignoreCost: true);

            Assert.AreEqual(
                destPlanet,
                mine.GetParent(),
                "Building parent should be the destination planet."
            );
            Assert.AreEqual(ManufacturingStatus.Building, mine.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies enqueue starfighter valid fighter parent is destination fleet.
        /// </summary>
        [Test]
        public void EnqueueStarfighter_ValidFighter_ParentIsDestinationFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip destShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, fighter, destFleet, ignoreCost: true);

            Assert.AreEqual(
                destShip,
                fighter.GetParent(),
                "Starfighter should be immediately attached to the destination capital ship."
            );
            Assert.AreEqual(ManufacturingStatus.Building, fighter.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies enqueue regiment valid regiment parent is destination planet.
        /// </summary>
        [Test]
        public void EnqueueRegiment_ValidRegiment_ParentIsDestinationPlanet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.GetFactions().Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(
                _game,
                new FleetSystem(_game),
                _movement
            );
            mfg.Enqueue(planet, regiment, destPlanet, ignoreCost: true);

            Assert.AreEqual(
                destPlanet,
                regiment.GetParent(),
                "Regiment parent should be the destination planet."
            );
            Assert.AreEqual(ManufacturingStatus.Building, regiment.ManufacturingStatus);
        }

        /// <summary>
        /// Verifies can accept manufacturing order without maintenance headroom returns true.
        /// </summary>
        [Test]
        public void CanAcceptManufacturingOrder_WithoutMaintenanceHeadroom_ReturnsTrue()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            CapitalShip template = CreateOrderTestCapitalShipTemplate(
                "dreadnaught",
                "Dreadnaught",
                1
            );

            bool canAccept = manager.CanAcceptManufacturingOrder(
                planet,
                template,
                planet,
                1,
                "empire"
            );

            Assert.IsTrue(canAccept);
            Assert.IsFalse(manager.CanStartManufacturing(planet, template, planet, 1, "empire"));
        }

        /// <summary>
        /// Verifies start manufacturing capital ships creates one destination fleet.
        /// </summary>
        [Test]
        public void StartManufacturing_CapitalShips_CreatesOneDestinationFleet()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            CapitalShip template = CreateOrderTestCapitalShipTemplate(
                "dreadnaught",
                "Dreadnaught",
                0
            );

            bool started = manager.StartManufacturing(planet, template, planet, 2, "empire");

            Assert.IsTrue(started);
            List<Fleet> fleets = planet.GetChildren<Fleet>().ToList();
            Assert.AreEqual(1, fleets.Count);
            Assert.AreEqual(2, fleets[0].GetChildren<CapitalShip>().Count);
            Assert.AreEqual(2, planet.GetManufacturingQueue()[ManufacturingType.Ship].Count);
            Assert.AreSame(
                fleets[0].GetChildren<CapitalShip>()[0],
                planet.GetManufacturingQueue()[ManufacturingType.Ship][0]
            );
            Assert.AreSame(
                fleets[0].GetChildren<CapitalShip>()[1],
                planet.GetManufacturingQueue()[ManufacturingType.Ship][1]
            );
        }

        /// <summary>
        /// Verifies capital ship production at a planet uses its first stationary friendly fleet.
        /// </summary>
        [Test]
        public void StartManufacturing_CapitalShipWithExistingFleets_AddsToFirstFleet()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            Fleet firstFleet = EntityFactory.CreateFleet("first", "empire");
            Fleet secondFleet = EntityFactory.CreateFleet("second", "empire");
            game.AttachNode(firstFleet, planet);
            game.AttachNode(secondFleet, planet);
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            CapitalShip template = CreateOrderTestCapitalShipTemplate(
                "dreadnaught",
                "Dreadnaught",
                0
            );

            bool started = manager.StartManufacturing(planet, template, planet, 1, "empire");

            Assert.IsTrue(started);
            Assert.AreEqual(2, planet.GetChildren<Fleet>().Count);
            Assert.AreEqual(1, firstFleet.GetChildren<CapitalShip>().Count);
            Assert.AreEqual(0, secondFleet.GetChildren<CapitalShip>().Count);
        }

        /// <summary>
        /// Verifies start manufacturing same project appends requested copies.
        /// </summary>
        [Test]
        public void StartManufacturing_SameProject_AppendsRequestedCopies()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            Building template = CreateOrderTestBuildingTemplate("mine");
            Assert.IsTrue(manager.StartManufacturing(planet, template, planet, 2, "empire"));

            bool started = manager.StartManufacturing(planet, template, planet, 1, "empire");

            Assert.IsTrue(started);
            List<IManufacturable> queue = planet.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(3, queue.Count);
            Assert.IsTrue(queue.All(item => item.GetTypeID() == "mine"));
        }

        /// <summary>
        /// Verifies start manufacturing different project replaces entire production lane.
        /// </summary>
        [Test]
        public void StartManufacturing_DifferentProject_ReplacesEntireProductionLane()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            Building mines = CreateOrderTestBuildingTemplate("mine");
            Building refineries = CreateOrderTestBuildingTemplate("refinery");
            Assert.IsTrue(manager.StartManufacturing(planet, mines, planet, 3, "empire"));
            List<IManufacturable> replacedItems = planet
                .GetManufacturingQueue()[ManufacturingType.Building]
                .ToList();

            bool started = manager.StartManufacturing(planet, refineries, planet, 2, "empire");

            Assert.IsTrue(started);
            List<IManufacturable> queue = planet.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(2, queue.Count);
            Assert.IsTrue(queue.All(item => item.GetTypeID() == "refinery"));
            Assert.IsTrue(replacedItems.All(item => item.GetParent() == null));
        }

        /// <summary>
        /// Verifies retarget manufacturing destination queued lane moves every item.
        /// </summary>
        [Test]
        public void RetargetManufacturingDestination_QueuedLaneMovesEveryItem()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestConstructionPlanet(game, "producer", "empire");
            Planet destination = CreateOrderTestPlanet(game, "destination", "empire");
            FleetSystem fleetSystem = new FleetSystem(game);
            MovementSystem movementSystem = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                fleetSystem
            );
            ManufacturingSystem manager = new ManufacturingSystem(
                game,
                fleetSystem,
                movementSystem
            );
            Building template = CreateOrderTestBuildingTemplate("mine");
            Assert.IsTrue(manager.StartManufacturing(producer, template, producer, 2, "empire"));

            bool retargeted = manager.RetargetManufacturingDestination(
                producer,
                ManufacturingType.Building,
                destination,
                "empire"
            );

            Assert.IsTrue(retargeted);
            Assert.IsTrue(
                producer
                    .GetManufacturingQueue()[ManufacturingType.Building]
                    .OfType<ISceneNode>()
                    .All(item => ReferenceEquals(item.GetParent(), destination))
            );
        }

        /// <summary>
        /// Verifies start manufacturing capital ship rejected removes empty destination fleet.
        /// </summary>
        [Test]
        public void StartManufacturing_CapitalShipRejected_RemovesEmptyDestinationFleet()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestShipyardPlanet(game, "p1", "empire");
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            CapitalShip template = CreateOrderTestCapitalShipTemplate(
                "dreadnaught",
                "Dreadnaught",
                1
            );

            bool started = manager.StartManufacturing(planet, template, planet, 1, "empire");

            Assert.IsFalse(started);
            Assert.AreEqual(0, planet.GetChildren<Fleet>().Count);
            Assert.IsFalse(planet.GetManufacturingQueue().ContainsKey(ManufacturingType.Ship));
        }

        /// <summary>
        /// Verifies start manufacturing order exceeds destination capacity does not queue partial order.
        /// </summary>
        [Test]
        public void StartManufacturing_OrderExceedsDestinationCapacity_DoesNotQueuePartialOrder()
        {
            GameRoot game = CreateOrderTestGame();
            Planet producer = CreateOrderTestConstructionPlanet(game, "p1", "empire");
            Planet destination = CreateOrderTestPlanet(game, "p2", "empire");
            destination.EnergyCapacity = 1;
            ManufacturingSystem manager = new ManufacturingSystem(game, new FleetSystem(game));
            Building template = CreateOrderTestBuildingTemplate("mine");

            bool started = manager.StartManufacturing(producer, template, destination, 2, "empire");

            Assert.IsFalse(started);
            Assert.AreEqual(0, destination.GetChildren<Building>().Count);
            Assert.IsFalse(
                producer.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
        }

        /// <summary>
        /// Verifies estimate manufacturing ticks mixed facility rates uses integer rate shares.
        /// </summary>
        [Test]
        public void EstimateManufacturingTicks_MixedFacilityRates_UsesIntegerRateShares()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            game.AttachNode(CreateOrderTestConstructionFacility("yard1", "empire", 3), planet);
            game.AttachNode(CreateOrderTestConstructionFacility("yard2", "empire", 6), planet);
            Building template = CreateOrderTestBuildingTemplate("mine");
            template.ConstructionCost = 1;

            int? estimate = ManufacturingSystem.EstimateManufacturingTicks(planet, template, 1);

            Assert.AreEqual(3, estimate);
        }

        /// <summary>
        /// Verifies estimate manufacturing ticks inactive facilities do not contribute.
        /// </summary>
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

            int? estimate = ManufacturingSystem.EstimateManufacturingTicks(planet, template, 1);

            Assert.AreEqual(4, estimate);
        }

        /// <summary>
        /// Verifies estimate completion ticks includes earlier queued work and current progress.
        /// </summary>
        [Test]
        public void EstimateCompletionTicks_IncludesEarlierQueuedWorkAndCurrentProgress()
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

            int? estimate = ManufacturingSystem.EstimateCompletionTicks(planet, second);

            Assert.AreEqual(42, estimate);
        }

        /// <summary>
        /// Verifies estimate completion ticks active facility cycle uses remaining cycle time.
        /// </summary>
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

            int? estimate = ManufacturingSystem.EstimateCompletionTicks(planet, item);

            Assert.AreEqual(3, estimate);
        }

        /// <summary>
        /// Verifies appended completion estimates include existing queued work.
        /// </summary>
        [Test]
        public void EstimateAppendedCompletionTicks_WithQueuedWork_IncludesQueueAndNewItems()
        {
            GameRoot game = CreateOrderTestGame();
            Planet planet = CreateOrderTestPlanet(game, "p1", "empire");
            game.AttachNode(CreateOrderTestConstructionFacility("yard", "empire", 2), planet);
            Building queued = CreateOrderTestBuildingTemplate("queued");
            queued.ConstructionCost = 10;
            queued.ManufacturingProgress = 4;
            queued.ManufacturingStatus = ManufacturingStatus.Building;
            queued.OwnerInstanceID = "empire";
            game.AttachNode(queued, planet);
            planet.AddToManufacturingQueue(queued);
            Building appended = CreateOrderTestBuildingTemplate("appended");
            appended.ConstructionCost = 5;

            int? estimate = ManufacturingSystem.EstimateAppendedCompletionTicks(
                planet,
                appended,
                1
            );

            Assert.AreEqual(22, estimate);
        }

        /// <summary>
        /// Builds shipyard planet.
        /// </summary>
        /// <param name="_game">The game.</param>
        /// <param name="planetId">The planet id.</param>
        /// <param name="factionId">The faction id.</param>
        /// <returns>The constructed shipyard planet.</returns>
        private Planet BuildShipyardPlanet(GameRoot _game, string planetId, string factionId)
        {
            PlanetSector planetSector = new PlanetSector
            {
                InstanceID = $"{planetId}_sector",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(planetSector, _game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = planetId,
                OwnerInstanceID = factionId,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                EnergyCapacity = 10,
                NumRawResourceNodes = 100,
            };
            _game.AttachNode(planet, planetSector);

            Building _shipyard = new Building
            {
                InstanceID = $"{planetId}__shipyard",
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.Shipyard,
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_shipyard, planet);

            Building trainingFacility = new Building
            {
                InstanceID = $"{planetId}_training",
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.TrainingFacility,
                ProductionType = ManufacturingType.Troop,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(trainingFacility, planet);

            AddResourceSupply(_game, planet, factionId, 1);
            Faction faction = _game.GetFactionByOwnerInstanceID(factionId);
            faction.RefinedMaterialStockpile = Math.Max(faction.RefinedMaterialStockpile, 1000);

            return planet;
        }

        /// <summary>
        /// Adds resource supply.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="factionId">The faction id.</param>
        /// <param name="count">The count.</param>
        private void AddResourceSupply(GameRoot game, Planet planet, string factionId, int count)
        {
            for (int i = 0; i < count; i++)
            {
                game.AttachNode(
                    new Building
                    {
                        InstanceID = $"{planet.InstanceID}_resource_mine_{i}",
                        OwnerInstanceID = factionId,
                        BuildingType = BuildingType.Mine,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    planet
                );
                game.AttachNode(
                    new Building
                    {
                        InstanceID = $"{planet.InstanceID}_resource_refinery_{i}",
                        OwnerInstanceID = factionId,
                        BuildingType = BuildingType.Refinery,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    planet
                );
            }
        }

        /// <summary>
        /// Adds resource supply planet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planetId">The planet id.</param>
        /// <param name="factionId">The faction id.</param>
        private void AddResourceSupplyPlanet(GameRoot game, string planetId, string factionId)
        {
            PlanetSector planetSector = new PlanetSector { InstanceID = $"{planetId}_sector" };
            game.AttachNode(planetSector, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = planetId,
                OwnerInstanceID = factionId,
                IsColonized = true,
                EnergyCapacity = 50,
                NumRawResourceNodes = 50,
            };
            game.AttachNode(planet, planetSector);
            AddResourceSupply(game, planet, factionId, 20);
        }

        /// <summary>
        /// Creates order test game.
        /// </summary>
        /// <returns>The created order test game.</returns>
        private static GameRoot CreateOrderTestGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
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
