using System.Drawing;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Movement
{
    /// <summary>
    /// Encapsulates active movement state for IMovable units.
    /// Existence of this object means unit is in transit.
    /// Null = unit is not moving (replaces Idle status).
    /// Only created and destroyed by MovementSystem.
    /// </summary>
    [PersistableObject]
    public class MovementState
    {
        /// <summary>
        /// Total ticks required to complete transit from origin to destination.
        /// Calculated based on distance and hyperdrive rating.
        /// </summary>
        public int TransitTicks { get; set; }

        /// <summary>
        /// Number of ticks elapsed since departure.
        /// When TicksElapsed >= TransitTicks, unit has arrived.
        /// </summary>
        public int TicksElapsed { get; set; }

        /// <summary>
        /// Position at the start of transit (origin planet coordinates).
        /// Used for interpolating position during transit.
        /// </summary>
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

        /// <summary>
        /// Current in-transit position (interpolated between origin and destination).
        /// </summary>
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
        /// Progress fraction in [0.0, 1.0] - 0.0 = just departed, 1.0 = arrived.
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
        /// True if the unit has completed transit.
        /// </summary>
        /// <returns>True if transit is complete.</returns>
        public bool IsComplete()
        {
            return TicksElapsed >= TransitTicks;
        }

        /// <summary>
        /// Remaining ticks until arrival.
        /// </summary>
        /// <returns>Number of ticks remaining.</returns>
        public int TicksRemaining()
        {
            return TransitTicks - TicksElapsed;
        }
    }
}
