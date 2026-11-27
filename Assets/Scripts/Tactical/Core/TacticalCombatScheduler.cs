using System;
using Rebellion.Game.Combat;

namespace Rebellion.Tactical
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
        private readonly Func<uint> _clock;
        private readonly ITacticalCombatSchedulerSteps _steps;
        private readonly ActiveBattle.ClockState _state;
        private uint _clockOffset;

        public uint Progress => _state.Progress;
        public uint SystemTime => _state.SystemTime;

        /// <summary>
        /// Borrows an explicit clock and the encounter operations whose ordering this scheduler owns.
        /// The clock continues while callers suspend ticks for pause; suspension never resets its bases.
        /// </summary>
        /// <param name="clock">The wrapping unsigned monotonic millisecond provider.</param>
        /// <param name="steps">The concrete encounter operations.</param>
        public TacticalCombatScheduler(Func<uint> clock, ITacticalCombatSchedulerSteps steps)
            : this(clock, steps, new ActiveBattle.ClockState()) { }

        /// <summary>
        /// Advances the saved clock in place, aligning a new provider with its previous sample.
        /// </summary>
        /// <param name="clock">The wrapping unsigned monotonic millisecond provider.</param>
        /// <param name="steps">The concrete encounter operations.</param>
        /// <param name="state">The battle's clock and dispatch state.</param>
        internal TacticalCombatScheduler(
            Func<uint> clock,
            ITacticalCombatSchedulerSteps steps,
            ActiveBattle.ClockState state
        )
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            RebaseClock();
        }

        /// <summary>
        /// Aligns a restored platform clock with the battle's last saved timestamp.
        /// </summary>
        internal void RebaseClock()
        {
            if (_state.Initialized)
                _clockOffset = unchecked(_state.LastSample - _clock());
        }

        /// <summary>
        /// Stores the save boundary's timestamp without dispatching any work.
        /// </summary>
        internal void CaptureTime()
        {
            if (_state.Initialized)
                SampleClock();
        }

        /// <summary>
        /// Samples the clock in the encounter's original time coordinates.
        /// </summary>
        /// <returns>The clock time stored for later continuation.</returns>
        private uint SampleClock()
        {
            _state.LastSample = unchecked(_clock() + _clockOffset);
            return _state.LastSample;
        }

        /// <summary>
        /// Processes one admitted frame, initializing clock bases only on the first call.
        /// A caller suspends this method during pause while retaining the same clock and scheduler.
        /// </summary>
        /// <param name="scaledFrameMilliseconds">The scaled frame interval used only to admit continuous work.</param>
        /// <returns>Whether a completed ending interval or immediate terminal result finishes the encounter.</returns>
        public bool Advance(float scaledFrameMilliseconds)
        {
            uint now = SampleClock();
            if (!_state.Initialized)
            {
                _state.Initialized = true;
                _state.CoarseBase = now;
                _state.MovementBase = now;
                _state.Progress = 0;
                _state.SystemTime = ActiveBattle.ClockState.InitialSystemTime;
                _state.Phase = ActiveBattle.ClockPhase.Commands;
            }

            uint elapsed = unchecked(now - _state.CoarseBase);
            if (elapsed >= _periodMilliseconds)
                elapsed = elapsed % _periodMilliseconds + _periodMilliseconds;

            _steps.DrainCommands();

            if (elapsed >= _periodMilliseconds)
            {
                if (_state.Phase == ActiveBattle.ClockPhase.Movement)
                {
                    if (_steps.HasTerminalResult)
                        return true;
                    if (!_steps.IsRunning && _steps.AdvanceEndingTick())
                        return true;
                }
                else
                    scaledFrameMilliseconds = 0;

                _state.Phase = ActiveBattle.ClockPhase.Commands;
                elapsed -= _periodMilliseconds;
                _state.Progress = unchecked(Progress + 1);
            }

            if (_state.Phase == ActiveBattle.ClockPhase.Commands)
            {
                _state.CoarseBase = unchecked(now - elapsed);
                if (Progress >= ActiveBattle.ClockState.InitialSystemTime)
                    _steps.DispatchCommands(unchecked(Progress - 1));
                _state.Phase = ActiveBattle.ClockPhase.Systems;
            }
            else if (_state.Phase == ActiveBattle.ClockPhase.Systems)
            {
                if (_steps.IsRunning)
                    _steps.AdvanceSystems();
                _state.SystemTime = unchecked(SystemTime + 1);
                _state.Phase = ActiveBattle.ClockPhase.Movement;
            }

            if (!float.IsNaN(scaledFrameMilliseconds) && scaledFrameMilliseconds != 0)
                scaledFrameMilliseconds =
                    unchecked(SampleClock() - _state.MovementBase) % _periodMilliseconds;
            if (
                !float.IsNaN(scaledFrameMilliseconds)
                && scaledFrameMilliseconds != 0
                && Progress >= ActiveBattle.ClockState.InitialSystemTime
                && _steps.IsRunning
            )
            {
                _state.MovementBase = unchecked(
                    _state.MovementBase + (uint)scaledFrameMilliseconds
                );
                _steps.AdvanceMovement(
                    (float)_state.MovementBase - scaledFrameMilliseconds,
                    scaledFrameMilliseconds
                );
            }
            return false;
        }
    }
}
