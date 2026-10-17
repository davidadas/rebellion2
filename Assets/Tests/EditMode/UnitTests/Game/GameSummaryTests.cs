using NUnit.Framework;
using Rebellion.Game;

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
        public void SerializeAndDeserialize_PopulatedSummary_MaintainsState()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Hard,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Abundant,
                StartingFactionIDs = new string[] { "FACTION1", "FACTION2" },
                PlayerFactionID = "FACTION1",
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
                GenerationConfigOverrideXml = "<GameGenerationConfig />",
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
                deserialized.PlayerDifficultyModifiers.MineOutputPercent,
                "Player modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                10,
                deserialized.PlayerDifficultyModifiers.MissionSuccessChancePoints,
                "Player mission modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                1.1,
                deserialized.PlayerDifficultyModifiers.DetectionRatingMultiplier,
                "Player detection modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                150,
                deserialized.AIDifficultyModifiers.ManufacturingSpeedPercent,
                "AI modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                20,
                deserialized.AIDifficultyModifiers.MissionFoilChancePoints,
                "AI mission modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                1.25,
                deserialized.AIDifficultyModifiers.DetectionRatingMultiplier,
                "AI detection modifiers should be correctly deserialized."
            );
            Assert.AreEqual(
                "<GameGenerationConfig />",
                deserialized.GenerationConfigOverrideXml,
                "Generation overrides should be correctly deserialized."
            );
        }
    }
} // namespace Rebellion.Tests.Game
