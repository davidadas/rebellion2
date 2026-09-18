using System.Collections.Generic;

namespace Rebellion.Util.Common
{
    public class ProbabilityTable
    {
        public const int GuaranteedProbability = 100;

        private readonly Dictionary<int, int> _table;

        /// <summary>
        /// Initializes a new instance of the ProbabilityTable class.
        /// </summary>
        /// <param name="entries">The entries.</param>
        public ProbabilityTable(Dictionary<int, int> entries)
        {
            _table = entries;
        }

        /// <summary>Returns the table result for an input value.</summary>
        /// <param name="value">The lookup value.</param>
        /// <returns>The matching table result.</returns>
        public int Lookup(int value)
        {
            if (_table.Count == 0)
                return 0;

            int lowestThreshold = int.MaxValue;
            int lowestResult = 0;
            int matchedThreshold = int.MinValue;
            int matchedResult = 0;
            bool hasMatch = false;
            foreach (KeyValuePair<int, int> entry in _table)
            {
                if (entry.Key < lowestThreshold)
                {
                    lowestThreshold = entry.Key;
                    lowestResult = entry.Value;
                }

                if (entry.Key <= value && (!hasMatch || entry.Key > matchedThreshold))
                {
                    matchedThreshold = entry.Key;
                    matchedResult = entry.Value;
                    hasMatch = true;
                }
            }

            return hasMatch ? matchedResult : lowestResult;
        }
    }
}
