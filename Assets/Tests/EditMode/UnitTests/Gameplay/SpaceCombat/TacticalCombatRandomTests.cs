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
    }
}
