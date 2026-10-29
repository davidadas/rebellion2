using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Generates proposals for the current faction turn.
    /// </summary>
    public sealed class AIPlanningPhase : IAIIncrementalTurnPhase
    {
        private readonly List<IAIProposalPlanner> _planners;
        private readonly System.Func<long> _getTimestamp;

        /// <summary>
        /// Creates a planning phase with the supplied proposal planners.
        /// </summary>
        /// <param name="planners">Proposal planners run by this phase.</param>
        internal AIPlanningPhase(IEnumerable<IAIProposalPlanner> planners)
            : this(planners, Stopwatch.GetTimestamp) { }

        /// <summary>
        /// Creates ordered proposal planning with a monotonic frame-budget clock.
        /// </summary>
        /// <param name="planners">The proposal planners run by this phase.</param>
        /// <param name="getTimestamp">Returns monotonic timestamps measured in Stopwatch.Frequency units.</param>
        internal AIPlanningPhase(
            IEnumerable<IAIProposalPlanner> planners,
            System.Func<long> getTimestamp
        )
        {
            _getTimestamp =
                getTimestamp ?? throw new System.ArgumentNullException(nameof(getTimestamp));
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
                if (planner is IAIIncrementalProposalPlanner incrementalPlanner)
                {
                    List<AIProposal> proposals = new();
                    double budget =
                        context.Game?.Config?.GameSpeed?.TickWorkBudgetMilliseconds ?? 0;
                    long started = _getTimestamp();
                    foreach (
                        object step in incrementalPlanner.PlanIncrementally(context, proposals)
                    )
                    {
                        if (
                            budget > 0
                            && (_getTimestamp() - started) * 1000d / Stopwatch.Frequency >= budget
                        )
                        {
                            yield return planner;
                            started = _getTimestamp();
                        }
                    }
                    context.AddProposals(proposals);
                }
                else
                    context.AddProposals(planner.Plan(context));
                yield return planner;
            }
        }
    }
}
