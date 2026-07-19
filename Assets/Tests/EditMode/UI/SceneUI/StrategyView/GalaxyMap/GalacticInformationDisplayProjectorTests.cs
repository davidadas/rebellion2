using System;
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
            _projector = new GalacticInformationDisplayProjector(() => _uiContext);
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
                false
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
                false
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
        }

        [Test]
        public void Project_DisplayOffHovered_ReturnsFactionHighlight()
        {
            FactionTheme playerTheme = _uiContext.GetPlayerFactionTheme();
            GalacticInformationDisplayState state = new GalacticInformationDisplayState(
                true,
                -1,
                -1,
                true
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
        public void ProjectLegend_ConfiguredFilter_ReturnsAuthoredLegendPresentation()
        {
            GalacticInformationDisplayTheme theme = _uiContext
                .GetPlayerFactionTheme()
                .GalacticInformationDisplay;
            GalacticInformationFilterTheme filter = theme.GetFilter(
                GalacticInformationFilterMode.Uprisings
            );
            Texture2D legendTexture = _uiContext.GetTexture(filter.LegendImagePath);
            Texture2D closeTexture = _uiContext.GetTexture(theme.CloseUpImagePath);
            Vector2Int legendSize = UILayout.GetTextureSourceSize(legendTexture);
            Vector2Int closeSize = UILayout.GetTextureSourceSize(closeTexture);
            Vector2Int position = new Vector2Int(17, 23);

            GalacticInformationLegendRenderData data = _projector.ProjectLegend(
                filter.Mode,
                position
            );

            Assert.IsNotNull(data);
            Assert.AreEqual(
                new RectInt(position.x, position.y, legendSize.x, legendSize.y),
                data.Bounds
            );
            Assert.AreSame(legendTexture, data.Texture);
            Assert.AreEqual(legendSize.x, data.Frame.Width);
            Assert.AreEqual(legendSize.y, data.Frame.Height);
            Assert.AreEqual(8, data.Frame.Textures.Count);
            for (int i = 0; i < data.Frame.Textures.Count; i++)
            {
                Assert.AreSame(
                    _uiContext.GetTexture(theme.Frame.GetImagePath(i)),
                    data.Frame.Textures[i]
                );
            }
            Assert.AreEqual(
                new RectInt(
                    legendSize.x - closeSize.x - theme.CloseSourceInset.X,
                    theme.CloseSourceInset.Y,
                    closeSize.x,
                    closeSize.y
                ),
                data.CloseBounds
            );
            Assert.AreSame(closeTexture, data.CloseTexture);
            Assert.AreSame(
                _uiContext.GetTexture(theme.ClosePressedImagePath),
                data.ClosePressedTexture
            );
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

        private static RectInt ToRect(SourceRectLayout layout)
        {
            return new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
        }
    }
}
