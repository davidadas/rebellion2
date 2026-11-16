using System;
using System.Numerics;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns scenery's bounded sequence and its lazy initialization of the shared battle stream.
    /// </summary>
    public sealed class TacticalCombatSceneryRandom
    {
        private readonly TacticalCombatRandom _sharedRandom;
        private readonly Func<int> _calendarSeed;
        private int _seed = -1;

        /// <summary>
        /// Captures the shared battle stream and the platform's calendar-seed boundary.
        /// </summary>
        /// <param name="sharedRandom">The battle stream reseeded when scenery first needs randomness.</param>
        /// <param name="calendarSeed">The calendar seed acquired on the first nontrivial bounded draw.</param>
        public TacticalCombatSceneryRandom(
            TacticalCombatRandom sharedRandom,
            Func<int> calendarSeed
        )
        {
            _sharedRandom = sharedRandom ?? throw new ArgumentNullException(nameof(sharedRandom));
            _calendarSeed = calendarSeed ?? throw new ArgumentNullException(nameof(calendarSeed));
        }

        /// <summary>
        /// Returns the local sequence's quotient-scaled value for a requested bound.
        /// </summary>
        /// <param name="upperBound">The scaling bound; values at most one do not consume randomness.</param>
        /// <returns>The bounded quotient, or zero for a trivial bound.</returns>
        public int NextInt(int upperBound)
        {
            if (upperBound <= 1)
                return 0;
            if (_seed == -1)
            {
                int selectedSeed = SelectSeed();
                _seed = selectedSeed;
                for (int index = 0; index < 16; index++)
                    _seed = Advance(_seed);
                _seed = selectedSeed;
            }
            _seed = Advance(_seed);
            return _seed / (0x7ffffffe / upperBound);
        }

        /// <summary>
        /// Chooses one background decoration's position using the stored half range for subtraction.
        /// </summary>
        /// <param name="depth">The encounter's deployment span.</param>
        /// <param name="horizontal">The horizontal position in deployment spans.</param>
        /// <param name="spread">The vertical and depth half range in deployment spans.</param>
        /// <returns>The encounter-local decoration position.</returns>
        public Vector3 NextPosition(float depth, float horizontal, float spread)
        {
            double extendedHalf = (double)depth * spread;
            float storedHalf = (float)extendedHalf;
            int y = NextInt(unchecked((int)(extendedHalf + extendedHalf)));
            int z = NextInt(unchecked((int)((double)storedHalf + storedHalf)));
            return new Vector3(
                (float)((double)depth * horizontal),
                (float)((double)y - storedHalf),
                (float)((double)z - storedHalf)
            );
        }

        /// <summary>
        /// Reseeds and consumes the four shared battle values that select scenery's initial seed.
        /// </summary>
        /// <returns>The selected nonnegative local seed.</returns>
        private int SelectSeed()
        {
            _sharedRandom.Reseed(unchecked((uint)_calendarSeed()));
            int a = _sharedRandom.NextInt(0, 32768);
            int b = _sharedRandom.NextInt(0, 32768);
            int c = _sharedRandom.NextInt(0, 32768);
            int d = _sharedRandom.NextInt(0, 32768);
            return unchecked(a * b + c * d);
        }

        /// <summary>
        /// Advances the signed local state with wrapped products and truncating division.
        /// </summary>
        /// <param name="seed">The current local state.</param>
        /// <returns>The next local state.</returns>
        private static int Advance(int seed)
        {
            int next = unchecked(seed * 0xa18e + seed / 0xcad4 * -0x7fffffff);
            return next < 0 ? unchecked(next + 0x7fffffff) : next;
        }
    }
}
