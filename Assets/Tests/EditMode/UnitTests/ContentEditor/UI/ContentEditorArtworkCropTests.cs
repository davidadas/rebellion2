using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorArtworkCropTests
    {
        [Test]
        public void GetCenteredUvRect_WideSourceForSquareOutput_CropsHorizontalCenter()
        {
            Rect crop = ContentEditorArtworkCrop.GetCenteredUvRect(
                new Vector2Int(1800, 900),
                new Vector2Int(360, 360)
            );

            Assert.AreEqual(new Rect(0.25f, 0, 0.5f, 1), crop);
        }

        [Test]
        public void GetCenteredUvRect_SourceNarrowerThanOutput_CropsVerticalCenter()
        {
            Rect crop = ContentEditorArtworkCrop.GetCenteredUvRect(
                new Vector2Int(1800, 900),
                new Vector2Int(297, 113)
            );

            Assert.AreEqual(1, crop.width);
            Assert.AreEqual(2f / (297f / 113f), crop.height, 0.0001f);
            Assert.AreEqual((1 - crop.height) * 0.5f, crop.y, 0.0001f);
        }

        [Test]
        public void GetCenteredUvRect_InvalidDimensions_ReturnsCompleteCanvas()
        {
            Rect crop = ContentEditorArtworkCrop.GetCenteredUvRect(
                Vector2Int.zero,
                new Vector2Int(360, 360)
            );

            Assert.AreEqual(new Rect(0, 0, 1, 1), crop);
        }
    }
}
