using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTriggerTests
    {
        [Test]
        public void Triggers_AuthoredContracts_RoundTripConcreteTypesAndBindings()
        {
            GameEvent gameEvent = new GameEvent
            {
                Triggers = new List<GameEventTrigger>
                {
                    new PlanetOwnershipChangedTrigger(),
                    new ResearchAdvancedTrigger(),
                    new MissionCompletedTrigger(),
                    new OfficerCaptureChangedTrigger(),
                    new UnitOwnershipChangedTrigger(),
                    new SpaceCombatCompletedTrigger(),
                    new ManufacturingCompletedTrigger
                    {
                        Bindings = new List<GameEventBinding>
                        {
                            new GameEventBinding { Argument = "DeployedObject", As = "unit" },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            CollectionAssert.AreEqual(
                gameEvent.Triggers.Select(trigger => trigger.GetType()),
                restored.Triggers.Select(trigger => trigger.GetType())
            );
            GameEventBinding binding = restored.Triggers.Last().Bindings.Single();
            Assert.AreEqual("DeployedObject", binding.Argument);
            Assert.AreEqual("unit", binding.As);
            Assert.IsFalse(xml.Contains("Trigger>"));
        }

        #region Planet

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

            Assert.IsTrue(trigger.Matches(result));
            result.Reason = PlanetOwnershipChangeReason.None;
            Assert.IsFalse(trigger.Matches(result));
        }

        #endregion

        #region Officer

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

            Assert.IsTrue(trigger.Matches(result));
            result.IsCaptured = false;
            Assert.IsFalse(trigger.Matches(result));
        }

        #endregion

        #region Unit Lifecycle

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

            Assert.IsTrue(trigger.Matches(result));
            result.Destination.InstanceID = "elsewhere";
            Assert.IsFalse(trigger.Matches(result));
        }

        #endregion

        #region Combat

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

            Assert.IsTrue(trigger.Matches(result));
            result.SourceEventInstanceID = "other";
            Assert.IsFalse(trigger.Matches(result));
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

            Assert.IsTrue(trigger.Matches(result));
            result.PlanetDestroyed = false;
            Assert.IsFalse(trigger.Matches(result));
        }

        #endregion

        [Test]
        public void Bind_TriggerArgument_ExposesOnlyAuthoredValue()
        {
            Officer officer = new Officer { InstanceID = "luke" };
            DuelResult result = new DuelResult { EncounteredOfficer = officer };
            DuelCompletedTrigger trigger = new DuelCompletedTrigger
            {
                Bindings = new List<GameEventBinding>
                {
                    new GameEventBinding { Argument = "FirstOfficer", As = "officer" },
                },
            };

            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState(),
                result,
                trigger
            );

            Assert.AreSame(officer, context.GetBinding<Officer>("officer"));
        }

        [Test]
        public void GetBindingReference_PropertyTraversal_ThrowsInvalidOperationException()
        {
            GameEventEvaluationContext context = new GameEventEvaluationContext(
                new GameEvent(),
                new GameEventState()
            );
            context.Bind("officer", new Officer { InstanceID = "luke" });

            Assert.Throws<System.InvalidOperationException>(() =>
                context.GetBindingReference<string>("$officer.InstanceID")
            );
        }
    }
}
