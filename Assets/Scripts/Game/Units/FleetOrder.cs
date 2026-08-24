using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Defines the strategic objective assigned to a fleet.
    /// </summary>
    public enum FleetOrderType
    {
        /// <summary>Captures an enemy planet.</summary>
        Attack,

        /// <summary>Claims an unowned planet.</summary>
        Colonize,

        /// <summary>Protects a friendly planet.</summary>
        Defend,

        /// <summary>Destroys a known hostile fleet without committing to an invasion.</summary>
        Engage,
    }

    /// <summary>
    /// Defines the readiness stage of a fleet order.
    /// </summary>
    public enum FleetOrderStatus
    {
        Building,
        Staging,
        Readying,
        Ready,

        /// <summary>The fleet is returning to friendly territory.</summary>
        Returning,
    }

    /// <summary>
    /// Stores the durable order assigned to a fleet, including its objective, readiness state,
    /// and target planet.
    /// </summary>
    [PersistableObject]
    public sealed class FleetOrder
    {
        public FleetOrderType OrderType { get; set; }

        public FleetOrderStatus Status { get; set; }

        public string TargetPlanetId { get; set; } = string.Empty;

        /// <summary>Gets or sets the friendly planet from which a temporary engagement departed.</summary>
        public string OriginPlanetId { get; set; } = string.Empty;

        /// <summary>
        /// Creates an independent copy of this fleet order.
        /// </summary>
        /// <returns>The copied fleet order.</returns>
        public FleetOrder CreateCopy() =>
            new FleetOrder
            {
                OrderType = OrderType,
                Status = Status,
                TargetPlanetId = TargetPlanetId,
                OriginPlanetId = OriginPlanetId,
            };
    }
}
