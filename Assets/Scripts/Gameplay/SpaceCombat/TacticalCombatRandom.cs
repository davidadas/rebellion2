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
        /// Replaces the shared battle state before scenery selects its local sequence.
        /// </summary>
        /// <param name="seed">The captured calendar seed, including its unsigned failure representation.</param>
        public void Reseed(uint seed) => _state = seed;

        /// <summary>
        /// Converts a captured local clock to the signed calendar seed supported by battle initialization.
        /// </summary>
        /// <param name="clock">The local calendar fields and their effective UTC offset.</param>
        /// <returns>The wrapped epoch seconds, or minus one for an unsupported local year.</returns>
        public static int GetCalendarSeed(DateTimeOffset clock) =>
            clock.Year < 1970 || clock.Year > 2038 ? -1 : unchecked((int)clock.ToUnixTimeSeconds());

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
