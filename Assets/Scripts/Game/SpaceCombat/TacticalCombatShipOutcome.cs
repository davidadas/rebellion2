using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Carries one captured ship's final tactical pools without serializing campaign object references.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatShipOutcome
    {
        public string UnitID { get; set; }
        public int Hull { get; set; }
        public CapitalShipSystemDamage SystemDamage { get; set; }
        public bool Withdrawn { get; set; }
    }
}
