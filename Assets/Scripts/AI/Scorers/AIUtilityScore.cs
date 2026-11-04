using Rebellion.Game;

namespace Rebellion.AI.Scorers
{
    /// <summary>
    /// Accumulates normalized considerations into a weighted average.
    /// </summary>
    public struct AIUtilityScore
    {
        private double _weightedUtility;
        private double _totalWeight;
        private double _costWeight;

        /// <summary>
        /// Returns the normalized weighted utility from zero through one.
        /// </summary>
        public double Value => _totalWeight > 0 ? _weightedUtility / _totalWeight : 0;

        /// <summary>
        /// Returns signed utility on a bounded ranking scale.
        /// </summary>
        /// <returns>Zero for non-positive utility; otherwise a monotonic value below one.</returns>
        public double RankValue
        {
            get
            {
                double signedUtility = _weightedUtility - _costWeight;
                return signedUtility > 0 ? signedUtility / (1 + signedUtility) : 0;
            }
        }

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
        /// Adds a normalized cost whose absence has full utility.
        /// </summary>
        /// <param name="input">The normalized cost input.</param>
        /// <param name="consideration">The configured curve and relative weight.</param>
        public void AddCost(double input, GameConfig.AIConsiderationConfig consideration)
        {
            AddCost(input, consideration, 1);
        }

        /// <summary>
        /// Adds the equivalent cost of a quantity of normalized cost units.
        /// </summary>
        /// <param name="input">The normalized cost input for one unit.</param>
        /// <param name="consideration">The configured curve and relative weight.</param>
        /// <param name="quantity">The number of equivalent cost units represented by the input.</param>
        public void AddCost(
            double input,
            GameConfig.AIConsiderationConfig consideration,
            double quantity
        )
        {
            if (consideration == null || consideration.Weight <= 0 || quantity <= 0)
                return;

            double weight = consideration.Weight * quantity;
            _weightedUtility += (1 - AIUtility.EvaluateCurve(input, consideration.Curve)) * weight;
            _totalWeight += weight;
            _costWeight += weight;
        }

        /// <summary>
        /// Adds every weighted consideration accumulated by another utility score.
        /// </summary>
        /// <param name="score">The utility score to merge.</param>
        public void Add(AIUtilityScore score)
        {
            _weightedUtility += score._weightedUtility;
            _totalWeight += score._totalWeight;
            _costWeight += score._costWeight;
        }
    }
}
