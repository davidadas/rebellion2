using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rebellion.Game.Events;

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
                    Actions = new List<GameAction> { new TriggerDuelAction() },
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
