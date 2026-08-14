using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSystem
{
    [TestFixture]
    public class PlanetSystemPlanetViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/PlanetSystemPlanet.prefab";

        private Texture2D _headquartersTexture;
        private Texture2D _normalTexture;
        private Texture2D _planetTexture;
        private Texture2D _pressedTexture;
        private Texture2D _uprisingTexture;
        private GameObject _rootObject;
        private PlanetSystemPlanetView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponent<PlanetSystemPlanetView>();
            _planetTexture = new Texture2D(100, 80);
            _normalTexture = new Texture2D(24, 24);
            _pressedTexture = new Texture2D(24, 24);
            _headquartersTexture = new Texture2D(16, 16);
            _uprisingTexture = new Texture2D(167, 167);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_headquartersTexture);
            UnityEngine.Object.DestroyImmediate(_pressedTexture);
            UnityEngine.Object.DestroyImmediate(_normalTexture);
            UnityEngine.Object.DestroyImmediate(_planetTexture);
            UnityEngine.Object.DestroyImmediate(_uprisingTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null, Vector2Int.zero));
        }

        [Test]
        public void Render_CompletePresentation_AppliesImagesNamePositionAndBars()
        {
            PlanetSystemPlanetRenderData data = CreateData(
                PlanetIcon.Facility,
                PlanetIcon.Mission,
                CreateSegmentedBar(true, 4, 2),
                CreateSegmentedBar(false, 0, 0),
                CreateContinuousBar(true, 0.5f)
            );
            RectInt planetTemplate = GetSourceRect(GetField<RawImage>("planetImage").transform);

            _view.Render(data, new Vector2Int(200, 150));

            Assert.AreEqual(7, data.PlanetIndex);
            RectInt planetBounds = _view.GetRenderedPlanetImageSourceRect();
            Assert.AreEqual(200, planetBounds.x);
            Assert.AreEqual(150, planetBounds.y);
            Assert.AreEqual(planetTemplate.width, planetBounds.width);
            Assert.AreEqual(planetTemplate.height, planetBounds.height);
            Assert.AreSame(_planetTexture, GetField<RawImage>("planetImage").texture);
            RawImage uprisingImage = GetField<RawImage>("uprisingImage");
            Assert.AreSame(_uprisingTexture, uprisingImage.texture);
            Assert.AreEqual(planetTemplate, GetSourceRect(uprisingImage.transform));
            Assert.IsFalse(uprisingImage.raycastTarget);
            Assert.Less(
                GetField<RawImage>("planetImage").transform.GetSiblingIndex(),
                uprisingImage.transform.GetSiblingIndex()
            );
            Assert.Less(
                uprisingImage.transform.GetSiblingIndex(),
                GetField<RawImage>("missionImage").transform.GetSiblingIndex()
            );
            Assert.AreSame(_pressedTexture, GetField<RawImage>("facilityImage").texture);
            Assert.IsFalse(GetField<RawImage>("defenseImage").gameObject.activeSelf);
            Assert.AreSame(_normalTexture, GetField<RawImage>("fleetImage").texture);
            Assert.AreSame(_pressedTexture, GetField<RawImage>("missionImage").texture);
            Assert.AreSame(_headquartersTexture, GetField<RawImage>("headquartersImage").texture);
            TextMeshProUGUI name = GetField<TextMeshProUGUI>("planetNameTextField");
            Assert.AreEqual("Coruscant", name.text);
            Assert.AreEqual((Color)new Color32(10, 20, 30, 255), name.color);

            RectTransform energyRoot = GetField<RectTransform>("energyBarRoot");
            Image[] energyCells = GetField<Image[]>("energyBarCellImages");
            Assert.IsTrue(energyRoot.gameObject.activeSelf);
            Assert.IsFalse(GetField<Image>("energyBarFillImage").gameObject.activeSelf);
            Assert.AreEqual((Color)new Color32(0, 255, 0, 255), energyCells[0].color);
            Assert.AreEqual((Color)new Color32(0, 255, 0, 255), energyCells[1].color);
            Assert.AreEqual((Color)new Color32(255, 0, 0, 255), energyCells[2].color);
            Assert.AreEqual((Color)new Color32(255, 0, 0, 255), energyCells[3].color);
            Assert.IsFalse(energyCells[4].gameObject.activeSelf);
            Assert.IsFalse(GetField<RectTransform>("rawBarRoot").gameObject.activeSelf);

            RawImage planetImage = GetField<RawImage>("planetImage");
            Image supportFill = GetField<Image>("supportBarFillImage");
            Assert.IsTrue(GetField<RectTransform>("supportBarRoot").gameObject.activeSelf);
            Assert.IsTrue(supportFill.gameObject.activeSelf);
            Assert.AreEqual(
                Mathf.RoundToInt(planetTemplate.width * 0.5f),
                GetSourceRect(supportFill.transform).width
            );
            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.IsTrue(planetImage.raycastTarget);
        }

        [Test]
        public void Render_ContinuousZeroFill_HidesFillImage()
        {
            PlanetSystemPlanetRenderData data = CreateData(
                PlanetIcon.None,
                PlanetIcon.None,
                CreateContinuousBar(true, 0f),
                CreateContinuousBar(true, -1f),
                CreateContinuousBar(true, 0f)
            );

            _view.Render(data, new Vector2Int(200, 150));

            Assert.IsFalse(GetField<Image>("energyBarFillImage").gameObject.activeSelf);
            Assert.IsFalse(GetField<Image>("rawBarFillImage").gameObject.activeSelf);
            Assert.IsFalse(GetField<Image>("supportBarFillImage").gameObject.activeSelf);
        }

        [Test]
        public void TryGetIconSourceRect_VisibleAndHiddenIcons_ReportsAvailability()
        {
            _view.Render(CreateData(), new Vector2Int(200, 150));

            bool foundFleet = _view.TryGetIconSourceRect(
                PlanetIcon.Fleet,
                out RectTransform fleetRect
            );
            bool foundDefense = _view.TryGetIconSourceRect(
                PlanetIcon.Defense,
                out RectTransform defenseRect
            );

            Assert.IsTrue(foundFleet);
            Assert.AreSame(GetField<RawImage>("fleetImage").rectTransform, fleetRect);
            Assert.IsFalse(foundDefense);
            Assert.IsNull(defenseRect);
        }

        [Test]
        public void TryGetFleetDragImage_RenderedFleet_PrefersPressedTexture()
        {
            _view.Render(CreateData(), new Vector2Int(200, 150));

            bool found = _view.TryGetFleetDragImage(out Texture texture, out RectTransform rect);

            Assert.IsTrue(found);
            Assert.AreSame(_pressedTexture, texture);
            Assert.AreSame(GetField<RawImage>("fleetImage").rectTransform, rect);
        }

        [Test]
        public void TryCreateElement_ExplicitVisibleIcon_ReturnsSemanticElement()
        {
            _view.Render(CreateData(), new Vector2Int(200, 150));
            PointerEventData eventData = CreatePointerEvent(
                GetField<RawImage>("facilityImage").gameObject,
                PointerEventData.InputButton.Left,
                1
            );

            bool found = _view.TryCreateElement(
                GetField<RawImage>("facilityImage").gameObject,
                eventData,
                out PlanetSystemWindowElement element
            );

            Assert.IsTrue(found);
            Assert.AreEqual(7, element.PlanetIndex);
            Assert.AreEqual(PlanetIcon.Facility, element.Icon);
            Assert.IsFalse(element.PlanetImage);
        }

        [Test]
        public void TryCreateElement_ExplicitPlanetImage_ReturnsPlanetElement()
        {
            _view.Render(CreateData(), new Vector2Int(200, 150));
            RawImage planetImage = GetField<RawImage>("planetImage");
            PointerEventData eventData = CreatePointerEvent(
                planetImage.gameObject,
                PointerEventData.InputButton.Left,
                1
            );

            bool found = _view.TryCreateElement(
                planetImage.gameObject,
                eventData,
                out PlanetSystemWindowElement element
            );

            Assert.IsTrue(found);
            Assert.AreEqual(PlanetIcon.None, element.Icon);
            Assert.IsTrue(element.PlanetImage);
        }

        [Test]
        public void PointerHandlers_VisibleIcon_RaiseSemanticInteractionEvents()
        {
            _view.Render(CreateData(), new Vector2Int(200, 150));
            RawImage fleetImage = GetField<RawImage>("fleetImage");
            PointerEventData eventData = CreatePointerEvent(
                fleetImage.gameObject,
                PointerEventData.InputButton.Left,
                2
            );
            int hoveredCount = 0;
            int hoverClearedCount = 0;
            int pressedCount = 0;
            int clickedCount = 0;
            int releasedCount = 0;
            PlanetSystemWindowElement lastElement = null;
            _view.Hovered += (_, element, _) =>
            {
                hoveredCount++;
                lastElement = element;
            };
            _view.HoverCleared += _ => hoverClearedCount++;
            _view.Pressed += (_, element, _) =>
            {
                pressedCount++;
                lastElement = element;
            };
            _view.Clicked += (_, element, _) =>
            {
                clickedCount++;
                lastElement = element;
            };
            _view.Released += (_, element, _) =>
            {
                releasedCount++;
                lastElement = element;
            };

            _view.OnPointerEnter(eventData);
            _view.OnPointerMove(eventData);
            _view.OnPointerDown(eventData);
            _view.OnPointerClick(eventData);
            _view.OnDrop(eventData);
            _view.OnPointerExit(eventData);

            Assert.AreEqual(2, hoveredCount);
            Assert.AreEqual(1, hoverClearedCount);
            Assert.AreEqual(1, pressedCount);
            Assert.AreEqual(1, clickedCount);
            Assert.AreEqual(2, releasedCount);
            Assert.AreEqual(PlanetIcon.Fleet, lastElement.Icon);
            Assert.AreEqual(7, lastElement.PlanetIndex);
        }

        [Test]
        public void PointerHandlers_UnsupportedButton_DoNotRaiseInteractionEvents()
        {
            _view.Render(CreateData(), new Vector2Int(200, 150));
            PointerEventData eventData = CreatePointerEvent(
                GetField<RawImage>("fleetImage").gameObject,
                PointerEventData.InputButton.Middle,
                2
            );
            int interactionCount = 0;
            _view.Pressed += (_, _, _) => interactionCount++;
            _view.Clicked += (_, _, _) => interactionCount++;
            _view.Released += (_, _, _) => interactionCount++;

            _view.OnPointerDown(eventData);
            _view.OnPointerClick(eventData);

            Assert.AreEqual(0, interactionCount);
        }

        private PlanetSystemPlanetRenderData CreateData(
            PlanetIcon selectedIcon = PlanetIcon.None,
            PlanetIcon hoveredIcon = PlanetIcon.None,
            PlanetSystemBarRenderData energyBar = null,
            PlanetSystemBarRenderData rawBar = null,
            PlanetSystemBarRenderData supportBar = null
        )
        {
            return new PlanetSystemPlanetRenderData(
                7,
                new Vector2Int(10, 20),
                _planetTexture,
                _uprisingTexture,
                _normalTexture,
                _pressedTexture,
                null,
                null,
                _normalTexture,
                _pressedTexture,
                _normalTexture,
                _pressedTexture,
                _headquartersTexture,
                "Coruscant",
                new Color32(10, 20, 30, 255),
                selectedIcon,
                hoveredIcon,
                energyBar ?? CreateSegmentedBar(true, 4, 2),
                rawBar ?? CreateSegmentedBar(true, 4, 2),
                supportBar ?? CreateContinuousBar(true, 0.5f)
            );
        }

        private static PlanetSystemBarRenderData CreateSegmentedBar(
            bool visible,
            int cellCount,
            int litCells
        )
        {
            return new PlanetSystemBarRenderData(
                visible,
                cellCount,
                litCells,
                0f,
                new Color32(0, 255, 0, 255),
                new Color32(255, 0, 0, 255),
                new Color32(0, 0, 0, 255)
            );
        }

        private static PlanetSystemBarRenderData CreateContinuousBar(bool visible, float ratio)
        {
            return new PlanetSystemBarRenderData(
                visible,
                0,
                0,
                ratio,
                new Color32(0, 255, 0, 255),
                default,
                new Color32(0, 0, 0, 255)
            );
        }

        private static PointerEventData CreatePointerEvent(
            GameObject target,
            PointerEventData.InputButton button,
            int clickCount
        )
        {
            return new PointerEventData(null)
            {
                button = button,
                clickCount = clickCount,
                position = new Vector2(-10000f, -10000f),
                pointerCurrentRaycast = new RaycastResult { gameObject = target },
                pointerPressRaycast = new RaycastResult { gameObject = target },
            };
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(PlanetSystemPlanetView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        private static RectInt GetSourceRect(Transform transform)
        {
            return UILayout.GetSourceRect(transform as RectTransform);
        }
    }
}
