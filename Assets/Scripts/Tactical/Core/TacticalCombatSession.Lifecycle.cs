using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Game.Combat;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        /// <summary>
        /// Starts the local player's arrival presentation before admitting battle commands or clocks.
        /// </summary>
        /// <param name="presentation">The renderer that owns arrival poses and the pause-control strobe.</param>
        public void BeginArrival(ITacticalCombatArrivalPresentation presentation)
        {
            if (_arrival != null || SystemTime != 3)
                throw new InvalidOperationException(
                    "Arrival can only begin once before combat starts."
                );

            List<TacticalCombatArrivalMember> members = new();
            foreach (TacticalCombatGroup group in _groups)
            {
                if (group.Side != PlayerSide)
                    continue;
                foreach (CombatUnit unit in group.Members)
                    members.Add(
                        new TacticalCombatArrivalMember(
                            unit.Index,
                            unit.Definition.PresentationIndex,
                            unit.Side,
                            unit.IsFighter,
                            unit.Status == CombatUnitStatus.Docked,
                            new Vector4(unit.Position, -1),
                            new Vector4(unit.Heading, -1)
                        )
                    );
            }
            _arrival = new TacticalCombatArrival(
                PlayerSide,
                members,
                GetSide(PlayerSide).Forward,
                presentation,
                messageID => Audio.EnqueueNotification(messageID)
            );
            _arrivalStarting = true;
        }

        /// <summary>
        /// Binds the explicit millisecond provider used by the production battle scheduler.
        /// This clock remains live during pause and is sampled only after arrival admits scheduling.
        /// </summary>
        /// <param name="clock">The wrapping unsigned monotonic playback clock.</param>
        public void ConfigureClockedAdvance(Func<uint> clock)
        {
            RequireSimulationAuthority();
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            if (_scheduler != null)
                throw new InvalidOperationException(
                    "The battle clock must be bound before advancement."
                );
            _scheduler = new TacticalCombatScheduler(clock, new SchedulerSteps(this));
        }

        /// <summary>
        /// Advances entry and briefing frame time separately from the explicitly bound battle clock.
        /// Pausing suspends scheduler calls without resetting or stopping its supplied clock.
        /// </summary>
        /// <param name="elapsedMilliseconds">The frame interval for entry, briefing, and continuous-work admission.</param>
        public void AdvanceClocked(float elapsedMilliseconds)
        {
            RequireSimulationAuthority();
            if (_scheduler == null)
                throw new InvalidOperationException(
                    "The battle requires an explicit clock before clocked advancement."
                );
            if (!AdvanceArrival(elapsedMilliseconds) || IsComplete)
                return;
            _briefing.Advance(elapsedMilliseconds, CaptureBriefingState());
            AdvanceSynchronizedNotifications(elapsedMilliseconds);
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
            RequireSimulationAuthority();
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
                    new SchedulerSteps(this)
                );
            }
            _elapsedFrameClock += elapsedMilliseconds;
            AdvanceClocked(elapsedMilliseconds);
        }
    }
}
