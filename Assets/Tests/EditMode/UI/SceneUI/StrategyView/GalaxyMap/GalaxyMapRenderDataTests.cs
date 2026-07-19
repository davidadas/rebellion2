using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalaxyMapRenderDataTests
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
        public void ActiveFilterLabel_NullText_ReturnsInvisibleNormalizedPresentation()
        {
            GalaxyMapActiveFilterLabelRenderData data = new GalaxyMapActiveFilterLabelRenderData(
                null,
                Color.red,
                new RectInt(1, 2, 3, 4),
                5
            );

            Assert.IsFalse(data.Visible);
            Assert.AreEqual(string.Empty, data.Text);
            Assert.AreEqual(Color.red, data.Color);
            Assert.AreEqual(new RectInt(1, 2, 3, 4), data.Bounds);
            Assert.AreEqual(5, data.FontSize);
        }

        [Test]
        public void Star_Values_PreservesNormalizedPresentation()
        {
            GalaxyMapStarRenderData data = new GalaxyMapStarRenderData(
                null,
                1,
                2,
                _firstTexture,
                _secondTexture
            );

            Assert.AreEqual(string.Empty, data.PlanetInstanceId);
            Assert.AreEqual(1, data.SourceX);
            Assert.AreEqual(2, data.SourceY);
            Assert.AreSame(_firstTexture, data.StarTexture);
            Assert.AreSame(_secondTexture, data.HeadquartersTexture);
        }

        [Test]
        public void Cluster_MissingSystemIdentifier_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new GalaxyMapClusterRenderData(null, 0, 0, null, false, null)
            );
        }

        [Test]
        public void Cluster_SourceChanges_PreservesNormalizedSnapshot()
        {
            GalaxyMapStarRenderData star = new GalaxyMapStarRenderData(
                "planet",
                1,
                2,
                _firstTexture,
                null
            );
            GalaxyMapStarRenderData[] stars = { star };

            GalaxyMapClusterRenderData data = new GalaxyMapClusterRenderData(
                "system",
                3,
                4,
                null,
                true,
                stars
            );
            stars[0] = null;

            Assert.AreEqual("system", data.SystemInstanceId);
            Assert.AreEqual(3, data.SourceX);
            Assert.AreEqual(4, data.SourceY);
            Assert.AreEqual(string.Empty, data.Label);
            Assert.IsTrue(data.ShowLabel);
            Assert.AreSame(star, data.Stars[0]);
        }

        [Test]
        public void Map_SourceChanges_PreservesCompleteSnapshot()
        {
            GalaxyMapClusterRenderData cluster = new GalaxyMapClusterRenderData(
                "system",
                1,
                2,
                "System",
                false,
                null
            );
            GalaxyMapClusterRenderData[] clusters = { cluster };
            RectInt bounds = new RectInt(3, 4, 5, 6);
            GalaxyMapActiveFilterLabelRenderData label = new GalaxyMapActiveFilterLabelRenderData(
                "Filter",
                Color.green,
                new RectInt(7, 8, 9, 10),
                11
            );

            GalaxyMapRenderData data = new GalaxyMapRenderData(
                _firstTexture,
                bounds,
                label,
                clusters
            );
            clusters[0] = null;

            Assert.AreSame(_firstTexture, data.BackgroundTexture);
            Assert.AreEqual(bounds, data.BackgroundBounds);
            Assert.AreEqual("Filter", data.ActiveFilterLabel.Text);
            Assert.AreSame(cluster, data.Clusters[0]);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<GalaxyMapClusterRenderData>)data.Clusters)[0] = null
            );
        }

        [Test]
        public void Map_NullClusters_ReturnsEmptySnapshot()
        {
            GalaxyMapRenderData data = new GalaxyMapRenderData(null, null, default, null);

            Assert.IsNull(data.BackgroundTexture);
            Assert.IsNull(data.BackgroundBounds);
            Assert.IsFalse(data.ActiveFilterLabel.Visible);
            Assert.IsEmpty(data.Clusters);
        }
    }
}
