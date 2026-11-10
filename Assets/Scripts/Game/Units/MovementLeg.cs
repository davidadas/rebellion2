using System.Drawing;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Describes a transit leg before it starts, without a running clock or journey metadata.
    /// </summary>
    [PersistableObject]
    public sealed class MovementLeg
    {
        [PersistableMember(Name = nameof(TransitTicks))]
        private int _transitTicks;

        [PersistableMember(Name = nameof(OriginPositionX))]
        private int _originPositionX;

        [PersistableMember(Name = nameof(OriginPositionY))]
        private int _originPositionY;

        [PersistableMember(Name = nameof(DestinationPlanetInstanceID))]
        private string _destinationPlanetInstanceID;

        public int TransitTicks => _transitTicks;
        public int OriginPositionX => _originPositionX;
        public int OriginPositionY => _originPositionY;
        public Point OriginPosition => new Point(_originPositionX, _originPositionY);
        public string DestinationPlanetInstanceID => _destinationPlanetInstanceID;

        /// <summary>
        /// Creates an empty leg for deserialization.
        /// </summary>
        public MovementLeg() { }

        /// <summary>
        /// Describes the duration and endpoints of a transit leg.
        /// </summary>
        /// <param name="transitTicks">The full duration of the leg.</param>
        /// <param name="originPosition">The departure position.</param>
        /// <param name="destinationPlanetInstanceID">The planet at the end of the leg.</param>
        public MovementLeg(
            int transitTicks,
            Point originPosition,
            string destinationPlanetInstanceID
        )
        {
            _transitTicks = transitTicks;
            _originPositionX = originPosition.X;
            _originPositionY = originPosition.Y;
            _destinationPlanetInstanceID = destinationPlanetInstanceID;
        }

        /// <summary>
        /// Creates an independent copy of the planned leg.
        /// </summary>
        /// <returns>The copied leg.</returns>
        public MovementLeg CreateCopy() =>
            new MovementLeg(TransitTicks, OriginPosition, DestinationPlanetInstanceID);
    }
}
