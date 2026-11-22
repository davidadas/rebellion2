using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.ContentEditor.UI
{
    [TestFixture]
    public sealed class ContentEditorArtworkCropTests
    {
        [Test]
        public void GetCenteredFitRect_WideSourceInsideSquareTarget_PreservesCompleteSource()
        {
            RectInt fitted = ContentEditorArtworkCrop.GetCenteredFitRect(
                new Vector2Int(200, 100),
                new RectInt(10, 20, 100, 100)
            );

            Assert.AreEqual(new RectInt(10, 45, 100, 50), fitted);
        }

        [Test]
        public void GetCenteredFitRect_SquareSourceInsideWideTarget_CentersCompleteSource()
        {
            RectInt fitted = ContentEditorArtworkCrop.GetCenteredFitRect(
                new Vector2Int(77, 77),
                new RectInt(10, 20, 200, 80)
            );

            Assert.AreEqual(new RectInt(70, 20, 80, 80), fitted);
        }

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
