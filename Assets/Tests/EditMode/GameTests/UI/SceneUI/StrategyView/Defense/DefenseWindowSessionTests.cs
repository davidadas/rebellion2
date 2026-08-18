using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Defense
{
    [TestFixture]
    public class DefenseWindowSessionTests
    {
        private GameObject _windowObject;
        private UIWindow _window;
        private Planet _planet;
        private GalaxyMapPlanet _mapPlanet;
        private DefenseWindowSession _session;

        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject(
                "DefenseWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 0, 0, 100, 100, false, true, false);
            _planet = new Planet { InstanceID = "planet", DisplayName = "Corellia" };
            _mapPlanet = new GalaxyMapPlanet(new GamePlanetSystem(), _planet, string.Empty);
            _session = new DefenseWindowSession(_mapPlanet, _window);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_NullPlanet_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DefenseWindowSession(null, _window));
        }

        [Test]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new DefenseWindowSession(_mapPlanet, null));
        }

        [Test]
        public void Reconcile_MixedPlanetUnits_MapsItemsToAuthoredTabs()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            SpecialForces specialForces = new SpecialForces { InstanceID = "special-forces" };
            Regiment regiment = new Regiment { InstanceID = "regiment" };
            Starfighter starfighter = new Starfighter { InstanceID = "starfighter" };
            Building shield = new Building
            {
                InstanceID = "shield",
                DefenseFacilityClass = DefenseFacilityClass.Shield,
            };
            Building deathStarShield = new Building
            {
                InstanceID = "death-star-shield",
                DefenseFacilityClass = DefenseFacilityClass.DeathStarShield,
            };
            Building kdy = new Building
            {
                InstanceID = "kdy",
                DefenseFacilityClass = DefenseFacilityClass.KDY,
            };
            Building lnr = new Building
            {
                InstanceID = "lnr",
                DefenseFacilityClass = DefenseFacilityClass.LNR,
            };
            _planet.AddTestChild(officer);
            _planet.AddTestChild(specialForces);
            _planet.AddTestChild(regiment);
            _planet.AddTestChild(starfighter);
            _planet.AddTestChild(shield);
            _planet.AddTestChild(kdy);
            _planet.AddTestChild(deathStarShield);
            _planet.AddTestChild(lnr);

            _session.Reconcile();

            CollectionAssert.AreEqual(
                new ISceneNode[] { officer, specialForces },
                _session.GetItems(DefenseWindowTab.Personnel)
            );
            CollectionAssert.AreEqual(
                new ISceneNode[] { regiment },
                _session.GetItems(DefenseWindowTab.Regiments)
            );
            CollectionAssert.AreEqual(
                new ISceneNode[] { starfighter },
                _session.GetItems(DefenseWindowTab.Starfighters)
            );
            CollectionAssert.AreEqual(
                new ISceneNode[] { shield, deathStarShield },
                _session.GetItems(DefenseWindowTab.Shields)
            );
            CollectionAssert.AreEqual(
                new ISceneNode[] { kdy, lnr },
                _session.GetItems(DefenseWindowTab.Batteries)
            );
        }

        [Test]
        public void Reconcile_RemovedItems_ClearsInteractionState()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            _planet.AddTestChild(officer);
            _session.Reconcile();
            _session.SelectItem(0);
            _session.CaptureContextItem(0);

            _planet.RemoveChildren<Officer>(_ => true);
            _session.Reconcile();

            Assert.IsEmpty(_session.SelectedItemIndexes);
            Assert.IsEmpty(_session.GetSelectedItems());
            Assert.AreEqual(-1, _session.ContextItemIndex);
        }

        [Test]
        public void SelectTab_DifferentAuthoredTab_ChangesTabAndClearsSelection()
        {
            _planet.AddTestChild(new Officer { InstanceID = "officer" });
            _session.Reconcile();
            _session.SelectItem(0);

            bool changed = _session.SelectTab(DefenseWindowTab.Regiments);

            Assert.IsTrue(changed);
            Assert.AreEqual(DefenseWindowTab.Regiments, _session.ActiveTab);
            Assert.IsEmpty(_session.SelectedItemIndexes);
            Assert.AreEqual(-1, _session.ContextItemIndex);
        }

        [Test]
        public void SelectTab_CurrentOrUnknownTab_ReturnsFalse()
        {
            bool currentChanged = _session.SelectTab(DefenseWindowTab.Personnel);
            bool unknownChanged = _session.SelectTab((DefenseWindowTab)99);

            Assert.IsFalse(currentChanged);
            Assert.IsFalse(unknownChanged);
            Assert.AreEqual(DefenseWindowTab.Personnel, _session.ActiveTab);
        }

        [Test]
        public void SelectSingleItem_ValidItem_SelectsRequestedTabItem()
        {
            Regiment regiment = new Regiment { InstanceID = "regiment" };
            _planet.AddTestChild(regiment);
            _session.Reconcile();

            bool selected = _session.SelectSingleItem(DefenseWindowTab.Regiments, 0);

            Assert.IsTrue(selected);
            Assert.AreEqual(DefenseWindowTab.Regiments, _session.ActiveTab);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedItemIndexes);
            CollectionAssert.AreEqual(new ISceneNode[] { regiment }, _session.GetSelectedItems());
        }

        [Test]
        public void SelectSingleItem_InvalidItem_ReturnsFalseWithEmptySelection()
        {
            bool selected = _session.SelectSingleItem(DefenseWindowTab.Regiments, 0);

            Assert.IsFalse(selected);
            Assert.AreEqual(DefenseWindowTab.Regiments, _session.ActiveTab);
            Assert.IsEmpty(_session.SelectedItemIndexes);
            Assert.IsEmpty(_session.GetSelectedItems());
        }

        [Test]
        public void TryGetItem_ValidAndInvalidIndexes_ReturnsExpectedResult()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            _planet.AddTestChild(officer);
            _session.Reconcile();

            bool found = _session.TryGetItem(0, out ISceneNode foundItem);
            bool missing = _session.TryGetItem(1, out ISceneNode missingItem);

            Assert.IsTrue(found);
            Assert.AreSame(officer, foundItem);
            Assert.IsFalse(missing);
            Assert.IsNull(missingItem);
        }

        [Test]
        public void CaptureContextItem_UnselectedItem_SelectsAndCapturesItem()
        {
            Officer first = new Officer { InstanceID = "first" };
            Officer second = new Officer { InstanceID = "second" };
            _planet.AddTestChild(first);
            _planet.AddTestChild(second);
            _session.Reconcile();
            _session.SelectItem(0);

            bool captured = _session.CaptureContextItem(1);

            Assert.IsTrue(captured);
            Assert.AreEqual(1, _session.ContextItemIndex);
            CollectionAssert.AreEqual(new[] { 1 }, _session.SelectedItemIndexes);
            CollectionAssert.AreEqual(new ISceneNode[] { second }, _session.GetSelectedItems());
        }

        [Test]
        public void CaptureContextItem_InvalidItem_ClearsContextOnly()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            _planet.AddTestChild(officer);
            _session.Reconcile();
            _session.SelectItem(0);
            _session.CaptureContextItem(0);

            bool captured = _session.CaptureContextItem(2);

            Assert.IsFalse(captured);
            Assert.AreEqual(-1, _session.ContextItemIndex);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedItemIndexes);
            CollectionAssert.AreEqual(new ISceneNode[] { officer }, _session.GetSelectedItems());
        }

        [Test]
        public void PrepareItemSelection_ValidItem_CapturesContext()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            _planet.AddTestChild(officer);
            _session.Reconcile();

            bool prepared = _session.PrepareItemSelection(0);

            Assert.IsTrue(prepared);
            Assert.AreEqual(0, _session.ContextItemIndex);
        }

        [Test]
        public void SelectItem_StationaryRegiment_ProducesDraggableSelection()
        {
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _planet.AddTestChild(regiment);
            _session.Reconcile();
            _session.SelectTab(DefenseWindowTab.Regiments);

            _session.SelectItem(0);

            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedItemIndexes);
            Assert.IsTrue(_session.CanDragSelectedItems());
        }

        [Test]
        public void SelectItem_MovingRegiment_ProducesNonDraggableSelection()
        {
            Regiment regiment = new Regiment
            {
                InstanceID = "regiment",
                ManufacturingStatus = ManufacturingStatus.Complete,
                Movement = new MovementState(),
            };
            _planet.AddTestChild(regiment);
            _session.Reconcile();
            _session.SelectTab(DefenseWindowTab.Regiments);

            _session.SelectItemForDrag(0);

            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedItemIndexes);
            Assert.IsFalse(_session.CanDragSelectedItems());
        }

        [Test]
        public void CanDragItem_MovableUnitAndBuilding_ReturnsExpectedEligibility()
        {
            Regiment regiment = new Regiment { ManufacturingStatus = ManufacturingStatus.Complete };
            Building building = new Building { ManufacturingStatus = ManufacturingStatus.Complete };

            bool regimentCanDrag = DefenseWindowSession.CanDragItem(regiment);
            bool buildingCanDrag = DefenseWindowSession.CanDragItem(building);
            bool nullCanDrag = DefenseWindowSession.CanDragItem(null);

            Assert.IsTrue(regimentCanDrag);
            Assert.IsFalse(buildingCanDrag);
            Assert.IsFalse(nullCanDrag);
        }

        [Test]
        public void RebindPlanet_ReplacementItemsWithSameIDs_PreservesInteractionTargets()
        {
            Officer original = new Officer { InstanceID = "officer" };
            _planet.AddTestChild(original);
            _session.Reconcile();
            _session.SelectItem(0);
            _session.CaptureContextItem(0);
            Officer replacement = new Officer { InstanceID = original.InstanceID };
            Planet refreshedPlanet = new Planet { InstanceID = _planet.InstanceID };
            refreshedPlanet.AddTestChild(replacement);
            GalaxyMapPlanet refreshedProjection = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                refreshedPlanet,
                string.Empty
            );

            _session.RebindPlanet(refreshedProjection);

            Assert.AreSame(refreshedProjection, _session.Planet);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedItemIndexes);
            CollectionAssert.AreEqual(
                new ISceneNode[] { replacement },
                _session.GetSelectedItems()
            );
            Assert.AreEqual(0, _session.ContextItemIndex);
        }

        [Test]
        public void ClearSelection_ActiveSelection_ClearsAllInteractionState()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            _planet.AddTestChild(officer);
            _session.Reconcile();
            _session.SelectItem(0);
            _session.CaptureContextItem(0);

            _session.ClearSelection();

            Assert.IsEmpty(_session.SelectedItemIndexes);
            Assert.IsEmpty(_session.GetSelectedItems());
            Assert.AreEqual(-1, _session.ContextItemIndex);
            Assert.IsFalse(_session.CanDragSelectedItems());
        }
    }
}
