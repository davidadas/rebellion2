using System;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Contains calculated objective, foiling, and overall mission probabilities.
    /// </summary>
    public sealed class MissionOdds
    {
        public double ObjectiveSuccessProbability { get; }

        public double FoilProbability { get; }

        public double OverallSuccessProbability { get; }

        /// <summary>
        /// Creates complete mission odds from the objective and pre-objective foiling chances.
        /// </summary>
        /// <param name="objectiveSuccessProbability">Chance that the objective succeeds if reached.</param>
        /// <param name="foilProbability">Chance that known opposing forces foil the mission first.</param>
        internal MissionOdds(double objectiveSuccessProbability, double foilProbability)
        {
            ObjectiveSuccessProbability = Math.Clamp(objectiveSuccessProbability, 0, 100);
            FoilProbability = Math.Clamp(foilProbability, 0, 100);
            OverallSuccessProbability = ObjectiveSuccessProbability * (1d - FoilProbability / 100d);
        }
    }
}
