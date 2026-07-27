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
                AnimationImageRoot = "pack/factions/example/strategy/ui/advisor",
                AnimationFilePrefix = "advisor",
            };

            string path = theme.GetFramePath(3331, 4, true);

            Assert.AreEqual(
                "pack/factions/example/strategy/ui/advisor/droid/3331/advisor-droid-3331-frame-004",
                path
            );
        }

        [Test]
        public void GetAudioPath_ReturnsNormalizedWavePath()
        {
            StrategyAdvisorTheme theme = new StrategyAdvisorTheme
            {
                AudioRoot = "pack/factions/example/strategy/audio/advisor",
                AudioFilePrefix = "advisor",
            };

            string path = theme.GetAudioPath(42);

            Assert.AreEqual("pack/factions/example/strategy/audio/advisor/advisor-0042", path);
        }
    }
}
