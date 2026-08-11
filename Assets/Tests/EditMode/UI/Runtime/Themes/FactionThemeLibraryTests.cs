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
            _library = TestContent.CreateThemeLibrary();
        }

        [Test]
        public void GetTheme_ConfiguredFaction_ReturnsExactTheme()
        {
            FactionTheme theme = _library.GetTheme("FNALL1");

            Assert.AreEqual("FNALL1", theme.FactionInstanceID);
        }

        [Test]
        public void GetTheme_EmptyFaction_ReturnsDefaultTheme()
        {
            FactionTheme nullTheme = _library.GetTheme(null);
            FactionTheme emptyTheme = _library.GetTheme(string.Empty);

            Assert.AreEqual("DEFAULT", nullTheme.FactionInstanceID);
            Assert.AreSame(nullTheme, emptyTheme);
        }

        [Test]
        public void GetTheme_UnknownFaction_ThrowsKeyNotFoundException()
        {
            Assert.Throws<KeyNotFoundException>(() => _library.GetTheme("missing-faction"));
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
                    "Pack/Shared/Strategy/Audio/Music/rescue-of-the-princess-heroics-of-luke-and-han-wampas-lair-jedi-training-medley",
                    "Pack/Shared/Strategy/Audio/Music/main-title-death-star-tatooine-emperor-medley",
                    "Pack/Shared/Strategy/Audio/Music/brother-and-sister-father-and-son-fleet-enters-hyperspace-heroic-ewok-medley",
                },
                allianceMusic.NeutralTrackPaths
            );
            CollectionAssert.AreEqual(
                allianceMusic.NeutralTrackPaths,
                empireMusic.NeutralTrackPaths
            );
            Assert.AreEqual(
                "Pack/Shared/Strategy/Audio/Music/landos-palace",
                allianceMusic.StrongAdvantageTrackPath
            );
            Assert.AreEqual(
                "Pack/Shared/Strategy/Audio/Music/emperor-arrives-death-of-yoda-obi-wans-revelation-medley",
                allianceMusic.AdvantageTrackPath
            );
            Assert.AreEqual(
                "Pack/Shared/Strategy/Audio/Music/imperial-march-darth-vaders-theme-intro-and-stinger",
                allianceMusic.DisadvantageTrackPath
            );
            Assert.AreEqual(
                "Pack/Shared/Strategy/Audio/Music/emperor-arrives-death-of-yoda-obi-wans-revelation-medley-stinger",
                empireMusic.StrongAdvantageTrackPath
            );
            Assert.AreEqual(
                "Pack/Shared/Strategy/Audio/Music/imperial-march-darth-vaders-theme-intro-and-stinger",
                empireMusic.AdvantageTrackPath
            );
            Assert.AreEqual(
                "Pack/Shared/Strategy/Audio/Music/emperor-arrives-death-of-yoda-obi-wans-revelation-medley",
                empireMusic.DisadvantageTrackPath
            );
            Assert.AreEqual(3, allianceMusic.NeutralTracksBetweenStrategicTracks);
            Assert.AreEqual(100, allianceMusic.PlanetRatioScale);
            Assert.AreEqual(10, allianceMusic.NoOpponentPlanetMultiplier);
            Assert.AreEqual(300, allianceMusic.StrongAdvantageMinimumRatio);
            Assert.AreEqual(200, allianceMusic.AdvantageMinimumRatio);
            Assert.AreEqual(50, allianceMusic.DisadvantageMaximumRatio);
        }

        [Test]
        public void GetTheme_TacticalBattleContainsFactionAudioMappings()
        {
            TacticalBattleTheme alliance = _library.GetTheme("FNALL1").TacticalBattle;
            TacticalBattleTheme empire = _library.GetTheme("FNEMP1").TacticalBattle;

            Assert.AreEqual("Pack/Shared/Tactical/Effects", alliance.SharedEffectsRoot);
            Assert.AreEqual("Pack/Shared/Tactical/Effects", empire.SharedEffectsRoot);
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13028-1033-tactical-blast",
                alliance.ArrivalAudioPath
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13029-1033-tactical-blast",
                alliance.WithdrawalAudioPath
            );
            Assert.IsNull(alliance.SuperlaserAudioPath);
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13012-1033-tactical-blast",
                alliance.EnergyShieldHitAudioPath
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13019-1033-tactical-blast",
                alliance.IonShieldPenetrationAudioPath
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13030-1033-tactical-blast",
                empire.ArrivalAudioPath
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13031-1033-tactical-blast",
                empire.WithdrawalAudioPath
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13020-1033-tactical-blast",
                empire.SuperlaserAudioPath
            );
        }
    }
}
