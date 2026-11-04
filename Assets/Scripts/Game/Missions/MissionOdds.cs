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
        /// Creates complete mission odds from the objective and cumulative encounter chances.
        /// </summary>
        /// <param name="objectiveSuccessProbability">Chance that the objective reports success if reached.</param>
        /// <param name="foilProbability">Chance that the mission is foiled before the objective.</param>
        /// <param name="personnelLossProbability">Chance that encounters kill or capture at least one main or decoy officer.</param>
        internal MissionOdds(
            double objectiveSuccessProbability,
            double foilProbability,
            double personnelLossProbability = 0
        )
        {
            ObjectiveSuccessProbability = Math.Clamp(objectiveSuccessProbability, 0, 100);
            FoilProbability = Math.Clamp(foilProbability, 0, 100);
            PersonnelLossProbability = Math.Clamp(personnelLossProbability, 0, 100);
            OverallSuccessProbability = CalculateOverallSuccessProbability(
                ObjectiveSuccessProbability,
                FoilProbability
            );
        }

        /// <summary>
        /// Combines objective and encounter probabilities using the same rounding as reported odds.
        /// </summary>
        /// <param name="objectiveSuccessProbability">The objective success percentage.</param>
        /// <param name="foilProbability">The percentage foiled before the objective.</param>
        /// <returns>The overall success percentage.</returns>
        internal static double CalculateOverallSuccessProbability(
            double objectiveSuccessProbability,
            double foilProbability
        ) =>
            Math.Clamp(objectiveSuccessProbability, 0, 100)
            * (1d - Math.Clamp(foilProbability, 0, 100) / 100d);
    }
}
