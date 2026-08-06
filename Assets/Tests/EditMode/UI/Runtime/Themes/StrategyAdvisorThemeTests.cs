using NUnit.Framework;

namespace Rebellion.Tests.UI.Runtime.Themes
{
    [TestFixture]
    public class StrategyAdvisorThemeTests
    {
        [Test]
        public void GetFramePath_ReturnsNormalizedRoleAndFramePath()
        {
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme
            {
                AnimationImageRoot = "Pack/Factions/Example/Strategy/UI/Advisor",
                AnimationFilePrefix = "advisor",
            };

            string path = theme.GetFramePath(3331, 4, true);

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/UI/Advisor/Droid/3331/advisor-droid-3331-frame-004",
                path
            );
        }

        [Test]
        public void GetAudioPath_ReturnsNormalizedWavePath()
        {
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme
            {
                AudioRoot = "Pack/Factions/Example/Strategy/Audio/Advisor",
                AudioFilePrefix = "advisor",
            };

            string path = theme.GetAudioPath(42);

            Assert.AreEqual("Pack/Factions/Example/Strategy/Audio/Advisor/advisor-0042", path);
        }

        [Test]
        public void CreatePreloadManifest_RepeatedAssets_ReturnsDistinctBriefingMedia()
        {
            StrategyBriefingTheme theme = new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Factions/Example/Strategy/UI/Briefing/Protocol",
                AudioRoot = "Pack/Factions/Example/Strategy/Audio/Briefing",
                AudioFilePrefix = "briefing",
                Skip = new StrategyAdvisorAnimationTheme { BitmapID = 20, WaveID = 40 },
            };
            theme.Segments.Add(new StrategyAdvisorAnimationTheme { BitmapID = 10, WaveID = 30 });
            theme.Segments.Add(new StrategyAdvisorAnimationTheme { BitmapID = 10, WaveID = 30 });

            ContentPreloadManifest manifest = theme.CreatePreloadManifest();

            Assert.AreEqual(64, manifest.TexturesPerFrame);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Factions/Example/Strategy/UI/Briefing/Protocol/10",
                    "Pack/Factions/Example/Strategy/UI/Briefing/Protocol/20",
                },
                manifest.TextureDirectories
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Factions/Example/Strategy/Audio/Briefing/briefing-0030",
                    "Pack/Factions/Example/Strategy/Audio/Briefing/briefing-0040",
                },
                manifest.Audio
            );
        }
    }
}
