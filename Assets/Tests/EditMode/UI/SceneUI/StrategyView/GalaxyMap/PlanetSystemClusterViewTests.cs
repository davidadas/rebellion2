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
    public class PlanetSystemClusterViewTests
    {
        private const string _prefabPath =
            "Assets/Prefabs/UI/StrategyView/PlanetSystemCluster.prefab";

        private Texture2D _headquartersTexture;
        private Texture2D _largeStarTexture;
        private Texture2D _starTexture;
        private PlanetSystemClusterView _view;
        private GameObject _viewObject;

        [SetUp]
        public void SetUp()
        {
            _viewObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _viewObject.GetComponent<PlanetSystemClusterView>();
            _starTexture = new Texture2D(15, 15);
            _largeStarTexture = new Texture2D(21, 19);
            _headquartersTexture = new Texture2D(14, 14);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_headquartersTexture);
            UnityEngine.Object.DestroyImmediate(_largeStarTexture);
            UnityEngine.Object.DestroyImmediate(_starTexture);
            UnityEngine.Object.DestroyImmediate(_viewObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_CompleteCluster_AppliesBoundsLabelStarsAndHeadquarters()
        {
            GalaxyMapClusterRenderData data = CreateCluster(
                "system-1",
                "Corellia",
                true,
                new[]
                {
                    new GalaxyMapStarRenderData(
                        "planet-1",
                        4,
                        6,
                        _starTexture,
                        _headquartersTexture
                    ),
                    new GalaxyMapStarRenderData("planet-2", 24, 18, _largeStarTexture, null),
                }
            );

            _view.Render(data);

            Assert.AreEqual("system-1", _view.SystemInstanceId);
            Assert.AreEqual(new RectInt(100, 120, 50, 50), _view.GetRenderedSourceRect());
            RawImage hitArea = FindComponent<RawImage>("HitAreaImage");
            Assert.AreEqual(
                new RectInt(0, 0, 50, 50),
                UILayout.GetSourceRect(hitArea.rectTransform)
            );
            Assert.IsTrue(hitArea.enabled);
            Assert.IsTrue(hitArea.raycastTarget);
            Assert.IsFalse(hitArea.canvasRenderer.cullTransparentMesh);
            TextMeshProUGUI label = FindComponent<TextMeshProUGUI>("SystemNameTextField");
            Assert.AreEqual("Corellia", label.text);
            Assert.IsTrue(label.gameObject.activeSelf);
            RawImage[] stars = FindGeneratedImages("Star");
            RawImage[] headquarters = FindGeneratedImages("Headquarters");
            Vector2Int firstStarSize = UILayout.GetTextureSourceSize(_starTexture);
            Vector2Int secondStarSize = UILayout.GetTextureSourceSize(_largeStarTexture);
            Vector2Int headquartersSize = UILayout.GetTextureSourceSize(_headquartersTexture);
            Assert.AreEqual(2, stars.Length);
            Assert.AreSame(_starTexture, stars[0].texture);
            Assert.AreEqual(
                new RectInt(4, 6, firstStarSize.x, firstStarSize.y),
                _view.GetRenderedStarSourceRect(0)
            );
            Assert.AreSame(_largeStarTexture, stars[1].texture);
            Assert.AreEqual(
                new RectInt(24, 18, secondStarSize.x, secondStarSize.y),
                _view.GetRenderedStarSourceRect(1)
            );
            Assert.AreSame(_headquartersTexture, headquarters[0].texture);
            Assert.AreEqual(
                new RectInt(4, 6, headquartersSize.x, headquartersSize.y),
                UILayout.GetSourceRect(headquarters[0].rectTransform)
            );
            Assert.IsFalse(headquarters[1].gameObject.activeSelf);
            Assert.IsFalse(stars[0].raycastTarget);
        }

        [Test]
        public void Render_ShorterCluster_ReusesAndHidesPooledImages()
        {
            _view.Render(
                CreateCluster(
                    "system-1",
                    "Corellia",
                    true,
                    new[]
                    {
                        new GalaxyMapStarRenderData(
                            "planet-1",
                            4,
                            6,
                            _starTexture,
                            _headquartersTexture
                        ),
                        new GalaxyMapStarRenderData(
                            "planet-2",
                            24,
                            18,
                            _largeStarTexture,
                            _headquartersTexture
                        ),
                    }
                )
            );
            RawImage firstStar = FindGeneratedImages("Star")[0];
            RawImage secondStar = FindGeneratedImages("Star")[1];
            RawImage secondHeadquarters = FindGeneratedImages("Headquarters")[1];

            _view.Render(
                CreateCluster(
                    "system-1",
                    string.Empty,
                    true,
                    new[] { new GalaxyMapStarRenderData("planet-3", 8, 9, _largeStarTexture, null) }
                )
            );

            Assert.AreSame(firstStar, FindGeneratedImages("Star")[0]);
            Assert.AreSame(_largeStarTexture, firstStar.texture);
            Assert.IsFalse(secondStar.gameObject.activeSelf);
            Assert.IsFalse(secondHeadquarters.gameObject.activeSelf);
            Assert.IsFalse(
                FindComponent<TextMeshProUGUI>("SystemNameTextField").gameObject.activeSelf
            );
        }

        [Test]
        public void Render_NullStarsAndHiddenLabel_HidesCachedPresentation()
        {
            _view.Render(
                CreateCluster(
                    "system-1",
                    "Corellia",
                    true,
                    new[]
                    {
                        new GalaxyMapStarRenderData(
                            "planet-1",
                            4,
                            6,
                            _starTexture,
                            _headquartersTexture
                        ),
                    }
                )
            );
            RawImage star = FindGeneratedImages("Star").Single();
            RawImage headquarters = FindGeneratedImages("Headquarters").Single();

            _view.Render(CreateCluster("system-1", "Corellia", false, null));

            Assert.IsFalse(star.gameObject.activeSelf);
            Assert.IsFalse(headquarters.gameObject.activeSelf);
            Assert.IsFalse(
                FindComponent<TextMeshProUGUI>("SystemNameTextField").gameObject.activeSelf
            );
        }

        [Test]
        public void TryGetPlanetInstanceId_OverlappingMarkers_ReturnsTopmostRenderedPlanet()
        {
            _view.Render(
                CreateCluster(
                    "system-1",
                    "Corellia",
                    true,
                    new[]
                    {
                        new GalaxyMapStarRenderData("planet-1", 10, 10, _starTexture, null),
                        new GalaxyMapStarRenderData("planet-2", 10, 10, _largeStarTexture, null),
                    }
                )
            );
            PointerEventData eventData = CreatePointerEvent(new Vector2(12f, 12f));

            bool found = _view.TryGetPlanetInstanceId(eventData, out string planetInstanceId);

            Assert.IsTrue(found);
            Assert.AreEqual("planet-2", planetInstanceId);
        }

        [Test]
        public void TryGetPlanetInstanceId_InvalidInputs_ReturnsFalseAndNullIdentity()
        {
            _view.Render(
                CreateCluster(
                    "system-1",
                    "Corellia",
                    true,
                    new[] { new GalaxyMapStarRenderData(string.Empty, 10, 10, _starTexture, null) }
                )
            );
            PointerEventData outside = CreatePointerEvent(new Vector2(500f, 500f));

            bool nullFound = _view.TryGetPlanetInstanceId(null, out string nullIdentity);
            bool outsideFound = _view.TryGetPlanetInstanceId(outside, out string outsideIdentity);
            _viewObject.SetActive(false);
            bool inactiveFound = _view.TryGetPlanetInstanceId(
                CreatePointerEvent(new Vector2(15f, 15f)),
                out string inactiveIdentity
            );

            Assert.IsFalse(nullFound);
            Assert.IsNull(nullIdentity);
            Assert.IsFalse(outsideFound);
            Assert.IsNull(outsideIdentity);
            Assert.IsFalse(inactiveFound);
            Assert.IsNull(inactiveIdentity);
        }

        [Test]
        public void GetRenderedStarSourceRect_InvalidIndex_ReturnsDefaultBounds()
        {
            _view.Render(CreateCluster("system-1", "Corellia", true, null));

            RectInt negativeBounds = _view.GetRenderedStarSourceRect(-1);
            RectInt missingBounds = _view.GetRenderedStarSourceRect(0);

            Assert.AreEqual(default(RectInt), negativeBounds);
            Assert.AreEqual(default(RectInt), missingBounds);
        }

        [Test]
        public void PointerEvents_RenderedCluster_EmitHoverExitAndDoubleClickRequests()
        {
            _view.Render(CreateCluster("system-1", "Corellia", true, null));
            PlanetSystemClusterView hoveredView = null;
            PlanetSystemClusterView exitedView = null;
            PlanetSystemClusterView openedView = null;
            PointerEventData openedEvent = null;
            _view.Hovered += view => hoveredView = view;
            _view.HoverCleared += view => exitedView = view;
            _view.OpenRequested += (view, eventData) =>
            {
                openedView = view;
                openedEvent = eventData;
            };
            PointerEventData eventData = CreatePointerEvent(new Vector2(25f, 25f));
            eventData.button = PointerEventData.InputButton.Left;
            eventData.clickCount = 2;

            _view.OnPointerEnter(eventData);
            _view.OnPointerExit(eventData);
            _view.OnPointerClick(eventData);

            Assert.AreSame(_view, hoveredView);
            Assert.AreSame(_view, exitedView);
            Assert.AreSame(_view, openedView);
            Assert.AreSame(eventData, openedEvent);
        }

        [Test]
        public void PointerEvents_UnrenderedOrSingleClick_DoNotEmitOpenOrHoverRequests()
        {
            int hoverCount = 0;
            int openCount = 0;
            _view.Hovered += _ => hoverCount++;
            _view.OpenRequested += (_, _) => openCount++;
            PointerEventData eventData = CreatePointerEvent(new Vector2(25f, 25f));
            eventData.button = PointerEventData.InputButton.Right;
            eventData.clickCount = 2;

            _view.OnPointerEnter(eventData);
            _view.OnPointerClick(eventData);
            _view.Render(CreateCluster("system-1", "Corellia", true, null));
            eventData.button = PointerEventData.InputButton.Left;
            eventData.clickCount = 1;
            _view.OnPointerClick(eventData);

            Assert.AreEqual(0, hoverCount);
            Assert.AreEqual(0, openCount);
        }

        private GalaxyMapClusterRenderData CreateCluster(
            string systemInstanceId,
            string label,
            bool showLabel,
            GalaxyMapStarRenderData[] stars
        )
        {
            return new GalaxyMapClusterRenderData(
                systemInstanceId,
                100,
                120,
                label,
                showLabel,
                stars
            );
        }

        private PointerEventData CreatePointerEvent(Vector2 sourcePosition)
        {
            RectTransform rect = _view.transform as RectTransform;
            Vector3 localPoint = new Vector3(
                rect.rect.xMin + sourcePosition.x,
                rect.rect.yMax - sourcePosition.y,
                0f
            );
            return new PointerEventData(null)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(localPoint)
                ),
            };
        }

        private RawImage[] FindGeneratedImages(string prefix)
        {
            return _viewObject
                .GetComponentsInChildren<RawImage>(true)
                .Where(image => image.name.StartsWith(prefix, StringComparison.Ordinal))
                .Where(image => !image.name.EndsWith("Template", StringComparison.Ordinal))
                .OrderBy(image => image.name)
                .ToArray();
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _viewObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }
    }
}
