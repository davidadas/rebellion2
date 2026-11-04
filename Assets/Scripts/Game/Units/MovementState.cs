using System.Drawing;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Encapsulates active movement state for IMovable units.
    /// Existence of this object means unit is in transit.
    /// Null = unit is not moving (replaces Idle status).
    /// Only created and destroyed by MovementCommands.
    /// </summary>
    [PersistableObject]
    public class MovementState
    {
        public int TransitTicks { get; set; }

        public int TicksElapsed { get; set; }

        /// <summary>
        /// Gets or sets the tick when this movement state was observed, when that time is known.
        /// </summary>
        public int? ObservedAtTick { get; set; }

        /// <summary>
        /// Gets or sets whether this movement state represents frozen intelligence rather than live movement.
        /// </summary>
        public bool IsObservation { get; set; }

        public string MovementGroupID { get; set; }

        public string SourceEventInstanceID { get; set; }

        public bool ResolveEncounterOnArrival { get; set; }

        /// <summary>
        /// Gets or sets the last visited container before this journey. Null denotes older
        /// movement without this information; an empty ID denotes a cleared previous location.
        /// </summary>
        [PersistableAttribute]
        public string PreviousLocationInstanceID { get; set; }

        /// <summary>
        /// Gets or sets the planet at the end of the current leg, which can differ from the
        /// unit's final destination after redirection.
        /// </summary>
        public string DestinationPlanetInstanceID { get; set; }

        /// <summary>
        /// Gets or sets the onward leg that starts after the current leg finishes.
        /// Its origin also identifies the current leg's endpoint while redirecting.
        /// </summary>
        public MovementState NextLeg { get; set; }

        [PersistableIgnore]
        public Point OriginPosition
        {
            get => new Point(OriginPositionX, OriginPositionY);
            set
            {
                OriginPositionX = value.X;
                OriginPositionY = value.Y;
            }
        }

        public int OriginPositionX { get; set; }
        public int OriginPositionY { get; set; }

        [PersistableIgnore]
        public Point CurrentPosition
        {
            get => new Point(CurrentPositionX, CurrentPositionY);
            set
            {
                CurrentPositionX = value.X;
                CurrentPositionY = value.Y;
            }
        }

        public int CurrentPositionX { get; set; }
        public int CurrentPositionY { get; set; }

        /// <summary>
        /// Default constructor for serialization.
        /// </summary>
        public MovementState() { }

        /// <summary>
        /// Creates an independent copy of this movement state.
        /// </summary>
        /// <returns>The copied movement state.</returns>
        public MovementState CreateCopy() =>
            new MovementState
            {
                TransitTicks = TransitTicks,
                TicksElapsed = TicksElapsed,
                ObservedAtTick = ObservedAtTick,
                IsObservation = IsObservation,
                MovementGroupID = MovementGroupID,
                SourceEventInstanceID = SourceEventInstanceID,
                ResolveEncounterOnArrival = ResolveEncounterOnArrival,
                PreviousLocationInstanceID = PreviousLocationInstanceID,
                DestinationPlanetInstanceID = DestinationPlanetInstanceID,
                NextLeg = NextLeg?.CreateCopy(),
                OriginPositionX = OriginPositionX,
                OriginPositionY = OriginPositionY,
                CurrentPositionX = CurrentPositionX,
                CurrentPositionY = CurrentPositionY,
            };

        /// <summary>
        /// Progress through the current leg: zero at departure and one at its endpoint.
        /// </summary>
        /// <returns>Progress as a float in [0.0, 1.0].</returns>
        public float Progress()
        {
            if (TransitTicks == 0)
            {
                return 1.0f;
            }
            return (float)TicksElapsed / TransitTicks;
        }

        /// <summary>
        /// True if the unit has completed its final transit leg.
        /// </summary>
        /// <returns>True if transit is complete.</returns>
        public bool IsComplete()
        {
            return NextLeg == null && TicksElapsed >= TransitTicks;
        }

        /// <summary>
        /// Remaining ticks until arrival.
        /// </summary>
        /// <returns>Number of ticks remaining.</returns>
        public int TicksRemaining()
        {
            return TransitTicks - TicksElapsed + (NextLeg?.TicksRemaining() ?? 0);
        }
    }
}
