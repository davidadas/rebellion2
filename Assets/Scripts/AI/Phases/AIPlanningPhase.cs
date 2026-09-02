using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Generates proposals for the current faction turn.
    /// </summary>
    public sealed class AIPlanningPhase : IAITurnPhase, IAIIncrementalTurnPhase
    {
        private readonly List<IAIProposalPlanner> _planners;

        /// <summary>
        /// Creates a planning phase with the default proposal planners.
        /// </summary>
        public AIPlanningPhase()
            : this(
                new IAIProposalPlanner[]
                {
                    new AIAbortMissionPlanner(),
                    new AIMissionPlanner(),
                    new AIOrbitalEngagementPlanner(),
                    new AIFleetPlanner(),
                    new AIProductionPlanner(),
                }
            ) { }

        /// <summary>
        /// Creates a planning phase with the supplied proposal planners.
        /// </summary>
        /// <param name="planners">Proposal planners run by this phase.</param>
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
            foreach (object _ in ExecuteIncrementally(context)) { }
        }

        /// <summary>
        /// Runs proposal planners one at a time.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>A sequence containing one marker per completed planner.</returns>
        public IEnumerable<object> ExecuteIncrementally(AITurnContext context)
        {
            if (context == null)
                yield break;

            foreach (IAIProposalPlanner planner in _planners)
            {
                context.AddProposals(planner.Plan(context));
                yield return null;
            }
        }
    }
}
