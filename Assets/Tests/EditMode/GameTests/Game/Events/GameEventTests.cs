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
        public void CanExecute_TriggerCountReached_ReturnsFalse()
        {
            GameEvent gameEvent = new GameEvent { TriggerCount = 3 };
            GameEventState state = new GameEventState { ExecutionCount = 3 };

            bool result = gameEvent.CanExecute(state);

            Assert.IsFalse(result);
        }

        [Test]
        public void CanExecute_UnlimitedEvent_ReturnsTrue()
        {
            GameEvent gameEvent = new GameEvent();
            GameEventState state = new GameEventState { ExecutionCount = 100 };

            bool result = gameEvent.CanExecute(state);

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
        public void Actions_AuthoredAliasesAndPresentation_RoundTripConcreteValues()
        {
            GameEvent gameEvent = new GameEvent
            {
                InstanceID = "EVENT_STORY",
                Actions = new List<GameAction>
                {
                    new RandomAction
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
                                        AdvisorNotification = new AdvisorNotification
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
                    new AddToVoidAction { UnitInstanceID = "LUKE_SKYWALKER" },
                    new IncreaseOfficerForceAction
                    {
                        OfficerInstanceID = "LUKE_SKYWALKER",
                        Amount = 5,
                    },
                },
            };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            RandomAction random = restored.Actions[0] as RandomAction;
            SendMessageAction message =
                random?.Outcomes.Single().Actions.Single() as SendMessageAction;
            Assert.IsNotNull(message);
            Assert.AreEqual("LUKE", message.SubjectInstanceID);
            Assert.AreEqual("VADER", message.RelatedSubjectInstanceID);
            Assert.AreEqual("Story/dialogue", message.BackgroundAudio.Path);
            Assert.AreEqual("Story/advisor", message.AdvisorNotification.Protocol.AnimationPath);
            Assert.AreEqual(3, message.AdvisorNotification.Protocol.FrameCount);
            Assert.AreEqual(
                "LUKE_SKYWALKER",
                ((AddToVoidAction)restored.Actions[1]).UnitInstanceID
            );
            Assert.AreEqual(5, ((IncreaseOfficerForceAction)restored.Actions[2]).Amount);
        }

        [Test]
        public void TriggerCount_AuthoredValue_RoundTripsAttribute()
        {
            GameEvent gameEvent = new GameEvent { InstanceID = "LIMITED_EVENT", TriggerCount = 5 };

            string xml = SerializationHelper.Serialize(gameEvent);
            GameEvent restored = SerializationHelper.Deserialize<GameEvent>(xml);

            Assert.AreEqual(5, restored.TriggerCount);
        }
    }
}
