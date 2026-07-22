using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Construction
{
    [TestFixture]
    public class ConstructionWindowSessionTests
    {
        private GameObject _sourceWindowObject;
        private ConstructionWindowSession _session;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject(
                "ConstructionWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            UIWindow window = _windowObject.GetComponent<UIWindow>();
            window.Configure(1, 0, 0, 100, 100, false, true, false);
            _sourceWindowObject = new GameObject(
                "FacilityWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            UIWindow sourceWindow = _sourceWindowObject.GetComponent<UIWindow>();
            sourceWindow.Configure(2, 0, 0, 100, 100, false, true, false);
            GalaxyMapPlanet planet = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                new Planet { InstanceID = "producer" },
                string.Empty
            );
            _session = new ConstructionWindowSession(
                window,
                planet,
                sourceWindow,
                FacilityWindowTab.Shipyards,
                "destination",
                null
            );
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sourceWindowObject);
            Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void SetItems_ReorderedTemplates_PreservesSelectionByTypeId()
        {
            CapitalShip first = new CapitalShip { TypeID = "first" };
            CapitalShip selected = new CapitalShip { TypeID = "selected" };
            _session.SetItems(new IManufacturable[] { first, selected });
            _session.SelectItem(1);
            CapitalShip selectedReplacement = new CapitalShip { TypeID = selected.TypeID };

            _session.SetItems(new IManufacturable[] { selectedReplacement, first });

            Assert.AreEqual(0, _session.SelectedItemIndex);
            Assert.AreSame(selectedReplacement, _session.SelectedItem);
        }

        [Test]
        public void Reinitialize_DifferentManufacturingTab_ResetsDialogState()
        {
            _session.SetItems(new IManufacturable[] { new CapitalShip { TypeID = "ship" } });
            _session.IncrementBuildCount();
            _session.ToggleDropdown();

            _session.Reinitialize(
                _session.Planet,
                _sourceWindowObject.GetComponent<UIWindow>(),
                FacilityWindowTab.Training,
                "destination",
                null
            );

            Assert.AreEqual(FacilityWindowTab.Training, _session.ManufacturingTab);
            Assert.AreEqual(1, _session.BuildCount);
            Assert.IsFalse(_session.DropdownOpen);
            Assert.IsEmpty(_session.Items);
        }

        [Test]
        public void TryUpdateDestination_MatchingSourceAndManufacturingType_UpdatesDestination()
        {
            UIWindow sourceWindow = _sourceWindowObject.GetComponent<UIWindow>();

            bool updated = _session.TryUpdateDestination(
                sourceWindow,
                ManufacturingType.Ship,
                "new-planet",
                "new-item"
            );

            Assert.IsTrue(updated);
            Assert.AreEqual("new-planet", _session.DestinationPlanetId);
            Assert.AreEqual("new-item", _session.DestinationItemId);
        }

        [Test]
        public void DismissDropdown_OpenDropdown_ClosesAndReturnsTrue()
        {
            _session.ToggleDropdown();

            bool dismissed = _session.DismissDropdown();

            Assert.IsTrue(dismissed);
            Assert.IsFalse(_session.DropdownOpen);
        }

        [TestCase(0, 1)]
        [TestCase(17, 17)]
        [TestCase(256, byte.MaxValue)]
        public void SetBuildCount_Value_ClampsToSupportedRange(int value, int expected)
        {
            _session.SetBuildCount(value);

            Assert.AreEqual(expected, _session.BuildCount);
        }
    }
}
