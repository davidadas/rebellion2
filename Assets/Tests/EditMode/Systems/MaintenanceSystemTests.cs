using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class MaintenanceSystemTests
    {
        private GameRoot CreateGame()
        {
            return new GameRoot(TestConfig.Create());
        }

        private Faction CreateFaction(string id, string name)
        {
            return new Faction { InstanceID = id, DisplayName = name };
        }

        private Planet CreatePlanet(string id, string name, string ownerID)
        {
            return new Planet
            {
                InstanceID = id,
                DisplayName = name,
                OwnerInstanceID = ownerID,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 5,
            };
        }

        private Building CreateMine(string id, string ownerID)
        {
            return new Building
            {
                InstanceID = id,
                DisplayName = "Mine",
                OwnerInstanceID = ownerID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 0,
                ConstructionCost = 1,
                BuildingType = BuildingType.Mine,
            };
        }

        private Building CreateRefinery(string id, string ownerID)
        {
            return new Building
            {
                InstanceID = id,
                DisplayName = "Refinery",
                OwnerInstanceID = ownerID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 0,
                ConstructionCost = 1,
                BuildingType = BuildingType.Refinery,
            };
        }

        [Test]
        public void ProcessTick_NoShortfall_DoesNotScrap()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(CreateMine("mine1", "empire"), planet);
            game.AttachNode(CreateRefinery("ref1", "empire"), planet);

            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };
            game.AttachNode(regiment, planet);

            MaintenanceSystem system2 = new MaintenanceSystem(game);
            FixedRNG rng = new FixedRNG();

            system2.ProcessTick(rng);

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
        }

        [Test]
        public void ProcessTick_Shortfall_ScrapsOneUnit()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);

            Regiment regiment1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };
            Regiment regiment2 = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Snowtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };
            game.AttachNode(regiment1, planet);
            game.AttachNode(regiment2, planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(game);
            // RNG returns 0, so first candidate is selected
            FixedRNG rng = new FixedRNG();

            List<GameResult> results = maintenanceSystem.ProcessTick(rng);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));
            Assert.IsTrue(results.OfType<GameObjectAutoscrappedResult>().Any());
            MaintenanceRequiredResult shortfall = results
                .OfType<MaintenanceRequiredResult>()
                .FirstOrDefault();
            Assert.IsNotNull(shortfall);
            Assert.AreEqual(empire, shortfall.Faction);
            Assert.Greater(shortfall.Amount, 0);
        }

        [Test]
        public void ProcessTick_Shortfall_ScrapsOnlyOnePerTick()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);

            Regiment regiment1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };
            Regiment regiment2 = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Snowtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };
            game.AttachNode(regiment1, planet);
            game.AttachNode(regiment2, planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(game);
            FixedRNG rng = new FixedRNG();

            maintenanceSystem.ProcessTick(rng);

            int remaining =
                (game.GetSceneNodeByInstanceID<Regiment>("r1") != null ? 1 : 0)
                + (game.GetSceneNodeByInstanceID<Regiment>("r2") != null ? 1 : 0);
            Assert.AreEqual(1, remaining);
        }

        [Test]
        public void ProcessTick_UnitUnderConstruction_DoesNotScrap()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);

            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
                MaintenanceCost = 1,
                ConstructionCost = 10,
            };
            game.AttachNode(regiment, planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(game);
            FixedRNG rng = new FixedRNG();

            maintenanceSystem.ProcessTick(rng);

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
        }

        [Test]
        public void ProcessTick_UnitInTransit_DoesNotScrap()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);

            Fleet fleet = EntityFactory.CreateFleet("f1", "empire");
            game.AttachNode(fleet, planet);

            CapitalShip ship = new CapitalShip
            {
                InstanceID = "cs1",
                DisplayName = "Star Destroyer",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
                Movement = new MovementState
                {
                    TransitTicks = 10,
                    TicksElapsed = 1,
                    OriginPosition = new Point(0, 0),
                    CurrentPosition = new Point(0, 0),
                },
            };
            game.AttachNode(ship, fleet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(game);
            FixedRNG rng = new FixedRNG();

            maintenanceSystem.ProcessTick(rng);

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<CapitalShip>("cs1"));
        }

        [Test]
        public void GetMaintenanceCapacity_FactionWithPlanets_CalculatesCorrectly()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);
            game.AttachNode(CreateMine("mine1", "empire"), planet);
            game.AttachNode(CreateMine("mine2", "empire"), planet);
            game.AttachNode(CreateRefinery("ref1", "empire"), planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(game);

            int capacity = maintenanceSystem.GetMaintenanceCapacity(empire);

            Assert.AreEqual(50, capacity);
        }

        [Test]
        public void ProcessTick_ExcessBuildingsOverCapacity_ScrapsBuildings()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);

            Building defense = new Building
            {
                InstanceID = "b1",
                DisplayName = "Planetary Turret",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
                BuildingType = BuildingType.Defense,
            };
            game.AttachNode(defense, planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(game);
            FixedRNG rng = new FixedRNG();

            maintenanceSystem.ProcessTick(rng);

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>("b1"));
        }
    }
}
