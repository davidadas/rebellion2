using System.Collections.Generic;
using System.Linq;

namespace Rebellion.Util.Common
{
    /// <summary>
    /// Probability lookup table with linear interpolation.
    /// Maps input value to probability percentage (0-100).
    /// Used for uprising chances, mission success rates, etc.
    /// </summary>
    public class ProbabilityTable
    {
        private readonly Dictionary<int, int> table;

        public ProbabilityTable(Dictionary<int, int> entries)
        {
            this.table = entries;
        }

        /// <summary>
        /// Lookup probability for a given value with linear interpolation.
        /// If value falls between two thresholds, interpolates linearly.
        /// If value is below all thresholds, returns 0.
        /// If value is at or above highest threshold, returns that threshold's value.
        /// </summary>
        /// <param name="value">Input value (e.g., loyalty)</param>
        /// <returns>Probability percentage (0-100)</returns>
        public int Lookup(int value)
        {
            if (table.Count == 0)
                return 0;

            var sortedKeys = table.Keys.OrderBy(k => k).ToList();

            // Value below all thresholds
            if (value < sortedKeys[0])
                return 0;

            // Value at or above highest threshold
            if (value >= sortedKeys[sortedKeys.Count - 1])
                return table[sortedKeys[sortedKeys.Count - 1]];

            // Find the two thresholds we're between
            for (int i = 0; i < sortedKeys.Count - 1; i++)
            {
                int lowerThreshold = sortedKeys[i];
                int upperThreshold = sortedKeys[i + 1];

                if (value >= lowerThreshold && value < upperThreshold)
                {
                    int lowerValue = table[lowerThreshold];
                    int upperValue = table[upperThreshold];

                    // Linear interpolation
                    double ratio =
                        (double)(value - lowerThreshold) / (upperThreshold - lowerThreshold);
                    int interpolated = lowerValue + (int)(ratio * (upperValue - lowerValue));

                    return interpolated;
                }
            }

            // Fallback (should not reach here)
            return 0;
        }
    }
}
