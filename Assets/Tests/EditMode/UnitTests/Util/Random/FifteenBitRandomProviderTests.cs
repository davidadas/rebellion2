using NUnit.Framework;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Util.Random
{
    [TestFixture]
    public sealed class FifteenBitRandomProviderTests
    {
        [Test]
        public void NextDouble_KnownSeed_ProducesSpecifiedNormalizedSequence()
        {
            FifteenBitRandomProvider random = new(1);

            Assert.AreEqual(0.001251220703125, random.NextDouble());
            Assert.AreEqual(0.563568115234375, random.NextDouble());
        }

        [Test]
        public void NextInt_KnownSeed_ProducesSpecifiedSequence()
        {
            FifteenBitRandomProvider random = new(1);
            Assert.AreEqual(41, random.NextInt(0, 32768));
            Assert.AreEqual(18467, random.NextInt(0, 32768));
            Assert.AreEqual(6334, random.NextInt(0, 32768));
        }

        [Test]
        public void NextInt_UnitRange_ConsumesOneRoll()
        {
            FifteenBitRandomProvider random = new(1);
            Assert.AreEqual(5, random.NextInt(5, 6));
            Assert.AreEqual(18467, random.NextInt(0, 32768));
        }

        [Test]
        public void NextInt_InclusiveBounds_UsesModuloWithinHalfOpenInterval()
        {
            FifteenBitRandomProvider random = new(1);
            Assert.AreEqual(3, random.NextInt(1, 4));
        }

        [Test]
        public void Reseed_PartiallyConsumedStream_RestartsAtSpecifiedSeed()
        {
            FifteenBitRandomProvider random = new(42);
            random.NextInt(0, 32768);
            random.Reseed(1);

            Assert.AreEqual(41, random.NextInt(0, 32768));
            Assert.AreEqual(18467, random.NextInt(0, 32768));
        }
    }
}
