using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Fleet
{
    [TestFixture]
    public class FleetWindowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/FleetWindow.prefab";

        private Texture2D _texture;
        private FleetWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<FleetWindowView>();
            _texture = new Texture2D(90, 45);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_SelectedFleet_AppliesListBannerTabsCapacityAndDetails()
        {
            FleetWindowRenderData data = CreateRenderData(
                true,
                new[] { CreateFleetRow("First Fleet", true), CreateFleetRow("Second Fleet") },
                new[] { CreateDetailItem("First Ship", true), CreateDetailItem("Second Ship") }
            );

            _view.Render(data);

            RectInt windowRect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(17, windowRect.x);
            Assert.AreEqual(29, windowRect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("TitleImage").texture);
            Assert.AreEqual("Corellia", FindComponent<TextMeshProUGUI>("CaptionTextField").text);
            Assert.AreSame(_texture, FindComponent<RawImage>("DetailBackgroundImage").texture);
            FleetListRowView[] rows = FindFleetRows();
            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("First Fleet", rows[0].NameTextField.text);
            Assert.IsTrue(FindRowObject(rows[0], "SelectionImage").activeSelf);
            Assert.IsTrue(FindRowObject(rows[0], "EnrouteOverlayImage").activeSelf);
            Assert.IsTrue(FindRowObject(rows[0], "DamagedOverlayImage").activeSelf);
            Assert.IsTrue(FindRowObject(rows[0], "StarfighterBadgeImage").activeSelf);
            Assert.IsTrue(FindRowObject(rows[0], "TroopBadgeImage").activeSelf);
            Assert.IsTrue(FindRowObject(rows[0], "PersonnelBadgeImage").activeSelf);
            Assert.AreSame(_texture, FindComponent<RawImage>("BannerImage").texture);
            Assert.IsTrue(FindObject("BannerEnrouteOverlayImage").activeSelf);
            Assert.IsTrue(FindObject("BannerDamagedOverlayImage").activeSelf);
            Assert.AreEqual(
                "First Fleet",
                FindComponent<TextMeshProUGUI>("FleetNameTextField").text
            );
            Assert.AreEqual("2", FindComponent<TextMeshProUGUI>("CapacityLeftTextField").text);
            Assert.AreEqual("6", FindComponent<TextMeshProUGUI>("CapacityRightTextField").text);
            Assert.IsTrue(FindObject("CapitalShipsTabButtonImage").activeSelf);
            StrategyUnitCardView[] items = FindDetailItems();
            Assert.AreEqual(2, items.Length);
            Assert.AreEqual("First Ship", items[0].NameTextField.text);
            Assert.IsTrue(FindCardObject(items[0], "ConstructionOverlayImage").activeSelf);
        }

        [Test]
        public void Render_NoSelectedFleet_HidesSelectedFleetPresentationAndDetailItems()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateFleetRow("First Fleet") },
                    new[] { CreateDetailItem("Ship") }
                )
            );
            StrategyUnitCardView detailItem = FindDetailItems().Single();

            _view.Render(
                CreateRenderData(
                    false,
                    new[] { CreateFleetRow("First Fleet") },
                    Array.Empty<StrategyUnitCardRenderData>()
                )
            );

            Assert.IsFalse(FindObject("BannerImage").activeSelf);
            Assert.IsFalse(FindObject("FleetNameTextField").activeSelf);
            Assert.IsFalse(FindObject("CapacityLeftTextField").activeSelf);
            Assert.IsFalse(FindObject("CapacityRightTextField").activeSelf);
            Assert.IsFalse(FindObject("Tabs").activeSelf);
            Assert.IsFalse(detailItem.gameObject.activeSelf);
        }

        [Test]
        public void Render_SelectedFleetWithoutCapacity_HidesCapacityFields()
        {
            FleetWindowRenderData data = CreateRenderData(
                true,
                new[] { CreateFleetRow("Fleet") },
                Array.Empty<StrategyUnitCardRenderData>(),
                false
            );

            _view.Render(data);

            Assert.IsFalse(FindObject("CapacityLeftTextField").activeSelf);
            Assert.IsFalse(FindObject("CapacityRightTextField").activeSelf);
        }

        [Test]
        public void Render_ShorterCollections_HidesUnusedCachedRowsAndDetailItems()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateFleetRow("First"), CreateFleetRow("Second") },
                    new[] { CreateDetailItem("First"), CreateDetailItem("Second") }
                )
            );
            FleetListRowView secondRow = FindFleetRows()[1];
            StrategyUnitCardView secondItem = FindDetailItems()[1];

            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateFleetRow("Replacement") },
                    new[] { CreateDetailItem("Replacement") }
                )
            );

            Assert.IsFalse(secondRow.gameObject.activeSelf);
            Assert.IsFalse(secondItem.gameObject.activeSelf);
            Assert.AreEqual("Replacement", FindFleetRows()[0].NameTextField.text);
            Assert.AreEqual("Replacement", FindDetailItems()[0].NameTextField.text);
        }

        [Test]
        public void Render_InvalidTabCount_ThrowsArgumentException()
        {
            FleetWindowRenderData data = CreateRenderData(
                true,
                Array.Empty<FleetListRowRenderData>(),
                Array.Empty<StrategyUnitCardRenderData>(),
                true,
                Array.Empty<FleetWindowTabRenderData>()
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        [Test]
        public void Render_InvalidTabOrder_ThrowsArgumentException()
        {
            FleetWindowTabRenderData[] tabs = CreateTabs();
            tabs[0] = new FleetWindowTabRenderData(FleetWindowTab.Starfighters, _texture, _texture);
            FleetWindowRenderData data = CreateRenderData(
                true,
                Array.Empty<FleetListRowRenderData>(),
                Array.Empty<StrategyUnitCardRenderData>(),
                true,
                tabs
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        [Test]
        public void Render_FleetRowRename_SubmitEmitsTrimmedValueAndRestoresLabel()
        {
            string submitted = null;
            _view.RenameSubmitted += (_, value) => submitted = value;
            FleetWindowRenderData data = CreateRenderData(
                true,
                new[] { CreateFleetRow("Fleet") },
                new[] { CreateDetailItem("Ship") },
                true,
                null,
                0,
                -1,
                "Fleet"
            );

            _view.Render(data);
            TMP_InputField input = _viewObject.GetComponentInChildren<TMP_InputField>(true);
            FleetListRowView row = FindFleetRows().Single();
            input.SetTextWithoutNotify("  Renamed Fleet  ");
            input.onSubmit.Invoke(input.text);

            Assert.AreEqual("Renamed Fleet", submitted);
            Assert.IsFalse(input.gameObject.activeSelf);
            Assert.IsTrue(row.NameTextField.enabled);
        }

        [Test]
        public void Render_DetailItemRename_EndEditEmitsCancellationAndRestoresLabel()
        {
            int cancelledCount = 0;
            _view.RenameCancelled += _ => cancelledCount++;
            FleetWindowRenderData data = CreateRenderData(
                true,
                new[] { CreateFleetRow("Fleet") },
                new[] { CreateDetailItem("Ship") },
                true,
                null,
                -1,
                0,
                "Ship"
            );

            _view.Render(data);
            TMP_InputField input = _viewObject.GetComponentInChildren<TMP_InputField>(true);
            StrategyUnitCardView item = FindDetailItems().Single();
            input.onEndEdit.Invoke(input.text);

            Assert.AreEqual(1, cancelledCount);
            Assert.IsFalse(input.gameObject.activeSelf);
            Assert.IsTrue(item.NameTextField.enabled);
        }

        [Test]
        public void Render_RemovedRenameTarget_EndsPresentationWithoutSubmitting()
        {
            int submittedCount = 0;
            int cancelledCount = 0;
            _view.RenameSubmitted += (_, _) => submittedCount++;
            _view.RenameCancelled += _ => cancelledCount++;
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateFleetRow("Fleet") },
                    Array.Empty<StrategyUnitCardRenderData>(),
                    true,
                    null,
                    0,
                    -1,
                    "Fleet"
                )
            );

            _view.Render(
                CreateRenderData(
                    true,
                    Array.Empty<FleetListRowRenderData>(),
                    Array.Empty<StrategyUnitCardRenderData>()
                )
            );

            Assert.AreEqual(0, submittedCount);
            Assert.AreEqual(0, cancelledCount);
            Assert.IsFalse(
                _viewObject.GetComponentInChildren<TMP_InputField>(true).gameObject.activeSelf
            );
        }

        [Test]
        public void OnPointerClick_PrimaryThenSecondaryClick_RaisesOnlyPrimarySurfaceEvent()
        {
            PointerEventData received = null;
            int clickCount = 0;
            _view.SurfaceClicked += (_, eventData) =>
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
        public void AuthoredTabButton_Click_RaisesSemanticTabRequest()
        {
            FleetWindowTab? requested = null;
            _view.TabRequested += (_, tab) => requested = tab;
            Button personnelButton = FindComponent<Button>("PersonnelTabButtonImage");

            personnelButton.onClick.Invoke();

            Assert.AreEqual(FleetWindowTab.Personnel, requested);
        }

        [Test]
        public void FleetRowGestures_RenderedRow_RaiseIndexedSemanticEvents()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateFleetRow("Fleet") },
                    Array.Empty<StrategyUnitCardRenderData>()
                )
            );
            FleetListRowView row = FindFleetRows().Single();
            UIComponentTestHelper.InvokeLifecycle(row, "Awake");
            int pressedIndex = -1;
            int releasedIndex = -1;
            int droppedIndex = -1;
            int doubleClickedIndex = -1;
            _view.FleetRowPressed += (_, index, _) => pressedIndex = index;
            _view.FleetRowReleased += (_, index, _) => releasedIndex = index;
            _view.FleetRowDropped += (_, index, _) => droppedIndex = index;
            _view.FleetRowDoubleClicked += (_, index, _) => doubleClickedIndex = index;
            UIPointerGestureRelay relay = row.GetComponent<UIPointerGestureRelay>();
            PointerEventData eventData = CreateDoubleClickEvent();

            relay.OnPointerDown(eventData);
            relay.OnPointerClick(eventData);
            relay.OnDrop(eventData);

            Assert.AreEqual(0, pressedIndex);
            Assert.AreEqual(0, releasedIndex);
            Assert.AreEqual(0, droppedIndex);
            Assert.AreEqual(0, doubleClickedIndex);
        }

        [Test]
        public void DetailItemGestures_RenderedItem_RaiseIndexedSemanticEvents()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateFleetRow("Fleet") },
                    new[] { CreateDetailItem("Ship", true) }
                )
            );
            StrategyUnitCardView item = FindDetailItems().Single();
            UIComponentTestHelper.InvokeLifecycle(item, "Awake");
            int pressedIndex = -1;
            int releasedIndex = -1;
            int droppedIndex = -1;
            int doubleClickedIndex = -1;
            _view.DetailItemPressed += (_, index, _) => pressedIndex = index;
            _view.DetailItemReleased += (_, index, _) => releasedIndex = index;
            _view.DetailItemDropped += (_, index, _) => droppedIndex = index;
            _view.DetailItemDoubleClicked += (_, index, _) => doubleClickedIndex = index;
            UIPointerGestureRelay relay = item.GetComponent<UIPointerGestureRelay>();
            PointerEventData eventData = CreateDoubleClickEvent();

            relay.OnPointerDown(eventData);
            relay.OnPointerClick(eventData);
            relay.OnDrop(eventData);

            Assert.AreEqual(0, pressedIndex);
            Assert.AreEqual(0, releasedIndex);
            Assert.AreEqual(0, droppedIndex);
            Assert.AreEqual(0, doubleClickedIndex);
        }

        [Test]
        public void ScrollGestures_BothScrollAreas_RaiseSharedAndFleetListEvents()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateFleetRow("Fleet") },
                    new[] { CreateDetailItem("Ship") }
                )
            );
            PointerEventData eventData = new PointerEventData(null);
            int draggedCount = 0;
            int endedCount = 0;
            int fleetListDropCount = 0;
            _view.ScrollDragged += (_, _) => draggedCount++;
            _view.ScrollDragEnded += (_, _) => endedCount++;
            _view.FleetListDropped += (_, _) => fleetListDropCount++;
            ScrollAreaView fleetScrollArea = FindFleetRows()[0]
                .GetComponentInParent<ScrollAreaView>();
            ScrollAreaView detailScrollArea = FindDetailItems()[0]
                .GetComponentInParent<ScrollAreaView>();

            fleetScrollArea.RelayDrag(eventData);
            fleetScrollArea.RelayDragEnd(eventData);
            fleetScrollArea.RelayDrop(eventData);
            detailScrollArea.RelayDrag(eventData);
            detailScrollArea.RelayDragEnd(eventData);

            Assert.AreEqual(2, draggedCount);
            Assert.AreEqual(2, endedCount);
            Assert.AreEqual(1, fleetListDropCount);
        }

        [Test]
        public void SelectionQueries_RenderedRowsAndItems_ResolveSemanticIndexes()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateFleetRow("Fleet") },
                    new[] { CreateDetailItem("Ship") }
                )
            );
            FleetListRowView row = FindFleetRows().Single();
            StrategyUnitCardView item = FindDetailItems().Single();
            PointerEventData rowEvent = CreateRaycastEvent(row.gameObject);
            PointerEventData itemEvent = CreateRaycastEvent(item.gameObject);

            bool foundRow = _view.TryGetFleetRowIndex(rowEvent, out int rowIndex);
            bool foundItem = _view.TryGetDetailItemIndex(itemEvent, out int itemIndex);
            bool rowSelection = _view.IsSelectionItemClick(rowEvent);
            bool itemSelection = _view.IsSelectionItemClick(itemEvent);
            bool missingSelection = _view.IsSelectionItemClick(null);

            Assert.IsTrue(foundRow);
            Assert.AreEqual(0, rowIndex);
            Assert.IsTrue(foundItem);
            Assert.AreEqual(0, itemIndex);
            Assert.IsTrue(rowSelection);
            Assert.IsTrue(itemSelection);
            Assert.IsFalse(missingSelection);
        }

        [Test]
        public void DragPreview_FleetThenDetailSource_UsesSelectedVisualAndClearsState()
        {
            _view.Render(
                CreateRenderData(
                    true,
                    new[] { CreateFleetRow("Fleet") },
                    new[] { CreateDetailItem("Ship", true) }
                )
            );

            _view.SetFleetRowDragSource(0);
            bool fleetPreviewCreated = _view.TryGetDragPreview(
                100,
                120,
                out DragPreview fleetPreview
            );
            _view.SetDetailItemDragSource(0);
            bool detailPreviewCreated = _view.TryGetDragPreview(
                100,
                120,
                out DragPreview detailPreview
            );
            _view.ClearDragSource();
            bool clearedPreviewCreated = _view.TryGetDragPreview(
                0,
                0,
                out DragPreview clearedPreview
            );

            Assert.IsTrue(fleetPreviewCreated);
            Assert.AreSame(_texture, fleetPreview.Texture);
            Assert.IsTrue(detailPreviewCreated);
            Assert.AreSame(_texture, detailPreview.Texture);
            Assert.IsFalse(clearedPreviewCreated);
            Assert.IsNull(clearedPreview);
            Assert.IsFalse(_view.FleetRowContainsDragSource(-1, null));
            Assert.IsFalse(_view.DetailItemContainsDragSource(-1, null));
        }

        [Test]
        public void ChildViews_NullRenderData_ThrowArgumentNullException()
        {
            FleetListRowView rowTemplate = _viewObject
                .GetComponentsInChildren<FleetListRowView>(true)
                .Single(row => row.name == "FleetListRowTemplate");
            StrategyUnitCardView itemTemplate = _viewObject
                .GetComponentsInChildren<StrategyUnitCardView>(true)
                .Single(item => item.name == "FleetDetailItemTemplate");

            Assert.Throws<ArgumentNullException>(() => rowTemplate.Render(null));
            Assert.Throws<ArgumentNullException>(() => itemTemplate.Render(null));
        }

        [Test]
        public void DetailItemTemplate_ConstructionRendersBehindEntityAndStatusRendersAbove()
        {
            StrategyUnitCardView itemTemplate = _viewObject
                .GetComponentsInChildren<StrategyUnitCardView>(true)
                .Single(item => item.name == "FleetDetailItemTemplate");
            Transform entity = FindCardObject(itemTemplate, "EntityImage").transform;

            Assert.Less(
                FindCardObject(itemTemplate, "ConstructionOverlayImage")
                    .transform.GetSiblingIndex(),
                entity.GetSiblingIndex()
            );
            Assert.Greater(
                FindCardObject(itemTemplate, "EnrouteOverlayImage").transform.GetSiblingIndex(),
                entity.GetSiblingIndex()
            );
            Assert.Greater(
                FindCardObject(itemTemplate, "DamagedOverlayImage").transform.GetSiblingIndex(),
                entity.GetSiblingIndex()
            );
        }

        [Test]
        public void OnDestroy_InitializedView_RaisesDestroyedEvent()
        {
            FleetWindowView destroyed = null;
            _view.Destroyed += view => destroyed = view;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");

            Assert.AreSame(_view, destroyed);
        }

        private FleetWindowRenderData CreateRenderData(
            bool hasSelectedFleet,
            FleetListRowRenderData[] rows,
            StrategyUnitCardRenderData[] items,
            bool showCapacity = true,
            FleetWindowTabRenderData[] tabs = null,
            int renameFleetRowIndex = -1,
            int renameDetailItemIndex = -1,
            string renameText = ""
        )
        {
            return new FleetWindowRenderData(
                17,
                29,
                _texture,
                "Corellia",
                _texture,
                rows,
                FleetWindowTab.CapitalShips,
                hasSelectedFleet ? 0 : -1,
                hasSelectedFleet,
                _texture,
                _texture,
                _texture,
                "First Fleet",
                Color.white,
                showCapacity,
                "2",
                "6",
                tabs ?? CreateTabs(),
                items,
                renameFleetRowIndex,
                renameDetailItemIndex,
                renameText
            );
        }

        private FleetWindowTabRenderData[] CreateTabs()
        {
            return FleetWindowRenderData
                .OrderedTabs.Select(tab => new FleetWindowTabRenderData(tab, _texture, _texture))
                .ToArray();
        }

        private FleetListRowRenderData CreateFleetRow(string name, bool showOptionalImages = false)
        {
            Texture optionalTexture = showOptionalImages ? _texture : null;
            return new FleetListRowRenderData(
                name,
                _texture,
                optionalTexture,
                optionalTexture,
                optionalTexture,
                optionalTexture,
                optionalTexture,
                optionalTexture
            );
        }

        private StrategyUnitCardRenderData CreateDetailItem(
            string name,
            bool showOptionalImages = false
        )
        {
            Texture optionalTexture = showOptionalImages ? _texture : null;
            return new StrategyUnitCardRenderData(
                name,
                Color.white,
                true,
                false,
                optionalTexture,
                optionalTexture,
                optionalTexture,
                optionalTexture,
                _texture,
                optionalTexture,
                optionalTexture,
                0,
                optionalTexture,
                optionalTexture,
                optionalTexture,
                true
            );
        }

        private static PointerEventData CreateDoubleClickEvent()
        {
            return new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };
        }

        private static PointerEventData CreateRaycastEvent(GameObject target)
        {
            return new PointerEventData(null)
            {
                pointerCurrentRaycast = new RaycastResult { gameObject = target },
            };
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _viewObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private StrategyUnitCardView[] FindDetailItems()
        {
            return _viewObject
                .GetComponentsInChildren<StrategyUnitCardView>(true)
                .Where(item =>
                    item.name.StartsWith("FleetDetailItem", StringComparison.Ordinal)
                    && item.name != "FleetDetailItemTemplate"
                )
                .OrderBy(item => item.Index)
                .ToArray();
        }

        private FleetListRowView[] FindFleetRows()
        {
            return _viewObject
                .GetComponentsInChildren<FleetListRowView>(true)
                .Where(row =>
                    row.name.StartsWith("FleetListRow", StringComparison.Ordinal)
                    && row.name != "FleetListRowTemplate"
                )
                .OrderBy(row => row.Index)
                .ToArray();
        }

        private GameObject FindObject(string objectName)
        {
            return _viewObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private static GameObject FindCardObject(StrategyUnitCardView card, string objectName)
        {
            return card.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private static GameObject FindRowObject(FleetListRowView row, string objectName)
        {
            return row.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }
    }
}
