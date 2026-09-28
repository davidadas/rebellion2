using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class MaintenanceCommandsTests
    {
        [Test]
        public void TryScrap_LaterSelectionIsUnfinished_PreservesEarlierSelection()
        {
            GameRoot game = CreateGame();
            Faction owner = CreateFaction("owner", "Owner");
            game.GetFactions().Add(owner);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = CreatePlanet("planet", "Planet", owner.InstanceID);
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            Building first = CreateMine("first", owner.InstanceID);
            Building second = CreateRefinery("second", owner.InstanceID);
            second.ManufacturingStatus = ManufacturingStatus.Building;
            game.AttachNode(first, planet);
            game.AttachNode(second, planet);
            MaintenanceCommands maintenance = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
            );

            Assert.IsFalse(
                maintenance.TryScrap(new IManufacturable[] { first, second }, owner.InstanceID)
            );

            Assert.AreSame(first, game.GetSceneNodeByInstanceID<Building>(first.InstanceID));
        }

        [Test]
        public void TryScrap_MultipleUnits_NotifiesAfterAllRemovals()
        {
            GameRoot game = CreateGame();
            Faction owner = CreateFaction("owner", "Owner");
            game.GetFactions().Add(owner);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = CreatePlanet("planet", "Planet", owner.InstanceID);
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            Building first = CreateMine("first", owner.InstanceID);
            Building second = CreateRefinery("second", owner.InstanceID);
            game.AttachNode(first, planet);
            game.AttachNode(second, planet);
            MaintenanceCommands maintenance = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
            );
            int deliveries = 0;
            maintenance.ResultsProduced += results =>
            {
                deliveries++;
                Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(first.InstanceID));
                Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(second.InstanceID));
                CollectionAssert.AreEqual(
                    new[] { first, second },
                    results
                        .OfType<GameObjectScrappedResult>()
                        .Select(result => result.ScrappedObject)
                );
            };

            maintenance.TryScrap(new IManufacturable[] { first, second }, owner.InstanceID);

            Assert.AreEqual(1, deliveries);
        }

        [Test]
        public void TryScrap_ListenerThrows_PropagatesAfterRemoval()
        {
            GameRoot game = CreateGame();
            Faction owner = CreateFaction("owner", "Owner");
            game.GetFactions().Add(owner);
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = CreatePlanet("planet", "Planet", owner.InstanceID);
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            Building building = CreateMine("mine", owner.InstanceID);
            game.AttachNode(building, planet);
            MaintenanceCommands maintenance = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
            );
            InvalidOperationException failure = new InvalidOperationException("listener failure");
            maintenance.ResultsProduced += _ => throw failure;

            Assert.AreSame(
                failure,
                Assert.Throws<InvalidOperationException>(() =>
                    maintenance.TryScrap(new IManufacturable[] { building }, owner.InstanceID)
                )
            );

            Assert.IsNull(game.GetSceneNodeByInstanceID<Building>(building.InstanceID));
        }

        [Test]
        public void TryScrap_CapitalShipWithOfficer_RestoresOfficerAtLocalPlanet()
        {
            var scene = BuildCarrierScrapScene();

            bool scrapped = scene.maintenance.TryScrap(
                new IManufacturable[] { scene.carrier },
                scene.owner.InstanceID
            );

            Assert.IsTrue(scrapped);
            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<CapitalShip>(scene.carrier.InstanceID, true)
            );
            Assert.AreSame(
                scene.officer,
                scene.game.GetSceneNodeByInstanceID<Officer>(scene.officer.InstanceID, true)
            );
            Assert.AreSame(scene.origin, scene.officer.GetParent());
            Assert.AreNotSame(scene.nearestDestination, scene.officer.GetParent());
            Assert.AreNotSame(scene.fartherDestination, scene.officer.GetParent());
            Assert.IsNull(scene.officer.Movement);
        }

        [Test]
        public void TryScrap_CapitalShipWithInactiveOfficer_RelocatesOfficerWithoutActivating()
        {
            var scene = BuildCarrierScrapScene();
            scene.officer.IsEnabled = false;

            bool scrapped = scene.maintenance.TryScrap(
                new IManufacturable[] { scene.carrier },
                scene.owner.InstanceID
            );

            Assert.IsTrue(scrapped);
            Assert.AreSame(
                scene.officer,
                scene.game.GetSceneNodeByInstanceID<Officer>(
                    scene.officer.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.origin, scene.officer.GetParent());
            Assert.IsNull(scene.officer.Movement);
            Assert.IsFalse(scene.officer.IsEnabled);
        }

        [Test]
        public void TryScrap_CapitalShipWithCompletedFighter_DestroysFighter()
        {
            var scene = BuildCarrierScrapScene();
            scene.carrier.StarfighterCapacity = 1;
            Starfighter fighter = EntityFactory.CreateStarfighter("scrap-fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            scene.game.AttachNode(fighter, scene.carrier);

            bool scrapped = scene.maintenance.TryScrap(
                new IManufacturable[] { scene.carrier },
                scene.owner.InstanceID
            );

            Assert.IsTrue(scrapped);
            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<Starfighter>(
                    fighter.InstanceID,
                    includeDisabled: true
                )
            );
        }

        [Test]
        public void Constructor_WithNullGame_ThrowsArgumentNullException()
        {
            GameRoot dependencyGame = CreateGame();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MaintenanceCommands(null, new FixedRNG(), new FleetCommands(dependencyGame))
            );

            Assert.AreEqual("game", exception.ParamName);
        }

        [Test]
        public void Constructor_WithNullFleetSystem_ThrowsArgumentNullException()
        {
            GameRoot game = CreateGame();

            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() =>
                new MaintenanceCommands(game, new FixedRNG(), null)
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
            MaintenanceCommands system2 = new MaintenanceCommands(
                game,
                rng,
                new FleetCommands(game)
            );

            new MaintenanceTickProcessor(system2).ProcessTick(game);

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
            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                rng,
                new FleetCommands(game)
            );

            IReadOnlyList<GameResult> firstResults = new MaintenanceTickProcessor(
                maintenanceSystem
            ).ProcessTick(game);
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            IReadOnlyList<GameResult> secondResults = new MaintenanceTickProcessor(
                maintenanceSystem
            ).ProcessTick(game);

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
        public void ProcessTick_AutoscrappedCapitalShipWithOfficer_RestoresOfficerAtLocalPlanet()
        {
            var scene = BuildCarrierScrapScene();
            IReadOnlyList<GameResult> initialResults = new MaintenanceTickProcessor(
                scene.maintenance
            ).ProcessTick(scene.game);
            scene.resultBus.Publish(initialResults);
            scene.game.CurrentTick = scene
                .game
                .Config
                .Production
                .MaintenanceShortfallAutoscrapInterval;

            IReadOnlyList<GameResult> autoscrapResults = new MaintenanceTickProcessor(
                scene.maintenance
            ).ProcessTick(scene.game);
            scene.resultBus.Publish(autoscrapResults);

            Assert.AreSame(
                scene.carrier,
                autoscrapResults.OfType<GameObjectAutoscrappedResult>().Single().DestroyedObject
            );
            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<CapitalShip>(scene.carrier.InstanceID, true)
            );
            Assert.AreSame(
                scene.officer,
                scene.game.GetSceneNodeByInstanceID<Officer>(scene.officer.InstanceID, true)
            );
            Assert.AreSame(scene.origin, scene.officer.GetParent());
            Assert.AreNotSame(scene.nearestDestination, scene.officer.GetParent());
            Assert.AreNotSame(scene.fartherDestination, scene.officer.GetParent());
            Assert.IsNull(scene.officer.Movement);
        }

        [Test]
        public void ProcessTick_AutoscrappedCapitalShipWithInactiveOfficer_RelocatesOfficerWithoutActivating()
        {
            var scene = BuildCarrierScrapScene();
            scene.officer.IsEnabled = false;
            IReadOnlyList<GameResult> initialResults = new MaintenanceTickProcessor(
                scene.maintenance
            ).ProcessTick(scene.game);
            scene.resultBus.Publish(initialResults);
            scene.game.CurrentTick = scene
                .game
                .Config
                .Production
                .MaintenanceShortfallAutoscrapInterval;

            IReadOnlyList<GameResult> autoscrapResults = new MaintenanceTickProcessor(
                scene.maintenance
            ).ProcessTick(scene.game);
            scene.resultBus.Publish(autoscrapResults);

            Assert.AreSame(
                scene.carrier,
                autoscrapResults.OfType<GameObjectAutoscrappedResult>().Single().DestroyedObject
            );
            Assert.AreSame(
                scene.officer,
                scene.game.GetSceneNodeByInstanceID<Officer>(
                    scene.officer.InstanceID,
                    includeDisabled: true
                )
            );
            Assert.AreSame(scene.origin, scene.officer.GetParent());
            Assert.IsNull(scene.officer.Movement);
            Assert.IsFalse(scene.officer.IsEnabled);
        }

        [Test]
        public void ProcessTick_AutoscrappedCapitalShipWithCompletedFighter_DestroysFighter()
        {
            var scene = BuildCarrierScrapScene();
            scene.carrier.StarfighterCapacity = 1;
            Starfighter fighter = EntityFactory.CreateStarfighter("autoscrap-fighter", "empire");
            fighter.ManufacturingStatus = ManufacturingStatus.Complete;
            fighter.ConstructionCost = 0;
            fighter.MaintenanceCost = 0;
            scene.game.AttachNode(fighter, scene.carrier);
            IReadOnlyList<GameResult> initialResults = new MaintenanceTickProcessor(
                scene.maintenance
            ).ProcessTick(scene.game);
            scene.resultBus.Publish(initialResults);
            scene.game.CurrentTick = scene
                .game
                .Config
                .Production
                .MaintenanceShortfallAutoscrapInterval;

            IReadOnlyList<GameResult> autoscrapResults = new MaintenanceTickProcessor(
                scene.maintenance
            ).ProcessTick(scene.game);
            scene.resultBus.Publish(autoscrapResults);

            Assert.AreSame(
                scene.carrier,
                autoscrapResults.OfType<GameObjectAutoscrappedResult>().Single().DestroyedObject
            );
            Assert.IsNull(
                scene.game.GetSceneNodeByInstanceID<Starfighter>(
                    fighter.InstanceID,
                    includeDisabled: true
                )
            );
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
            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                rng,
                new FleetCommands(game)
            );

            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);
            game.CurrentTick = 1;
            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);

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

            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
            );

            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval * 2;
            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);

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
            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                rng,
                new FleetCommands(game)
            );

            IReadOnlyList<GameResult> firstResults = new MaintenanceTickProcessor(
                maintenanceSystem
            ).ProcessTick(game);
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            IReadOnlyList<GameResult> secondResults = new MaintenanceTickProcessor(
                maintenanceSystem
            ).ProcessTick(game);

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

            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
            );

            IReadOnlyList<GameResult> results = new MaintenanceTickProcessor(
                maintenanceSystem
            ).ProcessTick(game);

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
            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                rng,
                new FleetCommands(game)
            );

            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;

            IReadOnlyList<GameResult> results = new MaintenanceTickProcessor(
                maintenanceSystem
            ).ProcessTick(game);

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
            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                rng,
                new FleetCommands(game)
            );

            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);

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

            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
            );

            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);
            game.CurrentTick = game.Config.Production.MaintenanceShortfallAutoscrapInterval;
            new MaintenanceTickProcessor(maintenanceSystem).ProcessTick(game);

            Assert.IsNotNull(game.GetSceneNodeByInstanceID<Building>("mine1"));
            Assert.IsNull(game.GetSceneNodeByInstanceID<Regiment>(regiment.InstanceID));
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
            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
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
        public void TryScrap_OwnedBuilding_ReportsScrappedObjectAndContext()
        {
            GameRoot game = CreateGame();
            Faction empire = CreateFaction("empire", "Empire");
            game.GetFactions().Add(empire);
            PlanetSector sector = new PlanetSector { InstanceID = "s1" };
            Planet planet = CreatePlanet("p1", "Coruscant", "empire");
            Building shipyard = new Building
            {
                InstanceID = "shipyard1",
                OwnerInstanceID = "empire",
                ManufacturingStatus = ManufacturingStatus.Complete,
                BuildingType = BuildingType.Shipyard,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            game.AttachNode(shipyard, planet);
            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
            );
            IReadOnlyList<GameResult> results = null;
            maintenanceSystem.ResultsProduced += producedResults => results = producedResults;

            bool scrapped = maintenanceSystem.TryScrap(
                new List<IManufacturable> { shipyard },
                "empire"
            );

            GameObjectScrappedResult scrappedResult = results
                .OfType<GameObjectScrappedResult>()
                .Single();
            Assert.IsTrue(scrapped);
            Assert.AreSame(shipyard, scrappedResult.ScrappedObject);
            Assert.AreSame(planet, scrappedResult.Context);
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
            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
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
            MaintenanceCommands maintenanceSystem = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
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

        /// <summary>
        /// Builds a maintenance scene containing a carrier with an officer and two eligible
        /// friendly fallback planets at different distances.
        /// </summary>
        /// <returns>The carrier-scrap scene and its connected result pipeline.</returns>
        private (
            GameRoot game,
            Faction owner,
            Planet origin,
            Planet nearestDestination,
            Planet fartherDestination,
            CapitalShip carrier,
            Officer officer,
            MaintenanceCommands maintenance,
            GameResultBus resultBus
        ) BuildCarrierScrapScene()
        {
            GameRoot game = CreateGame();
            Faction owner = CreateFaction("empire", "Empire");
            game.GetFactions().Add(owner);
            PlanetSector sector = new PlanetSector { InstanceID = "scrap-sector" };
            Planet origin = CreatePlanet("scrap-origin", "Scrap Origin", owner.InstanceID);
            origin.NumRawResourceNodes = 0;
            origin.PositionX = 0;
            origin.PositionY = 0;
            Planet nearestDestination = CreatePlanet(
                "scrap-nearest",
                "Scrap Nearest",
                owner.InstanceID
            );
            nearestDestination.PositionX = 25;
            nearestDestination.PositionY = 0;
            Planet fartherDestination = CreatePlanet(
                "scrap-farther",
                "Scrap Farther",
                owner.InstanceID
            );
            fartherDestination.PositionX = 100;
            fartherDestination.PositionY = 0;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(origin, sector);
            game.AttachNode(nearestDestination, sector);
            game.AttachNode(fartherDestination, sector);

            Fleet fleet = EntityFactory.CreateFleet("scrap-fleet", owner.InstanceID);
            CapitalShip carrier = new CapitalShip
            {
                InstanceID = "scrap-carrier",
                OwnerInstanceID = owner.InstanceID,
                ManufacturingStatus = ManufacturingStatus.Complete,
                ConstructionCost = 1,
                MaintenanceCost = 1,
                MaxHullStrength = 100,
                CurrentHullStrength = 100,
            };
            Officer officer = EntityFactory.CreateOfficer("scrap-officer", owner.InstanceID);
            game.AttachNode(fleet, origin);
            game.AttachNode(carrier, fleet);
            game.AttachNode(officer, carrier);

            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            GameResultBus resultBus = new GameResultBus();
            new MovementObserver(movement).Connect(resultBus);
            MaintenanceCommands maintenance = new MaintenanceCommands(
                game,
                new FixedRNG(),
                new FleetCommands(game)
            );
            maintenance.ResultsProduced += results =>
            {
                resultBus.Publish(results);
            };

            return (
                game,
                owner,
                origin,
                nearestDestination,
                fartherDestination,
                carrier,
                officer,
                maintenance,
                resultBus
            );
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
        private GameRoot CreateGame()
        {
            return TestGame.Create(TestConfig.Create());
        }

        /// <summary>
        /// Creates faction.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="name">The name.</param>
        /// <returns>The created faction.</returns>
        private Faction CreateFaction(string id, string name)
        {
            Faction faction = new Faction { InstanceID = id, DisplayName = name };
            faction.Settings.ResourceProcessingPointsPerFacility = 50;
            return faction;
        }

        /// <summary>
        /// Creates planet.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="name">The name.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The created planet.</returns>
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

        /// <summary>
        /// Creates mine.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The created mine.</returns>
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

        /// <summary>
        /// Creates refinery.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The created refinery.</returns>
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
