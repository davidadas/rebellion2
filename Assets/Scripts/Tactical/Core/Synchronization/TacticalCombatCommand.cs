using System;
using Rebellion.Game.Combat;

namespace Rebellion.Tactical.Synchronization
{
    /// <summary>
    /// Carries semantic input without granting the sender ownership of its authenticated side.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatCommand
    {
        public TacticalCombatCommandKind Kind;
        public string UnitID;
        public TacticalCombatSnapshotVector Waypoint;
        public bool Extend;
        public bool Next;
        public int Category;
        public TacticalCombatOrder Order;
        public bool CloseAttack;
        public TacticalCombatFormationKind Formation;
        public bool Paused;
    }

    /// <summary>
    /// Identifies a faction-owned semantic input at the shared combat boundary.
    /// </summary>
    public enum TacticalCombatCommandKind
    {
        Object,
        Waypoint,
        Clear,
        DisplayMode,
        ObjectCategory,
        FighterCategory,
        NearestTarget,
        Order,
        AttackMode,
        Formation,
        ToggleAutomation,
        Withdrawal,
        StationTarget,
        Pause,
        ImmediateResult,
    }
}
