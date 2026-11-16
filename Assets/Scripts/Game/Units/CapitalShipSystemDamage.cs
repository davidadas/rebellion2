using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    /// <summary>
    /// Stores the damaged components that remain on a capital ship between battles.
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
        public int RepairDueTick { get; set; }
        public int RepairInterval { get; set; }

        [PersistableIgnore]
        public int Total =>
            Shields + Weapons + TractorBeam + Engines + PrimaryHyperdrive + BackupHyperdrive;

        /// <summary>
        /// Creates an undamaged component state for a newly manufactured or loaded ship.
        /// </summary>
        public CapitalShipSystemDamage() { }

        /// <summary>
        /// Copies the component levels without sharing mutable campaign state.
        /// </summary>
        /// <returns>An independent component damage record.</returns>
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
                RepairDueTick = RepairDueTick,
                RepairInterval = RepairInterval,
            };
        }

        /// <summary>
        /// Repairs one component selected in proportion to its remaining damage levels.
        /// </summary>
        /// <param name="index">A zero-based selection below the total number of damage levels.</param>
        internal void RepairComponent(int index)
        {
            if (index < 0 || index >= Total)
                throw new System.ArgumentOutOfRangeException(nameof(index));
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
