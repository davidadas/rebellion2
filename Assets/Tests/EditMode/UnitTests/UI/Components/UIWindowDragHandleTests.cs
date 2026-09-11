using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class UIWindowDragHandleTests
    {
        private EventSystem _eventSystem;
        private UIWindowDragHandle _handle;
        private RectTransform _managerRect;
        private UIWindow _window;
        private GameObject _windowManagerObject;

        [TearDown]
        public void TearDown()
        {
            if (_windowManagerObject != null)
                Object.DestroyImmediate(_windowManagerObject);

            if (_eventSystem != null)
                Object.DestroyImmediate(_eventSystem.gameObject);
        }

        [Test]
        public void OnDrag_MovableWindow_PreviewsAndCommitsMoveOnRelease()
        {
            CreateWindow(canMove: true);
            PointerEventData eventData = CreatePointerData(60, 58);
            RectInt previewBounds = default;
            bool previewVisible = false;
            _window.MovePreviewChanged += (_, bounds) =>
            {
                previewBounds = bounds;
                previewVisible = true;
            };
            _window.MovePreviewEnded += _ => previewVisible = false;

            _handle.OnPointerDown(eventData);
            eventData.position = GetScreenPoint(100, 90);
            _handle.OnDrag(eventData);

            Assert.IsTrue(previewVisible);
            Assert.AreEqual(new RectInt(90, 82, 100, 80), previewBounds);
            Assert.AreEqual(50, _window.X);
            Assert.AreEqual(50, _window.Y);

            _handle.OnPointerUp(eventData);

            Assert.IsFalse(previewVisible);
            Assert.AreEqual(90, _window.X);
            Assert.AreEqual(82, _window.Y);
        }

        [Test]
        public void OnDrag_LockedWindow_DoesNotPreviewOrMoveWindow()
        {
            CreateWindow(canMove: false);
            PointerEventData eventData = CreatePointerData(60, 58);
            bool previewVisible = false;
            _window.MovePreviewChanged += (_, _) => previewVisible = true;

            _handle.OnPointerDown(eventData);
            eventData.position = GetScreenPoint(100, 90);
            _handle.OnDrag(eventData);
            _handle.OnPointerUp(eventData);

            Assert.IsFalse(previewVisible);
            Assert.AreEqual(50, _window.X);
            Assert.AreEqual(50, _window.Y);
        }

        private void CreateWindow(bool canMove)
        {
            _eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();
            _windowManagerObject = new GameObject(
                "WindowManager",
                typeof(RectTransform),
                typeof(UIWindowManager)
            );
            _managerRect = _windowManagerObject.GetComponent<RectTransform>();
            _managerRect.sizeDelta = new Vector2(640, 481);
            _managerRect.position = new Vector3(320, 240.5f, 0);
            UIWindowManager windowManager = _windowManagerObject.GetComponent<UIWindowManager>();

            GameObject windowObject = new GameObject(
                "Window",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(UIWindow)
            );
            windowObject.transform.SetParent(_windowManagerObject.transform, false);
            _window = windowObject.GetComponent<UIWindow>();
            _window.Configure(1, 50, 50, 100, 80, modal: false, canFocus: true, canMove);
            windowManager.Register(_window, behind: false);

            GameObject handleObject = new GameObject(
                "TitleImage",
                typeof(RectTransform),
                typeof(RawImage)
            );
            handleObject.transform.SetParent(windowObject.transform, false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 1f);
            handleRect.anchorMax = new Vector2(0f, 1f);
            handleRect.pivot = new Vector2(0f, 1f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(100, 16);
            handleObject.GetComponent<RawImage>().raycastTarget = true;
            _handle = handleObject.AddComponent<UIWindowDragHandle>();
            typeof(UIWindowDragHandle)
                .GetField("window", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_handle, _window);
        }

        private PointerEventData CreatePointerData(int x, int y)
        {
            return new PointerEventData(_eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = GetScreenPoint(x, y),
            };
        }

        private Vector2 GetScreenPoint(int x, int y)
        {
            Vector3 local = new Vector3(
                x - _managerRect.sizeDelta.x / 2f,
                _managerRect.sizeDelta.y / 2f - y
            );
            return RectTransformUtility.WorldToScreenPoint(
                null,
                _managerRect.TransformPoint(local)
            );
        }
    }
}
