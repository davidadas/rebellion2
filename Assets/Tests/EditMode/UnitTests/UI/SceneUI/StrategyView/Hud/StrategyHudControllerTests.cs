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

        /// <summary>
        /// Sets up.
        /// </summary>
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

        /// <summary>
        /// Verifies constructor null dependencies throw argument null exception.
        /// </summary>
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

        /// <summary>
        /// Verifies get source speed game speed returns source speed.
        /// </summary>
        /// <param name="speed">The speed.</param>
        /// <param name="expected">The expected.</param>
        [TestCase(TickSpeed.Paused, 0)]
        [TestCase(TickSpeed.VerySlow, 1)]
        [TestCase(TickSpeed.Slow, 2)]
        [TestCase(TickSpeed.Medium, 3)]
        [TestCase(TickSpeed.Fast, 4)]
        public void GetSourceSpeed_GameSpeed_ReturnsSourceSpeed(TickSpeed speed, int expected)
        {
            Assert.AreEqual(expected, StrategyHudController.GetSourceSpeed(speed));
        }

        /// <summary>
        /// Verifies get speed indicator path configured theme returns mapped artwork.
        /// </summary>
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

        /// <summary>
        /// Verifies create view data paused speed shows paused instead of tick.
        /// </summary>
        [Test]
        public void CreateViewData_PausedSpeed_ShowsPausedInsteadOfTick()
        {
            StrategyHudViewData data = _controller.CreateViewData(
                new StrategyHudRenderData("42", "100", "200", "300", TickSpeed.Paused, null),
                new FactionTheme()
            );

            Assert.AreEqual("PAUSED", data.TickCounter.Text);
        }

        /// <summary>
        /// Verifies create view data running speed shows tick.
        /// </summary>
        [Test]
        public void CreateViewData_RunningSpeed_ShowsTick()
        {
            StrategyHudViewData data = _controller.CreateViewData(
                new StrategyHudRenderData("42", "100", "200", "300", TickSpeed.Medium, null),
                new FactionTheme()
            );

            Assert.AreEqual("42", data.TickCounter.Text);
        }

        /// <summary>
        /// Verifies create view data configured button resolves released and pressed artwork.
        /// </summary>
        [Test]
        public void CreateViewData_ConfiguredButton_ResolvesReleasedAndPressedArtwork()
        {
            Texture2D upTexture = new Texture2D(2, 2);
            Texture2D pressedTexture = new Texture2D(2, 2);
            try
            {
                StrategyHudController controller = new StrategyHudController(
                    () => new Faction(),
                    () => new FactionTheme(),
                    path => path == "up" ? upTexture : pressedTexture,
                    _ => { }
                );
                FactionTheme theme = new FactionTheme
                {
                    TacticalHUDLayout = new TacticalHUDLayout
                    {
                        Buttons = new List<StrategyHudButtonTheme>
                        {
                            new StrategyHudButtonTheme
                            {
                                Action = StrategyHudAction.SystemFinder,
                                UpImagePath = "up",
                                PressedImagePath = "pressed",
                                PressedImageLayout = new SourceRectLayout
                                {
                                    X = 10,
                                    Y = 20,
                                    Width = 30,
                                    Height = 40,
                                },
                                HitArea = new SourceRectLayout
                                {
                                    X = 10,
                                    Y = 20,
                                    Width = 30,
                                    Height = 40,
                                },
                            },
                        },
                    },
                };

                StrategyHudViewData data = controller.CreateViewData(
                    new StrategyHudRenderData("42", "100", "200", "300", TickSpeed.Medium, null),
                    theme
                );

                Assert.AreSame(upTexture, data.Buttons[0].UpTexture);
                Assert.AreSame(pressedTexture, data.Buttons[0].PressedTexture);
                Assert.AreEqual(new RectInt(10, 20, 30, 40), data.Buttons[0].ImageBounds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(upTexture);
                UnityEngine.Object.DestroyImmediate(pressedTexture);
            }
        }

        /// <summary>
        /// Verifies build speed menu commands default catalog returns ordered enabled commands.
        /// </summary>
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
                    StrategyMenuAction.GameSpeedPause,
                    StrategyMenuAction.GameSpeedVerySlow,
                    StrategyMenuAction.GameSpeedSlow,
                    StrategyMenuAction.GameSpeedMedium,
                    StrategyMenuAction.GameSpeedFast,
                },
                commands.Select(command => command.Action)
            );
            Assert.IsTrue(commands.All(command => command.Enabled));
        }

        /// <summary>
        /// Verifies get unread message types mixed messages returns unread categories.
        /// </summary>
        [Test]
        public void GetUnreadMessageTypes_MixedMessages_ReturnsUnreadCategories()
        {
            Faction faction = new Faction
            {
                Messages = new Dictionary<MessageType, List<Message>>
                {
                    [MessageType.Fleet] = new List<Message>
                    {
                        new StatusMessage { Read = true },
                        new StatusMessage { Read = false },
                    },
                    [MessageType.Mission] = new List<Message> { new StatusMessage { Read = true } },
                    [MessageType.Resource] = null,
                },
            };

            HashSet<MessageType> types = StrategyHudController.GetUnreadMessageTypes(faction);

            CollectionAssert.AreEquivalent(new[] { MessageType.Fleet }, types);
        }

        /// <summary>
        /// Verifies get unread message types missing faction returns empty collection.
        /// </summary>
        [Test]
        public void GetUnreadMessageTypes_MissingFaction_ReturnsEmptyCollection()
        {
            HashSet<MessageType> types = StrategyHudController.GetUnreadMessageTypes(null);

            Assert.IsEmpty(types);
        }

        /// <summary>
        /// Verifies on context menu command selected owned enabled speed command sets game speed.
        /// </summary>
        [Test]
        public void OnContextMenuCommandSelected_OwnedEnabledSpeedCommand_SetsGameSpeed()
        {
            StrategyMenuCommand command = new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedFast,
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

        /// <summary>
        /// Verifies on context menu command selected foreign request ignores command.
        /// </summary>
        [Test]
        public void OnContextMenuCommandSelected_ForeignRequest_IgnoresCommand()
        {
            StrategyMenuCommand command = new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedFast,
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

            /// <summary>
            /// Executes begin advisor construction.
            /// </summary>
            /// <param name="manufacturingType">The manufacturing type.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void BeginAdvisorConstruction(
                ManufacturingType manufacturingType,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Opens advisor command context menu.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenAdvisorCommandContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Opens advisor notification context menu.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenAdvisorNotificationContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Opens advisor report.
            /// </summary>
            /// <param name="mode">The mode.</param>
            public void OpenAdvisorReport(AdvisorReportMode mode) { }

            /// <summary>
            /// Opens messages tab.
            /// </summary>
            /// <param name="tab">The tab.</param>
            public void OpenMessagesTab(MessagesTab tab) { }

            /// <summary>
            /// Processes advisor automation.
            /// </summary>
            /// <param name="faction">The faction.</param>
            public void ProcessAdvisorAutomation(Faction faction) { }

            /// <summary>
            /// Opens speed context menu.
            /// </summary>
            /// <param name="request">The request.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void OpenSpeedContextMenu(
                ContextMenuRequest request,
                int sourceX,
                int sourceY
            ) { }

            /// <summary>
            /// Executes release hud button.
            /// </summary>
            /// <param name="action">The action.</param>
            /// <param name="sourceX">The source x.</param>
            /// <param name="sourceY">The source y.</param>
            public void ReleaseHudButton(StrategyHudAction action, int sourceX, int sourceY) { }

            /// <summary>
            /// Sets game speed.
            /// </summary>
            /// <param name="speed">The speed.</param>
            public void SetGameSpeed(TickSpeed speed)
            {
                SelectedSpeed = speed;
            }

            /// <summary>
            /// Executes request hud render.
            /// </summary>
            public void RequestHudRender() { }
        }
    }
}
