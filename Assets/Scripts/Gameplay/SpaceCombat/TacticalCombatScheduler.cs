using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns the encounter operations admitted by the detailed battle clock.
    /// </summary>
    public interface ITacticalCombatSchedulerSteps
    {
        bool IsRunning { get; }
        bool HasTerminalResult { get; }

        /// <summary>
        /// Applies pending display commands and refreshes their notification queues.
        /// </summary>
        void DrainCommands();

        /// <summary>
        /// Dispatches the ordered commands admitted for one completed progress interval.
        /// </summary>
        /// <param name="iteration">The completed command interval.</param>
        void DispatchCommands(uint iteration);

        /// <summary>
        /// Advances the ordered side, group, and unit operations for one whole second.
        /// </summary>
        void AdvanceSystems();

        /// <summary>
        /// Consumes one whole-second ending interval after a completed dispatch phase.
        /// </summary>
        /// <returns>Whether the encounter is ready to finish.</returns>
        bool AdvanceEndingTick();

        /// <summary>
        /// Advances continuous formations, motion, and departures with the admitted clock remainder.
        /// </summary>
        /// <param name="startMilliseconds">The float timestamp preceding this continuous interval.</param>
        /// <param name="elapsedMilliseconds">The admitted continuous interval, between one and 999 milliseconds.</param>
        void AdvanceMovement(float startMilliseconds, float elapsedMilliseconds);
    }

    /// <summary>
    /// Coordinates detailed battle phases using an explicitly supplied wrapping millisecond clock.
    /// Frame elapsed time admits continuous work; the clock determines its actual interval.
    /// </summary>
    public sealed class TacticalCombatScheduler
    {
        private const uint _periodMilliseconds = 1000;
        private const uint _initialProgress = 3;
        private readonly Func<uint> _clock;
        private readonly ITacticalCombatSchedulerSteps _steps;
        private bool _initialized;
        private uint _coarseBase;
        private uint _movementBase;
        private uint _phase;

        public uint Progress { get; private set; }
        public uint SystemTime { get; private set; } = _initialProgress;

        /// <summary>
        /// Borrows an explicit clock and the encounter operations whose ordering this scheduler owns.
        /// The clock continues while callers suspend ticks for pause; suspension never resets its bases.
        /// </summary>
        /// <param name="clock">The wrapping unsigned monotonic millisecond provider.</param>
        /// <param name="steps">The concrete encounter operations.</param>
        public TacticalCombatScheduler(Func<uint> clock, ITacticalCombatSchedulerSteps steps)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        }

        /// <summary>
        /// Processes one admitted frame, initializing clock bases only on the first call.
        /// A caller suspends this method during pause while retaining the same clock and scheduler.
        /// </summary>
        /// <param name="scaledFrameMilliseconds">The scaled frame interval used only to admit continuous work.</param>
        /// <returns>Whether a completed ending interval or immediate terminal result finishes the encounter.</returns>
        public bool Advance(float scaledFrameMilliseconds)
        {
            uint now = _clock();
            if (!_initialized)
            {
                _initialized = true;
                _coarseBase = now;
                _movementBase = now;
                Progress = 0;
                SystemTime = _initialProgress;
                _phase = 0;
            }

            uint elapsed = unchecked(now - _coarseBase);
            if (elapsed >= _periodMilliseconds)
                elapsed = elapsed % _periodMilliseconds + _periodMilliseconds;

            _steps.DrainCommands();

            if (elapsed >= _periodMilliseconds)
            {
                if (_phase == 5)
                {
                    if (_steps.HasTerminalResult)
                        return true;
                    if (!_steps.IsRunning && _steps.AdvanceEndingTick())
                        return true;
                }
                else
                    scaledFrameMilliseconds = 0;

                _phase = 0;
                elapsed -= _periodMilliseconds;
                Progress = unchecked(Progress + 1);
            }

            if (_phase == 0)
            {
                _coarseBase = unchecked(now - elapsed);
                if (Progress >= _initialProgress)
                    _steps.DispatchCommands(unchecked(Progress - 1));
                _phase = 1;
            }
            else if (_phase == 1)
            {
                if (_steps.IsRunning)
                    _steps.AdvanceSystems();
                SystemTime = unchecked(SystemTime + 1);
                _phase = 5;
            }

            if (!float.IsNaN(scaledFrameMilliseconds) && scaledFrameMilliseconds != 0)
                scaledFrameMilliseconds = unchecked(_clock() - _movementBase) % _periodMilliseconds;
            if (
                !float.IsNaN(scaledFrameMilliseconds)
                && scaledFrameMilliseconds != 0
                && Progress >= _initialProgress
                && _steps.IsRunning
            )
            {
                _movementBase = unchecked(_movementBase + (uint)scaledFrameMilliseconds);
                _steps.AdvanceMovement(
                    (float)_movementBase - scaledFrameMilliseconds,
                    scaledFrameMilliseconds
                );
            }
            return false;
        }
    }
}
