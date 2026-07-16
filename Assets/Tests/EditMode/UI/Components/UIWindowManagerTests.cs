using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class UIWindowManagerTests
    {
        private GameObject _windowManagerObject;

        [TearDown]
        public void TearDown()
        {
            if (_windowManagerObject != null)
                Object.DestroyImmediate(_windowManagerObject);
        }

        [Test]
        public void TryCancel_ActiveWindow_EmitsCloseRequest()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow window = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            UIWindow closedWindow = null;
            windowManager.WindowCloseRequested += requestedWindow => closedWindow = requestedWindow;

            bool cancelled = windowManager.TryCancel();

            Assert.IsTrue(cancelled);
            Assert.AreSame(window, closedWindow);
        }

        [Test]
        public void TryCancel_FocusedWindow_UsesFocusedWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow firstWindow = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            CreateWindow(windowManager, 2, modal: false, canFocus: true);
            UIWindow closedWindow = null;
            windowManager.WindowCloseRequested += requestedWindow => closedWindow = requestedWindow;

            windowManager.Focus(firstWindow);
            bool cancelled = windowManager.TryCancel();

            Assert.IsTrue(cancelled);
            Assert.AreSame(firstWindow, closedWindow);
        }

        [Test]
        public void TryCancel_WithoutActiveWindow_ReturnsFalse()
        {
            UIWindowManager windowManager = CreateWindowManager();
            CreateWindow(windowManager, 1, modal: false, canFocus: false);

            bool cancelled = windowManager.TryCancel();

            Assert.IsFalse(cancelled);
        }

        [Test]
        public void TryCancel_WithoutCloseListener_ReturnsFalse()
        {
            UIWindowManager windowManager = CreateWindowManager();
            CreateWindow(windowManager, 1, modal: false, canFocus: true);

            bool cancelled = windowManager.TryCancel();

            Assert.IsFalse(cancelled);
        }

        [Test]
        public void TryCancel_ContentConsumesCancel_DoesNotEmitCloseRequest()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow window = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            TestCancelableContent content = window.gameObject.AddComponent<TestCancelableContent>();
            window.SetContent(content);
            int closeRequestCount = 0;
            windowManager.WindowCloseRequested += _ => closeRequestCount++;

            bool cancelled = windowManager.TryCancel();

            Assert.IsTrue(cancelled);
            Assert.AreEqual(1, content.CancelCount);
            Assert.AreEqual(0, closeRequestCount);
        }

        [Test]
        public void Register_ModalWindow_BlocksEarlierWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow firstWindow = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            UIWindow modalWindow = CreateWindow(windowManager, 2, modal: true, canFocus: true);

            Assert.IsFalse(windowManager.CanInteractWithWindow(firstWindow));
            Assert.IsTrue(windowManager.CanInteractWithWindow(modalWindow));
            Assert.AreSame(modalWindow, windowManager.ActiveWindow);
        }

        [Test]
        public void Unregister_ActiveWindow_PromotesPreviousFocusableWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow firstWindow = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            UIWindow secondWindow = CreateWindow(windowManager, 2, modal: false, canFocus: true);

            windowManager.Unregister(secondWindow);

            Assert.AreSame(firstWindow, windowManager.ActiveWindow);
            Assert.IsTrue(firstWindow.ActiveWindow);
        }

        private UIWindowManager CreateWindowManager()
        {
            _windowManagerObject = new GameObject(
                "WindowManager",
                typeof(RectTransform),
                typeof(UIWindowManager)
            );
            RectTransform windowManagerRect = _windowManagerObject.GetComponent<RectTransform>();
            windowManagerRect.sizeDelta = new Vector2(640, 481);
            return _windowManagerObject.GetComponent<UIWindowManager>();
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
            windowObject.transform.SetParent(_windowManagerObject.transform, false);
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
