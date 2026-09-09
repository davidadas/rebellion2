namespace Rebellion.AI.Director
{
    /// <summary>
    /// Holds turn-scoped strategic policies shared by AI planners and scorers.
    /// </summary>
    internal sealed class AIStrategicPolicies
    {
        internal AIProductionSitePolicy ProductionSites { get; }
        internal AISabotageTargetPolicy SabotageTargets { get; }

        /// <summary>
        /// Creates policies backed by the current turn assessment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        internal AIStrategicPolicies(AITurnContext context)
        {
            ProductionSites = new AIProductionSitePolicy(context);
            SabotageTargets = new AISabotageTargetPolicy(
                context.Assessment,
                context.Game?.Config?.AI?.MissionPlanning
            );
        }
    }
}
