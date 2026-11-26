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
        public int PrimaryHyperdrive { get; set; }
        public int BackupHyperdrive { get; set; }

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
        public int Total =>
            Shields + Weapons + TractorBeam + Engines + PrimaryHyperdrive + BackupHyperdrive;

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
                TacticalShipSystem.Hyperdrive => PrimaryHyperdrive + BackupHyperdrive,
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
                    PrimaryHyperdrive = Math.Min(damage, 1);
                    BackupHyperdrive = damage - PrimaryHyperdrive;
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
                PrimaryHyperdrive = PrimaryHyperdrive,
                BackupHyperdrive = BackupHyperdrive,
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
            if (index < PrimaryHyperdrive)
            {
                PrimaryHyperdrive--;
                return;
            }
            BackupHyperdrive--;
        }
    }
}
