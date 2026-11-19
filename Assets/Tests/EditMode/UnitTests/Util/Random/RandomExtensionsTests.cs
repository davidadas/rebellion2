using System;
using NUnit.Framework;
using Rebellion.Util.Random;
using SystemRandom = System.Random;

namespace Rebellion.Tests.Util.Random
{
    [TestFixture]
    public sealed class RandomExtensionsTests
    {
        [TestCase(1)]
        [TestCase(8)]
        [TestCase(15)]
        [TestCase(30)]
        [TestCase(31)]
        public void NextBits_SeededSystemRandom_StaysWithinRequestedWidth(int bitCount)
        {
            SystemRandom random = new(42);
            long upperBound = 1L << bitCount;

            for (int index = 0; index < 64; index++)
                Assert.That(random.NextBits(bitCount), Is.InRange(0L, upperBound - 1));
        }

        [TestCase(1, 1)]
        [TestCase(8, 255)]
        [TestCase(15, 32767)]
        [TestCase(30, 1073741823)]
        [TestCase(31, int.MaxValue)]
        public void NextBits_SystemRandomUpperBoundary_IncludesAllRequestedBits(
            int bitCount,
            int expected
        )
        {
            UpperBoundRandom random = new();

            Assert.AreEqual(expected, random.NextBits(bitCount));
            Assert.AreEqual(1, random.RequestCount);
        }

        [TestCase(1, 1)]
        [TestCase(8, 255)]
        [TestCase(15, 32767)]
        [TestCase(30, 1073741823)]
        [TestCase(31, int.MaxValue)]
        public void NextBits_ProviderUpperBoundary_IncludesAllRequestedBits(
            int bitCount,
            int expected
        )
        {
            UpperBoundProvider random = new();

            Assert.AreEqual(expected, random.NextBits(bitCount));
            Assert.AreEqual(1, random.RequestCount);
        }

        [Test]
        public void NextBits_KnownSeed_PreservesSequenceAndDrawCount()
        {
            LinearCongruentialRandomProvider random = new(1);

            Assert.AreEqual(41, random.NextBits(15));
            Assert.AreEqual(18467, random.NextInt(0, 32768));
            Assert.AreEqual(6334, random.NextBits(15));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(32)]
        [TestCase(int.MaxValue)]
        public void NextBits_SystemRandomInvalidWidth_DoesNotConsumeDraw(int bitCount)
        {
            UpperBoundRandom random = new();

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextBits(bitCount));
            Assert.AreEqual(0, random.RequestCount);
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(32)]
        [TestCase(int.MaxValue)]
        public void NextBits_ProviderInvalidWidth_DoesNotAdvanceSequence(int bitCount)
        {
            LinearCongruentialRandomProvider random = new(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextBits(bitCount));
            Assert.AreEqual(41, random.NextInt(0, 32768));
        }

        [Test]
        public void NextBits_NullSystemRandom_ThrowsArgumentNullException()
        {
            SystemRandom random = null;

            Assert.Throws<ArgumentNullException>(() => random.NextBits(15));
        }

        [Test]
        public void NextBits_NullProvider_ThrowsArgumentNullException()
        {
            IRandomNumberProvider random = null;

            Assert.Throws<ArgumentNullException>(() => random.NextBits(15));
        }

        private sealed class UpperBoundRandom : SystemRandom
        {
            public int RequestCount { get; private set; }

            /// <summary>
            /// Records a bounded draw and returns its highest permitted value.
            /// </summary>
            /// <param name="minValue">The inclusive lower bound.</param>
            /// <param name="maxValue">The exclusive upper bound.</param>
            /// <returns>The largest permitted integer.</returns>
            public override int Next(int minValue, int maxValue)
            {
                if (minValue >= maxValue)
                    throw new ArgumentOutOfRangeException(nameof(maxValue));
                RequestCount++;
                return maxValue - 1;
            }
        }

        private sealed class UpperBoundProvider : IRandomNumberProvider
        {
            public int RequestCount { get; private set; }

            /// <summary>
            /// Rejects floating-point draws in this bounded-integer test provider.
            /// </summary>
            /// <returns>Never returns a value.</returns>
            public double NextDouble() => throw new NotSupportedException();

            /// <summary>
            /// Records a bounded draw and returns its highest permitted value.
            /// </summary>
            /// <param name="min">The inclusive lower bound.</param>
            /// <param name="max">The exclusive upper bound.</param>
            /// <returns>The largest permitted integer.</returns>
            public int NextInt(int min, int max)
            {
                if (min >= max)
                    throw new ArgumentOutOfRangeException(nameof(max));
                RequestCount++;
                return max - 1;
            }
        }
    }
}
