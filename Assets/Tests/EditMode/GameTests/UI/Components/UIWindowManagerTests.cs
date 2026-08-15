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

        /// <summary>
        /// Verifies that repeating an exclusive-window request closes the current window.
        /// </summary>
        [Test]
        public void ToggleExclusiveWindow_RepeatedRequest_ClosesCurrentWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            TestContent prefab = CreateWindowPrefab<TestContent>();
            windowManager.WindowCloseRequested += windowManager.DestroyWindow;
            UIWindow first = ToggleExclusiveWindow(windowManager, prefab, null);

            UIWindow second = ToggleExclusiveWindow(windowManager, prefab, null);

            Assert.IsNotNull(first);
            Assert.IsNull(second);
            Assert.AreEqual(0, windowManager.Windows.Count);
        }

        /// <summary>
        /// Verifies that a different exclusive-window request replaces the current window.
        /// </summary>
        [Test]
        public void ToggleExclusiveWindow_DifferentRequest_ReplacesCurrentWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            TestContent firstPrefab = CreateWindowPrefab<TestContent>();
            OtherTestContent secondPrefab = CreateWindowPrefab<OtherTestContent>();
            windowManager.WindowCloseRequested += windowManager.DestroyWindow;
            UIWindow first = ToggleExclusiveWindow(windowManager, firstPrefab, null);

            UIWindow second = ToggleExclusiveWindow(windowManager, secondPrefab, null);

            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            Assert.AreEqual(1, windowManager.Windows.Count);
            Assert.AreSame(second, windowManager.Windows[0]);
        }

        /// <summary>
        /// Verifies that successive exclusive requests never register multiple exclusive windows.
        /// </summary>
        [Test]
        public void ToggleExclusiveWindow_SuccessiveRequests_RegisterOnlyOneWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            TestContent firstPrefab = CreateWindowPrefab<TestContent>();
            OtherTestContent secondPrefab = CreateWindowPrefab<OtherTestContent>();
            windowManager.WindowCloseRequested += windowManager.DestroyWindow;

            ToggleExclusiveWindow(windowManager, firstPrefab, null);
            ToggleExclusiveWindow(windowManager, secondPrefab, null);
            ToggleExclusiveWindow(windowManager, firstPrefab, null);

            Assert.AreEqual(1, windowManager.Windows.Count);
            Assert.IsTrue(windowManager.Windows[0].Content is TestContent);
        }

        /// <summary>
        /// Verifies that a blocking modal overlay prevents changes to the exclusive slot.
        /// </summary>
        [Test]
        public void ToggleExclusiveWindow_BlockingOverlay_DoesNotChangeExclusiveWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            TestContent prefab = CreateWindowPrefab<TestContent>();
            windowManager.WindowCloseRequested += windowManager.DestroyWindow;
            UIWindow exclusive = ToggleExclusiveWindow(windowManager, prefab, null);
            UIWindow overlay = CreateWindow(windowManager, 200, modal: true, canFocus: true);

            UIWindow result = ToggleExclusiveWindow(windowManager, prefab, null);

            Assert.IsNull(result);
            Assert.AreEqual(2, windowManager.Windows.Count);
            Assert.Contains(exclusive, (System.Collections.ICollection)windowManager.Windows);
            Assert.AreSame(overlay, windowManager.ActiveWindow);
        }

        /// <summary>
        /// Verifies that external closure releases the exclusive slot for another window.
        /// </summary>
        [Test]
        public void ToggleExclusiveWindow_AfterExternalClose_OpensReplacement()
        {
            UIWindowManager windowManager = CreateWindowManager();
            TestContent prefab = CreateWindowPrefab<TestContent>();
            UIWindow first = ToggleExclusiveWindow(windowManager, prefab, null);
            windowManager.DestroyWindow(first);

            UIWindow replacement = ToggleExclusiveWindow(windowManager, prefab, null);

            Assert.IsNotNull(replacement);
            Assert.AreEqual(1, windowManager.Windows.Count);
            Assert.AreSame(replacement, windowManager.Windows[0]);
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

        [Test]
        public void FindWindow_RegisteredContent_ReturnsOwningWindow()
        {
            UIWindowManager windowManager = CreateWindowManager();
            CreateWindow(windowManager, 1, modal: false, canFocus: true);
            UIWindow expected = CreateWindow(windowManager, 2, modal: false, canFocus: true);
            TestCancelableContent content =
                expected.gameObject.AddComponent<TestCancelableContent>();
            expected.SetContent(content);

            UIWindow window = windowManager.FindWindow<TestCancelableContent>();

            Assert.AreSame(expected, window);
        }

        [Test]
        public void FindWindow_MissingContent_ReturnsNull()
        {
            UIWindowManager windowManager = CreateWindowManager();
            CreateWindow(windowManager, 1, modal: false, canFocus: true);

            UIWindow window = windowManager.FindWindow<TestCancelableContent>();

            Assert.IsNull(window);
        }

        [Test]
        public void FindWindowView_MatchingPredicate_ReturnsAuthoredContent()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow first = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            UIWindow second = CreateWindow(windowManager, 2, modal: false, canFocus: true);
            TestCancelableContent firstContent =
                first.gameObject.AddComponent<TestCancelableContent>();
            TestCancelableContent expected =
                second.gameObject.AddComponent<TestCancelableContent>();
            first.SetContent(firstContent);
            second.SetContent(expected);

            TestCancelableContent result = windowManager.FindWindowView<TestCancelableContent>(
                content => ReferenceEquals(content, expected)
            );

            Assert.AreSame(expected, result);
        }

        [Test]
        public void ForEachWindow_MixedContent_VisitsMatchingWindowsOnly()
        {
            UIWindowManager windowManager = CreateWindowManager();
            UIWindow expectedWindow = CreateWindow(windowManager, 1, modal: false, canFocus: true);
            UIWindow otherWindow = CreateWindow(windowManager, 2, modal: false, canFocus: true);
            TestCancelableContent expected =
                expectedWindow.gameObject.AddComponent<TestCancelableContent>();
            expectedWindow.SetContent(expected);
            otherWindow.SetContent(otherWindow.gameObject.AddComponent<TestContent>());
            int visitCount = 0;
            UIWindow visitedWindow = null;
            TestCancelableContent visitedContent = null;

            windowManager.ForEachWindow<TestCancelableContent>(
                (window, content) =>
                {
                    visitCount++;
                    visitedWindow = window;
                    visitedContent = content;
                }
            );

            Assert.AreEqual(1, visitCount);
            Assert.AreSame(expectedWindow, visitedWindow);
            Assert.AreSame(expected, visitedContent);
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

        /// <summary>
        /// Creates one authored test-window prefab beneath the fixture root.
        /// </summary>
        /// <typeparam name="TContent">The feature content component type.</typeparam>
        /// <returns>The created feature content component.</returns>
        private TContent CreateWindowPrefab<TContent>()
            where TContent : MonoBehaviour
        {
            GameObject prefabObject = new GameObject(
                "TestWindowPrefab",
                typeof(RectTransform),
                typeof(UIWindow)
            );
            prefabObject.transform.SetParent(_windowManagerObject.transform, false);
            return prefabObject.AddComponent<TContent>();
        }

        /// <summary>
        /// Requests one exclusive test window with standard fixture geometry.
        /// </summary>
        /// <typeparam name="TContent">The feature content component type.</typeparam>
        /// <param name="windowManager">The manager under test.</param>
        /// <param name="prefab">The authored test-window prefab.</param>
        /// <param name="matchesRequestedWindow">The optional same-request predicate.</param>
        /// <returns>The created window, or null when the request closed or was blocked.</returns>
        private static UIWindow ToggleExclusiveWindow<TContent>(
            UIWindowManager windowManager,
            TContent prefab,
            System.Predicate<TContent> matchesRequestedWindow
        )
            where TContent : MonoBehaviour
        {
            return windowManager.ToggleExclusiveWindow(
                prefab,
                windowManager.transform,
                "TestExclusiveWindow",
                0,
                0,
                new Vector2Int(100, 80),
                false,
                matchesRequestedWindow,
                out TContent _
            );
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

        private sealed class TestContent : MonoBehaviour { }

        private sealed class OtherTestContent : MonoBehaviour { }
    }
}
