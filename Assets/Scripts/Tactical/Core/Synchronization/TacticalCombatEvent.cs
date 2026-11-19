using System;
using Rebellion.Game.Results;

namespace Rebellion.Tactical.Synchronization
{
    /// <summary>
    /// Captures an ordered presentation edge without asking a passive view to simulate it.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatEvent
    {
        public TacticalCombatEventKind Kind;
        public string SourceID;
        public string TargetID;
        public TacticalWeaponType Weapon;
        public TacticalCombatImpactKind Impact;
        public float Strength;
        public bool Enabled;
        public int MessageID;
        public int MovieID;
        public bool HasRecipient;
        public CombatSide Recipient;
    }
}
