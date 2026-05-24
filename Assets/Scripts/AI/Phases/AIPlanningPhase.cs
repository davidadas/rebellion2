using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Generates proposals for the current faction turn.
    /// </summary>
    public sealed class AIPlanningPhase : IAITurnPhase
    {
        private readonly List<IAIProposalPlanner> _planners;

        public AIPlanningPhase()
            : this(
                new IAIProposalPlanner[]
                {
                    new AIMissionPlanner(),
                    new AIFleetPlanner(),
                    new AIProductionPlanner(),
                }
            ) { }

        internal AIPlanningPhase(IEnumerable<IAIProposalPlanner> planners)
        {
            if (planners == null)
                throw new System.ArgumentNullException(nameof(planners));

            _planners = planners.ToList();
            if (_planners.Any(planner => planner == null))
                throw new System.ArgumentException(
                    "Planner list cannot contain null entries.",
                    nameof(planners)
                );
        }

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
