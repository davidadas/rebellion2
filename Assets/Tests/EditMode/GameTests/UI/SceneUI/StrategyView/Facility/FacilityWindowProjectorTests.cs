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
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Facility
{
    [TestFixture]
    public class FacilityWindowProjectorTests
    {
        private const string _ownerId = "FNALL1";

        private Texture2D _constructionTexture;
        private GameRoot _game;
        private GameObject _windowObject;
        private UIWindow _window;
        private Planet _planet;
        private GalaxyMapPlanet _mapPlanet;
        private FacilityWindowSession _session;
        private UIContext _uiContext;
        private FacilityWindowProjector _projector;
        private List<Texture2D> _textures;

        [SetUp]
        public void SetUp()
        {
            _game = new GameRoot(TestConfig.Create());
            _game.GetFactions().Add(new Faction { InstanceID = _ownerId });
            _game.Summary.PlayerFactionID = _ownerId;
            Dictionary<string, Texture2D> texturesByPath = CreateTextures();
            FactionTheme theme = CreateTheme();
            _uiContext = new UIContext(
                _game,
                new FactionThemeLibrary(
                    new FactionThemes
                    {
                        new FactionTheme { FactionInstanceID = "DEFAULT" },
                        theme,
                    }
                ),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>()),
                path => texturesByPath.TryGetValue(path, out Texture2D texture) ? texture : null
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
            GalaxyPlanetSector sector = new GalaxyPlanetSector { InstanceID = "sector" };
            _game.AttachNode(sector, _game.Galaxy);
            _game.AttachNode(_planet, sector);
            _mapPlanet = new GalaxyMapPlanet(new GalaxyPlanetSector(), _planet, string.Empty);
            _session = new FacilityWindowSession(_window, _mapPlanet);
            _projector = new FacilityWindowProjector(() => _uiContext);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_windowObject);
            foreach (Texture2D texture in _textures)
                UnityEngine.Object.DestroyImmediate(texture);
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
            _planet.AddTestChild(completedShipyard);
            _planet.AddTestChild(incompleteShipyard);
            _planet.AddTestChild(constructionYard);
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
            _planet.AddTestChild(mine);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Mines);
            _session.SelectBuilding(0);

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
            Building refinery = CreateBuilding(
                "refinery",
                "Refinery",
                BuildingType.Refinery,
                ManufacturingStatus.Complete
            );
            refinery.Movement = new MovementState();
            _planet.AddTestChild(refinery);
            _session.Reconcile();
            _session.SetActiveTab(FacilityWindowTab.Refineries);

            FacilityWindowRenderData data = _projector.CreateRenderData(_window, _session, null);

            Assert.IsNotNull(data.InventoryItems[0].Texture);
            Assert.AreSame(
                _uiContext.GetTexture(refinery.InTransitSmallImagePath),
                data.InventoryItems[0].Texture
            );
        }

        [Test]
        public void CreateRenderData_QueuedBuilding_UsesConfiguredConstructionTexture()
        {
            Building building = CreateBuilding(
                "queued-building",
                "Orbital Facility",
                BuildingType.Mine,
                ManufacturingStatus.Building
            );
            _planet.ManufacturingQueue[ManufacturingType.Building] = new List<IManufacturable>
            {
                building,
            };

            FacilityWindowRenderData data = _projector.CreateRenderData(_window, _session, null);

            Assert.AreSame(_constructionTexture, data.ManufacturingCards[2].EntityTexture);
        }

        [Test]
        public void CreateRenderData_ActiveQueueItem_UsesLiveDeliveryDestination()
        {
            Rebellion.Game.Units.Fleet liveDestination = new Rebellion.Game.Units.Fleet
            {
                InstanceID = "live-destination",
                DisplayName = "Live Destination",
                OwnerInstanceID = _ownerId,
            };
            CapitalShip ship = CreateCapitalShip("queued-ship", "Queued Ship");
            ship.ManufacturingStatus = ManufacturingStatus.Building;
            _game.AttachNode(liveDestination, _planet);
            _game.AttachNode(ship, liveDestination);
            _planet.ManufacturingQueue[ManufacturingType.Ship] = new List<IManufacturable> { ship };
            Dictionary<ManufacturingType, string> staleDestinations = new Dictionary<
                ManufacturingType,
                string
            >
            {
                { ManufacturingType.Ship, "Stale Destination" },
            };

            FacilityWindowRenderData data = _projector.CreateRenderData(
                _window,
                _session,
                staleDestinations
            );

            Assert.AreEqual(
                "Destination: Live Destination",
                data.ManufacturingCards[0].DestinationText
            );
        }

        private static Building CreateBuilding(
            string instanceId,
            string displayName,
            BuildingType buildingType,
            ManufacturingStatus status
        )
        {
            return new Building
            {
                InstanceID = instanceId,
                TypeID = buildingType.ToString(),
                DisplayName = displayName,
                OwnerInstanceID = _ownerId,
                BuildingType = buildingType,
                ManufacturingStatus = status,
                DisplayImagePath = "building-display",
                SmallDisplayImagePath = "building-small",
                InTransitSmallImagePath = "building-transit",
            };
        }

        private Dictionary<string, Texture2D> CreateTextures()
        {
            _constructionTexture = new Texture2D(4, 2);
            Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>
            {
                ["title-active"] = new Texture2D(1, 1),
                ["title-inactive"] = new Texture2D(1, 1),
                ["control-active"] = new Texture2D(1, 1),
                ["control-inactive"] = new Texture2D(1, 1),
                ["control-disabled"] = new Texture2D(1, 1),
                ["lane-active"] = new Texture2D(1, 1),
                ["lane-inactive"] = new Texture2D(1, 1),
                ["selection"] = new Texture2D(1, 1),
                ["raw-resource"] = new Texture2D(1, 1),
                ["building-display"] = new Texture2D(1, 1),
                ["building-small"] = new Texture2D(1, 1),
                ["building-transit"] = new Texture2D(1, 1),
                ["ship-display"] = new Texture2D(1, 1),
                ["ship-small"] = new Texture2D(1, 1),
                ["construction"] = _constructionTexture,
            };
            _textures = textures.Values.Distinct().ToList();
            return textures;
        }

        private static FactionTheme CreateTheme()
        {
            return new FactionTheme
            {
                FactionInstanceID = _ownerId,
                WindowTitleTheme = new WindowTitleTheme
                {
                    ActiveImagePath = "title-active",
                    InactiveImagePath = "title-inactive",
                },
                StrategyWindows = new StrategyWindowsTheme
                {
                    Facility = new FacilityWindowTheme
                    {
                        ControlTab = new WindowTabImageTheme
                        {
                            ActiveImagePath = "control-active",
                            InactiveImagePath = "control-inactive",
                            DisabledImagePath = "control-disabled",
                        },
                        SelectionImagePath = "selection",
                        RawResourceNodeImagePath = "raw-resource",
                        ConstructionImages = new List<FacilityConstructionImageTheme>
                        {
                            new FacilityConstructionImageTheme
                            {
                                TypeID = BuildingType.Mine.ToString(),
                                ImagePath = "construction",
                            },
                        },
                    },
                },
                PlanetWindowTheme = new PlanetWindowTheme
                {
                    BuildingsPane = new BuildingsPaneTheme
                    {
                        ManufacturingLaneState = new ManufacturingLaneStateTheme
                        {
                            ActiveImagePath = "lane-active",
                            InactiveImagePath = "lane-inactive",
                        },
                    },
                },
            };
        }

        private static CapitalShip CreateCapitalShip(string instanceId, string displayName)
        {
            return new CapitalShip
            {
                InstanceID = instanceId,
                TypeID = "capital-ship",
                DisplayName = displayName,
                OwnerInstanceID = _ownerId,
                DisplayImagePath = "ship-display",
                SmallDisplayImagePath = "ship-small",
            };
        }
    }
}
