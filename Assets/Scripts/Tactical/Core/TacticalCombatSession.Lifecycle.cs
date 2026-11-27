using System;
using System.Numerics;
using Rebellion.Game.Combat;
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

    /// <summary>
    /// Coordinates the shared encounter, its local entry, and movie acknowledgements.
    /// </summary>
    public interface ITacticalCombatPresentationDriver : IDisposable
    {
        TacticalCombatSession Session { get; }
        ITacticalCombatPresentationTimeline ReplicaPlayback { get; }
        bool IsAuthoritative { get; }
        bool CompletionReady { get; }
        SpaceCombatResult CommittedResult { get; }

        /// <summary>
        /// Begins local entry presentation after the required combat assets are ready.
        /// </summary>
        /// <param name="presentation">The local owner of entry transforms.</param>
        void Prepare(ITacticalCombatEntry presentation);

        /// <summary>
        /// Advances the owning playback boundary for one unscaled presentation frame.
        /// </summary>
        /// <param name="elapsedSeconds">The unscaled presentation interval.</param>
        void Advance(float elapsedSeconds);

        /// <summary>
        /// Reports that this view has finished an admitted movie.
        /// </summary>
        /// <param name="request">The movie whose local playback ended.</param>
        void AcknowledgeMovie(TacticalCombatMovieRequest request);
    }

    /// <summary>
    /// Supplies the renderer with poses sampled from the multiplayer playback clock.
    /// </summary>
    public interface ITacticalCombatPresentationTimeline
    {
        TacticalCombatSession Session { get; }
        double TimestampMilliseconds { get; }

        /// <summary>
        /// Samples a participant's position and heading at the presentation timestamp.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="position">The sampled canonical position.</param>
        /// <param name="heading">The sampled heading.</param>
        /// <returns>Whether a pose exists for this participant.</returns>
        bool TryGetPose(string unitID, out Vector3 position, out Vector3 heading);

        /// <summary>
        /// Samples motion and lifecycle at the presentation timestamp.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="position">The sampled canonical position.</param>
        /// <param name="heading">The sampled heading.</param>
        /// <param name="hyperspaceMilliseconds">The sampled departure animation time.</param>
        /// <param name="status">The sampled lifecycle.</param>
        /// <param name="isAlive">Whether the sampled hull was alive.</param>
        /// <returns>Whether a pose exists for this participant.</returns>
        bool TryGetPose(
            string unitID,
            out Vector3 position,
            out Vector3 heading,
            out float hyperspaceMilliseconds,
            out CombatUnitStatus status,
            out bool isAlive
        );

        /// <summary>
        /// Samples visibility without changing current command eligibility.
        /// </summary>
        /// <param name="unitID">The stable participant identity.</param>
        /// <param name="status">The sampled lifecycle.</param>
        /// <param name="isAlive">Whether the sampled hull was alive.</param>
        /// <returns>Whether a lifecycle exists for this participant.</returns>
        bool TryGetLifecycle(string unitID, out CombatUnitStatus status, out bool isAlive);
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
            RequireSimulationAuthority();
            if (clock == null)
                throw new ArgumentNullException(nameof(clock));
            if (_scheduler != null)
                throw new InvalidOperationException(
                    "The battle clock must be bound before advancement."
                );
            _scheduler = new TacticalCombatScheduler(clock, new SchedulerSteps(this), _clockState);
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
            _briefing.Advance(
                elapsedMilliseconds,
                CaptureBattleStationAttackReadiness(),
                IsArriving
            );
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
                    new SchedulerSteps(this),
                    _clockState
                );
            }
            _elapsedFrameClock += elapsedMilliseconds;
            AdvanceClocked(elapsedMilliseconds);
        }
    }
}
