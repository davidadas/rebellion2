using System.Collections.Generic;
using Rebellion.Game.Combat;
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

        internal int GetStrength(HardpointWeaponType weaponType)
        {
            int strength = 0;
            foreach (Hardpoint hardpoint in _hardpoints)
                if (hardpoint.Weapon is { } weapon && weapon.WeaponType == weaponType)
                    strength += weapon.Damage;
            return strength;
        }

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
}
