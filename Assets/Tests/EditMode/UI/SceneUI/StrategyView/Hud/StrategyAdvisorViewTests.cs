using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Hud
{
    [TestFixture]
    public class StrategyAdvisorViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/StrategyViewRoot.prefab";

        private Texture2D _droidIdleTexture;
        private Texture2D _droidPlaybackTexture;
        private Texture2D _protocolFirstTexture;
        private Texture2D _protocolIdleTexture;
        private Texture2D _protocolSecondTexture;
        private GameObject _rootObject;
        private StrategyAdvisorView _view;

        [SetUp]
        public void SetUp()
        {
            _rootObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _rootObject.GetComponentInChildren<StrategyAdvisorView>(true);
            _protocolIdleTexture = new Texture2D(20, 30);
            _droidIdleTexture = new Texture2D(20, 30);
            _protocolFirstTexture = new Texture2D(20, 30);
            _protocolSecondTexture = new Texture2D(20, 30);
            _droidPlaybackTexture = new Texture2D(20, 30);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_droidPlaybackTexture);
            UnityEngine.Object.DestroyImmediate(_protocolSecondTexture);
            UnityEngine.Object.DestroyImmediate(_protocolFirstTexture);
            UnityEngine.Object.DestroyImmediate(_droidIdleTexture);
            UnityEngine.Object.DestroyImmediate(_protocolIdleTexture);
            UnityEngine.Object.DestroyImmediate(_rootObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_VisiblePresentation_AppliesIdleFramesAndInputBounds()
        {
            StrategyAdvisorViewData data = CreatePresentation(true);

            _view.Render(data);

            RawImage protocolImage = GetField<RawImage>("protocolImage");
            RawImage droidImage = GetField<RawImage>("droidImage");
            UIRaycastArea protocolInput = GetField<UIRaycastArea>("protocolInput");
            UIRaycastArea droidInput = GetField<UIRaycastArea>("droidInput");
            Assert.AreSame(_protocolIdleTexture, protocolImage.texture);
            Assert.AreSame(_droidIdleTexture, droidImage.texture);
            Assert.IsTrue(protocolImage.enabled);
            Assert.IsTrue(droidImage.enabled);
            Assert.IsFalse(protocolImage.raycastTarget);
            Assert.IsFalse(droidImage.raycastTarget);
            Assert.AreEqual(new RectInt(100, 300, 40, 60), GetSourceRect(protocolImage.transform));
            Assert.AreEqual(new RectInt(700, 310, 45, 55), GetSourceRect(droidImage.transform));
            Assert.AreEqual(new RectInt(100, 300, 40, 60), GetSourceRect(protocolInput.transform));
            Assert.AreEqual(new RectInt(700, 310, 45, 55), GetSourceRect(droidInput.transform));
            Assert.IsTrue(protocolInput.gameObject.activeSelf);
            Assert.IsTrue(droidInput.gameObject.activeSelf);
        }

        [Test]
        public void Render_HiddenPresentation_HidesFramesAndInputs()
        {
            _view.Render(CreatePresentation(true));

            _view.Render(CreatePresentation(false));

            Assert.IsFalse(GetField<RawImage>("protocolImage").enabled);
            Assert.IsFalse(GetField<RawImage>("droidImage").enabled);
            Assert.IsFalse(GetField<UIRaycastArea>("protocolInput").gameObject.activeSelf);
            Assert.IsFalse(GetField<UIRaycastArea>("droidInput").gameObject.activeSelf);
        }

        [Test]
        public void Render_DuringPlayback_ClearsQueueAndAppliesNewIdlePresentation()
        {
            _view.Render(CreatePresentation(true));
            _view.EnqueuePlaybacks(
                new[]
                {
                    new StrategyAdvisorAnimationViewData(
                        new[] { _protocolFirstTexture, _protocolSecondTexture },
                        false,
                        null
                    ),
                }
            );
            Texture2D replacementProtocol = new Texture2D(20, 30);
            Texture2D replacementDroid = new Texture2D(20, 30);
            StrategyAdvisorViewData replacement = new StrategyAdvisorViewData(
                true,
                replacementProtocol,
                replacementDroid,
                null,
                null,
                0.5f
            );

            _view.Render(replacement);
            _view.AdvanceAnimation(10f);

            Assert.AreSame(replacementProtocol, GetField<RawImage>("protocolImage").texture);
            Assert.AreSame(replacementDroid, GetField<RawImage>("droidImage").texture);

            UnityEngine.Object.DestroyImmediate(replacementDroid);
            UnityEngine.Object.DestroyImmediate(replacementProtocol);
        }

        [Test]
        public void EnqueuePlaybacks_OrderedAnimations_PlaysFramesAndRestoresIdleImages()
        {
            _view.Render(CreatePresentation(true));
            StrategyAdvisorAnimationViewData protocolAnimation =
                new StrategyAdvisorAnimationViewData(
                    new[] { _protocolFirstTexture, _protocolSecondTexture },
                    false,
                    "protocol"
                );
            StrategyAdvisorAnimationViewData droidAnimation = new StrategyAdvisorAnimationViewData(
                new[] { _droidPlaybackTexture },
                true,
                "droid"
            );
            List<StrategyAdvisorAnimationViewData> started =
                new List<StrategyAdvisorAnimationViewData>();
            _view.PlaybackStarted += animation => started.Add(animation);

            _view.EnqueuePlaybacks(new[] { protocolAnimation, droidAnimation });

            Assert.AreSame(_protocolFirstTexture, GetField<RawImage>("protocolImage").texture);
            Assert.AreSame(_droidIdleTexture, GetField<RawImage>("droidImage").texture);
            CollectionAssert.AreEqual(new[] { protocolAnimation }, started);

            _view.AdvanceAnimation(0.5f);

            Assert.AreSame(_protocolSecondTexture, GetField<RawImage>("protocolImage").texture);
            CollectionAssert.AreEqual(new[] { protocolAnimation }, started);

            _view.AdvanceAnimation(0.5f);

            Assert.AreSame(_protocolIdleTexture, GetField<RawImage>("protocolImage").texture);
            Assert.AreSame(_droidPlaybackTexture, GetField<RawImage>("droidImage").texture);
            CollectionAssert.AreEqual(new[] { protocolAnimation, droidAnimation }, started);

            _view.AdvanceAnimation(0.5f);

            Assert.AreSame(_droidIdleTexture, GetField<RawImage>("droidImage").texture);
            CollectionAssert.AreEqual(new[] { protocolAnimation, droidAnimation }, started);
        }

        [Test]
        public void EnqueuePlaybacks_NullAndEmptyAnimations_DoesNotStartPlayback()
        {
            _view.Render(CreatePresentation(true));
            int startedCount = 0;
            _view.PlaybackStarted += _ => startedCount++;

            _view.EnqueuePlaybacks(null);
            _view.EnqueuePlaybacks(
                new[]
                {
                    (StrategyAdvisorAnimationViewData)null,
                    new StrategyAdvisorAnimationViewData(null, false, null),
                }
            );
            _view.AdvanceAnimation(10f);

            Assert.AreEqual(0, startedCount);
            Assert.AreSame(_protocolIdleTexture, GetField<RawImage>("protocolImage").texture);
            Assert.AreSame(_droidIdleTexture, GetField<RawImage>("droidImage").texture);
        }

        [Test]
        public void DroidInput_LeftClick_RaisesDroidClicked()
        {
            _view.Render(CreatePresentation(true));
            int clickCount = 0;
            _view.DroidClicked += () => clickCount++;

            GetField<UIRaycastArea>("droidInput")
                .OnPointerClick(CreatePointerEvent(PointerEventData.InputButton.Left));

            Assert.AreEqual(1, clickCount);
        }

        [Test]
        public void AdvisorInputs_RightPress_RaiseSourceCoordinates()
        {
            _view.Render(CreatePresentation(true));
            PointerEventData eventData = CreatePointerEvent(PointerEventData.InputButton.Right);
            UILayout.TryGetSourcePosition(
                _view.transform as RectTransform,
                eventData,
                out Vector2Int expectedPosition
            );
            Vector2Int protocolPosition = new Vector2Int(-1, -1);
            Vector2Int droidPosition = new Vector2Int(-1, -1);
            _view.ProtocolContextRequested += (x, y) => protocolPosition = new Vector2Int(x, y);
            _view.DroidContextRequested += (x, y) => droidPosition = new Vector2Int(x, y);

            GetField<UIRaycastArea>("protocolInput").OnPointerDown(eventData);
            GetField<UIRaycastArea>("droidInput").OnPointerDown(eventData);

            Assert.AreEqual(expectedPosition, protocolPosition);
            Assert.AreEqual(expectedPosition, droidPosition);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsInputsAndRaisesDestroyedEvent()
        {
            _view.Render(CreatePresentation(true));
            StrategyAdvisorView destroyedView = null;
            int droidClickCount = 0;
            int contextCount = 0;
            _view.Destroyed += view => destroyedView = view;
            _view.DroidClicked += () => droidClickCount++;
            _view.ProtocolContextRequested += (_, _) => contextCount++;
            _view.DroidContextRequested += (_, _) => contextCount++;
            PointerEventData leftClick = CreatePointerEvent(PointerEventData.InputButton.Left);
            PointerEventData rightPress = CreatePointerEvent(PointerEventData.InputButton.Right);

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            GetField<UIRaycastArea>("droidInput").OnPointerClick(leftClick);
            GetField<UIRaycastArea>("protocolInput").OnPointerDown(rightPress);
            GetField<UIRaycastArea>("droidInput").OnPointerDown(rightPress);

            Assert.AreSame(_view, destroyedView);
            Assert.AreEqual(0, droidClickCount);
            Assert.AreEqual(0, contextCount);
        }

        private StrategyAdvisorViewData CreatePresentation(bool visible)
        {
            return new StrategyAdvisorViewData(
                visible,
                _protocolIdleTexture,
                _droidIdleTexture,
                new RectInt(100, 300, 40, 60),
                new RectInt(700, 310, 45, 55),
                0.5f
            );
        }

        private PointerEventData CreatePointerEvent(PointerEventData.InputButton button)
        {
            return new PointerEventData(null)
            {
                button = button,
                position = RectTransformUtility.WorldToScreenPoint(null, _view.transform.position),
            };
        }

        private T GetField<T>(string fieldName)
        {
            return (T)
                typeof(StrategyAdvisorView)
                    .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(_view);
        }

        private static RectInt GetSourceRect(Transform transform)
        {
            return UILayout.GetSourceRect(transform as RectTransform);
        }
    }
}
