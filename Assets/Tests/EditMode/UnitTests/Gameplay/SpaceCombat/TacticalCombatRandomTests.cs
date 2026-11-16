using System;
using NUnit.Framework;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatRandomTests
    {
        [Test]
        public void NextInt_KnownSeed_ProducesSpecifiedBattleSequence()
        {
            TacticalCombatRandom random = new(1);
            Assert.AreEqual(41, random.NextInt(0, 32768));
            Assert.AreEqual(18467, random.NextInt(0, 32768));
            Assert.AreEqual(6334, random.NextInt(0, 32768));
        }

        [Test]
        public void NextInt_UnitRange_ConsumesOneRoll()
        {
            TacticalCombatRandom random = new(1);
            Assert.AreEqual(5, random.NextInt(5, 6));
            Assert.AreEqual(18467, random.NextInt(0, 32768));
        }

        [Test]
        public void NextInt_InclusiveBounds_UsesModuloWithinHalfOpenInterval()
        {
            TacticalCombatRandom random = new(1);
            Assert.AreEqual(3, random.NextInt(1, 4));
        }

        [Test]
        public void Reseed_PartiallyConsumedStream_RestartsAtSpecifiedSeed()
        {
            TacticalCombatRandom random = new(42);
            random.NextInt(0, 32768);
            random.Reseed(1);

            Assert.AreEqual(41, random.NextInt(0, 32768));
            Assert.AreEqual(18467, random.NextInt(0, 32768));
        }

        [TestCase(1970, 1, 1, 0, 0, 0, 0, 0)]
        [TestCase(1970, 1, 1, 0, 0, 0, 330, -19800)]
        [TestCase(1969, 12, 31, 23, 59, 59, -720, -1)]
        [TestCase(2000, 3, 1, 0, 0, 0, 0, 951868800)]
        [TestCase(2038, 1, 19, 3, 14, 8, 0, int.MinValue)]
        [TestCase(2039, 1, 1, 0, 0, 0, 0, -1)]
        public void GetCalendarSeed_CapturedLocalClock_PreservesSupportedYearAndWrappingRules(
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

            Assert.AreEqual(expected, TacticalCombatRandom.GetCalendarSeed(clock));
        }
    }
}
