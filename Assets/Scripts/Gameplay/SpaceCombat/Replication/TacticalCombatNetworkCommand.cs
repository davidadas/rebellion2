using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Carries semantic input without granting the sender ownership of its authenticated side.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkCommand
    {
        public TacticalCombatNetworkCommandKind Kind;
        public string UnitID;
        public TacticalCombatNetworkVector Waypoint;
        public bool Extend;
        public bool Next;
        public int Category;
        public TacticalCombatOrder Order;
        public bool CloseAttack;
        public TacticalCombatFormationKind Formation;
        public bool Paused;
    }
}
