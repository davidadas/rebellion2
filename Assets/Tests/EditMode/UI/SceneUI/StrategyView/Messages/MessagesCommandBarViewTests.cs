using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Messages
{
    [TestFixture]
    public class MessagesCommandBarViewTests
    {
        private const string _prefabPath = "Assets/Prefabs/UI/StrategyView/MessagesWindow.prefab";

        private Texture2D _pressedTexture;
        private Texture2D _texture;
        private MessagesCommandBarView _view;
        private GameObject _windowObject;

        [SetUp]
        public void SetUp()
        {
            _windowObject = UIComponentTestHelper.InstantiatePrefab(_prefabPath);
            _view = _windowObject.GetComponentInChildren<MessagesCommandBarView>(true);
            _texture = new Texture2D(32, 24);
            _pressedTexture = new Texture2D(32, 24);
            UIComponentTestHelper.InvokeLifecycle(_view, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_pressedTexture);
            UnityEngine.Object.DestroyImmediate(_texture);
            UnityEngine.Object.DestroyImmediate(_windowObject);
        }

        [Test]
        public void Render_NullData_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _view.Render(null));
        }

        [Test]
        public void Render_ProjectedButtons_AppliesTexturesVisibilityAndInteraction()
        {
            Texture displayFallback = FindComponent<RawImage>("DisplayButtonImage").texture;
            MessagesCommandBarRenderData data = new MessagesCommandBarRenderData(
                _texture,
                CreateButton(_texture, _pressedTexture, true, true),
                CreateButton(null, null, true, true),
                CreateButton(_texture, _pressedTexture, true, false),
                CreateButton(_texture, _pressedTexture, false, true),
                CreateButton(_texture, _pressedTexture, true, true),
                CreateButton(_texture, _pressedTexture, true, true)
            );

            _view.Render(data);

            Assert.AreSame(_texture, FindComponent<RawImage>("ButtonStripImage").texture);
            Assert.AreSame(_texture, FindComponent<RawImage>("CloseButtonImage").texture);
            Assert.AreSame(displayFallback, FindComponent<RawImage>("DisplayButtonImage").texture);
            Assert.IsTrue(FindComponent<Button>("DisplayButtonImage").interactable);
            Assert.IsTrue(FindComponent<RawImage>("DisplayButtonImage").raycastTarget);
            Assert.IsTrue(FindObject("IndexButtonImage").activeSelf);
            Assert.IsFalse(FindComponent<Button>("IndexButtonImage").interactable);
            Assert.IsFalse(FindComponent<RawImage>("IndexButtonImage").raycastTarget);
            Assert.IsFalse(FindObject("SignalButtonImage").activeSelf);
            Assert.IsFalse(FindComponent<Button>("SignalButtonImage").interactable);
        }

        [Test]
        public void CommandButtons_Click_RaiseControlAndSemanticRequests()
        {
            int controlCount = 0;
            int closeCount = 0;
            int displayCount = 0;
            int indexCount = 0;
            int signalCount = 0;
            int targetCount = 0;
            int chatCount = 0;
            _view.ControlPressed += () => controlCount++;
            _view.CloseRequested += () => closeCount++;
            _view.DisplayRequested += () => displayCount++;
            _view.IndexRequested += () => indexCount++;
            _view.SignalRequested += () => signalCount++;
            _view.TargetRequested += () => targetCount++;
            _view.ChatRequested += () => chatCount++;
            _view.Render(CreateRenderData());

            FindComponent<Button>("CloseButtonImage").onClick.Invoke();
            FindComponent<Button>("DisplayButtonImage").onClick.Invoke();
            FindComponent<Button>("IndexButtonImage").onClick.Invoke();
            FindComponent<Button>("SignalButtonImage").onClick.Invoke();
            FindComponent<Button>("SignalTargetButtonImage").onClick.Invoke();
            FindComponent<Button>("ChatCommandButtonImage").onClick.Invoke();

            Assert.AreEqual(6, controlCount);
            Assert.AreEqual(1, closeCount);
            Assert.AreEqual(1, displayCount);
            Assert.AreEqual(1, indexCount);
            Assert.AreEqual(1, signalCount);
            Assert.AreEqual(1, targetCount);
            Assert.AreEqual(1, chatCount);
        }

        [Test]
        public void OnDestroy_InitializedView_UnbindsCommandControls()
        {
            int controlCount = 0;
            int closeCount = 0;
            _view.ControlPressed += () => controlCount++;
            _view.CloseRequested += () => closeCount++;

            UIComponentTestHelper.InvokeLifecycle(_view, "OnDestroy");
            FindComponent<Button>("CloseButtonImage").onClick.Invoke();

            Assert.AreEqual(0, controlCount);
            Assert.AreEqual(0, closeCount);
        }

        private MessagesCommandBarRenderData CreateRenderData()
        {
            MessagesCommandButtonRenderData button = CreateButton(
                _texture,
                _pressedTexture,
                true,
                true
            );
            return new MessagesCommandBarRenderData(
                _texture,
                button,
                button,
                button,
                button,
                button,
                button
            );
        }

        private static MessagesCommandButtonRenderData CreateButton(
            Texture texture,
            Texture pressedTexture,
            bool visible,
            bool enabled
        )
        {
            return new MessagesCommandButtonRenderData(texture, pressedTexture, visible, enabled);
        }

        private T FindComponent<T>(string objectName)
            where T : Component
        {
            return _windowObject
                .GetComponentsInChildren<T>(true)
                .Single(component => component.name == objectName);
        }

        private GameObject FindObject(string objectName)
        {
            return _windowObject
                .GetComponentsInChildren<Transform>(true)
                .Single(item => item.name == objectName)
                .gameObject;
        }
    }
}
