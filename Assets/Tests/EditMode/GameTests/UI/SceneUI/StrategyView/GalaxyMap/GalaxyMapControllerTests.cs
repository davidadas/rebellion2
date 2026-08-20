using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using UnityEngine;
using UnityEngine.EventSystems;
using GamePlanetSector = Rebellion.Game.Galaxy.PlanetSector;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalaxyMapControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private GalaxyMapController _controller;
        private GalaxyMapSector _sector;
        private GameObject _rootObject;
        private GalaxyMapView _view;

        [SetUp]
        public void SetUp()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Player" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            UIContext uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _view = _rootObject.GetComponentInChildren<GalaxyMapView>(true);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
            _actions = new TestActions();
            _controller = new GalaxyMapController(() => uiContext);
            _controller.Initialize(_actions);
            _controller.BindView(_view);
            _sector = CreateSector();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new GalaxyMapController(null));
        }

        [Test]
        public void BindView_BeforeInitialization_ThrowsInvalidOperationException()
        {
            GalaxyMapController controller = new GalaxyMapController(() => null);

            Assert.Throws<InvalidOperationException>(() => controller.BindView(_view));
        }

        [Test]
        public void Render_BeforeViewBinding_ThrowsInvalidOperationException()
        {
            GalaxyMapController controller = new GalaxyMapController(() => null);
            controller.Initialize(_actions);

            Assert.Throws<InvalidOperationException>(() =>
                controller.Render(
                    Array.Empty<GalaxyMapSector>(),
                    _playerFactionId,
                    GalacticInformationFilterMode.DisplayOff
                )
            );
        }

        [Test]
        public void Render_VisibleSector_RoutesHoverAndOpenRequests()
        {
            _controller.Render(
                new[] { _sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff
            );
            PlanetSectorClusterView cluster = FindRenderedCluster();
            PointerEventData eventData = CreateMapPointerEvent(Vector2.zero);
            eventData.button = PointerEventData.InputButton.Left;
            eventData.clickCount = 2;

            cluster.OnPointerEnter(eventData);
            cluster.OnPointerEnter(eventData);
            cluster.OnPointerExit(eventData);
            cluster.OnPointerClick(eventData);

            Assert.AreEqual(2, _actions.RenderRequestCount);
            Assert.AreSame(_sector.PlanetSector, _actions.OpenedSector);
            Assert.AreEqual(426, _actions.OpenedX);
            Assert.AreEqual(240, _actions.OpenedY);
        }

        [Test]
        public void Render_EmptySnapshot_ClearsMissionTargetLookup()
        {
            _controller.Render(
                new[] { _sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff
            );
            PlanetSectorClusterView cluster = FindRenderedCluster();
            PointerEventData eventData = CreateClusterPointerEvent(cluster, new Vector2(10f, 14f));
            _controller.Render(null, _playerFactionId, GalacticInformationFilterMode.DisplayOff);

            bool found = _controller.TryGetMissionTarget(
                eventData,
                out StrategyMissionTarget target
            );

            Assert.IsFalse(found);
            Assert.IsNull(target);
        }

        [Test]
        public void TryGetMissionTarget_RenderedPlanetMarker_ReturnsDomainTarget()
        {
            _controller.Render(
                new[] { _sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff
            );
            PlanetSectorClusterView cluster = FindRenderedCluster();
            PointerEventData eventData = CreateClusterPointerEvent(cluster, new Vector2(10f, 14f));

            bool found = _controller.TryGetMissionTarget(
                eventData,
                out StrategyMissionTarget target
            );

            Assert.IsTrue(found);
            Assert.IsNotNull(target);
            Assert.AreSame(_sector.Planets[0], target.Planet);
            Assert.IsNull(target.Item);
        }

        [Test]
        public void FindPlanet_CurrentSnapshot_ReturnsProjectedPlanet()
        {
            _controller.Render(
                new[] { _sector },
                _playerFactionId,
                GalacticInformationFilterMode.DisplayOff
            );

            GalaxyMapPlanet planet = _controller.FindPlanet(_sector.Planets[0].Planet.InstanceID);

            Assert.AreSame(_sector.Planets[0], planet);
            Assert.IsNull(_controller.FindPlanet("missing"));
        }

        [Test]
        public void ClearHover_NoHoveredSector_ReturnsFalse()
        {
            Assert.IsFalse(_controller.ClearHover());
        }

        [Test]
        public void GetSectorSourcePosition_NullSector_ReturnsZero()
        {
            Assert.AreEqual(Vector2Int.zero, _controller.GetSectorSourcePosition(null));
        }

        private static GalaxyMapSector CreateSector()
        {
            GamePlanetSector planetSector = new GamePlanetSector
            {
                InstanceID = "sector",
                DisplayName = "Corellian",
                PositionX = 40,
                PositionY = 50,
            };
            Planet planet = new Planet
            {
                InstanceID = "planet",
                DisplayName = "Corellia",
                OwnerInstanceID = _playerFactionId,
                PositionX = 47,
                PositionY = 61,
            };
            return new GalaxyMapSector(
                planetSector,
                new[] { new GalaxyMapPlanet(planetSector, planet, string.Empty) }
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
            PlanetSectorClusterView cluster,
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

        private PlanetSectorClusterView FindRenderedCluster()
        {
            return _view
                .GetComponentsInChildren<PlanetSectorClusterView>(true)
                .Single(cluster => cluster.name == _sector.PlanetSector.InstanceID);
        }

        private sealed class TestActions : IGalaxyMapActions
        {
            public GamePlanetSector OpenedSector { get; private set; }
            public int OpenedX { get; private set; } = -1;
            public int OpenedY { get; private set; } = -1;
            public int RenderRequestCount { get; private set; }

            public void OpenPlanetSectorWindow(
                GamePlanetSector planetSector,
                int sourceX,
                int sourceY
            )
            {
                OpenedSector = planetSector;
                OpenedX = sourceX;
                OpenedY = sourceY;
            }

            public void RequestGalaxyMapRender()
            {
                RenderRequestCount++;
            }
        }
    }
}
