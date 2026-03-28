using System.Collections.Generic;

namespace Rebellion.Game.Results
{
    public class CombatResolvedResult : GameResult
    {
        public string PlanetInstanceID { get; set; }
        public List<string> WinningFactionIDs { get; set; } = new List<string>();
        public List<string> LosingFactionIDs { get; set; } = new List<string>();
    }
}
