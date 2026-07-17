using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSystem
{
    [TestFixture]
    public class PlanetSystemWindowRenderDataTests
    {
        private PlanetSystemBarRenderData _bar;
        private Texture2D _texture;

        [SetUp]
        public void SetUp()
        {
            _bar = new PlanetSystemBarRenderData(
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
            PlanetSystemPlanetRenderData planet = CreatePlanetData(_bar, _bar, _bar);
            PlanetSystemPlanetRenderData[] planets = { planet };

            PlanetSystemWindowRenderData data = new PlanetSystemWindowRenderData(null, planets);
            planets[0] = null;

            Assert.AreEqual(string.Empty, data.Title);
            Assert.AreSame(planet, data.Planets[0]);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<PlanetSystemPlanetRenderData>)data.Planets)[0] = null
            );
        }

        [Test]
        public void Window_NullPlanets_ReturnsEmptySnapshot()
        {
            PlanetSystemWindowRenderData data = new PlanetSystemWindowRenderData("System", null);

            Assert.AreEqual("System", data.Title);
            Assert.IsEmpty(data.Planets);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Planet_MissingBar_ThrowsArgumentNullException(int missingBarIndex)
        {
            PlanetSystemBarRenderData energyBar = missingBarIndex == 0 ? null : _bar;
            PlanetSystemBarRenderData rawResourceBar = missingBarIndex == 1 ? null : _bar;
            PlanetSystemBarRenderData supportBar = missingBarIndex == 2 ? null : _bar;

            Assert.Throws<ArgumentNullException>(() =>
                CreatePlanetData(energyBar, rawResourceBar, supportBar)
            );
        }

        [Test]
        public void Planet_Values_PreservesCompletePresentation()
        {
            PlanetSystemPlanetRenderData data = CreatePlanetData(_bar, _bar, _bar);

            Assert.AreEqual(3, data.PlanetIndex);
            Assert.AreEqual(new Vector2Int(4, 5), data.GalaxyOffset);
            Assert.AreSame(_texture, data.PlanetTexture);
            Assert.AreSame(_texture, data.FacilityTexture);
            Assert.AreSame(_texture, data.FacilityPressedTexture);
            Assert.AreSame(_texture, data.DefenseTexture);
            Assert.AreSame(_texture, data.DefensePressedTexture);
            Assert.AreSame(_texture, data.FleetTexture);
            Assert.AreSame(_texture, data.FleetPressedTexture);
            Assert.AreSame(_texture, data.MissionTexture);
            Assert.AreSame(_texture, data.MissionPressedTexture);
            Assert.AreSame(_texture, data.HeadquartersTexture);
            Assert.AreEqual("Planet", data.Name);
            Assert.AreEqual(new Color32(1, 2, 3, 4), data.NameColor);
            Assert.AreEqual(PlanetIcon.Fleet, data.SelectedIcon);
            Assert.AreEqual(PlanetIcon.Mission, data.HoveredIcon);
            Assert.AreSame(_bar, data.EnergyBar);
            Assert.AreSame(_bar, data.RawResourceBar);
            Assert.AreSame(_bar, data.SupportBar);
        }

        [Test]
        public void Planet_NullName_ReturnsEmptyName()
        {
            PlanetSystemPlanetRenderData data = new PlanetSystemPlanetRenderData(
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
                default,
                PlanetIcon.None,
                PlanetIcon.None,
                _bar,
                _bar,
                _bar
            );

            Assert.AreEqual(string.Empty, data.Name);
        }

        [Test]
        public void Bar_Values_PreservesCompletePresentation()
        {
            Assert.IsTrue(_bar.Visible);
            Assert.AreEqual(4, _bar.CellCount);
            Assert.AreEqual(2, _bar.LitCells);
            Assert.AreEqual(0.5f, _bar.FillRatio);
            Assert.AreEqual(new Color32(1, 2, 3, 4), _bar.FillColor);
            Assert.AreEqual(new Color32(5, 6, 7, 8), _bar.EmptyColor);
            Assert.AreEqual(new Color32(9, 10, 11, 12), _bar.BackgroundColor);
        }

        [Test]
        public void Element_Values_PreservesSemanticIdentity()
        {
            PlanetSystemWindowElement data = new PlanetSystemWindowElement(
                2,
                PlanetIcon.Defense,
                true
            );

            Assert.AreEqual(2, data.PlanetIndex);
            Assert.AreEqual(PlanetIcon.Defense, data.Icon);
            Assert.IsTrue(data.PlanetImage);
        }

        private PlanetSystemPlanetRenderData CreatePlanetData(
            PlanetSystemBarRenderData energyBar,
            PlanetSystemBarRenderData rawResourceBar,
            PlanetSystemBarRenderData supportBar
        )
        {
            return new PlanetSystemPlanetRenderData(
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
