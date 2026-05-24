using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Generates proposals for the current faction turn.
    /// </summary>
    public sealed class AIPlanningPhase : IAITurnPhase
    {
        private readonly List<IAIProposalPlanner> _planners = new List<IAIProposalPlanner>
        {
            new AIMissionPlanner(),
            new AIFleetPlanner(),
            new AIProductionPlanner(),
        };

        /// <summary>
        /// Runs all proposal planners for the current turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            if (context == null)
                return;

            foreach (IAIProposalPlanner planner in _planners)
                context.AddProposals(planner.Plan(context));
        }
    }
}
