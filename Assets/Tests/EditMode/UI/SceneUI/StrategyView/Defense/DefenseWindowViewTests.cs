using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Defense
{
    [TestFixture]
    public class DefenseWindowViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/DefenseWindow.prefab";

        private Texture2D _texture;
        private DefenseWindowView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<DefenseWindowView>();
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
        public void Render_ItemsAndTabs_AppliesCompleteAuthoredPresentation()
        {
            DefenseWindowRenderData data = CreateRenderData(
                DefenseWindowTab.Personnel,
                new[]
                {
                    CreateItem("First", true, true, false),
                    CreateItem("Second", false, false, true),
                }
            );

            _view.Render(data);

            RectInt windowRect = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(17, windowRect.x);
            Assert.AreEqual(29, windowRect.y);
            Assert.AreSame(_texture, FindComponent<RawImage>("TitleImage").texture);
            Assert.AreEqual("Corellia", FindComponent<TextMeshProUGUI>("CaptionTextField").text);
            Assert.AreEqual("Personnel", FindComponent<TextMeshProUGUI>("TabTitleTextField").text);
            Assert.AreEqual(
                string.Empty,
                FindComponent<TextMeshProUGUI>("GarrisonRequirementTextField").text
            );
            Assert.AreSame(_texture, FindComponent<RawImage>("PersonnelTabButtonImage").texture);
            StrategyUnitCardView[] cards = FindItemCards();
            Assert.AreEqual(2, cards.Length);
            Assert.AreEqual(0, cards[0].Index);
            Assert.AreEqual(1, cards[1].Index);
            Assert.AreEqual("First", FindCardText(cards[0], "NameTextField").text);
            Assert.IsTrue(FindCardObject(cards[0], "EnrouteOverlayImage").activeSelf);
            Assert.IsTrue(FindCardObject(cards[0], "DamagedOverlayImage").activeSelf);
            Assert.IsTrue(FindCardObject(cards[0], "CapturedOverlayImage").activeSelf);
            Assert.IsTrue(FindCardObject(cards[0], "SelectionImage").activeSelf);
            Assert.IsFalse(FindCardObject(cards[1], "NameTextField").activeSelf);
            Assert.Greater(_view.ItemColumnCount, 0);
            Assert.Greater(_view.GetItemScrollContentHeight(2), 0);
        }

        [Test]
        public void Render_ShorterItemCollection_HidesUnusedCachedCards()
        {
            _view.Render(
                CreateRenderData(
                    DefenseWindowTab.Personnel,
                    new[] { CreateItem("First"), CreateItem("Second") }
                )
            );
            StrategyUnitCardView second = FindItemCards()[1];

            _view.Render(
                CreateRenderData(
                    DefenseWindowTab.Regiments,
                    new[] { CreateItem("Replacement", true, false, true) }
                )
            );

            Assert.IsFalse(second.gameObject.activeSelf);
            Assert.AreEqual("Replacement", FindCardText(FindItemCards()[0], "NameTextField").text);
        }

        [Test]
        public void Render_InvalidTabCount_ThrowsArgumentException()
        {
            DefenseWindowRenderData data = new DefenseWindowRenderData(
                0,
                0,
                _texture,
                "Caption",
                DefenseWindowTab.Personnel,
                "Title",
                string.Empty,
                Array.Empty<DefenseWindowTabRenderData>(),
                Array.Empty<StrategyUnitCardRenderData>()
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
        }

        [Test]
        public void Render_RegimentTab_AppliesGarrisonRequirement()
        {
            _view.Render(
                CreateRenderData(
                    DefenseWindowTab.Regiments,
                    Array.Empty<StrategyUnitCardRenderData>()
                )
            );

            Assert.AreEqual(
                "Garrison Requirement: 3",
                FindComponent<TextMeshProUGUI>("GarrisonRequirementTextField").text
            );
        }

        [Test]
        public void AuthoredRegimentLabels_MatchSourceBounds()
        {
            RectInt titleRect = UILayout.GetSourceRect(
                FindComponent<TextMeshProUGUI>("TabTitleTextField").rectTransform
            );
            RectInt requirementRect = UILayout.GetSourceRect(
                FindComponent<TextMeshProUGUI>("GarrisonRequirementTextField").rectTransform
            );

            Assert.AreEqual(new RectInt(2, 51, 231, 16), titleRect);
            Assert.AreEqual(new RectInt(2, 63, 228, 15), requirementRect);
        }

        [Test]
        public void ItemTemplate_StatusRendersAboveEntity()
        {
            StrategyUnitCardView itemTemplate = _viewObject
                .GetComponentsInChildren<StrategyUnitCardView>(true)
                .Single(item => item.name == "ItemCardTemplate");
            Transform entity = FindCardObject(itemTemplate, "EntityImage").transform;

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
        public void Render_InvalidTabOrder_ThrowsArgumentException()
        {
            DefenseWindowTabRenderData[] tabs = CreateTabs();
            tabs[0] = new DefenseWindowTabRenderData(
                DefenseWindowTab.Regiments,
                _texture,
                _texture
            );
            DefenseWindowRenderData data = new DefenseWindowRenderData(
                0,
                0,
                _texture,
                "Caption",
                DefenseWindowTab.Personnel,
                "Title",
                string.Empty,
                tabs,
                Array.Empty<StrategyUnitCardRenderData>()
            );

            Assert.Throws<ArgumentException>(() => _view.Render(data));
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
            DefenseWindowTab? requested = null;
            _view.TabRequested += (_, tab) => requested = tab;
            Button batteriesButton = FindComponent<Button>("BatteriesTabButtonImage");

            batteriesButton.onClick.Invoke();

            Assert.AreEqual(DefenseWindowTab.Batteries, requested);
        }

        [Test]
        public void ItemGestures_RenderedCard_RaiseIndexedSemanticEvents()
        {
            _view.Render(
                CreateRenderData(
                    DefenseWindowTab.Personnel,
                    new[] { CreateItem("Item", true, true, false) }
                )
            );
            StrategyUnitCardView card = FindItemCards().Single();
            UIComponentTestHelper.InvokeLifecycle(card, "Awake");
            int pressedIndex = -1;
            int releasedIndex = -1;
            int droppedIndex = -1;
            int doubleClickedIndex = -1;
            _view.ItemPressed += (_, index, _) => pressedIndex = index;
            _view.ItemReleased += (_, index, _) => releasedIndex = index;
            _view.ItemDropped += (_, index, _) => droppedIndex = index;
            _view.ItemDoubleClicked += (_, index, _) => doubleClickedIndex = index;
            UIPointerGestureRelay relay = card.GetComponent<UIPointerGestureRelay>();
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
            Assert.AreEqual(0, droppedIndex);
            Assert.AreEqual(0, doubleClickedIndex);
        }

        [Test]
        public void ScrollGestures_AuthoredScrollArea_RaiseSemanticEvents()
        {
            PointerEventData eventData = new PointerEventData(null);
            PointerEventData dragged = null;
            PointerEventData ended = null;
            PointerEventData dropped = null;
            _view.ScrollDragged += (_, value) => dragged = value;
            _view.ScrollDragEnded += (_, value) => ended = value;
            _view.ItemsDropped += (_, value) => dropped = value;
            ScrollAreaView scrollArea = _viewObject.GetComponentInChildren<ScrollAreaView>(true);

            scrollArea.RelayDrag(eventData);
            scrollArea.RelayDragEnd(eventData);
            scrollArea.RelayDrop(eventData);
            scrollArea.RelayDrag(null);
            scrollArea.RelayDragEnd(null);

            Assert.AreSame(eventData, dragged);
            Assert.AreSame(eventData, ended);
            Assert.AreSame(eventData, dropped);
        }

        [Test]
        public void TabDrops_AllAuthoredTabs_RaisePlanetDestinationEvent()
        {
            PointerEventData eventData = new PointerEventData(null);
            int dropCount = 0;
            _view.ItemsDropped += (_, value) =>
            {
                Assert.AreSame(eventData, value);
                dropCount++;
            };
            UIPointerGestureRelay[] tabRelays = _view
                .GetComponentsInChildren<UIPointerGestureRelay>(true)
                .Where(relay => relay.name.EndsWith("TabButtonImage", StringComparison.Ordinal))
                .ToArray();

            foreach (UIPointerGestureRelay relay in tabRelays)
                relay.OnDrop(eventData);

            Assert.AreEqual(DefenseWindowRenderData.TabCount, tabRelays.Length);
            Assert.AreEqual(DefenseWindowRenderData.TabCount, dropCount);
        }

        [Test]
        public void ItemQueries_RenderedCard_ResolveIndexAndDragPreview()
        {
            _view.Render(
                CreateRenderData(
                    DefenseWindowTab.Personnel,
                    new[] { CreateItem("Item", true, true, false) }
                )
            );
            StrategyUnitCardView card = FindItemCards().Single();
            PointerEventData eventData = CreateRaycastEvent(card.gameObject);

            bool found = _view.TryGetItemIndex(eventData, out int itemIndex);
            bool selectionClick = _view.IsSelectionItemClick(eventData);
            bool previewCreated = _view.TryCreateDragPreview(0, 100, 120, out DragPreview preview);
            bool missingPreview = _view.TryCreateDragPreview(-1, 0, 0, out DragPreview missing);

            Assert.IsTrue(found);
            Assert.AreEqual(0, itemIndex);
            Assert.IsTrue(selectionClick);
            Assert.IsTrue(previewCreated);
            Assert.IsNotNull(preview);
            Assert.AreSame(_texture, preview.Texture);
            Assert.IsFalse(missingPreview);
            Assert.IsNull(missing);
            Assert.IsFalse(_view.ItemContainsDragSource(-1, eventData));
            Assert.IsFalse(_view.ItemContainsDragSource(0, null));
        }

        [Test]
        public void ItemQueries_MissingPointerTarget_ReturnFalseAndDefaultIndex()
        {
            bool found = _view.TryGetItemIndex(null, out int itemIndex);
            bool selectionClick = _view.IsSelectionItemClick(null);
            bool desktopPosition = _view.TryGetDesktopPosition(null, out int x, out int y);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, itemIndex);
            Assert.IsFalse(selectionClick);
            Assert.IsFalse(desktopPosition);
            Assert.AreEqual(0, x);
            Assert.AreEqual(0, y);
            Assert.IsNotNull(_view.WindowShell);
        }

        [Test]
        public void StrategyUnitCard_RenderAndDragState_UpdatesOptionalPresentation()
        {
            _view.Render(
                CreateRenderData(
                    DefenseWindowTab.Personnel,
                    new[] { CreateItem("Item", true, false, true) }
                )
            );
            StrategyUnitCardView card = FindItemCards().Single();

            bool hasDragImage = card.TryGetDragImage(
                out Texture texture,
                out RectTransform imageTransform
            );
            card.Render(CreateItem("Hidden", false, false, false));
            bool hasDisabledDragImage = card.TryGetDragImage(out _, out _);

            Assert.IsTrue(hasDragImage);
            Assert.AreSame(_texture, texture);
            Assert.IsNotNull(imageTransform);
            Assert.IsFalse(hasDisabledDragImage);
            Assert.IsFalse(FindCardObject(card, "NameTextField").activeSelf);
            Assert.Throws<ArgumentNullException>(() => card.Render(null));
        }

        [Test]
        public void OnDestroy_InitializedView_RaisesDestroyedEvent()
        {
            DefenseWindowView destroyed = null;
            _view.Destroyed += view => destroyed = view;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");

            Assert.AreSame(_view, destroyed);
        }

        private DefenseWindowRenderData CreateRenderData(
            DefenseWindowTab activeTab,
            StrategyUnitCardRenderData[] items
        )
        {
            return new DefenseWindowRenderData(
                17,
                29,
                _texture,
                "Corellia",
                activeTab,
                activeTab.ToString(),
                activeTab == DefenseWindowTab.Regiments ? "Garrison Requirement: 3" : string.Empty,
                CreateTabs(),
                items
            );
        }

        private DefenseWindowTabRenderData[] CreateTabs()
        {
            return DefenseWindowRenderData
                .OrderedTabs.Select(tab => new DefenseWindowTabRenderData(tab, _texture, _texture))
                .ToArray();
        }

        private StrategyUnitCardRenderData CreateItem(
            string name,
            bool canDrag = false,
            bool showOptionalImages = false,
            bool alternateNameLayout = false
        )
        {
            Texture optionalTexture = showOptionalImages ? _texture : null;
            return new StrategyUnitCardRenderData(
                name,
                Color.white,
                !string.Equals(name, "Second", StringComparison.Ordinal)
                    && !string.Equals(name, "Hidden", StringComparison.Ordinal),
                alternateNameLayout,
                optionalTexture,
                optionalTexture,
                optionalTexture,
                _texture,
                optionalTexture,
                optionalTexture,
                1,
                optionalTexture,
                optionalTexture,
                optionalTexture,
                canDrag
            );
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

        private StrategyUnitCardView[] FindItemCards()
        {
            return _viewObject
                .GetComponentsInChildren<StrategyUnitCardView>(true)
                .Where(card =>
                    card.name.StartsWith("ItemCard", StringComparison.Ordinal)
                    && card.name != "ItemCardTemplate"
                )
                .OrderBy(card => card.Index)
                .ToArray();
        }

        private static GameObject FindCardObject(StrategyUnitCardView card, string objectName)
        {
            return card.GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }

        private static TextMeshProUGUI FindCardText(StrategyUnitCardView card, string objectName)
        {
            return card.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Single(text => text.name == objectName);
        }
    }
}
