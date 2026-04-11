using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
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
                ProcessRate = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_shipyard, _coruscant);

            _movement = new MovementSystem(_game, new FogOfWarSystem(_game));
            _provider = new FixedRNG();
            _manager = new ManufacturingSystem(_game);
        }

        [Test]
        public void ProcessTick_EmptyGame_DoesNotCrash()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            GameRoot emptyGame = new GameRoot(config);
            ManufacturingSystem emptyManager = new ManufacturingSystem(emptyGame);

            MovementSystem emptyMovement = new MovementSystem(
                emptyGame,
                new FogOfWarSystem(emptyGame)
            );
            emptyManager.ProcessTick(emptyMovement, new FixedRNG());

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
        public void Enqueue_MultipleBuildings_MaintainsOrder()
        {
            Building b1 = new Building
            {
                InstanceID = "B1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
            };
            Building b2 = new Building
            {
                InstanceID = "B2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 200,
                BaseBuildSpeed = 20,
                BuildingType = BuildingType.Refinery,
            };
            Building b3 = new Building
            {
                InstanceID = "B3",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 150,
                BaseBuildSpeed = 15,
                BuildingType = BuildingType.Defense,
            };

            _manager.Enqueue(_coruscant, b1, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, b2, _coruscant, ignoreCost: true);
            _manager.Enqueue(_coruscant, b3, _coruscant, ignoreCost: true);

            List<IManufacturable> queue = _coruscant.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(3, queue.Count);
            Assert.AreEqual("B1", queue[0].InstanceID);
            Assert.AreEqual("B2", queue[1].InstanceID);
            Assert.AreEqual("B3", queue[2].InstanceID);
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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.AreEqual(0, queue[ManufacturingType.Building].Count);
            Assert.AreEqual(ManufacturingStatus.Complete, mine.ManufacturingStatus);
            ManufacturingCompletedResult completed = results
                .OfType<ManufacturingCompletedResult>()
                .FirstOrDefault();
            Assert.IsNotNull(completed);
            Assert.AreEqual(mine, completed.GameObject);
            Assert.AreEqual(_empire, completed.Faction);
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
        public void Enqueue_InsufficientFunds_ReturnsFalse()
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
            int available = _game.GetRefinedMaterials(_empire);
            Assert.Less(available, 9999);

            bool result = _manager.Enqueue(_coruscant, expensive, _coruscant, ignoreCost: false);

            Assert.IsFalse(result);
            // Queue should be empty since enqueue was rejected
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.IsFalse(
                queue.ContainsKey(ManufacturingType.Building)
                    && queue[ManufacturingType.Building].Count > 0
            );
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
            int available = _game.GetRefinedMaterials(_empire);
            Assert.Less(available, 9999);

            bool result = _manager.Enqueue(_coruscant, expensive, _coruscant, ignoreCost: true);

            Assert.IsTrue(result); // Should succeed despite insufficient funds when ignoreCost=true
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count);
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

            _manager.ProcessTick(_movement, _provider);

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
            _manager.ProcessTick(_movement, _provider);

            // Building should still be attached to planet after completion
            Assert.AreEqual(_coruscant, mine.GetParent());
            Assert.IsTrue(_coruscant.GetAllBuildings().Contains(mine));
        }

        [Test]
        public void ProcessTick_OverflowProgress_CarriesToNextItem()
        {
            // Add multiple _shipyards for higher production rate
            Building _shipyard2 = new Building
            {
                InstanceID = "SHIPYARD2",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 5, // Faster production
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_shipyard2, _coruscant);

            // Production rate = ceiling(1/10 + 1/5) = ceiling(0.3) = 1
            // That's still not enough. Let me use ProcessRate = 2 on second _shipyard
            _shipyard2.ProcessRate = 2;
            // Production rate = ceiling(1/10 + 1/2) = ceiling(0.6) = 1
            // Still not enough! Let me change the original _shipyard
            _shipyard.ProcessRate = 2;
            // Production rate = ceiling(1/2 + 1/2) = ceiling(1.0) = 1
            // Need even more. Let me add a third _shipyard
            Building _shipyard3 = new Building
            {
                InstanceID = "SHIPYARD3",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_shipyard3, _coruscant);

            // Production rate = ceiling(1/2 + 1/2 + 1/2) = ceiling(1.5) = 2 per tick

            Building mine1 = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 1, // Needs 1, gets 2, overflow = 1
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

            // One tick: production = 2
            // mine1 needs 1, gets 1, completes, overflow = 1
            // mine2 gets overflow of 1
            _manager.ProcessTick(_movement, _provider);

            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(1, mine2.ManufacturingProgress); // Got the overflow!
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

            _manager.ProcessTick(_movement, _provider);

            // mine1 completes exactly (1 == 1)
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(1, mine1.ManufacturingProgress);

            // mine2 should have NO progress yet
            Assert.AreEqual(0, mine2.ManufacturingProgress);
            Assert.AreEqual(ManufacturingStatus.Building, mine2.ManufacturingStatus);

            // Next tick advances mine2
            _manager.ProcessTick(_movement, _provider);
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

            // ProcessTick gives 1 progress (ceiling(1.0/10) = 1)
            // First item completes (1 >= 1), second should start
            _manager.ProcessTick(_movement, _provider);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                _coruscant.GetManufacturingQueue();
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(ManufacturingStatus.Building, mine2.ManufacturingStatus);
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count); // Only mine2 remains
        }

        [Test]
        public void ProcessTick_MultipleCompletions_SameTick()
        {
            // With _shipyard ProcessRate=10, production rate = ceiling(1.0/10) = 1
            // If we have 3 items with cost=1, all should complete in 3 ticks
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
            _manager.ProcessTick(_movement, _provider);
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(
                2,
                _coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count
            );

            // Tick 2: mine2 completes
            _manager.ProcessTick(_movement, _provider);
            Assert.AreEqual(ManufacturingStatus.Complete, mine2.ManufacturingStatus);
            Assert.AreEqual(
                1,
                _coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count
            );

            // Tick 3: mine3 completes
            _manager.ProcessTick(_movement, _provider);
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
            _manager.ProcessTick(_movement, _provider);
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);

            // Tick 2: mine2 should be active (not skipped)
            List<IManufacturable> queueBefore = _coruscant.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(mine2, queueBefore[0]); // mine2 is now first

            _manager.ProcessTick(_movement, _provider);
            Assert.AreEqual(ManufacturingStatus.Complete, mine2.ManufacturingStatus);

            // Tick 3: mine3 should complete
            _manager.ProcessTick(_movement, _provider);
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
            _manager.ProcessTick(_movement, _provider);
            int progressAfterTick1 = mine.ManufacturingProgress;
            Assert.Greater(progressAfterTick1, 0);

            // Remove production building
            _game.DetachNode(_shipyard);

            // Second tick should not advance progress (no production source)
            _manager.ProcessTick(_movement, _provider);
            Assert.AreEqual(progressAfterTick1, mine.ManufacturingProgress); // No change
        }

        [Test]
        public void ProcessTick_MultipleProductionSources_StackCorrectly()
        {
            // Add second construction facility
            Building _shipyard2 = new Building
            {
                InstanceID = "SHIPYARD2",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 20, // Faster than first _shipyard
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(_shipyard2, _coruscant);

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

            // With two production sources:
            // Source 1 (_shipyard): ProcessRate=10 → contributes 1/10 = 0.1
            // Source 2 (_shipyard2): ProcessRate=20 → contributes 1/20 = 0.05
            // Combined: ceiling(0.1 + 0.05) = ceiling(0.15) = 1
            _manager.ProcessTick(_movement, _provider);

            // Progress should be exactly 1 (production rate from two sources)
            Assert.AreEqual(1, mine.ManufacturingProgress);

            // Verify rate calculation directly
            int productionRate = _coruscant.GetProductionRate(ManufacturingType.Building);
            Assert.AreEqual(1, productionRate); // ceiling(1/10 + 1/20) = 1
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
            _manager.ProcessTick(_movement, _provider);

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
            _manager.ProcessTick(_movement, _provider);

            // Verify bidirectional relationship
            Assert.AreEqual(_coruscant, mine.GetParent()); // child → parent
            Assert.IsTrue(_coruscant.GetAllBuildings().Contains(mine)); // parent → child
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
            _manager.ProcessTick(_movement, _provider);

            // Planet captured mid-construction
            _coruscant.OwnerInstanceID = "REBELLION";

            _manager.ProcessTick(_movement, _provider);

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
            _manager.ProcessTick(_movement, _provider);

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

            _manager.ProcessTick(_movement, _provider);

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

            return planet;
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, newShip, existingFleet, ignoreCost: true);

            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(1, fleets.Count, "Ship should join the explicitly specified fleet.");
            Assert.AreEqual(2, fleets[0].CapitalShips.Count);
            Assert.AreEqual(ManufacturingStatus.Building, newShip.ManufacturingStatus);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick(_movement, _provider);
            mfg.ProcessTick(_movement, _provider);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick(_movement, _provider);

            Assert.AreEqual(ManufacturingStatus.Complete, ship.ManufacturingStatus);
            bool inQueue = planet
                .GetManufacturingQueue()
                .Values.Any(list => list.Any(i => i.InstanceID == "cs1"));
            Assert.IsFalse(inQueue, "Completed ship should be removed from queue.");
        }

        [Test]
        public void ProcessTick_CapitalShipComplete_SamePlanet_NoMovement()
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick(_movement, _provider);

            Assert.IsNotNull(ship.GetParentOfType<Fleet>(), "Ship should be in a fleet.");
            Assert.IsNull(ship.Movement, "No _movement needed for same-planet destination.");
        }

        [Test]
        public void ProcessTick_CapitalShipComplete_DifferentPlanet_ShipsFleet()
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(originPlanet, ship, fleet, ignoreCost: true);
            mfg.ProcessTick(_movement, _provider);

            Assert.AreEqual(ManufacturingStatus.Complete, ship.ManufacturingStatus);
            Assert.IsNotNull(ship.Movement, "Ship should have _movement state for transit.");
            Assert.Greater(ship.Movement.TransitTicks, 0, "Should have travel time.");
            Assert.IsNull(fleet.Movement, "Fleet should not move — the ship travels to it.");
        }

        [Test]
        public void ProcessTick_CapitalShipComplete_DestinationChangedSides_ShipStaysAtFleet()
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(originPlanet, ship, fleet, ignoreCost: true);

            destPlanet.OwnerInstanceID = "rebels";

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            mfg.ProcessTick(localMovement, _provider);

            Assert.AreEqual(ManufacturingStatus.Complete, ship.ManufacturingStatus);
            Assert.IsNull(
                ship.Movement,
                "No transit — no valid friendly planet accepts a capital ship directly."
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);
            mfg.ProcessTick(_movement, _provider);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);
            mfg.ProcessTick(_movement, _provider);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(originPlanet, regiment, destPlanet, ignoreCost: true);
            mfg.ProcessTick(_movement, _provider);

            Assert.AreEqual(ManufacturingStatus.Complete, regiment.ManufacturingStatus);
            Assert.IsNotNull(regiment.Movement, "Should have _movement state for shipping.");
            Assert.Greater(regiment.Movement.TransitTicks, 0, "Should have travel time.");
        }

        [Test]
        public void ProcessTick_RegimentComplete_SamePlanet_AttachedImmediately()
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, regiment, planet, ignoreCost: true);
            mfg.ProcessTick(_movement, _provider);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, regiment, destPlanet, ignoreCost: true);

            Assert.AreEqual(
                destPlanet,
                regiment.GetParent(),
                "Regiment parent should be the destination planet."
            );
            Assert.AreEqual(ManufacturingStatus.Building, regiment.ManufacturingStatus);
        }

        [Test]
        public void ProcessTick_BuildingComplete_DifferentPlanet_ShipsToDestination()
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(originPlanet, mine, destPlanet, ignoreCost: true);
            mfg.ProcessTick(_movement, _provider);

            Assert.AreEqual(ManufacturingStatus.Complete, mine.ManufacturingStatus);
            Assert.IsNotNull(mine.Movement, "Should have _movement state for shipping.");
            Assert.Greater(mine.Movement.TransitTicks, 0, "Should have travel time.");
        }

        [Test]
        public void ProcessTick_BuildingComplete_DestinationChangedSides_RedirectsToProductionPlanet()
        {
            // Mine queued from planetA to planetB. planetB captured before completion.
            // planetA has capacity — mine should redirect there and be in transit.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(sys, _game.Galaxy);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planetA, mine, planetB, ignoreCost: true);

            planetB.OwnerInstanceID = "rebels";

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            mfg.ProcessTick(localMovement, _provider);

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
        public void ProcessTick_BuildingComplete_DestinationChangedSides_NoCapacityAnywhere_StaysAtHostile()
        {
            // Mine queued from full planetA to planetB. planetB captured before completion.
            // planetA has no remaining capacity — mine stays at hostile planetB.
            GameConfig config = TestConfig.Create();
            GameRoot _game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            _game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem { InstanceID = "sys1" };
            _game.AttachNode(sys, _game.Galaxy);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planetA, mine, planetB, ignoreCost: true);

            planetB.OwnerInstanceID = "rebels";

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            mfg.ProcessTick(localMovement, _provider);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            // Tick once without blockade to get baseline progress
            mfg.ProcessTick(_movement, _provider);
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
                };
                _game.AttachNode(ship, hostileFleet);
            }

            // Tick with blockade
            mfg.ProcessTick(_movement, _provider);
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            // 20 hostile capital ships = 100% penalty → 0 production
            Fleet hostileFleet = EntityFactory.CreateFleet("hf1", "rebels");
            _game.AttachNode(hostileFleet, planet);
            for (int i = 0; i < 20; i++)
            {
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = $"hs{i}",
                    OwnerInstanceID = "rebels",
                };
                _game.AttachNode(ship, hostileFleet);
            }

            mfg.ProcessTick(_movement, _provider);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);
            mfg.Enqueue(planet, ship, fleet, ignoreCost: true);
            mfg.Enqueue(planet, regiment, fleet, ignoreCost: true);

            mfg.ProcessTick(localMovement, _provider);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, ship, planet, ignoreCost: true);

            // Tick many times — should never advance
            for (int i = 0; i < 20; i++)
                mfg.ProcessTick(_movement, _provider);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, regiment, planet, ignoreCost: true);

            for (int i = 0; i < 20; i++)
                mfg.ProcessTick(_movement, _provider);

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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            for (int i = 0; i < 20; i++)
                mfg.ProcessTick(_movement, _provider);

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
        public void ProcessTick_CapitalShipComplete_DestinationFleetDestroyed_ShipIsLost()
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

            CapitalShip cs = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(productionPlanet, cs, destFleet, ignoreCost: true);

            Assert.IsNotNull(cs.GetParentOfType<Fleet>(), "CS should be in fleet after enqueue.");

            // Destroy the fleet mid-production by detaching all ships and the fleet itself.
            _game.DetachNode(anchor);
            _game.DetachNode(cs);
            _game.DetachNode(destFleet);

            // One tick completes manufacturing and triggers the rescue.
            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            mfg.ProcessTick(localMovement, _provider);

            Assert.IsNull(
                _game.GetSceneNodeByInstanceID<CapitalShip>("cs1"),
                "Ship should be lost when its destination fleet was destroyed before completion."
            );
        }

        [Test]
        public void ProcessTick_StarfighterComplete_DestinationChangedSides_RedirectsToProductionPlanet()
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(productionPlanet, fighter, destFleet, ignoreCost: true);

            // Destination captured mid-production.
            destPlanet.OwnerInstanceID = "rebels";

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            mfg.ProcessTick(localMovement, _provider);

            Assert.AreEqual(ManufacturingStatus.Complete, fighter.ManufacturingStatus);
            Assert.AreEqual(
                productionPlanet,
                fighter.GetParent(),
                "Fighter should be redirected to production planet when destination changed sides."
            );
            Assert.IsNotNull(
                fighter.Movement,
                "Fighter should be in visual transit toward production planet."
            );
        }

        [Test]
        public void ProcessTick_RegimentComplete_DestinationChangedSides_RedirectsToProductionPlanet()
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);
            mfg.Enqueue(productionPlanet, regiment, destFleet, ignoreCost: true);

            // Destination captured mid-production.
            destPlanet.OwnerInstanceID = "rebels";

            MovementSystem localMovement = new MovementSystem(_game, new FogOfWarSystem(_game));
            mfg.ProcessTick(localMovement, _provider);

            Assert.AreEqual(ManufacturingStatus.Complete, regiment.ManufacturingStatus);
            Assert.AreEqual(
                productionPlanet,
                regiment.GetParent(),
                "Regiment should be redirected to production planet when destination changed sides."
            );
            Assert.IsNotNull(
                regiment.Movement,
                "Regiment should be in visual transit toward production planet."
            );
        }

        [Test]
        public void ProcessTick_BuildingBatch_DestinationChangedSides_ProductionPlanetHasCapacity_RedirectsAll()
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

            // Production planet A: EnergyCapacity = 10 (3 used by construction yards → 7 available for 3 mines).
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

            // 3 construction yards at ProcessRate=1 → production rate = 3/tick, enough to complete 3 mines.
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);

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

            mfg.Enqueue(planetA, mine1, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine2, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine3, planetB, ignoreCost: true);

            // Destination captured before mines complete.
            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick(localMovement, _provider);

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
        public void ProcessTick_BuildingBatch_DestinationChangedSides_ProductionPlanetNoCapacity_StaysAtCurrentLocation()
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

            // Production planet A: EnergyCapacity = 5, filled with 3 yards + 2 dummies → 0 available.
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

            // 3 construction yards at ProcessRate=1 → production rate = 3/tick, enough to complete 3 mines.
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

            ManufacturingSystem mfg = new ManufacturingSystem(_game);

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

            mfg.Enqueue(planetA, mine1, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine2, planetB, ignoreCost: true);
            mfg.Enqueue(planetA, mine3, planetB, ignoreCost: true);

            // Destination captured before mines complete.
            planetB.OwnerInstanceID = "rebels";

            mfg.ProcessTick(localMovement, _provider);

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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

            ManufacturingDeployedResult deployed = results
                .OfType<ManufacturingDeployedResult>()
                .First();
            Assert.AreEqual(_empire, deployed.Faction);
            Assert.AreEqual(mine, deployed.DeployedObject);
        }

        [Test]
        public void ProcessTick_LastItemCompletes_EmitsCompletedResult()
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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

            Assert.IsTrue(results.OfType<ManufacturingCompletedResult>().Any());
        }

        [Test]
        public void ProcessTick_FirstOfTwoItemsCompletes_DoesNotEmitCompletedResult()
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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

            Assert.IsTrue(
                results.OfType<ManufacturingDeployedResult>().Any(),
                "deployed should fire for completed item"
            );
            Assert.IsFalse(
                results.OfType<ManufacturingCompletedResult>().Any(),
                "completed should not fire while queue still has items"
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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

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
            List<GameResult> results = _manager.ProcessTick(_movement, _provider);

            ManufacturingPointsRequiredResult pointsResult = results
                .OfType<ManufacturingPointsRequiredResult>()
                .First();
            Assert.AreEqual(50, pointsResult.RequiredPoints);
        }
    }
}
