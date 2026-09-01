using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Advisor;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
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
                    "Manage Naming",
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
        public void BuildCommandMenu_WithoutPlayerFaction_DisablesAllCommands()
        {
            IReadOnlyList<StrategyMenuCommand> commandMenu =
                StrategyAdvisorController.BuildCommandMenu(null);

            Assert.IsTrue(commandMenu.All(command => !command.Enabled));
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
        public void OnContextMenuCommandSelected_ManageProductionEnabled_ProcessesAutomationImmediately()
        {
            Faction faction = new Faction();
            TestActions actions = new TestActions();
            StrategyAdvisorController controller = new StrategyAdvisorController(
                () => faction,
                _ => null,
                _ => { }
            );
            controller.Initialize(actions);
            StrategyMenuCommand command = StrategyAdvisorController
                .BuildCommandMenu(faction)
                .Single(item => item.Action == StrategyMenuAction.AdvisorManageProduction);
            ContextMenuRequest request = (ContextMenuRequest)
                typeof(StrategyAdvisorController)
                    .GetMethod(
                        "CreateContextMenuRequest",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )
                    ?.Invoke(
                        controller,
                        new object[]
                        {
                            new List<StrategyMenuCommand> { command },
                            0,
                            0,
                        }
                    );

            controller.OnContextMenuCommandSelected(request, command);

            Assert.IsTrue(faction.ManageProduction);
            Assert.AreSame(faction, actions.ProcessedFaction);
        }

        [Test]
        public void OnContextMenuCommandSelected_ManageNamingEnabled_ProcessesAutomationImmediately()
        {
            Faction faction = new Faction();
            TestActions actions = new TestActions();
            StrategyAdvisorController controller = new StrategyAdvisorController(
                () => faction,
                _ => null,
                _ => { }
            );
            controller.Initialize(actions);
            StrategyMenuCommand command = StrategyAdvisorController
                .BuildCommandMenu(faction)
                .Single(item => item.Action == StrategyMenuAction.AdvisorManageNaming);
            ContextMenuRequest request = (ContextMenuRequest)
                typeof(StrategyAdvisorController)
                    .GetMethod(
                        "CreateContextMenuRequest",
                        BindingFlags.Instance | BindingFlags.NonPublic
                    )
                    ?.Invoke(
                        controller,
                        new object[]
                        {
                            new List<StrategyMenuCommand> { command },
                            0,
                            0,
                        }
                    );

            controller.OnContextMenuCommandSelected(request, command);

            Assert.IsTrue(faction.ManageNaming);
            Assert.AreSame(faction, actions.ProcessedFaction);
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

                textures[theme.GetFramePath(theme.ProtocolIdleAnimation, 0, false)] =
                    protocolIdleTexture;
                textures[theme.GetFramePath(theme.DroidIdleAnimation, 0, true)] = droidIdleTexture;
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
            theme.Notifications.Add(
                new StrategyAdvisorNotificationTheme
                {
                    NotificationType = AdvisorNotificationType.PositivePopularSupport,
                    LifetimeTicks = 20,
                    Droid = new StrategyAdvisorAnimationTheme
                    {
                        Animation = "Alert",
                        FrameCount = 2,
                    },
                }
            );
            Texture2D protocolIdleTexture = new Texture2D(20, 30);
            Texture2D droidIdleTexture = new Texture2D(20, 30);
            Texture2D firstFrame = new Texture2D(20, 30);
            Texture2D secondFrame = new Texture2D(20, 30);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                [theme.GetFramePath(theme.ProtocolIdleAnimation, 0, false)] = protocolIdleTexture,
                [theme.GetFramePath(theme.DroidIdleAnimation, 0, true)] = droidIdleTexture,
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
                    new MessageDeliveredResult
                    {
                        Message = new StatusMessage(MessageType.Fleet, "Fleet arrived"),
                        NotificationType = AdvisorNotificationType.PositivePopularSupport,
                    },
                    0,
                    true
                );

                controller.ProcessPending(0, true);

                Assert.AreEqual(0, playbackCount);
                Assert.AreSame(droidIdleTexture, GetImage(rootObject, "DroidImage").texture);

                textures[theme.GetFramePath("Alert", 0, true)] = firstFrame;
                textures[theme.GetFramePath("Alert", 1, true)] = secondFrame;
                controller.ProcessPending(0, true);

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
        public void ProcessPending_CustomNotification_UsesAuthoredAnimationAndAudioPaths()
        {
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            StrategyAdvisorView view = rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            StrategyAdvisorTheme theme = CreateTheme();
            Texture2D protocolIdleTexture = new Texture2D(20, 30);
            Texture2D droidIdleTexture = new Texture2D(20, 30);
            Texture2D customFrame = new Texture2D(20, 30);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                [theme.GetFramePath(theme.ProtocolIdleAnimation, 0, false)] = protocolIdleTexture,
                [theme.GetFramePath(theme.DroidIdleAnimation, 0, true)] = droidIdleTexture,
                ["Pack/Custom/Advisor/frame-000"] = customFrame,
            };
            try
            {
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");
                StrategyAdvisorController controller = CreateController(textures);
                controller.BindView(view);
                controller.Render(theme);
                StrategyAdvisorAnimationViewData playback = null;
                view.PlaybackStarted += data => playback = data;

                controller.Notify(
                    new MessageDeliveredResult
                    {
                        Message = new StatusMessage(MessageType.Advice, "Custom"),
                        AdvisorNotification = new AdvisorNotification
                        {
                            LifetimeTicks = 20,
                            Droid = new AdvisorAnimation
                            {
                                AnimationPath = "Pack/Custom/Advisor",
                                FrameCount = 1,
                                AudioPath = "Pack/Custom/Audio/advisor",
                            },
                        },
                    },
                    1,
                    true
                );
                controller.ProcessPending(1, true);

                Assert.IsNotNull(playback);
                Assert.AreSame(customFrame, playback.Frames.Single());
                Assert.AreEqual("Pack/Custom/Audio/advisor", playback.AudioPath);
                Assert.AreEqual(0f, playback.DelayBeforeSeconds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(customFrame);
                UnityEngine.Object.DestroyImmediate(droidIdleTexture);
                UnityEngine.Object.DestroyImmediate(protocolIdleTexture);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void PlayInvalidOrderRejected_AuthoredResponse_ReplacesPlayback()
        {
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            StrategyAdvisorView view = rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            StrategyAdvisorTheme advisorTheme = CreateTheme();
            advisorTheme.AudioRoot = "Audio";
            advisorTheme.InvalidOrderRejected = new StrategyAdvisorAnimationTheme
            {
                Animation = "Rejected",
                FrameCount = 1,
                Audio = "Rejected",
            };
            Texture2D idle = new Texture2D(1, 1);
            Texture2D rejectedFrame = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                [advisorTheme.GetFramePath(advisorTheme.ProtocolIdleAnimation, 0, false)] = idle,
                [advisorTheme.GetFramePath(advisorTheme.DroidIdleAnimation, 0, true)] = idle,
                [advisorTheme.GetFramePath("Rejected", 0, false)] = rejectedFrame,
            };
            try
            {
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");
                StrategyAdvisorController controller = CreateController(textures);
                controller.BindView(view);
                controller.Render(advisorTheme);
                StrategyAdvisorAnimationViewData playback = null;
                view.PlaybackStarted += data => playback = data;

                controller.PlayInvalidOrderRejected();

                Assert.IsNotNull(playback);
                Assert.AreSame(rejectedFrame, playback.Frames.Single());
                Assert.AreEqual("Audio/Rejected", playback.AudioPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rejectedFrame);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ReplaceAnimation_ValidPlayback_InvokesPlaybackCallbacks()
        {
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            StrategyAdvisorView view = rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            StrategyAdvisorTheme advisorTheme = CreateTheme();
            Texture2D idle = new Texture2D(1, 1);
            Texture2D frame = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                [advisorTheme.GetFramePath(advisorTheme.ProtocolIdleAnimation, 0, false)] = idle,
                [advisorTheme.GetFramePath(advisorTheme.DroidIdleAnimation, 0, true)] = idle,
            };
            try
            {
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");
                StrategyAdvisorController controller = CreateController(textures);
                controller.BindView(view);
                controller.Render(advisorTheme);
                bool started = false;
                bool completed = false;

                controller.ReplaceAnimation(
                    new StrategyAdvisorAnimationViewData(new[] { frame }, false, null),
                    () => started = true,
                    () => completed = true
                );

                Assert.AreSame(frame, GetImage(rootObject, "ProtocolImage").texture);
                Assert.IsTrue(started);
                view.AdvanceAnimation(0.5f);

                Assert.IsTrue(completed);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ReplaceAnimation_EmptyPlayback_CancelsActivePlaybackAndCompletesReplacement()
        {
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            StrategyAdvisorView view = rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            StrategyAdvisorTheme advisorTheme = CreateTheme();
            Texture2D idle = new Texture2D(1, 1);
            Texture2D frame = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                [advisorTheme.GetFramePath(advisorTheme.ProtocolIdleAnimation, 0, false)] = idle,
                [advisorTheme.GetFramePath(advisorTheme.DroidIdleAnimation, 0, true)] = idle,
            };
            try
            {
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");
                StrategyAdvisorController controller = CreateController(textures);
                controller.BindView(view);
                controller.Render(advisorTheme);
                bool activeCompleted = false;
                bool replacementCompleted = false;
                controller.ReplaceAnimation(
                    new StrategyAdvisorAnimationViewData(new[] { frame }, false, null),
                    null,
                    () => activeCompleted = true
                );

                controller.ReplaceAnimation(null, null, () => replacementCompleted = true);

                Assert.AreSame(idle, GetImage(rootObject, "ProtocolImage").texture);
                Assert.IsFalse(activeCompleted);
                Assert.IsTrue(replacementCompleted);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(frame);
                UnityEngine.Object.DestroyImmediate(idle);
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void CancelAnimation_ActivePlayback_DoesNotInvokeCompletion()
        {
            GameObject rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            StrategyAdvisorView view = rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            StrategyAdvisorTheme advisorTheme = CreateTheme();
            Texture2D idle = new Texture2D(1, 1);
            Texture2D frame = new Texture2D(1, 1);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                [advisorTheme.GetFramePath(advisorTheme.ProtocolIdleAnimation, 0, false)] = idle,
                [advisorTheme.GetFramePath(advisorTheme.DroidIdleAnimation, 0, true)] = idle,
            };
            try
            {
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");
                StrategyAdvisorController controller = CreateController(textures);
                controller.BindView(view);
                controller.Render(advisorTheme);
                bool completed = false;
                controller.ReplaceAnimation(
                    new StrategyAdvisorAnimationViewData(new[] { frame }, false, null),
                    null,
                    () => completed = true
                );

                controller.CancelAnimation();

                Assert.IsFalse(completed);
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
                _ => { }
            );
            controller.Initialize(new TestActions());
            return controller;
        }

        private static StrategyAdvisorTheme CreateTheme()
        {
            return new StrategyAdvisorTheme
            {
                AnimationImageRoot = "Art/Test/Advisors",
                ProtocolIdleAnimation = "Idle",
                DroidIdleAnimation = "Standard",
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
            public Faction ProcessedFaction { get; private set; }

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

            public void ProcessAdvisorAutomation(Faction faction)
            {
                ProcessedFaction = faction;
            }

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
