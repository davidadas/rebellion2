using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GalaxyPlanetSector = Rebellion.Game.Galaxy.PlanetSector;
using GameFleet = Rebellion.Game.Units.Fleet;

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
        private CapitalShip _sourceShip;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _confirmedCount = 0;
            _dirtyCount = 0;
            _requestedCloseWindow = null;
            GameRoot game = CreateGame();
            _uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _sourceShip = CreateSourceShip(game);
            _playedSounds = new List<string>();
            _controller = CreateController();
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        /// <summary>
        /// Verifies constructor null required dependency throws argument null exception.
        /// </summary>
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

        /// <summary>
        /// Verifies bind window null view throws argument null exception.
        /// </summary>
        [Test]
        public void BindWindow_NullView_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _controller.BindWindow(null));
        }

        /// <summary>
        /// Verifies open scrap valid selection creates modal session plays prompt and marks dirty.
        /// </summary>
        [Test]
        public void OpenScrap_ValidSelection_CreatesModalSessionPlaysPromptAndMarksDirty()
        {
            _controller.OpenScrap(new ISceneNode[] { _sourceShip }, Confirm);

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

        /// <summary>
        /// Verifies render windows open scrap renders prompt selection and configured artwork.
        /// </summary>
        [Test]
        public void RenderWindows_OpenScrap_RendersPromptSelectionAndConfiguredArtwork()
        {
            _controller.OpenScrap(new ISceneNode[] { _sourceShip }, Confirm);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");

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

        /// <summary>
        /// Verifies cancel button open scrap closes without invoking action.
        /// </summary>
        [Test]
        public void CancelButton_OpenScrap_ClosesWithoutInvokingAction()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out UIWindow window);

            FindButton(view, "CancelButtonImage").onClick.Invoke();

            Assert.AreSame(window, _requestedCloseWindow);
            Assert.AreEqual(0, _confirmedCount);
        }

        /// <summary>
        /// Verifies confirm button open scrap invokes action and closes dialog.
        /// </summary>
        [Test]
        public void ConfirmButton_OpenScrap_InvokesActionAndClosesDialog()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out UIWindow window);

            FindButton(view, "ConfirmButtonImage").onClick.Invoke();

            Assert.AreEqual(1, _confirmedCount);
            Assert.AreSame(window, _requestedCloseWindow);
        }

        /// <summary>
        /// Verifies confirm button repeated choice invokes action once.
        /// </summary>
        [Test]
        public void ConfirmButton_RepeatedChoice_InvokesActionOnce()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out _);
            Button confirmButton = FindButton(view, "ConfirmButtonImage");

            confirmButton.onClick.Invoke();
            confirmButton.onClick.Invoke();

            Assert.AreEqual(1, _confirmedCount);
        }

        /// <summary>
        /// Verifies open stop construction null action does not keep window or play audio.
        /// </summary>
        [Test]
        public void OpenStopConstruction_NullAction_DoesNotKeepWindowOrPlayAudio()
        {
            _controller.OpenStopConstruction(new ISceneNode[] { _sourceShip }, null);

            Assert.IsEmpty(_windowManager.Windows);
            Assert.IsEmpty(_playedSounds);
        }

        /// <summary>
        /// Verifies open stop construction selection renders prompt and plays stop sound.
        /// </summary>
        [Test]
        public void OpenStopConstruction_Selection_RendersPromptAndPlaysStopSound()
        {
            Building building = new Building { DisplayName = "Shield Generator" };

            _controller.OpenStopConstruction(new ISceneNode[] { building }, Confirm);
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

        /// <summary>
        /// Verifies open retire selection renders prompt and plays retire sound.
        /// </summary>
        [Test]
        public void OpenRetire_Selection_RendersPromptAndPlaysRetireSound()
        {
            Officer officer = new Officer { DisplayName = "General Veers" };

            _controller.OpenRetire(new ISceneNode[] { officer }, Confirm);
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

        /// <summary>
        /// Verifies open move selection renders provided transit time without prompt sound.
        /// </summary>
        [Test]
        public void OpenMove_Selection_RendersProvidedTransitTimeWithoutPromptSound()
        {
            _controller.OpenMove(new ISceneNode[] { _sourceShip }, 12, Confirm);
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

        /// <summary>
        /// Verifies open mission abort mission renders original prompt without prompt sound.
        /// </summary>
        [Test]
        public void OpenMissionAbort_Mission_RendersOriginalPromptWithoutPromptSound()
        {
            Mission mission = new TestMission { DisplayName = "Diplomacy" };

            _controller.OpenMissionAbort(mission, Confirm);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            _controller.RenderWindows();

            CollectionAssert.AreEqual(
                new[] { "Are you sure you want to abort this mission?" },
                GetVisibleLines(view)
            );
            Assert.IsEmpty(_playedSounds);
            Assert.AreEqual(1, _dirtyCount);
        }

        /// <summary>
        /// Verifies view destroyed initialized session releases session state.
        /// </summary>
        [Test]
        public void ViewDestroyed_InitializedSession_ReleasesSessionState()
        {
            ConfirmDialogWindowView view = OpenScrapAndInitializeView(out UIWindow window);

            UIComponentTestHelper.InvokeLifecycle(view, "OnDestroy");

            Assert.Throws<InvalidOperationException>(() => _controller.RenderWindow(view, window));
        }

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
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

        private sealed class TestMission : Mission
        {
            /// <summary>
            /// Creates node copy.
            /// </summary>
            /// <returns>The created node copy.</returns>
            protected override BaseSceneNode CreateNodeCopy() => new TestMission();

            /// <summary>
            /// Checks whether the repeat after completion condition is met.
            /// </summary>
            /// <param name="game">The game.</param>
            /// <returns>True when the repeat after completion condition is met; otherwise false.</returns>
            public override bool ShouldRepeatAfterCompletion(GameRoot game) => false;
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
        private static GameRoot CreateGame()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _playerFactionId });
            game.Summary.PlayerFactionID = _playerFactionId;
            game.SetFactionController(_playerFactionId, "PLAYER1", PlayerControllerType.Human);
            return game;
        }

        /// <summary>
        /// Creates source ship.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <returns>The created source ship.</returns>
        private static CapitalShip CreateSourceShip(GameRoot game)
        {
            GalaxyPlanetSector planetSector = new GalaxyPlanetSector { InstanceID = "sector" };
            game.AttachNode(planetSector, game.GetGalaxyMap());
            Planet planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _playerFactionId,
                IsColonized = true,
            };
            game.AttachNode(planet, planetSector);
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

        /// <summary>
        /// Opens scrap and initialize view.
        /// </summary>
        /// <param name="window">Receives the window.</param>
        /// <returns>The result of open scrap and initialize view.</returns>
        private ConfirmDialogWindowView OpenScrapAndInitializeView(out UIWindow window)
        {
            _controller.OpenScrap(new ISceneNode[] { _sourceShip }, Confirm);
            window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            return view;
        }

        /// <summary>
        /// Executes confirm.
        /// </summary>
        private void Confirm()
        {
            _confirmedCount++;
        }

        /// <summary>
        /// Gets visible lines.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <returns>The requested visible lines.</returns>
        private static string[] GetVisibleLines(ConfirmDialogWindowView view)
        {
            return view.GetComponentsInChildren<TextMeshProUGUI>(true)
                .Where(text => text.gameObject.activeSelf)
                .Select(text => text.text)
                .ToArray();
        }

        /// <summary>
        /// Finds button.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching button.</returns>
        private static Button FindButton(ConfirmDialogWindowView view, string objectName)
        {
            return view.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == objectName);
        }

        /// <summary>
        /// Finds image.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="objectName">The object name.</param>
        /// <returns>The matching image.</returns>
        private static RawImage FindImage(ConfirmDialogWindowView view, string objectName)
        {
            return view.GetComponentsInChildren<RawImage>(true)
                .Single(image => image.name == objectName);
        }
    }
}
