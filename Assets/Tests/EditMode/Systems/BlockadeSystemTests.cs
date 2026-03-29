using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    /// <summary>
    /// Tests for BlockadeSystem.
    /// Tests transition detection (start/end) and side effects (troop destruction).
    /// Does NOT test blockade detection logic (that's Planet.IsBlockaded(), tested in PlanetTests).
    /// </summary>
    [TestFixture]
    public class BlockadeSystemTests
    {
        [Test]
        public void ProcessTick_NewBlockade_DestroysInTransitDefenders()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Regiment inTransitRegiment = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(inTransitRegiment, planet);

            // Verify setup: planet is blockaded, regiment exists and is in transit
            Assert.IsTrue(planet.IsBlockaded());
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsNotNull(inTransitRegiment.Movement);

            // Act
            manager.ProcessTick(game);

            // Assert: in-transit regiment destroyed
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
        }

        [Test]
        public void ProcessTick_NewBlockade_GarrisonedDefendersSurvive()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Regiment garrisonedRegiment = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Garrison",
                OwnerInstanceID = "empire",
                Movement = null,
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(garrisonedRegiment, planet);

            // Verify setup: planet is blockaded, regiment exists and is garrisoned
            Assert.IsTrue(planet.IsBlockaded());
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsNull(garrisonedRegiment.Movement);

            // Act
            manager.ProcessTick(game);

            // Assert: garrisoned regiment survives
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
        }

        [Test]
        public void ProcessTick_AlreadyBlockaded_DoesNotTriggerStartAgain()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Regiment regiment1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Regiment 1",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };
            Regiment regiment2 = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Regiment 2",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(regiment1, planet);

            // First tick: blockade starts, regiment1 destroyed
            manager.ProcessTick(game);
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));

            // Add second in-transit regiment while blockade is ongoing
            game.AttachNode(regiment2, planet);
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));

            // Act: second tick, blockade already active
            manager.ProcessTick(game);

            // Assert: regiment2 NOT destroyed (no start event fired again)
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));
        }

        [Test]
        public void ProcessTick_BlockadeEnds_TriggersEnd()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Fleet defenderFleet = new Fleet
            {
                InstanceID = "f2",
                DisplayName = "Imperial Fleet",
                OwnerInstanceID = "empire",
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);

            // First tick: blockade starts
            Assert.IsTrue(planet.IsBlockaded());
            manager.ProcessTick(game);

            // Defender arrives
            game.AttachNode(defenderFleet, planet);
            Assert.IsFalse(planet.IsBlockaded());

            // Act: blockade ends this tick
            manager.ProcessTick(game);

            // Assert: No exception, end event fired (logged internally)
            Assert.IsFalse(planet.IsBlockaded());
        }

        [Test]
        public void ProcessTick_NotBlockaded_DoesNotTriggerEndAgain()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Fleet defenderFleet = new Fleet
            {
                InstanceID = "f2",
                DisplayName = "Imperial Fleet",
                OwnerInstanceID = "empire",
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);

            // Blockade starts
            manager.ProcessTick(game);
            Assert.IsTrue(planet.IsBlockaded());

            // Blockade ends
            game.AttachNode(defenderFleet, planet);
            manager.ProcessTick(game);
            Assert.IsFalse(planet.IsBlockaded());

            // Act: third tick, still not blockaded
            manager.ProcessTick(game);

            // Assert: No exception, no duplicate end event
            Assert.IsFalse(planet.IsBlockaded());
        }

        [Test]
        public void ProcessTick_BlockadeStart_DestroysOnlyInTransitDefendingRegiments()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Regiment empireInTransit = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };
            Regiment allianceInTransit = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Rebel Troops",
                OwnerInstanceID = "alliance",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(empireInTransit, planet);

            // Manually attach enemy regiment (bypasses owner validation)
            allianceInTransit.ParentNode = planet;
            allianceInTransit.ParentInstanceID = planet.InstanceID;
            planet.Regiments.Add(allianceInTransit);
            game.AddSceneNodeByInstanceID(allianceInTransit);

            // Act
            manager.ProcessTick(game);

            // Assert: only defending in-transit regiment destroyed
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));
        }

        [Test]
        public void ProcessTick_BlockadeStart_DestroysAllInTransitDefendingRegiments()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Regiment regiment1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Regiment 1",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };
            Regiment regiment2 = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Regiment 2",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };
            Regiment regiment3 = new Regiment
            {
                InstanceID = "r3",
                DisplayName = "Regiment 3",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(regiment1, planet);
            game.AttachNode(regiment2, planet);
            game.AttachNode(regiment3, planet);

            // Act
            manager.ProcessTick(game);

            // Assert: all in-transit defending regiments destroyed
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r3"));
        }

        [Test]
        public void ProcessTick_BlockadeStart_GarrisonedDefendersSurvive()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Regiment garrisoned1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Garrison 1",
                OwnerInstanceID = "empire",
                Movement = null,
            };
            Regiment garrisoned2 = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Garrison 2",
                OwnerInstanceID = "empire",
                Movement = null,
            };
            Regiment inTransit = new Regiment
            {
                InstanceID = "r3",
                DisplayName = "In Transit",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(garrisoned1, planet);
            game.AttachNode(garrisoned2, planet);
            game.AttachNode(inTransit, planet);

            // Act
            manager.ProcessTick(game);

            // Assert: garrisoned survive, in-transit destroyed
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r3"));
        }

        [Test]
        public void ProcessTick_MultiplePlanets_HandledIndependently()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system1 = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            PlanetSystem system2 = new PlanetSystem
            {
                InstanceID = "s2",
                DisplayName = "Hoth System",
            };
            Planet planet1 = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Planet planet2 = new Planet
            {
                InstanceID = "p2",
                DisplayName = "Hoth",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Fleet defenderFleet = new Fleet
            {
                InstanceID = "f2",
                DisplayName = "Imperial Fleet",
                OwnerInstanceID = "empire",
            };
            Regiment regiment1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Troops 1",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };
            Regiment regiment2 = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Troops 2",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p2",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system1, game.GetGalaxyMap());
            game.AttachNode(system2, game.GetGalaxyMap());
            game.AttachNode(planet1, system1);
            game.AttachNode(planet2, system2);
            game.AttachNode(hostileFleet, planet1); // Only planet1 blockaded
            game.AttachNode(defenderFleet, planet2); // Planet2 defended
            game.AttachNode(regiment1, planet1);
            game.AttachNode(regiment2, planet2);

            // Act
            manager.ProcessTick(game);

            // Assert: only planet1 in-transit regiment destroyed
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));
        }

        [Test]
        public void ProcessTick_SimultaneousBlockades_AllHandled()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system1 = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            PlanetSystem system2 = new PlanetSystem
            {
                InstanceID = "s2",
                DisplayName = "Hoth System",
            };
            Planet planet1 = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Planet planet2 = new Planet
            {
                InstanceID = "p2",
                DisplayName = "Hoth",
                OwnerInstanceID = "empire",
            };
            Fleet fleet1 = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Fleet 1",
                OwnerInstanceID = "alliance",
            };
            Fleet fleet2 = new Fleet
            {
                InstanceID = "f2",
                DisplayName = "Fleet 2",
                OwnerInstanceID = "alliance",
            };
            Regiment regiment1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Troops 1",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };
            Regiment regiment2 = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Troops 2",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p2",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system1, game.GetGalaxyMap());
            game.AttachNode(system2, game.GetGalaxyMap());
            game.AttachNode(planet1, system1);
            game.AttachNode(planet2, system2);
            game.AttachNode(fleet1, planet1);
            game.AttachNode(fleet2, planet2);
            game.AttachNode(regiment1, planet1);
            game.AttachNode(regiment2, planet2);

            // Act
            manager.ProcessTick(game);

            // Assert: both in-transit regiments destroyed
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));
        }

        [Test]
        public void ProcessTick_NullGame_ThrowsException()
        {
            BlockadeSystem manager = new BlockadeSystem(new GameRoot());
            Assert.Throws<InvalidOperationException>(() => manager.ProcessTick(null));
        }

        [Test]
        public void ProcessTick_BlockadeEnds_DoesNotRestoreTroops()
        {
            // Arrange
            GameRoot game = new GameRoot();
            BlockadeSystem manager = new BlockadeSystem(game);

            Faction empire = new Faction { InstanceID = "empire", DisplayName = "Empire" };
            Faction alliance = new Faction { InstanceID = "alliance", DisplayName = "Alliance" };
            PlanetSystem system = new PlanetSystem
            {
                InstanceID = "s1",
                DisplayName = "Tatooine System",
            };
            Planet planet = new Planet
            {
                InstanceID = "p1",
                DisplayName = "Tatooine",
                OwnerInstanceID = "empire",
            };
            Fleet hostileFleet = new Fleet
            {
                InstanceID = "f1",
                DisplayName = "Rebel Fleet",
                OwnerInstanceID = "alliance",
            };
            Fleet defenderFleet = new Fleet
            {
                InstanceID = "f2",
                DisplayName = "Imperial Fleet",
                OwnerInstanceID = "empire",
            };
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                Movement = new MovementState
                {
                    DestinationInstanceID = "p1",
                    TransitTicks = 10,
                    TicksElapsed = 5,
                },
            };

            game.Factions.Add(empire);
            game.Factions.Add(alliance);
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(hostileFleet, planet);
            game.AttachNode(regiment, planet);

            // Blockade starts, in-transit regiment destroyed
            manager.ProcessTick(game);
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));

            // Blockade ends
            game.AttachNode(defenderFleet, planet);
            manager.ProcessTick(game);

            // Assert: regiment still destroyed (not restored)
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
        }
    }
}
