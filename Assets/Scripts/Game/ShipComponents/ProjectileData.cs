using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>
    /// Defines a shot's travel, damage, appearance, and impact audio independently of its weapon.
    /// </summary>
    [PersistableObject]
    public sealed class ProjectileData
    {
        public WeaponDeliveryMode DeliveryMode { get; set; }
        public float Damage { get; set; }
        public float Speed { get; set; }
        public float Length { get; set; }
        public float Width { get; set; }
        public float EffectDurationSeconds { get; set; }
        public string ColorHex { get; set; } = "#000000";
        public List<string> ImpactSoundPaths { get; set; } = new List<string>();

        /// <summary>
        /// Captures shot properties independently of later changes to the firing weapon.
        /// </summary>
        /// <returns>An independent copy of the projectile data.</returns>
        public ProjectileData CreateCopy()
        {
            return new ProjectileData
            {
                DeliveryMode = DeliveryMode,
                Damage = Damage,
                Speed = Speed,
                Length = Length,
                Width = Width,
                EffectDurationSeconds = EffectDurationSeconds,
                ColorHex = ColorHex,
                ImpactSoundPaths = new List<string>(ImpactSoundPaths),
            };
        }
    }
}
