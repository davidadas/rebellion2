using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Construction
{
    [TestFixture]
    public class ConstructionOrderControllerTests
    {
        private const int _destinationEnergyCapacity = 2;

        [Test]
        public void TryStartConstruction_BuildCountExceedsFacilityCount_UsesDestinationCapacity()
        {
            Building template = ResourceManager
                .GetEntityData<Building>()
                .Where(building =>
                    building.GetBuildingType()
                        is BuildingType.Mine
                            or BuildingType.Refinery
                            or BuildingType.Shipyard
                            or BuildingType.TrainingFacility
                            or BuildingType.ConstructionFacility
                )
                .First(building =>
                    ((IManufacturable)building).GetMaintenanceCost() == 0
                    && building.AllowedOwnerInstanceIDs?.Count > 0
                );
            string ownerId = template.AllowedOwnerInstanceIDs[0];
            GameRoot game = new GameRoot(TestConfig.Create());
            Faction owner = new Faction { InstanceID = ownerId };
            owner.SetHighestUnlockedOrder(
                ManufacturingType.Building,
                ((IManufacturable)template).GetResearchOrder()
            );
            game.Factions.Add(owner);
            GamePlanetSystem system = new GamePlanetSystem { InstanceID = "system" };
            game.AttachNode(system, game.GetGalaxyMap());
            Planet producer = CreatePlanet("producer", ownerId, 10);
            Planet destination = CreatePlanet("destination", ownerId, _destinationEnergyCapacity);
            game.AttachNode(producer, system);
            game.AttachNode(destination, system);
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
            Assert.AreEqual(_destinationEnergyCapacity, destination.Buildings.Count);
            Assert.AreEqual(
                _destinationEnergyCapacity,
                producer.GetManufacturingQueue()[ManufacturingType.Building].Count
            );
        }

        [Test]
        public void GetBuildSelection_ShipyardTab_ExcludesLockedTechnologies()
        {
            List<IManufacturable> templates = ResourceManager
                .GetEntityData<CapitalShip>()
                .Cast<IManufacturable>()
                .Concat(ResourceManager.GetEntityData<Starfighter>())
                .Where(template => template.AllowedOwnerInstanceIDs?.Count > 0)
                .ToList();
            IGrouping<string, IManufacturable> ownerTemplates = templates
                .SelectMany(template =>
                    template.AllowedOwnerInstanceIDs.Select(ownerId => new { ownerId, template })
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
            game.Factions.Add(owner);
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
