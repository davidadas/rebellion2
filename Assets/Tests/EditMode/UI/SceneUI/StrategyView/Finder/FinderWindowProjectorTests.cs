using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowProjectorTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _opponentFactionId = "FNEMP1";

        private GameObject _windowObject;
        private UIWindow _window;
        private UIContext _uiContext;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" }
            );
            game.Factions.Add(
                new Faction { InstanceID = _opponentFactionId, DisplayName = "Empire" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _windowObject = new GameObject("FinderWindow", typeof(RectTransform), typeof(UIWindow));
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(7, 12, 34, 320, 240, false, true, true);
            _window.SetActiveWindow(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void CreateRenderData_NullContext_ThrowsArgumentNullException()
        {
            FinderWindowSession session = new FinderWindowSession(_window, FinderMode.Systems);

            Assert.Throws<ArgumentNullException>(() =>
                FinderWindowProjector.CreateRenderData(
                    null,
                    _window,
                    false,
                    session,
                    new FinderWindowTab[0]
                )
            );
        }

        [Test]
        public void CreateRenderData_NullWindow_ThrowsArgumentNullException()
        {
            FinderWindowSession session = new FinderWindowSession(_window, FinderMode.Systems);

            Assert.Throws<ArgumentNullException>(() =>
                FinderWindowProjector.CreateRenderData(
                    _uiContext,
                    null,
                    false,
                    session,
                    new FinderWindowTab[0]
                )
            );
        }

        [Test]
        public void CreateRenderData_NullSession_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                FinderWindowProjector.CreateRenderData(
                    _uiContext,
                    _window,
                    false,
                    null,
                    new FinderWindowTab[0]
                )
            );
        }

        [Test]
        public void CreateRenderData_SystemFactionTab_ReturnsCompleteThemedPresentation()
        {
            FinderWindowSession session = new FinderWindowSession(_window, FinderMode.Systems);
            List<FinderWindowTab> tabs = CreateTabs(FinderMode.Systems);
            FinderWindowRow row = CreateRow("planet", "Planet", new[] { 1, 0, 3 });
            session.SelectTab(1);
            session.SetProjection(tabs, new[] { row });
            session.SelectRow("planet");
            FinderWindowTheme theme = _uiContext.GetPlayerFactionTheme().StrategyWindows.Finder;

            FinderWindowRenderData data = FinderWindowProjector.CreateRenderData(
                _uiContext,
                _window,
                false,
                session,
                tabs
            );

            Assert.AreEqual(FinderMode.Systems, data.Mode);
            Assert.IsFalse(data.Panel);
            Assert.AreEqual(1, data.ActiveTab);
            Assert.AreEqual(0, data.SelectedIndex);
            Assert.AreEqual("Planetary System Finder", data.Title);
            Assert.AreEqual("System Name", data.Label);
            Assert.AreEqual("Rebel Systems", data.ActiveTabText);
            Assert.AreEqual(12, data.Frame.X);
            Assert.AreEqual(34, data.Frame.Y);
            Assert.AreEqual(320, data.Frame.Width);
            Assert.AreEqual(240, data.Frame.Height);
            Assert.IsTrue(data.Frame.ActiveWindow);
            Assert.IsFalse(data.Frame.UseUpperButtonLayout);
            Assert.AreSame(
                _uiContext.GetTexture(theme.SystemFinderBackgroundImagePath),
                data.Frame.BackgroundTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.OverlayFrameImagePath),
                data.Frame.OverlayFrameTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.TwoButtonStripImagePath),
                data.Frame.ButtonStripTexture
            );
            CollectionAssert.AreEqual(
                new[] { FinderWindowCommand.Close, FinderWindowCommand.Target },
                data.Frame.DialogButtons.Select(button => button.Command)
            );
            Assert.IsTrue(data.Frame.DialogButtons.All(button => button.Texture != null));
            Assert.IsTrue(data.Frame.DialogButtons.All(button => button.PressedTexture != null));
            Assert.AreEqual(tabs.Count, data.Tabs.Count);
            Assert.IsTrue(data.Tabs.All(tab => tab.Texture != null));
            Assert.IsTrue(data.Tabs.All(tab => tab.PressedTexture != null));
            Assert.AreEqual(1, data.Rows.Count);
            Assert.IsTrue(data.Rows[0].Selected);
            CollectionAssert.AreEqual(new[] { "1", "3" }, data.Rows[0].Counts);
        }

        [Test]
        public void CreateRenderData_ShipPanel_ReturnsActivePanelCommandsAndFourButtonLayout()
        {
            FinderWindowSession session = new FinderWindowSession(_window, FinderMode.Fleets);
            List<FinderWindowTab> tabs = CreateTabs(FinderMode.Fleets);
            session.SelectPanel(true);
            session.SetProjection(tabs, new FinderWindowRow[0]);
            FinderWindowTheme theme = _uiContext.GetPlayerFactionTheme().StrategyWindows.Finder;

            FinderWindowRenderData data = FinderWindowProjector.CreateRenderData(
                _uiContext,
                _window,
                false,
                session,
                tabs
            );

            Assert.AreEqual("Ship Finder", data.Title);
            Assert.AreEqual("Ship Name", data.Label);
            Assert.AreEqual("All Ships", data.ActiveTabText);
            Assert.AreSame(
                _uiContext.GetTexture(theme.ShipFinderBackgroundImagePath),
                data.Frame.BackgroundTexture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.FourButtonStripImagePath),
                data.Frame.ButtonStripTexture
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    FinderWindowCommand.Close,
                    FinderWindowCommand.Target,
                    FinderWindowCommand.ShowShips,
                    FinderWindowCommand.ShowFleets,
                },
                data.Frame.DialogButtons.Select(button => button.Command)
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.ShipButton.GetImagePath(true)),
                data.Frame.DialogButtons[2].Texture
            );
            Assert.AreSame(
                _uiContext.GetTexture(theme.FleetButton.GetImagePath(false)),
                data.Frame.DialogButtons[3].Texture
            );
        }

        [Test]
        public void CreateRenderData_UpperButtonLayout_OmitsButtonStrip()
        {
            FinderWindowSession session = new FinderWindowSession(_window, FinderMode.Personnel);
            List<FinderWindowTab> tabs = CreateTabs(FinderMode.Personnel);
            session.SetProjection(tabs, new FinderWindowRow[0]);

            FinderWindowRenderData data = FinderWindowProjector.CreateRenderData(
                _uiContext,
                _window,
                true,
                session,
                tabs
            );

            Assert.IsTrue(data.Frame.UseUpperButtonLayout);
            Assert.IsNull(data.Frame.ButtonStripTexture);
            CollectionAssert.AreEqual(
                new[]
                {
                    FinderWindowCommand.Close,
                    FinderWindowCommand.Target,
                    FinderWindowCommand.ShowPersonnel,
                    FinderWindowCommand.ShowSpecialForces,
                },
                data.Frame.DialogButtons.Select(button => button.Command)
            );
        }

        [TestCase(1, "Rebel Systems")]
        [TestCase(2, "Imperial Systems")]
        [TestCase(3, "Neutral Systems")]
        [TestCase(4, "Unexplored Systems")]
        public void CreateRenderData_SystemTab_ReturnsExpectedTabText(
            int activeTab,
            string expected
        )
        {
            FinderWindowSession session = new FinderWindowSession(_window, FinderMode.Systems);
            List<FinderWindowTab> tabs = CreateTabs(FinderMode.Systems);
            session.SelectTab(activeTab);
            session.SetProjection(tabs, new FinderWindowRow[0]);

            FinderWindowRenderData data = FinderWindowProjector.CreateRenderData(
                _uiContext,
                _window,
                false,
                session,
                tabs
            );

            Assert.AreEqual(expected, data.ActiveTabText);
        }

        [TestCase(FinderMode.Fleets, false, "Alliance Fleets")]
        [TestCase(FinderMode.Fleets, true, "Alliance Ships")]
        [TestCase(FinderMode.Troops, false, "Alliance Troops")]
        [TestCase(FinderMode.Personnel, false, "Alliance Personnel")]
        [TestCase(FinderMode.Personnel, true, "Alliance Personnel")]
        public void CreateRenderData_FactionTab_ReturnsConfiguredFactionText(
            FinderMode mode,
            bool panel,
            string expected
        )
        {
            FinderWindowSession session = new FinderWindowSession(_window, mode);
            List<FinderWindowTab> tabs = CreateTabs(mode);
            if (panel)
                session.SelectPanel(true);
            int playerTab = tabs.FindIndex(tab => tab.FactionInstanceId == _playerFactionId);
            session.SelectTab(playerTab);
            session.SetProjection(tabs, new FinderWindowRow[0]);

            FinderWindowRenderData data = FinderWindowProjector.CreateRenderData(
                _uiContext,
                _window,
                false,
                session,
                tabs
            );

            Assert.AreEqual(expected, data.ActiveTabText);
        }

        [Test]
        public void CreateRenderData_MissingTabs_ReturnsEmptyTabPresentationAndTitle()
        {
            FinderWindowSession session = new FinderWindowSession(_window, FinderMode.Systems);

            FinderWindowRenderData data = FinderWindowProjector.CreateRenderData(
                _uiContext,
                _window,
                false,
                session,
                null
            );

            Assert.AreEqual(string.Empty, data.ActiveTabText);
            Assert.IsEmpty(data.Tabs);
        }

        [Test]
        public void FilterRows_NullRows_ReturnsEmptyList()
        {
            List<FinderWindowRow> rows = FinderWindowProjector.FilterRows(null, "query");

            Assert.IsEmpty(rows);
        }

        [Test]
        public void FilterRows_BlankSearch_ReturnsIndependentSourceOrder()
        {
            FinderWindowRow first = CreateRow("first", "First", null);
            FinderWindowRow second = CreateRow("second", "Second", null);
            FinderWindowRow[] source = { first, second };

            List<FinderWindowRow> rows = FinderWindowProjector.FilterRows(source, "  ");
            source[0] = second;

            CollectionAssert.AreEqual(new[] { first, second }, rows);
        }

        [Test]
        public void FilterRows_SearchText_ReturnsCaseInsensitiveMatchesInSourceOrder()
        {
            FinderWindowRow[] source =
            {
                CreateRow("alpha", "Alpha Prime", null),
                null,
                CreateRow("beta", "Beta", null),
                CreateRow("gamma", "Gamma alpha", null),
            };

            List<FinderWindowRow> rows = FinderWindowProjector.FilterRows(source, "ALPHA");

            CollectionAssert.AreEqual(
                new[] { "Alpha Prime", "Gamma alpha" },
                rows.Select(row => row.Name)
            );
        }

        [Test]
        public void CreateRows_RowsAndSelection_ReturnsNormalizedImmutablePresentation()
        {
            FinderWindowRow[] rows =
            {
                CreateRow("first", null, new[] { 0, 2, -1, 5 }),
                CreateRow("second", "Second", null),
                null,
            };

            IReadOnlyList<FinderWindowRowRenderData> data = FinderWindowProjector.CreateRows(
                rows,
                1
            );
            rows[0] = null;

            Assert.AreEqual("first", data[0].RowId);
            Assert.AreEqual(string.Empty, data[0].Name);
            Assert.IsFalse(data[0].Selected);
            CollectionAssert.AreEqual(new[] { "2", "5" }, data[0].Counts);
            Assert.IsTrue(data[1].Selected);
            Assert.AreEqual(string.Empty, data[2].RowId);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<FinderWindowRowRenderData>)data)[0] = null
            );
        }

        [TestCase(FinderMode.Systems, false, "Planetary System Finder")]
        [TestCase(FinderMode.Systems, true, "Planetary System Finder")]
        [TestCase(FinderMode.Fleets, false, "Fleet Finder")]
        [TestCase(FinderMode.Fleets, true, "Ship Finder")]
        [TestCase(FinderMode.Troops, false, "Troop Finder")]
        [TestCase(FinderMode.Personnel, false, "Personnel Finder")]
        [TestCase(FinderMode.Personnel, true, "Special Forces Finder")]
        [TestCase((FinderMode)99, false, "")]
        public void GetWindowTitle_ModeAndPanel_ReturnsExpectedTitle(
            FinderMode mode,
            bool panel,
            string expected
        )
        {
            string title = FinderWindowProjector.GetWindowTitle(mode, panel);

            Assert.AreEqual(expected, title);
        }

        [TestCase(FinderMode.Systems, false, "System Name")]
        [TestCase(FinderMode.Systems, true, "System Name")]
        [TestCase(FinderMode.Fleets, false, "Fleet Name")]
        [TestCase(FinderMode.Fleets, true, "Ship Name")]
        [TestCase(FinderMode.Troops, false, "Troop Location")]
        [TestCase(FinderMode.Personnel, false, "Personnel Name")]
        [TestCase(FinderMode.Personnel, true, "Special Forces Location")]
        [TestCase((FinderMode)99, false, "")]
        public void GetWindowLabel_ModeAndPanel_ReturnsExpectedLabel(
            FinderMode mode,
            bool panel,
            string expected
        )
        {
            string label = FinderWindowProjector.GetWindowLabel(mode, panel);

            Assert.AreEqual(expected, label);
        }

        private List<FinderWindowTab> CreateTabs(FinderMode mode)
        {
            return FinderWindowTabCatalog.Create(mode, _uiContext.Game.Factions, _playerFactionId);
        }

        private static FinderWindowRow CreateRow(
            string instanceId,
            string name,
            IReadOnlyList<int> counts
        )
        {
            Planet planet = new Planet
            {
                InstanceID = instanceId,
                DisplayName = name,
                OwnerInstanceID = _playerFactionId,
            };
            return new FinderWindowRow(
                name,
                new GalaxyMapPlanet(new GamePlanetSystem(), planet, string.Empty),
                node: planet,
                counts: counts
            );
        }
    }
}
