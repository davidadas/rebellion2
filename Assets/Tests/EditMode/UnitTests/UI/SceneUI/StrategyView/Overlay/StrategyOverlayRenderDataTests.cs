using System;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Overlay
{
    [TestFixture]
    public class StrategyOverlayRenderDataTests
    {
        [Test]
        public void Constructor_TextureWithoutBounds_ThrowsArgumentException()
        {
            Texture2D texture = new Texture2D(4, 4);

            Assert.Throws<ArgumentException>(() =>
                new StrategyOverlayRenderData(null, texture, null)
            );

            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void Constructor_BoundsWithoutTexture_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new StrategyOverlayRenderData(null, null, new RectInt(1, 2, 3, 4))
            );
        }

        [Test]
        public void Constructor_CompletePresentation_StoresAllValues()
        {
            Texture2D texture = new Texture2D(4, 4);
            RectInt frameBounds = new RectInt(10, 20, 100, 80);
            RectInt imageBounds = new RectInt(30, 40, 20, 16);

            StrategyOverlayRenderData data = new StrategyOverlayRenderData(
                frameBounds,
                texture,
                imageBounds
            );

            Assert.AreEqual(frameBounds, data.DragFrameBounds);
            Assert.AreSame(texture, data.DragImageTexture);
            Assert.AreEqual(imageBounds, data.DragImageBounds);

            UnityEngine.Object.DestroyImmediate(texture);
        }

        [Test]
        public void Constructor_EmptyPresentation_StoresAbsentValues()
        {
            StrategyOverlayRenderData data = new StrategyOverlayRenderData(null, null, null);

            Assert.IsNull(data.DragFrameBounds);
            Assert.IsNull(data.DragImageTexture);
            Assert.IsNull(data.DragImageBounds);
        }

        [Test]
        public void Constructor_MultipleDragImages_StoresPreviewAndPointerPosition()
        {
            Texture2D texture = new Texture2D(4, 4);
            DragPreview preview = new DragPreview(
                new[]
                {
                    new DragPreviewImage(texture, new RectInt(10, 20, 30, 40)),
                    new DragPreviewImage(texture, new RectInt(60, 80, 20, 10)),
                },
                17,
                29
            );

            StrategyOverlayRenderData data = new StrategyOverlayRenderData(null, preview, 47, 69);

            Assert.AreSame(preview, data.DragPreview);
            Assert.AreEqual(47, data.DragPointerX);
            Assert.AreEqual(69, data.DragPointerY);
            Assert.AreEqual(new RectInt(40, 60, 30, 40), data.DragImageBounds);

            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
