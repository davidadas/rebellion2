using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Contains the complete settings payload for a custom game.
    /// </summary>
    [PersistableObject]
    public sealed class CustomGameSettings
    {
        [PersistableMember(Name = nameof(PlayerDifficultyModifiers))]
        private DifficultyModifiers _playerDifficultyModifiers;

        [PersistableMember(Name = nameof(AIDifficultyModifiers))]
        private DifficultyModifiers _aiDifficultyModifiers;

        [PersistableMember(Name = nameof(GenerationConfig))]
        private GameGenerationConfig _generationConfig;

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
        public GameGenerationConfig GenerationConfig
        {
            get => _generationConfig;
            set => _generationConfig = value;
        }

        /// <summary>
        /// Creates an empty custom-game settings payload for population or deserialization.
        /// </summary>
        public CustomGameSettings() { }
    }
}
