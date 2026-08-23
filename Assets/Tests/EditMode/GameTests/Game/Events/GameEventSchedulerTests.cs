using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game.Events;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventSchedulerTests
    {
        [Test]
        public void GetInitialRange_AtSchedule_ReturnsAbsoluteTick()
        {
            GameEventScheduler scheduler = new GameEventScheduler { At = new AtTick { Tick = 25 } };

            scheduler.GetInitialRange(out int minimum, out int maximum);

            Assert.AreEqual(25, minimum);
            Assert.AreEqual(25, maximum);
        }

        [Test]
        public void GetInitialRange_EverySchedule_ReturnsInitialDelay()
        {
            GameEventScheduler scheduler = new GameEventScheduler
            {
                Every = new EveryTicks { Ticks = 20, InitialDelayTicks = 5 },
            };

            scheduler.GetInitialRange(out int minimum, out int maximum);

            Assert.AreEqual(5, minimum);
            Assert.AreEqual(5, maximum);
        }

        [Test]
        public void GetInitialRange_RandomDelaySchedule_ReturnsInclusiveRange()
        {
            GameEventScheduler scheduler = new GameEventScheduler
            {
                RandomDelay = new RandomDelay { MinimumTicks = 10, MaximumTicks = 30 },
            };

            scheduler.GetInitialRange(out int minimum, out int maximum);

            Assert.AreEqual(10, minimum);
            Assert.AreEqual(30, maximum);
        }

        [Test]
        public void GetRepeatRange_RandomIntervalSchedule_ReturnsInclusiveRange()
        {
            GameEventScheduler scheduler = new GameEventScheduler
            {
                RandomInterval = new RandomInterval { MinimumTicks = 10, MaximumTicks = 30 },
            };

            scheduler.GetRepeatRange(out int minimum, out int maximum);

            Assert.AreEqual(10, minimum);
            Assert.AreEqual(30, maximum);
        }

        [Test]
        public void Serialization_RandomIntervalUntilConditions_RoundTrips()
        {
            GameEventScheduler scheduler = new GameEventScheduler
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
            GameEventScheduler restored = SerializationHelper.Deserialize<GameEventScheduler>(xml);

            TickCountConditional condition = (TickCountConditional)restored.RandomInterval.Until[0];
            Assert.AreEqual(ComparisonOperator.GreaterThanOrEqual, condition.Comparison);
            Assert.AreEqual(100, condition.Ticks);
        }

        [Test]
        public void Serialization_ExplicitAfterAllDependencies_PreservesOrder()
        {
            GameEventScheduler scheduler = new GameEventScheduler
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
            GameEventScheduler restored = SerializationHelper.Deserialize<GameEventScheduler>(xml);

            CollectionAssert.AreEqual(
                new[] { "FIRST", "SECOND" },
                restored.AfterAll.Events.ConvertAll(dependency => dependency.EventInstanceID)
            );
        }
    }
}
