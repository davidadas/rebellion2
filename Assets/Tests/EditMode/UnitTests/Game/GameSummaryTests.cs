using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Util.Reflection;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class GameSummaryTests
    {
        [Test]
        public void Constructor_DefaultConstruction_InitializesWithDefaults()
        {
            GameSummary summary = new GameSummary();

            Assert.AreEqual(
                GameSize.Large,
                summary.GalaxySize,
                "GalaxySize should default to Large"
            );
            Assert.AreEqual(
                GameDifficulty.Easy,
                summary.Difficulty,
                "Difficulty should default to Easy"
            );
            Assert.AreEqual(
                GameVictoryCondition.Conquest,
                summary.VictoryCondition,
                "VictoryCondition should default to Conquest"
            );
            Assert.AreEqual(
                GameResourceAvailability.Normal,
                summary.ResourceAvailability,
                "ResourceAvailability should default to Normal"
            );
            Assert.IsEmpty(
                summary.StartingFactionIDs,
                "StartingFactionIDs should be empty by default"
            );
            Assert.IsNull(summary.CustomSettings);
            Assert.IsFalse(summary.IsCustomGame);
        }

        [Test]
        public void SerializeAndDeserialize_ExplicitSeed_RoundTripsExactly()
        {
            GameSummary summary = new GameSummary { Seed = 12345 };

            string serialized = SerializationHelper.Serialize(summary);
            GameSummary deserialized = SerializationHelper.Deserialize<GameSummary>(serialized);

            Assert.AreEqual(12345, deserialized.Seed);
        }

        [Test]
        public void SerializeAndDeserialize_StandardSetup_RemainsStandard()
        {
            GameSummary summary = new GameSummary { Seed = 12345 };

            string serialized = SerializationHelper.Serialize(summary);
            GameSummary deserialized = SerializationHelper.Deserialize<GameSummary>(serialized);

            Assert.IsNull(deserialized.CustomSettings);
            Assert.IsFalse(deserialized.IsCustomGame);
        }

        [Test]
        public void SerializeAndDeserialize_DifficultyOnlyCustomSettings_RemainsCustom()
        {
            GameSummary summary = new GameSummary
            {
                CustomSettings = new CustomGameSettings
                {
                    PlayerDifficultyModifiers = new DifficultyModifiers { MineOutputPercent = 125 },
                },
            };

            GameSummary restored = SerializationHelper.Deserialize<GameSummary>(
                SerializationHelper.Serialize(summary)
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
            GameSummary summary = new GameSummary
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
                    GarrisonRules = new GarrisonRuleSettings
                    {
                        SupportThreshold = 70,
                        SupportDeficitPerRegiment = 5,
                        UprisingMultiplier = 3,
                    },
                    MovementRules = new MovementRuleSettings { TravelSpeedPercent = 150 },
                },
            };

            string serialized = SerializationHelper.Serialize(summary);
            GameSummary deserialized = SerializationHelper.Deserialize<GameSummary>(serialized);

            Assert.AreEqual(
                summary.GalaxySize,
                deserialized.GalaxySize,
                "GalaxySize should be correctly deserialized."
            );
            Assert.AreEqual(
                summary.Difficulty,
                deserialized.Difficulty,
                "Difficulty should be correctly deserialized."
            );
            Assert.AreEqual(
                summary.VictoryCondition,
                deserialized.VictoryCondition,
                "VictoryCondition should be correctly deserialized."
            );
            Assert.AreEqual(
                summary.ResourceAvailability,
                deserialized.ResourceAvailability,
                "ResourceAvailability should be correctly deserialized."
            );
            Assert.AreEqual(
                summary.StartingFactionIDs.Length,
                deserialized.StartingFactionIDs.Length,
                "StartingFactionIDs length should be correctly deserialized."
            );
            Assert.AreEqual(
                summary.PlayerFactionID,
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
            Assert.AreEqual(70, deserialized.CustomSettings.GarrisonRules.SupportThreshold);
            Assert.AreEqual(5, deserialized.CustomSettings.GarrisonRules.SupportDeficitPerRegiment);
            Assert.AreEqual(3, deserialized.CustomSettings.GarrisonRules.UprisingMultiplier);
            Assert.AreEqual(150, deserialized.CustomSettings.MovementRules.TravelSpeedPercent);
            Assert.IsTrue(deserialized.IsCustomGame);
        }
    }
} // namespace Rebellion.Tests.Game
