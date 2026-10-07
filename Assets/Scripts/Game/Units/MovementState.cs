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

        public string MovementGroupID { get; set; }

        public string SourceEventInstanceID { get; set; }

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
                MovementGroupID = MovementGroupID,
                SourceEventInstanceID = SourceEventInstanceID,
                OriginPositionX = OriginPositionX,
                OriginPositionY = OriginPositionY,
                CurrentPositionX = CurrentPositionX,
                CurrentPositionY = CurrentPositionY,
            };

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
