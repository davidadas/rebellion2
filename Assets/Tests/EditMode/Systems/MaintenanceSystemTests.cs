using NUnit.Framework;
using Rebellion.Game;
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
            // Arrange: faction has 1 mine + 1 refinery on planet with 5 resource nodes
            // Capacity = min(1, 5, 1) * 50 = 50. Regiment costs 1. No shortfall.
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

            // Act
            system2.ProcessTick(rng);

            // Assert: regiment still exists
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
        }

        [Test]
        public void ProcessTick_Shortfall_ScrapsOneUnit()
        {
            // Arrange: no mines or refineries, so capacity = 0.
            // Regiment has maintenance cost 1, so required > capacity.
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);

            Regiment r1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };
            Regiment r2 = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Snowtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };
            game.AttachNode(r1, planet);
            game.AttachNode(r2, planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(game);
            // RNG returns 0, so first candidate is selected
            FixedRNG rng = new FixedRNG();

            // Act
            maintenanceSystem.ProcessTick(rng);

            // Assert: exactly one regiment was scrapped (the first one, index 0)
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));
        }

        [Test]
        public void ProcessTick_Shortfall_ScrapsOnlyOnePerTick()
        {
            // Arrange: capacity 0, two regiments each costing 1
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);

            Regiment r1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };
            Regiment r2 = new Regiment
            {
                InstanceID = "r2",
                DisplayName = "Snowtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };
            game.AttachNode(r1, planet);
            game.AttachNode(r2, planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(game);
            FixedRNG rng = new FixedRNG();

            // Act: single tick
            maintenanceSystem.ProcessTick(rng);

            // Assert: only one was scrapped, not both
            int remaining =
                (game.GetSceneNodeByInstanceID<Regiment>("r1") != null ? 1 : 0)
                + (game.GetSceneNodeByInstanceID<Regiment>("r2") != null ? 1 : 0);
            Assert.AreEqual(1, remaining);
        }

        [Test]
        public void ProcessTick_DoesNotScrapUnderConstruction()
        {
            // Arrange: only unit is under construction — should not be scrapped
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.Factions.Add(empire);

            PlanetSystem system = new PlanetSystem { InstanceID = "s1", DisplayName = "System" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(system, game.GetGalaxyMap());
            game.AttachNode(planet, system);

            Regiment r1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
                MaintenanceCost = 1,
                ConstructionCost = 10,
            };
            game.AttachNode(r1, planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(game);
            FixedRNG rng = new FixedRNG();

            // Act
            maintenanceSystem.ProcessTick(rng);

            // Assert: still exists (not eligible for scrap)
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
        }

        [Test]
        public void GetMaintenanceCapacity_CalculatesCorrectly()
        {
            // Arrange: 2 mines, 5 resource nodes, 1 refinery
            // Capacity = min(2, 5, 1) * 50 = 50
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

            // Act
            int capacity = maintenanceSystem.GetMaintenanceCapacity(empire);

            // Assert: min(2 mines, 5 nodes, 1 refinery) * 50 = 50
            Assert.AreEqual(50, capacity);
        }

        [Test]
        public void ProcessTick_CanScrapBuildings()
        {
            // Arrange: capacity 0, only unit is a completed defense building
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

            // Act
            maintenanceSystem.ProcessTick(rng);

            // Assert
            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>("b1"));
        }
    }
}
