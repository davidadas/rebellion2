using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventScheduleTests
    {
        [Test]
        public void Serialization_RandomIntervalUntilConditions_RoundTrips()
        {
            GameEventSchedule scheduler = new GameEventSchedule
            {
                RandomInterval = new RandomInterval
                {
                    MinimumTicks = 10,
                    MaximumTicks = 30,
                    Until = new List<GameConditional>
                    {
                        new TickCountConditional
                        {
                            Comparison = ComparisonOperator.GreaterThanOrEqual,
                            Ticks = 100,
                        },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(scheduler);
            GameEventSchedule restored = SerializationHelper.Deserialize<GameEventSchedule>(xml);

            TickCountConditional condition = (TickCountConditional)restored.RandomInterval.Until[0];
            Assert.AreEqual(ComparisonOperator.GreaterThanOrEqual, condition.Comparison);
            Assert.AreEqual(100, condition.Ticks);
        }

        [Test]
        public void Serialization_ExplicitAfterAllDependencies_PreservesOrder()
        {
            GameEventSchedule scheduler = new GameEventSchedule
            {
                AfterAll = new AfterEvents
                {
                    DelayTicks = 25,
                    Events = new List<EventDependency>
                    {
                        new EventDependency { EventInstanceID = "FIRST" },
                        new EventDependency { EventInstanceID = "SECOND" },
                    },
                },
            };

            string xml = SerializationHelper.Serialize(scheduler);
            GameEventSchedule restored = SerializationHelper.Deserialize<GameEventSchedule>(xml);

            CollectionAssert.AreEqual(
                new[] { "FIRST", "SECOND" },
                restored.AfterAll.Events.ConvertAll(dependency => dependency.EventInstanceID)
            );
        }
    }
}
