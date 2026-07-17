using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Fleet
{
    [TestFixture]
    public class FleetWindowSessionTests
    {
        private CapitalShip _capitalShip;
        private CapitalShip _secondCapitalShip;
        private GameFleet _fleet;
        private GameFleet _secondFleet;
        private Officer _officer;
        private Planet _planet;
        private Regiment _regiment;
        private FleetWindowSession _session;
        private SpecialForces _specialForces;
        private Starfighter _starfighter;
        private GameObject _windowObject;
        private UIWindow _window;

        [SetUp]
        public void SetUp()
        {
            _capitalShip = CreateCapitalShip("ship", "Capital Ship");
            _starfighter = new Starfighter
            {
                InstanceID = "starfighter",
                DisplayName = "Starfighter",
                OwnerInstanceID = "owner",
            };
            _regiment = new Regiment
            {
                InstanceID = "regiment",
                DisplayName = "Regiment",
                OwnerInstanceID = "owner",
            };
            _officer = new Officer
            {
                InstanceID = "officer",
                DisplayName = "Officer",
                OwnerInstanceID = "owner",
            };
            _specialForces = new SpecialForces
            {
                InstanceID = "special-forces",
                DisplayName = "Special Forces",
                OwnerInstanceID = "owner",
            };
            _capitalShip.Starfighters.Add(_starfighter);
            _capitalShip.Regiments.Add(_regiment);
            _capitalShip.Officers.Add(_officer);
            _capitalShip.SpecialForces.Add(_specialForces);
            _fleet = CreateFleet("fleet", "Fleet", _capitalShip);
            _secondCapitalShip = CreateCapitalShip("second-ship", "Second Ship");
            _secondFleet = CreateFleet("second-fleet", "Second Fleet", _secondCapitalShip);
            _planet = new Planet { InstanceID = "planet", Fleets = { _fleet, _secondFleet } };
            AttachFleetGraph(_planet, _fleet);
            AttachFleetGraph(_planet, _secondFleet);
            _windowObject = new GameObject("FleetWindow", typeof(RectTransform), typeof(UIWindow));
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 0, 0, 100, 100, false, true, false);
            _session = new FleetWindowSession(
                new GalaxyMapPlanet(new GamePlanetSystem(), _planet, string.Empty),
                _window
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
        public void Constructor_NullPlanet_ThrowsArgumentNullException()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                new FleetWindowSession(null, _window)
            );
        }

        [Test]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            GalaxyMapPlanet planet = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                _planet,
                string.Empty
            );

            Assert.Throws<System.ArgumentNullException>(() => new FleetWindowSession(planet, null));
        }

        [Test]
        public void Constructor_EmptyPlanet_InitializesEmptySelection()
        {
            Planet planet = new Planet { InstanceID = "empty" };

            FleetWindowSession session = new FleetWindowSession(
                new GalaxyMapPlanet(new GamePlanetSystem(), planet, string.Empty),
                _window
            );

            Assert.IsEmpty(session.Fleets);
            Assert.IsNull(session.SelectedFleet);
            Assert.AreEqual(-1, session.SelectedFleetIndex);
            Assert.IsEmpty(session.SelectedFleetItems);
            Assert.IsEmpty(session.DetailItems);
            Assert.IsEmpty(session.SelectedDetailItems);
        }

        [Test]
        public void ClearItemSelection_WithAvailableItems_RestoresRequiredSelections()
        {
            _session.ClearItemSelection();

            Assert.AreSame(_fleet, _session.SelectedFleet);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedFleetItems.ToArray());
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems.ToArray());
        }

        [Test]
        public void RebindPlanet_NullProjection_ThrowsArgumentException()
        {
            Assert.Throws<System.ArgumentException>(() => _session.RebindPlanet(null));
            Assert.Throws<System.ArgumentException>(() =>
                _session.RebindPlanet(
                    new GalaxyMapPlanet(new GamePlanetSystem(), null, string.Empty)
                )
            );
        }

        [Test]
        public void RebindPlanet_RecreatedNodes_PreservesSelectionByIdentity()
        {
            _session.SelectFleet(1);
            CapitalShip replacementShip = CreateCapitalShip("second-ship", "Replacement Ship");
            GameFleet replacementFleet = CreateFleet(
                "second-fleet",
                "Replacement Fleet",
                replacementShip
            );
            Planet replacementPlanet = new Planet
            {
                InstanceID = "planet",
                Fleets = { replacementFleet },
            };
            AttachFleetGraph(replacementPlanet, replacementFleet);
            GalaxyMapPlanet replacementProjection = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                replacementPlanet,
                string.Empty
            );

            _session.RebindPlanet(replacementProjection);

            Assert.AreSame(replacementProjection, _session.Planet);
            Assert.AreSame(replacementFleet, _session.SelectedFleet);
            Assert.AreEqual(0, _session.SelectedFleetIndex);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedFleetItems);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems);
            Assert.AreSame(replacementShip, _session.DetailItems[0]);
        }

        [Test]
        public void Reconcile_SelectedFleetRemoved_SelectsNearestRemainingFleet()
        {
            _session.SelectFleet(1);
            _planet.Fleets.Remove(_secondFleet);

            _session.Reconcile();

            Assert.AreSame(_fleet, _session.SelectedFleet);
            Assert.AreEqual(0, _session.SelectedFleetIndex);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedFleetItems);
        }

        [Test]
        public void Reconcile_AllFleetsRemoved_ClearsFleetAndDetailState()
        {
            _session.CaptureDetailContext(0);
            _session.BeginRename(_capitalShip);
            _planet.Fleets.Clear();

            _session.Reconcile();

            Assert.IsNull(_session.SelectedFleet);
            Assert.AreEqual(-1, _session.SelectedFleetIndex);
            Assert.IsEmpty(_session.SelectedFleetItems);
            Assert.IsEmpty(_session.DetailItems);
            Assert.IsEmpty(_session.SelectedDetailItems);
            Assert.AreEqual(-1, _session.ContextDetailItemIndex);
            Assert.IsNull(_session.RenameTarget);
        }

        [Test]
        public void SelectTarget_NullOrForeignTarget_ReturnsFalseWithoutChangingSelection()
        {
            CapitalShip foreign = CreateCapitalShip("foreign", "Foreign");

            bool nullSelected = _session.SelectTarget(null, FleetWindowTab.CapitalShips);
            bool foreignSelected = _session.SelectTarget(foreign, FleetWindowTab.CapitalShips);

            Assert.IsFalse(nullSelected);
            Assert.IsFalse(foreignSelected);
            Assert.AreSame(_fleet, _session.SelectedFleet);
            Assert.AreEqual(FleetWindowTab.CapitalShips, _session.ActiveTab);
        }

        [Test]
        public void SelectTarget_ContainedStarfighter_SelectsFleetTabAndItem()
        {
            bool selected = _session.SelectTarget(_starfighter, FleetWindowTab.Starfighters);

            Assert.IsTrue(selected);
            Assert.AreSame(_fleet, _session.SelectedFleet);
            Assert.AreEqual(FleetWindowTab.Starfighters, _session.ActiveTab);
            Assert.AreSame(_starfighter, _session.DetailItems[0]);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedFleetItems);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems);
        }

        [Test]
        public void TryGetFleet_Index_ReturnsOnlyCurrentRows()
        {
            bool found = _session.TryGetFleet(1, out GameFleet fleet);
            bool missing = _session.TryGetFleet(2, out GameFleet absent);

            Assert.IsTrue(found);
            Assert.AreSame(_secondFleet, fleet);
            Assert.IsFalse(missing);
            Assert.IsNull(absent);
        }

        [Test]
        public void TryGetDetailItem_Index_ReturnsOnlyCurrentCards()
        {
            bool found = _session.TryGetDetailItem(0, out ISceneNode item);
            bool missing = _session.TryGetDetailItem(-1, out ISceneNode absent);

            Assert.IsTrue(found);
            Assert.AreSame(_capitalShip, item);
            Assert.IsFalse(missing);
            Assert.IsNull(absent);
        }

        [Test]
        public void CaptureFleetContext_Row_SelectsFleetAndReturnsContextItems()
        {
            bool captured = _session.CaptureFleetContext(1);

            List<ISceneNode> items = _session.GetContextItems();

            Assert.IsTrue(captured);
            Assert.AreEqual(1, _session.ContextFleetIndex);
            Assert.AreEqual(-1, _session.ContextDetailItemIndex);
            Assert.AreSame(_secondFleet, _session.SelectedFleet);
            CollectionAssert.AreEqual(new ISceneNode[] { _secondFleet }, items);
        }

        [Test]
        public void CaptureDetailContext_Card_SelectsItemAndReturnsContextItems()
        {
            bool captured = _session.CaptureDetailContext(0);

            List<ISceneNode> items = _session.GetContextItems();

            Assert.IsTrue(captured);
            Assert.AreEqual(-1, _session.ContextFleetIndex);
            Assert.AreEqual(0, _session.ContextDetailItemIndex);
            CollectionAssert.AreEqual(new ISceneNode[] { _capitalShip }, items);
        }

        [Test]
        public void CaptureContext_InvalidIndexes_ReturnFalse()
        {
            Assert.IsFalse(_session.CaptureFleetContext(-1));
            Assert.IsFalse(_session.CaptureFleetContext(2));
            Assert.IsFalse(_session.CaptureDetailContext(-1));
            Assert.IsFalse(_session.CaptureDetailContext(1));
            Assert.AreEqual(-1, _session.ContextFleetIndex);
            Assert.AreEqual(-1, _session.ContextDetailItemIndex);
        }

        [Test]
        public void PrepareFleetDragSelection_SelectedRow_PreservesSelectionForDrag()
        {
            bool canStartDrag = _session.PrepareFleetDragSelection(0);

            Assert.IsTrue(canStartDrag);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedFleetItems);
        }

        [Test]
        public void PrepareFleetDragSelection_UnselectedRow_SelectsRowBeforeDrag()
        {
            bool canStartDrag = _session.PrepareFleetDragSelection(1);

            Assert.IsFalse(canStartDrag);
            Assert.AreSame(_secondFleet, _session.SelectedFleet);
            CollectionAssert.AreEqual(new[] { 1 }, _session.SelectedFleetItems);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems);
        }

        [Test]
        public void PrepareDetailDragSelection_SelectedAndInvalidCards_ReturnExpectedState()
        {
            bool selectedCanDrag = _session.PrepareDetailDragSelection(0);
            bool invalidCanDrag = _session.PrepareDetailDragSelection(1);

            Assert.IsTrue(selectedCanDrag);
            Assert.IsFalse(invalidCanDrag);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems);
        }

        [Test]
        public void SelectFleet_Row_ChangesDisplayedFleetAndRequiredSelections()
        {
            bool selected = _session.SelectFleet(1);

            Assert.IsTrue(selected);
            Assert.AreSame(_secondFleet, _session.SelectedFleet);
            CollectionAssert.AreEqual(new[] { 1 }, _session.SelectedFleetItems);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems);
            Assert.AreSame(_secondCapitalShip, _session.DetailItems[0]);
        }

        [Test]
        public void SelectDetailItem_InvalidIndex_ReturnsFalseWithoutChangingSelection()
        {
            bool selected = _session.SelectDetailItem(1);

            Assert.IsFalse(selected);
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems);
        }

        [Test]
        public void SelectTab_ValidTab_RefreshesItemsAndRequiredSelection()
        {
            _session.CaptureDetailContext(0);
            _session.BeginRename(_capitalShip);

            bool selected = _session.SelectTab(FleetWindowTab.Personnel);

            Assert.IsTrue(selected);
            Assert.AreEqual(FleetWindowTab.Personnel, _session.ActiveTab);
            CollectionAssert.AreEqual(
                new ISceneNode[] { _officer, _specialForces },
                _session.DetailItems
            );
            CollectionAssert.AreEqual(new[] { 0 }, _session.SelectedDetailItems);
            Assert.AreEqual(-1, _session.ContextDetailItemIndex);
            Assert.IsNull(_session.RenameTarget);
        }

        [Test]
        public void SelectTab_CurrentOrUnsupportedTab_ReturnsFalse()
        {
            Assert.IsFalse(_session.SelectTab(FleetWindowTab.CapitalShips));
            Assert.IsFalse(_session.SelectTab((FleetWindowTab)99));
            Assert.AreEqual(FleetWindowTab.CapitalShips, _session.ActiveTab);
        }

        [Test]
        public void BeginRename_FleetAndCapitalShip_TracksCurrentVisualTarget()
        {
            bool fleetRename = _session.BeginRename(_secondFleet);

            Assert.IsTrue(fleetRename);
            Assert.AreSame(_secondFleet, _session.RenameTarget);
            Assert.AreEqual(1, _session.RenameFleetRowIndex);
            Assert.AreEqual(-1, _session.RenameDetailItemIndex);

            bool shipRename = _session.BeginRename(_capitalShip);

            Assert.IsTrue(shipRename);
            Assert.AreSame(_capitalShip, _session.RenameTarget);
            Assert.AreEqual(-1, _session.RenameFleetRowIndex);
            Assert.AreEqual(0, _session.RenameDetailItemIndex);
        }

        [Test]
        public void BeginRename_UnsupportedOrHiddenTarget_ReturnsFalse()
        {
            Assert.IsFalse(_session.BeginRename(_regiment));

            _session.SelectTab(FleetWindowTab.Starfighters);

            Assert.IsFalse(_session.BeginRename(_capitalShip));
            Assert.IsNull(_session.RenameTarget);
        }

        [Test]
        public void EndRename_ActiveTarget_ClearsRenameState()
        {
            _session.BeginRename(_fleet);

            _session.EndRename();

            Assert.IsNull(_session.RenameTarget);
            Assert.AreEqual(-1, _session.RenameFleetRowIndex);
            Assert.AreEqual(-1, _session.RenameDetailItemIndex);
        }

        [Test]
        public void ClearContext_ActiveTarget_PreservesSelectionAndClearsContext()
        {
            _session.CaptureFleetContext(1);

            _session.ClearContext();

            Assert.AreEqual(-1, _session.ContextFleetIndex);
            Assert.AreEqual(-1, _session.ContextDetailItemIndex);
            Assert.AreSame(_secondFleet, _session.SelectedFleet);
            CollectionAssert.AreEqual(new[] { 1 }, _session.SelectedFleetItems);
            Assert.IsEmpty(_session.GetContextItems());
        }

        [Test]
        public void HasDetailItems_Tab_ReturnsSelectedFleetContentAvailability()
        {
            Assert.IsTrue(_session.HasDetailItems(FleetWindowTab.CapitalShips));
            Assert.IsTrue(_session.HasDetailItems(FleetWindowTab.Starfighters));
            Assert.IsTrue(_session.HasDetailItems(FleetWindowTab.Regiments));
            Assert.IsTrue(_session.HasDetailItems(FleetWindowTab.Personnel));
            Assert.IsFalse(_session.HasDetailItems((FleetWindowTab)99));

            _session.SelectFleet(1);

            Assert.IsTrue(_session.HasDetailItems(FleetWindowTab.CapitalShips));
            Assert.IsFalse(_session.HasDetailItems(FleetWindowTab.Starfighters));
            Assert.IsFalse(_session.HasDetailItems(FleetWindowTab.Regiments));
            Assert.IsFalse(_session.HasDetailItems(FleetWindowTab.Personnel));
        }

        private static CapitalShip CreateCapitalShip(string instanceId, string displayName)
        {
            return new CapitalShip
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                OwnerInstanceID = "owner",
            };
        }

        private static GameFleet CreateFleet(
            string instanceId,
            string displayName,
            params CapitalShip[] ships
        )
        {
            return new GameFleet("owner", displayName, ships.ToList()) { InstanceID = instanceId };
        }

        private static void AttachFleetGraph(Planet planet, GameFleet fleet)
        {
            fleet.SetParent(planet);
            foreach (CapitalShip ship in fleet.CapitalShips)
            {
                ship.SetParent(fleet);
                foreach (ISceneNode child in ship.GetChildren())
                    child.SetParent(ship);
            }
        }
    }
}
