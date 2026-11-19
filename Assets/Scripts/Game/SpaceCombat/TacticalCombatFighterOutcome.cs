using Rebellion.Util.Serialization;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Carries one captured squadron's final strength, recovery host, and departure state.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatFighterOutcome
    {
        public string UnitID { get; set; }
        public int SquadronSize { get; set; }
        public string CarrierID { get; set; }
        public bool Withdrawn { get; set; }
    }
}
