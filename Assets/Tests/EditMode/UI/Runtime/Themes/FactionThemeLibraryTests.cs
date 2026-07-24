using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.UI.Runtime.Themes
{
    [TestFixture]
    public class FactionThemeLibraryTests
    {
        private FactionThemeLibrary _library;

        [SetUp]
        public void SetUp()
        {
            _library = new FactionThemeLibrary();
        }

        [Test]
        public void GetTheme_ConfiguredFaction_ReturnsExactTheme()
        {
            FactionTheme theme = _library.GetTheme("FNALL1");

            Assert.AreEqual("FNALL1", theme.FactionInstanceID);
        }

        [Test]
        public void GetTheme_EmptyOrUnknownFaction_ReturnsDefaultTheme()
        {
            FactionTheme nullTheme = _library.GetTheme(null);
            FactionTheme emptyTheme = _library.GetTheme(string.Empty);
            FactionTheme unknownTheme = _library.GetTheme("missing-faction");

            Assert.AreEqual("DEFAULT", nullTheme.FactionInstanceID);
            Assert.AreSame(nullTheme, emptyTheme);
            Assert.AreSame(nullTheme, unknownTheme);
        }

        [Test]
        public void GetAllThemes_MutatedResult_DoesNotChangeLibraryContents()
        {
            List<FactionTheme> themes = _library.GetAllThemes();
            string[] configuredIds = themes.Select(theme => theme.FactionInstanceID).ToArray();

            themes.Clear();

            Assert.IsNotEmpty(configuredIds);
            CollectionAssert.AreEqual(
                configuredIds,
                _library.GetAllThemes().Select(theme => theme.FactionInstanceID).ToArray()
            );
            Assert.IsFalse(configuredIds.Contains("DEFAULT"));
        }

        [Test]
        public void GetTheme_StrategyMusicContainsFactionTrackMappingsAndCadence()
        {
            StrategyMusicTheme allianceMusic = _library.GetTheme("FNALL1").StrategyMusic;
            StrategyMusicTheme empireMusic = _library.GetTheme("FNEMP1").StrategyMusic;

            CollectionAssert.AreEqual(
                new[]
                {
                    "Audio/Music/rescue_of_the_princess_heroics_of_luke_and_han_wampas_lair_jedi_training_medley",
                    "Audio/Music/main_title_death_star_tatooine_emperor_medley",
                    "Audio/Music/brother_and_sister_father_and_son_fleet_enters_hyperspace_heroic_ewok_medley",
                },
                allianceMusic.NeutralTrackPaths
            );
            CollectionAssert.AreEqual(
                allianceMusic.NeutralTrackPaths,
                empireMusic.NeutralTrackPaths
            );
            Assert.AreEqual("Audio/Music/landos_palace", allianceMusic.StrongAdvantageTrackPath);
            Assert.AreEqual(
                "Audio/Music/emperor_arrives_death_of_yoda_obi_wans_revelation_medley",
                allianceMusic.AdvantageTrackPath
            );
            Assert.AreEqual(
                "Audio/Music/imperial_march_darth_vaders_theme_intro_and_stinger",
                allianceMusic.DisadvantageTrackPath
            );
            Assert.AreEqual(
                "Audio/Music/emperor_arrives_death_of_yoda_obi_wans_revelation_medley_stinger",
                empireMusic.StrongAdvantageTrackPath
            );
            Assert.AreEqual(
                "Audio/Music/imperial_march_darth_vaders_theme_intro_and_stinger",
                empireMusic.AdvantageTrackPath
            );
            Assert.AreEqual(
                "Audio/Music/emperor_arrives_death_of_yoda_obi_wans_revelation_medley",
                empireMusic.DisadvantageTrackPath
            );
            Assert.AreEqual(3, allianceMusic.NeutralTracksBetweenStrategicTracks);
            Assert.AreEqual(100, allianceMusic.PlanetRatioScale);
            Assert.AreEqual(10, allianceMusic.NoOpponentPlanetMultiplier);
            Assert.AreEqual(300, allianceMusic.StrongAdvantageMinimumRatio);
            Assert.AreEqual(200, allianceMusic.AdvantageMinimumRatio);
            Assert.AreEqual(50, allianceMusic.DisadvantageMaximumRatio);
        }
    }
}
