using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Results;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentPackTests
    {
        [Test]
        public void OpenActive_ConfiguredCatalog_ComposesSelectedPackAndScenario()
        {
            ContentPack pack = ContentPackLoader.OpenActive();

            Assert.AreEqual("classic-galactic-civil-war", pack.Definition.ID);
            Assert.AreEqual("standard", pack.Scenario.ID);
            CollectionAssert.AreEquivalent(
                pack.Scenario.PlayableFactionIDs,
                pack.Factions.Select(faction => faction.ID)
            );
            Assert.IsNotEmpty(pack.GameData.Factions);
            Assert.IsNotEmpty(pack.GameData.PlanetSystems);
            Assert.IsNotEmpty(pack.GameData.Officers);
        }

        [Test]
        public void OpenActive_ClassicStoryEvents_PreserveHeritageAndFinalBattleOutcomes()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            GameEvent heritage = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "LUKE_DISCOVERS_HERITAGE"
            );
            NarrativeMessageAction heritageMessage = heritage
                .Actions.OfType<NarrativeMessageAction>()
                .Single();
            GameEvent finalBattle = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "VADER_TAKES_LUKE_TO_EMPEROR"
            );
            StartStoryFinalBattleAction startFinalBattle = finalBattle
                .Actions.OfType<StartStoryFinalBattleAction>()
                .Single();
            NarrativeMessageAction victoryMessage = pack
                .GameData.GameEvents.Single(gameEvent =>
                    gameEvent.InstanceID == "LUKE_WINS_FINAL_BATTLE"
                )
                .Actions.OfType<NarrativeMessageAction>()
                .Single();
            NarrativeMessageAction defeatMessage = pack
                .GameData.GameEvents.Single(gameEvent =>
                    gameEvent.InstanceID == "LUKE_LOSES_FINAL_BATTLE"
                )
                .Actions.OfType<NarrativeMessageAction>()
                .Single();
            GameEvent lukeVaderEncounter = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "LUKE_ENCOUNTERS_VADER"
            );
            GameEvent lukeVaderEffects = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "LUKE_VADER_ENCOUNTER_EFFECTS"
            );
            NarrativeMessageAction confrontation = lukeVaderEffects
                .Actions.OfType<NarrativeMessageAction>()
                .Single();
            GameEvent forceDetectionEvent = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "FORCE_USERS_DETECT_ENEMIES"
            );
            ReportForceDetectionAction forceDetection = forceDetectionEvent
                .Actions.OfType<ReportForceDetectionAction>()
                .Single();

            Assert.AreEqual(6, heritageMessage.BodySegments.Count);
            Assert.AreEqual(nameof(UnitArrivedResult), lukeVaderEncounter.TriggerResultType);
            Assert.IsInstanceOf<OfficerPairArrivalConditional>(lukeVaderEncounter.Conditionals[0]);
            Assert.AreEqual(5, confrontation.BodySegments.Count);
            Assert.IsTrue(confrontation.VoicePathFromOfficerEncounter);
            Assert.AreEqual(nameof(UnitArrivedResult), forceDetectionEvent.TriggerResultType);
            Assert.AreEqual("{subject} Detects Enemy", forceDetection.TitleTemplate);
            Assert.AreEqual(
                "{subject} has detected {relatedSubject} because of a disturbance in the Force.",
                forceDetection.BodyTemplate
            );
            Assert.AreEqual(4, forceDetection.ExcludedPairs.Count);
            Assert.AreEqual(
                "Pack/Factions/Alliance/Strategy/Audio/Messages/message-faction-report",
                forceDetection.VoicePaths["FNALL1"]
            );
            Assert.AreEqual(
                "Pack/Factions/Empire/Strategy/Audio/Messages/message-faction-report",
                forceDetection.VoicePaths["FNEMP1"]
            );
            Assert.IsFalse(startFinalBattle.CaptivesCanEscapeOnVictory);
            Assert.AreEqual(
                "Pack/Shared/Events/FinalBattle/Audio/luke-victorious",
                victoryMessage.VoicePath
            );
            Assert.AreEqual(
                "Pack/Shared/Events/FinalBattle/Audio/luke-defeated",
                defeatMessage.VoicePath
            );
        }

        [TestCase("main-menu")]
        [TestCase("save-menu")]
        [TestCase("strategy")]
        public void PreloadManifests_ConfiguredScope_MatchesContentOwner(string preloadID)
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            ContentPreloadManifest applicationManifest =
                ContentPackLoader.LoadApplicationPreloadManifest(pack.ContentRootPath, preloadID);
            ContentPreloadManifest packManifest = pack.GetPreloadManifest(preloadID);
            string[] applicationAddresses = applicationManifest
                .Textures.Concat(applicationManifest.TextureDirectories)
                .Concat(applicationManifest.Audio)
                .Concat(applicationManifest.Models)
                .ToArray();
            string[] packAddresses = packManifest
                .Textures.Concat(packManifest.TextureDirectories)
                .Concat(packManifest.Audio)
                .Concat(packManifest.Models)
                .ToArray();

            Assert.IsNotEmpty(applicationAddresses);
            Assert.IsTrue(
                applicationAddresses.All(address =>
                    address.StartsWith("Application/", System.StringComparison.Ordinal)
                )
            );
            Assert.IsTrue(
                packAddresses.All(address =>
                    address.StartsWith("Pack/", System.StringComparison.Ordinal)
                )
            );
        }

        [TestCase(RuntimePlatform.OSXPlayer, "Game.app/Contents/Resources/Data")]
        [TestCase(RuntimePlatform.OSXPlayer, "Game.app/Contents")]
        [TestCase(RuntimePlatform.LinuxPlayer, "Game_Data")]
        [TestCase(RuntimePlatform.WindowsPlayer, "Game_Data")]
        public void ResolvePlayerContentRootPath_DesktopPlayer_ReturnsDirectoryBesideArtifact(
            RuntimePlatform platform,
            string relativeDataPath
        )
        {
            string playerDirectory = Path.Combine(Path.GetTempPath(), "content-pack-player-layout");
            string dataPath = Path.Combine(playerDirectory, relativeDataPath);

            string contentRoot = ContentPackLoader.ResolvePlayerContentRootPath(dataPath, platform);

            Assert.AreEqual(Path.Combine(playerDirectory, "Content"), contentRoot);
        }

        [Test]
        public void ResolvePlayerContentRootPath_MacBundleLayout_DoesNotDependOnPlatformEnum()
        {
            string playerDirectory = Path.Combine(Path.GetTempPath(), "content-pack-mac-layout");
            string dataPath = Path.Combine(
                playerDirectory,
                "Game.app",
                "Contents",
                "Resources",
                "Data"
            );

            string contentRoot = ContentPackLoader.ResolvePlayerContentRootPath(
                dataPath,
                RuntimePlatform.LinuxPlayer
            );

            Assert.AreEqual(Path.Combine(playerDirectory, "Content"), contentRoot);
        }
    }
}
