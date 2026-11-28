using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Results
{
    /// <summary>
    /// Transfers a completed authoritative encounter using the identities captured at battle entry.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatOutcome
    {
        public SpaceCombatResult.BattleOutcome Winner { get; set; }
        public SpaceCombatResult.SideOutcome AttackerOutcome { get; set; }
        public SpaceCombatResult.SideOutcome DefenderOutcome { get; set; }
        public int IterationsCompleted { get; set; }
        public ShipOutcome[] Ships { get; set; }
        public FighterOutcome[] Fighters { get; set; }

        /// <summary>
        /// Carries one ship's final damage and withdrawal state by unit identity.
        /// </summary>
        [PersistableObject(Name = "TacticalCombatShipOutcome")]
        public sealed class ShipOutcome
        {
            public string UnitID { get; set; }
            public int Hull { get; set; }
            public CapitalShipSystemDamage SystemDamage { get; set; }
            public bool Withdrawn { get; set; }
        }

        /// <summary>
        /// Carries one squadron's final strength, recovery host, and withdrawal state.
        /// </summary>
        [PersistableObject(Name = "TacticalCombatFighterOutcome")]
        public sealed class FighterOutcome
        {
            public string UnitID { get; set; }
            public int SquadronSize { get; set; }
            public string CarrierID { get; set; }
            public bool Withdrawn { get; set; }
        }
    }
}
