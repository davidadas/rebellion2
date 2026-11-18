using System;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Converts a captured local calendar clock into the encounter initialization seed.
    /// </summary>
    public static class TacticalCombatSeed
    {
        private const int _firstSupportedYear = 1970;
        private const int _lastSupportedYear = 2038;

        /// <summary>
        /// Retains the supported calendar range and wrapped epoch-second representation.
        /// </summary>
        /// <param name="clock">The local calendar fields and their effective UTC offset.</param>
        /// <returns>The wrapped epoch seconds, or minus one for an unsupported local year.</returns>
        public static int FromClock(DateTimeOffset clock) =>
            clock.Year < _firstSupportedYear || clock.Year > _lastSupportedYear
                ? -1
                : unchecked((int)clock.ToUnixTimeSeconds());
    }
}
