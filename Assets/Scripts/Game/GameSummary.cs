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
        [PersistableMember(Name = nameof(GalaxySize))]
        private GameSize _galaxySize = GameSize.Large;

        [PersistableMember(Name = nameof(Difficulty))]
        private GameDifficulty _difficulty = GameDifficulty.Easy;

        [PersistableMember(Name = nameof(VictoryCondition))]
        private GameVictoryCondition _victoryCondition = GameVictoryCondition.Conquest;

        [PersistableMember(Name = nameof(ResourceAvailability))]
        private GameResourceAvailability _resourceAvailability = GameResourceAvailability.Normal;

        [PersistableMember(Name = nameof(StartingFactionIDs))]
        private string[] _startingFactionIds = Array.Empty<string>();

        [PersistableMember(Name = nameof(StartingResearchLevel))]
        private int _startingResearchLevel;

        [PersistableMember(Name = nameof(PlayerFactionID))]
        private string _playerFactionId;

        [PersistableMember(Name = nameof(PackID))]
        private string _packId;

        [PersistableMember(Name = nameof(PackVersion))]
        private string _packVersion;

        [PersistableMember(Name = nameof(ScenarioID))]
        private string _scenarioId;

        [PersistableMember(Name = nameof(ModIDs))]
        private string[] _modIds = Array.Empty<string>();

        [PersistableMember(Name = nameof(ModVersions))]
        private string[] _modVersions = Array.Empty<string>();

        [PersistableMember(Name = nameof(Seed))]
        private int _seed = Guid.NewGuid().GetHashCode();

        [PersistableIgnore]
        public GameSize GalaxySize
        {
            get => _galaxySize;
            set => _galaxySize = value;
        }

        [PersistableIgnore]
        public GameDifficulty Difficulty
        {
            get => _difficulty;
            set => _difficulty = value;
        }

        [PersistableIgnore]
        public GameVictoryCondition VictoryCondition
        {
            get => _victoryCondition;
            set => _victoryCondition = value;
        }

        [PersistableIgnore]
        public GameResourceAvailability ResourceAvailability
        {
            get => _resourceAvailability;
            set => _resourceAvailability = value;
        }

        [PersistableIgnore]
        public string[] StartingFactionIDs
        {
            get => _startingFactionIds;
            set => _startingFactionIds = value;
        }

        [PersistableIgnore]
        public int StartingResearchLevel
        {
            get => _startingResearchLevel;
            set => _startingResearchLevel = value;
        }

        [PersistableIgnore]
        public string PlayerFactionID
        {
            get => _playerFactionId;
            set => _playerFactionId = value;
        }

        [PersistableIgnore]
        public string PackID
        {
            get => _packId;
            set => _packId = value;
        }

        [PersistableIgnore]
        public string PackVersion
        {
            get => _packVersion;
            set => _packVersion = value;
        }

        [PersistableIgnore]
        public string ScenarioID
        {
            get => _scenarioId;
            set => _scenarioId = value;
        }

        [PersistableIgnore]
        public string[] ModIDs
        {
            get => _modIds;
            set => _modIds = value;
        }

        [PersistableIgnore]
        public string[] ModVersions
        {
            get => _modVersions;
            set => _modVersions = value;
        }

        [PersistableIgnore]
        public int Seed
        {
            get => _seed;
            set => _seed = value;
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public GameSummary() { }
    }
}
