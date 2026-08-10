using NUnit.Framework;
using Rebellion.Game.Events;
using Rebellion.Game.Results;

namespace Rebellion.Tests.Game.Events
{
    [TestFixture]
    public class GameEventTriggerRegistryTests
    {
        [Test]
        public void IsKnown_RegisteredTrigger_ReturnsTrue()
        {
            bool isKnown = GameEventTriggerRegistry.IsKnown("core:mission.completed");

            Assert.IsTrue(isKnown);
        }

        [Test]
        public void Matches_RegisteredResultType_ReturnsTrue()
        {
            bool matches = GameEventTriggerRegistry.Matches(
                "core:mission.completed",
                new MissionCompletedResult()
            );

            Assert.IsTrue(matches);
        }

        [Test]
        public void Matches_UnknownTrigger_ReturnsFalse()
        {
            bool matches = GameEventTriggerRegistry.Matches(
                "mod:unknown",
                new MissionCompletedResult()
            );

            Assert.IsFalse(matches);
        }
    }
}
