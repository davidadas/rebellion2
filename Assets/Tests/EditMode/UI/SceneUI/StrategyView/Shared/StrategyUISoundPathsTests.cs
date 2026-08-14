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
    }
}
