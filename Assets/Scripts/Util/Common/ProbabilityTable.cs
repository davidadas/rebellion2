using System.Collections.Generic;
using System.Linq;

namespace Rebellion.Util.Common
{
    public class ProbabilityTable
    {
        public const int GuaranteedProbability = 100;

        private readonly Dictionary<int, int> _table;

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

            List<int> sortedKeys = _table.Keys.OrderBy(k => k).ToList();

            if (value < sortedKeys[0])
                return _table[sortedKeys[0]];

            for (int i = sortedKeys.Count - 1; i >= 0; i--)
            {
                int threshold = sortedKeys[i];
                if (value >= threshold)
                    return _table[threshold];
            }

            return 0;
        }
    }
}
