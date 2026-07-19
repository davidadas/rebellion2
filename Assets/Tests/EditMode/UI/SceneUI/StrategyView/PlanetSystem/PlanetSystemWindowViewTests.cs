using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.PlanetSystem
{
    [TestFixture]
    public class PlanetSystemWindowViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/PlanetSystemWindow.prefab";

        private Texture2D _fleetTexture;
        private Texture2D _planetTexture;
        private Texture2D _pressedTexture;
        private GameObject _rootObject;
        private PlanetSystemWindowView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponent<PlanetSystemWindowView>();
            _planetTexture = new Texture2D(100, 80);
            _fleetTexture = new Texture2D(24, 24);
            _pressedTexture = new Texture2D(24, 24);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_pressedTexture);
            UnityEngine.Object.DestroyImmediate(_fleetTexture);
            UnityEngine.Object.DestroyImmediate(_planetTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_MultiplePlanets_AppliesTitleProjectionAndStableViewNames()
        {
            PlanetSystemPlanetRenderData first = CreatePlanet(0, new Vector2Int(0, 0), "Coruscant");
            PlanetSystemPlanetRenderData second = CreatePlanet(
                1,
                new Vector2Int(100, 80),
                "Corellia"
            );

            _view.Render(new PlanetSystemWindowRenderData("Sesswenna", new[] { first, second }));

            Assert.AreEqual("Sesswenna", GetField<TextMeshProUGUI>("systemNameTextField").text);
            List<PlanetSystemPlanetView> planets = GetPlanetViews();
            Assert.AreEqual(2, planets.Count);
            Assert.AreEqual("Planet0", planets[0].name);
            Assert.AreEqual("Planet1", planets[1].name);
            Assert.IsTrue(planets[0].gameObject.activeSelf);
            Assert.IsTrue(planets[1].gameObject.activeSelf);
            Assert.AreEqual(
                ProjectPlanetPosition(first.GalaxyOffset),
                GetPlanetImagePosition(planets[0])
            );
            Assert.AreEqual(
                ProjectPlanetPosition(second.GalaxyOffset),
                GetPlanetImagePosition(planets[1])
            );
            Assert.IsTrue(_view.gameObject.activeSelf);
        }

        [Test]
        public void Render_ShorterSnapshot_ReusesAndHidesSurplusPlanetViews()
        {
            _view.Render(
                new PlanetSystemWindowRenderData(
                    "Sesswenna",
                    new[]
                    {
                        CreatePlanet(0, Vector2Int.zero, "Coruscant"),
                        CreatePlanet(1, new Vector2Int(100, 80), "Corellia"),
                    }
                )
            );
            List<PlanetSystemPlanetView> original = new List<PlanetSystemPlanetView>(
                GetPlanetViews()
            );

            _view.Render(
                new PlanetSystemWindowRenderData(
                    "Sesswenna",
                    new[] { CreatePlanet(0, Vector2Int.zero, "Coruscant") }
                )
            );

            List<PlanetSystemPlanetView> planets = GetPlanetViews();
            Assert.AreEqual(2, planets.Count);
            Assert.AreSame(original[0], planets[0]);
            Assert.AreSame(original[1], planets[1]);
            Assert.IsTrue(planets[0].gameObject.activeSelf);
            Assert.IsFalse(planets[1].gameObject.activeSelf);
        }

        [Test]
        public void TryCreateElement_RenderedFleetRaycast_ReturnsSemanticElement()
        {
            _view.Render(
                new PlanetSystemWindowRenderData(
                    "Sesswenna",
                    new[] { CreatePlanet(0, Vector2Int.zero, "Coruscant") }
                )
            );
            PlanetSystemPlanetView planet = GetPlanetViews()[0];
            RawImage fleetImage = GetPlanetField<RawImage>(planet, "fleetImage");
            PointerEventData eventData = CreatePointerEvent(
                fleetImage.gameObject,
                PointerEventData.InputButton.Left,
                1
            );

            bool found = _view.TryCreateElement(eventData, out PlanetSystemWindowElement element);

            Assert.IsTrue(found);
            Assert.AreEqual(0, element.PlanetIndex);
            Assert.AreEqual(PlanetIcon.Fleet, element.Icon);
            Assert.IsFalse(element.PlanetImage);
        }

        [Test]
        public void TryGetFleetDragPreview_RenderedFleet_ReturnsPressedTextureAndIconGeometry()
        {
            _view.Render(
                new PlanetSystemWindowRenderData(
                    "Sesswenna",
                    new[] { CreatePlanet(0, Vector2Int.zero, "Coruscant") }
                )
            );
            PlanetSystemPlanetView planet = GetPlanetViews()[0];
            RectInt iconBounds = GetSourceRect(
                GetPlanetField<RawImage>(planet, "fleetImage").transform
            );

            bool found = _view.TryGetFleetDragPreview(
                new PlanetSystemWindowElement(0, PlanetIcon.Fleet, false),
                100,
                50,
                120,
                80,
                out DragPreview preview
            );

            Assert.IsTrue(found);
            Assert.AreSame(_pressedTexture, preview.Texture);
            Assert.AreEqual(iconBounds.width, preview.Width);
            Assert.AreEqual(iconBounds.height, preview.Height);
        }

        [Test]
        public void TryGetFleetDragPreview_NonFleetOrMissingPlanet_ReturnsFalse()
        {
            _view.Render(
                new PlanetSystemWindowRenderData(
                    "Sesswenna",
                    new[] { CreatePlanet(0, Vector2Int.zero, "Coruscant") }
                )
            );

            bool planetFound = _view.TryGetFleetDragPreview(
                new PlanetSystemWindowElement(0, PlanetIcon.None, true),
                0,
                0,
                0,
                0,
                out DragPreview planetPreview
            );
            bool missingFound = _view.TryGetFleetDragPreview(
                new PlanetSystemWindowElement(10, PlanetIcon.Fleet, false),
                0,
                0,
                0,
                0,
                out DragPreview missingPreview
            );

            Assert.IsFalse(planetFound);
            Assert.IsNull(planetPreview);
            Assert.IsFalse(missingFound);
            Assert.IsNull(missingPreview);
        }

        [Test]
        public void PlanetInteraction_RenderedChild_ForwardsAllSemanticEvents()
        {
            _view.Render(
                new PlanetSystemWindowRenderData(
                    "Sesswenna",
                    new[] { CreatePlanet(0, Vector2Int.zero, "Coruscant") }
                )
            );
            PlanetSystemPlanetView planet = GetPlanetViews()[0];
            RawImage fleetImage = GetPlanetField<RawImage>(planet, "fleetImage");
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
            _view.Hovered += (_, _, _) => hoveredCount++;
            _view.HoverCleared += _ => hoverClearedCount++;
            _view.Pressed += (_, _, _) => pressedCount++;
            _view.Clicked += (_, _, _) => clickedCount++;
            _view.Released += (_, _, _) => releasedCount++;

            planet.OnPointerEnter(eventData);
            planet.OnPointerDown(eventData);
            planet.OnPointerClick(eventData);
            planet.OnPointerExit(eventData);

            Assert.AreEqual(1, hoveredCount);
            Assert.AreEqual(1, hoverClearedCount);
            Assert.AreEqual(1, pressedCount);
            Assert.AreEqual(1, clickedCount);
            Assert.AreEqual(1, releasedCount);
        }

        [Test]
        public void OnDestroy_RenderedChildren_UnbindsEventsAndRaisesDestroyedEvent()
        {
            _view.Render(
                new PlanetSystemWindowRenderData(
                    "Sesswenna",
                    new[] { CreatePlanet(0, Vector2Int.zero, "Coruscant") }
                )
            );
            PlanetSystemPlanetView planet = GetPlanetViews()[0];
            RawImage fleetImage = GetPlanetField<RawImage>(planet, "fleetImage");
            PointerEventData eventData = CreatePointerEvent(
                fleetImage.gameObject,
                PointerEventData.InputButton.Left,
                2
            );
            PlanetSystemWindowView destroyedView = null;
            int clickedCount = 0;
            _view.Destroyed += view => destroyedView = view;
            _view.Clicked += (_, _, _) => clickedCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            planet.OnPointerClick(eventData);

            Assert.AreSame(_view, destroyedView);
            Assert.AreEqual(0, clickedCount);
        }

        private PlanetSystemPlanetRenderData CreatePlanet(int index, Vector2Int offset, string name)
        {
            PlanetSystemBarRenderData segmented = new PlanetSystemBarRenderData(
                true,
                4,
                2,
                0f,
                Color.green,
                Color.red,
                Color.black
            );
            PlanetSystemBarRenderData continuous = new PlanetSystemBarRenderData(
                true,
                0,
                0,
                0.5f,
                Color.green,
                Color.clear,
                Color.black
            );
            return new PlanetSystemPlanetRenderData(
                index,
                offset,
                _planetTexture,
                null,
                null,
                null,
                null,
                _fleetTexture,
                _pressedTexture,
                null,
                null,
                null,
                name,
                Color.yellow,
                PlanetIcon.None,
                PlanetIcon.None,
                segmented,
                segmented,
                continuous
            );
        }

        private Vector2Int ProjectPlanetPosition(Vector2Int offset)
        {
            RectInt windowBounds = GetSourceRect(_view.transform);
            float sourceRange = GetField<float>("galaxyProjectionSourceRange");
            float projectionWidth = GetField<float>("galaxyProjectionWidth");
            float projectionHeight = GetField<float>("galaxyProjectionHeight");
            float coordinateRange = GetField<float>("sectorCoordinateRange");
            float scaleX = GetField<float>("sectorCoordinateScaleX");
            float scaleY = GetField<float>("sectorCoordinateScaleY");
            int offsetY = GetField<int>("planetPositionOffsetY");
            float localX =
                projectionWidth == 0f ? offset.x : offset.x * sourceRange / projectionWidth;
            float localY =
                projectionHeight == 0f ? offset.y : offset.y * sourceRange / projectionHeight;
            return new Vector2Int(
                Mathf.FloorToInt(localX / coordinateRange * scaleX * windowBounds.width),
                Mathf.FloorToInt(localY / coordinateRange * scaleY * windowBounds.height) + offsetY
            );
        }

        private static Vector2Int GetPlanetImagePosition(PlanetSystemPlanetView planet)
        {
            RectInt bounds = planet.GetRenderedPlanetImageSourceRect();
            return new Vector2Int(bounds.x, bounds.y);
        }

        private List<PlanetSystemPlanetView> GetPlanetViews()
        {
            return GetField<List<PlanetSystemPlanetView>>("planetViews");
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(PlanetSystemWindowView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        private static T GetPlanetField<T>(PlanetSystemPlanetView view, string fieldName)
        {
            return (T)
                typeof(PlanetSystemPlanetView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(view);
        }

        private static RectInt GetSourceRect(Transform transform)
        {
            return UILayout.GetSourceRect(transform as RectTransform);
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
    }
}
