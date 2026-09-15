using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Finder
{
    [TestFixture]
    public class FinderWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private FinderWindowController _controller;
        private int _dirtyCount;
        private readonly List<string> _playedSfx = new List<string>();
        private GameObject _rootObject;
        private IReadOnlyList<GalaxyMapSector> _sectors;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            _playedSfx.Clear();
            GameRoot game = new GameRoot(TestConfig.Create());
            game.GetFactions()
                .Add(new Faction { InstanceID = _playerFactionId, DisplayName = "Player" });
            game.Summary.PlayerFactionID = _playerFactionId;
            game.SetFactionController(_playerFactionId, "PLAYER1", PlayerControllerType.Human);
            _uiContext = TestContent.CreateUIContext(
                game,
                TestContent.CreateThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _sectors = Array.Empty<GalaxyMapSector>();
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _windowManager.WindowCloseRequested += _windowManager.DestroyWindow;
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
        /// Verifies constructor null context provider throws argument null exception.
        /// </summary>
        [Test]
        public void Constructor_NullContextProvider_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FinderWindowController(
                    null,
                    _ => { },
                    _windowLayer,
                    _windowManager,
                    () => _sectors,
                    () => Vector2Int.zero,
                    (_, _) => false,
                    _ => { },
                    () => { }
                )
            );
        }

        /// <summary>
        /// Verifies open closed mode creates named bound window at configured position.
        /// </summary>
        [Test]
        public void Open_ClosedMode_CreatesNamedBoundWindowAtConfiguredPosition()
        {
            _controller.Open(FinderMode.Fleets);

            Assert.AreEqual(1, _windowManager.Windows.Count);
            UIWindow window = _windowManager.Windows.Single();
            Assert.AreEqual("FinderWindow-Fleets", window.Content.name);
            Assert.AreEqual(new Vector2Int(87, 61), new Vector2Int(window.X, window.Y));
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out FinderWindowView finderView));
            Assert.AreEqual(FinderMode.Fleets, _controller.GetMode(finderView));
            FinderWindowState state = _controller.GetState(finderView);
            Assert.AreEqual(FinderMode.Fleets, state.Mode);
            Assert.AreEqual(1, state.ActiveTab);
            Assert.AreEqual(-1, state.SelectedIndex);
            Assert.AreEqual(1, _dirtyCount);
        }

        /// <summary>
        /// Verifies that repeating a finder command closes its existing window.
        /// </summary>
        [Test]
        public void Open_ExistingMode_TogglesWindowClosed()
        {
            _controller.Open(FinderMode.Systems);

            _controller.Open(FinderMode.Systems);

            Assert.AreEqual(0, _windowManager.Windows.Count);
            Assert.IsNull(_windowManager.ActiveWindow);
            Assert.AreEqual(1, _dirtyCount);
        }

        /// <summary>
        /// Verifies that requesting another finder mode replaces the exclusive finder.
        /// </summary>
        [Test]
        public void Open_DifferentModes_ReplacesExistingFinder()
        {
            _controller.Open(FinderMode.Systems);
            _controller.Open(FinderMode.Personnel);

            Assert.AreEqual(1, _windowManager.Windows.Count);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out FinderWindowView view);
            Assert.AreEqual(FinderMode.Personnel, _controller.GetMode(view));
            Assert.AreEqual(2, _dirtyCount);
        }

        /// <summary>
        /// Verifies bind window different mode for bound view throws invalid operation exception.
        /// </summary>
        [Test]
        public void BindWindow_DifferentModeForBoundView_ThrowsInvalidOperationException()
        {
            _controller.Open(FinderMode.Fleets);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out FinderWindowView view);

            Assert.Throws<InvalidOperationException>(() =>
                _controller.BindWindow(view, FinderMode.Troops)
            );
        }

        /// <summary>
        /// Verifies search input bound window updates controller session and marks dirty.
        /// </summary>
        [Test]
        public void SearchInput_BoundWindow_UpdatesControllerSessionAndMarksDirty()
        {
            _controller.Open(FinderMode.Fleets);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out FinderWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            TMP_InputField searchInput = view.GetComponentsInChildren<TMP_InputField>(true)
                .Single(input => input.name == "LabelInputField");

            searchInput.onValueChanged.Invoke("transport");

            Assert.AreEqual("transport", _controller.GetState(view).SearchText);
            Assert.AreEqual(2, _dirtyCount);
        }

        /// <summary>
        /// Verifies dialog control pointer down plays shared control sound before click.
        /// </summary>
        [Test]
        public void DialogControl_PointerDown_PlaysSharedControlSoundBeforeClick()
        {
            _controller.Open(FinderMode.Systems);
            UIWindow window = _windowManager.Windows.Single();
            _windowManager.TryGetWindowView(window, out FinderWindowView view);
            UIComponentTestHelper.InvokeLifecycle(view, "Awake");
            _controller.RenderWindow(view, window);
            RawImagePressVisual closePressVisual =
                view.GetComponentsInChildren<RawImagePressVisual>(true)
                    .Single(visual => visual.name == "TwoButtonLayoutCloseButtonImage");

            closePressVisual.OnPointerDown(
                new PointerEventData(null) { button = PointerEventData.InputButton.Left }
            );

            CollectionAssert.AreEqual(new[] { StrategyUISoundPaths.ControlPress }, _playedSfx);
            Assert.AreEqual(1, _windowManager.Windows.Count);
        }

        /// <summary>
        /// Verifies reconcile windows unavailable sectors throws invalid operation exception.
        /// </summary>
        [Test]
        public void ReconcileWindows_UnavailableSectors_ThrowsInvalidOperationException()
        {
            _controller.Open(FinderMode.Systems);
            _sectors = null;

            Assert.Throws<InvalidOperationException>(() => _controller.ReconcileWindows());
        }

        /// <summary>
        /// Verifies get mode unbound view throws invalid operation exception.
        /// </summary>
        [Test]
        public void GetMode_UnboundView_ThrowsInvalidOperationException()
        {
            FinderWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.FinderWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => _controller.GetMode(view));
        }

        /// <summary>
        /// Creates controller.
        /// </summary>
        /// <returns>The created controller.</returns>
        private FinderWindowController CreateController()
        {
            return new FinderWindowController(
                () => _uiContext,
                path => _playedSfx.Add(path),
                _windowLayer,
                _windowManager,
                () => _sectors,
                () => new Vector2Int(87, 61),
                (_, _) => false,
                _ => { },
                () => _dirtyCount++
            );
        }
    }
}
