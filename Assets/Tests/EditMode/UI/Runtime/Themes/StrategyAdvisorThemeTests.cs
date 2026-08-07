using NUnit.Framework;

namespace Rebellion.Tests.UI.Runtime.Themes
{
    [TestFixture]
    public class StrategyAdvisorThemeTests
    {
        [Test]
        public void GetFramePath_ReturnsRoleResourceAndFramePath()
        {
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme
            {
                AnimationImageRoot =
                    "Pack/Factions/Example/Strategy/Advisor/Animations/Notifications",
            };

            string path = theme.GetFramePath(3331, 4, true);

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/Advisor/Animations/Notifications/Alert/3331/frame-004",
                path
            );
        }

        [Test]
        public void BriefingGetFramePath_ReturnsResourceAndFramePath()
        {
            StrategyBriefingTheme theme = new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings",
            };

            string path = theme.GetFramePath(2100, 12);

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/2100/frame-012",
                path
            );
        }

        [Test]
        public void GetAudioPath_ReturnsNormalizedWavePath()
        {
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme
            {
                AudioRoot = "Pack/Factions/Example/Strategy/Advisor/Audio/Notifications",
                AudioFilePrefix = "advisor",
            };

            string path = theme.GetAudioPath(42);

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/Advisor/Audio/Notifications/advisor-0042",
                path
            );
        }

        [Test]
        public void CreatePreloadManifest_RepeatedAssets_ReturnsDistinctBriefingMedia()
        {
            StrategyBriefingTheme theme = new StrategyBriefingTheme
            {
                AnimationImageRoot = "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings",
                AudioRoot = "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings",
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
                    "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/10",
                    "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/20",
                },
                manifest.TextureDirectories
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings/briefing-0030",
                    "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings/briefing-0040",
                },
                manifest.Audio
            );
        }
    }
}
