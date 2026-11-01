using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class SplitterViewTests
    {
        private GameObject root;
        private RectTransform container;
        private SplitterView view;

        /// <summary>
        /// Creates a divider inside a known-size container.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Split container", typeof(RectTransform));
            container = root.GetComponent<RectTransform>();
            container.sizeDelta = new Vector2(800, 600);
            GameObject divider = new GameObject("Divider", typeof(RectTransform));
            divider.transform.SetParent(container, false);
            view = divider.AddComponent<SplitterView>();
            view.Configure(container);
        }

        /// <summary>
        /// Releases the divider and its container.
        /// </summary>
        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [TestCase(0.1f)]
        [TestCase(0.75f)]
        [TestCase(1.2f)]
        public void OnDrag_ContainerCoordinates_EmitsUnclampedPosition(float fraction)
        {
            float? reported = null;
            view.PositionChanged += value => reported = value;
            Vector3 point = container.TransformPoint(
                new Vector3(container.rect.xMin + container.rect.width * fraction, 0)
            );
            view.OnDrag(
                new PointerEventData(null)
                {
                    button = PointerEventData.InputButton.Left,
                    position = RectTransformUtility.WorldToScreenPoint(null, point),
                }
            );
            Assert.AreEqual(fraction, reported, 0.001f);
        }

        [Test]
        public void OnDrag_SecondaryButton_DoesNotRequestResize()
        {
            bool requested = false;
            view.PositionChanged += _ => requested = true;
            view.OnDrag(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Assert.IsFalse(requested);
        }
    }
}
