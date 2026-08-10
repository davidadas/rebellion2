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
        public void OpenActive_PlayableFactionThemes_ConfigureCampaignEndings()
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
        public void OpenActive_DagobahCompletion_UsesScheduledVoidWorkflow()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            GameEvent gameEvent = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "LUKE_LEAVES_DAGOBAH"
            );

            Assert.IsNull(gameEvent.Trigger);
            Assert.AreEqual("LUKE_VISITS_YODA", gameEvent.Schedule.After.EventInstanceID);
            Assert.AreEqual(100, gameEvent.Schedule.After.DelayTicks);
            Assert.AreEqual(
                "LUKE_VISITS_YODA",
                gameEvent.Conditionals.OfType<IsEventCompleteConditional>().Single().EventInstanceID
            );
            Assert.IsTrue(gameEvent.Actions.OfType<ReturnFromVoidAction>().Any());
            SendMessageAction message = gameEvent.Actions.OfType<SendMessageAction>().Single();
            Assert.AreEqual("LUKE_SKYWALKER", message.SubjectInstanceID);
            Assert.AreEqual("Luke Leaves Dagobah", message.Title);
            Assert.AreEqual("I have finished my training with Yoda.", message.Body);
            SetOfficerImagesAction presentation = gameEvent
                .Actions.OfType<SetOfficerImagesAction>()
                .Single();
            Assert.AreEqual("LUKE_SKYWALKER", presentation.OfficerInstanceID);
            StringAssert.EndsWith("/jedi-display", presentation.DisplayImagePath);
            SetOfficerVoiceSetAction voiceSet = gameEvent
                .Actions.OfType<SetOfficerVoiceSetAction>()
                .Single();
            Assert.AreEqual("LUKE_SKYWALKER", voiceSet.OfficerInstanceID);
            StringAssert.EndsWith(
                "/advanced-personnel-arrived-01",
                voiceSet.PersonnelArrivedVoicePaths.First()
            );
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
                TriggeredByConditional source = gameEvent
                    .Conditionals.OfType<TriggeredByConditional>()
                    .Single();
                OfficerCapturedConditional capture = gameEvent
                    .Conditionals.OfType<OfficerCapturedConditional>()
                    .Single();
                SendMessageAction message = gameEvent.Actions.OfType<SendMessageAction>().Single();

                Assert.AreEqual("core:officer.capture-changed", gameEvent.Trigger);
                Assert.IsTrue(gameEvent.SuppressSourceMessages);
                Assert.AreEqual(sourceEventId, source.EventInstanceID);
                Assert.AreEqual(officerId, capture.OfficerInstanceID);
                Assert.AreEqual(officerId, message.SubjectInstanceID);
                Assert.AreEqual("Jabba Captures {subject}", message.Title);
                Assert.AreEqual(
                    "{subject} was captured by Jabba while attempting to rescue Han Solo.",
                    message.Body
                );
            }

            GameEvent hanCapture = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "HAN_CAPTURED_BY_BOUNTY_HUNTERS"
            );
            Assert.IsTrue(hanCapture.SuppressSourceMessages);
            Assert.AreEqual(
                "HAN_BOUNTY_HUNTERS",
                hanCapture.Conditionals.OfType<TriggeredByConditional>().Single().EventInstanceID
            );

            GameEvent reportPolicy = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "PALACE_RESCUE_REPORT_POLICY"
            );
            Assert.IsTrue(reportPolicy.Repeats);
            Assert.AreEqual("core:mission.completed", reportPolicy.Trigger);
            Assert.IsTrue(reportPolicy.SuppressTriggerMessage);
            CollectionAssert.AreEquivalent(
                expectedEvents.Select(expected => expected.sourceEventId),
                reportPolicy
                    .Conditionals.OfType<OrConditional>()
                    .Single()
                    .Conditionals.OfType<TriggeredByConditional>()
                    .Select(source => source.EventInstanceID)
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
                    "core:officer.capture-attempted",
                    "HAN_BOUNTY_HUNTERS"
                ),
                (
                    "JABBA_DELIVERS_PRISONERS",
                    "core:prisoner-pickup.completed",
                    "VADER_COLLECTS_JABBAS_PRISONERS"
                ),
                (
                    "LUKE_WINS_FINAL_BATTLE",
                    "core:force-confrontation.completed",
                    "VADER_TAKES_LUKE_TO_EMPEROR"
                ),
                (
                    "LUKE_LOSES_FINAL_BATTLE",
                    "core:force-confrontation.completed",
                    "VADER_TAKES_LUKE_TO_EMPEROR"
                ),
            };

            foreach ((string eventId, string triggerType, string sourceEventId) in replacements)
            {
                GameEvent gameEvent = pack.GameData.GameEvents.Single(candidate =>
                    candidate.InstanceID == eventId
                );
                Assert.AreEqual(triggerType, gameEvent.Trigger, eventId);
                Assert.IsTrue(gameEvent.SuppressSourceMessages, eventId);
                Assert.AreEqual(
                    sourceEventId,
                    gameEvent
                        .Conditionals.OfType<TriggeredByConditional>()
                        .Single()
                        .EventInstanceID,
                    eventId
                );
            }

            GameEvent finalBattlePolicy = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "FINAL_BATTLE_REPORT_POLICY"
            );
            Assert.IsTrue(finalBattlePolicy.Repeats);
            Assert.AreEqual("core:mission.completed", finalBattlePolicy.Trigger);
            Assert.IsTrue(finalBattlePolicy.SuppressTriggerMessage);
            Assert.AreEqual(
                "VADER_TAKES_LUKE_TO_EMPEROR",
                finalBattlePolicy
                    .Conditionals.OfType<TriggeredByConditional>()
                    .Single()
                    .EventInstanceID
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
                Assert.AreEqual("core:officer.encountered", gameEvent.Trigger);
                Assert.IsTrue(gameEvent.SuppressSourceMessages, effectsEventId);
                Assert.AreEqual(
                    sourceEventId,
                    gameEvent
                        .Conditionals.OfType<TriggeredByConditional>()
                        .Single()
                        .EventInstanceID,
                    effectsEventId
                );
                DuelIncludesConditional participants = gameEvent
                    .Conditionals.OfType<DuelIncludesConditional>()
                    .Single();
                Assert.AreEqual(subjectId, participants.FirstOfficerInstanceID);
                Assert.AreEqual(opponentId, participants.SecondOfficerInstanceID);
                SendMessageAction report = gameEvent.Actions.OfType<SendMessageAction>().Single();
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
            AfterEvent dagobahReturn = pack
                .GameData.GameEvents.Single(gameEvent =>
                    gameEvent.InstanceID == "LUKE_LEAVES_DAGOBAH"
                )
                .Schedule.After;
            AddForceExperienceAction dagobahTraining = pack
                .GameData.GameEvents.Single(gameEvent =>
                    gameEvent.InstanceID == "LUKE_LEAVES_DAGOBAH"
                )
                .Actions.OfType<AddForceExperienceAction>()
                .Single();
            SendMessageAction heritageMessage = heritage
                .Actions.OfType<SendMessageAction>()
                .Single();
            GameEvent finalBattle = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "VADER_TAKES_LUKE_TO_EMPEROR"
            );
            StartMissionAction startFinalBattle = finalBattle
                .Actions.OfType<StartMissionAction>()
                .Single();
            SendMessageAction victoryMessage = pack
                .GameData.GameEvents.Single(gameEvent =>
                    gameEvent.InstanceID == "LUKE_WINS_FINAL_BATTLE"
                )
                .Actions.OfType<SendMessageAction>()
                .Single();
            SendMessageAction defeatMessage = pack
                .GameData.GameEvents.Single(gameEvent =>
                    gameEvent.InstanceID == "LUKE_LOSES_FINAL_BATTLE"
                )
                .Actions.OfType<SendMessageAction>()
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
            SendMessageAction confrontation = lukeVaderEffects
                .Actions.OfType<SendMessageAction>()
                .Single();
            StartMissionAction bountyCapture = pack
                .GameData.GameEvents.Single(gameEvent =>
                    gameEvent.InstanceID == "HAN_BOUNTY_HUNTERS"
                )
                .Actions.OfType<StartMissionAction>()
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
            GameEvent leiaHeritage = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "LEIA_DISCOVERS_HERITAGE"
            );

            Assert.AreEqual(6, heritageMessage.BodySegments.Count);
            Assert.AreEqual(100, dagobahReturn.DelayTicks);
            Assert.AreEqual(60, dagobahTraining.PercentOfCurrentRank);
            Assert.IsTrue(
                recurringEncounterEventIds.All(instanceId =>
                    pack.GameData.GameEvents.Single(gameEvent =>
                        gameEvent.InstanceID == instanceId
                    ).Repeats
                )
            );
            Assert.AreEqual("core:mission.completed", lukeVaderEncounter.Trigger);
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
            Assert.AreEqual("BOUNTY_HUNTER_CAPTURE", bountyCapture.MissionDefinitionID);
            Assert.AreEqual(
                0,
                pack.GameData.MissionDefinitions.Single(definition =>
                    definition.InstanceID == bountyCapture.MissionDefinitionID
                ).AttackRating
            );
            EventVariableConditional leiaHeritageCondition = leiaHeritage
                .Conditionals.OfType<EventVariableConditional>()
                .Single();
            Assert.AreEqual("luke.vader.encountered", leiaHeritageCondition.Key);
            Assert.IsNull(leiaHeritage.Trigger);
            Assert.AreEqual(
                "LEIA_ORGANA",
                leiaHeritage
                    .Actions.OfType<RevealOfficerForcePotentialAction>()
                    .Single()
                    .OfficerInstanceID
            );
            Assert.AreEqual(
                "Leia Uses Force",
                leiaHeritage.Actions.OfType<SendMessageAction>().Single().Title
            );
            Assert.AreEqual(
                1,
                leiaHeritageEffects.Actions.OfType<IncreaseOfficerForceAction>().Count()
            );
            Assert.IsEmpty(leiaHeritageEffects.Actions.OfType<RevealOfficerForcePotentialAction>());
            CustomMissionDefinition bountyDefinition = pack.GameData.MissionDefinitions.Single(
                definition => definition.InstanceID == bountyCapture.MissionDefinitionID
            );
            Assert.AreEqual(OfficerRating.Combat, bountyDefinition.ResistanceRating);
            Assert.AreEqual(AbductionMission.MissionTypeID, bountyDefinition.ProbabilityTableKey);
            Assert.IsFalse(
                pack.GameData.MissionDefinitions.Single(definition =>
                    definition.InstanceID == startFinalBattle.MissionDefinitionID
                ).CaptivesCanEscapeOnVictory
            );
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
        public void OpenActive_EmperorReturnsToCoruscant_PreservesConfiguredReport()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            GameEvent gameEvent = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "EMPEROR_RETURNS_TO_CORUSCANT"
            );
            UnitArrivedConditional arrival = gameEvent
                .Conditionals.OfType<UnitArrivedConditional>()
                .Single();
            SendMessageAction message = gameEvent.Actions.OfType<SendMessageAction>().Single();
            Assert.IsTrue(gameEvent.Repeats);
            Assert.AreEqual("core:unit.arrived", gameEvent.Trigger);
            Assert.AreEqual("EMPEROR_PALPATINE", arrival.UnitInstanceID);
            Assert.AreEqual("CORUSCANT", arrival.DestinationInstanceID);
            Assert.AreEqual("Emperor Arrives at Coruscant", message.Title);
            Assert.AreEqual("I have returned to the Seat of Power at Coruscant.", message.Body);
            Assert.AreEqual(
                "Pack/Factions/Empire/Units/Officers/OFEM001/Voice/seat-of-power-01",
                message.OfficerVoicePath
            );
        }

        [Test]
        public void OpenActive_AssassinationDeathReport_PreservesVictimMessage()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            MessageDefinition definition = pack.GameData.MessageDefinitions.Single(candidate =>
                candidate.ResultType == MessageResultType.OfficerAssassinated
            );

            Assert.AreEqual("{officer} Killed", definition.Subject);
            Assert.AreEqual(
                "{officer} was killed by Imperial Assassins at {system}.",
                definition.Body
            );
            Assert.AreEqual(
                "Pack/Factions/Alliance/Strategy/UI/Messages/character-killed",
                definition.ImagePaths["FNALL1"]
            );
        }

        [Test]
        public void OpenActive_EspionageSuccessReport_PreservesAdditionalSystemText()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            MessageDefinition definition = pack.GameData.MessageDefinitions.Single(candidate =>
                candidate.ResultType == MessageResultType.MissionReport
                && candidate.Outcome == MessageResultOutcome.Success
                && candidate.MissionTypeID == MissionTypeIDs.Espionage
            );

            Assert.AreEqual(
                "My espionage mission to {system} was successful.  {details}",
                definition.Body
            );
            Assert.AreEqual(
                "In addition, information was provided on the following systems:",
                definition.DetailListHeaderTemplate
            );
            Assert.AreEqual("\n     {system}", definition.DetailListItemTemplate);
        }

        [Test]
        public void OpenActive_RegimentDeploymentReport_PreservesGroupedUnitList()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            MessageDefinition definition = pack.GameData.MessageDefinitions.Single(candidate =>
                candidate.ResultType == MessageResultType.RegimentDeployed
            );

            Assert.AreEqual("{item} Deployed to {system}", definition.Subject);
            Assert.AreEqual(
                "The following units have been deployed to {system}:\n{items}",
                definition.Body
            );
        }

        [Test]
        public void OpenActive_MaintenanceShortfallReport_PreservesGroupedUnitList()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            MessageDefinition definition = pack.GameData.MessageDefinitions.Single(candidate =>
                candidate.ResultType == MessageResultType.MaintenanceAutoscrap
            );

            Assert.AreEqual("Maintenance Shortfall", definition.Subject);
            Assert.AreEqual(
                "The following units have been destroyed at {system} due to a maintenance shortfall:\n{items}",
                definition.Body
            );
        }

        [Test]
        public void OpenActive_ClassicArrivalReports_IncludeUnitsAndMobileHeadquarters()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            MessageDefinition units = pack.GameData.MessageDefinitions.Single(candidate =>
                candidate.ResultType == MessageResultType.UnitsArrived
            );
            MessageDefinition headquarters = pack.GameData.MessageDefinitions.Single(candidate =>
                candidate.ResultType == MessageResultType.HeadquartersArrived
            );

            Assert.AreEqual("Units Arrive at {system}", units.Subject);
            Assert.AreEqual("The following units have arrived at {system}:\n{units}", units.Body);
            Assert.AreEqual("Headquarters Arrives", headquarters.Subject);
            Assert.AreEqual("FNALL1", headquarters.FactionInstanceID);
            Assert.AreEqual("The Alliance Headquarters has arrived at {system}", headquarters.Body);
        }

        [Test]
        public void OpenActive_SpaceBattleReports_PreserveModdableNarrativeCatalog()
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
