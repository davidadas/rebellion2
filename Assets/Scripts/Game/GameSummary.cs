using System;
using Rebellion.Game.Events;
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

        [PersistableMember(Name = nameof(DisabledEventCategories))]
        private GameEventCategory[] _disabledEventCategories = Array.Empty<GameEventCategory>();

        [PersistableMember(Name = nameof(PlayerDifficultyModifiers))]
        private DifficultyModifiers _playerDifficultyModifiers;

        [PersistableMember(Name = nameof(AIDifficultyModifiers))]
        private DifficultyModifiers _aiDifficultyModifiers;

        [PersistableMember(Name = nameof(GenerationConfigOverride))]
        private GameGenerationConfig _generationConfigOverride;

        [PersistableMember(Name = nameof(StartingFactionIDs))]
        private string[] _startingFactionIds = Array.Empty<string>();

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
        public GameEventCategory[] DisabledEventCategories
        {
            get => _disabledEventCategories;
            set => _disabledEventCategories = value ?? Array.Empty<GameEventCategory>();
        }

        [PersistableIgnore]
        public DifficultyModifiers PlayerDifficultyModifiers
        {
            get => _playerDifficultyModifiers;
            set => _playerDifficultyModifiers = value;
        }

        [PersistableIgnore]
        public DifficultyModifiers AIDifficultyModifiers
        {
            get => _aiDifficultyModifiers;
            set => _aiDifficultyModifiers = value;
        }

        [PersistableIgnore]
        public GameGenerationConfig GenerationConfigOverride
        {
            get => _generationConfigOverride;
            set => _generationConfigOverride = value;
        }

        [PersistableIgnore]
        public string[] StartingFactionIDs
        {
            get => _startingFactionIds;
            set => _startingFactionIds = value;
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
        /// Gets whether this campaign was created with a custom game configuration.
        /// </summary>
        [PersistableIgnore]
        public bool IsCustomGame => _generationConfigOverride != null;

        /// <summary>
        /// Returns whether one classified event category is enabled for this campaign.
        /// </summary>
        /// <param name="category">The category to inspect.</param>
        /// <returns>True when events in the category should be included.</returns>
        public bool IsEventCategoryEnabled(GameEventCategory category)
        {
            return category == GameEventCategory.None
                || Array.IndexOf(_disabledEventCategories, category) < 0;
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public GameSummary() { }
    }
}
