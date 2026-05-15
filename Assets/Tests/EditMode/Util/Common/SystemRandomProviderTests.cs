using NUnit.Framework;
using Rebellion.Util.Common;

namespace Rebellion.Tests.Util.Common
{
    [TestFixture]
    public class SystemRandomProviderTests
    {
        [Test]
        public void CallCount_StartsAtZero_WithoutAdvanceTo()
        {
            SystemRandomProvider rng = new SystemRandomProvider(seed: 42);

            Assert.AreEqual(0, rng.CallCount);
        }

        [Test]
        public void NextInt_OnEachCall_IncrementsCallCount()
        {
            SystemRandomProvider rng = new SystemRandomProvider(seed: 42);

            rng.NextInt(0, 10);
            rng.NextInt(0, 10);
            rng.NextInt(0, 10);

            Assert.AreEqual(3, rng.CallCount);
        }

        [Test]
        public void NextDouble_OnEachCall_IncrementsCallCount()
        {
            SystemRandomProvider rng = new SystemRandomProvider(seed: 42);

            rng.NextDouble();
            rng.NextDouble();

            Assert.AreEqual(2, rng.CallCount);
        }

        [Test]
        public void Constructor_WithAdvanceTo_ReproducesMidStreamPosition()
        {
            SystemRandomProvider live = new SystemRandomProvider(seed: 42);
            int[] discarded = new int[5];
            for (int i = 0; i < 5; i++)
                discarded[i] = live.NextInt(0, int.MaxValue);
            int next = live.NextInt(0, int.MaxValue);

            SystemRandomProvider replayed = new SystemRandomProvider(seed: 42, advanceTo: 5);

            Assert.AreEqual(next, replayed.NextInt(0, int.MaxValue));
            Assert.AreEqual(6, replayed.CallCount);
        }

        [Test]
        public void Constructor_WithAdvanceToZero_BehavesAsFreshSeed()
        {
            SystemRandomProvider a = new SystemRandomProvider(seed: 42);
            SystemRandomProvider b = new SystemRandomProvider(seed: 42, advanceTo: 0);

            Assert.AreEqual(a.NextInt(0, int.MaxValue), b.NextInt(0, int.MaxValue));
        }
    }
}
