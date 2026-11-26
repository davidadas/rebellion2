using System;
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
        private int LegacyRepairDueTick
        {
            get => 0;
            set { }
        }

        [PersistableMember(Name = "RepairInterval")]
        [PersistableIgnore]
        private int LegacyRepairInterval
        {
            get => 0;
            set { }
        }

        [PersistableIgnore]
        public int Total =>
            Shields + Weapons + TractorBeam + Engines + PrimaryHyperdrive + BackupHyperdrive;

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

        internal void AddCombatDamage(TacticalShipSystem system, int capacity)
        {
            int damage = Math.Min(GetCombatDamage(system) + 1, capacity);
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
