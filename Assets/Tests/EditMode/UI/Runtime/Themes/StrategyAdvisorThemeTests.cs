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

            string path = theme.GetFramePath("Standard", 4, true);

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/Advisor/Animations/Notifications/Alert/Standard/frame-004",
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

            string path = theme.GetFramePath("Introduction", 12);

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/Introduction/frame-012",
                path
            );
        }

        [Test]
        public void GetAudioPath_ReturnsNamedAudioPath()
        {
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme
            {
                AudioRoot = "Pack/Factions/Example/Strategy/Advisor/Audio/Notifications",
            };

            string path = theme.GetAudioPath("FleetArrived");

            Assert.AreEqual(
                "Pack/Factions/Example/Strategy/Advisor/Audio/Notifications/FleetArrived",
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
                Skip = new StrategyBriefingSegmentTheme { Animation = "Skip", Audio = "Skip" },
            };
            theme.Segments.Add(
                new StrategyBriefingSegmentTheme
                {
                    Animation = "Introduction",
                    Audio = "Introduction",
                }
            );
            theme.Segments.Add(
                new StrategyBriefingSegmentTheme
                {
                    Animation = "Introduction",
                    Audio = "Introduction",
                }
            );

            ContentPreloadManifest manifest = theme.CreatePreloadManifest();

            Assert.AreEqual(64, manifest.TexturesPerFrame);
            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/Introduction",
                    "Pack/Factions/Example/Strategy/Advisor/Animations/Briefings/Skip",
                },
                manifest.TextureDirectories
            );
            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings/Introduction",
                    "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings/Skip",
                },
                manifest.Audio
            );
        }
    }
}
