using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.ShipComponents
{
    /// <summary>
    /// Groups weapon mounts that operate together.
    /// </summary>
    [PersistableObject]
    public sealed class HardpointGroup
    {
        [PersistableMember(Name = "Hardpoints")]
        private List<Hardpoint> _hardpoints = new List<Hardpoint>();

        public TacticalWeaponArc Arc { get; set; }

        /// <summary>
        /// Returns the weapon mounts in this group.
        /// </summary>
        /// <returns>The group's hardpoints.</returns>
        public List<Hardpoint> GetHardpoints()
        {
            return _hardpoints;
        }

        /// <summary>
        /// Totals the damage ratings of mounts in this battery that use one weapon type.
        /// </summary>
        /// <param name="weaponType">The authored weapon type that identifies the matching mounts.</param>
        /// <returns>The combined authored damage rating.</returns>
        internal int GetStrength(HardpointWeaponType weaponType)
        {
            int strength = 0;
            foreach (Hardpoint hardpoint in _hardpoints)
                if (hardpoint.Weapon is { } weapon && weapon.WeaponType == weaponType)
                    strength += weapon.Damage;
            return strength;
        }

        /// <summary>
        /// Finds the maximum range among mounts with the given weapon type.
        /// </summary>
        /// <param name="weaponType">The authored weapon type that identifies the matching mounts.</param>
        /// <returns>The maximum range, or zero when no mount matches.</returns>
        internal float GetRange(HardpointWeaponType weaponType)
        {
            float? range = null;
            foreach (Hardpoint hardpoint in _hardpoints)
                if (hardpoint.Weapon is { } weapon && weapon.WeaponType == weaponType)
                    range = range.HasValue
                        ? System.Math.Max(range.Value, weapon.Range)
                        : weapon.Range;
            return range.GetValueOrDefault();
        }

        /// <summary>
        /// Creates an independent copy of the hardpoint group and its mounts.
        /// </summary>
        /// <returns>The copied hardpoint group.</returns>
        public HardpointGroup CreateCopy()
        {
            return new HardpointGroup
            {
                Arc = Arc,
                _hardpoints = _hardpoints?.ConvertAll(hardpoint =>
                    (Hardpoint)hardpoint?.CreateCopy()
                ),
            };
        }
    }

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
                FiringArcDegrees = FiringArcDegrees,
                Weapon = Weapon?.CreateCopy(),
            };
            CopyComponentStateTo(copy);
            return copy;
        }
    }

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

    /// <summary>
    /// Identifies a ship's directional weapon battery in clockwise order.
    /// </summary>
    public enum TacticalWeaponArc
    {
        Forward,
        Starboard,
        Aft,
        Port,
    }
}
