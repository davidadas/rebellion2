using System;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    public enum GameSize
    {
        Small = 0,
        Medium = 1,
        Large = 2,
    }

    public enum GameDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2,
    }

    public enum GameVictoryCondition
    {
        Headquarters,
        Conquest,
    }

    public enum GameResourceAvailability
    {
        Limited,
        Normal,
        Abundant,
    }

    /// <summary>
    /// Contains the configuration and state for a game session.
    /// </summary>
    [PersistableObject]
    public sealed class GameSummary
    {
        // Game Options.
        public GameSize GalaxySize = GameSize.Large;
        public GameDifficulty Difficulty = GameDifficulty.Easy;
        public GameVictoryCondition VictoryCondition = GameVictoryCondition.Conquest;
        public GameResourceAvailability ResourceAvailability = GameResourceAvailability.Normal;
        public string[] StartingFactionIDs = Array.Empty<string>();
        public int StartingResearchLevel = 1;
        public string PlayerFactionID;
        public int Seed = Guid.NewGuid().GetHashCode();

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public GameSummary() { }
    }
}
