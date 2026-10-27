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
        public WeaponDeliveryMode DeliveryMode { get; set; }
        public float Damage { get; set; }
        public float Range { get; set; }
        public float FireIntervalSeconds { get; set; }
        public float ProjectileSpeed { get; set; }
        public float EffectDurationSeconds { get; set; }
        public int ShotsPerVolley { get; set; } = 1;
        public float MinimumRechargeSeconds { get; set; }
        public float MaximumRechargeSeconds { get; set; }
        public float ProjectileLength { get; set; }
        public float ProjectileWidth { get; set; }
        public float Red { get; set; }
        public float Green { get; set; }
        public float Blue { get; set; }
        public List<string> FireSoundPaths { get; set; } = new List<string>();
        public List<string> ImpactSoundPaths { get; set; } = new List<string>();

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
                ShotsPerVolley = ShotsPerVolley,
                MinimumRechargeSeconds = MinimumRechargeSeconds,
                MaximumRechargeSeconds = MaximumRechargeSeconds,
                ProjectileLength = ProjectileLength,
                ProjectileWidth = ProjectileWidth,
                Red = Red,
                Green = Green,
                Blue = Blue,
                FireSoundPaths = new List<string>(FireSoundPaths),
                ImpactSoundPaths = new List<string>(ImpactSoundPaths),
            };
        }
    }
}
