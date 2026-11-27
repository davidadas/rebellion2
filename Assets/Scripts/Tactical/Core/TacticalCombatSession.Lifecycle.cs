using System;
using Rebellion.Game.Results;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Supplies the local view's entry timing to the encounter admission gate.
    /// </summary>
    public interface ITacticalCombatEntry
    {
        bool IsComplete { get; }

        /// <summary>
        /// Advances the view's entry sequence by one presentation frame.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed interval in milliseconds.</param>
        /// <returns>Whether the local entry sequence has released battle admission.</returns>
        bool Advance(float elapsedMilliseconds);
    }

    public sealed partial class TacticalCombatSession
    {
        /// <summary>
        /// Holds battle admission until the local arrival reports completion.
        /// </summary>
        /// <param name="entry">The view sequence that releases battle admission.</param>
        public void BeginArrival(ITacticalCombatEntry entry)
        {
            if (_arrival != null || WasRestored || SystemTime != 3)
                throw new InvalidOperationException(
                    "Arrival can only begin once before combat starts."
                );

            _arrival = entry ?? throw new ArgumentNullException(nameof(entry));
            _arrivalStarting = true;
        }

        /// <summary>
        /// Binds the explicit millisecond provider used by the production battle scheduler.
        /// This clock remains live during pause and is sampled only after arrival admits scheduling.
        /// </summary>
        /// <param name="clock">The wrapping unsigned monotonic playback clock.</param>
        public void ConfigureClockedAdvance(Func<uint> clock)
        {
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            if (_scheduler != null)
                throw new InvalidOperationException(
                    "The battle clock must be bound before advancement."
                );
            _scheduler = new TacticalCombatScheduler(clock, new SchedulerSteps(this), _clockState);
        }

        /// <summary>
        /// Queues a notification for the player controlling the battle.
        /// </summary>
        /// <param name="side">The side addressed by the notification.</param>
        /// <param name="message">The authored notification identity.</param>
        internal void EnqueueFactionNotification(CombatSide side, int message)
        {
            if (side == PlayerSide)
                Audio.EnqueueNotification(message);
        }

        /// <summary>
        /// Advances entry and briefing frame time separately from the explicitly bound battle clock.
        /// Pausing suspends scheduler calls without resetting or stopping its supplied clock.
        /// </summary>
        /// <param name="elapsedMilliseconds">The frame interval for entry, briefing, and continuous-work admission.</param>
        public void AdvanceClocked(float elapsedMilliseconds)
        {
            if (_scheduler == null)
                throw new InvalidOperationException(
                    "The battle requires an explicit clock before clocked advancement."
                );
            if (!AdvanceArrival(elapsedMilliseconds) || IsComplete)
                return;
            _briefing.Advance(
                elapsedMilliseconds,
                CaptureBattleStationAttackReadiness(),
                IsArriving
            );
            if (IsPaused)
            {
                Selection.ProcessPendingCommands();
                return;
            }
            if (_scheduler.Advance(elapsedMilliseconds * 0.4f))
                Complete();
        }

        /// <summary>
        /// Presents entry before either battle-advancement contract admits commands or clocks.
        /// </summary>
        /// <param name="elapsedMilliseconds">The frame interval used by the entry owner.</param>
        /// <returns>Whether entry has released the subsequent battle operations.</returns>
        private bool AdvanceArrival(float elapsedMilliseconds)
        {
            if (!IsArriving)
                return true;
            if (_arrivalStarting)
            {
                _arrivalStarting = false;
                return false;
            }
            return _arrival.Advance(elapsedMilliseconds);
        }

        /// <summary>
        /// Advances using the frame intervals as a controllable millisecond clock.
        /// A zero interval flushes queued orders without advancing battle time.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed frame duration.</param>
        public void Advance(float elapsedMilliseconds)
        {
            if (!(elapsedMilliseconds > 0) || float.IsInfinity(elapsedMilliseconds))
            {
                if (AdvanceArrival(elapsedMilliseconds))
                    Selection.ProcessPendingCommands();
                return;
            }
            if (_scheduler != null && !_usesFrameClock)
                throw new InvalidOperationException(
                    "Clock-bound encounters require clocked advancement."
                );
            if (_scheduler == null)
            {
                _usesFrameClock = true;
                _scheduler = new TacticalCombatScheduler(
                    () => unchecked((uint)(ulong)_elapsedFrameClock),
                    new SchedulerSteps(this),
                    _clockState
                );
            }
            _elapsedFrameClock += elapsedMilliseconds;
            AdvanceClocked(elapsedMilliseconds);
        }
    }
}
