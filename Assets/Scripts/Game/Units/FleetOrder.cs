using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Defines the strategic objective assigned to a fleet.
    /// </summary>
    public enum FleetOrderType
    {
        Attack,
        Defend,
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
            };
    }
}
