using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Sectors
{
    [TestFixture]
    public class MaintenanceSystemTests
    {
        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            GameRoot dependencyGame = CreateGame();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MaintenanceSystem(null, new FixedRNG(), new FleetSystem(dependencyGame))
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        [Test]
        public void Constructor_WithNullFleetSystem_ThrowsArgumentNullException()
        {
            GameRoot game = CreateGame();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MaintenanceSystem(game, new FixedRNG(), null)
            );

            Assert.AreEqual("fleetSystem", exception.ParamName);
        }

        [Test]
        public void ProcessTick_NoShortfall_DoesNotScrap()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
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

            FixedRNG rng = new FixedRNG();
            MaintenanceSystem system2 = new MaintenanceSystem(game, rng, new FleetSystem(game));

            system2.ProcessTick();

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
        }

        [Test]
        public void ProcessTick_Shortfall_AfterAutoscrapInterval_ScrapsOneUnit()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

            Regiment regiment1 = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 7,
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

            FixedRNG rng = new FixedRNG();
            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                rng,
                new FleetSystem(game)
            );

            List<GameResult> firstResults = maintenanceSystem.ProcessTick();
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            List<GameResult> secondResults = maintenanceSystem.ProcessTick();

            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment1.InstanceID));
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r2"));
            Assert.IsFalse(firstResults.OfType<GameObjectAutoscrappedResult>().Any());
            Assert.IsTrue(secondResults.OfType<GameObjectAutoscrappedResult>().Any());
            Assert.AreSame(
                planet,
                secondResults.OfType<PlanetGarrisonChangedResult>().Single().Planet
            );
            Assert.AreEqual(3, empire.RefinedMaterialStockpile);
            MaintenanceRequiredResult shortfall = firstResults
                .OfType<MaintenanceRequiredResult>()
                .FirstOrDefault();
            Assert.IsNotNull(shortfall);
            Assert.AreEqual(empire, shortfall.Faction);
            Assert.Greater(shortfall.Amount, 0);
        }

        [Test]
        public void ProcessTick_Shortfall_BeforeAutoscrapInterval_DoesNotScrapAgain()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

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

            FixedRNG rng = new FixedRNG();
            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                rng,
                new FleetSystem(game)
            );

            maintenanceSystem.ProcessTick();
            game.CurrentTick = 1;
            maintenanceSystem.ProcessTick();

            int remaining =
                (game.GetSceneNodeByInstanceID<Regiment>("r1") != null ? 1 : 0)
                + (game.GetSceneNodeByInstanceID<Regiment>("r2") != null ? 1 : 0);
            Assert.AreEqual(2, remaining);
        }

        [Test]
        public void ProcessTick_Shortfall_ContinuesScrappingWhileOverCapacity()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

            for (int i = 0; i < 3; i++)
            {
                Regiment regiment = new Regiment
                {
                    InstanceID = $"r{i}",
                    DisplayName = $"Stormtroopers {i}",
                    OwnerInstanceID = "empire",
                    ManufacturingStatus = ManufacturingStatus.Complete,
                    MaintenanceCost = 1,
                    ConstructionCost = 1,
                };
                game.AttachNode(regiment, planet);
            }

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                new FixedRNG(),
                new FleetSystem(game)
            );

            maintenanceSystem.ProcessTick();
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            maintenanceSystem.ProcessTick();
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval * 2;
            maintenanceSystem.ProcessTick();

            int remaining = Enumerable
                .Range(0, 3)
                .Count(index => game.GetSceneNodeByInstanceID<Regiment>($"r{index}") != null);

            Assert.AreEqual(1, remaining);
        }

        [Test]
        public void ProcessTick_UnitUnderConstruction_DoesNotScrap()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

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

            FixedRNG rng = new FixedRNG();
            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                rng,
                new FleetSystem(game)
            );

            List<GameResult> firstResults = maintenanceSystem.ProcessTick();
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            List<GameResult> secondResults = maintenanceSystem.ProcessTick();

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
            Assert.IsTrue(firstResults.OfType<MaintenanceRequiredResult>().Any());
            Assert.IsFalse(secondResults.OfType<GameObjectAutoscrappedResult>().Any());
        }

        [Test]
        public void ProcessTick_UnitUnderConstruction_ReservesMaintenance()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
                MaintenanceCost = 3,
                ConstructionCost = 10,
            };
            game.AttachNode(regiment, planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                new FixedRNG(),
                new FleetSystem(game)
            );

            List<GameResult> results = maintenanceSystem.ProcessTick();

            MaintenanceRequiredResult shortfall = results
                .OfType<MaintenanceRequiredResult>()
                .Single();
            Assert.AreEqual(3, shortfall.Amount);
            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Regiment>("r1"));
        }

        [Test]
        public void ProcessTick_UnitInTransit_RemainsEligibleForAutoscrap()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

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

            FixedRNG rng = new FixedRNG();
            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                rng,
                new FleetSystem(game)
            );

            maintenanceSystem.ProcessTick();
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;

            List<GameResult> results = maintenanceSystem.ProcessTick();

            Assert.IsNull(game.GetSceneNodeByInstanceID<CapitalShip>(ship.InstanceID));
            Assert.AreSame(
                ship,
                results.OfType<GameObjectAutoscrappedResult>().Single().DestroyedObject
            );
        }

        [Test]
        public void ProcessTick_ExcessBuildingsOverCapacity_ScrapsBuildings()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

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

            FixedRNG rng = new FixedRNG();
            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                rng,
                new FleetSystem(game)
            );

            maintenanceSystem.ProcessTick();
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            maintenanceSystem.ProcessTick();

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(defense.InstanceID));
        }

        [Test]
        public void ProcessTick_ZeroMaintenanceInfrastructurePresent_ScrapsPositiveMaintenanceUnitFirst()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            planet.NumRawResourceNodes = 0;
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);

            Building mine = CreateMine("mine1", "empire");
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                DisplayName = "Stormtroopers",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 1,
                ConstructionCost = 1,
            };

            game.AttachNode(mine, planet);
            game.AttachNode(regiment, planet);

            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                new FixedRNG(),
                new FleetSystem(game)
            );

            maintenanceSystem.ProcessTick();
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            maintenanceSystem.ProcessTick();

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("mine1"));
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
        }

        [Test]
        public void GetMaintenanceCapacity_FactionWithPlanets_CalculatesCorrectly()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(CreateMine("mine1", "empire"), planet);
            game.AttachNode(CreateMine("mine2", "empire"), planet);
            game.AttachNode(CreateRefinery("ref1", "empire"), planet);

            int capacity = empire.MaintenanceCapacity;

            Assert.AreEqual(50, capacity);
        }

        [Test]
        public void GetMaintenanceCapacity_RefinementMultiplierDoesNotChangeCapacity()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            empire.Settings.RefinementMultiplier = 1;
            game.GetFactions().Add(empire);

            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(CreateMine("mine1", "empire"), planet);
            game.AttachNode(CreateRefinery("ref1", "empire"), planet);

            Assert.AreEqual(50, empire.MaintenanceCapacity);
        }

        [Test]
        public void GetMaintenanceCapacity_MineAndRefineryOnDifferentPlanets_CalculatesGlobalPair()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);
            PlanetSector sector = new PlanetSector { InstanceID = "s1", DisplayName = "Sector" };
            Planet minePlanet = CreatePlanet("p1", "Coruscant", empire.InstanceID);
            Planet refineryPlanet = CreatePlanet("p2", "Kessel", empire.InstanceID);
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(minePlanet, sector);
            game.AttachNode(refineryPlanet, sector);
            game.AttachNode(CreateMine("mine1", empire.InstanceID), minePlanet);
            game.AttachNode(CreateRefinery("ref1", empire.InstanceID), refineryPlanet);

            int capacity = empire.MaintenanceCapacity;

            Assert.AreEqual(50, capacity);
        }

        [Test]
        public void TryScrap_OwnedSurfaceRegiment_RefundsRemovesAndReportsGarrisonChange()
        {
            GameRoot game = CreateGame();
            game.Config.Production.ScrapRefundDivisor = 7;
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);
            PlanetSector sector = new PlanetSector { InstanceID = "s1" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                ConstructionCost = 7,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(regiment, planet);
            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                new FixedRNG(),
                new FleetSystem(game)
            );
            IReadOnlyList<GameResult> results = null;
            maintenanceSystem.ResultsProduced += producedResults => results = producedResults;

            bool scrapped = maintenanceSystem.TryScrap(
                new List<IManufacturable> { regiment },
                "empire"
            );

            Assert.IsTrue(scrapped);
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            Assert.IsNull(regiment.GetParent());
            Assert.AreEqual(1, empire.RefinedMaterialStockpile);
            Assert.AreSame(planet, results.OfType<PlanetGarrisonChangedResult>().Single().Planet);
        }

        [Test]
        public void TryScrap_UnitUnderConstruction_PreservesUnitAndMaterials()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);
            PlanetSector sector = new PlanetSector { InstanceID = "s1" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Building,
                ConstructionCost = 7,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(regiment, planet);
            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                new FixedRNG(),
                new FleetSystem(game)
            );
            IReadOnlyList<GameResult> results = null;
            maintenanceSystem.ResultsProduced += producedResults => results = producedResults;

            bool scrapped = maintenanceSystem.TryScrap(
                new List<IManufacturable> { regiment },
                "empire"
            );

            Assert.IsFalse(scrapped);
            Assert.AreSame(regiment, game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            Assert.AreSame(planet, regiment.GetParent());
            Assert.AreEqual(0, empire.RefinedMaterialStockpile);
            Assert.IsNull(results);
        }

        [Test]
        public void TryScrap_OtherFactionUnit_PreservesUnit()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);
            PlanetSector sector = new PlanetSector { InstanceID = "s1" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            Regiment regiment = new Regiment
            {
                InstanceID = "r1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(regiment, planet);
            MaintenanceSystem maintenanceSystem = new MaintenanceSystem(
                game,
                new FixedRNG(),
                new FleetSystem(game)
            );
            IReadOnlyList<GameResult> results = null;
            maintenanceSystem.ResultsProduced += producedResults => results = producedResults;

            bool scrapped = maintenanceSystem.TryScrap(
                new List<IManufacturable> { regiment },
                "alliance"
            );

            Assert.IsFalse(scrapped);
            Assert.AreSame(regiment, game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
            Assert.AreSame(planet, regiment.GetParent());
            Assert.IsNull(results);
        }

        private GameRoot CreateGame()
        {
            return new GameRoot(TestConfig.Create());
        }

        private Faction CreateFaction(string id, string name)
        {
            Faction faction = new Faction { InstanceID = id, DisplayName = name };
            faction.Settings.ResourceProcessingPointsPerFacility = 50;
            return faction;
        }

        private Planet CreatePlanet(string id, string name, string ownerId)
        {
            return new Planet
            {
                InstanceID = id,
                DisplayName = name,
                OwnerInstanceID = ownerId,
                IsColonized = true,
                EnergyCapacity = 10,
                NumRawResourceNodes = 5,
            };
        }

        private Building CreateMine(string id, string ownerId)
        {
            return new Building
            {
                InstanceID = id,
                DisplayName = "Mine",
                OwnerInstanceID = ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 0,
                ConstructionCost = 1,
                BuildingType = BuildingType.Mine,
            };
        }

        private Building CreateRefinery(string id, string ownerId)
        {
            return new Building
            {
                InstanceID = id,
                DisplayName = "Refinery",
                OwnerInstanceID = ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                MaintenanceCost = 0,
                ConstructionCost = 1,
                BuildingType = BuildingType.Refinery,
            };
        }
    }
}
