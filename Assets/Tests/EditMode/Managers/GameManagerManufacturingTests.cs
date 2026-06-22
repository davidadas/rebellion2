using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class GameManagerManufacturingTests
    {
        [Test]
        public void StartManufacturing_CapitalShips_CreatesOneDestinationFleet()
        {
            GameRoot game = CreateGame();
            Planet planet = CreateShipyardPlanet(game, "p1", "empire");
            GameManager manager = new GameManager(game);
            CapitalShip template = CreateCapitalShipTemplate("dreadnaught", "Dreadnaught", 0);

            bool started = manager.StartManufacturing(planet, template, planet, 2);

            Assert.IsTrue(started);
            List<Fleet> fleets = planet.GetFleets();
            Assert.AreEqual(1, fleets.Count);
            Assert.AreEqual(2, fleets[0].CapitalShips.Count);
            Assert.AreEqual(2, planet.GetManufacturingQueue()[ManufacturingType.Ship].Count);
            Assert.AreSame(
                fleets[0].CapitalShips[0],
                planet.GetManufacturingQueue()[ManufacturingType.Ship][0]
            );
            Assert.AreSame(
                fleets[0].CapitalShips[1],
                planet.GetManufacturingQueue()[ManufacturingType.Ship][1]
            );
        }

        [Test]
        public void StartManufacturing_CapitalShipRejected_RemovesEmptyDestinationFleet()
        {
            GameRoot game = CreateGame();
            Planet planet = CreateShipyardPlanet(game, "p1", "empire");
            GameManager manager = new GameManager(game);
            CapitalShip template = CreateCapitalShipTemplate("dreadnaught", "Dreadnaught", 1);

            bool started = manager.StartManufacturing(planet, template, planet, 1);

            Assert.IsFalse(started);
            Assert.AreEqual(0, planet.GetFleets().Count);
            Assert.IsFalse(planet.GetManufacturingQueue().ContainsKey(ManufacturingType.Ship));
        }

        [Test]
        public void StopManufacturing_BuildingQueue_ClearsQueueAndQueuedDestinationBuildings()
        {
            GameRoot game = CreateGame();
            Planet producer = CreateConstructionPlanet(game, "p1", "empire");
            Planet destination = CreatePlanet(game, "p2", "empire");
            GameManager manager = new GameManager(game);
            Building template = CreateBuildingTemplate("mine");

            bool started = manager.StartManufacturing(producer, template, destination, 2);
            List<IManufacturable> queued = new List<IManufacturable>(
                producer.GetManufacturingQueue()[ManufacturingType.Building]
            );

            bool stopped = manager.StopManufacturing(producer, ManufacturingType.Building);

            Assert.IsTrue(started);
            Assert.IsTrue(stopped);
            Assert.IsFalse(
                producer.GetManufacturingQueue().ContainsKey(ManufacturingType.Building)
            );
            Assert.AreEqual(0, destination.Buildings.Count);
            foreach (IManufacturable item in queued)
                Assert.IsNull(((ISceneNode)item).GetParent());
        }

        [Test]
        public void StopManufacturing_EmptyQueue_ReturnsFalse()
        {
            GameRoot game = CreateGame();
            Planet planet = CreateConstructionPlanet(game, "p1", "empire");
            GameManager manager = new GameManager(game);

            bool stopped = manager.StopManufacturing(planet, ManufacturingType.Building);

            Assert.IsFalse(stopped);
        }

        private static GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });
            return game;
        }

        private static Planet CreateShipyardPlanet(GameRoot game, string planetId, string factionId)
        {
            PlanetSystem system = new PlanetSystem { InstanceID = $"{planetId}_system" };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = planetId,
                OwnerInstanceID = factionId,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 10,
            };
            game.AttachNode(planet, system);

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

        private static Planet CreateConstructionPlanet(
            GameRoot game,
            string planetId,
            string factionId
        )
        {
            Planet planet = CreatePlanet(game, planetId, factionId);

            game.AttachNode(
                new Building
                {
                    InstanceID = $"{planetId}_construction",
                    OwnerInstanceID = factionId,
                    BuildingType = BuildingType.ConstructionFacility,
                    ProductionType = ManufacturingType.Building,
                    ProcessRate = 1,
                    ManufacturingStatus = ManufacturingStatus.Complete,
                },
                planet
            );

            return planet;
        }

        private static Planet CreatePlanet(GameRoot game, string planetId, string factionId)
        {
            PlanetSystem system = new PlanetSystem { InstanceID = $"{planetId}_system" };
            game.AttachNode(system, game.Galaxy);

            Planet planet = new Planet
            {
                InstanceID = planetId,
                OwnerInstanceID = factionId,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 10,
            };
            game.AttachNode(planet, system);

            return planet;
        }

        private static CapitalShip CreateCapitalShipTemplate(
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

        private static Building CreateBuildingTemplate(string typeId)
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
