using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class StrategyHudControllerTests
    {
        private TestActions _actions;
        private StrategyHudController _controller;

        [SetUp]
        public void SetUp()
        {
            _actions = new TestActions();
            _controller = new StrategyHudController(
                () => new Faction(),
                () => new FactionTheme(),
                _ => null,
                _ => { }
            );
            _controller.Initialize(_actions);
        }

        [Test]
        public void Constructor_NullDependencies_ThrowArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyHudController(null, () => null, _ => null, _ => { })
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyHudController(() => null, null, _ => null, _ => { })
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyHudController(() => null, () => null, null, _ => { })
            );
            Assert.Throws<ArgumentNullException>(() =>
                new StrategyHudController(() => null, () => null, _ => null, null)
            );
        }

        [TestCase(TickSpeed.Paused, 0)]
        [TestCase(TickSpeed.VerySlow, 1)]
        [TestCase(TickSpeed.Slow, 2)]
        [TestCase(TickSpeed.Medium, 3)]
        [TestCase(TickSpeed.Fast, 4)]
        public void GetSourceSpeed_GameSpeed_ReturnsSourceSpeed(TickSpeed speed, int expected)
        {
            Assert.AreEqual(expected, StrategyHudController.GetSourceSpeed(speed));
        }

        [Test]
        public void GetSpeedIndicatorPath_ConfiguredTheme_ReturnsMappedArtwork()
        {
            SpeedIndicatorTheme theme = new SpeedIndicatorTheme
            {
                PausedImagePath = "paused",
                VerySlowImagePath = "very-slow",
                SlowImagePath = "slow",
                MediumImagePath = "medium",
                FastImagePath = "fast",
            };

            Assert.AreEqual(
                "paused",
                StrategyHudController.GetSpeedIndicatorPath(theme, TickSpeed.Paused)
            );
            Assert.AreEqual(
                "very-slow",
                StrategyHudController.GetSpeedIndicatorPath(theme, TickSpeed.VerySlow)
            );
            Assert.AreEqual(
                "slow",
                StrategyHudController.GetSpeedIndicatorPath(theme, TickSpeed.Slow)
            );
            Assert.AreEqual(
                "medium",
                StrategyHudController.GetSpeedIndicatorPath(theme, TickSpeed.Medium)
            );
            Assert.AreEqual(
                "fast",
                StrategyHudController.GetSpeedIndicatorPath(theme, TickSpeed.Fast)
            );
            Assert.IsNull(StrategyHudController.GetSpeedIndicatorPath(null, TickSpeed.Fast));
        }

        [Test]
        public void BuildSpeedMenuCommands_DefaultCatalog_ReturnsOrderedEnabledCommands()
        {
            IReadOnlyList<StrategyMenuCommand> commands =
                StrategyHudController.BuildSpeedMenuCommands();

            CollectionAssert.AreEqual(
                new[] { "Pause", "Very Slow", "Slow", "Medium", "Fast" },
                commands.Select(command => command.Text)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyContextMenuActions.GameSpeedPause,
                    StrategyContextMenuActions.GameSpeedVerySlow,
                    StrategyContextMenuActions.GameSpeedSlow,
                    StrategyContextMenuActions.GameSpeedMedium,
                    StrategyContextMenuActions.GameSpeedFast,
                },
                commands.Select(command => command.Action)
            );
            Assert.IsTrue(commands.All(command => command.Enabled));
        }

        [Test]
        public void GetUnreadMessageTypes_MixedMessages_ReturnsUnreadCategories()
        {
            Faction faction = new Faction
            {
                Messages = new Dictionary<MessageType, List<Message>>
                {
                    [MessageType.Fleet] = new List<Message>
                    {
                        new Message { Read = true },
                        new Message { Read = false },
                    },
                    [MessageType.Mission] = new List<Message> { new Message { Read = true } },
                    [MessageType.Resource] = null,
                },
            };

            HashSet<MessageType> types = StrategyHudController.GetUnreadMessageTypes(faction);

            CollectionAssert.AreEquivalent(new[] { MessageType.Fleet }, types);
        }

        [Test]
        public void GetUnreadMessageTypes_MissingFaction_ReturnsEmptyCollection()
        {
            HashSet<MessageType> types = StrategyHudController.GetUnreadMessageTypes(null);

            Assert.IsEmpty(types);
        }

        [Test]
        public void OnContextMenuCommandSelected_OwnedEnabledSpeedCommand_SetsGameSpeed()
        {
            StrategyMenuCommand command = new StrategyMenuCommand(
                StrategyContextMenuActions.GameSpeedFast,
                "Fast",
                true
            );
            ContextMenuRequest request = new ContextMenuRequest(
                _controller,
                new[] { command },
                _controller
            );

            _controller.OnContextMenuCommandSelected(request, command);

            Assert.AreEqual(TickSpeed.Fast, _actions.SelectedSpeed);
        }

        [Test]
        public void OnContextMenuCommandSelected_ForeignRequest_IgnoresCommand()
        {
            StrategyMenuCommand command = new StrategyMenuCommand(
                StrategyContextMenuActions.GameSpeedFast,
                "Fast",
                true
            );
            ContextMenuRequest request = new ContextMenuRequest(
                new object(),
                new[] { command },
                _controller
            );

            _controller.OnContextMenuCommandSelected(request, command);

            Assert.IsNull(_actions.SelectedSpeed);
        }

        private sealed class TestActions : IStrategyHudActions
        {
            public TickSpeed? SelectedSpeed { get; private set; }

            public void BeginAdvisorConstruction(
                ManufacturingType manufacturingType,
                int sourceX,
                int sourceY
            ) { }

            public void OpenAdvisorCommandContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            public void OpenAdvisorNotificationContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            public void OpenAdvisorReport(AdvisorReportMode mode) { }

            public void OpenMessagesTab(MessagesTab tab) { }

            public void OpenSpeedContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            public void ReleaseHudButton(StrategyHudAction action, int sourceX, int sourceY) { }

            public void SetGameSpeed(TickSpeed speed)
            {
                SelectedSpeed = speed;
            }

            public void RequestHudRender() { }
        }
    }
}
