using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    public enum HardpointWeaponType
    {
        None,
        LaserCannon,
        Turbolaser,
        IonCannon,
        MissileLauncher,
        TorpedoLauncher,
        TractorBeam,
    }

    /// <summary>
    /// Represents a weapon mount installed on a ship.
    /// </summary>
    [PersistableObject]
    public sealed class Hardpoint : ShipComponent
    {
        public HardpointWeaponType WeaponType { get; set; }

        /// <summary>
        /// Creates an independent copy of the hardpoint.
        /// </summary>
        /// <returns>The copied hardpoint.</returns>
        public override ShipComponent CreateCopy()
        {
            Hardpoint copy = new Hardpoint { Health = Health, WeaponType = WeaponType };
            CopyEntityStateTo(copy);
            return copy;
        }
    }
}
