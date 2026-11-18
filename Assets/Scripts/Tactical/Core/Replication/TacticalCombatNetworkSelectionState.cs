using System;
using Rebellion.Game.Results;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Captures one faction's selection without coupling it to the other faction's camera.
    /// </summary>
    [Serializable]
    public sealed class TacticalCombatNetworkSelectionState
    {
        public CombatSide Side;
        public TacticalCombatSelectionKind Kind;
        public bool HasGroup;
        public int GroupID;
        public int Category;
        public bool HasWaypoint;
        public TacticalCombatNetworkVector PrimaryWaypoint;
        public string PrimaryID;
        public string InspectedID;
        public string[] Members = Array.Empty<string>();
        public string[] Highlights = Array.Empty<string>();
        public string[] Targets = Array.Empty<string>();
        public TacticalCombatNetworkVector[] Waypoints = Array.Empty<TacticalCombatNetworkVector>();
        public int[] Groups = Array.Empty<int>();
    }
}
