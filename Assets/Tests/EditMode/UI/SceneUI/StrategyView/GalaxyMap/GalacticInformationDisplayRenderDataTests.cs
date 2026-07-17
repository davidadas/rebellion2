using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalacticInformationDisplayRenderDataTests
    {
        private Texture2D _firstTexture;
        private Texture2D _secondTexture;

        [SetUp]
        public void SetUp()
        {
            _firstTexture = new Texture2D(1, 1);
            _secondTexture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_firstTexture);
            UnityEngine.Object.DestroyImmediate(_secondTexture);
        }

        [Test]
        public void State_Values_PreservesInteractionState()
        {
            GalacticInformationDisplayState state = new GalacticInformationDisplayState(
                true,
                2,
                3,
                true
            );

            Assert.IsTrue(state.Visible);
            Assert.AreEqual(2, state.ActiveCategoryIndex);
            Assert.AreEqual(3, state.HoveredFilterIndex);
            Assert.IsTrue(state.DisplayOffHovered);
        }

        [Test]
        public void Display_SourceChanges_PreservesReadOnlyCategorySnapshot()
        {
            GalacticInformationCategoryRenderData category = CreateCategory();
            GalacticInformationCategoryRenderData[] categories = { category };
            GalacticInformationFrameRenderData frame = new GalacticInformationFrameRenderData(
                3,
                4,
                null
            );
            GalacticInformationTextRowRenderData displayOff =
                new GalacticInformationTextRowRenderData(
                    true,
                    new RectInt(5, 6, 7, 8),
                    new GalacticInformationTextRenderData(
                        "Display Off",
                        Color.white,
                        new RectInt(5, 6, 7, 8)
                    )
                );

            GalacticInformationDisplayRenderData data = new GalacticInformationDisplayRenderData(
                true,
                new RectInt(1, 2, 3, 4),
                Color.black,
                frame,
                categories,
                displayOff
            );
            categories[0] = null;

            Assert.IsTrue(data.Visible);
            Assert.AreEqual(new RectInt(1, 2, 3, 4), data.SelectorBounds);
            Assert.AreEqual(Color.black, data.BackgroundColor);
            Assert.AreSame(frame, data.Frame);
            Assert.AreSame(category, data.Categories[0]);
            Assert.AreEqual("Display Off", data.DisplayOffRow.Label.Text);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<GalacticInformationCategoryRenderData>)data.Categories)[0] = null
            );
        }

        [Test]
        public void Category_Values_PreservesCompletePresentation()
        {
            GalacticInformationSubmenuRenderData submenu = new GalacticInformationSubmenuRenderData(
                true,
                new RectInt(1, 2, 3, 4),
                Color.black,
                null,
                null
            );
            GalacticInformationImageRenderData icon = new GalacticInformationImageRenderData(
                _firstTexture,
                new RectInt(5, 6, 7, 8)
            );
            GalacticInformationImageRenderData arrow = new GalacticInformationImageRenderData(
                _secondTexture,
                new RectInt(9, 10, 11, 12)
            );
            GalacticInformationTextRenderData label = new GalacticInformationTextRenderData(
                "Category",
                Color.green,
                new RectInt(13, 14, 15, 16)
            );

            GalacticInformationCategoryRenderData data = new GalacticInformationCategoryRenderData(
                true,
                new RectInt(17, 18, 19, 20),
                icon,
                arrow,
                label,
                submenu
            );

            Assert.IsTrue(data.Visible);
            Assert.AreEqual(new RectInt(17, 18, 19, 20), data.HitBounds);
            Assert.AreSame(_firstTexture, data.Icon.Texture);
            Assert.AreSame(_secondTexture, data.Arrow.Texture);
            Assert.AreEqual("Category", data.Label.Text);
            Assert.AreSame(submenu, data.Submenu);
        }

        [Test]
        public void Submenu_SourceChanges_PreservesReadOnlyFilterSnapshot()
        {
            GalacticInformationFilterRenderData filter = new GalacticInformationFilterRenderData(
                GalacticInformationFilterMode.Uprisings,
                true,
                new RectInt(1, 2, 3, 4),
                default,
                default
            );
            GalacticInformationFilterRenderData[] filters = { filter };

            GalacticInformationSubmenuRenderData data = new GalacticInformationSubmenuRenderData(
                true,
                new RectInt(5, 6, 7, 8),
                Color.red,
                null,
                filters
            );
            filters[0] = null;

            Assert.IsTrue(data.Visible);
            Assert.AreEqual(new RectInt(5, 6, 7, 8), data.Bounds);
            Assert.AreEqual(Color.red, data.BackgroundColor);
            Assert.AreSame(filter, data.Filters[0]);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<GalacticInformationFilterRenderData>)data.Filters)[0] = null
            );
        }

        [Test]
        public void Filter_Values_PreservesCompletePresentation()
        {
            GalacticInformationImageRenderData icon = new GalacticInformationImageRenderData(
                _firstTexture,
                new RectInt(1, 2, 3, 4)
            );
            GalacticInformationTextRenderData label = new GalacticInformationTextRenderData(
                null,
                Color.cyan,
                new RectInt(5, 6, 7, 8)
            );

            GalacticInformationFilterRenderData data = new GalacticInformationFilterRenderData(
                GalacticInformationFilterMode.FleetsEnroute,
                true,
                new RectInt(9, 10, 11, 12),
                icon,
                label
            );

            Assert.AreEqual(GalacticInformationFilterMode.FleetsEnroute, data.Mode);
            Assert.IsTrue(data.Visible);
            Assert.AreEqual(new RectInt(9, 10, 11, 12), data.HitBounds);
            Assert.AreSame(_firstTexture, data.Icon.Texture);
            Assert.AreEqual(string.Empty, data.Label.Text);
            Assert.AreEqual(Color.cyan, data.Label.Color);
            Assert.AreEqual(new RectInt(5, 6, 7, 8), data.Label.Bounds);
        }

        [Test]
        public void TextRow_Values_PreservesLabelPresentation()
        {
            GalacticInformationTextRenderData label = new GalacticInformationTextRenderData(
                "Display Off",
                Color.yellow,
                new RectInt(1, 2, 3, 4)
            );

            GalacticInformationTextRowRenderData data = new GalacticInformationTextRowRenderData(
                true,
                new RectInt(5, 6, 7, 8),
                label
            );

            Assert.IsTrue(data.Visible);
            Assert.AreEqual(new RectInt(5, 6, 7, 8), data.HitBounds);
            Assert.AreEqual("Display Off", data.Label.Text);
            Assert.AreEqual(Color.yellow, data.Label.Color);
        }

        [Test]
        public void Frame_SourceChanges_PreservesReadOnlyTextureSnapshot()
        {
            Texture2D[] textures = { _firstTexture, _secondTexture };

            GalacticInformationFrameRenderData data = new GalacticInformationFrameRenderData(
                10,
                20,
                textures
            );
            textures[0] = null;

            Assert.AreEqual(10, data.Width);
            Assert.AreEqual(20, data.Height);
            Assert.AreSame(_firstTexture, data.Textures[0]);
            Assert.AreSame(_secondTexture, data.Textures[1]);
            Assert.Throws<NotSupportedException>(() => ((IList<Texture2D>)data.Textures)[0] = null);
        }

        [Test]
        public void Legend_Values_PreservesCompletePresentation()
        {
            GalacticInformationFrameRenderData frame = new GalacticInformationFrameRenderData(
                10,
                20,
                null
            );

            GalacticInformationLegendRenderData data = new GalacticInformationLegendRenderData(
                new RectInt(1, 2, 3, 4),
                _firstTexture,
                frame,
                new RectInt(5, 6, 7, 8),
                _secondTexture,
                _firstTexture
            );

            Assert.AreEqual(new RectInt(1, 2, 3, 4), data.Bounds);
            Assert.AreSame(_firstTexture, data.Texture);
            Assert.AreSame(frame, data.Frame);
            Assert.AreEqual(new RectInt(5, 6, 7, 8), data.CloseBounds);
            Assert.AreSame(_secondTexture, data.CloseTexture);
            Assert.AreSame(_firstTexture, data.ClosePressedTexture);
        }

        private GalacticInformationCategoryRenderData CreateCategory()
        {
            return new GalacticInformationCategoryRenderData(
                true,
                new RectInt(1, 2, 3, 4),
                new GalacticInformationImageRenderData(_firstTexture, default),
                new GalacticInformationImageRenderData(_secondTexture, default),
                new GalacticInformationTextRenderData("Category", Color.white, default),
                null
            );
        }
    }
}
