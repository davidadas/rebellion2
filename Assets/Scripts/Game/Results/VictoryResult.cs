using Rebellion.Game;

namespace Rebellion.Game.Results
{
    /// <summary>
    /// Represents a victory condition being met.
    /// GameMode is null for special victories (e.g. Death Star destruction).
    /// </summary>
    public class VictoryResult : GameResult
    {
        public Faction Winner { get; set; }
        public Faction Loser { get; set; }

        // Null for non-standard victories (Death Star destruction, etc.)
        public GameVictoryCondition? GameMode { get; set; }
        public string Description { get; set; }

        public override string ToString()
        {
            string condition = Description ?? GameMode?.ToString() ?? "Unknown";
            return $"{Winner.GetDisplayName()} defeated {Loser.GetDisplayName()} via {condition} at tick {Tick}";
        }
    }
}
