using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>
    /// Identifies the gameplay function of a hardpoint weapon.
    /// </summary>
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
    /// Identifies how a hardpoint weapon reaches its target.
    /// </summary>
    public enum WeaponDeliveryMode
    {
        Projectile,
        Hitscan,
    }

    /// <summary>
    /// Defines the authored combat characteristics of a weapon.
    /// </summary>
    [PersistableObject]
    public sealed class WeaponData
    {
        public HardpointWeaponType WeaponType { get; set; }
        public WeaponDeliveryMode DeliveryMode { get; set; }
        public int Damage { get; set; }
        public float Range { get; set; }
        public float FireIntervalSeconds { get; set; }
        public float ProjectileSpeed { get; set; }
        public float EffectDurationSeconds { get; set; }

        /// <summary>
        /// Creates an independent copy of the authored weapon data.
        /// </summary>
        /// <returns>The copied weapon data.</returns>
        public WeaponData CreateCopy()
        {
            return new WeaponData
            {
                WeaponType = WeaponType,
                DeliveryMode = DeliveryMode,
                Damage = Damage,
                Range = Range,
                FireIntervalSeconds = FireIntervalSeconds,
                ProjectileSpeed = ProjectileSpeed,
                EffectDurationSeconds = EffectDurationSeconds,
            };
        }
    }
}
