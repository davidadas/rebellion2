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
        /// <summary>
        /// Verifies build manufacturing lane with queued items enables stop.
        /// </summary>
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

        /// <summary>
        /// Verifies build manufacturing lane without queued items disables stop.
        /// </summary>
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

        /// <summary>
        /// Verifies build manufacturing lane owned by another faction disables stop.
        /// </summary>
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

        /// <summary>
        /// Verifies build unreserved manufacturing lane returns unchecked reservation command.
        /// </summary>
        /// <param name="manufacturingTab">The manufacturing tab.</param>
        [TestCase(FacilityWindowTab.Shipyards)]
        [TestCase(FacilityWindowTab.Training)]
        [TestCase(FacilityWindowTab.Construction)]
        public void Build_UnreservedManufacturingLane_ReturnsUncheckedReservationCommand(
            FacilityWindowTab manufacturingTab
        )
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Manufacturing,
                manufacturingTab,
                null,
                "owner"
            );

            StrategyMenuCommand reserve = commands.Single(command =>
                command.Action == StrategyMenuAction.Reserve
            );
            Assert.IsTrue(reserve.Enabled);
            Assert.AreEqual(StrategyContextMenuIconKeys.None, reserve.IconKey);
            Assert.IsFalse(reserve.UsesIconColumn);
        }

        /// <summary>
        /// Verifies build reserved manufacturing lane returns checked reservation command.
        /// </summary>
        /// <param name="manufacturingTab">The manufacturing tab.</param>
        /// <param name="manufacturingType">The manufacturing type.</param>
        [TestCase(FacilityWindowTab.Shipyards, ManufacturingType.Ship)]
        [TestCase(FacilityWindowTab.Training, ManufacturingType.Troop)]
        [TestCase(FacilityWindowTab.Construction, ManufacturingType.Building)]
        public void Build_ReservedManufacturingLane_ReturnsCheckedReservationCommand(
            FacilityWindowTab manufacturingTab,
            ManufacturingType manufacturingType
        )
        {
            Planet planet = CreatePlanet(withBuildingQueue: false);
            planet.SetManufacturingReserved(manufacturingType, true);

            List<StrategyMenuCommand> commands = FacilityWindowContextMenuBuilder.Build(
                planet,
                FacilityWindowTab.Manufacturing,
                manufacturingTab,
                null,
                "owner"
            );

            StrategyMenuCommand reserve = commands.Single(command =>
                command.Action == StrategyMenuAction.Reserve
            );
            Assert.AreEqual(StrategyContextMenuIconKeys.CheckMark, reserve.IconKey);
            Assert.IsTrue(reserve.UsesIconColumn);
        }

        /// <summary>
        /// Verifies build inventory item under construction returns enabled stop command.
        /// </summary>
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

        /// <summary>
        /// Verifies build completed inventory item returns enabled scrap command.
        /// </summary>
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

        /// <summary>
        /// Verifies build inventory item owned by another faction disables destructive command.
        /// </summary>
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

        /// <summary>
        /// Verifies build no target returns no commands.
        /// </summary>
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

        /// <summary>
        /// Creates planet.
        /// </summary>
        /// <param name="withBuildingQueue">Whether with building queue.</param>
        /// <returns>The created planet.</returns>
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
