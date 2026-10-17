using System.Collections.Generic;

namespace Rebellion.Util.Random
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

        /// <summary>
        /// Returns the table result for an input value.
        /// </summary>
        /// <param name="value">The lookup value.</param>
        /// <returns>The matching table result.</returns>
        public int Lookup(int value)
        {
            if (_table.Count == 0)
                return 0;

            int lowestThreshold = int.MaxValue;
            int lowestResult = 0;
            int matchingThreshold = int.MinValue;
            int matchingResult = 0;
            bool foundMatch = false;
            foreach (KeyValuePair<int, int> entry in _table)
            {
                if (entry.Key <= lowestThreshold)
                {
                    lowestThreshold = entry.Key;
                    lowestResult = entry.Value;
                }
                if (entry.Key <= value && entry.Key >= matchingThreshold)
                {
                    matchingThreshold = entry.Key;
                    matchingResult = entry.Value;
                    foundMatch = true;
                }
            }

            return foundMatch ? matchingResult : lowestResult;
        }
    }
}
