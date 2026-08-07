using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class StrategyAdvisorControllerTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        [Test]
        public void BuildCommandMenu_PlayerFaction_ReturnsAuthoredOrderAndDefaultChecks()
        {
            Faction faction = new Faction();

            IReadOnlyList<StrategyMenuCommand> commands =
                StrategyAdvisorController.BuildCommandMenu(faction);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Build Ships",
                    "Build Troops",
                    "Build Facilities",
                    "Galaxy Overview",
                    "Objectives",
                    "Manage Garrisons",
                    "Manage Production",
                    "Translate Counterpart",
                    "Agent Advice",
                },
                commands.Select(command => command.Text)
            );
            Assert.AreEqual(
                StrategyContextMenuIconKeys.CheckMark,
                commands.Single(command => command.Text == "Translate Counterpart").IconKey
            );
            Assert.AreEqual(
                StrategyContextMenuIconKeys.CheckMark,
                commands.Single(command => command.Text == "Agent Advice").IconKey
            );
            Assert.AreEqual(
                StrategyContextMenuIconKeys.None,
                commands.Single(command => command.Text == "Manage Garrisons").IconKey
            );
        }

        [Test]
        public void BuildNotificationMenu_SavedCategorySetting_ReturnsAuthoredOrderAndChecks()
        {
            Faction faction = new Faction();
            faction.ToggleAdvisorMessageNotification(MessageType.Fleet);

            IReadOnlyList<StrategyMenuCommand> commands =
                StrategyAdvisorController.BuildNotificationMenu(faction);
            IReadOnlyList<StrategyMenuCommand> alerts = commands
                .Single(command => command.Text == "Message Alerts")
                .SubmenuCommands;

            CollectionAssert.AreEqual(
                new[] { "Messages", "Message Alerts" },
                commands.Select(command => command.Text)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Loyalty",
                    "Fleets",
                    "Mission",
                    "Resources",
                    "Manufacturing",
                    "Defense",
                    "Conflict",
                    "Advice",
                    "Chat",
                },
                alerts.Select(command => command.Text)
            );
            Assert.AreEqual(
                StrategyContextMenuIconKeys.None,
                alerts.Single(command => command.Text == "Fleets").IconKey
            );
            Assert.AreEqual(
                StrategyContextMenuIconKeys.CheckMark,
                alerts.Single(command => command.Text == "Mission").IconKey
            );
        }

        [Test]
        public void BuildCommandMenu_WithoutPlayerFaction_DisablesAllCommands()
        {
            IReadOnlyList<StrategyMenuCommand> commandMenu =
                StrategyAdvisorController.BuildCommandMenu(null);

            Assert.IsTrue(commandMenu.All(command => !command.Enabled));
        }

        [Test]
        public void BuildNotificationMenu_WithoutPlayerFaction_DisablesAllCommands()
        {
            IReadOnlyList<StrategyMenuCommand> notificationMenu =
                StrategyAdvisorController.BuildNotificationMenu(null);

            Assert.IsTrue(
                notificationMenu.All(command =>
                    !command.Enabled
                    && command.SubmenuCommands.All(submenuCommand => !submenuCommand.Enabled)
                )
            );
        }

        [Test]
        public void Render_SameThemeAfterIdleFramesLoad_RefreshesAdvisorImages()
        {
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            StrategyAdvisorView view = rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            StrategyAdvisorTheme theme = CreateTheme();
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
            Texture2D protocolIdleTexture = new Texture2D(20, 30);
            Texture2D droidIdleTexture = new Texture2D(20, 30);
            try
            {
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");
                StrategyAdvisorController controller = CreateController(textures);
                controller.BindView(view);

                controller.Render(theme);

                Assert.IsFalse(GetImage(rootObject, "ProtocolImage").enabled);
                Assert.IsFalse(GetImage(rootObject, "DroidImage").enabled);

                textures[theme.GetFramePath(theme.ProtocolIdleBitmapID, 0, false)] =
                    protocolIdleTexture;
                textures[theme.GetFramePath(theme.DroidIdleBitmapID, 0, true)] = droidIdleTexture;
                controller.Render(theme);

                Assert.AreSame(protocolIdleTexture, GetImage(rootObject, "ProtocolImage").texture);
                Assert.AreSame(droidIdleTexture, GetImage(rootObject, "DroidImage").texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(droidIdleTexture);
                UnityEngine.Object.DestroyImmediate(protocolIdleTexture);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ProcessPending_FramesStillLoading_RetainsNotificationUntilPlaybackIsReady()
        {
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            StrategyAdvisorView view = rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            StrategyAdvisorTheme theme = CreateTheme();
            theme.NotificationCodes.Add(
                new StrategyAdvisorNotificationCodeTheme
                {
                    Code = (int)AdvisorNotificationCode.PositivePopularSupport,
                    TableID = 10,
                    LifetimeTicks = 20,
                }
            );
            theme.Notifications.Add(
                new StrategyAdvisorNotificationTheme
                {
                    TableID = 10,
                    Droid = new StrategyAdvisorAnimationTheme { BitmapID = 3000, FrameCount = 2 },
                }
            );
            Texture2D protocolIdleTexture = new Texture2D(20, 30);
            Texture2D droidIdleTexture = new Texture2D(20, 30);
            Texture2D firstFrame = new Texture2D(20, 30);
            Texture2D secondFrame = new Texture2D(20, 30);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                [theme.GetFramePath(theme.ProtocolIdleBitmapID, 0, false)] = protocolIdleTexture,
                [theme.GetFramePath(theme.DroidIdleBitmapID, 0, true)] = droidIdleTexture,
            };
            try
            {
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");
                StrategyAdvisorController controller = CreateController(textures);
                controller.BindView(view);
                controller.Render(theme);
                int playbackCount = 0;
                view.PlaybackStarted += _ => playbackCount++;
                controller.Notify(
                    new Message(MessageType.Fleet, "Fleet arrived")
                    {
                        AdvisorNotificationCode = (int)
                            AdvisorNotificationCode.PositivePopularSupport,
                    },
                    1,
                    true
                );

                controller.ProcessPending(1, true);

                Assert.AreEqual(0, playbackCount);
                Assert.AreSame(droidIdleTexture, GetImage(rootObject, "DroidImage").texture);

                textures[theme.GetFramePath(3000, 0, true)] = firstFrame;
                textures[theme.GetFramePath(3000, 1, true)] = secondFrame;
                controller.ProcessPending(1, true);

                Assert.AreEqual(1, playbackCount);
                Assert.AreSame(firstFrame, GetImage(rootObject, "DroidImage").texture);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondFrame);
                UnityEngine.Object.DestroyImmediate(firstFrame);
                UnityEngine.Object.DestroyImmediate(droidIdleTexture);
                UnityEngine.Object.DestroyImmediate(protocolIdleTexture);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PlayBriefing_AdvancesConfiguredSegmentsAndCompletesInOrder()
        {
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            StrategyAdvisorView view = rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            StrategyAdvisorTheme advisorTheme = CreateTheme();
            StrategyBriefingTheme briefing = new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Test/Briefing/Protocol",
                AudioRoot = "Pack/Test/Briefing/Audio",
                AudioFilePrefix = "briefing",
            };
            briefing.Segments.Add(
                new StrategyAdvisorAnimationTheme
                {
                    BitmapID = 10,
                    FrameCount = 1,
                    WaveID = 20,
                }
            );
            briefing.Segments.Add(
                new StrategyAdvisorAnimationTheme
                {
                    BitmapID = 11,
                    FrameCount = 1,
                    WaveID = 21,
                }
            );
            Texture2D idle = new Texture2D(1, 1);
            Texture2D first = new Texture2D(1, 1);
            Texture2D second = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                [advisorTheme.GetFramePath(advisorTheme.ProtocolIdleBitmapID, 0, false)] = idle,
                [advisorTheme.GetFramePath(advisorTheme.DroidIdleBitmapID, 0, true)] = idle,
                [briefing.GetFramePath(10, 0)] = first,
                [briefing.GetFramePath(11, 0)] = second,
            };
            try
            {
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");
                StrategyAdvisorController controller = CreateController(textures);
                controller.BindView(view);
                controller.Render(advisorTheme);
                List<int> focused = new List<int>();
                bool completed = false;

                controller.PlayBriefing(
                    briefing,
                    segment => focused.Add(segment.BitmapID),
                    () => completed = true
                );

                Assert.AreSame(first, GetImage(rootObject, "ProtocolImage").texture);
                view.AdvanceAnimation(0.5f);
                Assert.AreSame(second, GetImage(rootObject, "ProtocolImage").texture);
                Assert.IsFalse(completed);
                view.AdvanceAnimation(0.5f);

                CollectionAssert.AreEqual(new[] { 10, 11 }, focused);
                Assert.IsTrue(completed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void SkipBriefing_WithoutSkipResponse_CompletesImmediately()
        {
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            StrategyAdvisorView view = rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            StrategyAdvisorTheme advisorTheme = CreateTheme();
            StrategyBriefingTheme briefing = new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Test/Briefing/Protocol",
            };
            briefing.Segments.Add(
                new StrategyAdvisorAnimationTheme { BitmapID = 10, FrameCount = 1 }
            );
            Texture2D idle = new Texture2D(1, 1);
            Texture2D frame = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                [advisorTheme.GetFramePath(advisorTheme.ProtocolIdleBitmapID, 0, false)] = idle,
                [advisorTheme.GetFramePath(advisorTheme.DroidIdleBitmapID, 0, true)] = idle,
                [briefing.GetFramePath(10, 0)] = frame,
            };
            try
            {
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");
                StrategyAdvisorController controller = CreateController(textures);
                controller.BindView(view);
                controller.Render(advisorTheme);
                bool completed = false;
                controller.PlayBriefing(briefing, _ => { }, () => completed = true);

                controller.SkipBriefing(briefing);

                Assert.IsTrue(completed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        private static StrategyAdvisorController CreateController(
            IReadOnlyDictionary<string, Texture2D> textures
        )
        {
            StrategyAdvisorController controller = new StrategyAdvisorController(
                () => new Faction(),
                path => textures.TryGetValue(path, out Texture2D texture) ? texture : null,
                _ => { },
                _ => 0f
            );
            controller.Initialize(new TestActions());
            return controller;
        }

        private static StrategyAdvisorTheme CreateTheme()
        {
            return new StrategyAdvisorTheme
            {
                AnimationImageRoot = "Art/Test/Advisors",
                ProtocolIdleBitmapID = 2001,
                DroidIdleBitmapID = 3331,
                FrameIntervalSeconds = 0.5f,
                RepeatCooldownTicks = 10,
            };
        }

        private static RawImage GetImage(GameObject rootObject, string name)
        {
            return rootObject
                .GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == name);
        }

        private sealed class TestActions : IStrategyHudActions
        {
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

            public void SetGameSpeed(TickSpeed speed) { }

            public void RequestHudRender() { }
        }
    }
}
