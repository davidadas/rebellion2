using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Messages;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
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
        public void OpenActive_PlayableFactionThemes_ConfigureOriginalCampaignEndings()
        {
            ContentPack pack = ContentPackLoader.OpenActive();

            foreach (string factionId in pack.Scenario.PlayableFactionIDs)
            {
                FactionTheme theme = pack.GameData.FactionThemes.Single(candidate =>
                    candidate.FactionInstanceID == factionId
                );
                StringAssert.EndsWith("/Cutscenes/victory", theme.VictoryCutscenePath);
                StringAssert.EndsWith("/Cutscenes/defeat", theme.DefeatCutscenePath);
            }
        }

        [Test]
        public void OpenActive_DagobahCompletion_ReplacesHiddenMissionReports()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            GameEvent gameEvent = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "LUKE_LEAVES_DAGOBAH"
            );

            Assert.AreEqual(nameof(DagobahCompletedResult), gameEvent.TriggerResultType);
            Assert.IsTrue(gameEvent.SuppressSourceMessages);
            Assert.AreEqual(
                "LUKE_VISITS_YODA",
                gameEvent
                    .Conditionals.OfType<ResultSourceEventConditional>()
                    .Single()
                    .SourceEventInstanceID
            );
            NarrativeMessageAction message = gameEvent
                .Actions.OfType<NarrativeMessageAction>()
                .Single();
            Assert.AreEqual("LUKE_SKYWALKER", message.SubjectInstanceID);
            Assert.AreEqual("Luke Leaves Dagobah", message.TitleTemplate);
            Assert.AreEqual("I have finished my training with Yoda.", message.BodyTemplate);
        }

        [Test]
        public void OpenActive_JabbaCaptureEvents_ReplaceEachPalaceRescueReport()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            (string eventId, string sourceEventId, string officerId)[] expectedEvents =
            {
                ("JABBA_CAPTURES_LUKE_SKYWALKER", "LUKE_RESCUES_HAN_FROM_JABBA", "LUKE_SKYWALKER"),
                ("JABBA_CAPTURES_LEIA_ORGANA", "LEIA_RESCUES_HAN_FROM_JABBA", "LEIA_ORGANA"),
                ("JABBA_CAPTURES_CHEWBACCA", "CHEWBACCA_RESCUES_HAN_FROM_JABBA", "CHEWBACCA"),
            };

            foreach ((string eventId, string sourceEventId, string officerId) in expectedEvents)
            {
                GameEvent gameEvent = pack.GameData.GameEvents.Single(candidate =>
                    candidate.InstanceID == eventId
                );
                ResultSourceEventConditional source = gameEvent
                    .Conditionals.OfType<ResultSourceEventConditional>()
                    .Single();
                OfficerCaptureStateConditional capture = gameEvent
                    .Conditionals.OfType<OfficerCaptureStateConditional>()
                    .Single();
                NarrativeMessageAction message = gameEvent
                    .Actions.OfType<NarrativeMessageAction>()
                    .Single();

                Assert.AreEqual(nameof(OfficerCaptureStateResult), gameEvent.TriggerResultType);
                Assert.IsTrue(gameEvent.SuppressSourceMessages);
                Assert.AreEqual(sourceEventId, source.SourceEventInstanceID);
                Assert.AreEqual(officerId, capture.OfficerInstanceID);
                Assert.IsTrue(capture.IsCaptured);
                Assert.AreEqual(officerId, message.SubjectInstanceID);
                Assert.AreEqual("Jabba Captures {subject}", message.TitleTemplate);
                Assert.AreEqual(
                    "{subject} was captured by Jabba while attempting to rescue Han Solo.",
                    message.BodyTemplate
                );
            }

            GameEvent hanCapture = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "HAN_CAPTURED_BY_BOUNTY_HUNTERS"
            );
            Assert.IsTrue(hanCapture.SuppressSourceMessages);
            Assert.AreEqual(
                "HAN_BOUNTY_HUNTERS",
                hanCapture
                    .Conditionals.OfType<ResultSourceEventConditional>()
                    .Single()
                    .SourceEventInstanceID
            );

            GameEvent reportPolicy = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "PALACE_RESCUE_REPORT_POLICY"
            );
            Assert.IsTrue(reportPolicy.IsRepeatable);
            Assert.AreEqual(nameof(MissionCompletedResult), reportPolicy.TriggerResultType);
            Assert.IsTrue(reportPolicy.SuppressTriggerMessage);
            CollectionAssert.AreEquivalent(
                expectedEvents.Select(expected => expected.sourceEventId),
                reportPolicy
                    .Conditionals.OfType<OrConditional>()
                    .Single()
                    .Conditionals.OfType<ResultSourceEventConditional>()
                    .Select(source => source.SourceEventInstanceID)
            );
        }

        [Test]
        public void OpenActive_HiddenStoryMissions_OnlyExposeAuthoredReports()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            (string eventId, string triggerType, string sourceEventId)[] replacements =
            {
                (
                    "HAN_EVADES_BOUNTY_HUNTERS",
                    nameof(StoryCaptureResolvedResult),
                    "HAN_BOUNTY_HUNTERS"
                ),
                (
                    "JABBA_DELIVERS_PRISONERS",
                    nameof(StoryPickupCompletedResult),
                    "VADER_COLLECTS_JABBAS_PRISONERS"
                ),
                (
                    "LUKE_WINS_FINAL_BATTLE",
                    nameof(StoryFinalBattleCompletedResult),
                    "VADER_TAKES_LUKE_TO_EMPEROR"
                ),
                (
                    "LUKE_LOSES_FINAL_BATTLE",
                    nameof(StoryFinalBattleCompletedResult),
                    "VADER_TAKES_LUKE_TO_EMPEROR"
                ),
            };

            foreach ((string eventId, string triggerType, string sourceEventId) in replacements)
            {
                GameEvent gameEvent = pack.GameData.GameEvents.Single(candidate =>
                    candidate.InstanceID == eventId
                );
                Assert.AreEqual(triggerType, gameEvent.TriggerResultType, eventId);
                Assert.IsTrue(gameEvent.SuppressSourceMessages, eventId);
                Assert.AreEqual(
                    sourceEventId,
                    gameEvent
                        .Conditionals.OfType<ResultSourceEventConditional>()
                        .Single()
                        .SourceEventInstanceID,
                    eventId
                );
            }

            GameEvent finalBattlePolicy = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "FINAL_BATTLE_REPORT_POLICY"
            );
            Assert.IsTrue(finalBattlePolicy.IsRepeatable);
            Assert.AreEqual(nameof(MissionCompletedResult), finalBattlePolicy.TriggerResultType);
            Assert.IsTrue(finalBattlePolicy.SuppressTriggerMessage);
            Assert.AreEqual(
                "VADER_TAKES_LUKE_TO_EMPEROR",
                finalBattlePolicy
                    .Conditionals.OfType<ResultSourceEventConditional>()
                    .Single()
                    .SourceEventInstanceID
            );
        }

        [Test]
        public void OpenActive_JediConfrontations_ReplaceGenericOfficerStateReports()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            (
                string effectsEventId,
                string sourceEventId,
                string subjectId,
                string opponentId
            )[] encounters =
            {
                (
                    "LUKE_VADER_ENCOUNTER_EFFECTS",
                    "LUKE_ENCOUNTERS_VADER",
                    "LUKE_SKYWALKER",
                    "DARTH_VADER"
                ),
                (
                    "LUKE_PALPATINE_ENCOUNTER_EFFECTS",
                    "LUKE_ENCOUNTERS_PALPATINE",
                    "LUKE_SKYWALKER",
                    "EMPEROR_PALPATINE"
                ),
                (
                    "LEIA_VADER_ENCOUNTER_EFFECTS",
                    "LEIA_ENCOUNTERS_VADER",
                    "LEIA_ORGANA",
                    "DARTH_VADER"
                ),
                (
                    "LEIA_PALPATINE_ENCOUNTER_EFFECTS",
                    "LEIA_ENCOUNTERS_PALPATINE",
                    "LEIA_ORGANA",
                    "EMPEROR_PALPATINE"
                ),
            };

            foreach (
                (
                    string effectsEventId,
                    string sourceEventId,
                    string subjectId,
                    string opponentId
                ) in encounters
            )
            {
                GameEvent gameEvent = pack.GameData.GameEvents.Single(candidate =>
                    candidate.InstanceID == effectsEventId
                );
                Assert.AreEqual(nameof(OfficerEncounterResult), gameEvent.TriggerResultType);
                Assert.IsTrue(gameEvent.SuppressSourceMessages, effectsEventId);
                Assert.AreEqual(
                    sourceEventId,
                    gameEvent
                        .Conditionals.OfType<ResultSourceEventConditional>()
                        .Single()
                        .SourceEventInstanceID,
                    effectsEventId
                );
                OfficerEncounterParticipantsConditional participants = gameEvent
                    .Conditionals.OfType<OfficerEncounterParticipantsConditional>()
                    .Single();
                Assert.AreEqual(subjectId, participants.EncounteredOfficerInstanceID);
                Assert.AreEqual(opponentId, participants.OpposingOfficerInstanceID);
                NarrativeMessageAction report = gameEvent
                    .Actions.OfType<NarrativeMessageAction>()
                    .Single();
                Assert.AreEqual(subjectId, report.SubjectInstanceID);
                Assert.AreEqual(opponentId, report.RelatedSubjectInstanceID);
                Assert.AreEqual(5, report.BodySegments.Count);
            }
        }

        [Test]
        public void OpenActive_ClassicStoryEvents_PreserveHeritageAndFinalBattleOutcomes()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            GameEvent heritage = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "LUKE_DISCOVERS_HERITAGE"
            );
            StartScriptedTrainingAction dagobahTraining = pack
                .GameData.GameEvents.Single(gameEvent => gameEvent.InstanceID == "LUKE_VISITS_YODA")
                .Actions.OfType<StartScriptedTrainingAction>()
                .Single();
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
            ConditionalAction firstLukeVaderInjury = lukeVaderEffects
                .Actions.OfType<ConditionalAction>()
                .Single();
            IncreaseOfficerForceAction lukeVaderForceIncrease = lukeVaderEffects
                .Actions.OfType<IncreaseOfficerForceAction>()
                .Single();
            GameEvent lukePalpatineEffects = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "LUKE_PALPATINE_ENCOUNTER_EFFECTS"
            );
            IncreaseOfficerForceAction lukePalpatineForceIncrease = lukePalpatineEffects
                .Actions.OfType<IncreaseOfficerForceAction>()
                .Single();
            NarrativeMessageAction confrontation = lukeVaderEffects
                .Actions.OfType<NarrativeMessageAction>()
                .Single();
            GameEvent forceDetectionEvent = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "FORCE_USERS_DETECT_ENEMIES"
            );
            ReportForceDetectionAction forceDetection = forceDetectionEvent
                .Actions.OfType<ReportForceDetectionAction>()
                .Single();
            StartStoryCaptureAction bountyCapture = pack
                .GameData.GameEvents.Single(gameEvent =>
                    gameEvent.InstanceID == "HAN_BOUNTY_HUNTERS"
                )
                .Actions.OfType<StartStoryCaptureAction>()
                .Single();
            string[] recurringEncounterEventIds =
            {
                "LUKE_ENCOUNTERS_VADER",
                "LUKE_VADER_ENCOUNTER_EFFECTS",
                "LUKE_ENCOUNTERS_PALPATINE",
                "LUKE_PALPATINE_ENCOUNTER_EFFECTS",
                "LEIA_ENCOUNTERS_VADER",
                "LEIA_VADER_ENCOUNTER_EFFECTS",
                "LEIA_ENCOUNTERS_PALPATINE",
                "LEIA_PALPATINE_ENCOUNTER_EFFECTS",
            };
            GameEvent leiaVaderEffects = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "LEIA_VADER_ENCOUNTER_EFFECTS"
            );
            ConditionalAction leiaHeritageEffects = leiaVaderEffects
                .Actions.OfType<ConditionalAction>()
                .Single();
            ForceDiscoveryRule leiaDiscoveryRule = pack
                .GameData.GameEvents.OfType<ForceDiscoveryRule>()
                .Single();
            GameEvent leiaHeritage = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "LEIA_DISCOVERS_HERITAGE"
            );

            Assert.AreEqual(6, heritageMessage.BodySegments.Count);
            Assert.AreEqual(100, dagobahTraining.DurationTicks);
            Assert.AreEqual(60, dagobahTraining.CompletionBonusPercent);
            Assert.AreEqual(2, dagobahTraining.InterruptionProgressDivisor);
            Assert.IsTrue(
                recurringEncounterEventIds.All(instanceId =>
                    pack.GameData.GameEvents.Single(gameEvent =>
                        gameEvent.InstanceID == instanceId
                    ).IsRepeatable
                )
            );
            Assert.AreEqual(nameof(UnitArrivedResult), lukeVaderEncounter.TriggerResultType);
            Assert.IsInstanceOf<OfficerPairArrivalConditional>(lukeVaderEncounter.Conditionals[0]);
            Assert.AreEqual(
                "luke.vader.encountered",
                firstLukeVaderInjury.Conditionals.OfType<EventVariableConditional>().Single().Key
            );
            Assert.AreEqual("DARTH_VADER", lukeVaderForceIncrease.ReferenceOfficerInstanceID);
            Assert.AreEqual(
                "EMPEROR_PALPATINE",
                lukePalpatineForceIncrease.ReferenceOfficerInstanceID
            );
            Assert.AreEqual(5, confrontation.BodySegments.Count);
            Assert.IsTrue(confrontation.VoicePathFromOfficerEncounter);
            Assert.AreEqual(nameof(UnitArrivedResult), forceDetectionEvent.TriggerResultType);
            Assert.AreEqual("{subject} Detects Enemy", forceDetection.TitleTemplate);
            Assert.AreEqual(
                "{subject} has detected {relatedSubject} because of a disturbance in the Force.",
                forceDetection.BodyTemplate
            );
            Assert.AreEqual(4, forceDetection.ExcludedPairs.Count);
            Assert.AreEqual(0, bountyCapture.AttackRating);
            Assert.AreEqual("LEIA_ORGANA", leiaDiscoveryRule.CandidateOfficerInstanceID);
            Assert.AreEqual("LUKE_SKYWALKER", leiaDiscoveryRule.DiscovererOfficerInstanceID);
            Assert.AreEqual(nameof(ForceDiscoveryResult), leiaHeritage.TriggerResultType);
            Assert.IsInstanceOf<ForceDiscoveryParticipantsConditional>(
                leiaHeritage.Conditionals.Single()
            );
            Assert.AreEqual(
                1,
                leiaHeritageEffects.Actions.OfType<IncreaseOfficerForceAction>().Count()
            );
            Assert.IsEmpty(leiaHeritageEffects.Actions.OfType<RevealOfficerForcePotentialAction>());
            Assert.AreEqual(OfficerRating.Combat, bountyCapture.ResistanceRating);
            Assert.AreEqual(AbductionMission.MissionTypeID, bountyCapture.ProbabilityTableKey);
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

        [Test]
        public void OpenActive_EmperorReturnsToCoruscant_PreservesOriginalReport()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            GameEvent gameEvent = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "EMPEROR_RETURNS_TO_CORUSCANT"
            );
            UnitArrivalConditional arrival = gameEvent
                .Conditionals.OfType<UnitArrivalConditional>()
                .Single();
            NarrativeMessageAction message = gameEvent
                .Actions.OfType<NarrativeMessageAction>()
                .Single();
            FactionOfficerRatingAuraEffect aura = gameEvent
                .Effects.OfType<FactionOfficerRatingAuraEffect>()
                .Single();

            Assert.IsTrue(gameEvent.IsRepeatable);
            Assert.AreEqual(nameof(UnitArrivedResult), gameEvent.TriggerResultType);
            Assert.AreEqual("EMPEROR_PALPATINE", arrival.UnitInstanceID);
            Assert.AreEqual("CORUSCANT", arrival.DestinationInstanceID);
            Assert.AreEqual("EMPEROR_PALPATINE", aura.SourceUnitInstanceID);
            Assert.AreEqual("CORUSCANT", aura.LocationInstanceID);
            Assert.AreEqual("FNEMP1", aura.AffectedFactionInstanceID);
            Assert.AreEqual(OfficerRating.Leadership, aura.Rating);
            Assert.AreEqual(50, aura.Amount);
            Assert.AreEqual("Emperor Arrives at Coruscant", message.TitleTemplate);
            Assert.AreEqual(
                "I have returned to the Seat of Power at Coruscant.",
                message.BodyTemplate
            );
            Assert.AreEqual(
                "Pack/Factions/Empire/Units/Officers/OFEM001/Voice/seat-of-power-01",
                message.OfficerVoicePath
            );
        }

        [Test]
        public void OpenActive_AssassinationDeathReport_PreservesOriginalVictimMessage()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            MessageDefinition definition = pack.GameData.MessageDefinitions.Single(candidate =>
                candidate.ResultType == MessageResultType.OfficerAssassinated
            );

            Assert.AreEqual("{officer} Killed", definition.TitleTemplate);
            Assert.AreEqual(
                "{officer} was killed by Imperial Assassins at {system}.",
                definition.BodyTemplate
            );
            Assert.AreEqual(
                "Pack/Factions/Alliance/Strategy/UI/Messages/character-killed",
                definition.ImagePaths["FNALL1"]
            );
        }

        [Test]
        public void OpenActive_SpaceBattleReports_PreserveOriginalModdableNarrativeCatalog()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            MessageDefinition[] definitions = pack
                .GameData.MessageDefinitions.Where(candidate =>
                    candidate.ResultType == MessageResultType.SpaceBattle
                )
                .ToArray();

            Assert.AreEqual(3, definitions.Length);
            Assert.IsTrue(definitions.All(definition => definition.SpaceBattleNarrative != null));
            SpaceBattleNarrativeTemplates narrative = definitions
                .Single(definition => definition.Outcome == MessageResultOutcome.Victory)
                .SpaceBattleNarrative;
            Assert.AreEqual(
                "The {fleetFaction} fleet has been completely destroyed.",
                narrative.FleetDestroyed
            );
            Assert.AreEqual(
                "The {fleetFaction} fleet has withdrawn to {retreatSystem}.",
                narrative.FleetWithdrawnTo
            );
            Assert.AreEqual(
                "All {faction} and {opponent} ships have been destroyed.",
                narrative.AllShipsDestroyed
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
