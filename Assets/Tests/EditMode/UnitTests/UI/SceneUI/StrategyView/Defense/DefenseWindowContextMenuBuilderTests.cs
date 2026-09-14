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
        /// <summary>
        /// Verifies build empty selection returns disabled inspection commands.
        /// </summary>
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
                new[] { StrategyMenuAction.Encyclopedia, StrategyMenuAction.Status },
                commands.Select(command => command.Action)
            );
            Assert.IsTrue(commands.All(command => !command.Enabled));
        }

        /// <summary>
        /// Verifies build personnel selection returns personnel command order and eligibility.
        /// </summary>
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
                new[] { true, true, false, true, true, true },
                commands.Select(command => command.Enabled)
            );
        }

        /// <summary>
        /// Verifies build regiment under construction returns enabled stop command.
        /// </summary>
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

            Assert.AreEqual(StrategyMenuAction.Stop, commands.Last().Action);
            Assert.AreEqual("Stop", commands.Last().Text);
            Assert.IsTrue(commands.Last().Enabled);
        }

        /// <summary>
        /// Verifies build completed starfighter returns scrap using move eligibility.
        /// </summary>
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

            Assert.AreEqual(StrategyMenuAction.Scrap, commands.Last().Action);
            Assert.AreEqual("Scrap", commands.Last().Text);
            Assert.IsFalse(commands.Last().Enabled);
        }

        /// <summary>
        /// Verifies build defense building under construction returns stop using control eligibility.
        /// </summary>
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
                    StrategyMenuAction.Encyclopedia,
                    StrategyMenuAction.Status,
                    StrategyMenuAction.Stop,
                },
                commands.Select(command => command.Action)
            );
            Assert.IsFalse(commands.Last().Enabled);
        }

        /// <summary>
        /// Verifies build completed defense building returns scrap using control eligibility.
        /// </summary>
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

            Assert.AreEqual(StrategyMenuAction.Scrap, commands.Last().Action);
            Assert.AreEqual("Scrap", commands.Last().Text);
            Assert.IsTrue(commands.Last().Enabled);
        }
    }
}
