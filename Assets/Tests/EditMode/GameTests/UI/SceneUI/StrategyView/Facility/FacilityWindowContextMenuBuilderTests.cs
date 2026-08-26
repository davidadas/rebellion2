using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Facility
{
    [TestFixture]
    public class FacilityWindowContextMenuBuilderTests
    {
        [Test]
        public void Build_ManufacturingLaneWithQueuedItems_EnablesStop()
        {
            Planet planet = CreatePlanet(withBuildingQueue: true);

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Manufacturing,
                FacilityWindowTab.Construction,
                null,
                "owner"
            );

            StrategyMenuCommand stop = commands.Single(command =>
                command.Action == StrategyMenuAction.Stop
            );
            Assert.IsTrue(stop.Enabled);
        }

        [Test]
        public void Build_ManufacturingLaneWithoutQueuedItems_DisablesStop()
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Manufacturing,
                FacilityWindowTab.Construction,
                null,
                "owner"
            );

            StrategyMenuCommand stop = commands.Single(command =>
                command.Action == StrategyMenuAction.Stop
            );
            Assert.IsFalse(stop.Enabled);
        }

        [Test]
        public void Build_ManufacturingLaneOwnedByAnotherFaction_DisablesStop()
        {
            Planet planet = CreatePlanet(withBuildingQueue: true);

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Manufacturing,
                FacilityWindowTab.Construction,
                null,
                "other"
            );

            StrategyMenuCommand stop = commands.Single(command =>
                command.Action == StrategyMenuAction.Stop
            );
            Assert.IsFalse(stop.Enabled);
        }

        [Test]
        public void Build_UnreservedConstructionLane_ReturnsUncheckedReservationCommand()
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Manufacturing,
                FacilityWindowTab.Construction,
                null,
                "owner"
            );

            StrategyMenuCommand reserve = commands.Single(command =>
                command.Action == StrategyMenuAction.Reserve
            );
            Assert.IsTrue(reserve.Enabled);
            Assert.AreEqual(StrategyContextMenuIconKeys.None, reserve.IconKey);
            Assert.IsTrue(reserve.UsesIconColumn);
        }

        [Test]
        public void Build_ReservedConstructionLane_ReturnsCheckedReservationCommand()
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);
            planet.IsConstructionYardReserved = true;

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Manufacturing,
                FacilityWindowTab.Construction,
                null,
                "owner"
            );

            StrategyMenuCommand reserve = commands.Single(command =>
                command.Action == StrategyMenuAction.Reserve
            );
            Assert.AreEqual(StrategyContextMenuIconKeys.CheckMark, reserve.IconKey);
        }

        [Test]
        public void Build_NonConstructionManufacturingLane_OmitsReservationCommand()
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Manufacturing,
                FacilityWindowTab.Shipyards,
                null,
                "owner"
            );

            Assert.IsFalse(commands.Any(command => command.Action == StrategyMenuAction.Reserve));
        }

        [Test]
        public void Build_InventoryItemUnderConstruction_ReturnsEnabledStopCommand()
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Construction,
                null,
                building,
                "owner"
            );

            StrategyMenuCommand command = commands.Single(item =>
                item.Action == StrategyMenuAction.Stop
            );
            Assert.AreEqual("Stop", command.Text);
            Assert.IsTrue(command.Enabled);
        }

        [Test]
        public void Build_CompletedInventoryItem_ReturnsEnabledScrapCommand()
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Construction,
                null,
                building,
                "owner"
            );

            StrategyMenuCommand command = commands.Single(item =>
                item.Action == StrategyMenuAction.Scrap
            );
            Assert.AreEqual("Scrap", command.Text);
            Assert.IsTrue(command.Enabled);
        }

        [Test]
        public void Build_InventoryItemOwnedByAnotherFaction_DisablesDestructiveCommand()
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Construction,
                null,
                building,
                "other"
            );

            StrategyMenuCommand command = commands.Single(item =>
                item.Action == StrategyMenuAction.Scrap
            );
            Assert.IsFalse(command.Enabled);
        }

        [Test]
        public void Build_NoTarget_ReturnsNoCommands()
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Manufacturing,
                null,
                null,
                "owner"
            );

            Assert.IsEmpty(commands);
        }

        private static Planet CreatePlanet(bool withBuildingQueue)
        {
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "owner" };
            if (withBuildingQueue)
            {
                planet.ManufacturingQueue[ManufacturingType.Building] = new List<IManufacturable>
                {
                    new Building { OwnerInstanceID = "owner" },
                };
            }

            return planet;
        }
    }
}
