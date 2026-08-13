using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class ScrollAreaViewTests
    {
        private RectTransform _contentRoot;
        private ScrollAreaDragRelay _dragRelay;
        private GameObject _rootObject;
        private ScrollAreaView _scrollArea;
        private Scrollbar _scrollbar;
        private Button _scrollDownButton;
        private Button _scrollUpButton;
        private RectTransform _slidingAreaRoot;
        private RectTransform _trackBackgroundRoot;
        private RectTransform _viewportRoot;

        [SetUp]
        public void SetUp()
        {
            _rootObject = new GameObject(
                "ScrollArea",
                typeof(RectTransform),
                typeof(ScrollRect),
                typeof(ScrollAreaView)
            );
            _rootObject.SetActive(false);
            _scrollArea = _rootObject.GetComponent<ScrollAreaView>();
            ScrollRect scrollRect = _rootObject.GetComponent<ScrollRect>();
            _viewportRoot = CreateRect("Viewport", _rootObject.transform, 100, 50);
            _contentRoot = CreateRect("Content", _viewportRoot, 100, 50);
            scrollRect.viewport = _viewportRoot;
            scrollRect.content = _contentRoot;
            GameObject scrollbarObject = new GameObject(
                "Scrollbar",
                typeof(RectTransform),
                typeof(Scrollbar)
            );
            scrollbarObject.transform.SetParent(_rootObject.transform, false);
            _scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            _trackBackgroundRoot = CreateRect("Track", scrollbarObject.transform, 10, 30);
            _slidingAreaRoot = CreateRect("SlidingArea", scrollbarObject.transform, 10, 30);
            _scrollUpButton = CreateButton("ScrollUpButton", scrollbarObject.transform, 10, 8);
            _scrollDownButton = CreateButton("ScrollDownButton", scrollbarObject.transform, 10, 12);
            _dragRelay = _viewportRoot.gameObject.AddComponent<ScrollAreaDragRelay>();
            SetField("scrollRect", scrollRect);
            SetField("contentRoot", _contentRoot);
            SetField("scrollbar", _scrollbar);
            SetField("trackBackgroundRoot", _trackBackgroundRoot);
            SetField("slidingAreaRoot", _slidingAreaRoot);
            SetField("scrollUpButton", _scrollUpButton);
            SetField("scrollDownButton", _scrollDownButton);
            SetField("dragRelay", _dragRelay);
            _rootObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Properties_ConfiguredHierarchy_ReturnAuthoredGeometry()
        {
            Assert.AreSame(_contentRoot, _scrollArea.ContentRoot);
            Assert.AreSame(_viewportRoot, _scrollArea.ViewportRoot);
            Assert.AreSame(_rootObject.transform, _scrollArea.ScrollRoot);
            Assert.AreEqual(100f, _scrollArea.ViewportWidth);
            Assert.AreEqual(50f, _scrollArea.ViewportHeight);
        }

        [Test]
        public void Properties_MissingRequiredReference_ThrowsMissingReferenceException()
        {
            SetField("contentRoot", null);

            Assert.Throws<MissingReferenceException>(() => _ = _scrollArea.ContentRoot);
        }

        [Test]
        public void SetLayout_Bounds_AppliesViewportScrollbarAndChildGeometry()
        {
            _scrollArea.SetLayout(
                new Vector2(1, 2),
                new Vector2(120, 60),
                new Vector2(130, 4),
                new Vector2(14, 100)
            );

            Assert.AreEqual(
                new RectInt(1, 2, 120, 60),
                UILayout.GetSourceRect(_rootObject.transform as RectTransform)
            );
            Assert.AreEqual(new RectInt(0, 0, 120, 60), UILayout.GetSourceRect(_viewportRoot));
            Assert.AreEqual(
                new RectInt(130, 4, 14, 100),
                UILayout.GetSourceRect(_scrollbar.transform as RectTransform)
            );
            Assert.AreEqual(
                new RectInt(0, 0, 14, 8),
                UILayout.GetSourceRect(_scrollUpButton.transform as RectTransform)
            );
            Assert.AreEqual(
                new RectInt(0, 88, 14, 12),
                UILayout.GetSourceRect(_scrollDownButton.transform as RectTransform)
            );
            Assert.AreEqual(
                new RectInt(0, 8, 14, 80),
                UILayout.GetSourceRect(_trackBackgroundRoot)
            );
            Assert.AreEqual(new RectInt(0, 8, 14, 80), UILayout.GetSourceRect(_slidingAreaRoot));
        }

        [Test]
        public void SetContentHeight_ContentFitsViewport_HidesScrollControlsAndResetsOffset()
        {
            _contentRoot.anchoredPosition = new Vector2(0, 20);

            _scrollArea.SetContentHeight(20, 5, false);

            Assert.AreEqual(50f, _contentRoot.sizeDelta.y);
            Assert.AreEqual(Vector2.zero, _contentRoot.anchoredPosition);
            Assert.AreEqual(1f, _scrollbar.value);
            Assert.IsFalse(_scrollbar.gameObject.activeSelf);
            Assert.IsFalse(_scrollUpButton.gameObject.activeSelf);
            Assert.IsFalse(_scrollDownButton.gameObject.activeSelf);
        }

        [Test]
        public void SetContentHeight_ContentOverflowsViewport_ShowsControlsAndSizesScrollbar()
        {
            _scrollArea.SetContentHeight(200, 10, true);

            Assert.AreEqual(200f, _contentRoot.sizeDelta.y);
            Assert.AreEqual(0.25f, _scrollbar.size);
            Assert.AreEqual(1f, _scrollbar.value);
            Assert.IsTrue(_scrollbar.gameObject.activeSelf);
            Assert.IsTrue(_scrollUpButton.gameObject.activeSelf);
            Assert.IsTrue(_scrollDownButton.gameObject.activeSelf);
        }

        [Test]
        public void RelayScroll_ContentOverflows_MovesByConfiguredStep()
        {
            _scrollArea.SetContentHeight(200, 10, true);
            PointerEventData eventData = new PointerEventData(null)
            {
                scrollDelta = new Vector2(0, -1),
            };

            _scrollArea.RelayScroll(eventData);

            Assert.AreEqual(10f, _contentRoot.anchoredPosition.y, 0.01f);
            Assert.AreEqual(1f - 10f / 150f, _scrollbar.value, 0.01f);
        }

        [Test]
        public void RelayScroll_NullEventData_PreservesPosition()
        {
            _scrollArea.SetContentHeight(200, 10, true);

            _scrollArea.RelayScroll(null);

            Assert.AreEqual(0f, _contentRoot.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void RevealContentRect_ContentBelowViewport_ScrollsMinimumRequiredDistance()
        {
            _scrollArea.SetContentHeight(200, 10, true);

            _scrollArea.RevealContentRect(100, 20);

            Assert.AreEqual(70f, _contentRoot.anchoredPosition.y, 0.01f);
            Assert.AreEqual(1f - 70f / 150f, _scrollbar.value, 0.01f);
        }

        [Test]
        public void RevealContentRect_AlreadyVisibleContent_PreservesPosition()
        {
            _scrollArea.SetContentHeight(200, 10, true);

            _scrollArea.RevealContentRect(10, 20);

            Assert.AreEqual(0f, _contentRoot.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void RevealContentRect_ContentFitsViewport_PreservesPosition()
        {
            _scrollArea.SetContentHeight(40, 10, true);

            _scrollArea.RevealContentRect(30, 10);

            Assert.AreEqual(0f, _contentRoot.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void RelayDragEvents_SubscribedHandlers_ReceivePointerEvents()
        {
            PointerEventData eventData = new PointerEventData(null);
            PointerEventData dragged = null;
            PointerEventData dragEnded = null;
            PointerEventData dropped = null;
            _scrollArea.Dragged += value => dragged = value;
            _scrollArea.DragEnded += value => dragEnded = value;
            _scrollArea.Dropped += value => dropped = value;

            _scrollArea.RelayDrag(eventData);
            _scrollArea.RelayDragEnd(eventData);
            _scrollArea.RelayDrop(eventData);

            Assert.AreSame(eventData, dragged);
            Assert.AreSame(eventData, dragEnded);
            Assert.AreSame(eventData, dropped);
        }

        [Test]
        public void DragRelay_InitializedOwner_ForwardsGestureLifecycle()
        {
            PointerEventData eventData = new PointerEventData(null)
            {
                scrollDelta = new Vector2(0, -1),
            };
            int dragCount = 0;
            int endCount = 0;
            int dropCount = 0;
            _scrollArea.Dragged += _ => dragCount++;
            _scrollArea.DragEnded += _ => endCount++;
            _scrollArea.Dropped += _ => dropCount++;
            _scrollArea.SetContentHeight(200, 10, true);
            _dragRelay.Initialize(_scrollArea);

            _dragRelay.OnInitializePotentialDrag(eventData);
            _dragRelay.OnBeginDrag(eventData);
            _dragRelay.OnDrag(eventData);
            _dragRelay.OnEndDrag(eventData);
            _dragRelay.OnDrop(eventData);
            _dragRelay.OnScroll(eventData);

            Assert.AreEqual(2, dragCount);
            Assert.AreEqual(1, endCount);
            Assert.AreEqual(1, dropCount);
            Assert.AreEqual(10f, _contentRoot.anchoredPosition.y, 0.01f);
        }

        [Test]
        public void DragRelay_ClearedOwner_DoesNotForwardGestures()
        {
            int dragCount = 0;
            _scrollArea.Dragged += _ => dragCount++;
            _dragRelay.Initialize(_scrollArea);

            _dragRelay.Clear(_scrollArea);
            _dragRelay.OnDrag(new PointerEventData(null));

            Assert.AreEqual(0, dragCount);
        }

        private static RectTransform CreateRect(
            string objectName,
            Transform parent,
            int width,
            int height
        )
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Button CreateButton(
            string objectName,
            Transform parent,
            int width,
            int height
        )
        {
            GameObject child = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            child.transform.SetParent(parent, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            return child.GetComponent<Button>();
        }

        private void SetField(string fieldName, object value)
        {
            typeof(ScrollAreaView)
                .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_scrollArea, value);
        }
    }
}
