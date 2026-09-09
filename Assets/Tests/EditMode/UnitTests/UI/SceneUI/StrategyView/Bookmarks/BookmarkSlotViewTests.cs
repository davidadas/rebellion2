using System;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Bookmarks
{
    [TestFixture]
    public class BookmarkSlotViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private GameObject _rootObject;
        private Texture2D _texture;
        private BookmarkSlotView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            BookmarkBarView bar = _rootObject.GetComponentInChildren<BookmarkBarView>(true);
            BookmarkSlotView template = GetField<BookmarkSlotView>(bar, "slotTemplate");
            _view = UnityEngine.Object.Instantiate(template, bar.transform);
            _view.name = "BookmarkSlotUnderTest";
            _texture = new Texture2D(12, 8);
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_AuthoredGeometry_AppliesSlotIconLabelAndInputBounds()
        {
            StrategyBookmarkLayout layout = CreateLayout();

            _view.Render(2, new BookmarkRenderData(true, "Coruscant", _texture), layout);

            Assert.AreEqual(2, _view.Index);
            Assert.AreEqual(new RectInt(700, 70, 120, 20), GetSourceRect(_view.transform));
            RawImage hitArea = GetField<RawImage>(_view, "hitAreaImage");
            Assert.AreEqual(new RectInt(0, 0, 120, 20), GetSourceRect(hitArea.transform));
            Assert.IsTrue(hitArea.enabled);
            Assert.IsTrue(hitArea.raycastTarget);
            RawImage icon = GetField<RawImage>(_view, "iconImage");
            Assert.AreSame(_texture, icon.texture);
            Assert.AreEqual(new RectInt(0, 6, 16, 8), GetSourceRect(icon.transform));
            TextMeshProUGUI label = GetField<TextMeshProUGUI>(_view, "labelTextField");
            Assert.AreEqual("Coruscant", label.text);
            Assert.AreEqual(Color.yellow, label.color);
            Assert.AreEqual(new RectInt(22, 0, 98, 20), GetSourceRect(label.transform));
            Assert.AreEqual(TextAlignmentOptions.MidlineLeft, label.alignment);
            Assert.AreEqual(TextWrappingModes.NoWrap, label.textWrappingMode);
            Assert.AreEqual(TextOverflowModes.Ellipsis, label.overflowMode);
            Assert.IsTrue(_view.gameObject.activeSelf);
        }

        [Test]
        public void Render_DerivedGeometry_UsesTextureDimensionsAndCentersIcon()
        {
            StrategyBookmarkLayout layout = CreateLayout();
            layout.IconWidth = 0;
            layout.IconHeight = 0;
            layout.LabelOffsetX = 0;

            _view.Render(0, new BookmarkRenderData(true, "Corellia", _texture), layout);

            int iconWidth = UILayout.GetTextureSourceWidth(_texture);
            int iconHeight = UILayout.GetTextureSourceHeight(_texture);
            Assert.AreEqual(
                new RectInt(0, (layout.ItemHeight - iconHeight) / 2, iconWidth, iconHeight),
                GetSourceRect(GetField<RawImage>(_view, "iconImage").transform)
            );
            Assert.AreEqual(
                new RectInt(iconWidth, 0, layout.Width - iconWidth, layout.ItemHeight),
                GetSourceRect(GetField<TextMeshProUGUI>(_view, "labelTextField").transform)
            );
        }

        [Test]
        public void OnPointerClick_LeftDoubleClick_RaisesViewEvent()
        {
            BookmarkSlotView requestedView = null;
            _view.DoubleClicked += view => requestedView = view;
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 2,
            };

            _view.OnPointerClick(eventData);

            Assert.AreSame(_view, requestedView);
        }

        [TestCase(PointerEventData.InputButton.Left, 1)]
        [TestCase(PointerEventData.InputButton.Right, 2)]
        [TestCase(PointerEventData.InputButton.Middle, 2)]
        public void OnPointerClick_NonActivationGesture_DoesNotRaiseEvent(
            PointerEventData.InputButton button,
            int clickCount
        )
        {
            int requestCount = 0;
            _view.DoubleClicked += _ => requestCount++;
            PointerEventData eventData = new PointerEventData(null)
            {
                button = button,
                clickCount = clickCount,
            };

            _view.OnPointerClick(eventData);

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

        private static T GetField<T>(object instance, string fieldName)
        {
            return (T)
                instance
                    .GetType()
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(instance);
        }

        private static RectInt GetSourceRect(Transform transform)
        {
            return UILayout.GetSourceRect(transform as RectTransform);
        }
    }
}
