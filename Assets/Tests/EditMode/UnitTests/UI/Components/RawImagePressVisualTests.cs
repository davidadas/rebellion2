using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class RawImagePressVisualTests
    {
        private Button _button;
        private GameObject _control;
        private RawImage _image;
        private Texture2D _normalTexture;
        private Texture2D _pressedTexture;
        private RawImagePressVisual _visual;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _control = new GameObject(
                "Control",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(Button)
            );
            _control.SetActive(false);
            _image = _control.GetComponent<RawImage>();
            _button = _control.GetComponent<Button>();
            _visual = _control.AddComponent<RawImagePressVisual>();
            typeof(RawImagePressVisual)
                .GetField("image", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_visual, _image);
            typeof(RawImagePressVisual)
                .GetField("button", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_visual, _button);
            _normalTexture = new Texture2D(1, 1);
            _pressedTexture = new Texture2D(1, 1);
            _control.SetActive(true);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_normalTexture);
            Object.DestroyImmediate(_pressedTexture);
            Object.DestroyImmediate(_control);
        }

        /// <summary>
        /// Verifies set interactive textures normal texture enables image and raycast target.
        /// </summary>
        [Test]
        public void SetInteractiveTextures_NormalTexture_EnablesImageAndRaycastTarget()
        {
            _image.enabled = false;
            _image.raycastTarget = false;

            _visual.SetInteractiveTextures(_normalTexture, _pressedTexture);

            Assert.AreSame(_normalTexture, _image.texture);
            Assert.IsTrue(_image.enabled);
            Assert.IsTrue(_image.raycastTarget);
            Assert.IsTrue(_control.activeSelf);
        }

        /// <summary>
        /// Verifies set interactive textures null normal texture disables control.
        /// </summary>
        [Test]
        public void SetInteractiveTextures_NullNormalTexture_DisablesControl()
        {
            _visual.SetInteractiveTextures(null, _pressedTexture);

            Assert.IsNull(_image.texture);
            Assert.IsFalse(_image.enabled);
            Assert.IsFalse(_image.raycastTarget);
            Assert.IsFalse(_control.activeSelf);
        }

        /// <summary>
        /// Verifies on pointer down interactive control uses pressed texture and emits press.
        /// </summary>
        [Test]
        public void OnPointerDown_InteractiveControl_UsesPressedTextureAndEmitsPress()
        {
            int pressedCount = 0;
            _visual.Pressed += () => pressedCount++;
            _visual.SetInteractiveTextures(_normalTexture, _pressedTexture);

            _visual.OnPointerDown(
                new PointerEventData(null) { button = PointerEventData.InputButton.Left }
            );

            Assert.AreSame(_pressedTexture, _image.texture);
            Assert.AreEqual(1, pressedCount);
        }

        /// <summary>
        /// Verifies on pointer down already pressed does not emit another press.
        /// </summary>
        [Test]
        public void OnPointerDown_AlreadyPressed_DoesNotEmitAnotherPress()
        {
            int pressedCount = 0;
            _visual.Pressed += () => pressedCount++;
            _visual.SetInteractiveTextures(_normalTexture, _pressedTexture);
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            _visual.OnPointerDown(eventData);
            _visual.OnPointerDown(eventData);

            Assert.AreEqual(1, pressedCount);
        }

        /// <summary>
        /// Verifies on pointer down disabled button preserves normal texture.
        /// </summary>
        [Test]
        public void OnPointerDown_DisabledButton_PreservesNormalTexture()
        {
            int pressedCount = 0;
            _visual.Pressed += () => pressedCount++;
            _visual.SetInteractiveTextures(_normalTexture, _pressedTexture);
            _button.interactable = false;

            _visual.OnPointerDown(new PointerEventData(null));

            Assert.AreSame(_normalTexture, _image.texture);
            Assert.AreEqual(0, pressedCount);
        }

        /// <summary>
        /// Verifies on pointer down right button preserves normal texture without emitting press.
        /// </summary>
        [Test]
        public void OnPointerDown_RightButton_PreservesNormalTextureWithoutEmittingPress()
        {
            int pressedCount = 0;
            _visual.Pressed += () => pressedCount++;
            _visual.SetInteractiveTextures(_normalTexture, _pressedTexture);

            _visual.OnPointerDown(
                new PointerEventData(null) { button = PointerEventData.InputButton.Right }
            );

            Assert.AreSame(_normalTexture, _image.texture);
            Assert.AreEqual(0, pressedCount);
        }

        /// <summary>
        /// Verifies on pointer up after pointer down restores normal texture.
        /// </summary>
        [Test]
        public void OnPointerUp_AfterPointerDown_RestoresNormalTexture()
        {
            _visual.SetInteractiveTextures(_normalTexture, _pressedTexture);
            _visual.OnPointerDown(new PointerEventData(null));

            _visual.OnPointerUp(new PointerEventData(null));

            Assert.AreSame(_normalTexture, _image.texture);
        }
    }
}
