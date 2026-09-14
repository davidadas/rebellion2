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

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("WindowManager", typeof(RectTransform), typeof(UIWindowManager));
            _windowManager = _root.GetComponent<UIWindowManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Capture_RegisteredModelessWindow_ReplacesSavedState()
        {
            List<WindowState> states = new List<WindowState>
            {
                new WindowState { WindowTypeID = "Stale" },
            };
            StrategyWindowStateManager manager = new StrategyWindowStateManager(
                _windowManager,
                states
            );
            manager.Register(CreateAdapter(_ => true));
            CreateWindow<TestWindowContent>(7, 11, modal: false);

            manager.Capture();

            Assert.AreEqual(1, states.Count);
            Assert.AreEqual("Test.Window", states[0].WindowTypeID);
            Assert.AreEqual("TARGET1", states[0].TargetInstanceID);
            Assert.AreEqual(7, states[0].X);
            Assert.AreEqual(11, states[0].Y);
            Assert.AreEqual(100, states[0].Width);
            Assert.AreEqual(80, states[0].Height);
        }

        [Test]
        public void Capture_UnsupportedAndModalWindows_DoesNotPersistThem()
        {
            List<WindowState> states = new List<WindowState>();
            StrategyWindowStateManager manager = new StrategyWindowStateManager(
                _windowManager,
                states
            );
            manager.Register(CreateAdapter(_ => true));
            CreateWindow<OtherWindowContent>(0, 0, modal: false);
            CreateWindow<TestWindowContent>(0, 0, modal: true);

            manager.Capture();

            CollectionAssert.IsEmpty(states);
        }

        [Test]
        public void Restore_RegisteredStates_RestoresInSavedStackOrder()
        {
            List<string> restoredTargets = new List<string>();
            List<WindowState> states = new List<WindowState>
            {
                new WindowState
                {
                    WindowTypeID = "Unknown.Window",
                    TargetInstanceID = "IGNORED",
                    ZOrder = 0,
                },
                new WindowState
                {
                    WindowTypeID = "Test.Window",
                    TargetInstanceID = "SECOND",
                    ZOrder = 2,
                },
                new WindowState
                {
                    WindowTypeID = "Test.Window",
                    TargetInstanceID = "FIRST",
                    ZOrder = 1,
                },
            };
            StrategyWindowStateManager manager = new StrategyWindowStateManager(
                _windowManager,
                states
            );
            manager.Register(
                CreateAdapter(state =>
                {
                    restoredTargets.Add(state.TargetInstanceID);
                    return true;
                })
            );

            manager.Restore();

            CollectionAssert.AreEqual(new[] { "FIRST", "SECOND" }, restoredTargets);
        }

        private StrategyWindowStateAdapter<TestWindowContent> CreateAdapter(
            System.Func<WindowState, bool> restore
        )
        {
            return new StrategyWindowStateAdapter<TestWindowContent>(
                "Test.Window",
                _ => "TARGET1",
                restore
            );
        }

        private void CreateWindow<TContent>(int x, int y, bool modal)
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
        }

        private sealed class TestWindowContent : MonoBehaviour { }

        private sealed class OtherWindowContent : MonoBehaviour { }
    }
}
