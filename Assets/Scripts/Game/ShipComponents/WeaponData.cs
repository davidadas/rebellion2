using System.Collections.Generic;
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
        public float Range { get; set; }
        public float FireIntervalSeconds { get; set; }
        public int ShotsPerVolley { get; set; } = 1;
        public float MinimumRechargeSeconds { get; set; }
        public float MaximumRechargeSeconds { get; set; }
        public List<string> FireSoundPaths { get; set; } = new List<string>();
        public ProjectileData Projectile { get; set; } = new ProjectileData();

        /// <summary>
        /// Creates an independent copy of the authored weapon data.
        /// </summary>
        /// <returns>The copied weapon data.</returns>
        public WeaponData CreateCopy()
        {
            return new WeaponData
            {
                WeaponType = WeaponType,
                Range = Range,
                FireIntervalSeconds = FireIntervalSeconds,
                ShotsPerVolley = ShotsPerVolley,
                MinimumRechargeSeconds = MinimumRechargeSeconds,
                MaximumRechargeSeconds = MaximumRechargeSeconds,
                FireSoundPaths = new List<string>(FireSoundPaths),
                Projectile = Projectile?.CreateCopy(),
            };
        }
    }
}
