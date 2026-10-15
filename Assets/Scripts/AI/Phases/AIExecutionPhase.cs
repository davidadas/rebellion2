using System;
using System.Collections.Generic;
using System.Diagnostics;
using Rebellion.AI.Proposals;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Executes proposals selected for the turn.
    /// </summary>
    public sealed class AIExecutionPhase : IAIIncrementalTurnPhase
    {
        private readonly Func<long> _getTimestamp;
        private readonly long _workSliceTimestampCount;

        /// <summary>
        /// Creates an execution phase with a four-millisecond scheduling budget.
        /// </summary>
        public AIExecutionPhase()
            : this(Stopwatch.GetTimestamp, Math.Max(1, Stopwatch.Frequency / 250)) { }

        /// <summary>
        /// Creates an execution phase with an injectable scheduling clock.
        /// </summary>
        /// <param name="getTimestamp">Returns the current scheduling timestamp.</param>
        /// <param name="workSliceTimestampCount">Maximum timestamp count processed before yielding.</param>
        internal AIExecutionPhase(Func<long> getTimestamp, long workSliceTimestampCount)
        {
            _getTimestamp = getTimestamp ?? throw new ArgumentNullException(nameof(getTimestamp));
            _workSliceTimestampCount = Math.Max(1, workSliceTimestampCount);
        }

        /// <summary>
        /// Executes selected proposals that still pass validation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            foreach (object _ in ExecuteIncrementally(context)) { }
        }

        /// <summary>
        /// Executes selected proposals within bounded scheduling slices.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>A sequence containing the last proposal in each completed work slice.</returns>
        public IEnumerable<object> ExecuteIncrementally(AITurnContext context)
        {
            if (context?.SelectedProposals == null)
                yield break;

            long sliceStartedAt = _getTimestamp();
            AIProposal pendingMarker = null;
            foreach (AIProposal proposal in context.SelectedProposals)
            {
                if (proposal?.CanExecute(context) == true)
                    proposal.Execute(context);
                pendingMarker = proposal;
                if (_getTimestamp() - sliceStartedAt < _workSliceTimestampCount)
                    continue;

                yield return pendingMarker;
                pendingMarker = null;
                sliceStartedAt = _getTimestamp();
            }

            if (pendingMarker != null)
                yield return pendingMarker;
        }
    }
}
