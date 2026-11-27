using System;
using Rebellion.Game.Results;
using Rebellion.Game.ShipComponents;

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
        public HardpointWeaponType Weapon;
        public TacticalCombatImpactKind Impact;
        public float Strength;
        public bool Enabled;
        public int MessageID;
        public int MovieID;
        public bool HasRecipient;
        public CombatSide Recipient;
        public string SoundName;
    }

    /// <summary>
    /// Identifies a one-shot presentation edge emitted by the authoritative simulation.
    /// </summary>
    public enum TacticalCombatEventKind
    {
        Shot,
        Impact,
        Destruction,
        Destroyed,
        Withdrawn,
        Recovered,
        Superlaser,
        TractorSources,
        GravityWell,
        Movie,
        Notification,
        FiringSound,
    }
}
