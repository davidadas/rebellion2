using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.UIState;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Windows
{
    [TestFixture]
    public class StrategyWindowStateManagerTests
    {
        private GameObject _root;
        private UIWindowManager _windowManager;

        /// <summary>
        /// Creates the window manager used by each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("WindowManager", typeof(RectTransform), typeof(UIWindowManager));
            _windowManager = _root.GetComponent<UIWindowManager>();
        }

        /// <summary>
        /// Destroys the test window hierarchy.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        /// <summary>
        /// Verifies that capture replaces stale state with a complete supported window record.
        /// </summary>
        [Test]
        public void Capture_RegisteredModelessWindow_ReplacesSavedState()
        {
            List<WindowState> states = new List<WindowState>
            {
                new WindowState("Stale", null, 0, 0, 0, 0, 0),
            };
            StrategyWindowStateManager manager = new StrategyWindowStateManager(
                _windowManager,
                states
            );
            manager.Register(CreateAdapter(_ => null));
            CreateWindow<TestWindowContent>(7, 11, modal: false);

            manager.Capture();

            Assert.AreEqual(1, states.Count);
            Assert.AreEqual("Test.Window", states[0].GetWindowTypeID());
            Assert.AreEqual("TARGET1", states[0].GetTargetInstanceID());
            Assert.AreEqual(7, states[0].GetX());
            Assert.AreEqual(11, states[0].GetY());
            Assert.AreEqual(100, states[0].GetWidth());
            Assert.AreEqual(80, states[0].GetHeight());
        }

        /// <summary>
        /// Verifies that unsupported and modal windows are excluded from persisted state.
        /// </summary>
        [Test]
        public void Capture_UnsupportedAndModalWindows_DoesNotPersistThem()
        {
            List<WindowState> states = new List<WindowState>();
            StrategyWindowStateManager manager = new StrategyWindowStateManager(
                _windowManager,
                states
            );
            manager.Register(CreateAdapter(_ => null));
            CreateWindow<OtherWindowContent>(0, 0, modal: false);
            CreateWindow<TestWindowContent>(0, 0, modal: true);

            manager.Capture();

            CollectionAssert.IsEmpty(states);
        }

        /// <summary>
        /// Verifies that supported windows restore in their persisted stacking order.
        /// </summary>
        [Test]
        public void Restore_RegisteredStates_RestoresInSavedStackOrder()
        {
            List<string> restoredTargets = new List<string>();
            List<WindowState> states = new List<WindowState>
            {
                new WindowState("Unknown.Window", "IGNORED", 0, 0, 0, 0, 0),
                new WindowState("Test.Window", "SECOND", 0, 0, 0, 0, 2),
                new WindowState("Test.Window", "FIRST", 0, 0, 0, 0, 1),
            };
            StrategyWindowStateManager manager = new StrategyWindowStateManager(
                _windowManager,
                states
            );
            manager.Register(
                CreateAdapter(state =>
                {
                    restoredTargets.Add(state.GetTargetInstanceID());
                    return CreateWindow<TestWindowContent>(0, 0, modal: false);
                })
            );

            manager.Restore();

            CollectionAssert.AreEqual(new[] { "FIRST", "SECOND" }, restoredTargets);
        }

        /// <summary>
        /// Verifies that restoration skips null entries and reapplies saved dimensions and order.
        /// </summary>
        [Test]
        public void Restore_NullAndSizedStates_RestoresValidWindowBoundsAndStackOrder()
        {
            List<WindowState> states = new List<WindowState>
            {
                new WindowState("Test.Window", "TOP", 0, 0, 140, 90, 2),
                null,
                new WindowState("Test.Window", "BOTTOM", 0, 0, 120, 70, 1),
            };
            StrategyWindowStateManager manager = new StrategyWindowStateManager(
                _windowManager,
                states
            );
            manager.Register(CreateAdapter(_ => CreateWindow<TestWindowContent>(0, 0, false)));

            manager.Restore();

            Assert.AreEqual(2, _windowManager.Windows.Count);
            Assert.AreEqual(120, _windowManager.Windows[0].Width);
            Assert.AreEqual(70, _windowManager.Windows[0].Height);
            Assert.AreEqual(140, _windowManager.Windows[1].Width);
            Assert.AreEqual(90, _windowManager.Windows[1].Height);
        }

        /// <summary>
        /// Creates the test adapter used to capture and restore test windows.
        /// </summary>
        /// <param name="restore">The callback invoked to restore persisted state.</param>
        /// <returns>The configured test adapter.</returns>
        private StrategyWindowStateAdapter<TestWindowContent> CreateAdapter(
            System.Func<WindowState, UIWindow> restore
        )
        {
            return new StrategyWindowStateAdapter<TestWindowContent>(
                "Test.Window",
                _ => "TARGET1",
                restore
            );
        }

        /// <summary>
        /// Creates and registers a test window with the runtime window manager.
        /// </summary>
        /// <typeparam name="TContent">The authored content component placed in the window.</typeparam>
        /// <param name="x">The horizontal window position.</param>
        /// <param name="y">The vertical window position.</param>
        /// <param name="modal">Whether the window is modal.</param>
        /// <returns>The registered test window.</returns>
        private UIWindow CreateWindow<TContent>(int x, int y, bool modal)
            where TContent : MonoBehaviour
        {
            GameObject windowObject = new GameObject(
                typeof(TContent).Name,
                typeof(RectTransform),
                typeof(UIWindow),
                typeof(TContent)
            );
            windowObject.transform.SetParent(_root.transform, false);
            UIWindow window = windowObject.GetComponent<UIWindow>();
            window.SetContent(windowObject.GetComponent<TContent>());
            window.Configure(1 + _windowManager.Windows.Count, x, y, 100, 80, modal, true, false);
            _windowManager.Register(window, false);
            return window;
        }

        private sealed class TestWindowContent : MonoBehaviour { }

        private sealed class OtherWindowContent : MonoBehaviour { }
    }
}
