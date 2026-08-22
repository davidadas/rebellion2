using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Advisor;
using Rebellion.Game.Events;
using Rebellion.Game.Messages;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public sealed class GameEventTests
    {
        [Test]
        public void CanActivate_MaximumActivationsReached_ReturnsFalse()
        {
            GameEvent gameEvent = new GameEvent { MaximumActivations = 3 };
            GameEventState state = new GameEventState { ExecutionCount = 3 };

            bool result = gameEvent.CanActivate(state);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanActivate_UnlimitedEvent_ReturnsTrue()
        {
            GameEvent gameEvent = new GameEvent();
            GameEventState state = new GameEventState { ExecutionCount = 100 };

            bool result = gameEvent.CanActivate(state);

            Assert.IsTrue(result);
        }

        [Test]
        public void Conditionals_AuthoredAliases_RoundTripConcreteTypes()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "EVENT_TEST",
                Conditionals = new List<GameConditional>
                {
                    new TickCountConditional
                    {
                        InstanceID = "TICK_CONDITION",
                        Ticks = 30,
                        Comparison = ComparisonOperator.GreaterThan,
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            StringAssert.Contains("<TickCount Comparison=\"GreaterThan\" Ticks=\"30\">", xml);
            Assert.IsFalse(xml.Contains("<TickCountConditional"));
            TickCountConditional conditional =
                restored.Conditionals.Single() as TickCountConditional;
            Assert.IsNotNull(conditional);
            Assert.AreEqual(30, conditional.Ticks);
            Assert.AreEqual(ComparisonOperator.GreaterThan, conditional.Comparison);
        }

        [Test]
        public void CompositeConditionals_RoundTripWithoutCollectionWrappers()
        {
            GameEvent gameEvent = new GameEvent
            {
                Conditionals = new List<GameConditional>
                {
                    new NotConditional
                    {
                        Conditionals = new List<GameConditional>
                        {
                            new AnyConditional
                            {
                                Conditionals = new List<GameConditional>
                                {
                                    new IsCapturedConditional
                                    {
                                        OfficerInstanceID = "LUKE_SKYWALKER",
                                    },
                                    new IsInTransitConditional
                                    {
                                        UnitInstanceID = "LUKE_SKYWALKER",
                                    },
                                },
                            },
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            StringAssert.Contains("<Not><Any><IsCaptured", xml);
            Assert.IsFalse(xml.Contains("<Not><Conditionals>"));
            NotConditional not = (NotConditional)restored.Conditionals.Single();
            AnyConditional any = (AnyConditional)not.Conditionals.Single();
            Assert.AreEqual(2, any.Conditionals.Count);
        }

        [Test]
        public void Actions_AuthoredAliasesAndPresentation_RoundTripConcreteValues()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "EVENT_STORY",
                Actions = new List<GameAction>
                {
                    new RollRandomAction
                    {
                        Outcomes = new List<RandomOutcome>
                        {
                            new RandomOutcome
                            {
                                Weight = 3,
                                Actions = new List<GameAction>
                                {
                                    new SendMessageAction
                                    {
                                        SubjectInstanceID = "LUKE",
                                        RelatedSubjectInstanceID = "VADER",
                                        MessageType = MessageType.Advice,
                                        BackgroundAudio = new MessageAudio
                                        {
                                            Path = "Story/dialogue",
                                        },
                                        TriggerAdvisorNotification = new AdvisorNotification
                                        {
                                            Preset = AdvisorNotificationPreset.SubjectReport,
                                            Protocol = new AdvisorAnimation
                                            {
                                                AnimationPath = "Story/advisor",
                                                FrameCount = 3,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                    new SetNodeActiveAction { InstanceID = "LUKE_SKYWALKER", IsActive = false },
                    new IncreaseOfficerForceAction
                    {
                        OfficerInstanceID = "LUKE_SKYWALKER",
                        Amount = 5,
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            RollRandomAction random = restored.Actions[0] as RollRandomAction;
            SendMessageAction message =
                random?.Outcomes.Single().Actions.Single() as SendMessageAction;
            Assert.IsNotNull(message);
            Assert.AreEqual("LUKE", message.SubjectInstanceID);
            Assert.AreEqual("VADER", message.RelatedSubjectInstanceID);
            Assert.AreEqual("Story/dialogue", message.BackgroundAudio.Path);
            Assert.AreEqual(
                "Story/advisor",
                message.TriggerAdvisorNotification.Protocol.AnimationPath
            );
            Assert.AreEqual(3, message.TriggerAdvisorNotification.Protocol.FrameCount);
            Assert.AreEqual(
                "LUKE_SKYWALKER",
                ((SetNodeActiveAction)restored.Actions[1]).InstanceID
            );
            Assert.IsFalse(((SetNodeActiveAction)restored.Actions[1]).IsActive);
            Assert.AreEqual(5, ((IncreaseOfficerForceAction)restored.Actions[2]).Amount);
        }

        [Test]
        public void MaximumActivations_AuthoredValue_RoundTripsAttribute()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "LIMITED_EVENT",
                MaximumActivations = 5,
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            Assert.AreEqual(5, restored.MaximumActivations);
        }
    }
}
