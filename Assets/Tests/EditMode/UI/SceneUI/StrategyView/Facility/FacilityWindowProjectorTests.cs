using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;
using UnityEngine;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Facility
{
    [TestFixture]
    public class FacilityWindowProjectorTests
    {
        private const string _ownerId = "FNALL1";

        private GameObject _windowObject;
        private UIWindow _window;
        private Planet _planet;
        private GalaxyMapPlanet _mapPlanet;
        private FacilityWindowSession _session;
        private UIContext _uiContext;
        private FacilityWindowProjector _projector;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _ownerId });
            game.Summary.PlayerFactionID = _ownerId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _windowObject = new GameObject(
                "FacilityWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            _window = _windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 27, 39, 100, 100, false, true, false);
            _window.SetActiveWindow(true);
            _planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _ownerId,
                NumRawResourceNodes = 3,
            };
            _mapPlanet = new GalaxyMapPlanet(new GamePlanetSystem(), _planet, string.Empty);
            _session = new FacilityWindowSession(_window, _mapPlanet);
            _projector = new FacilityWindowProjector(() => _uiContext);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new FacilityWindowProjector(null));
        }

        [Test]
        public void CreateRenderData_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _projector.CreateRenderData(null, _session, null)
            );
        }

        [Test]
        public void CreateRenderData_NullSession_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                _projector.CreateRenderData(_window, null, null)
            );
        }

        [Test]
        public void CreateRenderData_UnavailableContext_ThrowsInvalidOperationException()
        {
            FacilityWindowProjector projector = new FacilityWindowProjector(() => null);

            Assert.Throws<InvalidOperationException>(() =>
                projector.CreateRenderData(_window, _session, null)
            );
        }

        [Test]
        public void CreateRenderData_ManufacturingTab_ReturnsLaneAndTabPresentation()
        {
            Building completedShipyard = CreateBuilding(
                "complete-shipyard",
                "Complete Shipyard",
                BuildingType.Shipyard,
                ManufacturingStatus.Complete
            );
            Building incompleteShipyard = CreateBuilding(
                "incomplete-shipyard",
                "Incomplete Shipyard",
                BuildingType.Shipyard,
                ManufacturingStatus.Building
            );
            Building constructionYard = CreateBuilding(
                "construction-yard",
                "Construction Yard",
                BuildingType.ConstructionFacility,
                ManufacturingStatus.Complete
            );
            _planet.Buildings.Add(completedShipyard);
            _planet.Buildings.Add(incompleteShipyard);
            _planet.Buildings.Add(constructionYard);
            CapitalShip currentShip = CreateCapitalShip("current-ship", "Nebulon-B Frigate");
            currentShip.ConstructionCost = 80;
            currentShip.ManufacturingProgress = 30;
            currentShip.ManufacturingStatus = ManufacturingStatus.Building;
            CapitalShip queuedShip = CreateCapitalShip("queued-ship", "Corellian Corvette");
            queuedShip.ManufacturingStatus = ManufacturingStatus.Building;
            _planet.ManufacturingQueue[ManufacturingType.Ship] = new List<IManufacturable>
            {
                currentShip,
                queuedShip,
            };
            _session.Reconcile();
            _session.SelectManufacturingCard(
                (int)FacilityWindowTab.Shipyards,
                FacilityWindowRenderData.TabCount
            );
            Dictionary<ManufacturingType, string> destinations = new Dictionary<
                ManufacturingType,
                string
            >
            {
                { ManufacturingType.Ship, "Outer Rim Fleet" },
            };

            FacilityWindowRenderData data = _projector.CreateRenderData(
                _window,
                _session,
                destinations
            );

            Assert.AreEqual(27, data.X);
            Assert.AreEqual(39, data.Y);
            Assert.AreEqual("Corellia", data.Caption);
            Assert.AreEqual(FacilityWindowTab.Manufacturing, data.ActiveTab);
            Assert.IsTrue(data.ShowManufacturing);
            Assert.IsNotNull(data.TitleTexture);
            Assert.IsNotNull(data.ControlTabTexture);
            Assert.IsNotNull(data.ControlTabPressedTexture);
            Assert.AreEqual(FacilityWindowRenderData.TabCount, data.Tabs.Count);
            Assert.AreEqual(
                FacilityWindowTabState.Active,
                data.Tabs.Single(tab => tab.Tab == FacilityWindowTab.Manufacturing).State
            );
            Assert.AreEqual(
                FacilityWindowTabState.Inactive,
                data.Tabs.Single(tab => tab.Tab == FacilityWindowTab.Shipyards).State
            );
            Assert.AreEqual(
                FacilityWindowTabState.Disabled,
                data.Tabs.Single(tab => tab.Tab == FacilityWindowTab.Training).State
            );
            Assert.AreEqual(
                FacilityWindowTabState.Inactive,
                data.Tabs.Single(tab => tab.Tab == FacilityWindowTab.Construction).State
            );
            Assert.AreEqual(3, data.ManufacturingCards.Count);
            Assert.IsEmpty(data.InventoryItems);
            ManufacturingLaneCardRenderData shipCard = data.ManufacturingCards[0];
            Assert.AreEqual("Ship Construction", shipCard.Title);
            Assert.AreEqual("No Ships are being built", shipCard.EmptyText);
            Assert.AreEqual("Nebulon-B Frigate", shipCard.CurrentName);
            Assert.AreEqual("Building 2", shipCard.CurrentCount);
            Assert.AreEqual("Destination: Outer Rim Fleet", shipCard.DestinationText);
            Assert.AreEqual("1:2", shipCard.FacilityCount);
            Assert.AreEqual(30, shipCard.ManufacturingProgress);
            Assert.AreEqual(80, shipCard.ManufacturingCost);
            Assert.IsNotNull(shipCard.StateTexture);
            Assert.IsNotNull(shipCard.EntityTexture);
            ManufacturingLaneCardRenderData troopCard = data.ManufacturingCards[1];
            Assert.AreEqual("Troops in Training", troopCard.Title);
            Assert.AreEqual("No Troops in training", troopCard.EmptyText);
            Assert.AreEqual(string.Empty, troopCard.CurrentName);
            Assert.AreEqual(string.Empty, troopCard.CurrentCount);
            Assert.AreEqual("Destination: Corellia", troopCard.DestinationText);
            Assert.AreEqual("0:0", troopCard.FacilityCount);
        }

        [Test]
        public void CreateRenderData_MinesTab_ReturnsInventorySlotsAndSelection()
        {
            Building mine = CreateBuilding(
                "mine",
                "Mine",
                BuildingType.Mine,
                ManufacturingStatus.Complete
            );
            _planet.Buildings.Add(mine);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Mines);
            _session.SelectBuilding(0, 3);

            FacilityWindowRenderData data = _projector.CreateRenderData(_window, _session, null);

            Assert.AreEqual(FacilityWindowTab.Mines, data.ActiveTab);
            Assert.AreEqual("Mines", data.InventoryTitle);
            Assert.IsFalse(data.ShowManufacturing);
            Assert.IsEmpty(data.ManufacturingCards);
            Assert.AreEqual(3, data.InventoryItems.Count);
            Assert.IsTrue(data.InventoryItems[0].Selected);
            Assert.IsFalse(data.InventoryItems[1].Selected);
            Assert.IsFalse(data.InventoryItems[2].Selected);
            Assert.IsNotNull(data.InventoryItems[0].Texture);
            Assert.IsNotNull(data.InventoryItems[1].Texture);
            Assert.AreSame(data.InventoryItems[1].Texture, data.InventoryItems[2].Texture);
            Assert.IsNotNull(data.InventorySelectionTexture);
            Assert.AreEqual(
                FacilityWindowTabState.Active,
                data.Tabs.Single(tab => tab.Tab == FacilityWindowTab.Mines).State
            );
        }

        [Test]
        public void CreateRenderData_MovingBuilding_UsesTransitTexture()
        {
            Building definition = GetBuildingDefinition(BuildingType.Refinery);
            Building refinery = CreateBuilding(
                "refinery",
                "Refinery",
                BuildingType.Refinery,
                ManufacturingStatus.Complete
            );
            refinery.DisplayImagePath = definition.DisplayImagePath;
            refinery.SmallDisplayImagePath = definition.SmallDisplayImagePath;
            refinery.InTransitSmallImagePath = definition.InTransitSmallImagePath;
            refinery.Movement = new MovementState();
            _planet.Buildings.Add(refinery);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Refineries);

            FacilityWindowRenderData data = _projector.CreateRenderData(_window, _session, null);

            Assert.IsNotNull(data.InventoryItems[0].Texture);
            Assert.AreSame(
                _uiContext.GetTexture(definition.InTransitSmallImagePath),
                data.InventoryItems[0].Texture
            );
        }

        private static Building CreateBuilding(
            string instanceId,
            string displayName,
            BuildingType buildingType,
            ManufacturingStatus status
        )
        {
            Building definition = GetBuildingDefinition(buildingType);
            return new Building
            {
                InstanceID = instanceId,
                TypeID = definition.TypeID,
                DisplayName = displayName,
                OwnerInstanceID = _ownerId,
                BuildingType = buildingType,
                ManufacturingStatus = status,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
                InTransitSmallImagePath = definition.InTransitSmallImagePath,
            };
        }

        private static Building GetBuildingDefinition(BuildingType buildingType)
        {
            return ResourceManager
                .GetEntityData<Building>()
                .First(building =>
                    building.BuildingType == buildingType
                    && building.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true
                );
        }

        private static CapitalShip CreateCapitalShip(string instanceId, string displayName)
        {
            CapitalShip definition = ResourceManager
                .GetEntityData<CapitalShip>()
                .First(ship => ship.AllowedOwnerInstanceIDs?.Contains(_ownerId) == true);
            return new CapitalShip
            {
                InstanceID = instanceId,
                TypeID = definition.TypeID,
                DisplayName = displayName,
                OwnerInstanceID = _ownerId,
                DisplayImagePath = definition.DisplayImagePath,
                SmallDisplayImagePath = definition.SmallDisplayImagePath,
            };
        }
    }
}
