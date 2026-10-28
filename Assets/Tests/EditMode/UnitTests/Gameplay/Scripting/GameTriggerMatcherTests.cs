using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Game.Units;
using Rebellion.Gameplay.Scripting;
using Rebellion.SceneGraph;

namespace Rebellion.Tests.Gameplay.Scripting
{
    [TestFixture]
    public class GameTriggerMatcherTests
    {
        [Test]
        public void Matches_PlanetOwnershipChangedTrigger_AppliesOwnershipFilters()
        {
            PlanetOwnershipChangedTrigger trigger = new PlanetOwnershipChangedTrigger
            {
                PlanetInstanceID = "planet",
                NewOwnerFactionInstanceID = "alliance",
                Reason = PlanetOwnershipChangeReason.PopularSupport,
            };
            PlanetOwnershipChangedResult result = new PlanetOwnershipChangedResult
            {
                Planet = new Planet { InstanceID = "planet" },
                NewOwner = new Faction { InstanceID = "alliance" },
                Reason = PlanetOwnershipChangeReason.PopularSupport,
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
            result.Reason = PlanetOwnershipChangeReason.None;
            Assert.IsFalse(GameTriggerMatcher.Matches(trigger, result));
        }

        [Test]
        public void Matches_IntelligenceRevealedTrigger_AppliesRecipientAndObservationFilters()
        {
            IntelligenceRevealedTrigger trigger = new IntelligenceRevealedTrigger
            {
                RecipientFactionInstanceID = "alliance",
                ObservationInstanceID = "planet",
            };
            IntelligenceRevealedResult result = new IntelligenceRevealedResult
            {
                Recipient = new Faction { InstanceID = "alliance" },
                Observations = new List<ISceneNode> { new Planet { InstanceID = "planet" } },
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
            result.Observations.Clear();
            Assert.IsFalse(GameTriggerMatcher.Matches(trigger, result));
        }

        [Test]
        public void Matches_MaintenanceRequiredTrigger_AppliesFactionFilter()
        {
            MaintenanceRequiredTrigger trigger = new MaintenanceRequiredTrigger
            {
                FactionInstanceID = "alliance",
            };
            MaintenanceRequiredResult result = new MaintenanceRequiredResult
            {
                Faction = new Faction { InstanceID = "alliance" },
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
            result.Faction.InstanceID = "empire";
            Assert.IsFalse(GameTriggerMatcher.Matches(trigger, result));
        }

        [Test]
        public void Matches_MissionStartedTrigger_AppliesTypeAndParticipantFilters()
        {
            Officer officer = new Officer { InstanceID = "officer" };
            MissionStartedTrigger trigger = new MissionStartedTrigger
            {
                MissionTypeID = DiplomacyMission.MissionTypeID,
                Participants = new MissionParticipantFilter
                {
                    Units = new List<GameUnitReference>
                    {
                        new GameUnitReference { UnitInstanceID = officer.InstanceID },
                    },
                },
            };
            MissionStartedResult result = new MissionStartedResult
            {
                MissionTypeID = DiplomacyMission.MissionTypeID,
                Participants = new List<IMissionParticipant> { officer },
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
            result.Participants.Clear();
            Assert.IsFalse(GameTriggerMatcher.Matches(trigger, result));
        }

        [Test]
        public void Matches_OfficerCaptureChangedTrigger_AppliesOfficerAndStateFilters()
        {
            OfficerCaptureChangedTrigger trigger = new OfficerCaptureChangedTrigger
            {
                OfficerInstanceID = "han",
                IsCaptured = true,
            };
            OfficerCaptureStateResult result = new OfficerCaptureStateResult
            {
                TargetOfficer = new Officer { InstanceID = "han" },
                IsCaptured = true,
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
            result.IsCaptured = false;
            Assert.IsFalse(GameTriggerMatcher.Matches(trigger, result));
        }

        [Test]
        public void Matches_ForceDiscoveryChangedTrigger_AppliesOfficerAndEventTypeFilters()
        {
            ForceDiscoveryChangedTrigger trigger = new ForceDiscoveryChangedTrigger
            {
                OfficerInstanceID = "luke",
                EventType = ForceEventType.ForceUserDiscovered,
            };
            ForceDiscoveryResult result = new ForceDiscoveryResult
            {
                Officer = new Officer { InstanceID = "luke" },
                EventType = ForceEventType.ForceUserDiscovered,
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
            result.EventType = ForceEventType.DiscoveringForceUser;
            Assert.IsFalse(GameTriggerMatcher.Matches(trigger, result));
        }

        [Test]
        public void Matches_UnitArrivedTrigger_AppliesIdentityAndDestinationFilters()
        {
            UnitArrivedTrigger trigger = new UnitArrivedTrigger
            {
                UnitInstanceID = "officer",
                DestinationInstanceID = "planet",
            };
            UnitArrivedResult result = new UnitArrivedResult
            {
                Unit = new Officer { InstanceID = "officer" },
                Destination = new Planet { InstanceID = "planet" },
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
            result.Destination.InstanceID = "elsewhere";
            Assert.IsFalse(GameTriggerMatcher.Matches(trigger, result));
        }

        [TestCaseSource(nameof(UnitDestructionResults))]
        public void Matches_UnitDestroyedTrigger_CoversEveryDestructionPath(
            GameObjectDestroyedResult result,
            UnitDestructionReason reason
        )
        {
            UnitDestroyedTrigger trigger = new UnitDestroyedTrigger
            {
                UnitInstanceID = "unit",
                Reason = reason,
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
        }

        [Test]
        public void Matches_DuelCompletedTrigger_AppliesOfficerAndSourceFilters()
        {
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                FirstOfficerInstanceID = "luke",
                SecondOfficerInstanceID = "vader",
                SourceEventInstanceID = "encounter",
            };
            DuelResult result = new DuelResult
            {
                EncounteredOfficer = new Officer { InstanceID = "luke" },
                OpposingOfficer = new Officer { InstanceID = "vader" },
                SourceEventInstanceID = "encounter",
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
            result.SourceEventInstanceID = "other";
            Assert.IsFalse(GameTriggerMatcher.Matches(trigger, result));
        }

        [Test]
        public void Matches_BombardmentCompletedTrigger_AppliesOutcomeFilters()
        {
            BombardmentCompletedTrigger trigger = new BombardmentCompletedTrigger
            {
                PlanetInstanceID = "planet",
                Type = BombardmentType.DestroyPlanet,
                PlanetDestroyed = true,
            };
            BombardmentResult result = new BombardmentResult
            {
                Planet = new Planet { InstanceID = "planet" },
                Type = BombardmentType.DestroyPlanet,
                PlanetDestroyed = true,
            };

            Assert.IsTrue(GameTriggerMatcher.Matches(trigger, result));
            result.PlanetDestroyed = false;
            Assert.IsFalse(GameTriggerMatcher.Matches(trigger, result));
        }

        /// <summary>
        /// Executes unit destruction results.
        /// </summary>
        /// <returns>The result of unit destruction results.</returns>
        private static IEnumerable<TestCaseData> UnitDestructionResults()
        {
            Officer unit = new Officer { InstanceID = "unit" };
            yield return new TestCaseData(
                new GameObjectDestroyedResult
                {
                    DestroyedObject = unit,
                    Reason = UnitDestructionReason.Direct,
                },
                UnitDestructionReason.Direct
            );
            yield return new TestCaseData(
                new GameObjectDestroyedOnArrivalResult { DestroyedObject = unit },
                UnitDestructionReason.Arrival
            );
            yield return new TestCaseData(
                new GameObjectAutoscrappedResult { DestroyedObject = unit },
                UnitDestructionReason.Maintenance
            );
            yield return new TestCaseData(
                new GameObjectSabotagedResult { DestroyedObject = unit },
                UnitDestructionReason.Sabotage
            );
        }
    }
}
