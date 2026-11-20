using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalacticInformationDisplayProjectorTests
    {
        private const string _playerFactionId = "FNALL1";

        private GalacticInformationDisplayProjector _projector;
        private UIContext _uiContext;
        private GalacticInformationFilterTheme _minesFilter;
        private (int Low, int Medium, int High, string Image) _savedMinesFilter;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions()
                .Add(new Faction { InstanceID = _playerFactionId, DisplayName = "Alliance" });
            game.Summary.PlayerFactionID = _playerFactionId;
            game.SetFactionController(_playerFactionId, "PLAYER1", PlayerControllerType.Human);
            _uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _projector = new GalacticInformationDisplayProjector(() => _uiContext);
            _minesFilter = _uiContext
                .GetPlayerFactionTheme()
                .GalacticInformationDisplay.GetFilter(GalacticInformationFilterMode.Mines);
            _savedMinesFilter = (
                _minesFilter.LowThreshold,
                _minesFilter.MediumThreshold,
                _minesFilter.HighThreshold,
                _minesFilter.LegendImagePath
            );
        }

        /// <summary>
        /// Restores shared theme data after isolated marker-range scenarios.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_minesFilter != null)
                (
                    _minesFilter.LowThreshold,
                    _minesFilter.MediumThreshold,
                    _minesFilter.HighThreshold,
                    _minesFilter.LegendImagePath
                ) = _savedMinesFilter;
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new GalacticInformationDisplayProjector(null)
            );
        }

        [Test]
        public void Project_HiddenState_ReturnsEmptyPresentationWithoutContext()
        {
            GalacticInformationDisplayProjector projector = new GalacticInformationDisplayProjector(
                () =>
                    null
            );

            GalacticInformationDisplayRenderData data = projector.Project(default);

            Assert.IsFalse(data.Visible);
            Assert.AreEqual(default(RectInt), data.SelectorBounds);
            Assert.AreEqual(Color.clear, data.BackgroundColor);
            Assert.IsNull(data.Frame);
            Assert.IsEmpty(data.Categories);
            Assert.IsFalse(data.DisplayOffRow.Visible);
        }

        [Test]
        public void Project_VisibleStateWithoutContext_ThrowsInvalidOperationException()
        {
            GalacticInformationDisplayProjector projector = new GalacticInformationDisplayProjector(
                () =>
                    null
            );
            GalacticInformationDisplayState state = new GalacticInformationDisplayState(
                true,
                -1,
                -1,
                false,
                GalacticInformationFilterMode.DisplayOff
            );

            Assert.Throws<InvalidOperationException>(() => projector.Project(state));
        }

        [Test]
        public void Project_ActiveCategory_ReturnsAuthoredSelectorPresentation()
        {
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            GalacticInformationDisplayTheme theme = playerTheme.GalacticInformationDisplay;
            GalacticInformationCategoryTheme firstCategory = theme.Categories[0];
            GalacticInformationCategoryTheme secondCategory = theme.Categories[1];
            GalacticInformationDisplayState state = new GalacticInformationDisplayState(
                true,
                0,
                0,
                false,
                GalacticInformationFilterMode.DisplayOff
            );

            GalacticInformationDisplayRenderData data = _projector.Project(state);

            Assert.IsTrue(data.Visible);
            Assert.AreEqual(ToRect(theme.SelectorSourceLayout), data.SelectorBounds);
            Assert.AreEqual(theme.GetBackgroundColor(), data.BackgroundColor);
            Assert.AreEqual(theme.SelectorSourceLayout.Width, data.Frame.Width);
            Assert.AreEqual(theme.SelectorSourceLayout.Height, data.Frame.Height);
            Assert.AreEqual(8, data.Frame.Textures.Count);
            for (int i = 0; i < data.Frame.Textures.Count; i++)
            {
                Assert.AreSame(
                    _uiContext.GetTexture(theme.Frame.GetImagePath(i)),
                    data.Frame.Textures[i]
                );
            }
            Assert.AreEqual(theme.Categories.Count, data.Categories.Count);
            GalacticInformationCategoryRenderData active = data.Categories[0];
            Assert.IsTrue(active.Visible);
            Assert.AreEqual(ToRect(firstCategory.RowSourceLayout), active.HitBounds);
            Assert.AreSame(_uiContext.GetTexture(firstCategory.IconImagePath), active.Icon.Texture);
            Assert.AreSame(
                _uiContext.GetTexture(theme.SubmenuArrowActiveImagePath),
                active.Arrow.Texture
            );
            Assert.AreEqual(firstCategory.Label, active.Label.Text);
            Assert.AreEqual(playerTheme.GetPrimaryColor(), active.Label.Color);
            Assert.IsTrue(active.Submenu.Visible);
            Assert.AreEqual(ToRect(firstCategory.SubmenuSourceLayout), active.Submenu.Bounds);
            Assert.AreEqual(theme.GetBackgroundColor(), active.Submenu.BackgroundColor);
            Assert.AreEqual(firstCategory.Filters.Count, active.Submenu.Filters.Count);
            Assert.AreEqual(firstCategory.Filters[0].Mode, active.Submenu.Filters[0].Mode);
            Assert.AreEqual(playerTheme.GetPrimaryColor(), active.Submenu.Filters[0].Label.Color);
            Assert.AreEqual(Color.white, active.Submenu.Filters[1].Label.Color);
            GalacticInformationCategoryRenderData inactive = data.Categories[1];
            Assert.AreSame(
                _uiContext.GetTexture(theme.SubmenuArrowInactiveImagePath),
                inactive.Arrow.Texture
            );
            Assert.AreEqual(secondCategory.Label, inactive.Label.Text);
            Assert.AreEqual(Color.white, inactive.Label.Color);
            Assert.IsFalse(inactive.Submenu.Visible);
            Assert.IsTrue(data.DisplayOffRow.Visible);
            Assert.AreEqual(theme.DisplayOffLabel, data.DisplayOffRow.Label.Text);
            Assert.AreEqual(Color.white, data.DisplayOffRow.Label.Color);
            Assert.IsNull(data.DisplayOffRow.CheckMark.Texture);
        }

        [Test]
        public void Project_DisplayOffHovered_ReturnsFactionHighlight()
        {
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            GalacticInformationDisplayState state = new GalacticInformationDisplayState(
                true,
                -1,
                -1,
                true,
                GalacticInformationFilterMode.DisplayOff
            );

            GalacticInformationDisplayRenderData data = _projector.Project(state);

            Assert.AreEqual(playerTheme.GetPrimaryColor(), data.DisplayOffRow.Label.Color);
            foreach (GalacticInformationCategoryRenderData category in data.Categories)
            {
                Assert.AreEqual(Color.white, category.Label.Color);
                Assert.IsFalse(category.Submenu.Visible);
            }
        }

        [Test]
        public void ProjectLegend_ConfiguredFilter_ReturnsNativeLegendAndThemedFrame()
        {
            GalacticInformationDisplayTheme theme = _uiContext
                .GetPlayerFactionTheme()
                .GalacticInformationDisplay;
            GalacticInformationFilterTheme filter = theme.GetFilter(
                GalacticInformationFilterMode.Uprisings
            );
            Vector2Int position = new Vector2Int(17, 23);

            GalacticInformationLegendRenderData data = _projector.ProjectLegend(
                filter.Mode,
                position
            );

            Assert.IsNotNull(data);
            Assert.AreEqual(position, data.Bounds.position);
            Assert.AreEqual(filter.Label, data.Title.Text);
            Assert.AreEqual(theme.GetBackgroundColor(), data.BackgroundColor);
            CollectionAssert.AreEqual(
                new[] { "Uprisings", "No uprisings" },
                data.Rows.Take(2).Select(row => row.Label)
            );
            CollectionAssert.AreEqual(
                new[] { "Alliance", "Neutral", "Unexplored" },
                data.Rows.Skip(2).Select(row => row.Label)
            );
            Assert.AreEqual(data.Bounds.width, data.Frame.Width);
            Assert.AreEqual(data.Bounds.height, data.Frame.Height);
            Assert.AreEqual(8, data.Frame.Textures.Count);
            for (int i = 0; i < data.Frame.Textures.Count; i++)
            {
                Assert.AreSame(
                    _uiContext.GetTexture(theme.Frame.GetImagePath(i)),
                    data.Frame.Textures[i]
                );
            }
            Assert.IsTrue(
                new RectInt(0, 0, data.Bounds.width, data.Bounds.height).Contains(
                    data.CloseBounds.position
                )
            );
            Assert.IsTrue(
                data.Rows.All(row =>
                    row.Bounds.xMax <= data.Bounds.width && row.Bounds.yMax <= data.Bounds.height
                )
            );
        }

        [Test]
        public void ProjectLegend_CustomThresholds_UsesConfiguredRangesWithoutLegendBitmap()
        {
            GalacticInformationFilterTheme filter = _uiContext
                .GetPlayerFactionTheme()
                .GalacticInformationDisplay.GetFilter(GalacticInformationFilterMode.Mines);
            filter.LowThreshold = 2;
            filter.MediumThreshold = 5;
            filter.HighThreshold = 9;
            filter.LegendImagePath = "missing-bitmap";

            GalacticInformationLegendRenderData data = _projector.ProjectLegend(filter.Mode);

            CollectionAssert.AreEqual(
                new[] { "9+", "5–8", "2–4", "0–1" },
                data.Rows.Take(4).Select(row => row.Label)
            );
        }

        [Test]
        public void ProjectLegend_OverlappingThresholds_OmitsUnreachableBands()
        {
            GalacticInformationFilterTheme filter = _uiContext
                .GetPlayerFactionTheme()
                .GalacticInformationDisplay.GetFilter(GalacticInformationFilterMode.Mines);
            filter.LowThreshold = 10;
            filter.MediumThreshold = 2;
            filter.HighThreshold = 6;

            GalacticInformationLegendRenderData data = _projector.ProjectLegend(filter.Mode);

            CollectionAssert.AreEqual(
                new[] { "6+", "2–5", "0–1" },
                data.Rows.Take(3).Select(row => row.Label)
            );
        }

        [Test]
        public void ProjectLegend_PopularSupport_ShowsPercentagesWithinSupportBounds()
        {
            GalacticInformationLegendRenderData data = _projector.ProjectLegend(
                GalacticInformationFilterMode.PopularSupport
            );

            CollectionAssert.AreEqual(
                new[] { "81–100%", "60–80%", "50–59%", "0–49%" },
                data.Rows.Take(4).Select(row => row.Label)
            );
        }

        [TestCase(GalacticInformationFilterMode.IdleFleets)]
        [TestCase(GalacticInformationFilterMode.FleetsEnroute)]
        [TestCase(GalacticInformationFilterMode.FleetWaypoints)]
        [TestCase(GalacticInformationFilterMode.IdlePersonnel)]
        [TestCase(GalacticInformationFilterMode.ActivePersonnel)]
        public void ProjectLegend_FactionPresenceFilter_ShowsMixedPresenceKey(
            GalacticInformationFilterMode mode
        )
        {
            GalacticInformationLegendRenderData data = _projector.ProjectLegend(mode);

            Assert.IsTrue(data.Rows.Any(row => row.Label == "Mixed presence"));
        }

        [Test]
        public void ProjectLegend_UnconfiguredFilter_ReturnsNull()
        {
            GalacticInformationLegendRenderData data = _projector.ProjectLegend(
                GalacticInformationFilterMode.DisplayOff,
                Vector2Int.zero
            );

            Assert.IsNull(data);
        }

        [Test]
        public void ProjectLegend_PendingTextures_ReturnsReadableNativeText()
        {
            UIContext context = new UIContext(
                _uiContext.Game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>()),
                _ => null
            );
            GalacticInformationDisplayProjector projector = new(() => context);

            GalacticInformationLegendRenderData data = projector.ProjectLegend(
                GalacticInformationFilterMode.Mines
            );

            Assert.IsNotNull(data);
            Assert.AreEqual("Mines", data.Title.Text);
            Assert.IsNotEmpty(data.Rows);
            Assert.IsTrue(data.Rows.All(row => row.Texture == null));
        }

        /// <summary>
        /// Executes to rect.
        /// </summary>
        /// <param name="layout">The layout.</param>
        /// <returns>The result of to rect.</returns>
        private static RectInt ToRect(SourceRectLayout layout)
        {
            return new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
        }
    }
}
