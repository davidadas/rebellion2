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
                new[] { StrategyMenuAction.Encyclopedia, StrategyMenuAction.Status },
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
                false,
                true,
                false,
                true
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyMenuAction.Move,
                    StrategyMenuAction.MoveConfirm,
                    StrategyMenuAction.PlanetaryBombardment,
                    StrategyMenuAction.PlanetaryAssault,
                    StrategyMenuAction.Rename,
                    StrategyMenuAction.Encyclopedia,
                    StrategyMenuAction.Status,
                    StrategyMenuAction.Scrap,
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
            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyMenuAction.BombardMilitaryFacilities,
                    StrategyMenuAction.BombardCivilianFacilities,
                    StrategyMenuAction.GeneralBombardment,
                    StrategyMenuAction.DestroySystem,
                },
                commands[2].SubmenuCommands.Select(command => command.Action)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Target Military Facilities",
                    "Target Civilian Facilities",
                    "General Bombardment",
                    "Destroy System",
                },
                commands[2].SubmenuCommands.Select(command => command.Text)
            );
            CollectionAssert.AreEqual(
                new[] { true, true, true, false },
                commands[2].SubmenuCommands.Select(command => command.Enabled)
            );
        }

        [Test]
        public void Build_MultipleFleets_OffersFleetCommandsAndDisablesSingleItemCommands()
        {
            GameFleet first = new GameFleet();
            GameFleet second = new GameFleet();

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { first, second },
                true,
                true,
                false,
                false,
                true,
                false,
                true
            );

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyMenuAction.Move,
                    StrategyMenuAction.MoveConfirm,
                    StrategyMenuAction.PlanetaryBombardment,
                    StrategyMenuAction.PlanetaryAssault,
                    StrategyMenuAction.Rename,
                    StrategyMenuAction.Encyclopedia,
                    StrategyMenuAction.Status,
                    StrategyMenuAction.Scrap,
                },
                commands.Select(command => command.Action)
            );
            CollectionAssert.AreEqual(
                new[] { true, true, true, true, false, false, false, true },
                commands.Select(command => command.Enabled)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyMenuAction.BombardMilitaryFacilities,
                    StrategyMenuAction.BombardCivilianFacilities,
                    StrategyMenuAction.GeneralBombardment,
                    StrategyMenuAction.DestroySystem,
                },
                commands[2].SubmenuCommands.Select(command => command.Action)
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
                    StrategyMenuAction.Move,
                    StrategyMenuAction.MoveConfirm,
                    StrategyMenuAction.CreateFleet,
                    StrategyMenuAction.Rename,
                    StrategyMenuAction.Encyclopedia,
                    StrategyMenuAction.Status,
                    StrategyMenuAction.Scrap,
                },
                commands.Select(command => command.Action)
            );
            Assert.IsTrue(commands.All(command => command.Enabled));
        }

        [Test]
        public void Build_CapitalShipUnderConstruction_AllowsDeliveryReassignment()
        {
            CapitalShip ship = new CapitalShip
            {
                ManufacturingStatus = ManufacturingStatus.Building,
            };

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { ship },
                true,
                true,
                false,
                false
            );

            Assert.IsTrue(commands[0].Enabled);
            Assert.IsTrue(commands[1].Enabled);
            Assert.IsTrue(commands[2].Enabled);
            Assert.AreEqual(StrategyMenuAction.Stop, commands.Last().Action);
            Assert.AreEqual("Stop", commands.Last().Text);
            Assert.IsTrue(commands.Last().Enabled);
        }

        [Test]
        public void Build_CapitalShipWithoutMoveEligibility_DisablesMovementCommands()
        {
            CapitalShip ship = new CapitalShip
            {
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            List<StrategyMenuCommand> commands = FleetWindowContextMenuBuilder.Build(
                new ISceneNode[] { ship },
                true,
                false,
                false,
                false
            );

            Assert.IsFalse(commands[0].Enabled);
            Assert.IsFalse(commands[1].Enabled);
            Assert.IsFalse(commands[2].Enabled);
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
                    StrategyMenuAction.Move,
                    StrategyMenuAction.MoveConfirm,
                    StrategyMenuAction.Encyclopedia,
                    StrategyMenuAction.Status,
                    StrategyMenuAction.Scrap,
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

            Assert.AreEqual(StrategyMenuAction.Stop, commands.Last().Action);
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
                    StrategyMenuAction.Move,
                    StrategyMenuAction.MoveConfirm,
                    StrategyMenuAction.CreateMission,
                    StrategyMenuAction.None,
                    StrategyMenuAction.Encyclopedia,
                    StrategyMenuAction.Status,
                    StrategyMenuAction.Retire,
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
                    StrategyMenuAction.Move,
                    StrategyMenuAction.MoveConfirm,
                    StrategyMenuAction.CreateMission,
                    StrategyMenuAction.Encyclopedia,
                    StrategyMenuAction.Status,
                    StrategyMenuAction.Retire,
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
