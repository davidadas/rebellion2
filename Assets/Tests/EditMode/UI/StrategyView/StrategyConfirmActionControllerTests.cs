using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Tests.UI.StrategyView
{
    [TestFixture]
    public class StrategyConfirmActionControllerTests
    {
        [Test]
        public void TryExecuteMove_FleetToFleet_MovesCapitalShipsAndRemovesSourceFleet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Fleet sourceFleet = EntityFactory.CreateFleet("f0", "empire");
            game.AttachNode(sourceFleet, planet);

            CapitalShip firstShip = CreateCapitalShip("cs1");
            CapitalShip secondShip = CreateCapitalShip("cs2");
            game.AttachNode(firstShip, sourceFleet);
            game.AttachNode(secondShip, sourceFleet);

            Fleet destinationFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destinationFleet, planet);

            StrategyConfirmActionController controller = new StrategyConfirmActionController(
                new GameManager(game)
            );
            StrategyMissionTarget target = new StrategyMissionTarget(
                new GalaxyMapPlanet(system, planet, string.Empty),
                destinationFleet
            );

            bool moved = controller.TryExecuteMove(
                target,
                new List<ISceneNode> { sourceFleet },
                "empire"
            );

            Assert.IsTrue(moved);
            Assert.AreEqual(destinationFleet, firstShip.GetParent());
            Assert.AreEqual(destinationFleet, secondShip.GetParent());
            Assert.IsFalse(planet.Fleets.Contains(sourceFleet));
            Assert.IsNull(sourceFleet.GetParent());
            Assert.IsNull(firstShip.Movement);
            Assert.IsNull(secondShip.Movement);
        }

        [Test]
        public void TryExecuteMove_CapitalShipToFleet_MovesShipAndRemovesEmptySourceFleet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Fleet sourceFleet = EntityFactory.CreateFleet("f0", "empire");
            game.AttachNode(sourceFleet, planet);

            CapitalShip ship = CreateCapitalShip("cs1");
            game.AttachNode(ship, sourceFleet);

            Fleet destinationFleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(destinationFleet, planet);

            StrategyConfirmActionController controller = new StrategyConfirmActionController(
                new GameManager(game)
            );
            StrategyMissionTarget target = new StrategyMissionTarget(
                new GalaxyMapPlanet(system, planet, string.Empty),
                destinationFleet
            );

            bool moved = controller.TryExecuteMove(target, new List<ISceneNode> { ship }, "empire");

            Assert.IsTrue(moved);
            Assert.AreEqual(destinationFleet, ship.GetParent());
            Assert.IsFalse(planet.Fleets.Contains(sourceFleet));
            Assert.IsNull(sourceFleet.GetParent());
            Assert.IsNull(ship.Movement);
        }

        [Test]
        public void TryExecuteMove_CapitalShipToPlanet_CreatesDestinationFleet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Fleet sourceFleet = EntityFactory.CreateFleet("f0", "empire");
            game.AttachNode(sourceFleet, planet);

            CapitalShip ship = CreateCapitalShip("cs1");
            game.AttachNode(ship, sourceFleet);

            StrategyConfirmActionController controller = new StrategyConfirmActionController(
                new GameManager(game)
            );
            StrategyMissionTarget target = new StrategyMissionTarget(
                new GalaxyMapPlanet(system, planet, string.Empty),
                null
            );

            bool moved = controller.TryExecuteMove(target, new List<ISceneNode> { ship }, "empire");

            Assert.IsTrue(moved);
            Assert.AreEqual(1, planet.Fleets.Count);
            Assert.AreNotEqual(sourceFleet, planet.Fleets[0]);
            Assert.AreEqual(planet.Fleets[0], ship.GetParent());
            Assert.IsFalse(planet.Fleets.Contains(sourceFleet));
            Assert.IsNull(sourceFleet.GetParent());
            Assert.IsNull(ship.Movement);
        }

        [Test]
        public void TryExecuteMove_CapitalShipToViewPlanet_CreatesDestinationFleetOnLivePlanet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Fleet sourceFleet = EntityFactory.CreateFleet("f0", "empire");
            game.AttachNode(sourceFleet, planet);

            CapitalShip ship = CreateCapitalShip("cs1");
            game.AttachNode(ship, sourceFleet);

            PlanetSystem viewSystem = new PlanetSystem { InstanceID = system.InstanceID };
            Planet viewPlanet = new Planet
            {
                InstanceID = planet.InstanceID,
                OwnerInstanceID = planet.OwnerInstanceID,
                IsColonized = true,
                PositionX = planet.PositionX,
                PositionY = planet.PositionY,
            };

            StrategyConfirmActionController controller = new StrategyConfirmActionController(
                new GameManager(game)
            );
            StrategyMissionTarget target = new StrategyMissionTarget(
                new GalaxyMapPlanet(viewSystem, viewPlanet, string.Empty),
                null
            );

            bool moved = controller.TryExecuteMove(target, new List<ISceneNode> { ship }, "empire");

            Assert.IsTrue(moved);
            Assert.AreEqual(1, planet.Fleets.Count);
            Assert.AreNotEqual(sourceFleet, planet.Fleets[0]);
            Assert.AreEqual(planet.Fleets[0], ship.GetParent());
            Assert.AreEqual(planet, ship.GetParentOfType<Planet>());
            Assert.AreEqual(0, viewPlanet.Fleets.Count);
            Assert.IsFalse(planet.Fleets.Contains(sourceFleet));
            Assert.IsNull(sourceFleet.GetParent());
            Assert.IsNull(ship.Movement);
        }

        [Test]
        public void TryExecuteMove_MultipleCapitalShipsToPlanet_CreatesOneDestinationFleet()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });

            PlanetSystem system = new PlanetSystem { InstanceID = "sys1" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet planet = new Planet
            {
                InstanceID = "p1",
                OwnerInstanceID = "empire",
                IsColonized = true,
                PositionX = 0,
                PositionY = 0,
            };
            game.AttachNode(planet, system);

            Fleet sourceFleet = EntityFactory.CreateFleet("f0", "empire");
            game.AttachNode(sourceFleet, planet);

            CapitalShip firstShip = CreateCapitalShip("cs1");
            CapitalShip secondShip = CreateCapitalShip("cs2");
            game.AttachNode(firstShip, sourceFleet);
            game.AttachNode(secondShip, sourceFleet);

            StrategyConfirmActionController controller = new StrategyConfirmActionController(
                new GameManager(game)
            );
            StrategyMissionTarget target = new StrategyMissionTarget(
                new GalaxyMapPlanet(system, planet, string.Empty),
                null
            );

            bool moved = controller.TryExecuteMove(
                target,
                new List<ISceneNode> { firstShip, secondShip },
                "empire"
            );

            Assert.IsTrue(moved);
            Assert.AreEqual(1, planet.Fleets.Count);
            Assert.AreEqual(planet.Fleets[0], firstShip.GetParent());
            Assert.AreEqual(planet.Fleets[0], secondShip.GetParent());
            Assert.AreEqual(2, planet.Fleets[0].CapitalShips.Count);
            Assert.IsFalse(planet.Fleets.Contains(sourceFleet));
            Assert.IsNull(sourceFleet.GetParent());
            Assert.IsNull(firstShip.Movement);
            Assert.IsNull(secondShip.Movement);
        }

        private static CapitalShip CreateCapitalShip(string instanceId)
        {
            return new CapitalShip
            {
                InstanceID = instanceId,
                OwnerInstanceID = "empire",
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }
    }
}
