using System;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Metadata about the game (e.g. save game name, player id, etc).
    /// </summary>
    [PersistableObject]
    public sealed class GameMetadata
    {
        public const int CurrentSaveVersion = 1;

        [PersistableMember(Name = nameof(SaveDisplayName))]
        private string _saveDisplayName;

        [PersistableMember(Name = nameof(PlayerFactionID))]
        private string _playerFactionId;

        [PersistableMember(Name = nameof(Difficulty))]
        private GameDifficulty? _difficulty;

        [PersistableMember(Name = nameof(CurrentTick))]
        private int? _currentTick;

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

        [PersistableMember(Name = nameof(OpeningBriefingCompleted))]
        private bool _openingBriefingCompleted;

        [PersistableMember(Name = nameof(LastSavedUtc))]
        private DateTime _lastSavedUtc;

        [PersistableMember(Name = nameof(SaveVersion))]
        private int _saveVersion;

        [PersistableIgnore]
        public string SaveDisplayName
        {
            get => _saveDisplayName;
            set => _saveDisplayName = value;
        }

        [PersistableIgnore]
        public string PlayerFactionID
        {
            get => _playerFactionId;
            set => _playerFactionId = value;
        }

        [PersistableIgnore]
        public GameDifficulty? Difficulty
        {
            get => _difficulty;
            set => _difficulty = value;
        }

        [PersistableIgnore]
        public int? CurrentTick
        {
            get => _currentTick;
            set => _currentTick = value;
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
        public bool OpeningBriefingCompleted
        {
            get => _openingBriefingCompleted;
            set => _openingBriefingCompleted = value;
        }

        [PersistableIgnore]
        public DateTime LastSavedUtc
        {
            get => _lastSavedUtc;
            set => _lastSavedUtc = value;
        }

        [PersistableIgnore]
        public int SaveVersion
        {
            get => _saveVersion;
            set => _saveVersion = value;
        }
    }
}
