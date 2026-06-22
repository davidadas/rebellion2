using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.StrategyView
{
    [TestFixture]
    public sealed class UIWindowDragHandleTests
    {
        private EventSystem eventSystem;
        private GameObject windowManagerObject;

        [TearDown]
        public void TearDown()
        {
            if (windowManagerObject != null)
                Object.DestroyImmediate(windowManagerObject);

            if (eventSystem != null)
                Object.DestroyImmediate(eventSystem.gameObject);
        }

        [Test]
        public void DragHandle_PreviewsUnlockedWindowMoveAndCommitsOnRelease()
        {
            TestWindow testWindow = CreateWindow(canMove: true);
            PointerEventData eventData = CreatePointerData(testWindow.WindowManagerRect, 60, 58);
            RectInt previewBounds = default;
            bool previewVisible = false;
            testWindow.Window.MovePreviewChanged += (_, bounds) =>
            {
                previewBounds = bounds;
                previewVisible = true;
            };
            testWindow.Window.MovePreviewEnded += _ => previewVisible = false;

            testWindow.Handle.OnPointerDown(eventData);
            eventData.position = GetScreenPoint(testWindow.WindowManagerRect, 100, 90);
            testWindow.Handle.OnDrag(eventData);

            Assert.IsTrue(previewVisible);
            Assert.AreEqual(new RectInt(90, 82, 100, 80), previewBounds);
            Assert.AreEqual(50, testWindow.Window.X);
            Assert.AreEqual(50, testWindow.Window.Y);

            testWindow.Handle.OnPointerUp(eventData);

            Assert.IsFalse(previewVisible);
            Assert.AreEqual(90, testWindow.Window.X);
            Assert.AreEqual(82, testWindow.Window.Y);
        }

        [Test]
        public void DragHandle_DoesNotMoveLockedWindow()
        {
            TestWindow testWindow = CreateWindow(canMove: false);
            PointerEventData eventData = CreatePointerData(testWindow.WindowManagerRect, 60, 58);
            bool previewVisible = false;
            testWindow.Window.MovePreviewChanged += (_, _) => previewVisible = true;

            testWindow.Handle.OnPointerDown(eventData);
            eventData.position = GetScreenPoint(testWindow.WindowManagerRect, 100, 90);
            testWindow.Handle.OnDrag(eventData);
            testWindow.Handle.OnPointerUp(eventData);

            Assert.IsFalse(previewVisible);
            Assert.AreEqual(50, testWindow.Window.X);
            Assert.AreEqual(50, testWindow.Window.Y);
        }

        [Test]
        public void WindowBody_HasNoDragHandler()
        {
            TestWindow testWindow = CreateWindow(canMove: true);

            Assert.IsNull(testWindow.Body.GetComponent<IDragHandler>());
        }

        private TestWindow CreateWindow(bool canMove)
        {
            eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();

            windowManagerObject = new GameObject(
                "WindowManager",
                typeof(RectTransform),
                typeof(UIWindowManager)
            );
            RectTransform windowManagerRect = windowManagerObject.GetComponent<RectTransform>();
            windowManagerRect.sizeDelta = new Vector2(640, 481);
            windowManagerRect.position = new Vector3(320, 240.5f, 0);
            UIWindowManager windowManager = windowManagerObject.GetComponent<UIWindowManager>();

            GameObject windowObject = new GameObject(
                "Window",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(UIWindow)
            );
            windowObject.transform.SetParent(windowManagerObject.transform, false);
            RawImage body = windowObject.GetComponent<RawImage>();
            body.raycastTarget = true;
            UIWindow window = windowObject.GetComponent<UIWindow>();
            window.Configure(1, 50, 50, 100, 80, modal: false, canFocus: true, canMove);
            windowManager.Register(window, behind: false);

            GameObject handleObject = new GameObject(
                "TitleImage",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(UIWindowDragHandle)
            );
            handleObject.transform.SetParent(windowObject.transform, false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 1f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.pivot = new Vector2(0f, 1f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(100, 16);
            handleObject.GetComponent<RawImage>().raycastTarget = true;

            return new TestWindow(
                windowManagerRect,
                window,
                body,
                handleObject.GetComponent<UIWindowDragHandle>()
            );
        }

        private PointerEventData CreatePointerData(RectTransform windowManagerRect, int x, int y)
        {
            return new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = GetScreenPoint(windowManagerRect, x, y),
            };
        }

        private static Vector2 GetScreenPoint(RectTransform windowManagerRect, int x, int y)
        {
            Vector3 local = new Vector3(
                x - windowManagerRect.sizeDelta.x / 2f,
                windowManagerRect.sizeDelta.y / 2f - y
            );
            return RectTransformUtility.WorldToScreenPoint(
                null,
                windowManagerRect.TransformPoint(local)
            );
        }

        private readonly struct TestWindow
        {
            public TestWindow(
                RectTransform windowManagerRect,
                UIWindow window,
                RawImage body,
                UIWindowDragHandle handle
            )
            {
                WindowManagerRect = windowManagerRect;
                Window = window;
                Body = body;
                Handle = handle;
            }

            public RectTransform WindowManagerRect { get; }
            public UIWindow Window { get; }
            public RawImage Body { get; }
            public UIWindowDragHandle Handle { get; }
        }
    }
}
