using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Util.Reflection;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class GameSetupTests
    {
        [Test]
        public void Constructor_DefaultConstruction_InitializesWithDefaults()
        {
            GameSetup setup = new GameSetup();

            Assert.AreEqual(GameSize.Large, setup.GalaxySize, "GalaxySize should default to Large");
            Assert.AreEqual(
                GameDifficulty.Easy,
                setup.Difficulty,
                "Difficulty should default to Easy"
            );
            Assert.AreEqual(
                GameVictoryCondition.Conquest,
                setup.VictoryCondition,
                "VictoryCondition should default to Conquest"
            );
            Assert.AreEqual(
                GameResourceAvailability.Normal,
                setup.ResourceAvailability,
                "ResourceAvailability should default to Normal"
            );
            Assert.IsEmpty(
                setup.StartingFactionIDs,
                "StartingFactionIDs should be empty by default"
            );
            Assert.IsNull(setup.CustomSettings);
            Assert.IsFalse(setup.IsCustomGame);
        }

        [Test]
        public void SerializeAndDeserialize_ExplicitSeed_RoundTripsExactly()
        {
            GameSetup setup = new GameSetup { Seed = 12345 };

            string serialized = SerializationHelper.Serialize(setup);
            GameSetup deserialized = SerializationHelper.Deserialize<GameSetup>(serialized);

            Assert.AreEqual(12345, deserialized.Seed);
        }

        [Test]
        public void SerializeAndDeserialize_StandardSetup_RemainsStandard()
        {
            GameSetup setup = new GameSetup { Seed = 12345 };

            string serialized = SerializationHelper.Serialize(setup);
            GameSetup deserialized = SerializationHelper.Deserialize<GameSetup>(serialized);

            Assert.IsNull(deserialized.CustomSettings);
            Assert.IsFalse(deserialized.IsCustomGame);
        }

        [Test]
        public void SerializeAndDeserialize_DifficultyOnlyCustomSettings_RemainsCustom()
        {
            GameSetup setup = new GameSetup
            {
                CustomSettings = new CustomGameSettings
                {
                    PlayerDifficultyModifiers = new DifficultyModifiers { MineOutputPercent = 125 },
                },
            };

            GameSetup restored = SerializationHelper.Deserialize<GameSetup>(
                SerializationHelper.Serialize(setup)
            );

            Assert.IsTrue(restored.IsCustomGame);
            Assert.IsNull(restored.CustomSettings.GenerationConfig);
            Assert.AreEqual(
                125,
                restored.CustomSettings.PlayerDifficultyModifiers.MineOutputPercent
            );
        }

        [Test]
        public void SerializeAndDeserialize_PopulatedSetup_MaintainsState()
        {
            GameGenerationConfig generationConfig = TestContent.Data.GenerationConfig.GetDeepCopy();
            generationConfig.FacilityGeneration.CoreMineMultiplier = 17;
            GameSetup setup = new GameSetup
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Hard,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Abundant,
                StartingFactionIDs = new string[] { "FACTION1", "FACTION2" },
                PlayerFactionID = "FACTION1",
                CustomSettings = new CustomGameSettings
                {
                    PlayerDifficultyModifiers = new DifficultyModifiers
                    {
                        MineOutputPercent = 125,
                        MissionSuccessChancePoints = 10,
                        DetectionRatingMultiplier = 1.1,
                    },
                    AIDifficultyModifiers = new DifficultyModifiers
                    {
                        ManufacturingSpeedPercent = 150,
                        MissionFoilChancePoints = 20,
                        DetectionRatingMultiplier = 1.25,
                    },
                    GenerationConfig = generationConfig,
                },
            };

            string serialized = SerializationHelper.Serialize(setup);
            GameSetup deserialized = SerializationHelper.Deserialize<GameSetup>(serialized);

            Assert.AreEqual(
                setup.GalaxySize,
                deserialized.GalaxySize,
                "GalaxySize should be correctly deserialized."
            );
            Assert.AreEqual(
                setup.Difficulty,
                deserialized.Difficulty,
                "Difficulty should be correctly deserialized."
            );
            Assert.AreEqual(
                setup.VictoryCondition,
                deserialized.VictoryCondition,
                "VictoryCondition should be correctly deserialized."
            );
            Assert.AreEqual(
                setup.ResourceAvailability,
                deserialized.ResourceAvailability,
                "ResourceAvailability should be correctly deserialized."
            );
            Assert.AreEqual(
                setup.StartingFactionIDs.Length,
                deserialized.StartingFactionIDs.Length,
                "StartingFactionIDs length should be correctly deserialized."
            );
            Assert.AreEqual(
                setup.PlayerFactionID,
                deserialized.PlayerFactionID,
                "PlayerFactionID should be correctly deserialized."
            );
            Assert.AreEqual(
                125,
                deserialized.CustomSettings.PlayerDifficultyModifiers.MineOutputPercent,
                "Player modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                10,
                deserialized.CustomSettings.PlayerDifficultyModifiers.MissionSuccessChancePoints,
                "Player mission modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                1.1,
                deserialized.CustomSettings.PlayerDifficultyModifiers.DetectionRatingMultiplier,
                "Player detection modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                150,
                deserialized.CustomSettings.AIDifficultyModifiers.ManufacturingSpeedPercent,
                "AI modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                20,
                deserialized.CustomSettings.AIDifficultyModifiers.MissionFoilChancePoints,
                "AI mission modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                1.25,
                deserialized.CustomSettings.AIDifficultyModifiers.DetectionRatingMultiplier,
                "AI detection modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                17,
                deserialized.CustomSettings.GenerationConfig.FacilityGeneration.CoreMineMultiplier,
                "Generation overrides should be correctly deserialized."
            );
            Assert.IsTrue(deserialized.IsCustomGame);
        }
    }
} // namespace Rebellion.Tests.Game
