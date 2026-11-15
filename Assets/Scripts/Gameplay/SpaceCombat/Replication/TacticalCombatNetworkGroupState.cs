using System;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures requested and applied group state used by local command and inspection views.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkGroupState
    {
        public int GroupID;
        public CombatSide Side;
        public TacticalCombatGroupKind Kind;
        public bool Applied;
        public string[] Members = Array.Empty<string>();
        public string[] RequestedMembers = Array.Empty<string>();
        public string PrimaryMemberID;
        public string RequestedLeaderID;
        public string[] Targets = Array.Empty<string>();
        public string[] RequestedTargets = Array.Empty<string>();
        public string[] ComputedTargets = Array.Empty<string>();
        public TacticalCombatNetworkVector[] Waypoints = Array.Empty<TacticalCombatNetworkVector>();
        public TacticalCombatNetworkVector[] RequestedWaypoints =
            Array.Empty<TacticalCombatNetworkVector>();
        public TacticalCombatOrder Order;
        public TacticalCombatOrder ActiveOrder;
        public bool CloseAttack;
        public bool ActiveCloseAttack;
        public TacticalCombatFormationKind Formation;
        public TacticalCombatFormationKind RequestedFormation;
        public TacticalCombatNetworkVector Position;
        public TacticalCombatNetworkVector Heading;
        public TacticalCombatNetworkVector Velocity;
        public float MovementSpeed;
        public float TurnRate;
        public int StableUpdates;
        public int FighterCategory;
        public int FighterSlot = -1;
        public int ObjectCategory = -1;
        public bool HasAttackRun;
        public TacticalCombatAttackRunState AttackRunState;
        public float AttackRunMilliseconds;
    }
}
