using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Construction
{
    [TestFixture]
    public class ConstructionOrderControllerTests
    {
        private const int _destinationEnergyCapacity = 2;

        [Test]
        public void TryStartConstruction_BuildCountExceedsFacilityCount_UsesDestinationCapacity()
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
            GameRoot game = new GameRoot(TestConfig.Create());
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
            FogOfWarSystem fogOfWar = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fogOfWar, new FleetSystem(game));
            ManufacturingSystem manufacturing = new ManufacturingSystem(
                game,
                new FleetSystem(game),
                movement
            );
            ConstructionOrderController controller = new ConstructionOrderController(
                () => game,
                () => manufacturing,
                () => movement
            );

            bool started = controller.TryStartConstruction(
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
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction owner = new Faction { InstanceID = ownerId };
            owner.SetHighestUnlockedOrder(ManufacturingType.Ship, unlockedOrder);
            owner.RebuildResearchCatalog(templates.ToArray());
            game.GetFactions().Add(owner);
            FogOfWarSystem fogOfWar = new FogOfWarSystem(game);
            MovementSystem movement = new MovementSystem(game, fogOfWar, new FleetSystem(game));
            ManufacturingSystem manufacturing = new ManufacturingSystem(
                game,
                new FleetSystem(game),
                movement
            );
            ConstructionOrderController controller = new ConstructionOrderController(
                () => game,
                () => manufacturing,
                () => movement
            );

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
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction owner = new Faction { InstanceID = ownerId };
            owner.SetHighestUnlockedOrder(
                ManufacturingType.Building,
                applicableTemplates.Max(template => template.GetResearchOrder())
            );
            owner.RebuildResearchCatalog(applicableTemplates);
            game.GetFactions().Add(owner);
            FleetSystem fleetSystem = new FleetSystem(game);
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                fleetSystem
            );
            ConstructionOrderController controller = new ConstructionOrderController(
                () => game,
                () => new ManufacturingSystem(game, fleetSystem, movement),
                () => movement
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
        public void GetBuildEstimates_StationaryTemplate_ReturnsCompletionWithoutDeployment()
        {
            const string ownerId = "owner";
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = ownerId });
            GalaxyPlanetSector sector = new GalaxyPlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.GetGalaxyMap());
            Planet producer = CreatePlanet("producer", ownerId, 10);
            game.AttachNode(producer, sector);
            game.AttachNode(CreateConstructionFacility(ownerId), producer);
            FleetSystem fleetSystem = new FleetSystem(game);
            MovementSystem movement = new MovementSystem(
                game,
                new FogOfWarSystem(game),
                fleetSystem
            );
            ConstructionOrderController controller = new ConstructionOrderController(
                () => game,
                () => new ManufacturingSystem(game, fleetSystem, movement),
                () => movement
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
