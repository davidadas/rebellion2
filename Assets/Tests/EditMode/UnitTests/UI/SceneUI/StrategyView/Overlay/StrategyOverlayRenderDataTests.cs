using System;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Overlay
{
    [TestFixture]
    public class StrategyOverlayRenderDataTests
    {
        /// <summary>
        /// Verifies constructor texture without bounds throws argument exception.
        /// </summary>
        [Test]
        public void Constructor_TextureWithoutBounds_ThrowsArgumentException()
        {
            Texture2D texture = new Texture2D(4, 4);

            Assert.Throws<ArgumentException>(() =>
                new StrategyOverlayRenderData(null, texture, null)
            );

            UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary>
        /// Verifies constructor bounds without texture throws argument exception.
        /// </summary>
        [Test]
        public void Constructor_BoundsWithoutTexture_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
                new StrategyOverlayRenderData(null, null, new RectInt(1, 2, 3, 4))
            );
        }

        /// <summary>
        /// Verifies constructor multiple drag images stores preview and pointer position.
        /// </summary>
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
