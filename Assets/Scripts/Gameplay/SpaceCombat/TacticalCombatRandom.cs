using System;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns a battle-local random stream, keeping tactical rolls independent of campaign generation.
    /// </summary>
    public sealed class TacticalCombatRandom : IRandomNumberProvider
    {
        private uint _state;

        /// <summary>
        /// Starts a reproducible tactical random stream from a captured seed.
        /// </summary>
        /// <param name="seed">The battle-entry clock or replay seed.</param>
        public TacticalCombatRandom(uint seed) => _state = seed;

        /// <summary>
        /// Returns the next normalized tactical roll.
        /// </summary>
        /// <returns>A value in the half-open interval from zero to one.</returns>
        public double NextDouble() => NextValue() / 32768.0;

        /// <summary>
        /// Returns the next tactical integer using the requested half-open interval.
        /// </summary>
        /// <param name="min">The inclusive lower bound.</param>
        /// <param name="max">The exclusive upper bound.</param>
        /// <returns>The next bounded tactical roll.</returns>
        public int NextInt(int min, int max)
        {
            if (max <= min)
                throw new ArgumentOutOfRangeException(nameof(max));
            uint range = unchecked((uint)max - (uint)min);
            return unchecked((int)(NextValue() % range + (uint)min));
        }

        /// <summary>
        /// Advances the 32-bit battle state and returns its fifteen-bit output.
        /// </summary>
        /// <returns>The next value between zero and 32767.</returns>
        private uint NextValue()
        {
            _state = unchecked(_state * 214013 + 2531011);
            return _state >> 16 & 32767;
        }
    }
}
