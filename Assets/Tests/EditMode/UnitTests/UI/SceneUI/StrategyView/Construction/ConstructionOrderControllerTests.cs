using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.DependencyInjection;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Construction
{
    [TestFixture]
    public class ConstructionOrderControllerTests
    {
        private const int _destinationEnergyCapacity = 2;

        [Test]
        public void TrySetConstructionOrder_BuildCountExceedsFacilityCount_UsesDestinationCapacity()
        {
            Building template = TestContent
                .Data.Buildings.Where(building =>
                    building.GetBuildingType()
                        is BuildingType.Mine
                            or BuildingType.Refinery
                            or BuildingType.Shipyard
                            or BuildingType.TrainingFacility
                            or BuildingType.ConstructionFacility
                )
                .First(building =>
                    ((IManufacturable)building).GetMaintenanceCost() == 0
                    && building.ManufacturingFactionInstanceIDs?.Count > 0
                );
            string ownerId = template.ManufacturingFactionInstanceIDs[0];
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction owner = new Faction { InstanceID = ownerId };
            owner.SetHighestUnlockedOrder(
                ManufacturingType.Building,
                ((IManufacturable)template).GetResearchOrder()
            );
            game.GetFactions().Add(owner);
            GalaxyPlanetSector sector = new GalaxyPlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet producer = CreatePlanet("producer", ownerId, 10);
            Planet destination = CreatePlanet("destination", ownerId, _destinationEnergyCapacity);
            game.AttachNode(producer, sector);
            game.AttachNode(destination, sector);
            game.AttachNode(CreateConstructionFacility(ownerId), producer);
            FogOfWarCommands fogOfWar = new FogOfWarCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                fogOfWar,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            ManufacturingCommands manufacturing = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game),
                movement
            );
            ConstructionOrderController controller = CreateController(game, manufacturing);

            bool started = controller.TrySetConstructionOrder(
                producer,
                destination,
                template,
                _destinationEnergyCapacity,
                ownerId
            );

            Assert.IsTrue(started);
            Assert.AreEqual(_destinationEnergyCapacity, destination.GetChildren<Building>().Count);
            Assert.AreEqual(
                _destinationEnergyCapacity,
                producer.GetManufacturingQueue()[ManufacturingType.Building].Count
            );
        }

        [Test]
        public void GetBuildSelection_ShipyardTab_ExcludesLockedTechnologies()
        {
            List<IManufacturable> templates = TestContent
                .Data.CapitalShips.Cast<IManufacturable>()
                .Concat(TestContent.Data.Starfighters)
                .Where(template => template.ManufacturingFactionInstanceIDs?.Count > 0)
                .ToList();
            IGrouping<string, IManufacturable> ownerTemplates = templates
                .SelectMany(template =>
                    template.ManufacturingFactionInstanceIDs.Select(ownerId => new
                    {
                        ownerId,
                        template,
                    })
                )
                .GroupBy(entry => entry.ownerId, entry => entry.template)
                .First(group =>
                    group.Select(template => template.GetResearchOrder()).Distinct().Count() > 1
                );
            string ownerId = ownerTemplates.Key;
            int unlockedOrder = ownerTemplates.Min(template => template.GetResearchOrder());
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction owner = new Faction { InstanceID = ownerId };
            owner.SetHighestUnlockedOrder(ManufacturingType.Ship, unlockedOrder);
            owner.RebuildResearchCatalog(templates.ToArray());
            game.GetFactions().Add(owner);
            FogOfWarCommands fogOfWar = new FogOfWarCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                fogOfWar,
                new FleetCommands(game),
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            ManufacturingCommands manufacturing = new ManufacturingCommands(
                game,
                new FleetCommands(game),
                new ManufacturingQueries(game),
                movement
            );
            ConstructionOrderController controller = CreateController(game, manufacturing);

            IReadOnlyList<IManufacturable> selection = controller.GetBuildSelection(
                FacilityWindowTab.Shipyards,
                ownerId
            );

            Assert.IsNotEmpty(selection);
            Assert.IsTrue(
                ownerTemplates.Any(template => template.GetResearchOrder() > unlockedOrder)
            );
            Assert.IsTrue(selection.All(template => template.GetResearchOrder() <= unlockedOrder));
        }

        [Test]
        public void GetBuildSelection_ConstructionTab_IncludesAllApplicableBuildings()
        {
            List<IManufacturable> templates = TestContent
                .Data.Buildings.Cast<IManufacturable>()
                .Where(template => template.ManufacturingFactionInstanceIDs?.Count > 0)
                .ToList();
            string ownerId = templates
                .SelectMany(template =>
                    template.ManufacturingFactionInstanceIDs.Select(factionId =>
                        (FactionId: factionId, Building: (Building)template)
                    )
                )
                .GroupBy(candidate => candidate.FactionId)
                .First(group =>
                    group.Any(candidate =>
                        candidate.Building.GetBuildingType() == BuildingType.Defense
                    )
                    && group.Any(candidate =>
                        candidate.Building.GetBuildingType() == BuildingType.Weapon
                    )
                )
                .Key;
            IManufacturable[] applicableTemplates = templates
                .Where(template => template.ManufacturingFactionInstanceIDs.Contains(ownerId))
                .ToArray();
            GameRoot game = TestGame.Create(TestConfig.Create());
            Faction owner = new Faction { InstanceID = ownerId };
            owner.SetHighestUnlockedOrder(
                ManufacturingType.Building,
                applicableTemplates.Max(template => template.GetResearchOrder())
            );
            owner.RebuildResearchCatalog(applicableTemplates);
            game.GetFactions().Add(owner);
            FleetCommands fleetSystem = new FleetCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                fleetSystem,
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            ConstructionOrderController controller = CreateController(
                game,
                new ManufacturingCommands(
                    game,
                    fleetSystem,
                    new ManufacturingQueries(game),
                    movement
                )
            );

            IReadOnlyList<IManufacturable> selection = controller.GetBuildSelection(
                FacilityWindowTab.Construction,
                ownerId
            );

            CollectionAssert.AreEquivalent(applicableTemplates, selection);
            Assert.IsTrue(
                selection
                    .Cast<Building>()
                    .Any(building => building.GetBuildingType() == BuildingType.Defense)
            );
            Assert.IsTrue(
                selection
                    .Cast<Building>()
                    .Any(building => building.GetBuildingType() == BuildingType.Weapon)
            );
        }

        [Test]
        public void GetBuildSelection_ConstructionTab_IncludesDestroyedBuildingAtDestination()
        {
            const string ownerId = "owner";
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = ownerId });
            GalaxyPlanetSector sector = new GalaxyPlanetSector { InstanceID = "sector" };
            Planet destination = CreatePlanet("destination", ownerId, 1);
            Building rubble = new Building
            {
                InstanceID = "destroyed-mine",
                TypeID = "mine",
                DisplayName = "Mine",
                OwnerInstanceID = ownerId,
                BuildingType = BuildingType.Mine,
                IsDestroyed = true,
                ManufacturingStatus = ManufacturingStatus.Complete,
                ManufacturingFactionInstanceIDs = new List<string> { "other" },
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(destination, sector);
            game.AttachNode(rubble, destination);
            FleetCommands fleetSystem = new FleetCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                fleetSystem,
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            ConstructionOrderController controller = CreateController(
                game,
                new ManufacturingCommands(
                    game,
                    fleetSystem,
                    new ManufacturingQueries(game),
                    movement
                )
            );

            IReadOnlyList<IManufacturable> selection = controller.GetBuildSelection(
                FacilityWindowTab.Construction,
                ownerId,
                destination
            );

            CollectionAssert.Contains(selection, rubble);
        }

        [Test]
        public void GetBuildEstimates_StationaryTemplate_ReturnsCompletionWithoutDeployment()
        {
            const string ownerId = "owner";
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = ownerId });
            GalaxyPlanetSector sector = new GalaxyPlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet producer = CreatePlanet("producer", ownerId, 10);
            game.AttachNode(producer, sector);
            game.AttachNode(CreateConstructionFacility(ownerId), producer);
            FleetCommands fleetSystem = new FleetCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                fleetSystem,
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            ConstructionOrderController controller = CreateController(
                game,
                new ManufacturingCommands(
                    game,
                    fleetSystem,
                    new ManufacturingQueries(game),
                    movement
                )
            );
            Building template = new Building
            {
                InstanceID = "template",
                ConstructionCost = 10,
                BuildingType = BuildingType.Mine,
            };

            ConstructionBuildEstimate estimate = controller
                .GetBuildEstimates(
                    producer,
                    producer,
                    new IManufacturable[] { template },
                    1,
                    new[] { 0 }
                )
                .Single();

            Assert.AreEqual(10, estimate.CompletionTicks);
            Assert.IsNull(estimate.DeploymentTicks);
        }

        [Test]
        public void GetBuildSelection_UpgradeableBuilding_IncludesUpgradeOperation()
        {
            var scenario = CreateUpgradeScenario();

            IReadOnlyList<IManufacturable> selection = scenario.Controller.GetBuildSelection(
                FacilityWindowTab.Construction,
                scenario.Source.OwnerInstanceID,
                scenario.Destination,
                scenario.Producer
            );

            BuildingUpgradeSelection upgrade = selection
                .OfType<BuildingUpgradeSelection>()
                .Single();
            Assert.AreSame(scenario.Source, upgrade.Source);
            Assert.AreSame(scenario.Target, upgrade.Target);
        }

        [Test]
        public void TrySetConstructionOrder_UpgradeOperation_QueuesSourceInPlace()
        {
            var scenario = CreateUpgradeScenario();
            BuildingUpgradeSelection upgrade = scenario
                .Controller.GetBuildSelection(
                    FacilityWindowTab.Construction,
                    scenario.Source.OwnerInstanceID,
                    scenario.Destination,
                    scenario.Producer
                )
                .OfType<BuildingUpgradeSelection>()
                .Single();

            bool started = scenario.Controller.TrySetConstructionOrder(
                scenario.Producer,
                scenario.Destination,
                upgrade,
                1,
                scenario.Source.OwnerInstanceID
            );

            Assert.IsTrue(started);
            Assert.AreSame(scenario.Destination, scenario.Source.GetParent());
            Assert.AreSame(
                scenario.Source,
                scenario.Producer.GetManufacturingQueue()[ManufacturingType.Building].Single()
            );
        }

        [Test]
        public void TrySetConstructionOrder_TwoMatchingBuildings_QueuesBothSourcesInPlace()
        {
            var scenario = CreateUpgradeScenario();
            Building secondSource = new Building
            {
                InstanceID = "basic-shipyard-instance-2",
                TypeID = scenario.Source.TypeID,
                DisplayName = scenario.Source.DisplayName,
                OwnerInstanceID = scenario.Source.OwnerInstanceID,
                BuildingType = scenario.Source.BuildingType,
                ProductionType = scenario.Source.ProductionType,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Upgrades = { scenario.Target.TypeID },
            };
            scenario.Game.AttachNode(secondSource, scenario.Destination);
            BuildingUpgradeSelection upgrade = scenario
                .Controller.GetBuildSelection(
                    FacilityWindowTab.Construction,
                    scenario.Source.OwnerInstanceID,
                    scenario.Destination,
                    scenario.Producer
                )
                .OfType<BuildingUpgradeSelection>()
                .Single();

            bool started = scenario.Controller.TrySetConstructionOrder(
                scenario.Producer,
                scenario.Destination,
                upgrade,
                2,
                scenario.Source.OwnerInstanceID
            );

            Assert.IsTrue(started);
            CollectionAssert.AreEquivalent(
                new[] { scenario.Source, secondSource },
                scenario.Producer.GetManufacturingQueue()[ManufacturingType.Building]
            );
            Assert.IsTrue(scenario.Source.IsUpgrading());
            Assert.IsTrue(secondSource.IsUpgrading());
        }

        /// <summary>
        /// Creates a construction controller with the active test services.
        /// </summary>
        /// <param name="game">The game graph under test.</param>
        /// <param name="manufacturing">The manufacturing commands under test.</param>
        /// <returns>A construction controller using those services.</returns>
        private static ConstructionOrderController CreateController(
            GameRoot game,
            ManufacturingCommands manufacturing
        )
        {
            ServiceContainer container = new ServiceContainer();
            container.AddSingletonInstance(game);
            container.AddSingletonInstance(manufacturing);
            container.AddSingletonInstance(new MovementQueries(game));
            container.AddSingletonInstance(new ManufacturingQueries(game));
            return new ConstructionOrderController(container.BuildServiceLocator());
        }

        /// <summary>
        /// Creates an unlocked remote building-upgrade scenario.
        /// </summary>
        /// <returns>The controller, planets, source, and target for the scenario.</returns>
        private static (
            GameRoot Game,
            ConstructionOrderController Controller,
            Planet Producer,
            Planet Destination,
            Building Source,
            Building Target
        ) CreateUpgradeScenario()
        {
            const string ownerId = "owner";
            GameRoot game = TestGame.Create(TestConfig.Create());
            Building target = new Building
            {
                TypeID = "advanced-shipyard",
                DisplayName = "Advanced shipyard",
                BuildingType = BuildingType.Shipyard,
                ProductionType = ManufacturingType.Ship,
                ConstructionCost = 10,
                MaintenanceCost = 0,
                ManufacturingFactionInstanceIDs = new List<string> { ownerId },
            };
            Faction owner = new Faction { InstanceID = ownerId };
            owner.RebuildResearchCatalog(new IManufacturable[] { target });
            owner.SetHighestUnlockedOrder(ManufacturingType.Building, target.ResearchOrder);
            game.GetFactions().Add(owner);
            GalaxyPlanetSector sector = new GalaxyPlanetSector { InstanceID = "sector" };
            Planet producer = CreatePlanet("producer", ownerId, 10);
            Planet destination = CreatePlanet("destination", ownerId, 10);
            Building source = new Building
            {
                InstanceID = "basic-shipyard-instance",
                TypeID = "basic-shipyard",
                DisplayName = "Basic shipyard",
                OwnerInstanceID = ownerId,
                BuildingType = BuildingType.Shipyard,
                ProductionType = ManufacturingType.Ship,
                ManufacturingStatus = ManufacturingStatus.Complete,
                Upgrades = { target.TypeID },
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(producer, sector);
            game.AttachNode(destination, sector);
            game.AttachNode(CreateConstructionFacility(ownerId), producer);
            game.AttachNode(source, destination);
            FleetCommands fleetSystem = new FleetCommands(game);
            MovementCommands movement = new MovementCommands(
                game,
                new FogOfWarCommands(game),
                fleetSystem,
                new FogOfWarQueries(game),
                new MovementQueries(game)
            );
            ManufacturingCommands manufacturing = new ManufacturingCommands(
                game,
                fleetSystem,
                new ManufacturingQueries(game),
                movement
            );
            return (
                game,
                CreateController(game, manufacturing),
                producer,
                destination,
                source,
                target
            );
        }

        /// <summary>
        /// Creates planet.
        /// </summary>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerId">The owner id.</param>
        /// <param name="energyCapacity">The energy capacity.</param>
        /// <returns>The created planet.</returns>
        private static Planet CreatePlanet(string instanceId, string ownerId, int energyCapacity)
        {
            return new Planet
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerId,
                IsColonized = true,
                EnergyCapacity = energyCapacity,
                NumRawResourceNodes = 10,
            };
        }

        /// <summary>
        /// Creates construction facility.
        /// </summary>
        /// <param name="ownerId">The owner id.</param>
        /// <returns>The created construction facility.</returns>
        private static Building CreateConstructionFacility(string ownerId)
        {
            return new Building
            {
                InstanceID = "construction-facility",
                OwnerInstanceID = ownerId,
                BuildingType = BuildingType.ConstructionFacility,
                ProductionType = ManufacturingType.Building,
                ProcessRate = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
        }
    }
}
