using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.GalaxyMap
{
    [TestFixture]
    public class GalacticInformationLegendViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";
        private const int _sourceDesktopHeight = 480;
        private const int _sourceDesktopWidth = 853;

        private Texture2D _closePressedTexture;
        private Texture2D _closeTexture;
        private Texture2D _frameTexture;
        private Texture2D _legendTexture;
        private GameObject _rootObject;
        private GalacticInformationLegendView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<GalacticInformationLegendView>(true);
            _legendTexture = new Texture2D(180, 135);
            _closeTexture = new Texture2D(36, 36);
            _closePressedTexture = new Texture2D(36, 36);
            _frameTexture = new Texture2D(18, 18);
            RectTransform parent = (_view.transform as RectTransform).parent as RectTransform;
            parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _sourceDesktopWidth);
            parent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _sourceDesktopHeight);
            UIComponentTestHelper.InvokeLifecycle(
                _view.GetComponentInChildren<GalacticInformationFrameView>(true),
                "Awake"
            );
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_frameTexture);
            UnityEngine.Object.DestroyImmediate(_closePressedTexture);
            UnityEngine.Object.DestroyImmediate(_closeTexture);
            UnityEngine.Object.DestroyImmediate(_legendTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_Legend_AppliesBoundsArtworkFrameAndCloseControl()
        {
            GalacticInformationLegendRenderData data = CreateLegend(new Vector2Int(150, 120));

            _view.Render(data);

            Assert.IsTrue(_view.gameObject.activeSelf);
            Assert.AreEqual(
                new RectInt(150, 120, 180, 135),
                UILayout.GetSourceRect(_view.transform as RectTransform)
            );
            RawImage legend = FindComponent<RawImage>("LegendImage");
            Assert.AreSame(_legendTexture, legend.texture);
            Assert.IsTrue(legend.enabled);
            Assert.IsTrue(legend.raycastTarget);
            Assert.AreEqual(
                new RectInt(0, 0, 180, 135),
                UILayout.GetSourceRect(legend.rectTransform)
            );
            RawImage close = FindComponent<RawImage>("CloseImage");
            Assert.AreSame(_closeTexture, close.texture);
            Assert.AreEqual(
                new RectInt(160, 8, 12, 12),
                UILayout.GetSourceRect(close.rectTransform)
            );
            Assert.AreEqual(
                new RectInt(160, 8, 12, 12),
                UILayout.GetSourceRect(
                    FindComponent<UIRaycastArea>("CloseHitArea").transform as RectTransform
                )
            );
        }

        [Test]
        public void Render_SubsequentBounds_PreservesInitialSourcePosition()
        {
            _view.Render(CreateLegend(new Vector2Int(150, 120)));

            _view.Render(CreateLegend(new Vector2Int(300, 250)));

            RectInt bounds = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(150, bounds.x);
            Assert.AreEqual(120, bounds.y);
            Assert.AreEqual(180, bounds.width);
            Assert.AreEqual(135, bounds.height);
        }

        [Test]
        public void Render_InitialPositionOutsideParent_ClampsToParentBounds()
        {
            RectTransform parent = (_view.transform as RectTransform).parent as RectTransform;
            int expectedX = Mathf.Max(0, Mathf.RoundToInt(parent.rect.width) - 180);
            int expectedY = Mathf.Max(0, Mathf.RoundToInt(parent.rect.height) - 135);

            _view.Render(CreateLegend(new Vector2Int(999, 999)));

            RectInt bounds = UILayout.GetSourceRect(_view.transform as RectTransform);
            Assert.AreEqual(expectedX, bounds.x);
            Assert.AreEqual(expectedY, bounds.y);
        }

        [Test]
        public void Render_MissingLegendTexture_HidesLegend()
        {
            GalacticInformationLegendRenderData data = new GalacticInformationLegendRenderData(
                new RectInt(150, 120, 180, 135),
                null,
                CreateFrame(),
                new RectInt(160, 8, 12, 12),
                _closeTexture,
                _closePressedTexture
            );

            _view.Render(data);

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void Render_NullData_HidesVisibleLegend()
        {
            _view.Render(CreateLegend(new Vector2Int(150, 120)));

            _view.Render(null);

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void CloseHitArea_PressReleaseAndClick_UpdatesTextureAndRaisesRequest()
        {
            int closeCount = 0;
            _view.CloseRequested += () => closeCount++;
            _view.Render(CreateLegend(new Vector2Int(150, 120)));
            UIRaycastArea hitArea = FindComponent<UIRaycastArea>("CloseHitArea");
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            hitArea.OnPointerDown(eventData);
            Assert.AreSame(_closePressedTexture, FindComponent<RawImage>("CloseImage").texture);
            hitArea.OnPointerUp(eventData);
            Assert.AreSame(_closeTexture, FindComponent<RawImage>("CloseImage").texture);
            hitArea.OnPointerClick(eventData);

            Assert.AreEqual(1, closeCount);
        }

        [Test]
        public void Hide_VisibleLegend_DeactivatesLegend()
        {
            _view.Render(CreateLegend(new Vector2Int(150, 120)));

            _view.Hide();

            Assert.IsFalse(_view.gameObject.activeSelf);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsCloseAndRaisesDestroyedEvent()
        {
            GalacticInformationLegendView destroyed = null;
            int closeCount = 0;
            _view.Destroyed += view => destroyed = view;
            _view.CloseRequested += () => closeCount++;
            _view.Render(CreateLegend(new Vector2Int(150, 120)));
            UIRaycastArea hitArea = FindComponent<UIRaycastArea>("CloseHitArea");
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            hitArea.OnPointerClick(eventData);

            Assert.AreSame(_view, destroyed);
            Assert.AreEqual(0, closeCount);
        }

        private GalacticInformationLegendRenderData CreateLegend(Vector2Int position)
        {
            return new GalacticInformationLegendRenderData(
                new RectInt(position.x, position.y, 180, 135),
                _legendTexture,
                CreateFrame(),
                new RectInt(160, 8, 12, 12),
                _closeTexture,
                _closePressedTexture
            );
        }

        private GalacticInformationFrameRenderData CreateFrame()
        {
            return new GalacticInformationFrameRenderData(
                180,
                135,
                Enumerable.Repeat(_frameTexture, 8).ToArray()
            );
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _view
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }
    }
}
