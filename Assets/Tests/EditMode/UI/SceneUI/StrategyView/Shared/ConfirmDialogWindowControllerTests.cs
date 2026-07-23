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

        private ConfirmDialogWindowController _controller;
        private int _confirmedCount;
        private int _dirtyCount;
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
            _confirmedCount = 0;
            _dirtyCount = 0;
            _requestedCloseWindow = null;
            GameRoot game = CreateGame();
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _sourceWindow = CreateSourceWindow();
            _sourceShip = CreateSourceShip(game);
            _playedSounds = new List<string>();
            _controller = CreateController();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Constructor_NullRequiredDependency_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ConfirmDialogWindowController(
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
        public void BindWindow_NullView_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _controller.BindWindow(null));
        }

        [Test]
        public void OpenScrap_ValidSelection_CreatesModalSessionPlaysPromptAndMarksDirty()
        {
            _controller.OpenScrap(_sourceWindow, new ISceneNode[] { _sourceShip }, Confirm);

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
            _controller.OpenScrap(_sourceWindow, new ISceneNode[] { _sourceShip }, Confirm);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);

            _controller.RenderWindows();

            string[] lines = GetVisibleLines(view);
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
        public void CancelButton_OpenScrap_ClosesWithoutInvokingAction()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out UIWindow window);

            FindButton(view, "CancelButtonImage").onClick.Invoke();

            Assert.AreSame(window, _requestedCloseWindow);
            Assert.AreEqual(0, _confirmedCount);
        }

        [Test]
        public void ConfirmButton_OpenScrap_InvokesActionAndClosesDialog()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out UIWindow window);

            FindButton(view, "ConfirmButtonImage").onClick.Invoke();

            Assert.AreEqual(1, _confirmedCount);
            Assert.AreSame(window, _requestedCloseWindow);
        }

        [Test]
        public void ConfirmButton_RepeatedChoice_InvokesActionOnce()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out _);
            Button confirmButton = FindButton(view, "ConfirmButtonImage");

            confirmButton.onClick.Invoke();
            confirmButton.onClick.Invoke();

            Assert.AreEqual(1, _confirmedCount);
        }

        [Test]
        public void OpenStopConstruction_NullAction_DoesNotKeepWindowOrPlayAudio()
        {
            _controller.OpenStopConstruction(_sourceWindow, new ISceneNode[] { _sourceShip }, null);

            Assert.IsEmpty(_windowManager.Windows);
            Assert.IsEmpty(_playedSounds);
        }

        [Test]
        public void OpenStopConstruction_Selection_RendersPromptAndPlaysStopSound()
        {
            Building building = new Building { DisplayName = "Shield Generator" };

            _controller.OpenStopConstruction(_sourceWindow, new ISceneNode[] { building }, Confirm);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            _controller.RenderWindows();

            CollectionAssert.AreEqual(
                new[]
                {
                    "Are you sure you want to stop construction of the following?",
                    "Shield Generator",
                },
                GetVisibleLines(view)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    _uiContext.GetPlayerFactionTheme().ConfirmDialogTheme.StopConstructionSoundPath,
                },
                _playedSounds
            );
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void OpenRetire_Selection_RendersPromptAndPlaysRetireSound()
        {
            Officer officer = new Officer { DisplayName = "General Veers" };

            _controller.OpenRetire(_sourceWindow, new ISceneNode[] { officer }, Confirm);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            _controller.RenderWindows();

            CollectionAssert.AreEqual(
                new[] { "Retire these personnel?", "General Veers" },
                GetVisibleLines(view)
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    _uiContext.GetPlayerFactionTheme().ConfirmDialogTheme.ScrapRetireSoundPath,
                },
                _playedSounds
            );
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void OpenMove_Selection_RendersProvidedTransitTimeWithoutPromptSound()
        {
            _controller.OpenMove(_sourceWindow, new ISceneNode[] { _sourceShip }, 12, Confirm);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            _controller.RenderWindows();

            CollectionAssert.AreEqual(
                new[] { "Transit Time in Days 12", "Assault Frigate" },
                GetVisibleLines(view)
            );
            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(1, _dirtyCount);
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
                () => _uiContext,
                path => _playedSounds?.Add(path),
                _windowLayer,
                _windowManager,
                () => new Vector2Int(122, 81),
                window => _requestedCloseWindow = window,
                () => _dirtyCount++
            );
        }

        private static GameRoot CreateGame()
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

        private static CapitalShip CreateSourceShip(GameRoot game)
        {
            GamePlanetSystem system = new GamePlanetSystem { InstanceID = "system" };
            game.AttachNode(system, game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            game.AttachNode(planet, system);
            GameFleet fleet = new GameFleet
            {
                InstanceID = "fleet",
                OwnerInstanceID = _playerFactionId,
            };
            game.AttachNode(fleet, planet);
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "ship",
                DisplayName = "Assault Frigate",
                OwnerInstanceID = _playerFactionId,
                Hyperdrive = 1,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            game.AttachNode(ship, fleet);
            return ship;
        }

        private ConfirmDialogWindowView OpenScrapAndInitializeView(out UIWindow window)
        {
            _controller.OpenScrap(_sourceWindow, new ISceneNode[] { _sourceShip }, Confirm);
            window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            return view;
        }

        private void Confirm()
        {
            _confirmedCount++;
        }

        private static string[] GetVisibleLines(ConfirmDialogWindowView view)
        {
            return view.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Where(text => text.gameObject.activeSelf)
                .Select(text => text.text)
                .ToArray();
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
    }
}
