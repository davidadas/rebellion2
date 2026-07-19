using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Overlay
{
    [TestFixture]
    public class StrategyOverlayViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private bool _cursorWasVisible;
        private Texture2D _itemTexture;
        private GameObject _rootObject;
        private StrategyOverlayView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<StrategyOverlayView>(true);
            _itemTexture = new Texture2D(24, 18);
            _cursorWasVisible = Cursor.visible;
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UIComponentTestHelper.InvokeLifecycle(_view, "OnDisable");
            Cursor.visible = _cursorWasVisible;
            UnityEngine.Object.DestroyImmediate(_itemTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_NullPresentation_HidesDragFeedback()
        {
            _view.Render(
                new StrategyOverlayRenderData(
                    new RectInt(10, 20, 100, 80),
                    _itemTexture,
                    new RectInt(30, 40, 24, 18)
                )
            );

            _view.Render(null);

            Assert.IsFalse(GetField<Image>("dragFrameTopImage").gameObject.activeSelf);
            Assert.IsFalse(GetField<Image>("dragFrameBottomImage").gameObject.activeSelf);
            Assert.IsFalse(GetField<Image>("dragFrameLeftImage").gameObject.activeSelf);
            Assert.IsFalse(GetField<Image>("dragFrameRightImage").gameObject.activeSelf);
            Assert.IsFalse(GetField<RawImage>("destinationCursorImage").gameObject.activeSelf);
        }

        [Test]
        public void Render_FrameAndImage_AppliesEveryOverlayElement()
        {
            RectInt frameBounds = new RectInt(10, 20, 100, 80);
            RectInt imageBounds = new RectInt(30, 40, 24, 18);

            _view.Render(new StrategyOverlayRenderData(frameBounds, _itemTexture, imageBounds));

            Image top = GetField<Image>("dragFrameTopImage");
            Assert.AreEqual(new RectInt(10, 20, 100, 1), GetSourceRect(top.transform));
            Assert.AreEqual(Color.white, top.color);
            Assert.IsFalse(top.raycastTarget);
            Assert.IsTrue(top.gameObject.activeSelf);

            Image bottom = GetField<Image>("dragFrameBottomImage");
            Assert.AreEqual(new RectInt(10, 99, 100, 1), GetSourceRect(bottom.transform));
            Assert.AreEqual(Color.white, bottom.color);
            Assert.IsFalse(bottom.raycastTarget);
            Assert.IsTrue(bottom.gameObject.activeSelf);

            Image left = GetField<Image>("dragFrameLeftImage");
            Assert.AreEqual(new RectInt(10, 20, 1, 80), GetSourceRect(left.transform));
            Assert.AreEqual(Color.white, left.color);
            Assert.IsFalse(left.raycastTarget);
            Assert.IsTrue(left.gameObject.activeSelf);

            Image right = GetField<Image>("dragFrameRightImage");
            Assert.AreEqual(new RectInt(109, 20, 1, 80), GetSourceRect(right.transform));
            Assert.AreEqual(Color.white, right.color);
            Assert.IsFalse(right.raycastTarget);
            Assert.IsTrue(right.gameObject.activeSelf);
            RawImage image = GetField<RawImage>("destinationCursorImage");
            Assert.AreSame(_itemTexture, image.texture);
            Assert.AreEqual(imageBounds, GetSourceRect(image.transform));
            Assert.IsTrue(image.enabled);
            Assert.IsTrue(image.gameObject.activeSelf);
            Assert.IsFalse(image.raycastTarget);
        }

        [Test]
        public void Render_FrameWithoutImage_HidesSharedImageWhenTargetingInactive()
        {
            _view.Render(new StrategyOverlayRenderData(new RectInt(10, 20, 100, 80), null, null));

            Assert.IsTrue(GetField<Image>("dragFrameTopImage").gameObject.activeSelf);
            Assert.IsFalse(GetField<RawImage>("destinationCursorImage").gameObject.activeSelf);
        }

        [Test]
        public void Show_TargetPosition_DisplaysGeneratedCursorAndOwnsCancellationSelection()
        {
            _view.Show(250, 180);

            RawImage cursor = GetField<RawImage>("destinationCursorImage");
            int size = GetField<int>("destinationCursorSize");
            Assert.IsNotNull(cursor.texture);
            Assert.AreEqual(FilterMode.Point, cursor.texture.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, cursor.texture.wrapMode);
            Assert.AreEqual(
                new RectInt(250 - size / 2, 180 - size / 2, size, size),
                GetSourceRect(cursor.transform)
            );
            Assert.IsTrue(cursor.gameObject.activeSelf);
            Assert.IsTrue(cursor.enabled);
            Assert.IsFalse(cursor.raycastTarget);
            Assert.IsFalse(Cursor.visible);
            RawImage input = GetField<RawImage>("targetingInputImage");
            Assert.IsFalse(input.enabled);
            Assert.IsFalse(input.raycastTarget);
        }

        [Test]
        public void MoveTo_VisibleTarget_RepositionsExistingCursorTexture()
        {
            _view.Show(250, 180);
            RawImage cursor = GetField<RawImage>("destinationCursorImage");
            Texture originalTexture = cursor.texture;
            int size = GetField<int>("destinationCursorSize");

            _view.MoveTo(400, 220);

            Assert.AreSame(originalTexture, cursor.texture);
            Assert.AreEqual(
                new RectInt(400 - size / 2, 220 - size / 2, size, size),
                GetSourceRect(cursor.transform)
            );
        }

        [Test]
        public void MoveTo_HiddenTarget_DoesNotShowCursor()
        {
            _view.MoveTo(400, 220);

            Assert.IsFalse(GetField<RawImage>("destinationCursorImage").gameObject.activeSelf);
        }

        [Test]
        public void Hide_VisibleTarget_HidesCursorRestoresPlatformCursorAndClearsSelection()
        {
            Cursor.visible = true;
            _view.Show(250, 180);

            _view.Hide();

            Assert.IsFalse(GetField<RawImage>("destinationCursorImage").gameObject.activeSelf);
            Assert.IsTrue(Cursor.visible);
        }

        [Test]
        public void OnCancel_VisibleTarget_RaisesCancellationRequest()
        {
            int requestCount = 0;
            _view.TargetingCancelRequested += () => requestCount++;
            _view.OnCancel(new BaseEventData(null));

            _view.Show(250, 180);
            _view.OnCancel(new BaseEventData(null));

            Assert.AreEqual(1, requestCount);
        }

        [Test]
        public void OnDisable_VisibleTarget_ReleasesTransientState()
        {
            Cursor.visible = true;
            _view.Show(250, 180);

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDisable");

            Assert.IsFalse(GetField<RawImage>("destinationCursorImage").gameObject.activeSelf);
            Assert.IsTrue(Cursor.visible);
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(StrategyOverlayView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        private static RectInt GetSourceRect(Transform transform)
        {
            return UILayout.GetSourceRect(transform as RectTransform);
        }
    }
}
