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
        private FixedRNG _provider;
        private Faction _empire;
        private Planet _coruscant;
        private Building _shipyard;

        [SetUp]
        public void SetUp()
        {
            // Create game with galaxy
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            _game = new GameRoot(config);
            GalaxyMap galaxy = _game.Galaxy;

            // Create faction
            _empire = new Faction { InstanceID = "EMPIRE" };
            _game.Factions.Add(_empire);

            // Create planet system
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "SYSTEM1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(system, galaxy);

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
            _game.AttachNode(_coruscant, system);

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

            _movement = new MovementSystem(_game, new FogOfWarSystem(_game));
            _provider = new FixedRNG();
            _manager = new ManufacturingSystem(_game, _provider, _movement);
        }

        [Test]
        public void ProcessTick_EmptyGame_DoesNotCrash()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            GameRoot emptyGame = new GameRoot(config);
            ManufacturingSystem emptyManager = new ManufacturingSystem(
                emptyGame,
                new FixedRNG(),
                new MovementSystem(emptyGame, new FogOfWarSystem(emptyGame))
            );

            emptyManager.ProcessTick();

            Assert.Pass();
        }

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

        [Test]
        public void Enqueue_RegimentToUncolonizedPlanet_ReturnsFalse()
        {
            PlanetSystem system = _coruscant.GetParentOfType<PlanetSystem>();
            Planet destination = new Planet
            {
                InstanceID = "UNCHARTED",
                OwnerInstanceID = null,
                IsColonized = false,
            };
            _game.AttachNode(destination, system);

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
            bool result1 = _manager.Enqueue(_coruscant, mine1, _coruscant, ignoreCost: true);
            bool result2 = _manager.Enqueue(_coruscant, mine2, _coruscant, ignoreCost: true);

            Assert.IsTrue(result1);
            Assert.IsTrue(result2);

            List<IManufacturable> queue = _coruscant.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(2, queue.Count);
        }

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
            _game.AttachNode(tatooine, _game.GetSceneNodesByType<PlanetSystem>()[0]);

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

        [Test]
        public void AttachNode_DifferentOwner_ThrowsException()
        {
            // Scene graph must reject a building whose owner doesn't match the planet's owner.
            Building rebelBuilding = new Building
            {
                InstanceID = "REBEL_BUILDING",
                OwnerInstanceID = "REBELS",
                BuildingType = BuildingType.Mine,
                AllowedOwnerInstanceIDs = new List<string> { "REBELS" },
            };

            Assert.Throws<SceneAccessException>(
                () => _game.AttachNode(rebelBuilding, _coruscant),
                "Attaching a building to a planet owned by a different faction must throw SceneAccessException"
            );
        }

        [Test]
        public void Enqueue_DifferentFaction_ReturnsFalse()
        {
            // CanAcceptChild rejects the building before AttachNode is called.
            Building rebelBuilding = new Building
            {
                InstanceID = "REBEL_BUILDING",
                OwnerInstanceID = "REBELS",
                BuildingType = BuildingType.Mine,
                AllowedOwnerInstanceIDs = new List<string> { "REBELS" },
            };

            bool result = _manager.Enqueue(_coruscant, rebelBuilding, _coruscant, ignoreCost: true);
            Assert.IsFalse(
                result,
                "Enqueueing a building owned by a different faction must return false"
            );
        }

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

        [Test]
        public void ProcessTick_OverflowProgress_CarriesToNextItem()
        {
            AddResourceSupply(1);
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

        [Test]
        public void ProcessTick_ProductionBuildingRemoved_StopsProgress()
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

            // Second tick should not advance progress (no production source)
            _manager.ProcessTick();
            Assert.AreEqual(progressAfterTick1, mine.ManufacturingProgress); // No change
        }

        [Test]
        public void ProcessTick_MultipleProductionSources_StackCorrectly()
        {
            AddResourceSupply(1);
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
            Assert.GreaterOrEqual(_empire.RefinedMaterialSupply, 2);

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

        [Test]
        public void ProcessTick_FasterProductionSource_CompletesCycleFirst()
        {
            AddResourceSupply(1);
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
            Assert.GreaterOrEqual(_empire.RefinedMaterialSupply, 2);

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

        private void AddResourceSupply(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _game.AttachNode(
                    new Building
                    {
                        InstanceID = $"EXTRA_RESOURCE_MINE_{i}",
                        OwnerInstanceID = "EMPIRE",
                        BuildingType = BuildingType.Mine,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    _coruscant
                );
                _game.AttachNode(
                    new Building
                    {
                        InstanceID = $"EXTRA_RESOURCE_REFINERY_{i}",
                        OwnerInstanceID = "EMPIRE",
                        BuildingType = BuildingType.Refinery,
                        ManufacturingStatus = ManufacturingStatus.Complete,
                    },
                    _coruscant
                );
            }
        }

        [Test]
        public void ProcessTick_WithNoRefinedSupply_KeepsFacilityPointReady()
        {
            foreach (
                Building refinery in _coruscant
                    .GetAllBuildings()
                    .Where(building => building.BuildingType == BuildingType.Refinery)
            )
            {
                refinery.ManufacturingStatus = ManufacturingStatus.Building;
            }

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
            Assert.AreEqual(0, mine.ManufacturingProgress);
            Assert.IsTrue(_shipyard.ProductionPointReady);

            foreach (
                Building refinery in _coruscant
                    .GetAllBuildings()
                    .Where(building => building.BuildingType == BuildingType.Refinery)
            )
            {
                refinery.ManufacturingStatus = ManufacturingStatus.Complete;
            }

            _manager.ProcessTick();
            Assert.AreEqual(1, mine.ManufacturingProgress);
            Assert.IsFalse(_shipyard.ProductionPointReady);
        }

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
            _game.Factions.Add(new Faction { InstanceID = "REBELLION" });
            _coruscant.OwnerInstanceID = "REBELLION";

            _manager.ProcessTick();

            // Building should still belong to original producer (EMPIRE)
            Assert.AreEqual("EMPIRE", mine.OwnerInstanceID);
            Assert.AreEqual("EMPIRE", mine.ProducerOwnerID);
        }

        [Test]
        public void GetManufacturingQueue_NoItems_ReturnsEmptyDictionary()
        {
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();

            Assert.IsNotNull(queue);
            // Empty queue may not have the key yet - check both cases
            if (queue.ContainsKey(ManufacturingType.Building))
            {
                Assert.AreEqual(0, queue[ManufacturingType.Building].Count);
            }
            else
            {
                // Key doesn't exist yet, which is fine for an empty queue
                Assert.Pass();
            }
        }

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

        [Test]
        public void RebuildQueues_EmptyGame_NoQueues()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(planet, system);

            ManufacturingSystem _manager = new ManufacturingSystem(_game);
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(0, queue.Count, "Empty game should have no queues");
        }

        [Test]
        public void RebuildQueues_SinglePlanet_CorrectOrder()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(planet, system);

            Building item1 = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 10,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            Building item2 = new Building
            {
                InstanceID = "b2",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 5,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };
            Building item3 = new Building
            {
                InstanceID = "b3",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
            };

            _game.AttachNode(item1, planet);
            _game.AttachNode(item2, planet);
            _game.AttachNode(item3, planet);

            ManufacturingSystem _manager = new ManufacturingSystem(_game);
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(1, queue.Count, "Should have one manufacturing type");
            Assert.IsTrue(queue.ContainsKey(ManufacturingType.Building));

            List<IManufacturable> items = queue[ManufacturingType.Building];
            Assert.AreEqual(3, items.Count, "Should have 3 items");
            Assert.AreEqual("b3", items[0].InstanceID, "Lowest progress first");
            Assert.AreEqual("b2", items[1].InstanceID, "Middle progress second");
            Assert.AreEqual("b1", items[2].InstanceID, "Highest progress last");
        }

        [Test]
        public void RebuildQueues_MultiplePlanets_CorrectGrouping()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.Factions.Add(new Faction { InstanceID = "empire" });

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
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(planet1, system);
            _game.AttachNode(planet2, system);

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

            ManufacturingSystem _manager = new ManufacturingSystem(_game);
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue1 =
                planet1.GetManufacturingQueue();
            Assert.AreEqual(2, queue1.Count, "Planet 1 should have 2 types");
            Assert.AreEqual(1, queue1[ManufacturingType.Building].Count);
            Assert.AreEqual(1, queue1[ManufacturingType.Troop].Count);

            Dictionary<ManufacturingType, List<IManufacturable>> queue2 =
                planet2.GetManufacturingQueue();
            Assert.AreEqual(1, queue2.Count, "Planet 2 should have 1 type");
            Assert.AreEqual(1, queue2[ManufacturingType.Building].Count);
        }

        [Test]
        public void RebuildQueues_CalledTwice_NoDuplication()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(planet, system);

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

            ManufacturingSystem _manager = new ManufacturingSystem(_game);
            _manager.RebuildQueues();
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count, "No duplication");
        }

        [Test]
        public void RebuildQueues_OnlyBuilding_IgnoresComplete()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(planet, system);

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

            ManufacturingSystem _manager = new ManufacturingSystem(_game);
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count, "Only Building status");
            Assert.AreEqual("b1", queue[ManufacturingType.Building][0].InstanceID);
        }

        private Planet BuildShipyardPlanet(GameRoot _game, string planetId, string factionId)
        {
            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = $"{planetId}_sys",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(sys, _game.Galaxy);

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
            _game.AttachNode(planet, sys);

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

            return planet;
        }

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

        private void AddResourceSupplyPlanet(GameRoot game, string planetId, string factionId)
        {
            PlanetSystem system = new PlanetSystem { InstanceID = $"{planetId}_sys" };
            game.AttachNode(system, game.Galaxy);
            Planet planet = new Planet
            {
                InstanceID = planetId,
                OwnerInstanceID = factionId,
                IsColonized = true,
                EnergyCapacity = 50,
                NumRawResourceNodes = 50,
            };
            game.AttachNode(planet, system);
            AddResourceSupply(game, planet, factionId, 20);
        }

        [Test]
        public void EnqueueCapitalShip_ValidShip_AttachesToFleetAtPlanet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);

            Assert.AreEqual(2, fleet.CapitalShips.Count, "Ship should be in fleet.");
            Assert.AreEqual("cs1", fleet.CapitalShips[1].InstanceID);
            Assert.AreEqual(ManufacturingStatus.Building, ship.ManufacturingStatus);

            CapitalShip found = _game.GetSceneNodeByInstanceID<CapitalShip>("cs1");
            Assert.IsNotNull(found, "Ship should be in the scene graph during production.");
        }

        [Test]
        public void EnqueueCapitalShip_PlanetDestination_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            bool result = mfg.Enqueue(planet, ship, planet, ignoreCost: true);

            Assert.IsFalse(
                result,
                "Capital ship production requires an existing fleet destination."
            );
        }

        [Test]
        public void EnqueueCapitalShip_WithFleetDestination_JoinsFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, newShip, existingFleet, ignoreCost: true);

            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(1, fleets.Count, "Ship should join the explicitly specified fleet.");
            Assert.AreEqual(2, fleets[0].CapitalShips.Count);
            Assert.AreEqual(ManufacturingStatus.Building, newShip.ManufacturingStatus);
        }

        [Test]
        public void Enqueue_FleetDestinationOwnedByDifferentFaction_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
            game.Factions.Add(new Faction { InstanceID = "rebels" });
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

            ManufacturingSystem mfg = new ManufacturingSystem(game, _provider, _movement);

            Assert.IsFalse(mfg.Enqueue(planet, fighter, fleet, ignoreCost: true));
            Assert.IsNull(fighter.GetParent());
        }

        [Test]
        public void Enqueue_CapitalShipDestinationAvailable_QueuesPassengerOnShip()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
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

            ManufacturingSystem mfg = new ManufacturingSystem(game, _provider, _movement);

            Assert.IsTrue(mfg.Enqueue(planet, fighter, carrier, ignoreCost: true));
            Assert.AreEqual(carrier, fighter.GetParent());
            Assert.Contains(fighter, carrier.Starfighters);
        }

        [Test]
        public void Enqueue_CapitalShipDestinationInTransit_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
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

            ManufacturingSystem mfg = new ManufacturingSystem(game, _provider, _movement);

            Assert.IsFalse(mfg.Enqueue(planet, fighter, carrier, ignoreCost: true));
            Assert.IsNull(fighter.GetParent());
        }

        [Test]
        public void Enqueue_CapitalShipDestinationUnderConstruction_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });
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

            ManufacturingSystem mfg = new ManufacturingSystem(game, _provider, _movement);

            Assert.IsFalse(mfg.Enqueue(planet, fighter, carrier, ignoreCost: true));
            Assert.IsNull(fighter.GetParent());
        }

        [Test]
        public void EnqueueCapitalShip_PlanetDestinationWithFleetPresent_StillReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            bool result = mfg.Enqueue(planet, newShip, planet, ignoreCost: true);

            Assert.IsFalse(
                result,
                "Capital ships must use the fleet destination overload, not the planet overload."
            );
            Assert.AreEqual(1, planet.GetFleets().Count, "No new fleet should be created.");
        }

        [Test]
        public void EnqueueCapitalShip_NoOwner_ReturnsFalse()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            bool result = mfg.Enqueue(planet, ship, fleet, ignoreCost: true);

            Assert.IsFalse(result, "Enqueueing a capital ship with no owner should fail.");
        }

        [Test]
        public void ProcessTick_CapitalShipBuilding_RemainsInFleetWithProgress()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
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

        [Test]
        public void ProcessTick_CapitalShipComplete_RemovedFromQueue()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, ship.ManufacturingStatus);
            bool inQueue = planet
                .GetManufacturingQueue()
                .Values.Any(list => list.Any(i => i.InstanceID == "cs1"));
            Assert.IsFalse(inQueue, "Completed ship should be removed from queue.");
        }

        [Test]
        public void ProcessTick_CapitalShipCompleteOnSamePlanet_NoMovement()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.IsNotNull(ship.GetParentOfType<Fleet>(), "Ship should be in a fleet.");
            Assert.IsNull(ship.Movement, "No _movement needed for same-planet destination.");
        }

        [Test]
        public void ProcessTick_CapitalShipCompleteOnDifferentPlanet_ShipsFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(originPlanet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, ship.ManufacturingStatus);
            Assert.IsNotNull(ship.Movement, "Ship should have _movement state for transit.");
            Assert.Greater(ship.Movement.TransitTicks, 0, "Should have travel time.");
            Assert.IsNull(fleet.Movement, "Fleet should not move — the ship travels to it.");
        }

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
            _game.Factions.Add(empire);
            _game.Factions.Add(rebels);
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

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, localMovement);
            mfg.Enqueue(originPlanet, ship, fleet, ignoreCost: true);

            destPlanet.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, ship.ManufacturingStatus);
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

        [Test]
        public void EnqueueTwoCapitalShips_SameFleet_BothJoin()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, ship1, fleet, ignoreCost: true);
            mfg.Enqueue(planet, ship2, fleet, ignoreCost: true);

            Assert.AreEqual(3, fleet.CapitalShips.Count, "Both ships should join the fleet.");
        }

        [Test]
        public void EnqueueTwoCapitalShips_SameExplicitFleet_JoinSameFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, ship1, targetFleet, ignoreCost: true);
            mfg.Enqueue(planet, ship2, targetFleet, ignoreCost: true);

            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(1, fleets.Count, "Both ships explicitly target the same fleet.");
            Assert.AreEqual(2, fleets[0].CapitalShips.Count);
        }

        [Test]
        public void ProcessTick_StarfighterComplete_ShipsToDestination()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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
            };
            _game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, fighter.ManufacturingStatus);
            Assert.IsNotNull(fighter.Movement, "Should have _movement state for shipping.");
            Assert.Greater(fighter.Movement.TransitTicks, 0, "Should have travel time.");
        }

        [Test]
        public void ProcessTick_StarfighterComplete_RemainsInsideDestinationFleet()
        {
            // When a starfighter is enqueued into an existing fleet on a different planet,
            // completing manufacturing must ship it to the fleet's capital ship.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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
            };
            _game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(
                destShip,
                fighter.GetParent(),
                "Completed starfighter must be parented to the destination capital ship."
            );
        }

        [Test]
        public void ProcessTick_RegimentComplete_ShipsToDestination()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(originPlanet, regiment, destPlanet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, regiment.ManufacturingStatus);
            Assert.IsNotNull(regiment.Movement, "Should have _movement state for shipping.");
            Assert.Greater(regiment.Movement.TransitTicks, 0, "Should have travel time.");
        }

        [Test]
        public void ProcessTick_RegimentCompleteOnSamePlanet_AttachedImmediately()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
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
                planet.Regiments.Contains(regiment),
                "Regiment should be in planet's regiment list."
            );
        }

        [Test]
        public void ProcessTick_DestinationDestroyed_UnitIsAlsoDestroyed()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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
            };
            _game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, fighter, destFleet, ignoreCost: true);

            // Destroy the destination fleet mid-production — fighter is a child so it is deregistered too
            _game.DetachNode(destFleet);

            Starfighter found = _game.GetSceneNodeByInstanceID<Starfighter>("sf1");
            Assert.IsNull(
                found,
                "Fighter should be deregistered when its destination fleet is destroyed."
            );
        }

        [Test]
        public void EnqueueBuilding_ValidBuilding_ParentIsDestinationPlanet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, mine, destPlanet, ignoreCost: true);

            Assert.AreEqual(
                destPlanet,
                mine.GetParent(),
                "Building parent should be the destination planet."
            );
            Assert.AreEqual(ManufacturingStatus.Building, mine.ManufacturingStatus);
        }

        [Test]
        public void EnqueueStarfighter_ValidFighter_ParentIsDestinationFleet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip destShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
            };
            _game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, fighter, destFleet, ignoreCost: true);

            Assert.AreEqual(
                destShip,
                fighter.GetParent(),
                "Starfighter should be immediately attached to the destination capital ship."
            );
            Assert.AreEqual(ManufacturingStatus.Building, fighter.ManufacturingStatus);
        }

        [Test]
        public void EnqueueRegiment_ValidRegiment_ParentIsDestinationPlanet()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, regiment, destPlanet, ignoreCost: true);

            Assert.AreEqual(
                destPlanet,
                regiment.GetParent(),
                "Regiment parent should be the destination planet."
            );
            Assert.AreEqual(ManufacturingStatus.Building, regiment.ManufacturingStatus);
        }

        [Test]
        public void ProcessTick_BuildingCompleteOnDifferentPlanet_ShipsToDestination()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(originPlanet, mine, destPlanet, ignoreCost: true);
            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, mine.ManufacturingStatus);
            Assert.IsNotNull(mine.Movement, "Should have _movement state for shipping.");
            Assert.Greater(mine.Movement.TransitTicks, 0, "Should have travel time.");
        }

        [Test]
        public void ProcessTick_BuildingForOwnedUncolonizedPlanet_ColonizesOnArrival()
        {
            PlanetSystem system = _coruscant.GetParentOfType<PlanetSystem>();
            Planet destination = new Planet
            {
                InstanceID = "OUTER_RIM",
                OwnerInstanceID = "EMPIRE",
                IsColonized = false,
                EnergyCapacity = 5,
            };
            _game.AttachNode(destination, system);
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

        [Test]
        public void ProcessTick_BuildingCompleteDestinationChangedSides_RedirectsToProductionPlanet()
        {
            // Mine queued from planetA to planetB. planetB captured before completion.
            // planetA has capacity — mine should redirect there and be in transit.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(sys, _game.Galaxy);
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
            _game.AttachNode(planetA, sys);

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
            _game.AttachNode(planetB, sys);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, localMovement);
            mfg.Enqueue(planetA, mine, planetB, ignoreCost: true);

            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, mine.ManufacturingStatus);
            Assert.AreEqual(
                planetA,
                mine.GetParent(),
                "Mine should redirect to production planet."
            );
            Assert.IsNotNull(
                mine.Movement,
                "Mine should be in visual transit to production planet."
            );
        }

        [Test]
        public void ProcessTick_BuildingCompleteDestinationChangedNoCapacity_StaysAtHostile()
        {
            // Mine queued from full planetA to planetB. planetB captured before completion.
            // planetA has no remaining capacity — mine stays at hostile planetB.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(sys, _game.Galaxy);
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
            _game.AttachNode(planetA, sys);

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
            _game.AttachNode(planetB, sys);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, localMovement);
            mfg.Enqueue(planetA, mine, planetB, ignoreCost: true);

            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, mine.ManufacturingStatus);
            Assert.IsNotNull(
                _game.GetSceneNodeByInstanceID<Building>("mine1"),
                "Mine should remain in the scene — not destroyed."
            );
            Assert.IsNull(mine.Movement, "No transit state when no valid planet found.");
        }

        [Test]
        public void ProcessTick_Blockade_ReducesProductionRate()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            Faction rebels = new Faction { InstanceID = "rebels" };
            _game.Factions.Add(empire);
            _game.Factions.Add(rebels);
            Planet planet = BuildShipyardPlanet(_game, "p1", "empire");

            // Enqueue a building that costs 100
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            // Add a construction facility
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            // Tick once without blockade to get baseline progress
            mfg.ProcessTick();
            int progressWithout = mine.ManufacturingProgress;

            // Reset progress
            mine.ManufacturingProgress = 0;

            // Now add hostile fleet to create blockade (10 capital ships = 50% penalty)
            Fleet hostileFleet = EntityFactory.CreateFleet("hf1", "rebels");
            _game.AttachNode(hostileFleet, planet);
            for (int i = 0; i < 10; i++)
            {
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = $"hs{i}",
                    OwnerInstanceID = "rebels",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _game.AttachNode(ship, hostileFleet);
            }

            // Tick with blockade
            mfg.ProcessTick();
            int progressWith = mine.ManufacturingProgress;

            Assert.Greater(
                progressWithout,
                progressWith,
                "Blockade should reduce production progress"
            );
        }

        [Test]
        public void ProcessTick_HeavyBlockade_ZeroesProduction()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            Faction rebels = new Faction { InstanceID = "rebels" };
            _game.Factions.Add(empire);
            _game.Factions.Add(rebels);
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

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ProductionType = ManufacturingType.Building,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            // 20 hostile capital ships apply a 100% blockade penalty, halting all production.
            Fleet hostileFleet = EntityFactory.CreateFleet("hf1", "rebels");
            _game.AttachNode(hostileFleet, planet);
            for (int i = 0; i < 20; i++)
            {
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = $"hs{i}",
                    OwnerInstanceID = "rebels",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                };
                _game.AttachNode(ship, hostileFleet);
            }

            mfg.ProcessTick();

            Assert.AreEqual(
                0,
                mine.ManufacturingProgress,
                "Heavy blockade should completely stop production"
            );
        }

        [Test]
        public void ProcessTick_ThreeManufacturingTypes_AllAdvance()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            empire.Settings.RefinementMultiplier = 4;
            _game.Factions.Add(empire);
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
            };
            _game.AttachNode(anchor, fleet);

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, localMovement);
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

        [Test]
        public void ProcessTick_NoShipyard_ShipMakesNoProgress()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(sys, _game.Galaxy);

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
            _game.AttachNode(planet, sys);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 10,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
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

        [Test]
        public void ProcessTick_NoTrainingFacility_RegimentMakesNoProgress()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(sys, _game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                EnergyCapacity = 10,
            };
            _game.AttachNode(planet, sys);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, regiment, planet, ignoreCost: true);

            for (int i = 0; i < 20; i++)
                mfg.ProcessTick();

            Assert.AreEqual(
                0,
                regiment.ManufacturingProgress,
                "Regiment should make no progress without a training facility"
            );
        }

        [Test]
        public void ProcessTick_NoConstructionYard_BuildingMakesNoProgress()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            _game.AttachNode(sys, _game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                EnergyCapacity = 10,
            };
            _game.AttachNode(planet, sys);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                ProductionType = ManufacturingType.Building,
                ConstructionCost = 10,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, _movement);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            for (int i = 0; i < 20; i++)
                mfg.ProcessTick();

            Assert.AreEqual(
                0,
                mine.ManufacturingProgress,
                "Building should make no progress without a construction yard"
            );
        }

        [Test]
        public void RebuildQueues_NoProducerPlanetID_SkipsItem()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(planet, system);

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

            ManufacturingSystem _manager = new ManufacturingSystem(_game);
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(0, queue.Count, "Should skip null ProducerPlanetID");
        }

        [Test]
        public void RebuildQueues_InvalidProducerPlanetID_SkipsItem()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            _game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                EnergyCapacity = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(system, _game.Galaxy);
            _game.AttachNode(planet, system);

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

            ManufacturingSystem _manager = new ManufacturingSystem(_game);
            _manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(0, queue.Count, "Should skip invalid ProducerPlanetID");
        }

        [Test]
        public void ProcessTick_CapitalShipCompleteDestinationFleetDestroyed_ShipIsLost()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
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

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, localMovement);
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

        [Test]
        public void ProcessTick_StarfighterCompleteFleetOverHostilePlanet_TravelsToCarrier()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
            Planet productionPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
            };
            _game.AttachNode(carrier, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, localMovement);
            mfg.Enqueue(productionPlanet, fighter, destFleet, ignoreCost: true);

            destPlanet.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, fighter.ManufacturingStatus);
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

        [Test]
        public void ProcessTick_RegimentCompleteFleetOverHostilePlanet_TravelsToTransport()
        {
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);
            Planet productionPlanet = BuildShipyardPlanet(_game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(_game, "p2", "empire");
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            _game.AttachNode(destFleet, destPlanet);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                RegimentCapacity = 2,
            };
            _game.AttachNode(carrier, destFleet);

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, localMovement);
            mfg.Enqueue(productionPlanet, regiment, destFleet, ignoreCost: true);

            destPlanet.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, regiment.ManufacturingStatus);
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

        [Test]
        public void ProcessTick_BuildingBatchDestinationChangedSidesWithCapacity_RedirectsAll()
        {
            // 3 mines queued from production planet A to destination planet B.
            // B changes sides. A has enough ground slots for all 3.
            // Expected: all 3 mines redirect to A and are marked complete.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(sys, _game.Galaxy);
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
            _game.AttachNode(planetA, sys);

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
            _game.AttachNode(planetB, sys);

            Building mine1 = new Building
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            Building mine2 = new Building
            {
                InstanceID = "m2",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            Building mine3 = new Building
            {
                InstanceID = "m3",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, localMovement);

            mfg.Enqueue(planetA, mine1, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine2, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine3, planetB, ignoreCost: true);

            // Destination captured before mines complete.
            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(ManufacturingStatus.Complete, mine2.ManufacturingStatus);
            Assert.AreEqual(ManufacturingStatus.Complete, mine3.ManufacturingStatus);
            Assert.AreEqual(
                planetA,
                mine1.GetParent(),
                "Mine 1 should redirect to production planet."
            );
            Assert.AreEqual(
                planetA,
                mine2.GetParent(),
                "Mine 2 should redirect to production planet."
            );
            Assert.AreEqual(
                planetA,
                mine3.GetParent(),
                "Mine 3 should redirect to production planet."
            );
        }

        [Test]
        public void ProcessTick_BuildingBatchDestinationChangedSidesNoCapacity_StaysAtCurrentLocation()
        {
            // 3 mines queued from production planet A to destination planet B.
            // B changes sides. A has no capacity for redirected mines.
            // Expected: MovementSystem cannot find a valid placement; mines remain in the scene
            // at their current location (planetB) rather than being destroyed.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(sys, _game.Galaxy);
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
            _game.AttachNode(planetA, sys);

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
            _game.AttachNode(planetB, sys);

            Building mine1 = new Building
            {
                InstanceID = "m1",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            Building mine2 = new Building
            {
                InstanceID = "m2",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };
            Building mine3 = new Building
            {
                InstanceID = "m3",
                OwnerInstanceID = "empire",
                AllowedOwnerInstanceIDs = new List<string> { "empire" },
                BuildingType = BuildingType.Mine,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            ManufacturingSystem mfg = new ManufacturingSystem(_game, _provider, localMovement);

            mfg.Enqueue(planetA, mine1, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine2, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine3, planetB, ignoreCost: true);

            // Destination captured before mines complete.
            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick();

            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(ManufacturingStatus.Complete, mine2.ManufacturingStatus);
            Assert.AreEqual(ManufacturingStatus.Complete, mine3.ManufacturingStatus);
            Assert.IsNotNull(
                _game.GetSceneNodeByInstanceID<Building>("m1"),
                "Mine 1 should remain in the scene when no valid placement is found."
            );
            Assert.IsNotNull(
                _game.GetSceneNodeByInstanceID<Building>("m2"),
                "Mine 2 should remain in the scene when no valid placement is found."
            );
            Assert.IsNotNull(
                _game.GetSceneNodeByInstanceID<Building>("m3"),
                "Mine 3 should remain in the scene when no valid placement is found."
            );
        }

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
    }
}
