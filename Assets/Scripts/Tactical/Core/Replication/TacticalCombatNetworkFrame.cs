using System;
using Rebellion.Game.Results;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Captures one complete authoritative view update and the presentation edges since its predecessor.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkFrame
    {
        public long Sequence;
        public double TimestampMilliseconds;
        public int SystemTime;
        public bool Paused;
        public bool Complete;
        public CombatSide Winner;
        public SpaceCombatSideOutcome AttackerOutcome;
        public SpaceCombatSideOutcome DefenderOutcome;
        public bool[] TerminalFlags = new bool[7];
        public float StationCharge;
        public bool StationDeparted;
        public int MovieID;
        public int MovieGroupID;
        public CombatSide MovieSide;
        public string MovieUnitID;
        public int MovieCategory;
        public TacticalCombatAttackRunState MovieState;
        public TacticalCombatNetworkUnitState[] Units =
            Array.Empty<TacticalCombatNetworkUnitState>();
        public TacticalCombatNetworkGroupState[] Groups =
            Array.Empty<TacticalCombatNetworkGroupState>();
        public TacticalCombatNetworkSideState[] Sides =
            Array.Empty<TacticalCombatNetworkSideState>();
        public TacticalCombatNetworkSelectionState[] Selections =
            Array.Empty<TacticalCombatNetworkSelectionState>();
        public TacticalCombatNetworkEffect[] Effects = Array.Empty<TacticalCombatNetworkEffect>();
    }
}
