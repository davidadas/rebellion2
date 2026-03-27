using NUnit.Framework;
using Rebellion.Game;

[TestFixture]
public class GameSummaryTests
{
    [Test]
    public void Constructor_InitializesWithDefaults()
    {
        GameSummary summary = new GameSummary();

        Assert.IsTrue(summary.IsNewGame, "IsNewGame should be true by default");
        Assert.AreEqual(GameSize.Large, summary.GalaxySize, "GalaxySize should default to Large");
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
        Assert.IsEmpty(summary.StartingFactionIDs, "StartingFactionIDs should be empty by default");
        Assert.AreEqual(1, summary.StartingResearchLevel, "StartingResearchLevel should be 1");
    }

    [Test]
    public void SerializeAndDeserialize_MaintainsState()
    {
        GameSummary summary = new GameSummary
        {
            IsNewGame = false,
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Hard,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            StartingFactionIDs = new string[] { "FACTION1", "FACTION2" },
            StartingResearchLevel = 3,
            PlayerFactionID = "FACTION1",
        };

        string serialized = SerializationHelper.Serialize(summary);
        GameSummary deserialized = SerializationHelper.Deserialize<GameSummary>(serialized);

        Assert.AreEqual(
            summary.IsNewGame,
            deserialized.IsNewGame,
            "IsNewGame should be correctly deserialized."
        );
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
            summary.StartingResearchLevel,
            deserialized.StartingResearchLevel,
            "StartingResearchLevel should be correctly deserialized."
        );
        Assert.AreEqual(
            summary.PlayerFactionID,
            deserialized.PlayerFactionID,
            "PlayerFactionID should be correctly deserialized."
        );
    }
}
