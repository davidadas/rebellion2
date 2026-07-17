using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GameFleet = Rebellion.Game.Units.Fleet;
using GamePlanetSystem = Rebellion.Game.Galaxy.PlanetSystem;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public class ConfirmDialogWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private TestActions _actions;
        private ConfirmDialogWindowController _controller;
        private int _dirtyCount;
        private GameRoot _game;
        private GameObject _rootObject;
        private List<string> _playedSounds;
        private UIWindow _requestedCloseWindow;
        private UIWindow _sourceWindow;
        private CapitalShip _sourceShip;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            _requestedCloseWindow = null;
            _game = CreateGame();
            _uiContext = new UIContext(
                _game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _sourceWindow = CreateSourceWindow();
            _sourceShip = CreateSourceShip();
            _playedSounds = new List<string>();
            _actions = new TestActions();
            _controller = CreateController();
            _controller.Initialize(_actions);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullRequiredDependency_ThrowsArgumentNullException()
        {
            StrategyConfirmActionController actionController = CreateActionController();

            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogWindowController(
                    null,
                    () => _uiContext,
                    _ => { },
                    _windowLayer,
                    _windowManager,
                    () => Vector2Int.zero,
                    _ => { },
                    () => { }
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogWindowController(
                    actionController,
                    null,
                    _ => { },
                    _windowLayer,
                    _windowManager,
                    () => Vector2Int.zero,
                    _ => { },
                    () => { }
                )
            );
            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogWindowController(
                    actionController,
                    () => _uiContext,
                    _ => { },
                    _windowLayer,
                    _windowManager,
                    () => Vector2Int.zero,
                    _ => { },
                    null
                )
            );
        }

        [Test]
        public void Initialize_NullActions_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _controller.Initialize(null));
        }

        [Test]
        public void BindWindow_BeforeInitialize_ThrowsInvalidOperationException()
        {
            ConfirmDialogWindowController controller = CreateController();
            ConfirmDialogWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.ConfirmDialogWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindWindow(view));
        }

        [Test]
        public void OpenScrap_ValidSelection_CreatesModalSessionPlaysPromptAndMarksDirty()
        {
            _controller.OpenScrap(_sourceWindow, new ISceneNode[] { _sourceShip });

            UIWindow window = _windowManager.Windows.Single();
            Assert.AreEqual("ConfirmDialogWindow", window.Content.name);
            Assert.AreEqual(new Vector2Int(122, 81), new Vector2Int(window.X, window.Y));
            Assert.IsTrue(window.Modal);
            Assert.AreEqual(1, _dirtyCount);
            CollectionAssert.AreEqual(
                new[]
                {
                    _uiContext.GetPlayerFactionTheme().ConfirmDialogTheme.ScrapRetireSoundPath,
                },
                _playedSounds
            );
        }

        [Test]
        public void RenderWindows_OpenScrap_RendersPromptSelectionAndConfiguredArtwork()
        {
            _controller.OpenScrap(_sourceWindow, new ISceneNode[] { _sourceShip });
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);

            _controller.RenderWindows();

            string[] lines = view.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Where(text => text.gameObject.activeSelf)
                .Select(text => text.text)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "Scrap these units?", "Assault Frigate" }, lines);
            Assert.AreSame(
                _uiContext.GetTexture(
                    _uiContext.GetPlayerFactionTheme().ConfirmDialogTheme.BackgroundImagePath
                ),
                FindImage(view, "BackgroundImage").texture
            );
            Assert.IsTrue(view.gameObject.activeSelf);
        }

        [Test]
        public void CancelButton_OpenScrap_ClosesWithoutRefreshingSourceWindow()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out UIWindow window);

            FindButton(view, "CancelButtonImage").onClick.Invoke();

            Assert.AreSame(window, _requestedCloseWindow);
            Assert.IsNull(_actions.RefreshedWindow);
            Assert.IsNotNull(_sourceShip.GetParent());
        }

        [Test]
        public void ConfirmButton_OpenScrap_ScrapsUnitRefreshesSourceAndClosesDialog()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out UIWindow window);

            FindButton(view, "ConfirmButtonImage").onClick.Invoke();

            Assert.IsNull(_game.GetSceneNodeByInstanceID<CapitalShip>(_sourceShip.InstanceID));
            Assert.AreSame(_sourceWindow, _actions.RefreshedWindow);
            Assert.AreSame(window, _requestedCloseWindow);
        }

        [Test]
        public void TryInitializeStopConstruction_NonBuildingItem_ReturnsFalseWithoutAudio()
        {
            ConfirmDialogWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.ConfirmDialogWindowPrefab,
                _rootObject.transform
            );

            bool initialized = _controller.TryInitializeStopConstructionConfirmWindow(
                view,
                _sourceWindow,
                new ISceneNode[] { _sourceShip }
            );

            Assert.IsFalse(initialized);
            Assert.IsEmpty(_playedSounds);
        }

        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesSessionState()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out UIWindow window);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.Throws<InvalidOperationException>(() => _controller.RenderWindow(view, window));
        }

        private ConfirmDialogWindowController CreateController()
        {
            return new ConfirmDialogWindowController(
                CreateActionController(),
                () => _uiContext,
                path => _playedSounds?.Add(path),
                _windowLayer,
                _windowManager,
                () => new Vector2Int(122, 81),
                window => _requestedCloseWindow = window,
                () => _dirtyCount++
            );
        }

        private StrategyConfirmActionController CreateActionController()
        {
            return new StrategyConfirmActionController(new GameManager(_game), _ => { });
        }

        private GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _playerFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            return game;
        }

        private UIWindow CreateSourceWindow()
        {
            GameObject sourceObject = new GameObject(
                "SourceWindow",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            sourceObject.transform.SetParent(_rootObject.transform, false);
            UIWindow sourceWindow = sourceObject.GetComponent<UIWindow>();
            sourceWindow.Configure(500, 10, 20, 100, 80, false, true, false);
            return sourceWindow;
        }

        private CapitalShip CreateSourceShip()
        {
            GamePlanetSystem system = new GamePlanetSystem { InstanceID = "system" };
            _game.AttachNode(system, _game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            _game.AttachNode(planet, system);
            GameFleet fleet = new GameFleet
            {
                InstanceID = "fleet",
                OwnerInstanceID = _playerFactionId,
            };
            _game.AttachNode(fleet, planet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                DisplayName = "Assault Frigate",
                OwnerInstanceID = _playerFactionId,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            _game.AttachNode(ship, fleet);
            return ship;
        }

        private ConfirmDialogWindowView OpenScrapAndInitializeView(out UIWindow window)
        {
            _controller.OpenScrap(_sourceWindow, new ISceneNode[] { _sourceShip });
            window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            return view;
        }

        private static Button FindButton(ConfirmDialogWindowView view, string objectName)
        {
            return view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == objectName);
        }

        private static RawImage FindImage(ConfirmDialogWindowView view, string objectName)
        {
            return view.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == objectName);
        }

        private sealed class TestActions : IConfirmDialogWindowActions
        {
            public UIWindow RefreshedWindow { get; private set; }

            public void RefreshAfterConfirmedAction(UIWindow sourceWindow)
            {
                RefreshedWindow = sourceWindow;
            }
        }
    }
}
