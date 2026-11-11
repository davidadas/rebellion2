using NUnit.Framework;
using Rebellion.Gameplay;
using Vector3 = System.Numerics.Vector3;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public sealed class TacticalCombatSceneryRandomTests
    {
        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        public void NextInt_TrivialBound_DoesNotAcquireClockOrTouchBattleStream(int bound)
        {
            TacticalCombatRandom shared = new(42);
            int clockCalls = 0;
            TacticalCombatSceneryRandom scenery = new(
                shared,
                () =>
                {
                    clockCalls++;
                    return 1;
                }
            );

            Assert.AreEqual(0, scenery.NextInt(bound));
            Assert.AreEqual(0, clockCalls);
            Assert.AreEqual(175, shared.NextInt(0, 32768));
        }

        [Test]
        public void NextInt_FirstBoundedDraw_ReseedsAndConsumesFourBattleValues()
        {
            TacticalCombatRandom shared = new(42);
            TacticalCombatSceneryRandom scenery = new(shared, () => 1);

            Assert.AreEqual(19, scenery.NextInt(100));
            Assert.AreEqual(19169, shared.NextInt(0, 32768));
        }

        [Test]
        public void NextInt_SubsequentDraws_PreserveLocalSequenceAndSharedBattleState()
        {
            TacticalCombatRandom shared = new(42);
            int clockCalls = 0;
            TacticalCombatSceneryRandom scenery = new(
                shared,
                () =>
                {
                    clockCalls++;
                    return 1;
                }
            );

            Assert.AreEqual(19, scenery.NextInt(100));
            Assert.AreEqual(25, scenery.NextInt(100));
            Assert.AreEqual(74, scenery.NextInt(100));
            Assert.AreEqual(10, scenery.NextInt(100));
            Assert.AreEqual(1, clockCalls);
            Assert.AreEqual(19169, shared.NextInt(0, 32768));
        }

        [Test]
        public void NextInt_CalendarFailure_UsesUnsignedFailureSeed()
        {
            TacticalCombatRandom shared = new(42);
            TacticalCombatSceneryRandom scenery = new(shared, () => -1);

            Assert.AreEqual(97, scenery.NextInt(100));
            Assert.AreEqual(31308, shared.NextInt(0, 32768));
        }

        [Test]
        public void NextPosition_PlanetThenStation_PreservesFourLocalDrawsInOrder()
        {
            TacticalCombatSceneryRandom scenery = new(new TacticalCombatRandom(42), () => 1);

            Assert.AreEqual(new Vector3(200, -7, -5), scenery.NextPosition(100, 2, 0.1f));
            Assert.AreEqual(new Vector3(-200, 9, -16), scenery.NextPosition(100, -2, 0.2f));
        }
    }
}
