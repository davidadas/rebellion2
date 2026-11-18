using System;
using SystemRandom = System.Random;

namespace Rebellion.Util.Random
{
    /// <summary>
    /// Supplies bounded bit-width requests for existing random streams.
    /// </summary>
    public static class RandomExtensions
    {
        private const int _maximumBitCount = 31;

        /// <summary>
        /// Draws a nonnegative integer containing at most the requested number of bits.
        /// </summary>
        /// <param name="random">The stream whose existing state is consumed.</param>
        /// <param name="bitCount">The requested width, from one through thirty-one.</param>
        /// <returns>A value from zero through two raised to the bit count, minus one.</returns>
        public static int NextBits(this SystemRandom random, int bitCount)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            ValidateBitCount(bitCount);
            return bitCount == _maximumBitCount
                ? random.Next(int.MinValue, 0) & int.MaxValue
                : random.Next(0, 1 << bitCount);
        }

        /// <summary>
        /// Draws a nonnegative integer through one bounded request to the supplied provider.
        /// </summary>
        /// <param name="random">The provider whose existing state is consumed.</param>
        /// <param name="bitCount">The requested width, from one through thirty-one.</param>
        /// <returns>A value from zero through two raised to the bit count, minus one.</returns>
        public static int NextBits(this IRandomNumberProvider random, int bitCount)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            ValidateBitCount(bitCount);
            return bitCount == _maximumBitCount
                ? random.NextInt(int.MinValue, 0) & int.MaxValue
                : random.NextInt(0, 1 << bitCount);
        }

        /// <summary>
        /// Rejects widths outside the nonnegative integer representation before requesting a draw.
        /// </summary>
        /// <param name="bitCount">The requested output width.</param>
        private static void ValidateBitCount(int bitCount)
        {
            if (bitCount < 1 || bitCount > _maximumBitCount)
                throw new ArgumentOutOfRangeException(nameof(bitCount));
        }
    }
}
