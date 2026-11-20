using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorColorWheelTests
    {
        private GameObject canvas;
        private GameObject eventSystem;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject(
                "Color wheel canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster)
            );
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            eventSystem = new GameObject(
                "Color wheel event system",
                typeof(EventSystem),
                typeof(StandaloneInputModule)
            );
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(eventSystem);
        }

        [Test]
        public void OnPointerGesture_RightEdge_PreviewsFullySaturatedRedAndCommits()
        {
            GameObject wheelObject = new GameObject(
                "Color wheel",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(ContentEditorColorWheel)
            );
            RectTransform rect = (RectTransform)wheelObject.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200, 200);
            ContentEditorColorWheel wheel = wheelObject.GetComponent<ContentEditorColorWheel>();
            float previewHue = -1;
            float previewSaturation = -1;
            bool committed = false;
            wheel.Configure(
                (hue, saturation) =>
                {
                    previewHue = hue;
                    previewSaturation = saturation;
                },
                () => committed = true
            );
            Canvas.ForceUpdateCanvases();
            Vector2 rightEdge = RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(new Vector3(rect.rect.xMax, rect.rect.center.y))
            );
            PointerEventData eventData = new(EventSystem.current) { position = rightEdge };

            wheel.OnPointerDown(eventData);
            wheel.OnPointerUp(eventData);

            Assert.That(previewHue, Is.EqualTo(0).Within(0.01f));
            Assert.That(previewSaturation, Is.EqualTo(1).Within(0.01f));
            Assert.IsTrue(committed);
        }
    }
}
