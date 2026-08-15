using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class UIRaycastAreaTests
    {
        private UIRaycastArea _area;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("UIRaycastArea", typeof(RectTransform));
            _root.SetActive(false);
            _area = _root.AddComponent<UIRaycastArea>();
            RawImage image = new GameObject(
                "RaycastTargetImage",
                typeof(RectTransform),
                typeof(RawImage)
            ).GetComponent<RawImage>();
            image.transform.SetParent(_root.transform, false);
            typeof(UIRaycastArea)
                .GetField("raycastTargetImage", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_area, image);
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void OnPointerUp_AfterLeftButtonPress_EmitsRelease()
        {
            int releaseCount = 0;
            _area.Released += (_, _) => releaseCount++;
            PointerEventData eventData = new PointerEventData(null)
            {
                button = PointerEventData.InputButton.Left,
            };

            _area.OnPointerDown(eventData);
            _area.OnPointerUp(eventData);

            Assert.AreEqual(1, releaseCount);
        }

        [Test]
        public void OnPointerUp_WithoutLeftButtonPress_DoesNotEmitRelease()
        {
            int releaseCount = 0;
            _area.Released += (_, _) => releaseCount++;

            _area.OnPointerUp(
                new PointerEventData(null) { button = PointerEventData.InputButton.Left }
            );

            Assert.AreEqual(0, releaseCount);
        }

        [Test]
        public void OnPointerDown_RightButton_EmitsContextRequestOnly()
        {
            int pressCount = 0;
            int contextCount = 0;
            _area.Pressed += (_, _) => pressCount++;
            _area.ContextRequested += (_, _) => contextCount++;

            _area.OnPointerDown(
                new PointerEventData(null) { button = PointerEventData.InputButton.Right }
            );

            Assert.AreEqual(0, pressCount);
            Assert.AreEqual(1, contextCount);
        }
    }
}
