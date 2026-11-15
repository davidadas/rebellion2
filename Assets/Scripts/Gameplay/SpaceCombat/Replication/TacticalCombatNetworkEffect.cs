using System;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures an ordered presentation edge without asking the receiving peer to simulate it.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkEffect
    {
        public TacticalCombatNetworkEffectKind Kind;
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
