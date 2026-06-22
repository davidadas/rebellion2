using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.StrategyView
{
    [TestFixture]
    public sealed class ConstructionWindowControllerTests
    {
        private const int _buildingBuildPanel = 3;
        private const int _destinationEnergyCapacity = 2;

        [Test]
        public void TryStartConstruction_BuildingCountUsesDestinationCapacityBeyondIdleProducerSlots()
        {
            Building template = ResourceManager
                .GetGameData<Building>()
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
            game.Factions.Add(new Faction { InstanceID = ownerId });

            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            game.AttachNode(system, game.GetGalaxyMap());

            Planet producer = CreatePlanet("producer", ownerId);
            Planet destination = CreatePlanet(
                "destination",
                ownerId,
                energyCapacity: _destinationEnergyCapacity
            );
            game.AttachNode(producer, system);
            game.AttachNode(destination, system);
            game.AttachNode(CreateConstructionFacility(ownerId), producer);

            GameManager manager = new GameManager(game);
            ConstructionWindowController controller = new ConstructionWindowController(manager);
            int buildSelection = controller
                .GetBuildSelection(_buildingBuildPanel, ownerId)
                .FindIndex(item => item.TypeID == template.TypeID);
            Assert.GreaterOrEqual(buildSelection, 0);

            GameObject windowObject = new GameObject("construction-window");
            try
            {
                UIWindow window = windowObject.AddComponent<UIWindow>();
                ConstructionWindowView view = windowObject.AddComponent<ConstructionWindowView>();
                view.InitializeWindow(
                    new GalaxyMapPlanet(system, producer, string.Empty),
                    null,
                    _buildingBuildPanel,
                    destination.InstanceID,
                    null
                );
                window.SetContent(view);

                bool started = controller.TryStartConstruction(
                    window,
                    _buildingBuildPanel,
                    buildSelection,
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
            finally
            {
                Object.DestroyImmediate(windowObject);
            }
        }

        private static Planet CreatePlanet(
            string instanceId,
            string ownerId,
            int energyCapacity = 10
        )
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
