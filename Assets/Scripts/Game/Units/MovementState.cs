using System;
using System.Drawing;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Owns the running transit clock and shared journey metadata for a moving unit.
    /// A null movement state denotes a stationary unit.
    /// </summary>
    [PersistableObject]
    public class MovementState
    {
        [PersistableMember(Name = nameof(TransitTicks))]
        private int _transitTicks;

        [PersistableMember(Name = nameof(TicksElapsed))]
        private int _ticksElapsed;

        [PersistableMember(Name = nameof(ObservedAtTick))]
        private int? _observedAtTick;

        [PersistableMember(Name = nameof(IsObservation))]
        private bool _isObservation;

        [PersistableMember(Name = nameof(MovementGroupID))]
        private string _movementGroupID;

        [PersistableMember(Name = nameof(SourceEventInstanceID))]
        private string _sourceEventInstanceID;

        [PersistableMember(Name = nameof(ResolveEncounterOnArrival))]
        private bool _resolveEncounterOnArrival;

        [PersistableAttribute(Name = nameof(PreviousLocationInstanceID))]
        private string _previousLocationInstanceID;

        [PersistableMember(Name = nameof(DestinationPlanetInstanceID))]
        private string _destinationPlanetInstanceID;

        [PersistableMember(Name = nameof(NextLeg))]
        private MovementLeg _nextLeg;

        [PersistableMember(Name = nameof(OriginPositionX))]
        private int _originPositionX;

        [PersistableMember(Name = nameof(OriginPositionY))]
        private int _originPositionY;

        [PersistableMember(Name = nameof(CurrentPositionX))]
        private int _currentPositionX;

        [PersistableMember(Name = nameof(CurrentPositionY))]
        private int _currentPositionY;

        public int TransitTicks => _transitTicks;
        public int TicksElapsed => _ticksElapsed;
        public int? ObservedAtTick => _observedAtTick;
        public bool IsObservation => _isObservation;
        public string MovementGroupID => _movementGroupID;
        public string SourceEventInstanceID => _sourceEventInstanceID;
        public bool ResolveEncounterOnArrival => _resolveEncounterOnArrival;

        /// <summary>
        /// Gets the last visited container. Null means unknown; an empty ID means none remains valid.
        /// </summary>
        public string PreviousLocationInstanceID => _previousLocationInstanceID;

        /// <summary>
        /// Gets the physical endpoint of the running leg, which may differ from the final destination.
        /// </summary>
        public string DestinationPlanetInstanceID => _destinationPlanetInstanceID;

        public MovementLeg NextLeg => _nextLeg;
        public int OriginPositionX => _originPositionX;
        public int OriginPositionY => _originPositionY;
        public int CurrentPositionX => _currentPositionX;
        public int CurrentPositionY => _currentPositionY;
        public Point OriginPosition => new Point(_originPositionX, _originPositionY);
        public Point CurrentPosition => new Point(_currentPositionX, _currentPositionY);

        /// <summary>
        /// Creates an empty movement state for deserialization.
        /// </summary>
        public MovementState() { }

        /// <summary>
        /// Starts a journey with its route and shared context recorded together.
        /// </summary>
        /// <param name="leg">The initial transit leg.</param>
        /// <param name="previousLocationInstanceID">The last visited container, or null if unknown.</param>
        /// <param name="movementGroupID">The group travelling together.</param>
        /// <param name="sourceEventInstanceID">The event that requested this journey, when applicable.</param>
        /// <param name="resolveEncounterOnArrival">Whether arrival requires a personnel encounter check.</param>
        public MovementState(
            MovementLeg leg,
            string previousLocationInstanceID,
            string movementGroupID,
            string sourceEventInstanceID = null,
            bool resolveEncounterOnArrival = false
        )
        {
            ApplyLeg(leg ?? throw new ArgumentNullException(nameof(leg)));
            _previousLocationInstanceID = previousLocationInstanceID;
            _movementGroupID = movementGroupID;
            _sourceEventInstanceID = sourceEventInstanceID;
            _resolveEncounterOnArrival = resolveEncounterOnArrival;
        }

        /// <summary>
        /// Advances the running clock, starting the planned continuation when its first leg ends.
        /// </summary>
        /// <param name="ticks">The number of elapsed simulation ticks.</param>
        /// <param name="destinationPosition">The final destination's current position.</param>
        public void Advance(int ticks, Point destinationPosition)
        {
            if (ticks < 0)
                throw new ArgumentOutOfRangeException(nameof(ticks));
            _ticksElapsed += ticks;
            if (_nextLeg != null && _ticksElapsed >= _transitTicks)
            {
                int remainingElapsed = _ticksElapsed - _transitTicks;
                ApplyLeg(_nextLeg);
                _nextLeg = null;
                _ticksElapsed = remainingElapsed;
            }
            Point endpoint = _nextLeg?.OriginPosition ?? destinationPosition;
            float progress = Progress();
            SetPosition(
                new Point(
                    (int)(_originPositionX + (endpoint.X - _originPositionX) * progress),
                    (int)(_originPositionY + (endpoint.Y - _originPositionY) * progress)
                )
            );
        }

        /// <summary>
        /// Replaces the remaining route while retaining the journey's identity and history.
        /// </summary>
        /// <param name="leg">The new route from the unit's current position.</param>
        public void Retarget(MovementLeg leg)
        {
            ApplyLeg(leg ?? throw new ArgumentNullException(nameof(leg)));
            _nextLeg = null;
        }

        /// <summary>
        /// Preserves the running leg and replaces its planned continuation.
        /// </summary>
        /// <param name="onwardLeg">The route starting at the running leg's endpoint.</param>
        /// <param name="currentLegDestinationPlanetInstanceID">The running leg's physical endpoint.</param>
        /// <param name="previousLocationInstanceID">The remaining valid departure history.</param>
        public void Redirect(
            MovementLeg onwardLeg,
            string currentLegDestinationPlanetInstanceID,
            string previousLocationInstanceID
        )
        {
            if (onwardLeg == null)
                throw new ArgumentNullException(nameof(onwardLeg));
            _destinationPlanetInstanceID = currentLegDestinationPlanetInstanceID;
            _previousLocationInstanceID = previousLocationInstanceID;
            if (_ticksElapsed >= _transitTicks)
            {
                ApplyLeg(onwardLeg);
                _nextLeg = null;
            }
            else
                _nextLeg = onwardLeg;
        }

        /// <summary>
        /// Restarts the current leg's clock from the current position with a coordinated duration.
        /// </summary>
        /// <param name="transitTicks">The new duration of the current leg.</param>
        public void RetimeCurrentLeg(int transitTicks)
        {
            _originPositionX = _currentPositionX;
            _originPositionY = _currentPositionY;
            _transitTicks = transitTicks;
            _ticksElapsed = 0;
        }

        /// <summary>
        /// Records the remaining valid previously visited container after a destination check.
        /// </summary>
        /// <param name="previousLocationInstanceID">The container ID, or an empty ID when cleared.</param>
        public void RecordPreviousLocation(string previousLocationInstanceID)
        {
            _previousLocationInstanceID = previousLocationInstanceID;
        }

        /// <summary>
        /// Updates the current map position without changing the journey's clock or context.
        /// </summary>
        /// <param name="position">The unit's current position.</param>
        public void SetPosition(Point position)
        {
            _currentPositionX = position.X;
            _currentPositionY = position.Y;
        }

        /// <summary>
        /// Marks copied movement as observed intelligence, retaining an existing timestamp unless supplied.
        /// </summary>
        /// <param name="observedAtTick">The tick of the observation, when newly known.</param>
        public void MarkObserved(int? observedAtTick = null)
        {
            _isObservation = true;
            if (observedAtTick.HasValue)
                _observedAtTick = observedAtTick;
        }

        /// <summary>
        /// Creates an independent snapshot of the journey and its planned continuation.
        /// </summary>
        /// <returns>The copied movement state.</returns>
        public MovementState CreateCopy() =>
            new MovementState
            {
                _transitTicks = _transitTicks,
                _ticksElapsed = _ticksElapsed,
                _observedAtTick = _observedAtTick,
                _isObservation = _isObservation,
                _movementGroupID = _movementGroupID,
                _sourceEventInstanceID = _sourceEventInstanceID,
                _resolveEncounterOnArrival = _resolveEncounterOnArrival,
                _previousLocationInstanceID = _previousLocationInstanceID,
                _destinationPlanetInstanceID = _destinationPlanetInstanceID,
                _nextLeg = _nextLeg?.CreateCopy(),
                _originPositionX = _originPositionX,
                _originPositionY = _originPositionY,
                _currentPositionX = _currentPositionX,
                _currentPositionY = _currentPositionY,
            };

        /// <summary>
        /// Gets progress through the running leg.
        /// </summary>
        /// <returns>The elapsed fraction of the current leg.</returns>
        public float Progress() => _transitTicks == 0 ? 1.0f : (float)_ticksElapsed / _transitTicks;

        /// <summary>
        /// Checks whether the final transit leg has finished.
        /// </summary>
        /// <returns>True when no continuation remains and the running leg has ended.</returns>
        public bool IsComplete() => _nextLeg == null && _ticksElapsed >= _transitTicks;

        /// <summary>
        /// Gets the time remaining across the running and planned legs.
        /// </summary>
        /// <returns>The number of ticks until final arrival.</returns>
        public int TicksRemaining() =>
            _transitTicks - _ticksElapsed + (_nextLeg?.TransitTicks ?? 0);

        /// <summary>
        /// Starts a leg's geometry and clock together without replacing shared journey metadata.
        /// </summary>
        /// <param name="leg">The leg to start.</param>
        private void ApplyLeg(MovementLeg leg)
        {
            _transitTicks = leg.TransitTicks;
            _ticksElapsed = 0;
            _destinationPlanetInstanceID = leg.DestinationPlanetInstanceID;
            _originPositionX = leg.OriginPositionX;
            _originPositionY = leg.OriginPositionY;
            SetPosition(leg.OriginPosition);
        }
    }
}
