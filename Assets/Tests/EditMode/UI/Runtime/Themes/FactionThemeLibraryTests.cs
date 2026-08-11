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
                "Pack/Shared/Tactical/Environment/starfield",
                alliance.StarfieldImagePath
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Environment/starfield",
                empire.StarfieldImagePath
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13064-1033-tactical-pass-by",
                alliance.CapitalShipArrivalAudio.Paths[0]
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13065-1033-tactical-pass-by",
                alliance.CapitalShipWithdrawalAudio.Paths[0]
            );
            Assert.IsNull(alliance.SuperlaserAudio);
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13030-1033-tactical-blast",
                alliance.EnergyShieldHitAudio.Paths[0]
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13053-1033-tactical-blast",
                alliance.IonShieldPenetrationAudio.Paths[2]
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13018-1033-tactical-blast",
                alliance.TractorLockAudio.Paths[0]
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13019-1033-tactical-blast",
                alliance.TractorReleaseAudio.Paths[0]
            );
            Assert.AreEqual(3, alliance.SmallShipDestructionAudio.Paths.Count);
            Assert.AreEqual(3, alliance.MediumShipDestructionAudio.Paths.Count);
            Assert.AreEqual(3, alliance.LargeShipDestructionAudio.Paths.Count);
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13062-1033-tactical-pass-by-fighter-alliance",
                alliance.FighterArrivalAudio.Paths[0]
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13063-1033-tactical-pass-by",
                alliance.FighterWithdrawalAudio.Paths[0]
            );
            Assert.AreEqual(4, alliance.LaserCannonFireAudio.Paths.Count);
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13012-1033-tactical-blast",
                alliance.FighterLaserCannonFireAudio.Paths[0]
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13014-1033-tactical-blast",
                empire.FighterLaserCannonFireAudio.Paths[0]
            );
            CollectionAssert.AreEqual(
                alliance.CapitalShipArrivalAudio.Paths,
                empire.CapitalShipArrivalAudio.Paths
            );
            CollectionAssert.AreEqual(
                alliance.FighterWithdrawalAudio.Paths,
                empire.FighterWithdrawalAudio.Paths
            );
            Assert.AreEqual(
                "Pack/Shared/Tactical/Audio/13054-1033-tactical-blast",
                empire.SuperlaserAudio.Paths[0]
            );
        }

        [Test]
        public void GetTheme_TacticalBattleContainsFactionVoiceMappings()
        {
            TacticalVoiceTheme alliance = _library.GetTheme("FNALL1").TacticalBattle.Voice;
            TacticalVoiceTheme empire = _library.GetTheme("FNEMP1").TacticalBattle.Voice;

            Assert.AreEqual("Pack/Factions/Alliance/Tactical/Audio/Voice", alliance.AudioRoot);
            Assert.AreEqual(
                "task-force-8-maneuver-acknowledged",
                alliance.ManeuverAcknowledged.TaskForces[7]
            );
            Assert.AreEqual(
                "fighter-group-gold-attack-acknowledged",
                alliance.AttackAcknowledged.FighterGroups[3]
            );
            Assert.AreEqual("Pack/Factions/Empire/Tactical/Audio/Voice", empire.AudioRoot);
            Assert.AreEqual(
                "task-force-1-formation-acknowledged",
                empire.FormationAcknowledged.TaskForces[0]
            );
            Assert.AreEqual(
                "fighter-group-red-mission-acknowledged",
                empire.MissionAcknowledged.FighterGroups[0]
            );
            Assert.AreEqual("death-star-firing-warning", alliance.DeathStar.SuperlaserWarning);
            Assert.AreEqual("superlaser-firing", empire.DeathStar.SuperlaserFiring);
            Assert.AreEqual("superlaser-ready", empire.DeathStar.SuperlaserReady);
        }
    }
}
