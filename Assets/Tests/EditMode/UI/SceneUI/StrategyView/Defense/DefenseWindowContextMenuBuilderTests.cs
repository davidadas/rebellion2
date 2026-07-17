using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Defense
{
    [TestFixture]
    public class DefenseWindowContextMenuBuilderTests
    {
        [Test]
        public void Build_EmptySelection_ReturnsDisabledInspectionCommands()
        {
            List<StrategyMenuCommand> commands = DefenseWindowContextMenuBuilder.Build(
                new ISceneNode[0],
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
        public void Build_PersonnelSelection_ReturnsPersonnelCommandOrderAndEligibility()
        {
            Officer officer = new Officer();

            List<StrategyMenuCommand> commands = DefenseWindowContextMenuBuilder.Build(
                new ISceneNode[] { officer },
                officer,
                true,
                true,
                false,
                true
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
                new[] { true, true, false, true, true, true },
                commands.Select(command => command.Enabled)
            );
        }

        [Test]
        public void Build_RegimentUnderConstruction_ReturnsEnabledStopCommand()
        {
            Regiment regiment = new Regiment { ManufacturingStatus = ManufacturingStatus.Building };

            List<StrategyMenuCommand> commands = DefenseWindowContextMenuBuilder.Build(
                new ISceneNode[] { regiment },
                regiment,
                false,
                true,
                false,
                false
            );

            Assert.AreEqual(StrategyContextMenuActions.Stop, commands.Last().Action);
            Assert.AreEqual("Stop", commands.Last().Text);
            Assert.IsTrue(commands.Last().Enabled);
        }

        [Test]
        public void Build_CompletedStarfighter_ReturnsScrapUsingMoveEligibility()
        {
            Starfighter starfighter = new Starfighter
            {
                ManufacturingStatus = ManufacturingStatus.Complete,
            };

            List<StrategyMenuCommand> commands = DefenseWindowContextMenuBuilder.Build(
                new ISceneNode[] { starfighter },
                starfighter,
                false,
                true,
                false,
                false
            );

            Assert.AreEqual(StrategyContextMenuActions.Scrap, commands.Last().Action);
            Assert.AreEqual("Scrap", commands.Last().Text);
            Assert.IsFalse(commands.Last().Enabled);
        }

        [Test]
        public void Build_DefenseBuildingUnderConstruction_ReturnsStopUsingControlEligibility()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Building };

            List<StrategyMenuCommand> commands = DefenseWindowContextMenuBuilder.Build(
                new ISceneNode[] { building },
                building,
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
                    StrategyContextMenuActions.Stop,
                },
                commands.Select(command => command.Action)
            );
            Assert.IsFalse(commands.Last().Enabled);
        }

        [Test]
        public void Build_CompletedDefenseBuilding_ReturnsScrapUsingControlEligibility()
        {
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            List<StrategyMenuCommand> commands = DefenseWindowContextMenuBuilder.Build(
                new ISceneNode[] { building },
                building,
                false,
                true,
                false,
                false
            );

            Assert.AreEqual(StrategyContextMenuActions.Scrap, commands.Last().Action);
            Assert.AreEqual("Scrap", commands.Last().Text);
            Assert.IsTrue(commands.Last().Enabled);
        }
    }
}
