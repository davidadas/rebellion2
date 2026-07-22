using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Facility
{
    [TestFixture]
    public class FacilityWindowSessionTests
    {
        private GameObject _windowObject;
        private Planet _planet;
        private GalaxyMapPlanet _mapPlanet;
        private FacilityWindowSession _session;

        [SetUp]
        public void SetUp()
        {
            _windowObject = new GameObject(
                "FacilityWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            UIWindow window = _windowObject.GetComponent<UIWindow>();
            window.Configure(1, 10, 20, 100, 100, false, true, false);
            _planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                NumRawResourceNodes = 4,
            };
            _mapPlanet = new GalaxyMapPlanet(new GamePlanetSystem(), _planet, string.Empty);
            _session = new FacilityWindowSession(window, _mapPlanet);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FacilityWindowSession(null, _mapPlanet));
        }

        [Test]
        public void Constructor_PlanetProjectionWithoutPlanet_ThrowsArgumentException()
        {
            GalaxyMapPlanet projection = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                null,
                string.Empty
            );

            Assert.Throws<ArgumentException>(() =>
                new FacilityWindowSession(_windowObject.GetComponent<UIWindow>(), projection)
            );
        }

        [Test]
        public void Reconcile_MixedFacilities_OrdersInventoryAndCalculatesDisplayCounts()
        {
            _planet.Buildings.Add(CreateBuilding("z-shipyard", "Zeta", BuildingType.Shipyard));
            _planet.Buildings.Add(CreateBuilding("a-shipyard", "Alpha", BuildingType.Shipyard));
            _planet.Buildings.Add(
                CreateBuilding("training", "Training", BuildingType.TrainingFacility)
            );
            _planet.Buildings.Add(CreateBuilding("mine", "Mine", BuildingType.Mine));

            _session.Reconcile();

            CollectionAssert.AreEqual(
                new[] { "Alpha", "Zeta" },
                _session.GetItems(FacilityWindowTab.Shipyards).Select(item => item.DisplayName)
            );
            Assert.AreEqual(1, _session.GetDisplayCount(FacilityWindowTab.Manufacturing));
            Assert.AreEqual(2, _session.GetDisplayCount(FacilityWindowTab.Shipyards));
            Assert.AreEqual(1, _session.GetDisplayCount(FacilityWindowTab.Training));
            Assert.AreEqual(0, _session.GetDisplayCount(FacilityWindowTab.Construction));
            Assert.AreEqual(4, _session.GetDisplayCount(FacilityWindowTab.Mines));
        }

        [Test]
        public void Reconcile_RemovedContextBuilding_ClearsSelectionAndContext()
        {
            Building building = CreateBuilding("shipyard", "Shipyard", BuildingType.Shipyard);
            _planet.Buildings.Add(building);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Shipyards);
            _session.SelectBuildingForContext(0);

            _planet.Buildings.Clear();
            _session.Reconcile();

            Assert.IsEmpty(_session.SelectedBuildingIds);
            Assert.IsNull(_session.GetContextBuilding());
            Assert.IsNull(_session.GetStatusBuilding());
        }

        [Test]
        public void SelectManufacturingCard_ValidCard_SelectsSemanticLane()
        {
            _session.SelectManufacturingCard(
                (int)FacilityWindowTab.Shipyards,
                FacilityWindowRenderData.TabCount
            );

            Assert.AreEqual(FacilityWindowTab.Shipyards, _session.GetSelectedManufacturingTab());
            Assert.AreEqual(FacilityWindowTab.Shipyards, _session.GetContextManufacturingTab());
            CollectionAssert.AreEqual(
                new[] { (int)FacilityWindowTab.Shipyards },
                _session.SelectedCards
            );
        }

        [Test]
        public void SelectManufacturingCard_InvalidCard_DoesNotChangeSelection()
        {
            _session.SelectManufacturingCard(
                (int)FacilityWindowTab.Shipyards,
                FacilityWindowRenderData.TabCount
            );

            _session.SelectManufacturingCard(-1, FacilityWindowRenderData.TabCount);

            Assert.AreEqual(FacilityWindowTab.Shipyards, _session.GetSelectedManufacturingTab());
            CollectionAssert.AreEqual(
                new[] { (int)FacilityWindowTab.Shipyards },
                _session.SelectedCards
            );
        }

        [Test]
        public void SelectManufacturingCardForContext_DifferentLane_ReplacesSelection()
        {
            _session.SelectManufacturingCard(
                (int)FacilityWindowTab.Shipyards,
                FacilityWindowRenderData.TabCount
            );

            _session.SelectManufacturingCardForContext(
                FacilityWindowTab.Training,
                (int)FacilityWindowTab.Training
            );

            Assert.AreEqual(FacilityWindowTab.Training, _session.ContextManufacturingTab);
            Assert.AreEqual(FacilityWindowTab.Training, _session.GetContextManufacturingTab());
            CollectionAssert.AreEqual(
                new[] { (int)FacilityWindowTab.Training },
                _session.SelectedCards
            );
        }

        [Test]
        public void SetActiveTab_DifferentTab_ClearsManufacturingSelectionAndContext()
        {
            _session.SelectManufacturingCardForContext(
                FacilityWindowTab.Shipyards,
                (int)FacilityWindowTab.Shipyards
            );

            _session.SetActiveTab(FacilityWindowTab.Shipyards);

            Assert.AreEqual(FacilityWindowTab.Shipyards, _session.ActiveTab);
            Assert.IsEmpty(_session.SelectedCards);
            Assert.IsNull(_session.ContextManufacturingTab);
            Assert.IsNull(_session.GetSelectedManufacturingTab());
            Assert.IsNull(_session.GetContextManufacturingTab());
        }

        [Test]
        public void SelectBuilding_ValidIndex_SelectsBuildingByIdentity()
        {
            Building alpha = CreateBuilding("alpha", "Alpha", BuildingType.Shipyard);
            Building zeta = CreateBuilding("zeta", "Zeta", BuildingType.Shipyard);
            _planet.Buildings.Add(zeta);
            _planet.Buildings.Add(alpha);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Shipyards);

            _session.SelectBuilding(0, 3);

            CollectionAssert.AreEqual(new[] { "alpha" }, _session.SelectedBuildingIds);
            CollectionAssert.AreEqual(new[] { alpha }, _session.GetSelectedBuildings());
            Assert.AreSame(alpha, _session.GetStatusBuilding());
            Assert.AreSame(alpha, _session.GetInventoryBuilding(0));
        }

        [Test]
        public void SelectBuilding_InvalidIndex_PreservesSelection()
        {
            Building building = CreateBuilding("shipyard", "Shipyard", BuildingType.Shipyard);
            _planet.Buildings.Add(building);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Shipyards);
            _session.SelectBuilding(0, 3);

            _session.SelectBuilding(2, 3);

            CollectionAssert.AreEqual(new[] { "shipyard" }, _session.SelectedBuildingIds);
            CollectionAssert.AreEqual(new[] { building }, _session.GetSelectedBuildings());
        }

        [Test]
        public void SelectBuilding_KnownBuilding_NavigatesToInventoryTab()
        {
            Building refinery = CreateBuilding("refinery", "Refinery", BuildingType.Refinery);
            _planet.Buildings.Add(refinery);
            _session.Reconcile();

            bool selected = _session.SelectBuilding(FacilityWindowTab.Refineries, refinery);

            Assert.IsTrue(selected);
            Assert.AreEqual(FacilityWindowTab.Refineries, _session.ActiveTab);
            CollectionAssert.AreEqual(new[] { "refinery" }, _session.SelectedBuildingIds);
            CollectionAssert.AreEqual(new[] { refinery }, _session.GetSelectedBuildings());
        }

        [Test]
        public void SelectBuilding_BuildingOutsideTab_ReturnsFalse()
        {
            Building refinery = CreateBuilding("refinery", "Refinery", BuildingType.Refinery);
            _planet.Buildings.Add(refinery);
            _session.Reconcile();

            bool selected = _session.SelectBuilding(FacilityWindowTab.Shipyards, refinery);

            Assert.IsFalse(selected);
            Assert.AreEqual(FacilityWindowTab.Manufacturing, _session.ActiveTab);
            Assert.IsEmpty(_session.SelectedBuildingIds);
        }

        [Test]
        public void SelectBuildingForContext_UnselectedBuilding_ReplacesSelection()
        {
            Building alpha = CreateBuilding("alpha", "Alpha", BuildingType.Shipyard);
            Building beta = CreateBuilding("beta", "Beta", BuildingType.Shipyard);
            _planet.Buildings.Add(alpha);
            _planet.Buildings.Add(beta);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Shipyards);
            _session.SelectBuilding(0, 3);

            _session.SelectBuildingForContext(1);

            CollectionAssert.AreEqual(new[] { "beta" }, _session.SelectedBuildingIds);
            Assert.AreSame(beta, _session.GetContextBuilding());
            Assert.AreSame(beta, _session.GetStatusBuilding());
        }

        [Test]
        public void RebindPlanet_ReplacementBuildingWithSameID_PreservesSelection()
        {
            Building original = CreateBuilding("shipyard", "Original", BuildingType.Shipyard);
            _planet.Buildings.Add(original);
            _session.Reconcile();
            _session.SelectBuilding(FacilityWindowTab.Shipyards, original);
            Building replacement = CreateBuilding(
                original.InstanceID,
                "Replacement",
                BuildingType.Shipyard
            );
            Planet refreshedPlanet = new Planet
            {
                InstanceID = _planet.InstanceID,
                Buildings = { replacement },
            };
            GalaxyMapPlanet refreshedProjection = new GalaxyMapPlanet(
                new GamePlanetSystem(),
                refreshedPlanet,
                string.Empty
            );

            _session.RebindPlanet(refreshedProjection);

            Assert.AreSame(refreshedProjection, _session.Planet);
            CollectionAssert.AreEqual(new[] { original.InstanceID }, _session.SelectedBuildingIds);
            CollectionAssert.AreEqual(new[] { replacement }, _session.GetSelectedBuildings());
        }

        [Test]
        public void GetDestination_WithoutOverride_ReturnsRepresentedPlanet()
        {
            _session.GetDestination(ManufacturingType.Ship, out string planetId, out string itemId);

            Assert.AreEqual(_planet.InstanceID, planetId);
            Assert.IsNull(itemId);
        }

        [Test]
        public void SetDestination_ValidPair_ReturnsStoredDestination()
        {
            _session.SetDestination(ManufacturingType.Troop, "destination-planet", "fleet");

            _session.GetDestination(
                ManufacturingType.Troop,
                out string planetId,
                out string itemId
            );

            Assert.AreEqual("destination-planet", planetId);
            Assert.AreEqual("fleet", itemId);
        }

        [Test]
        public void SetDestination_EmptyPlanetID_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                _session.SetDestination(ManufacturingType.Building, string.Empty, null)
            );
        }

        [Test]
        public void ClearContext_SelectedBuilding_PreservesSelection()
        {
            Building building = CreateBuilding("shipyard", "Shipyard", BuildingType.Shipyard);
            _planet.Buildings.Add(building);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Shipyards);
            _session.SelectBuildingForContext(0);

            _session.ClearContext();

            CollectionAssert.AreEqual(new[] { "shipyard" }, _session.SelectedBuildingIds);
            CollectionAssert.AreEqual(new[] { building }, _session.GetSelectedBuildings());
            Assert.IsNull(_session.GetContextBuilding());
        }

        [Test]
        public void ClearSelection_SelectedBuilding_ClearsSelectionAndContext()
        {
            Building building = CreateBuilding("shipyard", "Shipyard", BuildingType.Shipyard);
            _planet.Buildings.Add(building);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Shipyards);
            _session.SelectBuildingForContext(0);

            _session.ClearSelection();

            Assert.IsEmpty(_session.SelectedBuildingIds);
            Assert.IsEmpty(_session.SelectedCards);
            Assert.IsNull(_session.GetContextBuilding());
            Assert.IsNull(_session.GetStatusBuilding());
        }

        private static Building CreateBuilding(
            string instanceId,
            string displayName,
            BuildingType buildingType
        )
        {
            return new Building
            {
                InstanceID = instanceId,
                DisplayName = displayName,
                BuildingType = buildingType,
            };
        }
    }
}
