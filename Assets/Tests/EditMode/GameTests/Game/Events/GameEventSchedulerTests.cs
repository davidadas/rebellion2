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
        public void GetRepeatRange_RandomDelaySchedule_ReturnsInclusiveRange()
        {
            GameEventScheduler scheduler = new GameEventScheduler
            {
                RandomDelay = new RandomTickRange { MinimumTicks = 10, MaximumTicks = 30 },
            };

            scheduler.GetRepeatRange(out int minimum, out int maximum);

            Assert.AreEqual(10, minimum);
            Assert.AreEqual(30, maximum);
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
