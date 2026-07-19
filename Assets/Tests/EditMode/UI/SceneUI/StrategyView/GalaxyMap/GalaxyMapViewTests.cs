using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalaxyMapViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private Texture2D _backgroundTexture;
        private Texture2D _headquartersTexture;
        private GameObject _rootObject;
        private Texture2D _starTexture;
        private GalaxyMapView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<GalaxyMapView>(true);
            _backgroundTexture = new Texture2D(800, 400);
            _starTexture = new Texture2D(45, 45);
            _headquartersTexture = new Texture2D(36, 36);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_headquartersTexture);
            UnityEngine.Object.DestroyImmediate(_starTexture);
            UnityEngine.Object.DestroyImmediate(_backgroundTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_CompleteMap_AppliesBackgroundFilterLabelAndClusters()
        {
            GalaxyMapRenderData data = CreateMap(
                new[]
                {
                    CreateCluster("system-1", "Corellia", 100, 120, "planet-1"),
                    CreateCluster("system-2", "Kessel", 300, 220, "planet-2"),
                },
                "Idle Shipyards"
            );

            _view.Render(data);

            RawImage background = GetField<RawImage>("backgroundImage");
            Assert.AreSame(_backgroundTexture, background.texture);
            Assert.IsTrue(background.enabled);
            Assert.IsFalse(background.raycastTarget);
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), background.uvRect);
            Assert.AreEqual(
                new RectInt(49, 26, 777, 392),
                UILayout.GetSourceRect(_view.Background)
            );
            Assert.AreEqual(
                new RectInt(49, 26, 777, 392),
                UILayout.GetSourceRect(_view.PlanetSystemClusters)
            );
            TextMeshProUGUI filterLabel = GetField<TextMeshProUGUI>("activeFilterLabel");
            Assert.IsTrue(filterLabel.gameObject.activeSelf);
            Assert.AreEqual("Idle Shipyards", filterLabel.text);
            Assert.AreEqual(Color.yellow, filterLabel.color);
            Assert.AreEqual(15f, filterLabel.fontSize);
            Assert.AreEqual(
                new RectInt(200, 12, 300, 20),
                UILayout.GetSourceRect(filterLabel.rectTransform)
            );
            PlanetSystemClusterView[] clusters = FindClusters();
            Assert.AreEqual(2, clusters.Length);
            Assert.AreEqual("system-1", clusters[0].name);
            Assert.AreEqual("system-1", clusters[0].SystemInstanceId);
            Assert.AreEqual(new RectInt(100, 120, 50, 50), clusters[0].GetRenderedSourceRect());
            Assert.AreEqual("system-2", clusters[1].name);
        }

        [Test]
        public void Render_ChangedClusterSet_ReusesExistingAndHidesMissingClusters()
        {
            _view.Render(
                CreateMap(
                    new[]
                    {
                        CreateCluster("system-1", "Corellia", 100, 120, "planet-1"),
                        CreateCluster("system-2", "Kessel", 300, 220, "planet-2"),
                    },
                    string.Empty
                )
            );
            PlanetSystemClusterView first = FindCluster("system-1");
            PlanetSystemClusterView second = FindCluster("system-2");

            _view.Render(
                CreateMap(
                    new[]
                    {
                        CreateCluster("system-2", "Updated Kessel", 320, 230, "planet-2"),
                        CreateCluster("system-3", "Naboo", 500, 100, "planet-3"),
                    },
                    string.Empty
                )
            );

            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreSame(second, FindCluster("system-2"));
            Assert.IsTrue(second.gameObject.activeSelf);
            Assert.AreEqual(new RectInt(320, 230, 50, 50), second.GetRenderedSourceRect());
            Assert.IsTrue(FindCluster("system-3").gameObject.activeSelf);
        }

        [Test]
        public void Render_NullClustersAndEmptyFilter_HidesPooledClustersAndLabel()
        {
            _view.Render(
                CreateMap(
                    new[] { CreateCluster("system-1", "Corellia", 100, 120, "planet-1") },
                    "Idle Shipyards"
                )
            );
            PlanetSystemClusterView cluster = FindCluster("system-1");

            _view.Render(
                new GalaxyMapRenderData(
                    null,
                    null,
                    new GalaxyMapActiveFilterLabelRenderData(string.Empty, Color.white, default, 0),
                    null
                )
            );

            Assert.IsFalse(GetField<RawImage>("backgroundImage").enabled);
            Assert.IsFalse(GetField<TextMeshProUGUI>("activeFilterLabel").gameObject.activeSelf);
            Assert.IsFalse(cluster.gameObject.activeSelf);
        }

        [Test]
        public void TryGetPlanetInstanceId_PointerOverRenderedMarker_ReturnsPlanetIdentity()
        {
            _view.Render(
                CreateMap(
                    new[] { CreateCluster("system-1", "Corellia", 100, 120, "planet-1") },
                    string.Empty
                )
            );
            PlanetSystemClusterView cluster = FindCluster("system-1");
            PointerEventData eventData = CreateClusterPointerEvent(cluster, new Vector2(7f, 9f));

            bool found = _view.TryGetPlanetInstanceId(eventData, out string planetInstanceId);

            Assert.IsTrue(found);
            Assert.AreEqual("planet-1", planetInstanceId);
        }

        [Test]
        public void TryGetPlanetInstanceId_NullOrOutsidePointer_ReturnsFalse()
        {
            _view.Render(
                CreateMap(
                    new[] { CreateCluster("system-1", "Corellia", 100, 120, "planet-1") },
                    string.Empty
                )
            );
            PointerEventData outside = CreateMapPointerEvent(new Vector2(-1000f, -1000f));

            bool nullFound = _view.TryGetPlanetInstanceId(null, out string nullIdentity);
            bool outsideFound = _view.TryGetPlanetInstanceId(outside, out string outsideIdentity);

            Assert.IsFalse(nullFound);
            Assert.IsNull(nullIdentity);
            Assert.IsFalse(outsideFound);
            Assert.IsNull(outsideIdentity);
        }

        [Test]
        public void TryGetSourcePosition_InsideOutsideAndNullPointers_ReturnExpectedResults()
        {
            PointerEventData inside = CreateMapPointerEvent(Vector2.zero);
            PointerEventData outside = CreateMapPointerEvent(new Vector2(-10000f, -10000f));

            bool insideResult = _view.TryGetSourcePosition(
                inside,
                out int insideX,
                out int insideY
            );
            bool outsideResult = _view.TryGetSourcePosition(
                outside,
                out int outsideX,
                out int outsideY
            );
            bool nullResult = _view.TryGetSourcePosition(null, out int nullX, out int nullY);

            Assert.IsTrue(insideResult);
            Assert.AreEqual(426, insideX);
            Assert.AreEqual(240, insideY);
            Assert.IsFalse(outsideResult);
            Assert.Less(outsideX, 0);
            Assert.Greater(outsideY, 480);
            Assert.IsFalse(nullResult);
            Assert.AreEqual(0, nullX);
            Assert.AreEqual(0, nullY);
        }

        [Test]
        public void ClusterPointerEvents_RenderedCluster_ForwardSemanticMapRequests()
        {
            _view.Render(
                CreateMap(
                    new[] { CreateCluster("system-1", "Corellia", 100, 120, "planet-1") },
                    string.Empty
                )
            );
            string hoveredSystem = null;
            int hoverClearedCount = 0;
            string openedSystem = null;
            int openedX = -1;
            int openedY = -1;
            _view.SystemHovered += systemId => hoveredSystem = systemId;
            _view.SystemHoverCleared += () => hoverClearedCount++;
            _view.SystemOpenRequested += (systemId, x, y) =>
            {
                openedSystem = systemId;
                openedX = x;
                openedY = y;
            };
            PlanetSystemClusterView cluster = FindCluster("system-1");
            PointerEventData eventData = CreateMapPointerEvent(Vector2.zero);
            eventData.button = PointerEventData.InputButton.Left;
            eventData.clickCount = 2;

            cluster.OnPointerEnter(eventData);
            cluster.OnPointerExit(eventData);
            cluster.OnPointerClick(eventData);

            Assert.AreEqual("system-1", hoveredSystem);
            Assert.AreEqual(1, hoverClearedCount);
            Assert.AreEqual("system-1", openedSystem);
            Assert.AreEqual(426, openedX);
            Assert.AreEqual(240, openedY);
        }

        [Test]
        public void OnDestroy_RenderedClusters_UnbindsChildrenClearsStateAndRaisesDestroyedEvent()
        {
            _view.Render(
                CreateMap(
                    new[] { CreateCluster("system-1", "Corellia", 100, 120, "planet-1") },
                    string.Empty
                )
            );
            GalaxyMapView destroyedView = null;
            int hoverCount = 0;
            int clearCount = 0;
            int openCount = 0;
            _view.Destroyed += view => destroyedView = view;
            _view.SystemHovered += _ => hoverCount++;
            _view.SystemHoverCleared += () => clearCount++;
            _view.SystemOpenRequested += (_, _, _) => openCount++;
            PlanetSystemClusterView cluster = FindCluster("system-1");
            PointerEventData eventData = CreateMapPointerEvent(Vector2.zero);
            eventData.button = PointerEventData.InputButton.Left;
            eventData.clickCount = 2;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            cluster.OnPointerEnter(eventData);
            cluster.OnPointerExit(eventData);
            cluster.OnPointerClick(eventData);
            bool found = _view.TryGetPlanetInstanceId(eventData, out string planetInstanceId);

            Assert.AreSame(_view, destroyedView);
            Assert.AreEqual(0, hoverCount);
            Assert.AreEqual(0, clearCount);
            Assert.AreEqual(0, openCount);
            Assert.IsFalse(found);
            Assert.IsNull(planetInstanceId);
        }

        private GalaxyMapRenderData CreateMap(
            GalaxyMapClusterRenderData[] clusters,
            string activeFilter
        )
        {
            return new GalaxyMapRenderData(
                _backgroundTexture,
                new RectInt(49, 26, 777, 392),
                new GalaxyMapActiveFilterLabelRenderData(
                    activeFilter,
                    Color.yellow,
                    new RectInt(200, 12, 300, 20),
                    15
                ),
                clusters
            );
        }

        private GalaxyMapClusterRenderData CreateCluster(
            string systemInstanceId,
            string label,
            int sourceX,
            int sourceY,
            string planetInstanceId
        )
        {
            return new GalaxyMapClusterRenderData(
                systemInstanceId,
                sourceX,
                sourceY,
                label,
                true,
                new[]
                {
                    new GalaxyMapStarRenderData(
                        planetInstanceId,
                        5,
                        7,
                        _starTexture,
                        _headquartersTexture
                    ),
                }
            );
        }

        private PointerEventData CreateMapPointerEvent(Vector2 localPosition)
        {
            RectTransform rect = _view.transform as RectTransform;
            return new PointerEventData(null)
            {
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    rect.TransformPoint(localPosition)
                ),
            };
        }

        private static PointerEventData CreateClusterPointerEvent(
            PlanetSystemClusterView cluster,
            Vector2 sourcePosition
        )
        {
            RectTransform rect = cluster.transform as RectTransform;
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

        private PlanetSystemClusterView FindCluster(string systemInstanceId)
        {
            return FindClusters().Single(cluster => cluster.name == systemInstanceId);
        }

        private PlanetSystemClusterView[] FindClusters()
        {
            return _view
                .GetComponentsInChildren<PlanetSystemClusterView>(true)
                .OrderBy(cluster => cluster.name)
                .ToArray();
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(GalaxyMapView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }
    }
}
