using System;

namespace Rebellion.Util.Random
{
    /// <summary>
    /// Generates a deterministic linear congruential sequence from a wrapped thirty-two-bit state.
    /// </summary>
    public sealed class LinearCongruentialRandomProvider : IRandomNumberProvider
    {
        private const uint _stateMultiplier = 214013;
        private const uint _stateIncrement = 2531011;
        private const int _outputShift = 16;
        private const int _outputBits = 15;
        private const uint _outputMask = (1u << _outputBits) - 1;
        public const int OutputValueCount = 1 << _outputBits;
        private uint _state;

        /// <summary>
        /// Starts a reproducible random stream from a captured seed.
        /// </summary>
        /// <param name="seed">The initial stream state.</param>
        public LinearCongruentialRandomProvider(uint seed) => _state = seed;

        /// <summary>
        /// Replaces the current stream state.
        /// </summary>
        /// <param name="seed">The replacement stream state.</param>
        public void Reseed(uint seed) => _state = seed;

        /// <summary>
        /// Returns the next normalized random value.
        /// </summary>
        /// <returns>A value in the half-open interval from zero to one.</returns>
        public double NextDouble() => NextValue() / (double)OutputValueCount;

        /// <summary>
        /// Returns the next random integer using the requested half-open interval.
        /// </summary>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The exclusive upper bound.</param>
        /// <returns>The next bounded random value.</returns>
        public int NextInt(int min, int max)
        {
            if (max <= min)
                throw new ArgumentOutOfRangeException(nameof(max));
            uint range = unchecked((uint)max - (uint)min);
            return unchecked((int)(NextValue() % range + (uint)min));
        }

        /// <summary>
        /// Advances the 32-bit generator state and returns its fifteen-bit output.
        /// </summary>
        /// <returns>The next value between zero and 32767.</returns>
        private uint NextValue()
        {
            _state = unchecked(_state * _stateMultiplier + _stateIncrement);
            return _state >> _outputShift & _outputMask;
        }
    }
}
