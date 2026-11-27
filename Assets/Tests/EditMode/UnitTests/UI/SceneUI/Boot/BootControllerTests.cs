using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.Boot
{
    [TestFixture]
    public sealed class BootControllerTests
    {
        [TestCase(
            null,
            null,
            "Application/Boot/Videos/intro",
            "Application/Boot/Videos/opening-crawl"
        )]
        [TestCase("Pack/intro.mp4", " ", "Pack/intro.mp4", "Application/Boot/Videos/opening-crawl")]
        [TestCase("Pack/intro.mp4", "Pack/crawl.webm", "Pack/intro.mp4", "Pack/crawl.webm")]
        public void GetVideoPaths_WithOptionalOverrides_ResolvesVideosInPlaybackOrder(
            string intro,
            string crawl,
            string expectedIntro,
            string expectedCrawl
        )
        {
            ContentPackDefinition definition = new()
            {
                StartupIntroVideoPath = intro,
                OpeningCrawlVideoPath = crawl,
            };

            CollectionAssert.AreEqual(
                new[] { expectedIntro, expectedCrawl },
                BootController.GetVideoPaths(definition)
            );
        }
    }
}
