using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Fleet
{
    [TestFixture]
    public class FleetWindowSessionTests
    {
        private CapitalShip _capitalShip;
        private GameFleet _fleet;
        private FleetWindowSession _session;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            _capitalShip = new CapitalShip { InstanceID = "ship" };
            _fleet = new GameFleet(
                "owner",
                "Fleet",
                new System.Collections.Generic.List<CapitalShip> { _capitalShip }
            )
            {
                InstanceID = "fleet",
            };
            Planet planet = new Planet { InstanceID = "planet", Fleets = { _fleet } };
            _windowObject = new GameObject("FleetWindow", typeof(RectTransform), typeof(UIWindow));
            UIWindow window = _windowObject.GetComponent<UIWindow>();
            window.Configure(1, 0, 0, 100, 100, false, true, false);
            _session = new FleetWindowSession(
                new GalaxyMapPlanet(new GamePlanetSystem(), planet, string.Empty),
                window
            );
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_WithFleetAndDetailItem_SelectsBoth()
        {
            Assert.AreSame(_fleet, _session.SelectedFleet);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedFleetItems.ToArray());
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems.ToArray());
        }

        [Test]
        public void ClearItemSelection_WithAvailableItems_RestoresRequiredSelections()
        {
            _session.ClearItemSelection();

            Assert.AreSame(_fleet, _session.SelectedFleet);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedFleetItems.ToArray());
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems.ToArray());
        }
    }
}
