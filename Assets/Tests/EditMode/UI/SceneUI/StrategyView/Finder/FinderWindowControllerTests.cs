using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using TMPro;
using UnityEngine;

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
        private GameObject _rootObject;
        private IReadOnlyList<GalaxyMapSector> _sectors;
        private UIContext _uiContext;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _dirtyCount = 0;
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(
                new Faction { InstanceID = _playerFactionId, DisplayName = "Player" }
            );
            game.Summary.PlayerFactionID = _playerFactionId;
            _uiContext = new UIContext(
                game,
                new FactionThemeLibrary(),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>())
            );
            _sectors = Array.Empty<GalaxyMapSector>();
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _controller = CreateController();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

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
            Assert.AreEqual(0, state.ActiveTab);
            Assert.AreEqual(-1, state.SelectedIndex);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void Open_ExistingMode_FocusesWithoutCreatingAnotherWindow()
        {
            _controller.Open(FinderMode.Systems);
            UIWindow window = _windowManager.Windows.Single();

            _controller.Open(FinderMode.Systems);

            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreSame(window, _windowManager.ActiveWindow);
            Assert.AreEqual(1, _dirtyCount);
        }

        [Test]
        public void Open_DifferentModes_CreatesIndependentWindowSessions()
        {
            _controller.Open(FinderMode.Systems);
            _controller.Open(FinderMode.Personnel);

            Assert.AreEqual(2, _windowManager.Windows.Count);
            CollectionAssert.AreEquivalent(
                new[] { FinderMode.Systems, FinderMode.Personnel },
                _windowManager.Windows.Select(window =>
                {
                    _windowManager.TryGetWindowView(window, out FinderWindowView view);
                    return _controller.GetMode(view);
                })
            );
            Assert.AreEqual(2, _dirtyCount);
        }

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

        [Test]
        public void ReconcileWindows_UnavailableSectors_ThrowsInvalidOperationException()
        {
            _controller.Open(FinderMode.Systems);
            _sectors = null;

            Assert.Throws<InvalidOperationException>(() => _controller.ReconcileWindows());
        }

        [Test]
        public void GetMode_UnboundView_ThrowsInvalidOperationException()
        {
            FinderWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.FinderWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => _controller.GetMode(view));
        }

        private FinderWindowController CreateController()
        {
            return new FinderWindowController(
                () => _uiContext,
                _ => { },
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
