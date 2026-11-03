using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesWindowControllerTests
    {
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        [Test]
        public void GetDetailAudioPaths_MessageAndOfficerPaths_ReturnsBothPaths()
        {
            Message message = new StatusMessage(MessageType.Fleet, "Fleet Arrived", "Fleet Arrived")
            {
                BackgroundAudioPath = "Audio/SFX/StrategyView/Messages/fleet",
                OfficerVoicePath = "Audio/Voices/Officers/officer",
            };

            IReadOnlyList<string> paths = MessagesWindowController.GetDetailAudioPaths(message);

            CollectionAssert.AreEqual(
                new[] { "Audio/SFX/StrategyView/Messages/fleet", "Audio/Voices/Officers/officer" },
                paths
            );
        }

        [Test]
        public void GetDetailAudioPaths_MissingMessage_ReturnsEmptyCollection()
        {
            IReadOnlyList<string> paths = MessagesWindowController.GetDetailAudioPaths(null);

            Assert.IsEmpty(paths);
        }

        [Test]
        public void GetDetailAudioPaths_EmptyPaths_ReturnsEmptyCollection()
        {
            Message message = new StatusMessage(
                MessageType.Fleet,
                "Fleet Arrived",
                "Fleet Arrived"
            );

            IReadOnlyList<string> paths = MessagesWindowController.GetDetailAudioPaths(message);

            Assert.IsEmpty(paths);
        }

        [Test]
        public void PlayMessageDetailAudio_NewMessage_StopsPreviousAudioAndStartsCurrentAudio()
        {
            DestroyAudioManagers();
            AudioManager audioManager = AudioManager.EnsureExists();
            AudioClip firstBackground = AudioClip.Create("FirstBackground", 1, 1, 44100, false);
            AudioClip firstVoice = AudioClip.Create("FirstVoice", 1, 1, 44100, false);
            AudioClip secondBackground = AudioClip.Create("SecondBackground", 1, 1, 44100, false);
            Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>
            {
                ["first-background"] = firstBackground,
                ["first-voice"] = firstVoice,
                ["second-background"] = secondBackground,
            };
            List<AudioPlaybackHandle> playbacks = new List<AudioPlaybackHandle>();
            GameObject dependencies = new GameObject("MessagesDependencies");

            try
            {
                MessagesWindowController controller = new MessagesWindowController(
                    TestPlayerRequests.RejectAll(),
                    _ => { },
                    path =>
                    {
                        AudioPlaybackHandle playback = audioManager.PlaySfxInstance(clips[path]);
                        playbacks.Add(playback);
                        return playback;
                    },
                    () => null,
                    dependencies.AddComponent<StrategyWindowLayerView>(),
                    dependencies.AddComponent<UIWindowManager>(),
                    () => Vector2Int.zero,
                    _ => { },
                    () => { },
                    () => default
                );
                Message firstMessage = new StatusMessage(MessageType.Fleet, "First", "First")
                {
                    BackgroundAudioPath = "first-background",
                    OfficerVoicePath = "first-voice",
                };
                Message secondMessage = new StatusMessage(MessageType.Fleet, "Second", "Second")
                {
                    BackgroundAudioPath = "second-background",
                };

                controller.PlayMessageDetailAudio(firstMessage);

                Assert.AreEqual(2, playbacks.Count);
                Assert.AreSame(firstBackground, playbacks[0].Source.clip);
                Assert.AreSame(firstVoice, playbacks[1].Source.clip);
                Assert.AreNotSame(playbacks[0].Source, playbacks[1].Source);

                controller.PlayMessageDetailAudio(secondMessage);

                Assert.IsNull(playbacks[0].Source);
                Assert.IsNull(playbacks[1].Source);
                Assert.AreEqual(3, playbacks.Count);
                Assert.AreSame(secondBackground, playbacks[2].Source.clip);
            }
            finally
            {
                audioManager.StopSfx();
                UnityEngine.Object.DestroyImmediate(firstBackground);
                UnityEngine.Object.DestroyImmediate(firstVoice);
                UnityEngine.Object.DestroyImmediate(secondBackground);
                UnityEngine.Object.DestroyImmediate(dependencies);
                DestroyAudioManagers();
            }
        }

        [Test]
        public void MessageRemovalRequested_SelectedMiddleRow_SelectsDisplayedRowAbove()
        {
            Message oldest = CreateMessage("oldest");
            Message middle = CreateMessage("middle");
            Message newest = CreateMessage("newest");
            (
                GameObject root,
                Faction faction,
                MessagesWindowController controller,
                MessagesWindowView view
            ) = OpenMessagesWindow(new[] { oldest, middle, newest }, () => default);

            try
            {
                SelectMessage(view, middle.InstanceID);
                RemoveSelectedMessages(view);
                controller.RenderWindows();

                RemoveSelectedMessages(view);

                CollectionAssert.AreEqual(new[] { oldest }, faction.Messages[MessageType.Fleet]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MessageRowSelected_MultiSelectModifier_RemovesToggledMessagesTogether()
        {
            Message oldest = CreateMessage("oldest");
            Message middle = CreateMessage("middle");
            Message newest = CreateMessage("newest");
            SelectionModifierState modifiers = default;
            (
                GameObject root,
                Faction faction,
                MessagesWindowController _,
                MessagesWindowView view
            ) = OpenMessagesWindow(new[] { oldest, middle, newest }, () => modifiers);

            try
            {
                SelectMessage(view, newest.InstanceID);
                modifiers = new SelectionModifierState(true, false);
                SelectMessage(view, oldest.InstanceID);
                modifiers = default;

                RemoveSelectedMessages(view);

                CollectionAssert.AreEqual(new[] { middle }, faction.Messages[MessageType.Fleet]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MessageRowSelected_RangeSelectModifier_RemovesContiguousMessagesTogether()
        {
            Message oldest = CreateMessage("oldest");
            Message middle = CreateMessage("middle");
            Message newest = CreateMessage("newest");
            SelectionModifierState modifiers = default;
            (
                GameObject root,
                Faction faction,
                MessagesWindowController _,
                MessagesWindowView view
            ) = OpenMessagesWindow(new[] { oldest, middle, newest }, () => modifiers);

            try
            {
                SelectMessage(view, newest.InstanceID);
                modifiers = new SelectionModifierState(false, true);
                SelectMessage(view, oldest.InstanceID);
                modifiers = default;

                RemoveSelectedMessages(view);

                Assert.IsEmpty(faction.Messages[MessageType.Fleet]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CanChooseMissionContinuation_PendingOwnedWorkingMission_ReturnsTrue()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "mission",
                OwnerInstanceID = "player",
            };
            mission.Initiate(1);
            game.AddSceneNodeByInstanceID(mission);
            Message message = new StatusMessage
            {
                MissionInstanceID = mission.InstanceID,
                MissionContinuationPending = true,
            };

            bool available = MessagesWindowController.CanChooseMissionContinuation(
                game,
                "player",
                message
            );

            Assert.IsTrue(available);
        }

        [Test]
        public void CanChooseMissionContinuation_NonPendingMissionMessage_ReturnsFalse()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = "mission",
                OwnerInstanceID = "player",
            };
            mission.Initiate(1);
            game.AddSceneNodeByInstanceID(mission);
            Message message = new StatusMessage { MissionInstanceID = mission.InstanceID };

            bool available = MessagesWindowController.CanChooseMissionContinuation(
                game,
                "player",
                message
            );

            Assert.IsFalse(available);
        }

        [Test]
        public void MissionContinueRequested_PendingReport_ReturnsToIndexWithoutAbortingMission()
        {
            Message message = CreateMessage("report");
            message.MissionInstanceID = "mission";
            message.MissionContinuationPending = true;
            TestActions actions = new TestActions();
            (GameObject root, _, MessagesWindowController controller, MessagesWindowView view) =
                OpenMessagesWindow(
                    new[] { message },
                    () => default,
                    game =>
                        game.AddSceneNodeByInstanceID(CreateWorkingMission("mission", "FNALL1")),
                    actions
                );

            try
            {
                controller.OpenDetail(message, MessagesTab.Fleet);
                controller.RenderWindows();

                view.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "MissionContinueButton")
                    .onClick.Invoke();
                controller.RenderWindows();

                Assert.IsFalse(message.MissionContinuationPending);
                Assert.IsNull(actions.AbortedMissionInstanceId);
                AssertMessageIndexVisible(controller, view, message.InstanceID);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MissionAbortRequested_PendingReport_AbortsMissionAndReturnsToIndex()
        {
            Message message = CreateMessage("report");
            message.MissionInstanceID = "mission";
            message.MissionContinuationPending = true;
            TestActions actions = new TestActions();
            (GameObject root, _, MessagesWindowController controller, MessagesWindowView view) =
                OpenMessagesWindow(
                    new[] { message },
                    () => default,
                    game =>
                        game.AddSceneNodeByInstanceID(CreateWorkingMission("mission", "FNALL1")),
                    actions
                );

            try
            {
                controller.OpenDetail(message, MessagesTab.Fleet);
                controller.RenderWindows();

                view.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "MissionAbortButton")
                    .onClick.Invoke();
                controller.RenderWindows();

                Assert.AreEqual("mission", actions.AbortedMissionInstanceId);
                Assert.IsFalse(message.MissionContinuationPending);
                AssertMessageIndexVisible(controller, view, message.InstanceID);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Open_CategoryTab_PreservesIndividualUnreadState()
        {
            Message first = CreateMessage("first");
            Message second = CreateMessage("second");
            (GameObject root, Faction faction, _, _) = OpenMessagesWindow(
                new[] { first, second },
                () => default
            );

            try
            {
                Assert.IsFalse(first.Read);
                Assert.IsFalse(second.Read);
                Assert.IsFalse(first.NotificationAcknowledged);
                Assert.IsFalse(second.NotificationAcknowledged);
                Assert.IsTrue(faction.HasUnreadMessages());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OpenTab_DifferentTab_AcknowledgesPreviousTabWithoutMarkingMessagesRead()
        {
            Message first = CreateMessage("first");
            Message second = CreateMessage("second");
            (
                GameObject root,
                Faction faction,
                MessagesWindowController controller,
                MessagesWindowView view
            ) = OpenMessagesWindow(new[] { first, second }, () => default);

            try
            {
                controller.OpenTab(view, MessagesTab.Mission);

                Assert.IsTrue(first.NotificationAcknowledged);
                Assert.IsTrue(second.NotificationAcknowledged);
                Assert.IsFalse(first.Read);
                Assert.IsFalse(second.Read);
                Assert.IsTrue(faction.HasUnreadMessages());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DestroyWindow_PresentedMessages_ClearsNotifications()
        {
            Message first = CreateMessage("first");
            Message second = CreateMessage("second");
            (GameObject root, _, _, _) = OpenMessagesWindow(new[] { first, second }, () => default);

            try
            {
                UIWindowManager windowManager = root.GetComponentInChildren<UIWindowManager>(true);
                windowManager.DestroyWindow(windowManager.Windows.Single());

                Assert.IsTrue(first.NotificationAcknowledged);
                Assert.IsTrue(second.NotificationAcknowledged);
                Assert.IsFalse(first.Read);
                Assert.IsFalse(second.Read);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HasNavigationTarget_AnyNavigationIdentifier_ReturnsTrue()
        {
            Message primary = new StatusMessage { NavigationTargetInstanceID = "primary" };
            Message secondary = new StatusMessage
            {
                NavigationSecondaryTargetInstanceID = "secondary",
            };
            Message location = new StatusMessage { EventLocationInstanceID = "location" };

            Assert.IsTrue(MessagesWindowController.HasNavigationTarget(primary));
            Assert.IsTrue(MessagesWindowController.HasNavigationTarget(secondary));
            Assert.IsTrue(MessagesWindowController.HasNavigationTarget(location));
            Assert.IsFalse(MessagesWindowController.HasNavigationTarget(new StatusMessage()));
            Assert.IsFalse(MessagesWindowController.HasNavigationTarget(null));
        }

        [Test]
        public void GetRows_NullFaction_ReturnsEmptyList()
        {
            List<Message> rows = MessagesWindowController.GetRows(null, MessagesTab.All);

            Assert.IsEmpty(rows);
        }

        [Test]
        public void GetRows_AllMessages_ReturnsMessagesAcrossBucketsInChronologicalOrder()
        {
            Message fleet = new StatusMessage(MessageType.Fleet, "Fleet", "Fleet")
            {
                CreatedTick = 20,
            };
            Message mission = new StatusMessage(MessageType.Mission, "Mission", "Mission")
            {
                CreatedTick = 30,
            };
            Message advice = new StatusMessage(MessageType.Advice, "Advice", "Advice")
            {
                CreatedTick = 10,
            };
            Faction faction = new Faction();
            faction.Messages[MessageType.Fleet] = new List<Message> { fleet };
            faction.Messages[MessageType.Mission] = new List<Message> { mission };
            faction.Messages[MessageType.Advice] = new List<Message> { advice };

            List<Message> rows = MessagesWindowController.GetRows(faction, MessagesTab.All);

            CollectionAssert.AreEqual(new[] { advice, fleet, mission }, rows);
        }

        [Test]
        public void GetRows_CategoryTab_ReturnsStoredCategoryOrEmptyList()
        {
            Message fleet = new StatusMessage(MessageType.Fleet, "Fleet", "Fleet");
            Faction faction = new Faction();
            faction.Messages[MessageType.Fleet] = new List<Message> { fleet };

            List<Message> fleetRows = MessagesWindowController.GetRows(faction, MessagesTab.Fleet);
            List<Message> missingRows = MessagesWindowController.GetRows(
                faction,
                MessagesTab.Mission
            );
            List<Message> unsupportedRows = MessagesWindowController.GetRows(
                faction,
                (MessagesTab)20
            );

            CollectionAssert.AreEqual(new[] { fleet }, fleetRows);
            Assert.IsEmpty(missingRows);
            Assert.IsEmpty(unsupportedRows);
        }

        [Test]
        public void Open_AdviceTab_OpensMessageIndexOnAgentAdvice()
        {
            Faction faction = new Faction { InstanceID = "FNALL1" };
            faction.Messages[MessageType.Advice] = new List<Message>
            {
                new StatusMessage(MessageType.Advice, "Agent Advice", "Agent Advice"),
            };
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(faction);
            game.Summary.PlayerFactionID = faction.InstanceID;
            game.SetFactionController(faction.InstanceID, "PLAYER1", PlayerControllerType.Human);
            UIContext uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            GameObject root = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);

            try
            {
                StrategyWindowLayerView windowLayer =
                    root.GetComponentInChildren<StrategyWindowLayerView>(true);
                UIWindowManager windowManager = root.GetComponentInChildren<UIWindowManager>(true);
                MessagesWindowController controller = new MessagesWindowController(
                    TestPlayerRequests.Create(game, () => faction.InstanceID),
                    _ => { },
                    _ => null,
                    () => uiContext,
                    windowLayer,
                    windowManager,
                    () => Vector2Int.zero,
                    windowManager.DestroyWindow,
                    () => { },
                    () => default
                );
                controller.Initialize(new TestActions());

                controller.Open(MessagesTab.Advice);

                UIWindow window = windowManager.Windows.Single();
                Assert.IsTrue(windowManager.TryGetWindowView(window, out MessagesWindowView view));
                Assert.AreEqual(MessagesTab.Advice, controller.GetActiveTab(view));
                Assert.IsFalse(controller.IsDetailVisible(view));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NotificationOperations_NullFaction_ReturnDisabledWithoutThrowing()
        {
            Assert.IsFalse(
                MessagesWindowController.IsMessageNotificationEnabled(null, MessagesTab.All)
            );
        }

        [Test]
        public void OpenDetail_CombatReport_OpensBattleResultInsteadOfGenericDetail()
        {
            CombatReport report = new CombatReport
            {
                Type = MessageType.Conflict,
                CombatType = CombatReportType.SpaceBattle,
                Title = "Battle at Test",
                InstanceID = "combat-message",
            };
            Faction faction = new Faction { InstanceID = "player" };
            faction.AddMessage(report);
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(faction);
            game.Summary.PlayerFactionID = faction.InstanceID;
            game.SetFactionController(faction.InstanceID, "PLAYER1", PlayerControllerType.Human);
            UIContext uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            GameObject root = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);

            try
            {
                StrategyWindowLayerView windowLayer =
                    root.GetComponentInChildren<StrategyWindowLayerView>(true);
                UIWindowManager windowManager = root.GetComponentInChildren<UIWindowManager>(true);
                MessagesWindowController controller = new MessagesWindowController(
                    TestPlayerRequests.Create(game, () => faction.InstanceID),
                    _ => { },
                    _ => null,
                    () => uiContext,
                    windowLayer,
                    windowManager,
                    () => Vector2Int.zero,
                    windowManager.DestroyWindow,
                    () => { },
                    () => default
                );
                TestActions actions = new TestActions();
                controller.Initialize(actions);

                controller.OpenDetail(report, MessagesTab.All);

                Assert.AreSame(report, actions.OpenedCombatReport);
                Assert.AreEqual(MessagesTab.All, actions.CombatReportReturnTab);
                Assert.IsTrue(report.Read);
                Assert.IsFalse(controller.IsOpen);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TabClick_FromMessageDetail_LoadsRequestedTabRows()
        {
            Message fleetMessage = new StatusMessage(MessageType.Fleet, "Fleet", "Fleet")
            {
                InstanceID = "fleet-message",
            };
            Message missionMessage = new StatusMessage(MessageType.Mission, "Mission", "Mission")
            {
                InstanceID = "mission-message",
            };
            Faction faction = new Faction { InstanceID = "FNALL1" };
            faction.Messages[MessageType.Fleet] = new List<Message> { fleetMessage };
            faction.Messages[MessageType.Mission] = new List<Message> { missionMessage };
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(faction);
            game.Summary.PlayerFactionID = faction.InstanceID;
            game.SetFactionController(faction.InstanceID, "PLAYER1", PlayerControllerType.Human);
            UIContext uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            GameObject root = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);

            try
            {
                StrategyWindowLayerView windowLayer =
                    root.GetComponentInChildren<StrategyWindowLayerView>(true);
                UIWindowManager windowManager = root.GetComponentInChildren<UIWindowManager>(true);
                MessagesWindowController controller = new MessagesWindowController(
                    TestPlayerRequests.Create(game, () => faction.InstanceID),
                    _ => { },
                    _ => null,
                    () => uiContext,
                    windowLayer,
                    windowManager,
                    () => Vector2Int.zero,
                    windowManager.DestroyWindow,
                    () => { },
                    () => default
                );
                controller.Initialize(new TestActions());
                controller.OpenDetail(fleetMessage, MessagesTab.Fleet);
                UIWindow window = windowManager.Windows.Single();
                Assert.IsTrue(windowManager.TryGetWindowView(window, out MessagesWindowView view));
                Assert.IsTrue(controller.IsDetailVisible(view));
                UIComponentTestHelper.InvokeLifecycle(view, "Awake");

                Button missionTabButton = view.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "MissionTabButtonImage");
                missionTabButton.onClick.Invoke();
                controller.RenderWindows();

                Assert.AreEqual(MessagesTab.Mission, controller.GetActiveTab(view));
                Assert.IsFalse(controller.IsDetailVisible(view));
                MessageWindowRowView row = view.GetComponentsInChildren<MessageWindowRowView>(true)
                    .Single(candidate => candidate.gameObject.activeSelf);
                Assert.AreEqual(missionMessage.InstanceID, row.MessageId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Opens an empty composer without changing the selected message category.
        /// </summary>
        [Test]
        public void ChatRequested_PeerAvailable_OpensEmptyComposerAndPreservesIndexCategory()
        {
            var fixture = OpenMessagesWindow(
                Array.Empty<Message>(),
                () => default,
                actions: new TestActions { CanSendPeerChat = true }
            );
            try
            {
                OpenChatComposer(fixture.controller, fixture.view);

                Assert.IsTrue(
                    fixture
                        .view.GetComponentInChildren<MessagesChatComposerView>(true)
                        .gameObject.activeSelf
                );
                Assert.AreEqual(string.Empty, GetChatInput(fixture.view).text);
                Assert.AreEqual(MessagesTab.Fleet, fixture.controller.GetActiveTab(fixture.view));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.root);
            }
        }

        [Test]
        public async Task SubmitPeerChatAsync_MultilineRawText_SendsUnchangedAndReturnsToIndexAsync()
        {
            TestActions actions = new TestActions { CanSendPeerChat = true };
            var fixture = OpenMessagesWindow(
                Array.Empty<Message>(),
                () => default,
                actions: actions
            );
            try
            {
                OpenChatComposer(fixture.controller, fixture.view);
                GetChatInput(fixture.view).text = "  Hello\nother player  ";

                await fixture.controller.SubmitPeerChatAsync(fixture.view);
                fixture.controller.RenderWindows();

                Assert.AreEqual("  Hello\nother player  ", actions.SubmittedPeerChat);
                Assert.IsFalse(
                    fixture
                        .view.GetComponentInChildren<MessagesChatComposerView>(true)
                        .gameObject.activeSelf
                );
                Assert.IsEmpty(fixture.faction.Messages[MessageType.Chat]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.root);
            }
        }

        [Test]
        public async Task SubmitPeerChatAsync_EmptyDraft_DoesNotSubmitAsync()
        {
            TestActions actions = new TestActions { CanSendPeerChat = true };
            var fixture = OpenMessagesWindow(
                Array.Empty<Message>(),
                () => default,
                actions: actions
            );
            try
            {
                OpenChatComposer(fixture.controller, fixture.view);

                await fixture.controller.SubmitPeerChatAsync(fixture.view);

                Assert.AreEqual(0, actions.PeerChatSubmissionCount);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.root);
            }
        }

        [Test]
        public async Task SubmitPeerChatAsync_RejectedSubmission_PreservesDraftAsync()
        {
            TestActions actions = new TestActions
            {
                CanSendPeerChat = true,
                PeerChatSubmit = _ => Task.FromResult(false),
            };
            var fixture = OpenMessagesWindow(
                Array.Empty<Message>(),
                () => default,
                actions: actions
            );
            try
            {
                OpenChatComposer(fixture.controller, fixture.view);
                GetChatInput(fixture.view).text = "  keep this\ndraft  ";

                await fixture.controller.SubmitPeerChatAsync(fixture.view);
                fixture.controller.RenderWindows();

                Assert.IsTrue(
                    fixture
                        .view.GetComponentInChildren<MessagesChatComposerView>(true)
                        .gameObject.activeSelf
                );
                Assert.AreEqual("  keep this\ndraft  ", GetChatInput(fixture.view).text);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixture.root);
            }
        }

        [Test]
        public async Task SubmitPeerChatAsync_OverlappingSubmissions_SendsOnceAsync()
        {
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            TestActions actions = new TestActions
            {
                CanSendPeerChat = true,
                PeerChatSubmit = _ => completion.Task,
            };
            var fixture = OpenMessagesWindow(
                Array.Empty<Message>(),
                () => default,
                actions: actions
            );
            try
            {
                OpenChatComposer(fixture.controller, fixture.view);
                GetChatInput(fixture.view).text = "Only once";
                Task first = fixture.controller.SubmitPeerChatAsync(fixture.view);

                await fixture.controller.SubmitPeerChatAsync(fixture.view);

                Assert.AreEqual(1, actions.PeerChatSubmissionCount);
                completion.SetResult(true);
                await first;
            }
            finally
            {
                completion.TrySetResult(false);
                UnityEngine.Object.DestroyImmediate(fixture.root);
            }
        }

        [Test]
        public async Task SubmitPeerChatAsync_CompletionAfterCancelAndReopen_PreservesNewDraftAsync()
        {
            TaskCompletionSource<bool> completion = new TaskCompletionSource<bool>();
            TestActions actions = new TestActions
            {
                CanSendPeerChat = true,
                PeerChatSubmit = _ => completion.Task,
            };
            var fixture = OpenMessagesWindow(
                Array.Empty<Message>(),
                () => default,
                actions: actions
            );
            try
            {
                OpenChatComposer(fixture.controller, fixture.view);
                GetChatInput(fixture.view).text = "First draft";
                Task first = fixture.controller.SubmitPeerChatAsync(fixture.view);
                Assert.IsTrue(fixture.view.TryCancel());
                fixture.controller.RenderWindows();
                OpenChatComposer(fixture.controller, fixture.view);
                GetChatInput(fixture.view).text = "New draft";

                completion.SetResult(true);
                await first;
                fixture.controller.RenderWindows();

                Assert.IsTrue(
                    fixture
                        .view.GetComponentInChildren<MessagesChatComposerView>(true)
                        .gameObject.activeSelf
                );
                Assert.AreEqual("New draft", GetChatInput(fixture.view).text);
            }
            finally
            {
                completion.TrySetResult(false);
                UnityEngine.Object.DestroyImmediate(fixture.root);
            }
        }

        /// <summary>
        /// Opens the authored chat editor through the existing window command.
        /// </summary>
        /// <param name="controller">The window controller.</param>
        /// <param name="view">The authored window view.</param>
        private static void OpenChatComposer(
            MessagesWindowController controller,
            MessagesWindowView view
        )
        {
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ChatCommandButtonImage")
                .onClick.Invoke();
            controller.RenderWindows();
        }

        /// <summary>
        /// Resolves the authored player-message input.
        /// </summary>
        /// <param name="view">The authored window view.</param>
        /// <returns>The player-message input.</returns>
        private static TMPro.TMP_InputField GetChatInput(MessagesWindowView view)
        {
            return view.GetComponentInChildren<MessagesChatComposerView>(true)
                .GetComponentInChildren<TMPro.TMP_InputField>(true);
        }

        /// <summary>
        /// Executes destroy audio managers.
        /// </summary>
        private static void DestroyAudioManagers()
        {
            foreach (AudioManager manager in UnityEngine.Object.FindObjectsByType<AudioManager>())
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        /// <summary>
        /// Verifies that resolving a mission choice restores the message index.
        /// </summary>
        /// <param name="controller">The Messages controller under test.</param>
        /// <param name="view">The rendered Messages view.</param>
        /// <param name="expectedMessageId">The message expected to remain in the index.</param>
        private static void AssertMessageIndexVisible(
            MessagesWindowController controller,
            MessagesWindowView view,
            string expectedMessageId
        )
        {
            IReadOnlyList<Button> buttons = view.GetComponentsInChildren<Button>(true);

            Assert.IsFalse(controller.IsDetailVisible(view));
            Assert.IsFalse(
                buttons
                    .Single(button => button.name == "MissionContinueButton")
                    .gameObject.activeInHierarchy
            );
            Assert.IsFalse(
                buttons
                    .Single(button => button.name == "MissionAbortButton")
                    .gameObject.activeInHierarchy
            );
            MessageWindowRowView row = view.GetComponentsInChildren<MessageWindowRowView>(true)
                .Single(candidate => candidate.gameObject.activeSelf);
            Assert.AreEqual(expectedMessageId, row.MessageId);
        }

        /// <summary>
        /// Creates a message with a stable identity.
        /// </summary>
        /// <param name="instanceId">The message identity and title.</param>
        /// <returns>The created message.</returns>
        private static Message CreateMessage(string instanceId)
        {
            return new StatusMessage(MessageType.Fleet, instanceId, instanceId)
            {
                InstanceID = instanceId,
            };
        }

        /// <summary>
        /// Creates one working mission for continuation tests.
        /// </summary>
        /// <param name="instanceId">The mission instance identifier.</param>
        /// <param name="ownerInstanceId">The owning faction identifier.</param>
        /// <returns>The working mission.</returns>
        private static DiplomacyMission CreateWorkingMission(
            string instanceId,
            string ownerInstanceId
        )
        {
            DiplomacyMission mission = new DiplomacyMission
            {
                InstanceID = instanceId,
                OwnerInstanceID = ownerInstanceId,
            };
            mission.Initiate(1);
            return mission;
        }

        /// <summary>
        /// Opens a rendered Messages window around a player faction and modifier source.
        /// </summary>
        /// <param name="messages">The messages to display in source order.</param>
        /// <param name="getSelectionModifiers">Returns the active selection modifiers.</param>
        /// <param name="configureGame">Optionally configures the game before UI creation.</param>
        /// <param name="actions">Optional test actions used by the controller.</param>
        /// <returns>The created view fixture.</returns>
        private static (
            GameObject root,
            Faction faction,
            MessagesWindowController controller,
            MessagesWindowView view
        ) OpenMessagesWindow(
            IReadOnlyList<Message> messages,
            Func<SelectionModifierState> getSelectionModifiers,
            Action<GameRoot> configureGame = null,
            TestActions actions = null
        )
        {
            Faction faction = new Faction { InstanceID = "FNALL1" };
            faction.Messages[MessageType.Fleet] = messages.ToList();
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(faction);
            game.Summary.PlayerFactionID = faction.InstanceID;
            game.SetFactionController(faction.InstanceID, "PLAYER1", PlayerControllerType.Human);
            configureGame?.Invoke(game);
            UIContext uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            GameObject root = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            StrategyWindowLayerView windowLayer =
                root.GetComponentInChildren<StrategyWindowLayerView>(true);
            UIWindowManager windowManager = root.GetComponentInChildren<UIWindowManager>(true);
            MessagesWindowController controller = new MessagesWindowController(
                TestPlayerRequests.Create(game, () => faction.InstanceID),
                _ => { },
                _ => null,
                () => uiContext,
                windowLayer,
                windowManager,
                () => Vector2Int.zero,
                windowManager.DestroyWindow,
                () => { },
                getSelectionModifiers
            );
            controller.Initialize(actions ?? new TestActions());
            controller.Open(MessagesTab.Fleet);
            controller.RenderWindows();
            UIWindow window = windowManager.Windows.Single();
            Assert.IsTrue(windowManager.TryGetWindowView(window, out MessagesWindowView view));
            return (root, faction, controller, view);
        }

        /// <summary>
        /// Selects one rendered message row through its pointer contract.
        /// </summary>
        /// <param name="view">The rendered Messages view.</param>
        /// <param name="messageId">The message identity to select.</param>
        private static void SelectMessage(MessagesWindowView view, string messageId)
        {
            MessageWindowRowView row = view.GetComponentsInChildren<MessageWindowRowView>(true)
                .Single(candidate =>
                    candidate.gameObject.activeSelf && candidate.MessageId == messageId
                );
            row.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
        }

        /// <summary>
        /// Invokes the rendered remove-selected control.
        /// </summary>
        /// <param name="view">The rendered Messages view.</param>
        private static void RemoveSelectedMessages(MessagesWindowView view)
        {
            view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "RemoveSelectedButtonImage")
                .onClick.Invoke();
        }

        private sealed class TestActions : IMessagesWindowActions
        {
            public bool CanSendPeerChat { get; set; }

            public Func<string, Task<bool>> PeerChatSubmit { get; set; }

            public string SubmittedPeerChat { get; private set; }

            public int PeerChatSubmissionCount { get; private set; }

            public string AbortedMissionInstanceId { get; private set; }

            public CombatReport OpenedCombatReport { get; private set; }

            public MessagesTab CombatReportReturnTab { get; private set; }

            /// <summary>
            /// Opens combat report.
            /// </summary>
            /// <param name="report">The report.</param>
            /// <param name="returnTab">The Messages tab restored after closing the report.</param>
            public void OpenCombatReport(CombatReport report, MessagesTab returnTab)
            {
                OpenedCombatReport = report;
                CombatReportReturnTab = returnTab;
            }

            /// <summary>
            /// Opens message target.
            /// </summary>
            /// <param name="targetInstanceId">The target instance id.</param>
            /// <param name="secondaryTargetInstanceId">The secondary target instance id.</param>
            /// <param name="locationInstanceId">The location instance id.</param>
            /// <returns>True when the operation succeeds; otherwise false.</returns>
            public bool OpenMessageTarget(
                string targetInstanceId,
                string secondaryTargetInstanceId,
                string locationInstanceId
            )
            {
                return true;
            }

            /// <summary>
            /// Aborts mission.
            /// </summary>
            /// <param name="missionInstanceId">The mission instance id.</param>
            /// <returns>True when the mission id is present.</returns>
            public Task<bool> TryAbortMissionAsync(string missionInstanceId)
            {
                AbortedMissionInstanceId = missionInstanceId;
                return Task.FromResult(!string.IsNullOrEmpty(missionInstanceId));
            }

            /// <summary>
            /// Captures the submitted raw draft and returns the configured acceptance.
            /// </summary>
            /// <param name="rawText">The unmodified player-message text.</param>
            /// <returns>The configured submission result.</returns>
            public Task<bool> SendPeerChatAsync(string rawText)
            {
                SubmittedPeerChat = rawText;
                PeerChatSubmissionCount++;
                return PeerChatSubmit?.Invoke(rawText) ?? Task.FromResult(true);
            }
        }
    }
}
