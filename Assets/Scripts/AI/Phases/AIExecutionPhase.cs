using System;
using System.Collections.Generic;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Util.Logging;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Executes proposals selected for the turn.
    /// </summary>
    public sealed class AIExecutionPhase : IAIIncrementalTurnPhase
    {
        private readonly GameResultBus _results;

        /// <summary>
        /// Creates an execution phase that observes completed changes between incremental steps.
        /// </summary>
        /// <param name="results">The completed game-result bus, when execution can span frames.</param>
        public AIExecutionPhase(GameResultBus results = null)
        {
            _results = results;
        }

        /// <summary>
        /// Executes selected proposals that still pass validation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            using var performanceMeasurement = PerformanceLogger.Measure(
                nameof(Execute),
                context?.Faction?.PeekInstanceID(),
                subjectType: typeof(AIExecutionPhase)
            );
            foreach (object _ in ExecuteIncrementally(context)) { }
        }

        /// <summary>
        /// Executes selected proposals one at a time.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>A sequence containing one marker per executed proposal.</returns>
        public IEnumerable<object> ExecuteIncrementally(AITurnContext context)
        {
            if (context?.SelectedProposals == null)
                yield break;

            using IDisposable resultObservation = _results?.ObserveAll(_ =>
            {
                using var performanceObservation = PerformanceLogger.Measure(
                    "Observe result",
                    context.Faction?.PeekInstanceID(),
                    subjectType: typeof(AIExecutionPhase)
                );
                context.InvalidateManufacturingBudget();
            });
            foreach (
                AIProposal proposal in PerformanceLogger.MeasureSteps(
                    GetExecutionOrder(context),
                    "Execution order",
                    context.Faction?.PeekInstanceID(),
                    subjectType: typeof(AIExecutionPhase)
                )
            )
            {
                bool canExecute;
                using (
                    PerformanceLogger.Measure(
                        "CanExecute",
                        context.Faction?.PeekInstanceID(),
                        items: 1,
                        subjectType: proposal?.GetType()
                    )
                )
                    canExecute = proposal?.CanExecute(context) == true;
                if (canExecute)
                {
                    using (
                        PerformanceLogger.Measure(
                            "Execute proposal",
                            context.Faction?.PeekInstanceID(),
                            items: 1,
                            subjectType: proposal.GetType()
                        )
                    )
                        proposal.Execute(context);
                    if (proposal is not AIManufactureProposal)
                        using (
                            PerformanceLogger.Measure(
                                "Invalidate manufacturing budget",
                                context.Faction?.PeekInstanceID(),
                                subjectType: typeof(AIExecutionPhase)
                            )
                        )
                            context.InvalidateManufacturingBudget();
                }
                yield return proposal;
            }
        }

        /// <summary>
        /// Places selected local boarding immediately before the same fleet's selected attack departure.
        /// </summary>
        /// <param name="context">The turn containing the selected actions.</param>
        /// <returns>Each selected proposal once, with all other proposals retaining their relative order.</returns>
        private static IEnumerable<AIProposal> GetExecutionOrder(AITurnContext context)
        {
            IReadOnlyList<AIProposal> selected = context.SelectedProposals;
            bool[] promoted = new bool[selected.Count];
            for (int index = 0; index < selected.Count; index++)
            {
                if (promoted[index])
                    continue;

                AIProposal proposal = selected[index];
                if (
                    proposal is AIFleetAttackProposal attack
                    && attack.Status != FleetOrderStatus.Returning
                    && attack.TargetPlanet != null
                    && attack.Fleet?.Movement == null
                    && attack.Fleet?.IsInCombat == false
                    && attack.Fleet.GetParentOfType<Planet>() is Planet origin
                    && origin.InstanceID != attack.TargetPlanet.InstanceID
                    && attack.CanExecute(context)
                )
                {
                    for (int later = index + 1; later < selected.Count; later++)
                    {
                        if (
                            promoted[later]
                            || selected[later] is not AITransferUnitProposal transfer
                            || transfer.TargetFleet != attack.Fleet
                            || transfer.TargetPlanet?.InstanceID != attack.TargetPlanet.InstanceID
                            || !transfer.CanBoardImmediately(context)
                        )
                            continue;

                        promoted[later] = true;
                        yield return transfer;
                    }
                }

                yield return proposal;
            }
        }
    }
}
