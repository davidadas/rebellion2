using System;
using NUnit.Framework;
using Rebellion.Util.Random;

namespace Rebellion.Tests.Util.Random
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
            SystemRandomProvider freshProvider = new SystemRandomProvider(seed: 42);
            SystemRandomProvider zeroAdvanceProvider = new SystemRandomProvider(
                seed: 42,
                advanceTo: 0
            );

            Assert.AreEqual(
                freshProvider.NextInt(0, int.MaxValue),
                zeroAdvanceProvider.NextInt(0, int.MaxValue)
            );
        }

        [TestCase(int.MinValue, int.MaxValue)]
        [TestCase(-1, int.MaxValue)]
        [TestCase(int.MinValue, 0)]
        [TestCase(0, int.MaxValue)]
        [TestCase(5, 5)]
        public void Constructor_MixedRangeDraws_ResumesExactStream(int min, int max)
        {
            for (int seed = 0; seed < 32; seed++)
            {
                SystemRandomProvider live = new(seed);
                System.Random reference = new(seed);
                for (int index = 0; index < 10; index++)
                {
                    Assert.AreEqual(reference.Next(min, max), live.NextInt(min, max));
                    Assert.AreEqual(reference.NextDouble(), live.NextDouble());
                    SystemRandomProvider restored = new(seed, live.CallCount);
                    Assert.AreEqual(live.NextDouble(), restored.NextDouble());
                    reference.NextDouble();
                }
            }
        }

        [Test]
        public void NextInt_InvalidBounds_PreservesSavedStreamPosition()
        {
            SystemRandomProvider live = new(42);
            live.NextDouble();
            Assert.Throws<ArgumentOutOfRangeException>(() => live.NextInt(10, 1));
            SystemRandomProvider restored = new(42, live.CallCount);
            Assert.AreEqual(live.NextDouble(), restored.NextDouble());
        }
    }
}
