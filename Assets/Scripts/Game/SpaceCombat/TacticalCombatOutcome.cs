using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Transfers a completed authoritative encounter using the identities captured at battle entry.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatOutcome
    {
        public const int TerminalFlagCount = 7;

        public CombatSide Winner { get; set; }
        public SpaceCombatSideOutcome AttackerOutcome { get; set; }
        public SpaceCombatSideOutcome DefenderOutcome { get; set; }
        public int IterationsCompleted { get; set; }
        public bool[] TerminalFlags { get; set; }
        public TacticalCombatShipOutcome[] Ships { get; set; }
        public TacticalCombatFighterOutcome[] Fighters { get; set; }
    }
}
