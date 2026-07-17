using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSystem
{
    [TestFixture]
    public class PlanetSystemWindowContextMenuBuilderTests
    {
        private const string _opposingFactionId = "FNEMP1";
        private const string _playerFactionId = "FNALL1";

        [Test]
        public void Create_MissingHit_ReturnsDisabledPlanetInformationCommands()
        {
            List<StrategyMenuCommand> commands = PlanetSystemWindowContextMenuBuilder.Create(
                null,
                null,
                _playerFactionId
            );

            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual(StrategyContextMenuActions.Encyclopedia, commands[0].Action);
            Assert.AreEqual("Encyclopedia", commands[0].Text);
            Assert.IsFalse(commands[0].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.Status, commands[1].Action);
            Assert.AreEqual("Status", commands[1].Text);
            Assert.IsFalse(commands[1].Enabled);
        }

        [TestCase(PlanetIcon.None, false)]
        [TestCase(PlanetIcon.Facility, false)]
        [TestCase(PlanetIcon.Defense, false)]
        [TestCase(PlanetIcon.Fleet, true)]
        public void Create_PlanetInformationHit_ReturnsEnabledPlanetInformationCommands(
            PlanetIcon icon,
            bool planetImage
        )
        {
            PlanetSystemWindowHit hit = CreateHit(icon, planetImage);

            List<StrategyMenuCommand> commands = PlanetSystemWindowContextMenuBuilder.Create(
                hit,
                null,
                _playerFactionId
            );

            Assert.AreEqual(2, commands.Count);
            Assert.AreEqual(StrategyContextMenuActions.Encyclopedia, commands[0].Action);
            Assert.IsTrue(commands[0].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.Status, commands[1].Action);
            Assert.IsTrue(commands[1].Enabled);
        }

        [Test]
        public void Create_MissionHit_ReturnsDisabledMissionCommands()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Mission, false);

            List<StrategyMenuCommand> commands = PlanetSystemWindowContextMenuBuilder.Create(
                hit,
                null,
                _playerFactionId
            );

            Assert.AreEqual(3, commands.Count);
            Assert.AreEqual(StrategyContextMenuActions.Encyclopedia, commands[0].Action);
            Assert.AreEqual("Encyclopedia", commands[0].Text);
            Assert.IsFalse(commands[0].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.Status, commands[1].Action);
            Assert.AreEqual("Status", commands[1].Text);
            Assert.IsFalse(commands[1].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.Abort, commands[2].Action);
            Assert.AreEqual("Abort", commands[2].Text);
            Assert.IsFalse(commands[2].Enabled);
        }

        [Test]
        public void Create_EmptyFleetHit_ReturnsDisabledFleetCommands()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Fleet, false);

            List<StrategyMenuCommand> commands = PlanetSystemWindowContextMenuBuilder.Create(
                hit,
                null,
                _playerFactionId
            );

            Assert.AreEqual(7, commands.Count);
            Assert.AreEqual(StrategyContextMenuActions.Move, commands[0].Action);
            Assert.IsFalse(commands[0].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.MoveConfirm, commands[1].Action);
            Assert.IsFalse(commands[1].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.PlanetaryBombardment, commands[2].Action);
            Assert.IsFalse(commands[2].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.DestroySystem, commands[3].Action);
            Assert.IsFalse(commands[3].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.Encyclopedia, commands[4].Action);
            Assert.IsFalse(commands[4].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.Status, commands[5].Action);
            Assert.IsFalse(commands[5].Enabled);
            Assert.AreEqual(StrategyContextMenuActions.Scrap, commands[6].Action);
            Assert.IsFalse(commands[6].Enabled);
        }

        [Test]
        public void Create_PlayerFleetHit_ReturnsEnabledFleetCommandsAndStatus()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            List<ISceneNode> fleets = new List<ISceneNode>
            {
                new GameFleet(_playerFactionId, "Fleet"),
            };

            List<StrategyMenuCommand> commands = PlanetSystemWindowContextMenuBuilder.Create(
                hit,
                fleets,
                _playerFactionId
            );

            Assert.IsTrue(commands[0].Enabled);
            Assert.IsTrue(commands[1].Enabled);
            Assert.IsTrue(commands[2].Enabled);
            Assert.IsFalse(commands[3].Enabled);
            Assert.IsFalse(commands[4].Enabled);
            Assert.IsTrue(commands[5].Enabled);
            Assert.IsTrue(commands[6].Enabled);
        }

        [Test]
        public void Create_OpposingFleetHit_ReturnsDisabledCommandsAndEnabledStatus()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            List<ISceneNode> fleets = new List<ISceneNode>
            {
                new GameFleet(_opposingFactionId, "Fleet"),
            };

            List<StrategyMenuCommand> commands = PlanetSystemWindowContextMenuBuilder.Create(
                hit,
                fleets,
                _playerFactionId
            );

            Assert.IsFalse(commands[0].Enabled);
            Assert.IsFalse(commands[1].Enabled);
            Assert.IsFalse(commands[2].Enabled);
            Assert.IsTrue(commands[5].Enabled);
            Assert.IsFalse(commands[6].Enabled);
        }

        [Test]
        public void Create_MultiplePlayerFleets_ReturnsCommandsWithoutSingleFleetStatus()
        {
            PlanetSystemWindowHit hit = CreateHit(PlanetIcon.Fleet, false);
            List<ISceneNode> fleets = new List<ISceneNode>
            {
                new GameFleet(_playerFactionId, "First Fleet"),
                new GameFleet(_playerFactionId, "Second Fleet"),
            };

            List<StrategyMenuCommand> commands = PlanetSystemWindowContextMenuBuilder.Create(
                hit,
                fleets,
                _playerFactionId
            );

            Assert.IsTrue(commands[0].Enabled);
            Assert.IsTrue(commands[1].Enabled);
            Assert.IsTrue(commands[2].Enabled);
            Assert.IsFalse(commands[5].Enabled);
            Assert.IsTrue(commands[6].Enabled);
        }

        private static PlanetSystemWindowHit CreateHit(PlanetIcon icon, bool planetImage)
        {
            GamePlanetSystem system = new GamePlanetSystem();
            GalaxyMapPlanet planet = new GalaxyMapPlanet(system, new Planet(), string.Empty);
            return new PlanetSystemWindowHit(planet, icon, planetImage);
        }
    }
}
