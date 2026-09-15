using Rebellion.Game;

namespace Rebellion.AI.Scoring
{
    /// <summary>
    /// Accumulates normalized considerations into a weighted average.
    /// </summary>
    public struct AIUtilityScore
    {
        private double _weightedUtility;
        private double _totalWeight;

        /// <summary>
        /// Returns the normalized weighted utility from zero through one.
        /// </summary>
        public double Value => _totalWeight > 0 ? _weightedUtility / _totalWeight : 0;

        /// <summary>
        /// Adds a beneficial normalized consideration.
        /// </summary>
        /// <param name="input">The normalized consideration input.</param>
        /// <param name="consideration">The configured curve and relative weight.</param>
        public void Add(double input, GameConfig.AIConsiderationConfig consideration)
        {
            if (consideration == null || consideration.Weight <= 0)
                return;

            _weightedUtility +=
                AIUtility.EvaluateCurve(input, consideration.Curve) * consideration.Weight;
            _totalWeight += consideration.Weight;
        }

        /// <summary>
        /// Adds a beneficial raw consideration after normalization by its configured maximum.
        /// </summary>
        /// <param name="value">The raw consideration value.</param>
        /// <param name="consideration">The configured input range, curve, and relative weight.</param>
        public void AddRaw(double value, GameConfig.AIConsiderationConfig consideration)
        {
            if (consideration == null)
                return;

            Add(AIUtility.Fulfillment(value, consideration.InputMaximum), consideration);
        }

        /// <summary>
        /// Adds a normalized cost whose absence has full utility.
        /// </summary>
        /// <param name="input">The normalized cost input.</param>
        /// <param name="consideration">The configured curve and relative weight.</param>
        public void AddCost(double input, GameConfig.AIConsiderationConfig consideration)
        {
            if (consideration == null || consideration.Weight <= 0)
                return;

            _weightedUtility +=
                (1 - AIUtility.EvaluateCurve(input, consideration.Curve)) * consideration.Weight;
            _totalWeight += consideration.Weight;
        }

        /// <summary>
        /// Adds a raw cost after normalizing it against the consideration's configured range.
        /// </summary>
        /// <param name="value">The raw cost value.</param>
        /// <param name="consideration">The response curve, range, and contribution weight.</param>
        public void AddCostRaw(double value, GameConfig.AIConsiderationConfig consideration)
        {
            if (consideration == null)
                return;

            AddCost(AIUtility.Fulfillment(value, consideration.InputMaximum), consideration);
        }

        /// <summary>
        /// Adds every weighted consideration accumulated by another utility score.
        /// </summary>
        /// <param name="score">The utility score to merge.</param>
        public void Add(AIUtilityScore score)
        {
            _weightedUtility += score._weightedUtility;
            _totalWeight += score._totalWeight;
        }
    }
}
