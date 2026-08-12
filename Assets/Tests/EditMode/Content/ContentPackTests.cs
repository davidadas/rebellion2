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

            Assert.IsEmpty(gameEvent.Triggers);
            Assert.AreEqual("LUKE_VISITS_YODA", gameEvent.Schedule.After.EventInstanceID);
            Assert.AreEqual(100, gameEvent.Schedule.After.DelayTicks);
            Assert.AreEqual(
                "LUKE_VISITS_YODA",
                gameEvent.Conditionals.OfType<IsEventCompleteConditional>().Single().EventInstanceID
            );
            Assert.IsTrue(gameEvent.Actions.OfType<RemoveFromVoidAction>().Any());
            SendMessageAction message = gameEvent.Actions.OfType<SendMessageAction>().Single();
            Assert.AreEqual("LUKE_SKYWALKER", message.SubjectInstanceID);
            Assert.AreEqual("Luke Leaves Dagobah", message.Subject);
            Assert.AreEqual("I have finished my training with Yoda.", message.Body);
            SetOfficerImageSetAction presentation = gameEvent
                .Actions.OfType<SetOfficerImageSetAction>()
                .Single();
            Assert.AreEqual("LUKE_SKYWALKER", presentation.OfficerInstanceID);
            StringAssert.EndsWith("/jedi-display", presentation.ImageSet.DisplayImagePath);
            SetOfficerVoiceSetAction voiceSet = gameEvent
                .Actions.OfType<SetOfficerVoiceSetAction>()
                .Single();
            Assert.AreEqual("LUKE_SKYWALKER", voiceSet.OfficerInstanceID);
            StringAssert.EndsWith(
                "/advanced-personnel-arrived-01",
                voiceSet.VoiceSet.PersonnelArrived.First()
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
                EvaluateBindingConditional source =
                    gameEvent.Conditionals.Single(condition =>
                        condition is EvaluateBindingConditional binding
                        && binding.Binding == "$sourceEvent"
                    ) as EvaluateBindingConditional;
                EvaluateBindingConditional outcome =
                    gameEvent.Conditionals.Single(condition =>
                        condition is EvaluateBindingConditional binding
                        && binding.Binding == "$outcome"
                    ) as EvaluateBindingConditional;
                SetCaptivityAction capture = gameEvent
                    .Actions.OfType<SetCaptivityAction>()
                    .Single();
                SendMessageAction message = gameEvent.Actions.OfType<SendMessageAction>().Single();

                Assert.AreEqual("core:mission.completed", gameEvent.Triggers.Single().Event);
                SuppressNextAutomaticMessageAction suppression = gameEvent
                    .Actions.OfType<SuppressNextAutomaticMessageAction>()
                    .Single();
                Assert.AreEqual(MessageResultType.OfficerCaptured, suppression.MessageType);
                Assert.AreEqual("FNALL1", suppression.RecipientFactionInstanceID);
                Assert.AreEqual(sourceEventId, source.ExpectedValue);
                Assert.AreEqual("Failed", outcome.ExpectedValue);
                Assert.AreEqual(officerId, capture.OfficerInstanceID);
                Assert.AreEqual(officerId, message.SubjectInstanceID);
                Assert.AreEqual("Jabba Captures {subject}", message.Subject);
                Assert.AreEqual(
                    "{subject} was captured by Jabba while attempting to rescue Han Solo.",
                    message.Body
                );
            }

            GameEvent hanCapture = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "HAN_CAPTURED_BY_BOUNTY_HUNTERS"
            );
            Assert.AreEqual(
                MessageResultType.OfficerCaptured,
                hanCapture
                    .Actions.OfType<SuppressNextAutomaticMessageAction>()
                    .Single(action => action.MessageType == MessageResultType.OfficerCaptured)
                    .MessageType
            );
            Assert.AreEqual(
                "HAN_BOUNTY_HUNTERS",
                hanCapture
                    .Conditionals.OfType<EvaluateBindingConditional>()
                    .Single(binding => binding.Binding == "$sourceEvent")
                    .ExpectedValue
            );

            GameEvent reportPolicy = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "PALACE_RESCUE_REPORT_POLICY"
            );
            Assert.IsTrue(reportPolicy.Repeats);
            Assert.AreEqual("core:mission.completed", reportPolicy.Triggers.Single().Event);
            Assert.AreEqual(
                MessageResultType.MissionReport,
                reportPolicy
                    .Actions.OfType<SuppressNextAutomaticMessageAction>()
                    .Single()
                    .MessageType
            );
            CollectionAssert.AreEquivalent(
                expectedEvents.Select(expected => expected.sourceEventId),
                reportPolicy
                    .Conditionals.OfType<AnyConditional>()
                    .Single()
                    .Conditionals.OfType<EvaluateBindingConditional>()
                    .Select(source => source.ExpectedValue)
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
                Assert.AreEqual("core:duel.completed", gameEvent.Triggers.Single().Event);
                Assert.IsEmpty(
                    gameEvent.Actions.OfType<SuppressNextAutomaticMessageAction>(),
                    effectsEventId
                );
                EvaluateBindingConditional[] bindings = gameEvent
                    .Conditionals.OfType<EvaluateBindingConditional>()
                    .ToArray();
                Assert.AreEqual(
                    sourceEventId,
                    bindings.Single(binding => binding.Binding == "$sourceEvent").ExpectedValue,
                    effectsEventId
                );
                Assert.AreEqual(
                    subjectId,
                    bindings.Single(binding => binding.Binding == "$officer").ExpectedValue
                );
                Assert.AreEqual(
                    opponentId,
                    bindings.Single(binding => binding.Binding == "$opponent").ExpectedValue
                );
                SendMessageAction report = gameEvent.Actions.OfType<SendMessageAction>().Single();
                Assert.AreEqual(subjectId, report.SubjectInstanceID);
                Assert.AreEqual(opponentId, report.RelatedSubjectInstanceID);
                Assert.AreEqual(5, report.ConditionalBodies.Count);
            }
        }

        [Test]
        public void OpenActive_FinalBattle_UsesMovementInsteadOfMissionDefinitions()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            GameEvent gather = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "VADER_TAKES_LUKE_TO_EMPEROR"
            );
            GameEvent escort = pack.GameData.GameEvents.Single(gameEvent =>
                gameEvent.InstanceID == "VADER_REACHES_LUKE"
            );
            RequestMovementAction gatherMovement = gather
                .Actions.OfType<RequestMovementAction>()
                .Single();
            RequestMovementAction escortMovement = escort
                .Actions.OfType<RequestMovementAction>()
                .Single();

            Assert.AreEqual("DARTH_VADER", gatherMovement.UnitInstanceID);
            Assert.AreEqual("LUKE_SKYWALKER", gatherMovement.DestinationUnitInstanceID);
            Assert.AreEqual("EMPEROR_PALPATINE", escortMovement.DestinationUnitInstanceID);
            CollectionAssert.AreEquivalent(
                new[] { "DARTH_VADER", "LUKE_SKYWALKER" },
                escortMovement.Units.Select(unit => unit.UnitInstanceID)
            );
            Assert.IsFalse(
                pack.GameData.MissionDefinitions.Any(definition =>
                    definition.InstanceID == "GATHER_LUKE_FOR_FINAL_BATTLE"
                    || definition.InstanceID == "ESCORT_LUKE_TO_FINAL_BATTLE"
                )
            );
        }

        [Test]
        public void OpenActive_EmperorReturnsToCoruscant_PreservesConfiguredReport()
        {
            ContentPack pack = ContentPackLoader.OpenActive();
            GameEvent gameEvent = pack.GameData.GameEvents.Single(candidate =>
                candidate.InstanceID == "EMPEROR_RETURNS_TO_CORUSCANT"
            );
            EvaluateBindingConditional[] arrival = gameEvent
                .Conditionals.OfType<EvaluateBindingConditional>()
                .ToArray();
            SendMessageAction message = gameEvent.Actions.OfType<SendMessageAction>().Single();
            Assert.IsTrue(gameEvent.Repeats);
            Assert.AreEqual("core:unit.arrived", gameEvent.Triggers.Single().Event);
            Assert.AreEqual(
                "EMPEROR_PALPATINE",
                arrival.Single(binding => binding.Binding == "$unit").ExpectedValue
            );
            Assert.AreEqual(
                "CORUSCANT",
                arrival.Single(binding => binding.Binding == "$destination").ExpectedValue
            );
            Assert.AreEqual("Emperor Arrives at Coruscant", message.Subject);
            Assert.AreEqual("I have returned to the Seat of Power at Coruscant.", message.Body);
            Assert.AreEqual(
                "Pack/Factions/Empire/Units/Officers/OFEM001/Voice/seat-of-power-01",
                message.OfficerVoice.Path
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
