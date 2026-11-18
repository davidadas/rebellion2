using System;
using NUnit.Framework;
using Rebellion.Game.SpaceCombat;

namespace Rebellion.Tests.Game.SpaceCombat
{
    [TestFixture]
    public sealed class TacticalCombatSeedTests
    {
        [TestCase(1970, 1, 1, 0, 0, 0, 0, 0)]
        [TestCase(1970, 1, 1, 0, 0, 0, 330, -19800)]
        [TestCase(1969, 12, 31, 23, 59, 59, -720, -1)]
        [TestCase(2000, 3, 1, 0, 0, 0, 0, 951868800)]
        [TestCase(2038, 1, 19, 3, 14, 8, 0, int.MinValue)]
        [TestCase(2039, 1, 1, 0, 0, 0, 0, -1)]
        public void FromClock_CapturedLocalClock_PreservesSupportedYearAndWrappingRules(
            int year,
            int month,
            int day,
            int hour,
            int minute,
            int second,
            int offsetMinutes,
            int expected
        )
        {
            DateTimeOffset clock = new(
                year,
                month,
                day,
                hour,
                minute,
                second,
                TimeSpan.FromMinutes(offsetMinutes)
            );

            Assert.AreEqual(expected, TacticalCombatSeed.FromClock(clock));
        }
    }
}
