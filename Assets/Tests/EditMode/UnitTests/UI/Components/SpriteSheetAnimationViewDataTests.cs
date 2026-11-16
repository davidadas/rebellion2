using System;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class SpriteSheetAnimationViewDataTests
    {
        private Texture2D texture;

        /// <summary>
        /// Creates a small texture for the playback-data contract.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(8, 8);
        }

        /// <summary>
        /// Releases the owned test texture.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        [TestCase(0, 1, 1f)]
        [TestCase(1, 0, 1f)]
        [TestCase(1, 1, 0f)]
        [TestCase(1, 1, -1f)]
        [TestCase(1, 1, float.NaN)]
        [TestCase(1, 1, float.PositiveInfinity)]
        public void Constructor_InvalidPlayback_RejectsInvalidData(
            int frames,
            int columns,
            float fps
        )
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SpriteSheetAnimationViewData(texture, frames, columns, fps)
            );
        }

        [Test]
        public void Constructor_NoTexture_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SpriteSheetAnimationViewData(null, 4, 2, 4f)
            );
        }

        [Test]
        public void Rows_IncompleteFinalRow_IncludesRemainingFrames()
        {
            SpriteSheetAnimationViewData data = new SpriteSheetAnimationViewData(
                texture,
                5,
                3,
                10f
            );
            Assert.AreEqual(2, data.Rows);
        }
    }
}
