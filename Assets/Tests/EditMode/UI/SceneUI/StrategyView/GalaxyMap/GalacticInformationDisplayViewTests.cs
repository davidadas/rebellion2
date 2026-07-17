using System;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalacticInformationDisplayViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private Texture2D _texture;
        private GalacticInformationDisplayView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<GalacticInformationDisplayView>(true);
            _texture = new Texture2D(45, 45);
            foreach (
                GalacticInformationFrameView frame in _view.GetComponentsInChildren<GalacticInformationFrameView>(
                    true
                )
            )
            {
                UIComponentTestHelper.InvokeLifecycle(frame, "Awake");
            }
            foreach (
                GalacticInformationSubmenuView submenu in _view.GetComponentsInChildren<GalacticInformationSubmenuView>(
                    true
                )
            )
            {
                UIComponentTestHelper.InvokeLifecycle(submenu, "Awake");
            }
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_VisibleDisplay_AppliesSelectorCategoriesSubmenuAndDisplayOffRow()
        {
            GalacticInformationDisplayRenderData data = CreateDisplay(true, true);

            _view.Render(data);

            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.AreEqual(
                new RectInt(200, 120, 210, 160),
                UILayout.GetSourceRect(FindTransform("SelectorPanel") as RectTransform)
            );
            Image background = FindDirectChildComponent<Image>(
                FindTransform("SelectorPanel"),
                "BackgroundImage"
            );
            Assert.AreEqual(new Color32(10, 20, 30, 255), (Color32)background.color);
            Assert.AreEqual(
                new RectInt(0, 0, 210, 160),
                UILayout.GetSourceRect(background.rectTransform)
            );
            Assert.AreSame(_texture, FindComponent<RawImage>("LoyaltyCategoryIconImage").texture);
            Assert.AreSame(_texture, FindComponent<RawImage>("LoyaltyCategoryArrowImage").texture);
            Assert.AreEqual("Loyalty", FindText("LoyaltyCategoryTextField").text);
            Assert.AreEqual(Color.yellow, FindText("LoyaltyCategoryTextField").color);
            Assert.AreEqual(
                new RectInt(8, 12, 130, 18),
                UILayout.GetSourceRect(FindTransform("LoyaltyCategoryHitArea") as RectTransform)
            );
            Assert.IsTrue(FindTransform("LoyaltySubmenu").gameObject.activeSelf);
            Assert.IsFalse(FindTransform("FleetsSubmenu").gameObject.activeSelf);
            Assert.AreEqual("Display Off", FindText("DisplayOffText").text);
            Assert.AreEqual(Color.cyan, FindText("DisplayOffText").color);
        }

        [Test]
        public void Render_MissingCategoryIcon_HidesIconButRetainsArrowSlot()
        {
            GalacticInformationCategoryRenderData category = CreateCategory(
                true,
                null,
                null,
                false
            );
            GalacticInformationDisplayRenderData data = CreateDisplay(
                true,
                true,
                new[] { category }
            );

            _view.Render(data);

            Assert.IsFalse(FindTransform("LoyaltyCategoryIconImage").gameObject.activeSelf);
            Assert.IsTrue(FindTransform("LoyaltyCategoryArrowImage").gameObject.activeSelf);
            Assert.IsFalse(FindComponent<RawImage>("LoyaltyCategoryArrowImage").enabled);
        }

        [Test]
        public void Render_InvisibleCategoryAndDisplayOff_HidesAuthoredRows()
        {
            GalacticInformationCategoryRenderData category = CreateCategory(
                false,
                _texture,
                _texture,
                false
            );
            GalacticInformationDisplayRenderData data = CreateDisplay(
                true,
                false,
                new[] { category }
            );

            _view.Render(data);

            Assert.IsFalse(FindTransform("LoyaltyCategoryHitArea").gameObject.activeSelf);
            Assert.IsFalse(FindTransform("LoyaltyCategoryIconImage").gameObject.activeSelf);
            Assert.IsFalse(FindTransform("DisplayOffHitArea").gameObject.activeSelf);
            Assert.IsFalse(FindTransform("DisplayOffText").gameObject.activeSelf);
        }

        [Test]
        public void Render_HiddenDisplay_HidesDisplayAndEverySubmenu()
        {
            _view.Render(CreateDisplay(true, true));

            _view.Render(CreateDisplay(false, false));

            Assert.IsFalse(_view.gameObject.activeSelf);
            foreach (
                GalacticInformationSubmenuView submenu in _view.GetComponentsInChildren<GalacticInformationSubmenuView>(
                    true
                )
            )
            {
                Assert.IsFalse(submenu.gameObject.activeSelf);
            }
        }

        [Test]
        public void SelectorHitAreas_Interact_RaiseCategoryDisplayOffAndDismissRequests()
        {
            int requestedCategory = -1;
            int displayEnteredCount = 0;
            int displayExitedCount = 0;
            int displaySelectedCount = 0;
            int dismissCount = 0;
            _view.CategoryRequested += category => requestedCategory = category;
            _view.DisplayOffEntered += () => displayEnteredCount++;
            _view.DisplayOffExited += () => displayExitedCount++;
            _view.DisplayOffSelected += () => displaySelectedCount++;
            _view.DismissRequested += () => dismissCount++;
            _view.Render(CreateDisplay(true, true));
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            FindComponent<UIRaycastArea>("LoyaltyCategoryHitArea").OnPointerEnter(eventData);
            FindComponent<UIRaycastArea>("DisplayOffHitArea").OnPointerEnter(eventData);
            FindComponent<UIRaycastArea>("DisplayOffHitArea").OnPointerExit(eventData);
            FindComponent<UIRaycastArea>("DisplayOffHitArea").OnPointerClick(eventData);
            FindComponent<UIRaycastArea>("DismissHitArea").OnPointerClick(eventData);

            Assert.AreEqual(0, requestedCategory);
            Assert.AreEqual(1, displayEnteredCount);
            Assert.AreEqual(1, displayExitedCount);
            Assert.AreEqual(1, displaySelectedCount);
            Assert.AreEqual(1, dismissCount);
        }

        [Test]
        public void SubmenuFilterHitArea_Interact_ForwardsFilterEvents()
        {
            int enteredCategory = -1;
            int enteredFilter = -1;
            int exitedCategory = -1;
            int exitedFilter = -1;
            GalacticInformationFilterMode? selectedMode = null;
            _view.FilterEntered += (category, filter) =>
            {
                enteredCategory = category;
                enteredFilter = filter;
            };
            _view.FilterExited += (category, filter) =>
            {
                exitedCategory = category;
                exitedFilter = filter;
            };
            _view.FilterSelected += mode => selectedMode = mode;
            _view.Render(CreateDisplay(true, true));
            UIRaycastArea filter = FindComponent<UIRaycastArea>("PopularSupportFilterHitArea");
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            filter.OnPointerEnter(eventData);
            filter.OnPointerExit(eventData);
            filter.OnPointerClick(eventData);

            Assert.AreEqual(0, enteredCategory);
            Assert.AreEqual(0, enteredFilter);
            Assert.AreEqual(0, exitedCategory);
            Assert.AreEqual(0, exitedFilter);
            Assert.AreEqual(GalacticInformationFilterMode.PopularSupport, selectedMode);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsInputAndRaisesDestroyedEvent()
        {
            GalacticInformationDisplayView destroyed = null;
            int categoryCount = 0;
            int displayCount = 0;
            int filterCount = 0;
            int dismissCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.CategoryRequested += _ => categoryCount++;
            _view.DisplayOffSelected += () => displayCount++;
            _view.FilterSelected += _ => filterCount++;
            _view.DismissRequested += () => dismissCount++;
            _view.Render(CreateDisplay(true, true));
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<UIRaycastArea>("LoyaltyCategoryHitArea").OnPointerClick(eventData);
            FindComponent<UIRaycastArea>("DisplayOffHitArea").OnPointerClick(eventData);
            FindComponent<UIRaycastArea>("PopularSupportFilterHitArea").OnPointerClick(eventData);
            FindComponent<UIRaycastArea>("DismissHitArea").OnPointerClick(eventData);

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, categoryCount);
            Assert.AreEqual(0, displayCount);
            Assert.AreEqual(0, filterCount);
            Assert.AreEqual(0, dismissCount);
        }

        private GalacticInformationDisplayRenderData CreateDisplay(
            bool visible,
            bool displayOffVisible
        )
        {
            return CreateDisplay(
                visible,
                displayOffVisible,
                new[] { CreateCategory(true, _texture, _texture, true) }
            );
        }

        private GalacticInformationDisplayRenderData CreateDisplay(
            bool visible,
            bool displayOffVisible,
            GalacticInformationCategoryRenderData[] categories
        )
        {
            return new GalacticInformationDisplayRenderData(
                visible,
                new RectInt(200, 120, 210, 160),
                new Color32(10, 20, 30, 255),
                CreateFrame(210, 160),
                categories,
                new GalacticInformationTextRowRenderData(
                    displayOffVisible,
                    new RectInt(8, 136, 130, 18),
                    new GalacticInformationTextRenderData(
                        "Display Off",
                        Color.cyan,
                        new RectInt(34, 136, 100, 18)
                    )
                )
            );
        }

        private GalacticInformationCategoryRenderData CreateCategory(
            bool visible,
            Texture2D iconTexture,
            Texture2D arrowTexture,
            bool submenuVisible
        )
        {
            GalacticInformationFilterRenderData filter = new GalacticInformationFilterRenderData(
                GalacticInformationFilterMode.PopularSupport,
                true,
                new RectInt(7, 9, 82, 14),
                new GalacticInformationImageRenderData(_texture, new RectInt(7, 9, 14, 14)),
                new GalacticInformationTextRenderData(
                    "Popular Support",
                    Color.white,
                    new RectInt(25, 9, 64, 14)
                )
            );
            return new GalacticInformationCategoryRenderData(
                visible,
                new RectInt(8, 12, 130, 18),
                new GalacticInformationImageRenderData(iconTexture, new RectInt(10, 13, 16, 16)),
                new GalacticInformationImageRenderData(arrowTexture, new RectInt(2, 17, 6, 8)),
                new GalacticInformationTextRenderData(
                    "Loyalty",
                    Color.yellow,
                    new RectInt(30, 12, 100, 18)
                ),
                new GalacticInformationSubmenuRenderData(
                    submenuVisible,
                    new RectInt(-120, 0, 120, 60),
                    Color.black,
                    CreateFrame(120, 60),
                    new[] { filter }
                )
            );
        }

        private GalacticInformationFrameRenderData CreateFrame(int width, int height)
        {
            return new GalacticInformationFrameRenderData(
                width,
                height,
                Enumerable.Repeat(_texture, 8).ToArray()
            );
        }

        private T FindComponent<T>(string objectName, Transform root = null)
            where T : Component
        {
            return (root ?? _view.transform)
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private static T FindDirectChildComponent<T>(Transform root, string objectName)
            where T : Component
        {
            return root.GetComponentsInChildren<T>(true)
                .Single(component =>
                    component.name == objectName && component.transform.parent == root
                );
        }

        private TextMeshProUGUI FindText(string objectName)
        {
            return FindComponent<TextMeshProUGUI>(objectName);
        }

        private Transform FindTransform(string objectName)
        {
            return _view
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName);
        }
    }
}
