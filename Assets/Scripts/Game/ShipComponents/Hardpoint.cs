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

        /// <summary>
        /// Creates an independent copy of the hardpoint.
        /// </summary>
        /// <returns>The copied hardpoint.</returns>
        public override ShipComponent CreateCopy()
        {
            Hardpoint copy = new Hardpoint
            {
                Health = Health,
                Targetable = Targetable,
                FiringArcDegrees = FiringArcDegrees,
                Weapon = Weapon?.CreateCopy(),
            };
            CopyEntityStateTo(copy);
            return copy;
        }
    }
}
