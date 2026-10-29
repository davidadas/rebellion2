using Rebellion.Game.Combat;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>
    /// Represents a weapon mount installed on a ship.
    /// </summary>
    [PersistableObject]
    public sealed class Hardpoint : ShipComponent
    {
        public float FiringArcDegrees { get; set; }
        public WeaponData Weapon { get; set; }
        public BattleVector3 Direction { get; set; } = new BattleVector3 { Z = 1f };
        public BattleVector3 Up { get; set; } = new BattleVector3 { Y = 1f };
        public bool UsesHullBounds { get; set; }
        public bool Articulated { get; set; }
        public float MinimumYawDegrees { get; set; }
        public float MaximumYawDegrees { get; set; }
        public float MinimumPitchDegrees { get; set; }
        public float MaximumPitchDegrees { get; set; }
        public float TrackingDegreesPerSecond { get; set; }
        public float TrackingResponse { get; set; }
        public float AimToleranceDegrees { get; set; }

        /// <summary>
        /// Creates an independent copy of the hardpoint.
        /// </summary>
        /// <returns>The copied hardpoint.</returns>
        public override ShipComponent CreateCopy()
        {
            Hardpoint copy = new Hardpoint
            {
                FiringArcDegrees = FiringArcDegrees,
                Weapon = Weapon?.CreateCopy(),
                Direction = new BattleVector3
                {
                    X = Direction.X,
                    Y = Direction.Y,
                    Z = Direction.Z,
                },
                Up = new BattleVector3
                {
                    X = Up.X,
                    Y = Up.Y,
                    Z = Up.Z,
                },
                UsesHullBounds = UsesHullBounds,
                Articulated = Articulated,
                MinimumYawDegrees = MinimumYawDegrees,
                MaximumYawDegrees = MaximumYawDegrees,
                MinimumPitchDegrees = MinimumPitchDegrees,
                MaximumPitchDegrees = MaximumPitchDegrees,
                TrackingDegreesPerSecond = TrackingDegreesPerSecond,
                TrackingResponse = TrackingResponse,
                AimToleranceDegrees = AimToleranceDegrees,
            };
            CopyComponentStateTo(copy);
            return copy;
        }
    }
}
