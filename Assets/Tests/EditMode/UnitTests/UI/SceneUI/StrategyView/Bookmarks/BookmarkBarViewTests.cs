using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Bookmarks
{
    [TestFixture]
    public class BookmarkBarViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private Texture2D _texture;
        private BookmarkBarView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<BookmarkBarView>(true);
            _texture = new Texture2D(12, 8);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_NullLayout_ThrowsMissingReferenceException()
        {
            Assert.Throws<MissingReferenceException>(() => _view.Render(null, null));
        }

        [Test]
        public void Render_ActiveAndInactiveSlots_CreatesOnlyRequiredVisibleViews()
        {
            BookmarkRenderData[] data =
            {
                new BookmarkRenderData(true, "Coruscant", _texture),
                new BookmarkRenderData(false, string.Empty, null),
                new BookmarkRenderData(true, "Corellia", _texture),
            };

            _view.Render(data, CreateLayout());

            List<BookmarkSlotView> slots = GetSlots();
            Assert.AreEqual(3, slots.Count);
            Assert.IsTrue(slots[0].gameObject.activeSelf);
            Assert.IsFalse(slots[1].gameObject.activeSelf);
            Assert.IsTrue(slots[2].gameObject.activeSelf);
            Assert.AreEqual("BookmarkSlot0", slots[0].name);
            Assert.AreEqual("BookmarkSlot1", slots[1].name);
            Assert.AreEqual("BookmarkSlot2", slots[2].name);
            Assert.AreEqual(0, slots[0].Index);
            Assert.AreEqual(2, slots[2].Index);
            Assert.IsTrue(_view.gameObject.activeSelf);
        }

        [Test]
        public void Render_ShorterSnapshot_ReusesAndHidesSurplusViews()
        {
            _view.Render(
                new[]
                {
                    new BookmarkRenderData(true, "Coruscant", _texture),
                    new BookmarkRenderData(true, "Corellia", _texture),
                },
                CreateLayout()
            );
            List<BookmarkSlotView> originalSlots = new List<BookmarkSlotView>(GetSlots());

            _view.Render(
                new[] { new BookmarkRenderData(true, "Kessel", _texture) },
                CreateLayout()
            );

            List<BookmarkSlotView> slots = GetSlots();
            Assert.AreEqual(2, slots.Count);
            Assert.AreSame(originalSlots[0], slots[0]);
            Assert.AreSame(originalSlots[1], slots[1]);
            Assert.IsTrue(slots[0].gameObject.activeSelf);
            Assert.IsFalse(slots[1].gameObject.activeSelf);
        }

        [Test]
        public void Render_NullSnapshot_HidesExistingViews()
        {
            _view.Render(
                new[] { new BookmarkRenderData(true, "Coruscant", _texture) },
                CreateLayout()
            );

            _view.Render(null, CreateLayout());

            Assert.IsFalse(GetSlots()[0].gameObject.activeSelf);
        }

        [Test]
        public void SlotDoubleClick_ActiveSlot_RaisesBookmarkIndex()
        {
            _view.Render(
                new[]
                {
                    new BookmarkRenderData(true, "Coruscant", _texture),
                    new BookmarkRenderData(true, "Corellia", _texture),
                },
                CreateLayout()
            );
            int requestedIndex = -1;
            _view.BookmarkRequested += index => requestedIndex = index;
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };

            GetSlots()[1].OnPointerClick(eventData);

            Assert.AreEqual(1, requestedIndex);
        }

        [Test]
        public void OnDestroy_RenderedSlots_DetachesBookmarkRequests()
        {
            _view.Render(
                new[] { new BookmarkRenderData(true, "Coruscant", _texture) },
                CreateLayout()
            );
            int requestCount = 0;
            _view.BookmarkRequested += _ => requestCount++;
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            GetSlots()[0].OnPointerClick(eventData);

            Assert.AreEqual(0, requestCount);
        }

        private static StrategyBookmarkLayout CreateLayout()
        {
            return new StrategyBookmarkLayout
            {
                StartX = 700,
                StartY = 30,
                Width = 120,
                ListHeight = 200,
                ItemHeight = 20,
                IconWidth = 16,
                IconHeight = 8,
                LabelOffsetX = 22,
            };
        }

        private List<BookmarkSlotView> GetSlots()
        {
            return GetField<List<BookmarkSlotView>>(_view, "slotViews");
        }

        private static T GetField<T>(object instance, string fieldName)
        {
            return (T)
                instance
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(instance);
        }
    }
}
