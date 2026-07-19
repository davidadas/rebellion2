using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Facility
{
    [TestFixture]
    public class FacilityWindowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/FacilityWindow.prefab";

        private Texture2D _texture;
        private FacilityWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<FacilityWindowView>();
            _texture = new Texture2D(90, 45);
            foreach (
                ManufacturingLaneCardView card in _viewObject.GetComponentsInChildren<ManufacturingLaneCardView>(
                    true
                )
            )
                UIComponentTestHelper.InvokeLifecycle(card, "Awake");
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            if (_viewObject != null)
                UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_ManufacturingMode_AppliesLanePresentationAndProgress()
        {
            ManufacturingLaneCardRenderData[] cards =
            {
                new ManufacturingLaneCardRenderData(
                    _texture,
                    _texture,
                    25,
                    100,
                    "Ship Construction",
                    "No Ships",
                    "Current Ship",
                    "Building 2",
                    "Destination: Corellia",
                    "1:2"
                ),
                new ManufacturingLaneCardRenderData(
                    _texture,
                    null,
                    0,
                    0,
                    "Troop Training",
                    "No Troops",
                    string.Empty,
                    string.Empty,
                    "Destination: Coruscant",
                    "0:1"
                ),
                new ManufacturingLaneCardRenderData(
                    _texture,
                    _texture,
                    100,
                    100,
                    "Facility Construction",
                    "No Facilities",
                    "Current Facility",
                    "Building 1",
                    "Destination: Kessel",
                    "2:2"
                ),
            };
            FacilityWindowRenderData data = CreateRenderData(
                FacilityWindowTab.Manufacturing,
                cards,
                Array.Empty<FacilityInventoryItemRenderData>()
            );

            _view.Render(data);

            RectInt windowRect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(17, windowRect.x);
            Assert.AreEqual(29, windowRect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("TitleImage").texture);
            Assert.AreEqual("Corellia", FindComponent<TextMeshProUGUI>("CaptionTextField").text);
            Assert.IsTrue(FindObject("ManufacturingStripImage").activeSelf);
            Assert.IsFalse(FindObject("Inventory").activeSelf);
            ManufacturingLaneCardView shipCard = FindCard("ShipyardsManufacturingLaneCard");
            Assert.AreEqual("Ship Construction", FindCardText(shipCard, "TitleTextField").text);
            Assert.AreEqual("Current Ship", FindCardText(shipCard, "CurrentNameTextField").text);
            Assert.IsTrue(FindCardObject(shipCard, "EntityImage").activeSelf);
            Assert.IsFalse(FindCardObject(shipCard, "EmptyTextField").activeSelf);
            Assert.IsTrue(FindCardObject(shipCard, "ProgressFillImage").activeSelf);
            Assert.Greater(
                UILayout
                    .GetSourceRect(
                        FindCardObject(shipCard, "ProgressFillImage").transform as RectTransform
                    )
                    .width,
                0
            );
            ManufacturingLaneCardView troopCard = FindCard("TrainingManufacturingLaneCard");
            Assert.IsFalse(FindCardObject(troopCard, "EntityImage").activeSelf);
            Assert.IsTrue(FindCardObject(troopCard, "EmptyTextField").activeSelf);
            Assert.AreEqual("No Troops", FindCardText(troopCard, "EmptyTextField").text);
        }

        [Test]
        public void Render_InventoryMode_AppliesGridSelectionAndHidesManufacturingCards()
        {
            FacilityWindowRenderData initial = CreateRenderData(
                FacilityWindowTab.Manufacturing,
                new[]
                {
                    new ManufacturingLaneCardRenderData(
                        _texture,
                        _texture,
                        0,
                        1,
                        "Title",
                        "Empty",
                        "Current",
                        "Count",
                        "Destination",
                        "Facilities"
                    ),
                },
                Array.Empty<FacilityInventoryItemRenderData>()
            );
            _view.Render(initial);
            FacilityWindowRenderData inventory = CreateRenderData(
                FacilityWindowTab.Mines,
                Array.Empty<ManufacturingLaneCardRenderData>(),
                new[]
                {
                    new FacilityInventoryItemRenderData(_texture, true),
                    new FacilityInventoryItemRenderData(_texture, false),
                    new FacilityInventoryItemRenderData(null, false),
                }
            );

            _view.Render(inventory);

            Assert.IsFalse(FindObject("ManufacturingStripImage").activeSelf);
            Assert.IsTrue(FindObject("Inventory").activeSelf);
            Assert.AreEqual(
                "Mines",
                FindComponent<TextMeshProUGUI>("InventoryTitleTextField").text
            );
            Assert.Greater(_view.InventoryColumnCount, 0);
            FacilityInventoryItemView[] items = FindInventoryItems();
            Assert.AreEqual(3, items.Length);
            Assert.AreEqual(0, items[0].Index);
            Assert.AreEqual(1, items[1].Index);
            Assert.AreEqual(2, items[2].Index);
            Assert.IsTrue(FindInventoryObject(items[0], "SelectionImage").activeSelf);
            Assert.IsFalse(FindInventoryObject(items[1], "SelectionImage").activeSelf);
            Assert.IsTrue(FindInventoryObject(items[2], "ItemImage").activeSelf);
            Assert.IsFalse(FindCard("ShipyardsManufacturingLaneCard").gameObject.activeSelf);
        }

        [Test]
        public void Render_ShorterInventory_HidesUnusedCachedItems()
        {
            _view.Render(
                CreateRenderData(
                    FacilityWindowTab.Mines,
                    Array.Empty<ManufacturingLaneCardRenderData>(),
                    new[]
                    {
                        new FacilityInventoryItemRenderData(_texture, false),
                        new FacilityInventoryItemRenderData(_texture, false),
                    }
                )
            );
            FacilityInventoryItemView second = FindInventoryItems()[1];

            _view.Render(
                CreateRenderData(
                    FacilityWindowTab.Mines,
                    Array.Empty<ManufacturingLaneCardRenderData>(),
                    new[] { new FacilityInventoryItemRenderData(_texture, false) }
                )
            );

            Assert.IsFalse(second.gameObject.activeSelf);
        }

        [Test]
        public void Render_InvalidTabCount_ThrowsArgumentException()
        {
            FacilityWindowRenderData data = new FacilityWindowRenderData(
                0,
                0,
                _texture,
                "Caption",
                FacilityWindowTab.Manufacturing,
                Array.Empty<FacilityWindowTabRenderData>(),
                _texture,
                _texture,
                Array.Empty<ManufacturingLaneCardRenderData>(),
                string.Empty,
                Array.Empty<FacilityInventoryItemRenderData>(),
                _texture
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        [Test]
        public void Render_InvalidTabOrder_ThrowsArgumentException()
        {
            FacilityWindowTabRenderData[] tabs = CreateTabs(FacilityWindowTab.Manufacturing);
            tabs[0] = new FacilityWindowTabRenderData(
                FacilityWindowTab.Shipyards,
                FacilityWindowTabState.Active
            );
            FacilityWindowRenderData data = new FacilityWindowRenderData(
                0,
                0,
                _texture,
                "Caption",
                FacilityWindowTab.Manufacturing,
                tabs,
                _texture,
                _texture,
                Array.Empty<ManufacturingLaneCardRenderData>(),
                string.Empty,
                Array.Empty<FacilityInventoryItemRenderData>(),
                _texture
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        [Test]
        public void OnPointerClick_PrimaryThenSecondaryClick_RaisesOnlyPrimaryBackgroundEvent()
        {
            PointerEventData received = null;
            int clickCount = 0;
            _view.BackgroundClicked += (_, eventData) =>
            {
                clickCount++;
                received = eventData;
            };
            PointerEventData leftClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };
            PointerEventData rightClick = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Right,
            };

            _view.OnPointerClick(leftClick);
            _view.OnPointerClick(rightClick);
            _view.OnPointerClick(null);

            Assert.AreEqual(1, clickCount);
            Assert.AreSame(leftClick, received);
        }

        [Test]
        public void AuthoredTabButton_Click_RaisesSemanticTabSelection()
        {
            FacilityWindowTab? selected = null;
            _view.TabSelected += (_, tab) => selected = tab;
            Button minesButton = FindComponent<Button>("MinesTabButtonImage");

            minesButton.onClick.Invoke();

            Assert.AreEqual(FacilityWindowTab.Mines, selected);
        }

        [Test]
        public void ManufacturingCardGestures_AuthoredCard_RaiseIndexedEvents()
        {
            int pressedIndex = -1;
            int releasedIndex = -1;
            int releasedCount = 0;
            _view.ManufacturingCardPressed += (_, index, _) => pressedIndex = index;
            _view.ManufacturingCardReleased += (_, index, _) =>
            {
                releasedCount++;
                releasedIndex = index;
            };
            ManufacturingLaneCardView card = FindCard("ShipyardsManufacturingLaneCard");
            UIPointerGestureRelay relay = card.GetComponent<UIPointerGestureRelay>();
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            relay.OnPointerDown(eventData);
            relay.OnPointerClick(eventData);
            relay.OnDrop(eventData);

            Assert.AreEqual((int)FacilityWindowTab.Shipyards, pressedIndex);
            Assert.AreEqual((int)FacilityWindowTab.Shipyards, releasedIndex);
            Assert.AreEqual(2, releasedCount);
        }

        [Test]
        public void InventoryItemGestures_RenderedItem_RaiseIndexedEvents()
        {
            _view.Render(
                CreateRenderData(
                    FacilityWindowTab.Mines,
                    Array.Empty<ManufacturingLaneCardRenderData>(),
                    new[] { new FacilityInventoryItemRenderData(_texture, false) }
                )
            );
            int pressedIndex = -1;
            int releasedIndex = -1;
            int doubleClickedIndex = -1;
            _view.InventoryItemPressed += (_, index, _) => pressedIndex = index;
            _view.InventoryItemReleased += (_, index, _) => releasedIndex = index;
            _view.InventoryItemDoubleClicked += (_, index, _) => doubleClickedIndex = index;
            FacilityInventoryItemView item = FindInventoryItems().Single();
            UIComponentTestHelper.InvokeLifecycle(item, "Awake");
            UIPointerGestureRelay relay = item.GetComponent<UIPointerGestureRelay>();
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };

            relay.OnPointerDown(eventData);
            relay.OnPointerClick(eventData);
            relay.OnDrop(eventData);

            Assert.AreEqual(0, pressedIndex);
            Assert.AreEqual(0, releasedIndex);
            Assert.AreEqual(0, doubleClickedIndex);
        }

        [Test]
        public void PointerTargetQueries_RenderedControls_ReturnSemanticIndexes()
        {
            _view.Render(
                CreateRenderData(
                    FacilityWindowTab.Manufacturing,
                    new[]
                    {
                        new ManufacturingLaneCardRenderData(
                            _texture,
                            _texture,
                            0,
                            1,
                            "Title",
                            "Empty",
                            "Current",
                            "Count",
                            "Destination",
                            "Facilities"
                        ),
                    },
                    Array.Empty<FacilityInventoryItemRenderData>()
                )
            );
            ManufacturingLaneCardView card = FindCard("ShipyardsManufacturingLaneCard");
            PointerEventData cardEvent = CreateRaycastEvent(card.gameObject);
            bool foundCard = _view.TryGetManufacturingCardIndex(cardEvent, out int cardIndex);

            _view.Render(
                CreateRenderData(
                    FacilityWindowTab.Mines,
                    Array.Empty<ManufacturingLaneCardRenderData>(),
                    new[] { new FacilityInventoryItemRenderData(_texture, false) }
                )
            );
            FacilityInventoryItemView item = FindInventoryItems().Single();
            PointerEventData itemEvent = CreateRaycastEvent(item.gameObject);
            bool foundItem = _view.TryGetInventoryItemIndex(itemEvent, out int itemIndex);
            bool foundMissing = _view.TryGetInventoryItemIndex(null, out int missingIndex);

            Assert.IsTrue(foundCard);
            Assert.AreEqual((int)FacilityWindowTab.Shipyards, cardIndex);
            Assert.IsTrue(foundItem);
            Assert.AreEqual(0, itemIndex);
            Assert.IsFalse(foundMissing);
            Assert.AreEqual(-1, missingIndex);
        }

        [Test]
        public void ChildViews_NullRenderData_ThrowArgumentNullException()
        {
            ManufacturingLaneCardView card = FindCard("ShipyardsManufacturingLaneCard");
            FacilityInventoryItemView template = _viewObject
                .GetComponentsInChildren<FacilityInventoryItemView>(true)
                .Single(item => item.name == "InventoryItemTemplate");

            Assert.Throws<ArgumentNullException>(() => card.Render(null));
            Assert.Throws<ArgumentNullException>(() =>
                template.Render(0, null, _texture, new RectInt(0, 0, 10, 10))
            );
        }

        [Test]
        public void OnDestroy_InitializedView_RaisesDestroyedEvent()
        {
            FacilityWindowView destroyed = null;
            _view.Destroyed += view => destroyed = view;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");

            Assert.AreSame(_view, destroyed);
        }

        private FacilityWindowRenderData CreateRenderData(
            FacilityWindowTab activeTab,
            IReadOnlyList<ManufacturingLaneCardRenderData> cards,
            IReadOnlyList<FacilityInventoryItemRenderData> items
        )
        {
            return new FacilityWindowRenderData(
                17,
                29,
                _texture,
                "Corellia",
                activeTab,
                CreateTabs(activeTab),
                _texture,
                _texture,
                cards,
                activeTab == FacilityWindowTab.Mines ? "Mines" : "Inventory",
                items,
                _texture
            );
        }

        private static FacilityWindowTabRenderData[] CreateTabs(FacilityWindowTab activeTab)
        {
            return FacilityWindowRenderData
                .OrderedTabs.Select(tab => new FacilityWindowTabRenderData(
                    tab,
                    tab == activeTab ? FacilityWindowTabState.Active
                        : tab == FacilityWindowTab.Construction ? FacilityWindowTabState.Disabled
                        : FacilityWindowTabState.Inactive
                ))
                .ToArray();
        }

        private static PointerEventData CreateRaycastEvent(GameObject target)
        {
            return new PointerEventData(null)
            {
                pointerCurrentRaycast = new RaycastResult { gameObject = target },
            };
        }

        private ManufacturingLaneCardView FindCard(string objectName)
        {
            return _viewObject
                .GetComponentsInChildren<ManufacturingLaneCardView>(true)
                .Single(card => card.name == objectName);
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _viewObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private FacilityInventoryItemView[] FindInventoryItems()
        {
            return _viewObject
                .GetComponentsInChildren<FacilityInventoryItemView>(true)
                .Where(item =>
                    item.name.StartsWith("InventoryItem", StringComparison.Ordinal)
                    && item.name != "InventoryItemTemplate"
                )
                .OrderBy(item => item.Index)
                .ToArray();
        }

        private GameObject FindObject(string objectName)
        {
            return _viewObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private static GameObject FindCardObject(ManufacturingLaneCardView card, string objectName)
        {
            return card.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private static TextMeshProUGUI FindCardText(
            ManufacturingLaneCardView card,
            string objectName
        )
        {
            return card.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == objectName);
        }

        private static GameObject FindInventoryObject(
            FacilityInventoryItemView item,
            string objectName
        )
        {
            return item.GetComponentsInChildren<Transform>(true)
                .Single(child => child.name == objectName)
                .gameObject;
        }
    }
}
