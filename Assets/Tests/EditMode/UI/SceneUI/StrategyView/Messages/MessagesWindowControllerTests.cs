using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesWindowControllerTests
    {
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        [Test]
        public void GetDetailAudioPaths_MessageAndOfficerPaths_ReturnsPlaybackOrder()
        {
            Message message = new Message(MessageType.Fleet, "Fleet Arrived")
            {
                AmbientAudioPath = "Audio/SFX/StrategyView/Messages/fleet",
                OfficerVoicePath = "Audio/Voices/Officers/officer",
            };

            IReadOnlyList<string> paths = MessagesWindowController.GetDetailAudioPaths(message);

            CollectionAssert.AreEqual(
                new[] { "Audio/SFX/StrategyView/Messages/fleet", "Audio/Voices/Officers/officer" },
                paths
            );
        }

        [Test]
        public void GetDetailAudioPaths_MissingMessage_ReturnsEmptyPlaybackOrder()
        {
            IReadOnlyList<string> paths = MessagesWindowController.GetDetailAudioPaths(null);

            Assert.IsEmpty(paths);
        }

        [Test]
        public void GetDetailAudioPaths_EmptyPaths_ReturnsEmptyPlaybackOrder()
        {
            Message message = new Message(MessageType.Fleet, "Fleet Arrived");

            IReadOnlyList<string> paths = MessagesWindowController.GetDetailAudioPaths(message);

            Assert.IsEmpty(paths);
        }

        [Test]
        public void ToggleMessageNotification_AllMessagesTab_TogglesGlobalSetting()
        {
            Faction faction = new Faction();
            faction.ToggleAdvisorMessageNotification(MessageType.Fleet);

            MessagesWindowController.ToggleMessageNotification(faction, MessagesTab.All);

            Assert.IsFalse(faction.AdvisorMessageNotificationsEnabled);
            Assert.IsFalse(
                MessagesWindowController.IsMessageNotificationEnabled(faction, MessagesTab.Fleet)
            );
            Assert.IsFalse(
                MessagesWindowController.IsMessageNotificationEnabled(faction, MessagesTab.Mission)
            );
        }

        [Test]
        public void ToggleMessageNotification_CategoryTab_TogglesOnlyCategorySetting()
        {
            Faction faction = new Faction();

            MessagesWindowController.ToggleMessageNotification(faction, MessagesTab.Fleet);

            Assert.IsTrue(faction.AdvisorMessageNotificationsEnabled);
            Assert.IsFalse(
                MessagesWindowController.IsMessageNotificationEnabled(faction, MessagesTab.Fleet)
            );
            Assert.IsTrue(
                MessagesWindowController.IsMessageNotificationEnabled(faction, MessagesTab.Mission)
            );
        }

        [Test]
        public void RemoveSelectedMessages_SelectedIDs_RemovesMatchingMessagesAcrossBuckets()
        {
            Message fleet = new Message(MessageType.Fleet, "Fleet") { InstanceID = "fleet" };
            Message mission = new Message(MessageType.Mission, "Mission")
            {
                InstanceID = "mission",
            };
            Message retained = new Message(MessageType.Mission, "Retained")
            {
                InstanceID = "retained",
            };
            Faction faction = new Faction();
            faction.Messages[MessageType.Fleet] = new List<Message> { fleet };
            faction.Messages[MessageType.Mission] = new List<Message> { mission, retained };

            bool removed = MessagesWindowController.RemoveSelectedMessages(
                faction,
                new[] { fleet.InstanceID, mission.InstanceID }
            );

            Assert.IsTrue(removed);
            Assert.IsEmpty(faction.Messages[MessageType.Fleet]);
            CollectionAssert.AreEqual(new[] { retained }, faction.Messages[MessageType.Mission]);
        }

        [Test]
        public void RemoveSelectedMessages_MissingInputOrMatch_ReturnsFalse()
        {
            Faction faction = new Faction();
            faction.Messages[MessageType.Fleet] = new List<Message>
            {
                new Message(MessageType.Fleet, "Fleet") { InstanceID = "fleet" },
            };

            Assert.IsFalse(
                MessagesWindowController.RemoveSelectedMessages(null, new[] { "fleet" })
            );
            Assert.IsFalse(MessagesWindowController.RemoveSelectedMessages(faction, null));
            Assert.IsFalse(
                MessagesWindowController.RemoveSelectedMessages(faction, new List<string>())
            );
            Assert.IsFalse(
                MessagesWindowController.RemoveSelectedMessages(
                    faction,
                    new[] { null, string.Empty, "missing" }
                )
            );
            Assert.AreEqual(1, faction.Messages[MessageType.Fleet].Count);
        }

        [Test]
        public void MarkMessageRead_Message_SetsReadState()
        {
            Message message = new Message(MessageType.Fleet, "Fleet") { Read = false };

            MessagesWindowController.MarkMessageRead(message);
            MessagesWindowController.MarkMessageRead(null);

            Assert.IsTrue(message.Read);
        }

        [Test]
        public void HasNavigationTarget_AnyNavigationIdentifier_ReturnsTrue()
        {
            Message primary = new Message { NavigationTargetInstanceID = "primary" };
            Message secondary = new Message { NavigationSecondaryTargetInstanceID = "secondary" };
            Message location = new Message { EventLocationInstanceID = "location" };

            Assert.IsTrue(MessagesWindowController.HasNavigationTarget(primary));
            Assert.IsTrue(MessagesWindowController.HasNavigationTarget(secondary));
            Assert.IsTrue(MessagesWindowController.HasNavigationTarget(location));
            Assert.IsFalse(MessagesWindowController.HasNavigationTarget(new Message()));
            Assert.IsFalse(MessagesWindowController.HasNavigationTarget(null));
        }

        [Test]
        public void GetRows_NullFaction_ReturnsEmptyList()
        {
            List<Message> rows = MessagesWindowController.GetRows(null, MessagesTab.All);

            Assert.IsEmpty(rows);
        }

        [Test]
        public void GetRows_AllMessages_ReturnsMessagesAcrossBucketsInStorageOrder()
        {
            Message fleet = new Message(MessageType.Fleet, "Fleet");
            Message mission = new Message(MessageType.Mission, "Mission");
            Faction faction = new Faction();
            faction.Messages[MessageType.Fleet] = new List<Message> { fleet };
            faction.Messages[MessageType.Mission] = new List<Message> { mission };

            List<Message> rows = MessagesWindowController.GetRows(faction, MessagesTab.All);

            CollectionAssert.AreEqual(new[] { fleet, mission }, rows);
        }

        [Test]
        public void GetRows_CategoryTab_ReturnsStoredCategoryOrEmptyList()
        {
            Message fleet = new Message(MessageType.Fleet, "Fleet");
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
                new Message(MessageType.Advice, "Agent Advice"),
            };
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(faction);
            game.Summary.PlayerFactionID = faction.InstanceID;
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
                    _ => { },
                    () => uiContext,
                    windowLayer,
                    windowManager,
                    () => Vector2Int.zero,
                    windowManager.DestroyWindow,
                    () => { }
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
            MessagesWindowController.ToggleMessageNotification(null, MessagesTab.All);
        }

        [Test]
        public void TabClick_FromMessageDetail_LoadsRequestedTabRows()
        {
            Message fleetMessage = new Message(MessageType.Fleet, "Fleet")
            {
                InstanceID = "fleet-message",
            };
            Message missionMessage = new Message(MessageType.Mission, "Mission")
            {
                InstanceID = "mission-message",
            };
            Faction faction = new Faction { InstanceID = "FNALL1" };
            faction.Messages[MessageType.Fleet] = new List<Message> { fleetMessage };
            faction.Messages[MessageType.Mission] = new List<Message> { missionMessage };
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(faction);
            game.Summary.PlayerFactionID = faction.InstanceID;
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
                    _ => { },
                    () => uiContext,
                    windowLayer,
                    windowManager,
                    () => Vector2Int.zero,
                    windowManager.DestroyWindow,
                    () => { }
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

        private sealed class TestActions : IMessagesWindowActions
        {
            public bool OpenMessageTarget(
                string targetInstanceId,
                string secondaryTargetInstanceId,
                string locationInstanceId
            )
            {
                return true;
            }
        }
    }
}
