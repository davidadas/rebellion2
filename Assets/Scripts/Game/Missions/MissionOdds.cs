using System;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Contains calculated visible objective, foiling, and overall mission probabilities.
    /// Hidden betrayal state is deliberately excluded from planning odds.
    /// </summary>
    public sealed class MissionOdds
    {
        public double ObjectiveSuccessProbability { get; }

        public double FoilProbability { get; }

        public double PersonnelLossProbability { get; }

        public double OverallSuccessProbability { get; }

        /// <summary>
        /// Creates complete mission odds from the objective and pre-objective foiling chances.
        /// </summary>
        /// <param name="objectiveSuccessProbability">Chance that the objective reports success if reached.</param>
        /// <param name="foilProbability">Chance that the mission is foiled before the objective.</param>
        /// <param name="personnelLossProbability">Chance that foiling removes at least one main officer.</param>
        internal MissionOdds(
            double objectiveSuccessProbability,
            double foilProbability,
            double personnelLossProbability = 0
        )
        {
            ObjectiveSuccessProbability = Math.Clamp(objectiveSuccessProbability, 0, 100);
            FoilProbability = Math.Clamp(foilProbability, 0, 100);
            PersonnelLossProbability = Math.Clamp(personnelLossProbability, 0, 100);
            OverallSuccessProbability = ObjectiveSuccessProbability * (1d - FoilProbability / 100d);
        }
    }
}
