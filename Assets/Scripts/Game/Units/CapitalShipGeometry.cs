using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Ship dimensions and maneuver limits used by combat.
    /// </summary>
    [PersistableObject]
    public sealed class CapitalShipGeometry
    {
        internal const float DefaultCollisionRadius = 3.6f;
        internal const float DefaultHalfHeight = 3f;

        public float CollisionRadius { get; set; } = DefaultCollisionRadius;
        public float HalfHeight { get; set; } = DefaultHalfHeight;
        public float HullWidth { get; set; }
        public float HullHeight { get; set; }
        public float HullDepth { get; set; }
        public float MaximumTurnRate { get; set; }

        internal CapitalShipGeometry CreateCopy() => (CapitalShipGeometry)MemberwiseClone();
    }
}
