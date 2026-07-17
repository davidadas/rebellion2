using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Encyclopedia
{
    [TestFixture]
    public class EncyclopediaWindowProjectorTests
    {
        private const string _opposingFactionId = "FNEMP1";
        private const string _playerFactionId = "FNALL1";

        private EncyclopediaCatalog _catalog;
        private EncyclopediaEntry _firstEntry;
        private EncyclopediaEntry _secondEntry;
        private EncyclopediaEntry _thirdEntry;
        private UIContext _uiContext;
        private UIWindow _window;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            FactionThemeLibrary themes = new FactionThemeLibrary();
            string imagePath = themes.GetTheme(_playerFactionId).GalaxyBackground.ImagePath;
            _firstEntry = CreateEntry(
                "first",
                "A-Wing",
                EncyclopediaEntryCategory.Ship,
                null,
                imagePath
            );
            _secondEntry = CreateEntry(
                "second",
                "B-Wing",
                EncyclopediaEntryCategory.Ship,
                _playerFactionId,
                imagePath
            );
            _thirdEntry = CreateEntry(
                "third",
                "Corellia",
                EncyclopediaEntryCategory.System,
                null,
                imagePath
            );
            EncyclopediaEntry hidden = CreateEntry(
                "hidden",
                "Destroyer",
                EncyclopediaEntryCategory.Ship,
                _opposingFactionId,
                imagePath
            );
            _catalog = new EncyclopediaCatalog(
                new[] { _thirdEntry, hidden, _secondEntry, _firstEntry }
            );
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" }
            );
            game.Factions.Add(
                new Faction { InstanceID = _opposingFactionId, DisplayName = "Empire" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(game, themes, _catalog);
            _windowObject = new GameObject(
                "EncyclopediaWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 11, 22, 333, 222, false, true, true);
            _window.SetActiveWindow(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void CreateRenderData_MissingRequiredInput_ThrowsArgumentNullException()
        {
            EncyclopediaWindowSession session = new EncyclopediaWindowSession(_window);

            Assert.Throws<ArgumentNullException>(() =>
                EncyclopediaWindowProjector.CreateRenderData(null, _window, false, session)
            );
            Assert.Throws<ArgumentNullException>(() =>
                EncyclopediaWindowProjector.CreateRenderData(_uiContext, null, false, session)
            );
            Assert.Throws<ArgumentNullException>(() =>
                EncyclopediaWindowProjector.CreateRenderData(_uiContext, _window, false, null)
            );
        }

        [Test]
        public void CreateRenderData_SelectedTopic_ReturnsCompleteLowerLayoutPresentation()
        {
            EncyclopediaWindowSession session = new EncyclopediaWindowSession(_window);
            session.SelectTab(EncyclopediaWindowTab.Ships);
            session.SetProjectedEntries(new[] { _firstEntry, _secondEntry, _thirdEntry });
            session.ActivateRow(_secondEntry.TypeID);
            EncyclopediaWindowTheme theme = _uiContext
                .GetPlayerFactionTheme()
                .StrategyWindows.Encyclopedia;

            EncyclopediaWindowRenderData data = EncyclopediaWindowProjector.CreateRenderData(
                _uiContext,
                _window,
                false,
                session
            );

            Assert.IsTrue(data.Panel);
            Assert.AreEqual(11, data.Frame.X);
            Assert.AreEqual(22, data.Frame.Y);
            Assert.AreEqual(333, data.Frame.Width);
            Assert.AreEqual(222, data.Frame.Height);
            Assert.IsTrue(data.Frame.ActiveWindow);
            Assert.IsFalse(data.Frame.UseUpperButtonLayout);
            Assert.AreSame(
                _uiContext.GetTexture(theme.OverlayFrameImagePath),
                data.Frame.OverlayFrameTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.ButtonStripImagePath),
                data.Frame.ButtonStripTexture
            );
            Assert.AreEqual(3, data.Frame.DialogButtons.Count);
            Assert.AreEqual(EncyclopediaWindowCommand.Close, data.Frame.DialogButtons[0].Command);
            Assert.AreSame(
                _uiContext.GetTexture(theme.CloseButton.GetImagePath(false)),
                data.Frame.DialogButtons[0].Texture
            );
            Assert.AreEqual(
                EncyclopediaWindowCommand.ShowTopic,
                data.Frame.DialogButtons[1].Command
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.TopicButton.GetImagePath(true)),
                data.Frame.DialogButtons[1].Texture
            );
            Assert.IsNull(data.Frame.DialogButtons[1].SourceRect);
            Assert.AreEqual(EncyclopediaWindowTab.Ships, data.Index.ActiveTab);
            Assert.AreEqual(1, data.Index.SelectedIndex);
            Assert.AreEqual(string.Empty, data.Index.SearchText);
            Assert.AreEqual("Ship Database", data.Index.TabTitle);
            Assert.AreEqual(EncyclopediaWindowTabCatalog.Count, data.Index.Tabs.Count);
            Assert.AreEqual(EncyclopediaWindowTab.Ships, data.Index.Tabs[2].Tab);
            Assert.AreSame(
                _uiContext.GetTexture(theme.ShipButton.GetImagePath(true)),
                data.Index.Tabs[2].Texture
            );
            Assert.AreEqual(3, data.Index.Rows.Count);
            Assert.IsTrue(data.Index.Rows[1].Selected);
            Assert.AreEqual("B-Wing", data.Detail.Title);
            Assert.AreEqual(_secondEntry.GetInfoText(), data.Detail.Text);
            Assert.AreSame(_uiContext.GetTexture(_secondEntry.ImagePath), data.Detail.Image);
            Assert.IsFalse(data.Detail.PreviousDisabled);
            Assert.IsFalse(data.Detail.NextDisabled);
        }

        [Test]
        public void CreateRenderData_IndexPanelUpperLayout_ReturnsUpperCommandPresentation()
        {
            EncyclopediaWindowSession session = new EncyclopediaWindowSession(_window);
            session.SetProjectedEntries(new[] { _firstEntry, _secondEntry });
            session.SelectRow(_firstEntry.TypeID);
            EncyclopediaWindowTheme theme = _uiContext
                .GetPlayerFactionTheme()
                .StrategyWindows.Encyclopedia;

            EncyclopediaWindowRenderData data = EncyclopediaWindowProjector.CreateRenderData(
                _uiContext,
                _window,
                true,
                session
            );

            Assert.IsFalse(data.Panel);
            Assert.IsTrue(data.Frame.UseUpperButtonLayout);
            Assert.IsNull(data.Frame.ButtonStripTexture);
            Assert.AreSame(
                _uiContext.GetTexture(theme.TopicButton.GetImagePath(false)),
                data.Frame.DialogButtons[1].Texture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.IndexButton.GetImagePath(true)),
                data.Frame.DialogButtons[2].Texture
            );
            Assert.AreEqual("A-Wing", data.Detail.Title);
            Assert.IsTrue(data.Detail.PreviousDisabled);
            Assert.IsFalse(data.Detail.NextDisabled);
        }

        [Test]
        public void CreateRenderData_NoSelection_ReturnsEmptyDisabledDetail()
        {
            EncyclopediaWindowSession session = new EncyclopediaWindowSession(_window);
            session.SetSearchText("wing");
            session.SetProjectedEntries(new[] { _firstEntry, _secondEntry });

            EncyclopediaWindowRenderData data = EncyclopediaWindowProjector.CreateRenderData(
                _uiContext,
                _window,
                false,
                session
            );

            Assert.AreEqual("wing", data.Index.SearchText);
            Assert.AreEqual(string.Empty, data.Detail.Title);
            Assert.AreEqual(string.Empty, data.Detail.Text);
            Assert.IsNull(data.Detail.Image);
            Assert.IsTrue(data.Detail.PreviousDisabled);
            Assert.IsTrue(data.Detail.NextDisabled);
        }

        [Test]
        public void GetVisibleEntries_NullCatalog_ReturnsEmptyList()
        {
            List<EncyclopediaEntry> entries = EncyclopediaWindowProjector.GetVisibleEntries(
                null,
                _playerFactionId,
                default
            );

            Assert.IsEmpty(entries);
        }

        [Test]
        public void GetVisibleEntries_CategoryFactionAndSearch_ReturnsMatchingEntries()
        {
            EncyclopediaWindowState state = new EncyclopediaWindowState(
                false,
                EncyclopediaWindowTab.Ships,
                -1,
                "wing"
            );

            List<EncyclopediaEntry> entries = EncyclopediaWindowProjector.GetVisibleEntries(
                _catalog,
                _playerFactionId,
                state
            );

            Assert.AreEqual(2, entries.Count);
            Assert.AreSame(_firstEntry, entries[0]);
            Assert.AreSame(_secondEntry, entries[1]);
        }

        [Test]
        public void FilterEntries_WhitespaceSearch_ReturnsIsolatedSourceOrder()
        {
            EncyclopediaEntry[] source = { _secondEntry, null, _firstEntry };

            List<EncyclopediaEntry> entries = EncyclopediaWindowProjector.FilterEntries(
                source,
                "  "
            );
            source[0] = _thirdEntry;

            Assert.AreEqual(3, entries.Count);
            Assert.AreSame(_secondEntry, entries[0]);
            Assert.IsNull(entries[1]);
            Assert.AreSame(_firstEntry, entries[2]);
        }

        [Test]
        public void FilterEntries_SearchText_ReturnsCaseInsensitiveDisplayNameMatches()
        {
            EncyclopediaEntry[] source = { _firstEntry, null, _secondEntry, _thirdEntry };

            List<EncyclopediaEntry> entries = EncyclopediaWindowProjector.FilterEntries(
                source,
                "WING"
            );

            Assert.AreEqual(2, entries.Count);
            Assert.AreSame(_firstEntry, entries[0]);
            Assert.AreSame(_secondEntry, entries[1]);
            Assert.IsEmpty(EncyclopediaWindowProjector.FilterEntries(null, "wing"));
        }

        [Test]
        public void CreateRows_Entries_ReturnsNormalizedSelectionPresentation()
        {
            EncyclopediaEntry[] entries = { _firstEntry, null, _secondEntry };

            IReadOnlyList<EncyclopediaWindowRowRenderData> rows =
                EncyclopediaWindowProjector.CreateRows(entries, 2);

            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual("first", rows[0].EntryTypeId);
            Assert.AreEqual("A-Wing", rows[0].Name);
            Assert.IsFalse(rows[0].Selected);
            Assert.AreEqual(string.Empty, rows[1].EntryTypeId);
            Assert.AreEqual(string.Empty, rows[1].Name);
            Assert.IsFalse(rows[1].Selected);
            Assert.AreEqual("second", rows[2].EntryTypeId);
            Assert.IsTrue(rows[2].Selected);
            Assert.IsEmpty(EncyclopediaWindowProjector.CreateRows(null, 0));
        }

        private static EncyclopediaEntry CreateEntry(
            string typeId,
            string displayName,
            EncyclopediaEntryCategory category,
            string visibleFactionId,
            string imagePath
        )
        {
            return new EncyclopediaEntry
            {
                TypeID = typeId,
                DisplayName = displayName,
                Category = category,
                VisibleFactionInstanceID = visibleFactionId,
                ImagePath = imagePath,
                Stats = new List<EncyclopediaEntryStat>
                {
                    new EncyclopediaEntryStat { Label = "Class", Value = "Test" },
                },
                Description = displayName + " description",
            };
        }
    }
}
