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
    public class GalacticInformationSubmenuViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private Texture2D _texture;
        private GalacticInformationSubmenuView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = FindTransform("ResourcesSubmenu")
                .GetComponent<GalacticInformationSubmenuView>();
            _texture = new Texture2D(45, 45);
            UIComponentTestHelper.InvokeLifecycle(
                _view.GetComponentInChildren<GalacticInformationFrameView>(true),
                "Awake"
            );
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_VisibleSubmenu_AppliesBoundsBackgroundFrameAndRows()
        {
            RectInt firstBounds = new RectInt(7, 9, 82, 14);
            RectInt secondBounds = new RectInt(7, 23, 82, 14);
            GalacticInformationSubmenuRenderData data = CreateSubmenu(
                new[]
                {
                    CreateFilter(
                        GalacticInformationFilterMode.AvailableRawMaterial,
                        true,
                        firstBounds,
                        "Raw Materials",
                        Color.yellow
                    ),
                    CreateFilter(
                        GalacticInformationFilterMode.AvailableEnergy,
                        true,
                        secondBounds,
                        "Energy",
                        Color.cyan
                    ),
                }
            );

            _view.Render(data, 3);

            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.AreEqual(
                new RectInt(40, 50, 120, 90),
                UILayout.GetSourceRect(_view.transform as RectTransform)
            );
            Image background = FindChild<Image>(_view, "BackgroundImage");
            Assert.AreEqual(new Color32(20, 30, 40, 255), (Color32)background.color);
            Assert.AreEqual(
                new RectInt(0, 0, 120, 90),
                UILayout.GetSourceRect(background.rectTransform)
            );
            UIRaycastArea firstHitArea = FindHitArea(firstBounds);
            Assert.IsTrue(firstHitArea.gameObject.activeSelf);
            RawImage firstIcon = FindFilterIcon(firstBounds);
            Assert.AreSame(_texture, firstIcon.texture);
            Assert.IsFalse(firstIcon.raycastTarget);
            TextMeshProUGUI firstText = FindFilterText(firstBounds);
            Assert.AreEqual("Raw Materials", firstText.text);
            Assert.AreEqual(Color.yellow, firstText.color);
        }

        [Test]
        public void Render_HiddenFilter_HidesAuthoredRowSlot()
        {
            RectInt firstBounds = new RectInt(7, 9, 82, 14);
            RectInt secondBounds = new RectInt(7, 23, 82, 14);
            GalacticInformationSubmenuRenderData data = CreateSubmenu(
                new[]
                {
                    CreateFilter(
                        GalacticInformationFilterMode.AvailableRawMaterial,
                        true,
                        firstBounds,
                        "Raw Materials",
                        Color.white
                    ),
                    CreateFilter(
                        GalacticInformationFilterMode.AvailableEnergy,
                        false,
                        secondBounds,
                        "Energy",
                        Color.white
                    ),
                }
            );

            _view.Render(data, 3);

            Assert.AreEqual(1, FindActiveFilterIcons().Length);
            Assert.AreEqual(1, FindActiveFilterTexts().Length);
            Assert.AreEqual(1, FindActiveFilterHitAreas().Length);
        }

        [Test]
        public void Render_NullData_HidesSubmenuAndRows()
        {
            _view.Render(
                CreateSubmenu(
                    new[]
                    {
                        CreateFilter(
                            GalacticInformationFilterMode.AvailableEnergy,
                            true,
                            new RectInt(7, 9, 82, 14),
                            "Energy",
                            Color.white
                        ),
                    }
                ),
                3
            );

            _view.Render(null, 3);

            Assert.IsFalse(_view.gameObject.activeSelf);
            Assert.IsEmpty(FindActiveFilterIcons());
            Assert.IsEmpty(FindActiveFilterTexts());
            Assert.IsEmpty(FindActiveFilterHitAreas());
        }

        [Test]
        public void Render_ShorterFilterCollection_HidesUnusedAuthoredRows()
        {
            _view.Render(
                CreateSubmenu(
                    new[]
                    {
                        CreateFilter(
                            GalacticInformationFilterMode.AvailableRawMaterial,
                            true,
                            new RectInt(7, 9, 82, 14),
                            "Raw Materials",
                            Color.white
                        ),
                        CreateFilter(
                            GalacticInformationFilterMode.AvailableEnergy,
                            true,
                            new RectInt(7, 23, 82, 14),
                            "Energy",
                            Color.white
                        ),
                    }
                ),
                3
            );

            _view.Render(
                CreateSubmenu(
                    new[]
                    {
                        CreateFilter(
                            GalacticInformationFilterMode.Mines,
                            true,
                            new RectInt(7, 9, 82, 14),
                            "Mines",
                            Color.white
                        ),
                    }
                ),
                3
            );

            Assert.AreEqual(1, FindActiveFilterIcons().Length);
            Assert.AreEqual("Mines", FindActiveFilterTexts().Single().text);
        }

        [Test]
        public void FilterHitArea_Interact_RaisesCategoryIndexFilterIndexAndMode()
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
            RectInt hitBounds = new RectInt(7, 9, 82, 14);
            _view.Render(
                CreateSubmenu(
                    new[]
                    {
                        CreateFilter(
                            GalacticInformationFilterMode.AvailableEnergy,
                            true,
                            hitBounds,
                            "Energy",
                            Color.white
                        ),
                    }
                ),
                4
            );
            UIRaycastArea hitArea = FindHitArea(hitBounds);
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            hitArea.OnPointerEnter(eventData);
            hitArea.OnPointerExit(eventData);
            hitArea.OnPointerClick(eventData);

            Assert.AreEqual(4, enteredCategory);
            Assert.AreEqual(0, enteredFilter);
            Assert.AreEqual(4, exitedCategory);
            Assert.AreEqual(0, exitedFilter);
            Assert.AreEqual(GalacticInformationFilterMode.AvailableEnergy, selectedMode);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsFilterHitAreas()
        {
            int enteredCount = 0;
            int exitedCount = 0;
            int selectedCount = 0;
            _view.FilterEntered += (_, _) => enteredCount++;
            _view.FilterExited += (_, _) => exitedCount++;
            _view.FilterSelected += _ => selectedCount++;
            RectInt hitBounds = new RectInt(7, 9, 82, 14);
            _view.Render(
                CreateSubmenu(
                    new[]
                    {
                        CreateFilter(
                            GalacticInformationFilterMode.AvailableEnergy,
                            true,
                            hitBounds,
                            "Energy",
                            Color.white
                        ),
                    }
                ),
                4
            );
            UIRaycastArea hitArea = FindHitArea(hitBounds);
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            hitArea.OnPointerEnter(eventData);
            hitArea.OnPointerExit(eventData);
            hitArea.OnPointerClick(eventData);

            Assert.AreEqual(0, enteredCount);
            Assert.AreEqual(0, exitedCount);
            Assert.AreEqual(0, selectedCount);
        }

        private GalacticInformationSubmenuRenderData CreateSubmenu(
            GalacticInformationFilterRenderData[] filters
        )
        {
            return new GalacticInformationSubmenuRenderData(
                true,
                new RectInt(40, 50, 120, 90),
                new Color32(20, 30, 40, 255),
                new GalacticInformationFrameRenderData(
                    120,
                    90,
                    Enumerable.Repeat(_texture, 8).ToArray()
                ),
                filters
            );
        }

        private GalacticInformationFilterRenderData CreateFilter(
            GalacticInformationFilterMode mode,
            bool visible,
            RectInt hitBounds,
            string label,
            Color color
        )
        {
            return new GalacticInformationFilterRenderData(
                mode,
                visible,
                hitBounds,
                new GalacticInformationImageRenderData(_texture, hitBounds),
                new GalacticInformationTextRenderData(label, color, hitBounds)
            );
        }

        private UIRaycastArea FindHitArea(RectInt bounds)
        {
            return _view
                .GetComponentsInChildren<UIRaycastArea>(true)
                .Single(area => UILayout.GetSourceRect(area.transform as RectTransform) == bounds);
        }

        private RawImage FindFilterIcon(RectInt bounds)
        {
            return FindActiveFilterIcons()
                .Single(image => UILayout.GetSourceRect(image.rectTransform) == bounds);
        }

        private TextMeshProUGUI FindFilterText(RectInt bounds)
        {
            return FindActiveFilterTexts()
                .Single(text => UILayout.GetSourceRect(text.rectTransform) == bounds);
        }

        private RawImage[] FindActiveFilterIcons()
        {
            return _view
                .GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.EndsWith("FilterIconImage", StringComparison.Ordinal))
                .Where(image => image.gameObject.activeSelf)
                .ToArray();
        }

        private TextMeshProUGUI[] FindActiveFilterTexts()
        {
            return _view
                .GetComponentsInChildren<TextMeshProUGUI>(true)
                .Where(text => text.name.EndsWith("FilterTextField", StringComparison.Ordinal))
                .Where(text => text.gameObject.activeSelf)
                .ToArray();
        }

        private UIRaycastArea[] FindActiveFilterHitAreas()
        {
            return _view
                .GetComponentsInChildren<UIRaycastArea>(true)
                .Where(area => area.name.EndsWith("FilterHitArea", StringComparison.Ordinal))
                .Where(area => area.gameObject.activeSelf)
                .ToArray();
        }

        private static T FindChild<T>(Component parent, string objectName)
            where T : Component
        {
            return parent
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private Transform FindTransform(string objectName)
        {
            return _rootObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName);
        }
    }
}
