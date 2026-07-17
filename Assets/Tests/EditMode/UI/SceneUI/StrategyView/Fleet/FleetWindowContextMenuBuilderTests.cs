using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Fleet
{
    [TestFixture]
    public class FleetWindowContextMenuBuilderTests
    {
        [Test]
        public void Build_EmptySelection_ReturnsDisabledInformationCommands()
        {
            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                null,
                false,
                false,
                false,
                false
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyContextMenuActions.Encyclopedia,
                    StrategyContextMenuActions.Status,
                },
                commands.Select(command => command.Action)
            );
            Assert.IsTrue(commands.All(command => !command.Enabled));
        }

        [Test]
        public void Build_SingleFleet_ReturnsFleetOperationsInAuthoredOrder()
        {
            GameFleet fleet = new GameFleet();

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { fleet },
                true,
                true,
                false,
                false
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyContextMenuActions.Move,
                    StrategyContextMenuActions.MoveConfirm,
                    StrategyContextMenuActions.PlanetaryBombardment,
                    0,
                    StrategyContextMenuActions.Rename,
                    StrategyContextMenuActions.Encyclopedia,
                    StrategyContextMenuActions.Status,
                    StrategyContextMenuActions.Scrap,
                },
                commands.Select(command => command.Action)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Move",
                    "Confirmed Move",
                    "Planetary Bombardment",
                    "Planetary Assault",
                    "Rename",
                    "Encyclopedia",
                    "Status",
                    "Scrap",
                },
                commands.Select(command => command.Text)
            );
            Assert.IsTrue(commands.All(command => command.Enabled));
        }

        [Test]
        public void Build_MultipleFleets_DisablesSingleItemCommandsAndOmitsFleetActionCommands()
        {
            GameFleet first = new GameFleet();
            GameFleet second = new GameFleet();

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { first, second },
                true,
                true,
                false,
                false
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyContextMenuActions.Move,
                    StrategyContextMenuActions.MoveConfirm,
                    StrategyContextMenuActions.Rename,
                    StrategyContextMenuActions.Encyclopedia,
                    StrategyContextMenuActions.Status,
                    StrategyContextMenuActions.Scrap,
                },
                commands.Select(command => command.Action)
            );
            CollectionAssert.AreEqual(
                new[] { true, true, false, false, false, true },
                commands.Select(command => command.Enabled)
            );
        }

        [Test]
        public void Build_SingleCapitalShip_ReturnsCreateFleetAndRenameCommands()
        {
            CapitalShip ship = new CapitalShip
            {
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { ship },
                true,
                true,
                false,
                false
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyContextMenuActions.Move,
                    StrategyContextMenuActions.MoveConfirm,
                    StrategyContextMenuActions.CreateFleet,
                    StrategyContextMenuActions.Rename,
                    StrategyContextMenuActions.Encyclopedia,
                    StrategyContextMenuActions.Status,
                    StrategyContextMenuActions.Scrap,
                },
                commands.Select(command => command.Action)
            );
            Assert.IsTrue(commands.All(command => command.Enabled));
        }

        [Test]
        public void Build_CapitalShipUnderConstruction_ReturnsStopCommand()
        {
            CapitalShip ship = new CapitalShip
            {
                ManufacturingStatus = ManufacturingStatus.Building,
            };

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { ship },
                false,
                false,
                false,
                false
            );

            Assert.AreEqual(StrategyContextMenuActions.Stop, commands.Last().Action);
            Assert.AreEqual("Stop", commands.Last().Text);
            Assert.IsFalse(commands.Last().Enabled);
        }

        [Test]
        public void Build_CompletedTransportedUnit_UsesMoveEligibilityForScrap()
        {
            Starfighter starfighter = new Starfighter
            {
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { starfighter },
                true,
                false,
                false,
                false
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyContextMenuActions.Move,
                    StrategyContextMenuActions.MoveConfirm,
                    StrategyContextMenuActions.Encyclopedia,
                    StrategyContextMenuActions.Status,
                    StrategyContextMenuActions.Scrap,
                },
                commands.Select(command => command.Action)
            );
            CollectionAssert.AreEqual(
                new[] { false, false, true, true, false },
                commands.Select(command => command.Enabled)
            );
        }

        [Test]
        public void Build_TransportedUnitUnderConstruction_UsesControlEligibilityForStop()
        {
            Regiment regiment = new Regiment { ManufacturingStatus = ManufacturingStatus.Building };

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { regiment },
                true,
                false,
                false,
                false
            );

            Assert.AreEqual(StrategyContextMenuActions.Stop, commands.Last().Action);
            Assert.IsTrue(commands.Last().Enabled);
        }

        [Test]
        public void Build_OfficerSelection_ReturnsCommandAndPersonnelOperations()
        {
            Officer officer = new Officer();

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { officer },
                true,
                true,
                true,
                true
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyContextMenuActions.Move,
                    StrategyContextMenuActions.MoveConfirm,
                    StrategyContextMenuActions.CreateMission,
                    StrategyContextMenuActions.Submenu,
                    StrategyContextMenuActions.Encyclopedia,
                    StrategyContextMenuActions.Status,
                    StrategyContextMenuActions.Retire,
                },
                commands.Select(command => command.Action)
            );
            Assert.AreEqual("Retire ", commands.Last().Text);
            Assert.IsTrue(commands.All(command => command.Enabled));
        }

        [Test]
        public void Build_SpecialForcesSelection_OmitsOfficerCommandOperation()
        {
            SpecialForces specialForces = new SpecialForces();

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { specialForces },
                true,
                true,
                false,
                false
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyContextMenuActions.Move,
                    StrategyContextMenuActions.MoveConfirm,
                    StrategyContextMenuActions.CreateMission,
                    StrategyContextMenuActions.Encyclopedia,
                    StrategyContextMenuActions.Status,
                    StrategyContextMenuActions.Retire,
                },
                commands.Select(command => command.Action)
            );
            CollectionAssert.AreEqual(
                new[] { true, true, false, true, true, false },
                commands.Select(command => command.Enabled)
            );
        }

        [Test]
        public void Build_UnsupportedSelection_ReturnsDisabledInformationCommands()
        {
            Building building = new Building();

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { building },
                true,
                true,
                true,
                true
            );

            Assert.AreEqual(2, commands.Count);
            Assert.IsTrue(commands.All(command => !command.Enabled));
        }
    }
}
