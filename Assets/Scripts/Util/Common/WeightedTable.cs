using System.Collections.Generic;
using Rebellion.Core.Simulation;

namespace Rebellion.Util.Common
{
    /// <summary>
    /// Weighted random selection table using cumulative thresholds.
    /// Matches the original game's table lookup: roll a value, find the first entry
    /// whose cumulative weight is strictly greater than the roll.
    /// </summary>
    public class WeightedTable<T>
    {
        private readonly List<(int cumulativeWeight, T item)> entries;
        private readonly int rollMin;
        private readonly int rollMax;
        private readonly bool fallbackToLast;

        /// <param name="entries">Table entries with cumulative weights.</param>
        /// <param name="rollMin">Minimum roll value (inclusive). Default 0 for facility tables.</param>
        /// <param name="rollMax">Maximum roll value (exclusive upper bound for NextInt). Default 101.</param>
        /// <param name="fallbackToLast">
        /// If true, returns the last entry when roll exceeds all weights (unit table behavior).
        /// If false, returns default(T) (facility table behavior — null/no-op).
        /// </param>
        public WeightedTable(
            List<(int cumulativeWeight, T item)> entries,
            int rollMin = 0,
            int rollMax = 101,
            bool fallbackToLast = false
        )
        {
            this.entries = entries;
            this.rollMin = rollMin;
            this.rollMax = rollMax;
            this.fallbackToLast = fallbackToLast;
        }

        public T Roll(IRandomNumberProvider rng)
        {
            if (entries.Count == 0)
                return default;

            int roll = rng.NextInt(rollMin, rollMax);

            foreach ((int cumulativeWeight, T item) entry in entries)
            {
                if (roll < entry.cumulativeWeight)
                    return entry.item;
            }

            // Roll exceeds all entries
            if (fallbackToLast)
                return entries[entries.Count - 1].item;

            return default;
        }
    }
}
