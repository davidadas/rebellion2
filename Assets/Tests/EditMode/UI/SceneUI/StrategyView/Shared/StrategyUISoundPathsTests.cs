using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Shared
{
    [TestFixture]
    public sealed class StrategyUISoundPathsTests
    {
        [Test]
        public void GetPreloadPaths_NullTheme_ReturnsOnlySharedCues()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyUISoundPaths.ControlPress,
                    StrategyUISoundPaths.SectorWindowOpen,
                    StrategyUISoundPaths.SectorWindowClose,
                    StrategyUISoundPaths.GalacticInformationOpen,
                    StrategyUISoundPaths.GalacticInformationControl,
                    StrategyUISoundPaths.PlanetaryAssault,
                },
                StrategyUISoundPaths.GetPreloadPaths(null).ToArray()
            );
        }

        [Test]
        public void GetPreloadPaths_ConfiguredTheme_ReturnsSharedAndThemedCues()
        {
            FactionTheme theme = new FactionTheme
            {
                StrategyWindowSounds = new StrategyWindowSoundTheme
                {
                    PlanetWindowOpenSoundPath = "window-open",
                    PlanetWindowExpandSoundPath = "window-expand",
                    PlanetWindowCollapseSoundPath = "window-collapse",
                    PlanetWindowMinimizeSoundPath = "window-minimize",
                },
                ConfirmDialogTheme = new ConfirmDialogTheme
                {
                    ScrapRetireSoundPath = " scrap-retire ",
                    StopConstructionSoundPath = "stop-construction",
                },
            };

            CollectionAssert.AreEqual(
                new[]
                {
                    StrategyUISoundPaths.ControlPress,
                    StrategyUISoundPaths.SectorWindowOpen,
                    StrategyUISoundPaths.SectorWindowClose,
                    StrategyUISoundPaths.GalacticInformationOpen,
                    StrategyUISoundPaths.GalacticInformationControl,
                    StrategyUISoundPaths.PlanetaryAssault,
                    "window-open",
                    "window-expand",
                    "window-collapse",
                    "window-minimize",
                    "scrap-retire",
                    "stop-construction",
                },
                StrategyUISoundPaths.GetPreloadPaths(theme).ToArray()
            );
        }

        [Test]
        public void GetPreloadPaths_ConfiguredBriefing_ReturnsSegmentAndSkipAudio()
        {
            FactionTheme theme = new FactionTheme
            {
                StrategyBriefing = new StrategyBriefingTheme
                {
                    AudioRoot = "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings",
                    AudioFilePrefix = "briefing",
                    Skip = new StrategyAdvisorAnimationTheme { WaveID = 42 },
                },
            };
            theme.StrategyBriefing.Segments.Add(new StrategyAdvisorAnimationTheme { WaveID = 41 });

            string[] paths = StrategyUISoundPaths.GetBriefingPreloadPaths(theme).ToArray();

            CollectionAssert.AreEqual(
                new[]
                {
                    "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings/briefing-0041",
                    "Pack/Factions/Example/Strategy/Advisor/Audio/Briefings/briefing-0042",
                },
                paths
            );
        }
    }
}
