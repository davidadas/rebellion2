using System;
using Rebellion.Game.Results;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Projects stable, content-facing bindings from simulation results into an event execution.
    /// Result-specific knowledge remains outside the execution-context state container.
    /// </summary>
    internal static class GameEventTriggerBindings
    {
        /// <summary>
        /// Adds the bindings exposed by one triggering result.
        /// </summary>
        /// <param name="context">The event execution receiving the bindings.</param>
        /// <param name="result">The result that activated the event, if any.</param>
        internal static void Bind(GameEventExecutionContext context, GameResult result)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            switch (result)
            {
                case UnitArrivedResult arrival:
                    context.Bind("unit", arrival.Unit);
                    context.Bind("destination", arrival.Destination);
                    context.Bind("planet", arrival.Destination);
                    break;
                case OfficerEncounterResult encounter:
                    context.Bind("officer", encounter.EncounteredOfficer);
                    context.Bind("opponent", encounter.OpposingOfficer);
                    break;
                case OfficerCaptureStateResult capture:
                    context.Bind("officer", capture.TargetOfficer ?? capture.CapturedOfficer);
                    context.Bind("linkedOfficer", capture.LinkedOfficer);
                    context.Bind("context", capture.Context);
                    break;
                case MissionCompletedResult completion:
                    context.Bind("mission", completion.Mission);
                    break;
            }
        }
    }
}
