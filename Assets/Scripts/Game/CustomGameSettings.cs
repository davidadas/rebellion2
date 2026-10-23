using System;
using System.IO;
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

        [PersistableMember(Name = nameof(GarrisonRules))]
        private GarrisonRuleSettings _garrisonRules;

        [PersistableMember(Name = nameof(MovementRules))]
        private MovementRuleSettings _movementRules;

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

        [PersistableIgnore]
        public GarrisonRuleSettings GarrisonRules
        {
            get => _garrisonRules;
            set => _garrisonRules = value;
        }

        [PersistableIgnore]
        public MovementRuleSettings MovementRules
        {
            get => _movementRules;
            set => _movementRules = value;
        }

        /// <summary>
        /// Creates an empty custom-game settings payload for population or deserialization.
        /// </summary>
        public CustomGameSettings() { }

        /// <summary>
        /// Applies persisted campaign-specific runtime rules to a detached game configuration.
        /// </summary>
        /// <param name="config">The runtime configuration receiving the overrides.</param>
        public void ApplyRuntimeRules(GameConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (GarrisonRules != null)
            {
                if (GarrisonRules.SupportThreshold is < 0 or > 100)
                    throw new InvalidDataException(
                        "Garrison support threshold must be from 0 to 100."
                    );
                if (GarrisonRules.SupportDeficitPerRegiment < 1)
                    throw new InvalidDataException(
                        "Garrison support deficit per regiment must be positive."
                    );
                if (GarrisonRules.UprisingMultiplier < 1)
                    throw new InvalidDataException(
                        "Garrison uprising multiplier must be positive."
                    );

                GameConfig.GarrisonConfig garrison =
                    config.AI?.Garrison
                    ?? throw new InvalidDataException("Runtime garrison configuration is missing.");
                garrison.SupportThreshold = GarrisonRules.SupportThreshold;
                garrison.GarrisonDivisor = GarrisonRules.SupportDeficitPerRegiment;
                garrison.UprisingMultiplier = GarrisonRules.UprisingMultiplier;
            }

            if (MovementRules != null)
            {
                if (MovementRules.TravelSpeedPercent < 1)
                    throw new InvalidDataException("Travel speed percent must be positive.");

                GameConfig.MovementConfig movement =
                    config.Movement
                    ?? throw new InvalidDataException("Runtime movement configuration is missing.");
                if (movement.DistanceDivisor <= 0)
                    throw new InvalidDataException(
                        "Movement distance divisor must be greater than zero."
                    );

                movement.DistanceDivisor *= MovementRules.TravelSpeedPercent / 100d;
            }
        }
    }

    /// <summary>
    /// Defines campaign-specific rules for support-based garrison requirements.
    /// </summary>
    [PersistableObject]
    public sealed class GarrisonRuleSettings
    {
        public int SupportThreshold { get; set; }

        public int SupportDeficitPerRegiment { get; set; }

        public int UprisingMultiplier { get; set; }
    }

    /// <summary>
    /// Defines campaign-specific strategic movement rules.
    /// </summary>
    [PersistableObject]
    public sealed class MovementRuleSettings
    {
        public int TravelSpeedPercent { get; set; }
    }
}
