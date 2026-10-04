using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSector
{
    [TestFixture]
    public class PlanetSectorPlanetViewTests
    {
        private const int _galacticInformationMarkerBottomOverhang = 4;
        private const int _galacticInformationMarkerSourceSize = 15;
        private const int _galacticInformationTexturePixelSize = 68;
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/PlanetSectorPlanet.prefab";

        private Texture2D _galacticInformationTexture;
        private Texture2D _headquartersTexture;
        private Texture2D _normalTexture;
        private Texture2D _planetTexture;
        private Texture2D _pressedTexture;
        private Texture2D _uprisingTexture;
        private GameObject _rootObject;
        private PlanetSectorPlanetView _view;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponent<PlanetSectorPlanetView>();
            _planetTexture = new Texture2D(100, 80);
            _normalTexture = new Texture2D(24, 24);
            _pressedTexture = new Texture2D(24, 24);
            _galacticInformationTexture = new Texture2D(
                _galacticInformationTexturePixelSize,
                _galacticInformationTexturePixelSize
            );
            _headquartersTexture = new Texture2D(16, 16);
            _uprisingTexture = new Texture2D(167, 167);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_galacticInformationTexture);
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
            PlanetSectorPlanetRenderData data = CreateData(
                PlanetIcon.Facility,
                PlanetIcon.Mission,
                CreateSegmentedBar(true, 4, 2),
                CreateSegmentedBar(false, 0, 0),
                CreateContinuousBar(true, 0.5f),
                _galacticInformationTexture
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
            RawImage galacticInformationImage = _view
                .transform.Find("GalacticInformationImage")
                .GetComponent<RawImage>();
            RectInt galacticInformationBounds = GetSourceRect(galacticInformationImage.transform);
            Assert.AreSame(_galacticInformationTexture, galacticInformationImage.texture);
            Assert.AreEqual(
                new Vector2Int(
                    _galacticInformationMarkerSourceSize,
                    _galacticInformationMarkerSourceSize
                ),
                galacticInformationBounds.size
            );
            Assert.AreEqual(planetTemplate.x, galacticInformationBounds.x);
            Assert.AreEqual(
                planetTemplate.y
                    + planetTemplate.height
                    + _galacticInformationMarkerBottomOverhang
                    - _galacticInformationMarkerSourceSize,
                galacticInformationBounds.y
            );
            Assert.IsFalse(galacticInformationImage.raycastTarget);
            Assert.Greater(
                galacticInformationImage.transform.GetSiblingIndex(),
                GetField<RawImage>("uprisingImage").transform.GetSiblingIndex()
            );
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
            Image supportBackground = GetField<Image>("supportBarBackgroundImage");
            Assert.IsTrue(GetField<RectTransform>("supportBarRoot").gameObject.activeSelf);
            Assert.IsTrue(supportFill.gameObject.activeSelf);
            Assert.AreEqual((Color)new Color32(0, 0, 0, 255), supportBackground.color);
            Assert.AreEqual((Color)new Color32(0, 255, 0, 255), supportFill.color);
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
            PlanetSectorPlanetRenderData data = CreateData(
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
        public void Render_SelectedIcon_UsesTightVisiblePixelHitBounds()
        {
            RawImage facilityImage = GetField<RawImage>("facilityImage");
            RectInt authoredBounds = GetSourceRect(facilityImage.transform);
            RectInt contentBounds = new RectInt(6, 4, 12, 16);
            PlanetSectorPlanetRenderData data = CreateData(
                selectedIcon: PlanetIcon.Facility,
                getTextureContentBounds: texture =>
                    texture == _pressedTexture
                        ? contentBounds
                        : new RectInt(0, 0, texture.width, texture.height)
            );

            _view.Render(data, new Vector2Int(200, 150));

            bool found = _view.TryGetIconHitBounds(PlanetIcon.Facility, out RectInt hitBounds);

            Assert.IsTrue(found);
            Assert.AreEqual(
                new RectInt(authoredBounds.x + 6, authoredBounds.y + 3, 15, 12),
                hitBounds
            );
            Assert.AreEqual(authoredBounds, GetSourceRect(facilityImage.transform));
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), facilityImage.uvRect);
            MethodInfo getSourceIcon = typeof(PlanetSectorPlanetView).GetMethod(
                "GetSourceIcon",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.AreEqual(
                PlanetIcon.None,
                getSourceIcon.Invoke(_view, new object[] { authoredBounds.x, authoredBounds.y })
            );
            Assert.AreEqual(
                PlanetIcon.Facility,
                getSourceIcon.Invoke(_view, new object[] { hitBounds.x, hitBounds.y })
            );
        }

        [Test]
        public void Render_MissingGalacticInformationTexture_HidesMarker()
        {
            _view.Render(CreateData(), new Vector2Int(200, 150));

            Assert.IsFalse(_view.transform.Find("GalacticInformationImage").gameObject.activeSelf);
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
                out PlanetSectorWindowElement element
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
                out PlanetSectorWindowElement element
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
            PlanetSectorWindowElement lastElement = null;
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

        [Test]
        public void PointerHandlers_StatusBar_RaisesHoverWithoutInteractionEvents()
        {
            _view.Render(
                CreateData(energyBar: CreateSegmentedBar(true, 4, 2, "Energy Consumption 2/4")),
                new Vector2Int(200, 150)
            );
            Image energyBackground = GetField<Image>("energyBarBackgroundImage");
            PointerEventData eventData = CreatePointerEvent(
                energyBackground.gameObject,
                PointerEventData.InputButton.Left,
                1
            );
            int statusHoverCount = 0;
            int interactionCount = 0;
            PlanetSectorStatusBar hoveredBar = PlanetSectorStatusBar.None;
            _view.StatusBarHovered += (_, statusBar) =>
            {
                statusHoverCount++;
                hoveredBar = statusBar;
            };
            _view.Pressed += (_, _, _) => interactionCount++;
            _view.Clicked += (_, _, _) => interactionCount++;
            _view.Released += (_, _, _) => interactionCount++;

            _view.OnPointerEnter(eventData);
            _view.OnPointerDown(eventData);
            _view.OnPointerClick(eventData);
            _view.OnDrop(eventData);

            Assert.AreEqual(1, statusHoverCount);
            Assert.AreEqual(PlanetSectorStatusBar.Energy, hoveredBar);
            Assert.AreEqual(0, interactionCount);
        }

        [Test]
        public void Render_StatusBarTooltips_CreatesOneHitAreaAcrossCompleteBarBlock()
        {
            _view.gameObject.SetActive(false);
            _view.Render(
                CreateData(
                    energyBar: CreateSegmentedBar(true, 4, 2, "Energy Consumption 2/4"),
                    rawBar: CreateSegmentedBar(true, 8, 3, "Raw Materials 3/8"),
                    supportBar: CreateContinuousBar(true, 0.5f, "Popular Support")
                ),
                new Vector2Int(200, 150)
            );
            RawImage hitArea = GetField<RawImage>("hitAreaImage");
            RectInt energyBounds = GetSourceRect(GetField<RectTransform>("energyBarRoot"));
            RectInt supportBounds = GetSourceRect(GetField<RectTransform>("supportBarRoot"));
            RectInt expectedBounds = new RectInt(
                Mathf.Min(energyBounds.xMin, supportBounds.xMin),
                energyBounds.yMin,
                Mathf.Max(energyBounds.xMax, supportBounds.xMax)
                    - Mathf.Min(energyBounds.xMin, supportBounds.xMin),
                supportBounds.yMax - energyBounds.yMin
            );

            Assert.IsTrue(hitArea.enabled);
            Assert.IsTrue(hitArea.raycastTarget);
            Assert.AreEqual(expectedBounds, GetSourceRect(hitArea.transform));

            MethodInfo getStatusBar = typeof(PlanetSectorPlanetView).GetMethod(
                "GetSourceStatusBar",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            int rightEdge = expectedBounds.xMax - 1;
            Assert.AreEqual(
                PlanetSectorStatusBar.Energy,
                getStatusBar.Invoke(_view, new object[] { rightEdge, energyBounds.yMax })
            );
            Assert.AreEqual(
                PlanetSectorStatusBar.RawMaterials,
                getStatusBar.Invoke(_view, new object[] { rightEdge, energyBounds.yMax + 1 })
            );
            Assert.AreEqual(
                PlanetSectorStatusBar.PopularSupport,
                getStatusBar.Invoke(_view, new object[] { rightEdge, supportBounds.yMax - 1 })
            );
        }

        /// <summary>
        /// Creates data.
        /// </summary>
        /// <param name="selectedIcon">The selected icon.</param>
        /// <param name="hoveredIcon">The hovered icon.</param>
        /// <param name="energyBar">The energy bar.</param>
        /// <param name="rawBar">The raw bar.</param>
        /// <param name="supportBar">The support bar.</param>
        /// <param name="galacticInformationTexture">The active filter marker texture.</param>
        /// <param name="getTextureContentBounds">The optional visible-pixel bounds resolver.</param>
        /// <returns>The created data.</returns>
        private PlanetSectorPlanetRenderData CreateData(
            PlanetIcon selectedIcon = PlanetIcon.None,
            PlanetIcon hoveredIcon = PlanetIcon.None,
            PlanetSectorBarRenderData energyBar = null,
            PlanetSectorBarRenderData rawBar = null,
            PlanetSectorBarRenderData supportBar = null,
            Texture2D galacticInformationTexture = null,
            Func<Texture2D, RectInt> getTextureContentBounds = null
        )
        {
            return new PlanetSectorPlanetRenderData(
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
                supportBar ?? CreateContinuousBar(true, 0.5f),
                galacticInformationTexture,
                getTextureContentBounds
            );
        }

        /// <summary>
        /// Creates segmented bar.
        /// </summary>
        /// <param name="visible">Whether visible.</param>
        /// <param name="cellCount">The cell count.</param>
        /// <param name="litCells">The lit cells.</param>
        /// <param name="tooltipText">The optional hover label.</param>
        /// <returns>The created segmented bar.</returns>
        private static PlanetSectorBarRenderData CreateSegmentedBar(
            bool visible,
            int cellCount,
            int litCells,
            string tooltipText = null
        )
        {
            return new PlanetSectorBarRenderData(
                visible,
                cellCount,
                litCells,
                0f,
                new Color32(0, 255, 0, 255),
                new Color32(255, 0, 0, 255),
                new Color32(0, 0, 0, 255),
                tooltipText
            );
        }

        /// <summary>
        /// Creates continuous bar.
        /// </summary>
        /// <param name="visible">Whether visible.</param>
        /// <param name="ratio">The ratio.</param>
        /// <param name="tooltipText">The optional hover label.</param>
        /// <returns>The created continuous bar.</returns>
        private static PlanetSectorBarRenderData CreateContinuousBar(
            bool visible,
            float ratio,
            string tooltipText = null
        )
        {
            return new PlanetSectorBarRenderData(
                visible,
                0,
                0,
                ratio,
                new Color32(0, 255, 0, 255),
                default,
                new Color32(0, 0, 0, 255),
                tooltipText
            );
        }

        /// <summary>
        /// Creates pointer event.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="button">The button.</param>
        /// <param name="clickCount">The click count.</param>
        /// <returns>The created pointer event.</returns>
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

        /// <summary>
        /// Gets field.
        /// </summary>
        /// <param name="fieldName">The field name.</param>
        /// <typeparam name="T">The t type.</typeparam>
        /// <returns>The requested field.</returns>
        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(PlanetSectorPlanetView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        /// <summary>
        /// Gets source rect.
        /// </summary>
        /// <param name="transform">The transform.</param>
        /// <returns>The requested source rect.</returns>
        private static RectInt GetSourceRect(Transform transform)
        {
            return UILayout.GetSourceRect(transform as RectTransform);
        }
    }
}
