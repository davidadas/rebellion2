using Rebellion.Game;

namespace Rebellion.AI.Core
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
            if (consideration == null || consideration.Weight <= 0)
                return;

            _weightedUtility +=
                (1 - AIUtility.EvaluateCurve(input, consideration.Curve)) * consideration.Weight;
            _totalWeight += consideration.Weight;
            _costWeight += consideration.Weight;
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
