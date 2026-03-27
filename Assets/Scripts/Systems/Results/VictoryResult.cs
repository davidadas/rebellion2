using Rebellion.Game;

namespace Rebellion.Systems.Results
{
    /// <summary>
    /// Represents a victory condition that has been met.
    /// </summary>
    public struct VictoryResult
    {
        /// <summary>
        /// The faction that achieved victory.
        /// </summary>
        public Faction Winner { get; set; }

        /// <summary>
        /// The faction that was defeated.
        /// </summary>
        public Faction Loser { get; set; }

        /// <summary>
        /// The type of victory that occurred.
        /// Uses GameVictoryCondition for HQ/Conquest modes.
        /// Set to null for automatic victories (Death Star destruction).
        /// </summary>
        public GameVictoryCondition? GameMode { get; set; }

        /// <summary>
        /// Optional description for non-standard victories (e.g., "Death Star Destruction").
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The tick on which the victory occurred.
        /// </summary>
        public int Tick { get; set; }

        /// <summary>
        /// Returns a string representation of the victory result.
        /// </summary>
        public override string ToString()
        {
            string condition = Description ?? GameMode?.ToString() ?? "Unknown";
            return $"{Winner.GetDisplayName()} defeated {Loser.GetDisplayName()} via {condition} at tick {Tick}";
        }
    }
}
