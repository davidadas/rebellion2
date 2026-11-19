using System;
using Rebellion.Game.Results;

namespace Rebellion.Tactical.Synchronization
{
    /// <summary>
    /// Captures one complete authoritative view update and the presentation edges since its predecessor.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatSnapshot
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
        public TacticalCombatUnitSnapshot[] Units = Array.Empty<TacticalCombatUnitSnapshot>();
        public TacticalCombatGroupSnapshot[] Groups = Array.Empty<TacticalCombatGroupSnapshot>();
        public TacticalCombatSideSnapshot[] Sides = Array.Empty<TacticalCombatSideSnapshot>();
        public TacticalCombatSelectionSnapshot[] Selections =
            Array.Empty<TacticalCombatSelectionSnapshot>();
        public TacticalCombatEvent[] Effects = Array.Empty<TacticalCombatEvent>();
    }
}
