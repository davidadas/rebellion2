using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class ManufacturingSystemTests
    {
        private GameRoot game;
        private ManufacturingSystem manager;
        private Faction empire;
        private Planet coruscant;
        private Building shipyard;

        [SetUp]
        public void SetUp()
        {
            // Create game with galaxy
            GameConfig config = ConfigLoader.LoadGameConfig();
            game = new GameRoot(config);
            GalaxyMap galaxy = game.Galaxy;

            // Create faction
            empire = new Faction { InstanceID = "EMPIRE" };
            game.Factions.Add(empire);

            // Create planet system
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "SYSTEM1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(system, galaxy);

            // Create planet with resources
            coruscant = new Planet
            {
                InstanceID = "CORUSCANT",
                OwnerInstanceID = "EMPIRE",
                PositionX = 0,
                PositionY = 0,
                NumRawResourceNodes = 100,
                IsColonized = true,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            game.AttachNode(coruscant, system);

            // Create construction yard for production
            shipyard = new Building
            {
                InstanceID = "SHIPYARD1",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Ground,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 10,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard, coruscant);

            manager = new ManufacturingSystem(game);
        }

        [Test]
        public void ProcessTick_EmptyGame_DoesNotCrash()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();
            GameRoot emptyGame = new GameRoot(config);
            ManufacturingSystem emptyManager = new ManufacturingSystem(emptyGame);

            emptyManager.ProcessTick(emptyGame);

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
                BuildingSlot = BuildingSlot.Ground,
            };

            bool result = manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);

            Assert.IsTrue(result);
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                coruscant.GetManufacturingQueue();
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
                BuildingSlot = BuildingSlot.Ground,
            };
            Building b2 = new Building
            {
                InstanceID = "B2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 200,
                BaseBuildSpeed = 20,
                BuildingType = BuildingType.Refinery,
                BuildingSlot = BuildingSlot.Ground,
            };
            Building b3 = new Building
            {
                InstanceID = "B3",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 150,
                BaseBuildSpeed = 15,
                BuildingType = BuildingType.Defense,
                BuildingSlot = BuildingSlot.Orbit,
            };

            manager.Enqueue(coruscant, b1, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, b2, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, b3, coruscant, ignoreCost: true);

            List<IManufacturable> queue = coruscant.GetManufacturingQueue()[
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);
            manager.ProcessTick(game);

            Assert.Greater(mine.ManufacturingProgress, 0);
            Assert.AreEqual(ManufacturingStatus.Building, mine.ManufacturingStatus);
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);
            manager.ProcessTick(game);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                coruscant.GetManufacturingQueue();
            Assert.AreEqual(0, queue[ManufacturingType.Building].Count);
            Assert.AreEqual(ManufacturingStatus.Complete, mine.ManufacturingStatus);
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);

            // Building should be in the game's node registry
            Building retrievedBuilding = game.GetSceneNodeByInstanceID<Building>("MINE1");
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
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
            };

            // Both should enqueue successfully
            bool result1 = manager.Enqueue(coruscant, mine1, coruscant, ignoreCost: true);
            bool result2 = manager.Enqueue(coruscant, mine2, coruscant, ignoreCost: true);

            Assert.IsTrue(result1);
            Assert.IsTrue(result2);

            List<IManufacturable> queue = coruscant.GetManufacturingQueue()[
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
                OrbitSlots = 5,
                GroundSlots = 5,
            };
            game.AttachNode(tatooine, game.GetSceneNodesByType<PlanetSystem>()[0]);

            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            // Attach to first planet (complete, so goes into Buildings list)
            game.AttachNode(mine, coruscant);

            // Attempt to enqueue on different planet should throw
            Assert.Throws<InvalidOperationException>(() =>
            {
                manager.Enqueue(tatooine, mine, tatooine, ignoreCost: true);
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);

            // Second enqueue should throw - same instance already has a parent
            Assert.Throws<InvalidOperationException>(() =>
            {
                manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);
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
                BuildingSlot = BuildingSlot.Ground,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine1, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, mine2, coruscant, ignoreCost: true);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                coruscant.GetManufacturingQueue();
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
                BuildingSlot = BuildingSlot.Ground,
                AllowedOwnerInstanceIDs = new List<string> { "REBELS" },
            };

            Assert.Throws<SceneAccessException>(
                () => game.AttachNode(rebelBuilding, coruscant),
                "Attaching a building to a planet owned by a different faction must throw SceneAccessException"
            );
        }

        [Test]
        public void Enqueue_DifferentFaction_ThrowsException()
        {
            // Enqueue internally calls game.AttachNode — ownership mismatch must propagate.
            Building rebelBuilding = new Building
            {
                InstanceID = "REBEL_BUILDING",
                OwnerInstanceID = "REBELS",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                AllowedOwnerInstanceIDs = new List<string> { "REBELS" },
            };

            Assert.Throws<SceneAccessException>(
                () => manager.Enqueue(coruscant, rebelBuilding, coruscant, ignoreCost: true),
                "Enqueueing a building owned by a different faction must throw SceneAccessException"
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
                BuildingSlot = BuildingSlot.Ground,
            };

            // Verify we have insufficient materials
            int available = game.GetRefinedMaterials(empire);
            Assert.Less(available, 9999);

            bool result = manager.Enqueue(coruscant, expensive, coruscant, ignoreCost: false);

            Assert.IsFalse(result);
            // Queue should be empty since enqueue was rejected
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                coruscant.GetManufacturingQueue();
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
                BuildingSlot = BuildingSlot.Ground,
            };

            // Verify we have insufficient materials
            int available = game.GetRefinedMaterials(empire);
            Assert.Less(available, 9999);

            bool result = manager.Enqueue(coruscant, expensive, coruscant, ignoreCost: true);

            Assert.IsTrue(result); // Should succeed despite insufficient funds when ignoreCost=true
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                coruscant.GetManufacturingQueue();
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);
            Assert.AreEqual(ManufacturingStatus.Building, mine.ManufacturingStatus);

            manager.ProcessTick(game);

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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);
            manager.ProcessTick(game);

            // Building should still be attached to planet after completion
            Assert.AreEqual(coruscant, mine.GetParent());
            Assert.IsTrue(coruscant.GetAllBuildings().Contains(mine));
        }

        [Test]
        public void ProcessTick_OverflowProgress_CarriesToNextItem()
        {
            // Add multiple shipyards for higher production rate
            Building shipyard2 = new Building
            {
                InstanceID = "SHIPYARD2",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Orbit,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 5, // Faster production
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard2, coruscant);

            // Production rate = ceiling(1/10 + 1/5) = ceiling(0.3) = 1
            // That's still not enough. Let me use ProcessRate = 2 on second shipyard
            shipyard2.ProcessRate = 2;
            // Production rate = ceiling(1/10 + 1/2) = ceiling(0.6) = 1
            // Still not enough! Let me change the original shipyard
            shipyard.ProcessRate = 2;
            // Production rate = ceiling(1/2 + 1/2) = ceiling(1.0) = 1
            // Need even more. Let me add a third shipyard
            Building shipyard3 = new Building
            {
                InstanceID = "SHIPYARD3",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Orbit,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 2,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard3, coruscant);

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
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine1, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, mine2, coruscant, ignoreCost: true);

            // One tick: production = 2
            // mine1 needs 1, gets 1, completes, overflow = 1
            // mine2 gets overflow of 1
            manager.ProcessTick(game);

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
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine1, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, mine2, coruscant, ignoreCost: true);

            manager.ProcessTick(game);

            // mine1 completes exactly (1 == 1)
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(1, mine1.ManufacturingProgress);

            // mine2 should have NO progress yet
            Assert.AreEqual(0, mine2.ManufacturingProgress);
            Assert.AreEqual(ManufacturingStatus.Building, mine2.ManufacturingStatus);

            // Next tick advances mine2
            manager.ProcessTick(game);
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
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine1, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, mine2, coruscant, ignoreCost: true);

            // ProcessTick gives 1 progress (ceiling(1.0/10) = 1)
            // First item completes (1 >= 1), second should start
            manager.ProcessTick(game);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                coruscant.GetManufacturingQueue();
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(ManufacturingStatus.Building, mine2.ManufacturingStatus);
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count); // Only mine2 remains
        }

        [Test]
        public void ProcessTick_MultipleCompletions_SameTick()
        {
            // With shipyard ProcessRate=10, production rate = ceiling(1.0/10) = 1
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
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine1, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, mine2, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, mine3, coruscant, ignoreCost: true);

            // Tick 1: mine1 completes
            manager.ProcessTick(game);
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);
            Assert.AreEqual(2, coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count);

            // Tick 2: mine2 completes
            manager.ProcessTick(game);
            Assert.AreEqual(ManufacturingStatus.Complete, mine2.ManufacturingStatus);
            Assert.AreEqual(1, coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count);

            // Tick 3: mine3 completes
            manager.ProcessTick(game);
            Assert.AreEqual(ManufacturingStatus.Complete, mine3.ManufacturingStatus);
            Assert.AreEqual(0, coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count);
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
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine1, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, mine2, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, mine3, coruscant, ignoreCost: true);

            // Tick 1: mine1 completes and is removed - mine2 should still process next tick
            manager.ProcessTick(game);
            Assert.AreEqual(ManufacturingStatus.Complete, mine1.ManufacturingStatus);

            // Tick 2: mine2 should be active (not skipped)
            List<IManufacturable> queueBefore = coruscant.GetManufacturingQueue()[
                ManufacturingType.Building
            ];
            Assert.AreEqual(mine2, queueBefore[0]); // mine2 is now first

            manager.ProcessTick(game);
            Assert.AreEqual(ManufacturingStatus.Complete, mine2.ManufacturingStatus);

            // Tick 3: mine3 should complete
            manager.ProcessTick(game);
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);

            // First tick advances progress
            manager.ProcessTick(game);
            int progressAfterTick1 = mine.ManufacturingProgress;
            Assert.Greater(progressAfterTick1, 0);

            // Remove production building
            game.DetachNode(shipyard);

            // Second tick should not advance progress (no production source)
            manager.ProcessTick(game);
            Assert.AreEqual(progressAfterTick1, mine.ManufacturingProgress); // No change
        }

        [Test]
        public void ProcessTick_MultipleProductionSources_StackCorrectly()
        {
            // Add second construction facility
            Building shipyard2 = new Building
            {
                InstanceID = "SHIPYARD2",
                OwnerInstanceID = "EMPIRE",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Orbit, // Different slot
                ProductionType = ManufacturingType.Building,
                ProcessRate = 20, // Faster than first shipyard
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard2, coruscant);

            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);

            // With two production sources:
            // Source 1 (shipyard): ProcessRate=10 → contributes 1/10 = 0.1
            // Source 2 (shipyard2): ProcessRate=20 → contributes 1/20 = 0.05
            // Combined: ceiling(0.1 + 0.05) = ceiling(0.15) = 1
            manager.ProcessTick(game);

            // Progress should be exactly 1 (production rate from two sources)
            Assert.AreEqual(1, mine.ManufacturingProgress);

            // Verify rate calculation directly
            int productionRate = coruscant.GetProductionRate(ManufacturingType.Building);
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);
            manager.ProcessTick(game);

            // Verify only one instance in scene graph
            List<Building> allBuildings = game.GetSceneNodesByType<Building>();
            List<Building> matchingBuildings = allBuildings
                .Where(b => b.InstanceID == "MINE1")
                .ToList();
            Assert.AreEqual(1, matchingBuildings.Count);

            // Verify only one instance in planet's building list
            List<Building> planetBuildings = coruscant
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);
            manager.ProcessTick(game);

            // Verify bidirectional relationship
            Assert.AreEqual(coruscant, mine.GetParent()); // child → parent
            Assert.IsTrue(coruscant.GetAllBuildings().Contains(mine)); // parent → child
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
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);
            manager.ProcessTick(game);

            // Planet captured mid-construction
            coruscant.OwnerInstanceID = "REBELLION";

            manager.ProcessTick(game);

            // Building should still belong to original producer (EMPIRE)
            Assert.AreEqual("EMPIRE", mine.OwnerInstanceID);
            Assert.AreEqual("EMPIRE", mine.ProducerOwnerID);
        }

        [Test]
        public void GetManufacturingQueue_NoItems_ReturnsEmptyDictionary()
        {
            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                coruscant.GetManufacturingQueue();

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
                BuildingSlot = BuildingSlot.Ground,
            };

            Building mine2 = new Building
            {
                InstanceID = "MINE2",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine1, coruscant, ignoreCost: true);
            manager.Enqueue(coruscant, mine2, coruscant, ignoreCost: true);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                coruscant.GetManufacturingQueue();

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
                BuildingSlot = BuildingSlot.Ground,
            };

            bool result = manager.Enqueue(coruscant, free, coruscant, ignoreCost: true);
            Assert.IsTrue(result);

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                coruscant.GetManufacturingQueue();
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count);

            // Process one tick - zero cost should complete immediately
            manager.ProcessTick(game);

            // Verify completion behavior
            Assert.AreEqual(ManufacturingStatus.Complete, free.ManufacturingStatus);
            Assert.AreEqual(0, coruscant.GetManufacturingQueue()[ManufacturingType.Building].Count);
        }

        [Test]
        public void ProcessTick_ZeroProductionRate_NoProgress()
        {
            // Remove the shipyard to have zero production
            game.DetachNode(shipyard);

            Building mine = new Building
            {
                InstanceID = "MINE1",
                OwnerInstanceID = "EMPIRE",
                ConstructionCost = 100,
                BaseBuildSpeed = 10,
                ManufacturingProgress = 0,
                ManufacturingStatus = ManufacturingStatus.Building,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
            };

            manager.Enqueue(coruscant, mine, coruscant, ignoreCost: true);
            int initialProgress = mine.ManufacturingProgress;

            manager.ProcessTick(game);

            // No production facilities = no progress
            Assert.AreEqual(initialProgress, mine.ManufacturingProgress);
        }

        [Test]
        public void RebuildQueues_EmptyGame_NoQueues()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            ManufacturingSystem manager = new ManufacturingSystem(game);
            manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(0, queue.Count, "Empty game should have no queues");
        }

        [Test]
        public void RebuildQueues_SinglePlanet_CorrectOrder()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Building item1 = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 10,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 100,
            };

            game.AttachNode(item1, planet);
            game.AttachNode(item2, planet);
            game.AttachNode(item3, planet);

            ManufacturingSystem manager = new ManufacturingSystem(game);
            manager.RebuildQueues();

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
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet1 = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            Planet planet2 = new Planet
            {
                InstanceID = "p2",
                OwnerInstanceID = "empire",
                IsColonized = true,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet1, system);
            game.AttachNode(planet2, system);

            Building item1 = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 100,
            };

            game.AttachNode(item1, planet1);
            game.AttachNode(item2, planet1);
            game.AttachNode(item3, planet2);

            ManufacturingSystem manager = new ManufacturingSystem(game);
            manager.RebuildQueues();

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
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Building item = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 100,
            };
            game.AttachNode(item, planet);

            ManufacturingSystem manager = new ManufacturingSystem(game);
            manager.RebuildQueues();
            manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count, "No duplication");
        }

        [Test]
        public void RebuildQueues_OnlyBuilding_IgnoresComplete()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Building itemBuilding = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p1",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 100,
            };

            game.AttachNode(itemBuilding, planet);
            game.AttachNode(itemComplete, planet);

            ManufacturingSystem manager = new ManufacturingSystem(game);
            manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(1, queue.Count);
            Assert.AreEqual(1, queue[ManufacturingType.Building].Count, "Only Building status");
            Assert.AreEqual("b1", queue[ManufacturingType.Building][0].InstanceID);
        }

        private Planet BuildShipyardPlanet(GameRoot game, string planetId, string factionId)
        {
            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = $"{planetId}_sys",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sys, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = planetId,
                OwnerInstanceID = factionId,
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                OrbitSlots = 10,
                GroundSlots = 10,
                NumRawResourceNodes = 100,
            };
            game.AttachNode(planet, sys);

            Building shipyard = new Building
            {
                InstanceID = $"{planetId}_shipyard",
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.Shipyard,
                BuildingSlot = BuildingSlot.Orbit,
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard, planet);

            Building trainingFacility = new Building
            {
                InstanceID = $"{planetId}_training",
                OwnerInstanceID = factionId,
                BuildingType = BuildingType.TrainingFacility,
                BuildingSlot = BuildingSlot.Ground,
                ProductionType = ManufacturingType.Troop,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(trainingFacility, planet);

            return planet;
        }

        [Test]
        public void EnqueueCapitalShip_ValidShip_AttachesToFleetAtPlanet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, ship, planet, ignoreCost: true);

            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(1, fleets.Count, "Fleet should be created at enqueue.");
            Assert.AreEqual(1, fleets[0].CapitalShips.Count, "Ship should be in fleet.");
            Assert.AreEqual("cs1", fleets[0].CapitalShips[0].InstanceID);
            Assert.AreEqual(ManufacturingStatus.Building, ship.ManufacturingStatus);

            CapitalShip found = game.GetSceneNodeByInstanceID<CapitalShip>("cs1");
            Assert.IsNotNull(found, "Ship should be in the scene graph during production.");
        }

        [Test]
        public void EnqueueCapitalShip_ValidShip_RegistersFleetToFaction()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, ship, planet, ignoreCost: true);

            Assert.AreEqual(
                1,
                empire.GetOwnedUnitsByType<Fleet>().Count,
                "Fleet should be registered to faction at enqueue."
            );
        }

        [Test]
        public void EnqueueCapitalShip_WithFleetDestination_JoinsFleet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Fleet existingFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(existingFleet, planet);
            CapitalShip existingShip = new CapitalShip
            {
                InstanceID = "cs0",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
            };
            game.AttachNode(existingShip, existingFleet);

            CapitalShip newShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, newShip, existingFleet, ignoreCost: true);

            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(1, fleets.Count, "Ship should join the explicitly specified fleet.");
            Assert.AreEqual(2, fleets[0].CapitalShips.Count);
            Assert.AreEqual(ManufacturingStatus.Building, newShip.ManufacturingStatus);
        }

        [Test]
        public void EnqueueCapitalShip_NoFleetDestination_CreatesNewFleet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Fleet existingFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(existingFleet, planet);

            CapitalShip newShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, newShip, planet, ignoreCost: true);

            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(
                2,
                fleets.Count,
                "No destination fleet given — a new fleet should be created."
            );
            Assert.IsTrue(fleets.Any(f => f.CapitalShips.Contains(newShip)));
            Assert.AreEqual(ManufacturingStatus.Building, newShip.ManufacturingStatus);
        }

        [Test]
        public void EnqueueCapitalShip_NoOwner_ThrowsException()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = null,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            Assert.Throws<SceneAccessException>(
                () => mfg.Enqueue(planet, ship, planet, ignoreCost: true),
                "Enqueueing a capital ship with no owner should throw."
            );
        }

        [Test]
        public void ProcessTick_CapitalShipBuilding_RemainsInFleetWithProgress()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, ship, planet, ignoreCost: true);
            mfg.ProcessTick(game);
            mfg.ProcessTick(game);

            Assert.AreEqual(ManufacturingStatus.Building, ship.ManufacturingStatus);
            Assert.Greater(ship.ManufacturingProgress, 0, "Progress should advance.");
            Assert.IsTrue(
                ship.GetParent() is Fleet,
                "Ship should be in a fleet during production."
            );

            Fleet fleet = (Fleet)ship.GetParent();
            Assert.AreEqual(planet, fleet.GetParent(), "Fleet should be at production planet.");
        }

        [Test]
        public void ProcessTick_CapitalShipComplete_RemovedFromQueue()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, ship, planet, ignoreCost: true);
            mfg.ProcessTick(game);

            Assert.AreEqual(ManufacturingStatus.Complete, ship.ManufacturingStatus);
            bool inQueue = planet
                .GetManufacturingQueue()
                .Values.Any(list => list.Any(i => i.InstanceID == "cs1"));
            Assert.IsFalse(inQueue, "Completed ship should be removed from queue.");
        }

        [Test]
        public void ProcessTick_CapitalShipComplete_SamePlanet_NoMovement()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, ship, planet, ignoreCost: true);
            mfg.ProcessTick(game);

            Fleet fleet = ship.GetParent() as Fleet;
            Assert.IsNotNull(fleet, "Ship should be in a fleet.");
            Assert.IsNull(fleet.Movement, "No movement needed for same-planet destination.");
        }

        [Test]
        public void ProcessTick_CapitalShipComplete_DifferentPlanet_ShipsFleet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet originPlanet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(originPlanet, ship, destPlanet, ignoreCost: true);
            mfg.ProcessTick(game);

            Assert.AreEqual(ManufacturingStatus.Complete, ship.ManufacturingStatus);

            Fleet fleet = ship.GetParent() as Fleet;
            Assert.IsNotNull(fleet, "Ship should be in a fleet.");
            Assert.IsNotNull(fleet.Movement, "Fleet should have movement state for shipping.");
            Assert.AreEqual("p2", fleet.Movement.DestinationInstanceID);
            Assert.Greater(fleet.Movement.TransitTicks, 0, "Should have travel time.");
        }

        [Test]
        public void EnqueueTwoCapitalShips_NoFleet_EachGetsOwnFleet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

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

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, ship1, planet, ignoreCost: true);
            mfg.Enqueue(planet, ship2, planet, ignoreCost: true);

            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(
                2,
                fleets.Count,
                "Each ship without an explicit fleet destination gets its own new fleet."
            );
            Assert.AreEqual(1, fleets[0].CapitalShips.Count);
            Assert.AreEqual(1, fleets[1].CapitalShips.Count);
        }

        [Test]
        public void EnqueueTwoCapitalShips_SameExplicitFleet_JoinSameFleet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Fleet targetFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(targetFleet, planet);

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

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, ship1, targetFleet, ignoreCost: true);
            mfg.Enqueue(planet, ship2, targetFleet, ignoreCost: true);

            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(1, fleets.Count, "Both ships explicitly target the same fleet.");
            Assert.AreEqual(2, fleets[0].CapitalShips.Count);
        }

        [Test]
        public void ProcessTick_StarfighterComplete_ShipsToDestination()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet originPlanet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destFleet, destPlanet);
            CapitalShip destShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
            };
            game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);
            mfg.ProcessTick(game);

            Assert.AreEqual(ManufacturingStatus.Complete, fighter.ManufacturingStatus);
            Assert.IsNotNull(fighter.Movement, "Should have movement state for shipping.");
            Assert.AreEqual("cs1", fighter.Movement.DestinationInstanceID, "Should be shipping to the destination capital ship.");
            Assert.Greater(fighter.Movement.TransitTicks, 0, "Should have travel time.");
        }

        [Test]
        public void ProcessTick_StarfighterComplete_RemainsInsideDestinationFleet()
        {
            // When a starfighter is enqueued into an existing fleet on a different planet,
            // completing manufacturing must ship it to the fleet's capital ship.
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet originPlanet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destFleet, destPlanet);
            CapitalShip destShip = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                StarfighterCapacity = 2,
            };
            game.AttachNode(destShip, destFleet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);
            mfg.ProcessTick(game);

            Assert.AreEqual(
                destShip,
                fighter.GetParent(),
                "Completed starfighter must be parented to the destination fleet's capital ship."
            );
        }

        [Test]
        public void ProcessTick_RegimentComplete_ShipsToDestination()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet originPlanet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(originPlanet, regiment, destPlanet, ignoreCost: true);
            mfg.ProcessTick(game);

            Assert.AreEqual(ManufacturingStatus.Complete, regiment.ManufacturingStatus);
            Assert.IsNotNull(regiment.Movement, "Should have movement state for shipping.");
            Assert.AreEqual("p2", regiment.Movement.DestinationInstanceID);
            Assert.Greater(regiment.Movement.TransitTicks, 0, "Should have travel time.");
        }

        [Test]
        public void ProcessTick_RegimentComplete_SamePlanet_AttachedImmediately()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, regiment, planet, ignoreCost: true);
            mfg.ProcessTick(game);

            Assert.AreEqual(ManufacturingStatus.Complete, regiment.ManufacturingStatus);
            Assert.AreEqual(
                planet,
                regiment.GetParent(),
                "Regiment should be on the production planet."
            );
            Assert.IsNull(regiment.Movement, "No movement needed for same-planet destination.");
            Assert.IsTrue(
                planet.Regiments.Contains(regiment),
                "Regiment should be in planet's regiment list."
            );
        }

        [Test]
        public void ProcessTick_DestinationDestroyed_UnitRemainsAtProductionPlanet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destFleet, destPlanet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, fighter, destFleet, ignoreCost: true);

            // Destroy the destination fleet mid-production — fighter stays at production planet
            game.DetachNode(destFleet);

            Starfighter found = game.GetSceneNodeByInstanceID<Starfighter>("sf1");
            Assert.IsNotNull(found, "Fighter should remain registered at production planet.");
            Assert.AreEqual(planet, found.GetParent(), "Fighter should still be at the production planet.");
        }

        [Test]
        public void EnqueueBuilding_ValidBuilding_ParentIsDestinationPlanet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Ground,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(constructionYard, planet);

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
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
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destFleet, destPlanet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, fighter, destFleet, ignoreCost: true);

            Assert.AreEqual(
                planet,
                fighter.GetParent(),
                "Starfighter should remain at production planet during manufacturing."
            );
            Assert.AreEqual("f1", fighter.DestinationInstanceID, "Destination fleet ID should be stored.");
            Assert.AreEqual(ManufacturingStatus.Building, fighter.ManufacturingStatus);
        }

        [Test]
        public void EnqueueRegiment_ValidRegiment_ParentIsDestinationPlanet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
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
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet originPlanet = BuildShipyardPlanet(game, "p1", "empire");

            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Ground,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(constructionYard, originPlanet);

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(originPlanet, mine, destPlanet, ignoreCost: true);
            mfg.ProcessTick(game);

            Assert.AreEqual(ManufacturingStatus.Complete, mine.ManufacturingStatus);
            Assert.IsNotNull(mine.Movement, "Should have movement state for shipping.");
            Assert.AreEqual("p2", mine.Movement.DestinationInstanceID);
            Assert.Greater(mine.Movement.TransitTicks, 0, "Should have travel time.");
        }

        [Test]
        public void ProcessTick_Blockade_ReducesProductionRate()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            Faction rebels = new Faction { InstanceID = "rebels" };
            game.Factions.Add(empire);
            game.Factions.Add(rebels);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            // Enqueue a building that costs 100
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            // Add a construction facility
            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Ground,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(constructionYard, planet);

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            // Tick once without blockade to get baseline progress
            mfg.ProcessTick(game);
            int progressWithout = mine.ManufacturingProgress;

            // Reset progress
            mine.ManufacturingProgress = 0;

            // Now add hostile fleet to create blockade (10 capital ships = 50% penalty)
            Fleet hostileFleet = EntityFactory.CreateFleet("hf1", "rebels");
            game.AttachNode(hostileFleet, planet);
            for (int i = 0; i < 10; i++)
            {
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = $"hs{i}",
                    OwnerInstanceID = "rebels",
                };
                game.AttachNode(ship, hostileFleet);
            }

            // Tick with blockade
            mfg.ProcessTick(game);
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
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            Faction rebels = new Faction { InstanceID = "rebels" };
            game.Factions.Add(empire);
            game.Factions.Add(rebels);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            Building constructionYard = new Building
            {
                InstanceID = "cy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Ground,
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
                BuildingSlot = BuildingSlot.Ground,
                ProductionType = ManufacturingType.Building,
                ConstructionCost = 100,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            // 20 hostile capital ships = 100% penalty → 0 production
            Fleet hostileFleet = EntityFactory.CreateFleet("hf1", "rebels");
            game.AttachNode(hostileFleet, planet);
            for (int i = 0; i < 20; i++)
            {
                CapitalShip ship = new CapitalShip
                {
                    InstanceID = $"hs{i}",
                    OwnerInstanceID = "rebels",
                };
                game.AttachNode(ship, hostileFleet);
            }

            mfg.ProcessTick(game);

            Assert.AreEqual(
                0,
                mine.ManufacturingProgress,
                "Heavy blockade should completely stop production"
            );
        }

        [Test]
        public void ProcessTick_ThreeManufacturingTypes_AllAdvance()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet planet = BuildShipyardPlanet(game, "p1", "empire");

            // BuildShipyardPlanet already adds a shipyard (Ship) and training facility (Troop).
            // Add a construction yard for Building production.
            Building constructionYard = new Building
            {
                InstanceID = "p1_construction",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.ConstructionFacility,
                BuildingSlot = BuildingSlot.Ground,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(constructionYard, planet);

            // Enqueue one item per manufacturing type
            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
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

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);
            mfg.Enqueue(planet, ship, planet, ignoreCost: true);
            mfg.Enqueue(planet, regiment, planet, ignoreCost: true);

            mfg.ProcessTick(game);

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
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sys, game.Galaxy);

            // Planet with NO facilities at all
            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                OrbitSlots = 10,
                GroundSlots = 10,
                NumRawResourceNodes = 100,
            };
            game.AttachNode(planet, sys);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                OwnerInstanceID = "empire",
                ConstructionCost = 10,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, ship, planet, ignoreCost: true);

            // Tick many times — should never advance
            for (int i = 0; i < 20; i++)
                mfg.ProcessTick(game);

            Assert.AreEqual(
                0,
                ship.ManufacturingProgress,
                "Ship should make no progress without a shipyard"
            );
        }

        [Test]
        public void ProcessTick_NoTrainingFacility_RegimentMakesNoProgress()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sys, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            game.AttachNode(planet, sys);

            // Add a shipyard but NOT a training facility
            Building shipyard = new Building
            {
                InstanceID = "sy1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Shipyard,
                BuildingSlot = BuildingSlot.Orbit,
                ProductionType = ManufacturingType.Ship,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(shipyard, planet);

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 10,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, regiment, planet, ignoreCost: true);

            for (int i = 0; i < 20; i++)
                mfg.ProcessTick(game);

            Assert.AreEqual(
                0,
                regiment.ManufacturingProgress,
                "Regiment should make no progress without a training facility"
            );
        }

        [Test]
        public void ProcessTick_NoConstructionYard_BuildingMakesNoProgress()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);

            PlanetSystem sys = new PlanetSystem
            {
                InstanceID = "sys1",
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(sys, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            game.AttachNode(planet, sys);

            Building mine = new Building
            {
                InstanceID = "mine1",
                OwnerInstanceID = "empire",
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ProductionType = ManufacturingType.Building,
                ConstructionCost = 10,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(planet, mine, planet, ignoreCost: true);

            for (int i = 0; i < 20; i++)
                mfg.ProcessTick(game);

            Assert.AreEqual(
                0,
                mine.ManufacturingProgress,
                "Building should make no progress without a construction yard"
            );
        }

        [Test]
        public void RebuildQueues_NoProducerPlanetID_SkipsItem()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Building itemNoProducer = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = null,
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 100,
            };

            game.AttachNode(itemNoProducer, planet);

            ManufacturingSystem manager = new ManufacturingSystem(game);
            manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(0, queue.Count, "Should skip null ProducerPlanetID");
        }

        [Test]
        public void RebuildQueues_InvalidProducerPlanetID_SkipsItem()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            game.Factions.Add(new Faction { InstanceID = "empire" });

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                OrbitSlots = 10,
                GroundSlots = 10,
            };
            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.Galaxy);
            game.AttachNode(planet, system);

            Building itemOrphan = new Building
            {
                InstanceID = "b1",
                OwnerInstanceID = "empire",
                ProducerPlanetID = "p999",
                ManufacturingStatus = ManufacturingStatus.Building,
                ManufacturingProgress = 0,
                ProductionType = ManufacturingType.Building,
                BuildingType = BuildingType.Mine,
                BuildingSlot = BuildingSlot.Ground,
                ConstructionCost = 100,
            };

            game.AttachNode(itemOrphan, planet);

            ManufacturingSystem manager = new ManufacturingSystem(game);
            manager.RebuildQueues();

            Dictionary<ManufacturingType, List<IManufacturable>> queue =
                planet.GetManufacturingQueue();
            Assert.AreEqual(0, queue.Count, "Should skip invalid ProducerPlanetID");
        }

        [Test]
        public void ProcessTick_StarfighterComplete_DestinationChangedOwner_RedirectsToProductionPlanet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet originPlanet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destFleet, destPlanet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);

            // Destination captured mid-production
            destPlanet.OwnerInstanceID = "rebels";

            mfg.ProcessTick(game);

            Assert.AreEqual(ManufacturingStatus.Complete, fighter.ManufacturingStatus);
            Assert.AreEqual(
                originPlanet,
                fighter.GetParent(),
                "Unit should fall back to production planet."
            );
            Assert.IsNull(fighter.Movement, "No movement when redirected to production planet.");
        }

        [Test]
        public void ProcessTick_RegimentComplete_DestinationChangedOwner_RedirectsToProductionPlanet()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet originPlanet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destFleet, destPlanet);

            Regiment regiment = new Regiment
            {
                InstanceID = "rg1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(originPlanet, regiment, destFleet, ignoreCost: true);

            // Destination captured mid-production
            destPlanet.OwnerInstanceID = "rebels";

            mfg.ProcessTick(game);

            Assert.AreEqual(ManufacturingStatus.Complete, regiment.ManufacturingStatus);
            Assert.AreEqual(
                originPlanet,
                regiment.GetParent(),
                "Unit should fall back to production planet."
            );
            Assert.IsNull(regiment.Movement, "No movement when redirected to production planet.");
        }

        [Test]
        public void ProcessTick_UnitComplete_BothPlanetsChangedOwner_CancelsUnit()
        {
            GameConfig config = new GameConfig();
            GameRoot game = new GameRoot(config);
            Faction empire = new Faction { InstanceID = "empire" };
            game.Factions.Add(empire);
            Planet originPlanet = BuildShipyardPlanet(game, "p1", "empire");

            Planet destPlanet = BuildShipyardPlanet(game, "p2", "empire");
            destPlanet.PositionX = 500;
            destPlanet.PositionY = 500;
            Fleet destFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destFleet, destPlanet);

            Starfighter fighter = new Starfighter
            {
                InstanceID = "sf1",
                OwnerInstanceID = "empire",
                ConstructionCost = 1,
                BaseBuildSpeed = 1,
            };

            ManufacturingSystem mfg = new ManufacturingSystem(game);
            mfg.Enqueue(originPlanet, fighter, destFleet, ignoreCost: true);

            // Both planets captured mid-production
            destPlanet.OwnerInstanceID = "rebels";
            originPlanet.OwnerInstanceID = "rebels";

            mfg.ProcessTick(game);

            Starfighter found = game.GetSceneNodeByInstanceID<Starfighter>("sf1");
            Assert.IsNull(found, "Unit should be removed when no friendly planet can accept it.");
        }
    }
}
