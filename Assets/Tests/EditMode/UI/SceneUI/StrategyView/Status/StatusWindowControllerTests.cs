using System;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Status
{
    [TestFixture]
    public class StatusWindowControllerTests
    {
        private const string _playerFactionId = "FNALL1";
        private const string _strategyViewPrefabPath =
            "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private int _closeCount;
        private StatusWindowController _controller;
        private int _dirtyCount;
        private GameObject _rootObject;
        private UIContext _uiContext;
        private ISceneNode _visibleNode;
        private StrategyWindowLayerView _windowLayer;
        private UIWindowManager _windowManager;

        [SetUp]
        public void SetUp()
        {
            _closeCount = 0;
            _dirtyCount = 0;
            _visibleNode = null;
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
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_strategyViewPrefabPath);
            _windowLayer = _rootObject.GetComponentInChildren<StrategyWindowLayerView>(true);
            _windowManager = _rootObject.GetComponentInChildren<UIWindowManager>(true);
            _controller = CreateController();
            _controller.Initialize(new TestActions());
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
                new StatusWindowController(
                    null,
                    _windowLayer,
                    _windowManager,
                    () => Array.Empty<GalaxyMapSector>(),
                    _ => null,
                    _ => { },
                    () => Vector2Int.zero,
                    _ => { },
                    () => { }
                )
            );
        }

        [Test]
        public void BindWindow_BeforeInitialization_ThrowsInvalidOperationException()
        {
            StatusWindowController controller = CreateController();
            StatusWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.StatusWindowPrefab,
                _rootObject.transform
            );

            Assert.Throws<InvalidOperationException>(() => controller.BindWindow(view));
        }

        [Test]
        public void TryInitializeWindow_InvalidInputs_ReturnFalse()
        {
            StatusWindowView view = UnityEngine.Object.Instantiate(
                _windowLayer.StatusWindowPrefab,
                _rootObject.transform
            );
            StrategyStatusTarget target = new StrategyStatusTarget(null, new Officer());

            Assert.IsFalse(_controller.TryInitializeWindow(null, target, false));
            Assert.IsFalse(_controller.TryInitializeWindow(view, null, false));
        }

        [Test]
        public void Open_ValidTarget_CreatesNamedBoundWindowAndMarksDirty()
        {
            Officer officer = new Officer { DisplayName = "General Veers" };

            bool opened = _controller.Open(new StrategyStatusTarget(null, officer));

            Assert.IsTrue(opened);
            Assert.AreEqual(1, _windowManager.Windows.Count);
            UIWindow window = _windowManager.Windows.Single();
            Assert.AreEqual("StatusWindow-General Veers", window.Content.name);
            Assert.IsTrue(_windowManager.TryGetWindowView(window, out StatusWindowView _));
            Assert.AreEqual(1, _dirtyCount);
            Assert.AreEqual(0, _closeCount);
        }

        [Test]
        public void Open_NullTarget_ReturnsFalseWithoutCreatingWindow()
        {
            bool opened = _controller.Open(null);

            Assert.IsFalse(opened);
            Assert.IsEmpty(_windowManager.Windows);
            Assert.AreEqual(0, _dirtyCount);
        }

        [Test]
        public void Open_ReplacementTarget_ClosesExistingStatusWindow()
        {
            _controller.Open(new StrategyStatusTarget(null, new Officer { DisplayName = "First" }));

            bool opened = _controller.Open(
                new StrategyStatusTarget(null, new Officer { DisplayName = "Second" })
            );

            Assert.IsTrue(opened);
            Assert.AreEqual(1, _windowManager.Windows.Count);
            Assert.AreEqual(1, _closeCount);
            Assert.AreEqual(2, _dirtyCount);
        }

        [Test]
        public void ReconcileWindows_MissingVisibleTarget_ClosesStatusWindow()
        {
            Officer officer = new Officer { InstanceID = "officer", DisplayName = "Officer" };
            _visibleNode = officer;
            _controller.Open(new StrategyStatusTarget(null, officer));
            _visibleNode = null;

            _controller.ReconcileWindows(Array.Empty<GalaxyMapSector>());

            Assert.IsEmpty(_windowManager.Windows);
            Assert.AreEqual(1, _closeCount);
        }

        private StatusWindowController CreateController()
        {
            return new StatusWindowController(
                () => _uiContext,
                _windowLayer,
                _windowManager,
                () => Array.Empty<GalaxyMapSector>(),
                _ => _visibleNode,
                _ => { },
                () => new Vector2Int(75, 40),
                window =>
                {
                    _closeCount++;
                    _windowManager.Unregister(window);
                    if (window?.Content != null)
                        UnityEngine.Object.DestroyImmediate(window.Content.gameObject);
                },
                () => _dirtyCount++
            );
        }

        private sealed class TestActions : IStatusWindowActions
        {
            public void OpenStatusInfo(StrategyStatusTarget target) { }
        }
    }
}
