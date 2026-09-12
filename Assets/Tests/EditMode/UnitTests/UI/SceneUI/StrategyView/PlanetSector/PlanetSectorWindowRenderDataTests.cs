using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSector
{
    [TestFixture]
    public class PlanetSectorWindowRenderDataTests
    {
        private PlanetSectorBarRenderData _bar;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _bar = new PlanetSectorBarRenderData(
                true,
                4,
                2,
                0.5f,
                new Color32(1, 2, 3, 4),
                new Color32(5, 6, 7, 8),
                new Color32(9, 10, 11, 12)
            );
            _texture = new Texture2D(1, 1);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
        }

        [Test]
        public void Window_SourceChanges_PreservesReadOnlyPlanetSnapshot()
        {
            PlanetSectorPlanetRenderData planet = CreatePlanetData(_bar, _bar, _bar);
            PlanetSectorPlanetRenderData[] planets = { planet };

            PlanetSectorWindowRenderData data = new PlanetSectorWindowRenderData(null, planets);
            planets[0] = null;

            Assert.AreEqual(string.Empty, data.Title);
            Assert.AreSame(planet, data.Planets[0]);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<PlanetSectorPlanetRenderData>)data.Planets)[0] = null
            );
        }

        [Test]
        public void Window_NullPlanets_ReturnsEmptySnapshot()
        {
            PlanetSectorWindowRenderData data = new PlanetSectorWindowRenderData("Sector", null);

            Assert.AreEqual("Sector", data.Title);
            Assert.IsEmpty(data.Planets);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Planet_MissingBar_ThrowsArgumentNullException(int missingBarIndex)
        {
            PlanetSectorBarRenderData energyBar = missingBarIndex == 0 ? null : _bar;
            PlanetSectorBarRenderData rawResourceBar = missingBarIndex == 1 ? null : _bar;
            PlanetSectorBarRenderData supportBar = missingBarIndex == 2 ? null : _bar;

            Assert.Throws<ArgumentNullException>(() =>
                CreatePlanetData(energyBar, rawResourceBar, supportBar)
            );
        }

        [Test]
        public void Planet_NullName_ReturnsEmptyName()
        {
            PlanetSectorPlanetRenderData data = new PlanetSectorPlanetRenderData(
                0,
                Vector2Int.zero,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                default,
                PlanetIcon.None,
                PlanetIcon.None,
                _bar,
                _bar,
                _bar
            );

            Assert.AreEqual(string.Empty, data.Name);
        }

        private PlanetSectorPlanetRenderData CreatePlanetData(
            PlanetSectorBarRenderData energyBar,
            PlanetSectorBarRenderData rawResourceBar,
            PlanetSectorBarRenderData supportBar
        )
        {
            return new PlanetSectorPlanetRenderData(
                3,
                new Vector2Int(4, 5),
                _texture,
                _texture,
                _texture,
                _texture,
                _texture,
                _texture,
                _texture,
                _texture,
                _texture,
                _texture,
                _texture,
                "Planet",
                new Color32(1, 2, 3, 4),
                PlanetIcon.Fleet,
                PlanetIcon.Mission,
                energyBar,
                rawResourceBar,
                supportBar
            );
        }
    }
}
