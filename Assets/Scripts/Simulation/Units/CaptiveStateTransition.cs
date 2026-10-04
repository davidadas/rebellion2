using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>Applies shared captive-state transitions independently of their trigger.</summary>
    internal static class CaptiveStateTransition
    {
        /// <summary>Clears an officer's custody state and describes the release.</summary>
        /// <param name="officer">The officer being released.</param>
        /// <param name="context">The planet where the release occurred.</param>
        /// <param name="tick">The tick when the release occurred.</param>
        /// <param name="captorInstanceId">The faction that held the officer.</param>
        /// <returns>The resulting capture-state change.</returns>
        internal static OfficerCaptureStateResult Release(
            Officer officer,
            Planet context,
            int tick,
            string captorInstanceId
        )
        {
            officer.IsCaptured = false;
            officer.CaptorInstanceID = null;
            officer.CanEscape = false;
            officer.NextEscapeAttemptTick = 0;

            return new OfficerCaptureStateResult
            {
                TargetOfficer = officer,
                IsCaptured = false,
                CaptorInstanceID = captorInstanceId,
                Context = context,
                Tick = tick,
            };
        }
    }
}
