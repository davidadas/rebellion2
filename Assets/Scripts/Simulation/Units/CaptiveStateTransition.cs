using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>Applies shared captive-state transitions independently of their trigger.</summary>
    internal static class CaptiveStateTransition
    {
        /// <summary>Captures an officer and describes the resulting custody change.</summary>
        /// <param name="officer">The officer being captured.</param>
        /// <param name="captor">The faction taking custody.</param>
        /// <param name="capturingUnit">The unit responsible for the capture, when present.</param>
        /// <param name="context">The location where the capture occurred.</param>
        /// <param name="tick">The tick when the capture occurred.</param>
        /// <param name="canEscape">Whether the officer may attempt to escape.</param>
        /// <returns>The capture-state change, or null when the officer cannot be captured.</returns>
        internal static OfficerCaptureStateResult Capture(
            Officer officer,
            Faction captor,
            ISceneNode capturingUnit,
            Planet context,
            int tick,
            bool canEscape = true
        )
        {
            if (
                officer == null
                || captor == null
                || !officer.TryCapture(captor.InstanceID, canEscape)
            )
                return null;

            return new OfficerCaptureStateResult
            {
                TargetOfficer = officer,
                IsCaptured = true,
                CaptorInstanceID = captor.InstanceID,
                ParentAtCapture = officer.GetParent(),
                CapturingUnit = capturingUnit,
                Context = context,
                Tick = tick,
            };
        }

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
