using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rebellion.Tactical
{
    public sealed partial class TacticalCombatSession
    {
        /// <summary>
        /// Starts the captured entering force's presentation before admitting battle commands or clocks.
        /// </summary>
        /// <param name="presentation">The renderer that owns arrival poses and the pause-control strobe.</param>
        public void BeginArrival(ITacticalCombatArrivalPresentation presentation)
        {
            if (!EnteringSide.HasValue)
                throw new InvalidOperationException(
                    "The encounter has no captured entering force."
                );
            if (_arrival != null || SystemTime != 3)
                throw new InvalidOperationException(
                    "Arrival can only begin once before combat starts."
                );

            List<TacticalCombatArrivalMember> members = new();
            foreach (TacticalCombatGroup group in _groups)
            {
                if (group.Side != EnteringSide.Value)
                    continue;
                foreach (TacticalCombatUnit unit in group.Members)
                    members.Add(
                        new TacticalCombatArrivalMember(
                            unit.Index,
                            unit.Definition.PresentationIndex,
                            unit.Side,
                            unit.IsFighter,
                            unit.Status == TacticalCombatUnitStatus.Docked,
                            new Vector4(unit.Position, -1),
                            new Vector4(unit.Heading, -1)
                        )
                    );
            }
            _arrival = new TacticalCombatArrival(
                EnteringSide.Value,
                members,
                GetSide(EnteringSide.Value).Forward,
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
            if (_scheduler != null || _legacyTimeAdvanced)
                throw new InvalidOperationException(
                    "The battle clock must be bound once before delta-driven time advances."
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
        /// Advances a delta-driven encounter used without a bound production clock.
        /// A zero interval can still drain pending commands after entry has completed.
        /// </summary>
        /// <param name="elapsedMilliseconds">The elapsed unpaused frame duration.</param>
        public void Advance(float elapsedMilliseconds)
        {
            RequireSimulationAuthority();
            if (_scheduler != null && elapsedMilliseconds != 0)
                throw new InvalidOperationException(
                    "Clock-bound encounters require clocked advancement."
                );
            if (elapsedMilliseconds != 0)
                _legacyTimeAdvanced = true;
            if (!AdvanceArrival(elapsedMilliseconds))
                return;
            Selection.ProcessPendingCommands();
            if (IsComplete || elapsedMilliseconds <= 0)
                return;
            _briefing.Advance(elapsedMilliseconds, CaptureBriefingState());
            AdvanceSynchronizedNotifications(elapsedMilliseconds);
            _elapsedInTick += elapsedMilliseconds;
            if (IsPaused)
                return;
            if (_elapsedInTick >= 1000)
            {
                _elapsedInTick %= 1000;
                if (_phase != 5)
                    elapsedMilliseconds = 0;
                else if (_completionStarted && _ending.AdvanceTerminalTick())
                {
                    Complete();
                    return;
                }
                _progress++;
                _phase = 0;
            }
            if (_phase == 0)
            {
                if (_progress >= 3)
                    DispatchCommands(_progress - 1);
                _phase = 1;
            }
            else if (_phase == 1)
            {
                if (!_completionStarted)
                    AdvanceSystems();
                _systemTime++;
                _phase = 5;
            }
            if (_progress >= 3 && !_completionStarted && elapsedMilliseconds > 0)
                AdvanceMovement(elapsedMilliseconds);
        }
    }
}
