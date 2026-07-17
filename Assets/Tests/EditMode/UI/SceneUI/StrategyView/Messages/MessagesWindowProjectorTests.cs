using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesWindowProjectorTests
    {
        private const string _playerFactionId = "FNALL1";

        private string _imagePath;
        private MessagesWindowTheme _theme;
        private UIContext _uiContext;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _theme = _uiContext.GetPlayerFactionTheme().StrategyWindows.Messages;
            _imagePath = _uiContext.GetPlayerFactionTheme().GalaxyBackground.ImagePath;
        }

        [Test]
        public void GetHeader_MessageWithTitle_ReturnsTitle()
        {
            Message message = new Message(
                MessageType.Mission,
                "Diplomacy Mission Report",
                "The diplomacy mission failed.\nMore text."
            );

            string header = MessagesWindowProjector.GetHeader(message);

            Assert.AreEqual("Diplomacy Mission Report", header);
        }

        [Test]
        public void GetHeader_MessageWithoutTitle_ReturnsFirstBodyLine()
        {
            Message message = new Message(
                MessageType.Mission,
                null,
                "First body line\nSecond line"
            );

            string header = MessagesWindowProjector.GetHeader(message);

            Assert.AreEqual("First body line", header);
        }

        [Test]
        public void GetHeader_MissingMessageOrText_ReturnsEmptyHeader()
        {
            Assert.AreEqual(string.Empty, MessagesWindowProjector.GetHeader(null));
            Assert.AreEqual(
                string.Empty,
                MessagesWindowProjector.GetHeader(new Message(MessageType.Fleet, null, null))
            );
        }

        [Test]
        public void CreateIndexRows_StoredMessages_ReturnsNewestFirstWithoutMutatingReadState()
        {
            Message first = new Message(MessageType.Fleet, "First")
            {
                InstanceID = "first",
                Read = true,
            };
            Message second = new Message(MessageType.Fleet, "Second")
            {
                InstanceID = "second",
                Read = false,
            };
            List<Message> messages = new List<Message> { first, second };

            List<MessageWindowRowRenderData> rows = MessagesWindowProjector.CreateIndexRows(
                messages,
                new[] { first.InstanceID }
            );

            CollectionAssert.AreEqual(
                new[] { "second", "first" },
                rows.Select(row => row.MessageId)
            );
            Assert.IsTrue(rows[0].Unread);
            Assert.IsFalse(rows[0].Selected);
            Assert.IsFalse(rows[1].Unread);
            Assert.IsTrue(rows[1].Selected);
            Assert.IsTrue(first.Read);
            Assert.IsFalse(second.Read);
        }

        [Test]
        public void CreateIndexRows_NullMessages_ReturnsEmptyRows()
        {
            List<MessageWindowRowRenderData> rows = MessagesWindowProjector.CreateIndexRows(
                null,
                null
            );

            Assert.IsEmpty(rows);
        }

        [Test]
        public void CreateIndexRows_NullMessage_ReturnsNormalizedUnreadRow()
        {
            List<MessageWindowRowRenderData> rows = MessagesWindowProjector.CreateIndexRows(
                new Message[] { null },
                new[] { string.Empty }
            );

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(string.Empty, rows[0].MessageId);
            Assert.AreEqual(string.Empty, rows[0].Header);
            Assert.AreEqual(default(MessageType), rows[0].Type);
            Assert.IsFalse(rows[0].Selected);
            Assert.IsFalse(rows[0].Unread);
        }

        [Test]
        public void Project_IndexPanel_ReturnsCompleteThemedPresentation()
        {
            Message first = new Message(MessageType.Fleet, "First")
            {
                InstanceID = "first",
                Read = true,
            };
            Message second = new Message(MessageType.Fleet, "Second")
            {
                InstanceID = "second",
                Read = false,
            };

            MessagesWindowRenderData data = MessagesWindowProjector.Project(
                _uiContext,
                new[] { first, second },
                MessagesTab.Fleet,
                false,
                first.InstanceID,
                new[] { first.InstanceID },
                false,
                false,
                12,
                24
            );

            Assert.IsFalse(data.DetailVisible);
            Assert.AreEqual(new Vector2Int(12, 24), data.FramePosition);
            Assert.AreSame(
                _uiContext.GetTexture(_theme.OverlayFrameImagePath),
                data.OverlayFrameTexture
            );
            Assert.IsNotNull(data.CommandBar);
            Assert.AreSame(
                _uiContext.GetTexture(_theme.ButtonStripImagePath),
                data.CommandBar.ButtonStripTexture
            );
            Assert.IsTrue(data.CommandBar.CloseButton.Visible);
            Assert.IsTrue(data.CommandBar.CloseButton.Enabled);
            Assert.IsTrue(data.CommandBar.DisplayButton.Visible);
            Assert.IsTrue(data.CommandBar.DisplayButton.Enabled);
            Assert.IsFalse(data.CommandBar.IndexButton.Visible);
            Assert.IsTrue(data.CommandBar.IndexButton.Enabled);
            Assert.AreSame(
                _uiContext.GetTexture(_theme.SignalSilentImagePath),
                data.CommandBar.SignalButton.Texture
            );
            Assert.IsTrue(data.CommandBar.SignalTargetButton.Visible);
            Assert.IsFalse(data.CommandBar.SignalTargetButton.Enabled);
            Assert.IsTrue(data.CommandBar.ChatButton.Visible);
            Assert.IsTrue(data.CommandBar.ChatButton.Enabled);
            Assert.IsNotNull(data.IndexPanel);
            Assert.IsNull(data.DetailPanel);
            Assert.AreEqual(MessagesTab.Fleet, data.IndexPanel.ActiveTab);
            Assert.AreEqual("Fleet Messages", data.IndexPanel.Title);
            Assert.AreEqual(MessagesTabCatalog.Count, data.IndexPanel.Tabs.Count);
            Assert.AreEqual(MessagesTab.Fleet, data.IndexPanel.Tabs[2].Tab);
            Assert.AreSame(
                _uiContext.GetTexture(_theme.FleetButton.GetImagePath(true)),
                data.IndexPanel.Tabs[2].Texture
            );
            Assert.AreSame(
                _uiContext.GetTexture(_theme.FleetButton.GetImagePath(true)),
                data.IndexPanel.Tabs[2].PressedTexture
            );
            Assert.AreEqual(2, data.IndexPanel.Rows.Count);
            MessageWindowRowRenderData newest = data.IndexPanel.Rows[0];
            Assert.AreEqual(second.InstanceID, newest.MessageId);
            Assert.IsFalse(newest.Selected);
            Assert.IsTrue(newest.Unread);
            Assert.AreEqual((Color32)Color.white, newest.HeaderColor);
            Assert.AreSame(
                _uiContext.GetTexture(_theme.GetIconImagePath(MessageType.Fleet)),
                newest.SelectedIconTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(_theme.GetNormalIconImagePath(MessageType.Fleet)),
                newest.NormalIconTexture
            );
            MessageWindowRowRenderData selected = data.IndexPanel.Rows[1];
            Assert.IsTrue(selected.Selected);
            Assert.IsFalse(selected.Unread);
            Assert.AreEqual((Color32)_theme.GetSelectedRowTextColor(), selected.HeaderColor);
            Assert.AreSame(
                _uiContext.GetTexture(_theme.SelectionImagePath),
                selected.SelectionTexture
            );
        }

        [Test]
        public void Project_DetailPanel_ReturnsSelectedMessageAndNavigationCommands()
        {
            Message first = CreateMessage("first", "First", MessageType.Fleet);
            Message second = CreateMessage("second", "Second", MessageType.Advice);
            Message third = CreateMessage("third", "Third", MessageType.Mission);
            second.Text = "Second body";
            second.DisplayImagePath = _imagePath;
            second.OverlayImagePath = _imagePath;

            MessagesWindowRenderData data = MessagesWindowProjector.Project(
                _uiContext,
                new[] { first, second, third },
                MessagesTab.Advice,
                true,
                second.InstanceID,
                new[] { second.InstanceID },
                true,
                true,
                1,
                2
            );

            Assert.IsTrue(data.DetailVisible);
            Assert.IsNull(data.IndexPanel);
            Assert.IsNotNull(data.DetailPanel);
            Assert.IsFalse(data.CommandBar.DisplayButton.Visible);
            Assert.IsTrue(data.CommandBar.IndexButton.Visible);
            Assert.IsTrue(data.CommandBar.SignalTargetButton.Enabled);
            Assert.IsTrue(data.CommandBar.ChatButton.Enabled);
            Assert.AreSame(
                _uiContext.GetTexture(_theme.SignalButton.GetImagePath(false)),
                data.CommandBar.SignalButton.Texture
            );
            Assert.AreEqual(second.InstanceID, data.DetailPanel.MessageId);
            Assert.AreEqual("Second", data.DetailPanel.Header);
            Assert.AreEqual("Second body", data.DetailPanel.Text);
            Assert.AreSame(_uiContext.GetTexture(_imagePath), data.DetailPanel.CardTexture);
            Assert.AreSame(_uiContext.GetTexture(_imagePath), data.DetailPanel.OverlayTexture);
            Assert.AreSame(
                _uiContext.GetTexture(_theme.GetNormalIconImagePath(MessageType.Advice)),
                data.DetailPanel.IconTexture
            );
            Assert.IsFalse(data.DetailPanel.PreviousDisabled);
            Assert.IsFalse(data.DetailPanel.NextDisabled);
        }

        [Test]
        public void Project_AdviceWithoutExplicitImage_ReturnsConfiguredAdviceImage()
        {
            Message message = CreateMessage("advice", "Advice", MessageType.Advice);

            MessagesWindowRenderData data = MessagesWindowProjector.Project(
                _uiContext,
                new[] { message },
                MessagesTab.Advice,
                true,
                message.InstanceID,
                new[] { message.InstanceID },
                true,
                false,
                0,
                0
            );

            Assert.AreSame(
                _uiContext.GetTexture(_theme.GetDetailImagePath("advice")),
                data.DetailPanel.CardTexture
            );
            Assert.IsTrue(data.DetailPanel.PreviousDisabled);
            Assert.IsTrue(data.DetailPanel.NextDisabled);
        }

        [Test]
        public void Project_ExplicitDetailImageKey_ReturnsConfiguredDetailImage()
        {
            Message message = CreateMessage("message", "Message", MessageType.Conflict);
            MessageDetailImageTheme configuredImage = _theme.DetailImages.First();
            message.DisplayImageKey = configuredImage.Key;

            MessagesWindowRenderData data = MessagesWindowProjector.Project(
                _uiContext,
                new[] { message },
                MessagesTab.Conflict,
                true,
                message.InstanceID,
                null,
                true,
                false,
                0,
                0
            );

            Assert.AreSame(
                _uiContext.GetTexture(configuredImage.ImagePath),
                data.DetailPanel.CardTexture
            );
        }

        [Test]
        public void Project_MissingSelectedMessage_ReturnsIndexPanel()
        {
            Message message = CreateMessage("message", "Message", MessageType.Fleet);

            MessagesWindowRenderData data = MessagesWindowProjector.Project(
                _uiContext,
                new[] { message },
                MessagesTab.Fleet,
                true,
                "missing",
                null,
                true,
                false,
                0,
                0
            );

            Assert.IsFalse(data.DetailVisible);
            Assert.IsNotNull(data.IndexPanel);
            Assert.IsNull(data.DetailPanel);
        }

        [Test]
        public void Project_ChatIndex_ReturnsPressedDisabledChatCommand()
        {
            MessagesWindowRenderData data = MessagesWindowProjector.Project(
                _uiContext,
                Array.Empty<Message>(),
                MessagesTab.Chat,
                false,
                null,
                null,
                true,
                false,
                0,
                0
            );

            Assert.IsFalse(data.CommandBar.DisplayButton.Enabled);
            Assert.IsTrue(data.CommandBar.ChatButton.Visible);
            Assert.IsFalse(data.CommandBar.ChatButton.Enabled);
        }

        [Test]
        public void Project_NullContext_ReturnsPresentationWithoutTextures()
        {
            MessagesWindowRenderData data = MessagesWindowProjector.Project(
                null,
                null,
                MessagesTab.All,
                false,
                null,
                null,
                false,
                false,
                0,
                0
            );

            Assert.IsNull(data.OverlayFrameTexture);
            Assert.IsNull(data.CommandBar.ButtonStripTexture);
            Assert.IsEmpty(data.IndexPanel.Rows);
        }

        private static Message CreateMessage(string instanceId, string title, MessageType type)
        {
            return new Message(type, title) { InstanceID = instanceId };
        }
    }
}
