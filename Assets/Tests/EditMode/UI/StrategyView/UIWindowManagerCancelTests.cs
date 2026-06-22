using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.StrategyView
{
    [TestFixture]
    public sealed class UIWindowManagerCancelTests
    {
        private GameObject windowManagerObject;

        [TearDown]
        public void TearDown()
        {
            if (windowManagerObject != null)
                Object.DestroyImmediate(windowManagerObject);
        }

        [Test]
        public void TryCancel_InvokesActiveWindowCloseRequest()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow window = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            int closedWindowId = 0;
            window.CloseRequested += closedWindow => closedWindowId = closedWindow.Id;

            Assert.IsTrue(windowManager.TryCancel());

            Assert.AreEqual(1, closedWindowId);
        }

        [Test]
        public void TryCancel_UsesFocusedWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow firstWindow = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            UIWindow secondWindow = CreateWindow(windowManager, 2, modal: false, canFocus: true);
            int closedWindowId = 0;
            firstWindow.CloseRequested += closedWindow => closedWindowId = closedWindow.Id;
            secondWindow.CloseRequested += closedWindow => closedWindowId = closedWindow.Id;

            Assert.IsTrue(windowManager.Focus(firstWindow));
            Assert.IsTrue(windowManager.TryCancel());

            Assert.AreEqual(1, closedWindowId);
        }

        [Test]
        public void TryCancel_ReturnsFalseWithoutActiveWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            CreateWindow(windowManager, 1, modal: false, canFocus: false);

            Assert.IsFalse(windowManager.TryCancel());
        }

        [Test]
        public void TryCancel_ReturnsFalseWhenWindowCannotClose()
        {
            UIWindowManager windowManager = CreateWindowManager();
            CreateWindow(windowManager, 1, modal: false, canFocus: true);

            Assert.IsFalse(windowManager.TryCancel());
        }

        [Test]
        public void TryCancel_LetsWindowContentConsumeCancelBeforeClose()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow window = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            TestCancelableContent content = window.gameObject.AddComponent<TestCancelableContent>();
            window.SetContent(content);
            bool closeRequested = false;
            window.CloseRequested += _ => closeRequested = true;

            Assert.IsTrue(windowManager.TryCancel());

            Assert.AreEqual(1, content.CancelCount);
            Assert.IsFalse(closeRequested);
        }

        private UIWindowManager CreateWindowManager()
        {
            windowManagerObject = new GameObject(
                "WindowManager",
                typeof(RectTransform),
                typeof(UIWindowManager)
            );
            RectTransform windowManagerRect = windowManagerObject.GetComponent<RectTransform>();
            windowManagerRect.sizeDelta = new Vector2(640, 481);
            return windowManagerObject.GetComponent<UIWindowManager>();
        }

        private UIWindow CreateWindow(
            UIWindowManager windowManager,
            int id,
            bool modal,
            bool canFocus
        )
        {
            GameObject windowObject = new GameObject(
                $"Window{id}",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            windowObject.transform.SetParent(windowManagerObject.transform, false);
            UIWindow window = windowObject.GetComponent<UIWindow>();
            window.Configure(id, 0, 0, 100, 80, modal, canFocus, canMove: false);
            windowManager.Register(window, behind: false);
            return window;
        }

        private sealed class TestCancelableContent : MonoBehaviour, ICancelable
        {
            public int CancelCount { get; private set; }

            public bool TryCancel()
            {
                CancelCount++;
                return true;
            }
        }
    }
}
