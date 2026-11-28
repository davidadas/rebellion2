using System;
using System.Diagnostics.CodeAnalysis;
using Rebellion.Game.Combat;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Stores damaged capital-ship components that persist between battles.
    /// </summary>
    [PersistableObject]
    public sealed class CapitalShipSystemDamage
    {
        public int Shields { get; set; }
        public int Weapons { get; set; }
        public int TractorBeam { get; set; }
        public int Engines { get; set; }
        private int[] _hyperdriveDamage = Array.Empty<int>();
        private bool _hasDriveDamageArray;

        public int[] HyperdriveDamage
        {
            get => _hyperdriveDamage;
            set
            {
                _hyperdriveDamage = value ?? Array.Empty<int>();
                _hasDriveDamageArray = true;
            }
        }

        [PersistableMember(Name = "PrimaryHyperdrive")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Reads older ship damage.")]
        private int LegacyFirstDriveDamage
        {
            get => 0;
            set => ReadLegacyDriveDamage(0, value);
        }

        [PersistableMember(Name = "BackupHyperdrive")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Reads older ship damage.")]
        private int LegacySecondDriveDamage
        {
            get => 0;
            set => ReadLegacyDriveDamage(1, value);
        }

        // Older saves and battle outcomes carried a separate component timer.
        [PersistableMember(Name = "RepairDueTick")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private int LegacyRepairDueTick
        {
            get => 0;
            set { }
        }

        [PersistableMember(Name = "RepairInterval")]
        [PersistableIgnore]
        [SuppressMessage("Roslynator", "RCS1213", Justification = "Used by the game serializer.")]
        private int LegacyRepairInterval
        {
            get => 0;
            set { }
        }

        [PersistableIgnore]
        public int Total => Shields + Weapons + TractorBeam + Engines + TotalHyperdriveDamage;

        private int TotalHyperdriveDamage
        {
            get
            {
                int total = 0;
                foreach (int damage in _hyperdriveDamage)
                    total += damage;
                return total;
            }
        }

        /// <summary>
        /// Reads the recorded damage to one installed drive.
        /// </summary>
        /// <param name="index">The drive index.</param>
        /// <returns>The recorded damage, or zero if the drive is undamaged.</returns>
        internal int GetHyperdriveDamage(int index) =>
            index < _hyperdriveDamage.Length ? _hyperdriveDamage[index] : 0;

        /// <summary>
        /// Updates one drive and preserves damage to the other drives.
        /// </summary>
        /// <param name="index">The drive index.</param>
        /// <param name="damage">The new damage level.</param>
        internal void SetHyperdriveDamage(int index, int damage)
        {
            if (index >= _hyperdriveDamage.Length)
            {
                if (damage == 0)
                    return;
                Array.Resize(ref _hyperdriveDamage, index + 1);
            }
            _hyperdriveDamage[index] = damage;
        }

        /// <summary>
        /// Imports damage from an older save with two named drive elements.
        /// </summary>
        /// <param name="index">The legacy drive index.</param>
        /// <param name="damage">The serialized damage level.</param>
        private void ReadLegacyDriveDamage(int index, int damage)
        {
            if (!_hasDriveDamageArray)
                SetHyperdriveDamage(index, damage);
        }

        /// <summary>
        /// Reads damage using the system categories shared by both battle modes.
        /// </summary>
        /// <param name="system">The affected ship system.</param>
        /// <returns>The damage level for the selected system.</returns>
        internal int GetCombatDamage(TacticalShipSystem system) =>
            system switch
            {
                TacticalShipSystem.Shields => Shields,
                TacticalShipSystem.Weapons => Weapons,
                TacticalShipSystem.TractorBeam => TractorBeam,
                TacticalShipSystem.Engines => Engines,
                TacticalShipSystem.Hyperdrive => TotalHyperdriveDamage,
                _ => throw new ArgumentOutOfRangeException(nameof(system)),
            };

        /// <summary>
        /// Increments a system's damage up to its installed capacity.
        /// </summary>
        /// <param name="system">The affected ship system.</param>
        /// <param name="capacity">The maximum damage allowed for the installed system.</param>
        internal void AddCombatDamage(TacticalShipSystem system, int capacity) =>
            SetCombatDamage(system, Math.Min(GetCombatDamage(system) + 1, capacity));

        /// <summary>
        /// Stores the new damage level for a combat system.
        /// </summary>
        /// <param name="system">The affected ship system.</param>
        /// <param name="damage">The new damage level.</param>
        internal void SetCombatDamage(TacticalShipSystem system, int damage)
        {
            switch (system)
            {
                case TacticalShipSystem.Shields:
                    Shields = damage;
                    break;
                case TacticalShipSystem.Weapons:
                    Weapons = damage;
                    break;
                case TacticalShipSystem.TractorBeam:
                    TractorBeam = damage;
                    break;
                case TacticalShipSystem.Engines:
                    Engines = damage;
                    break;
                case TacticalShipSystem.Hyperdrive:
                    // The automatic resolver uses the total without mapping it to installed equipment.
                    HyperdriveDamage = damage == 0 ? Array.Empty<int>() : new[] { damage };
                    break;
            }
        }

        /// <summary>
        /// Copies the component levels without sharing mutable campaign state.
        /// </summary>
        /// <returns>An independent component-damage record.</returns>
        public CapitalShipSystemDamage CreateCopy()
        {
            return new CapitalShipSystemDamage
            {
                Shields = Shields,
                Weapons = Weapons,
                TractorBeam = TractorBeam,
                Engines = Engines,
                HyperdriveDamage = (int[])_hyperdriveDamage.Clone(),
            };
        }

        /// <summary>
        /// Repairs one component selected in proportion to its remaining damage levels.
        /// </summary>
        /// <param name="index">A zero-based selection below the total damage level.</param>
        internal void RepairComponent(int index)
        {
            if (index < 0 || index >= Total)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (index < Shields)
            {
                Shields--;
                return;
            }
            index -= Shields;
            if (index < Weapons)
            {
                Weapons--;
                return;
            }
            index -= Weapons;
            if (index < TractorBeam)
            {
                TractorBeam--;
                return;
            }
            index -= TractorBeam;
            if (index < Engines)
            {
                Engines--;
                return;
            }
            index -= Engines;
            for (int drive = 0; drive < _hyperdriveDamage.Length; drive++)
            {
                if (index < _hyperdriveDamage[drive])
                {
                    _hyperdriveDamage[drive]--;
                    return;
                }
                index -= _hyperdriveDamage[drive];
            }
        }
    }
}
