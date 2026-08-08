using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Missions;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public class GameEventCatalogValidatorTests
    {
        [Test]
        public void Validate_ValidComposableCatalog_DoesNotThrow()
        {
            GameEvent child = CreateEvent("CHILD");
            GameEvent root = CreateEvent("ROOT");
            root.InitialDelayTicks = 300;
            root.InitialDelayRandomTicks = 100;
            root.Conditionals.Add(
                new AndConditional
                {
                    Conditionals = new List<GameConditional>
                    {
                        new IsMovableConditional { ConditionalValue = "LUKE" },
                    },
                }
            );
            root.Actions.Add(new TriggerEventAction { EventInstanceID = child.InstanceID });

            Assert.DoesNotThrow(() => GameEventCatalogValidator.Validate(new[] { root, child }));
        }

        [Test]
        public void Validate_ValidForceDiscoveryRule_DoesNotThrow()
        {
            ForceDiscoveryRule rule = new ForceDiscoveryRule
            {
                InstanceID = "LEIA_DISCOVERY_RULE",
                CandidateOfficerInstanceID = "LEIA",
                DiscovererOfficerInstanceID = "LUKE",
                Conditionals = new List<GameConditional>
                {
                    new EventVariableConditional
                    {
                        Key = "luke.vader.encountered",
                        Comparison = EventVariableComparison.Equal,
                        Value = 1,
                    },
                },
            };

            Assert.DoesNotThrow(() => GameEventCatalogValidator.Validate(new[] { rule }));
        }

        [Test]
        public void Validate_MalformedForceDiscoveryRule_ReportsPolicyErrors()
        {
            ForceDiscoveryRule rule = new ForceDiscoveryRule
            {
                InstanceID = "BROKEN_RULE",
                TriggerResultType = "ForceDiscoveryResult",
                Actions = new List<GameAction> { new SetEventVariableAction() },
            };

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { rule })
            );

            StringAssert.Contains("CandidateOfficerInstanceID is required", exception.Message);
            StringAssert.Contains("cannot declare TriggerResultType", exception.Message);
            StringAssert.Contains("cannot declare actions", exception.Message);
        }

        [Test]
        public void Validate_MultipleProblems_ReportsEventSpecificAggregateError()
        {
            GameEvent broken = CreateEvent("BROKEN");
            broken.InitialDelayTicks = -1;
            broken.Conditionals.Add(new NotConditional());
            broken.Actions.Add(new TriggerEventAction { EventInstanceID = "MISSING" });

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { broken, CreateEvent("BROKEN") })
            );

            StringAssert.Contains("Event 'BROKEN'.InitialDelayTicks", exception.Message);
            StringAssert.Contains("requires exactly 1 child condition", exception.Message);
            StringAssert.Contains("defined more than once", exception.Message);
            StringAssert.Contains("triggers unknown event 'MISSING'", exception.Message);
        }

        [Test]
        public void Validate_RecursiveTriggerGraph_ReportsCycle()
        {
            GameEvent first = CreateEvent("FIRST");
            GameEvent second = CreateEvent("SECOND");
            first.Actions.Add(new TriggerEventAction { EventInstanceID = second.InstanceID });
            second.Actions.Add(new TriggerEventAction { EventInstanceID = first.InstanceID });

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { first, second })
            );

            StringAssert.Contains("Event trigger cycle", exception.Message);
        }

        [Test]
        public void Validate_NarrativeMessageWithoutRecipientOrText_ReportsBothProblems()
        {
            GameEvent gameEvent = CreateEvent("STORY");
            gameEvent.Actions.Add(new NarrativeMessageAction());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("requires RecipientFactionInstanceID", exception.Message);
            StringAssert.Contains("requires a title or body template", exception.Message);
        }

        [Test]
        public void Validate_RandomOutcomeProbabilityOutsideUnitRange_ReportsProblem()
        {
            GameEvent gameEvent = CreateEvent("RANDOM");
            gameEvent.Actions.Add(
                new RandomOutcomeAction
                {
                    Probability = 1.1,
                    Actions = new List<GameAction>
                    {
                        new ResolveOfficerEncounterAction
                        {
                            EncounteredOfficerInstanceID = "luke",
                            OpposingOfficerInstanceID = "vader",
                        },
                    },
                }
            );

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("Probability must be between 0 and 1", exception.Message);
        }

        [Test]
        public void Validate_ConditionalBranch_ValidatesNestedConditionsActionsAndReferences()
        {
            GameEvent gameEvent = CreateEvent("BRANCH");
            gameEvent.Actions.Add(
                new ConditionalAction
                {
                    Conditionals = new List<GameConditional> { new EventVariableConditional() },
                    Actions = new List<GameAction>
                    {
                        new TriggerEventAction { EventInstanceID = "MISSING" },
                    },
                    ElseActions = new List<GameAction> { new SetEventVariableAction() },
                }
            );

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("Key is required", exception.Message);
            StringAssert.Contains("triggers unknown event 'MISSING'", exception.Message);
        }

        [Test]
        public void Validate_MovementAndLocationWithoutReferences_ReportsAllMissingIds()
        {
            GameEvent gameEvent = CreateEvent("TRAVEL");
            gameEvent.Conditionals.Add(new IsAtLocationConditional());
            gameEvent.Actions.Add(new RequestMovementAction());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("UnitInstanceID is required", exception.Message);
            StringAssert.Contains("LocationInstanceID is required", exception.Message);
            StringAssert.Contains("DestinationInstanceID is required", exception.Message);
        }

        [Test]
        public void Validate_UnitArrivalWithoutReferences_ReportsBothMissingIds()
        {
            GameEvent gameEvent = CreateEvent("ARRIVAL");
            gameEvent.Conditionals.Add(new UnitArrivalConditional());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("UnitInstanceID is required", exception.Message);
            StringAssert.Contains("DestinationInstanceID is required", exception.Message);
        }

        [Test]
        public void Validate_StoryCaptureWithoutProbabilityTable_ReportsMissingKey()
        {
            GameEvent gameEvent = CreateEvent("CAPTURE");
            gameEvent.Actions.Add(
                new StartStoryCaptureAction
                {
                    TargetOfficerInstanceID = "target",
                    ProbabilityTableKey = null,
                }
            );

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("ProbabilityTableKey is required", exception.Message);
        }

        [Test]
        public void Validate_ScriptedTrainingWithInvalidConfiguration_ReportsAllProblems()
        {
            GameEvent gameEvent = CreateEvent("TRAINING");
            gameEvent.Actions.Add(
                new StartScriptedTrainingAction
                {
                    DurationTicks = 0,
                    CompletionBonusPercent = -1,
                    InterruptionProgressDivisor = 0,
                }
            );

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("TraineeInstanceID is required", exception.Message);
            StringAssert.Contains("DurationTicks must be at least 1", exception.Message);
            StringAssert.Contains("CompletionBonusPercent cannot be negative", exception.Message);
            StringAssert.Contains(
                "InterruptionProgressDivisor must be at least 1",
                exception.Message
            );
            StringAssert.Contains("CompletionVariableKey is required", exception.Message);
        }

        [Test]
        public void Validate_OfficerEffectsWithInvalidConfiguration_ReportsAllProblems()
        {
            GameEvent gameEvent = CreateEvent("EFFECTS");
            gameEvent.Actions.Add(
                new IncreaseOfficerForceAction
                {
                    MinimumIncrease = -1,
                    CurrentRankPercent = -1,
                    PositiveRankGapPercent = 20,
                }
            );
            gameEvent.Actions.Add(
                new ApplyOfficerInjuryAction { MinimumInjury = 2, MaximumInjury = 1 }
            );

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("OfficerInstanceID is required", exception.Message);
            StringAssert.Contains("MinimumIncrease cannot be negative", exception.Message);
            StringAssert.Contains("CurrentRankPercent cannot be negative", exception.Message);
            StringAssert.Contains("ReferenceOfficerInstanceID is required", exception.Message);
            StringAssert.Contains("MaximumInjury cannot be less", exception.Message);
        }

        [Test]
        public void Validate_ForcePotentialRevealWithoutOfficer_ReportsProblem()
        {
            GameEvent gameEvent = CreateEvent("REVEAL");
            gameEvent.Actions.Add(new RevealOfficerForcePotentialAction());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("Actions[0].OfficerInstanceID is required", exception.Message);
        }

        [Test]
        public void Validate_OngoingAuraWithInvalidConfiguration_ReportsAllProblems()
        {
            GameEvent gameEvent = CreateEvent("AURA");
            gameEvent.Effects.Add(new FactionOfficerRatingAuraEffect());

            InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
                GameEventCatalogValidator.Validate(new[] { gameEvent })
            );

            StringAssert.Contains("SourceUnitInstanceID is required", exception.Message);
            StringAssert.Contains("LocationInstanceID is required", exception.Message);
            StringAssert.Contains("AffectedFactionInstanceID is required", exception.Message);
            StringAssert.Contains("Rating is required", exception.Message);
            StringAssert.Contains("Amount cannot be zero", exception.Message);
            StringAssert.Contains("ongoing effects require a repeatable event", exception.Message);
        }

        [Test]
        public void Validate_ValidOngoingAura_DoesNotThrow()
        {
            GameEvent gameEvent = CreateEvent("AURA");
            gameEvent.IsRepeatable = true;
            gameEvent.Effects.Add(
                new FactionOfficerRatingAuraEffect
                {
                    SourceUnitInstanceID = "PALPATINE",
                    LocationInstanceID = "CORUSCANT",
                    AffectedFactionInstanceID = "EMPIRE",
                    Rating = OfficerRating.Leadership,
                    Amount = 50,
                }
            );

            Assert.DoesNotThrow(() => GameEventCatalogValidator.Validate(new[] { gameEvent }));
        }

        private static GameEvent CreateEvent(string instanceId)
        {
            return new GameEvent
            {
                InstanceID = instanceId,
                Conditionals = new List<GameConditional>(),
                Actions = new List<GameAction>(),
            };
        }
    }
}
