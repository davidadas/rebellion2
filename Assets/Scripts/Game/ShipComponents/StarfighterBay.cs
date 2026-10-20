using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>
    /// Houses starfighters aboard a ship.
    /// </summary>
    [PersistableObject]
    public sealed class StarfighterBay : ShipComponent
    {
        public int Capacity { get; set; }

        /// <summary>
        /// Gets or sets the ordered bay-to-clear-space route in the referenced model node's coordinates.
        /// </summary>
        public List<StarfighterBayWaypoint> FlightPath { get; set; } =
            new List<StarfighterBayWaypoint>();

        /// <summary>
        /// Creates an independent copy of this starfighter bay.
        /// </summary>
        /// <returns>The copied starfighter bay.</returns>
        public override ShipComponent CreateCopy()
        {
            StarfighterBay copy = new StarfighterBay
            {
                Capacity = Capacity,
                FlightPath = FlightPath.ConvertAll(point => new StarfighterBayWaypoint
                {
                    X = point.X,
                    Y = point.Y,
                    Z = point.Z,
                }),
            };
            CopyComponentStateTo(copy);
            return copy;
        }
    }

    /// <summary>
    /// Stores one authored model-local point on a starfighter bay's departure and recovery route.
    /// </summary>
    [PersistableObject]
    public sealed class StarfighterBayWaypoint
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
    }
}
