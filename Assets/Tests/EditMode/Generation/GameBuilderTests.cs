using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class GameBuilderTests
{
    [Test]
    public void TestGameBuilderWithConfigs()
    {
        // Create a new GameSummary object with specific configurations
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        // Create a new GameBuilder instance with the summary
        GameBuilder builder = new GameBuilder(summary);

        // Build the game using the GameBuilder
        Game game = builder.BuildGame();

        Assert.IsNotNull(game, "Game should not be null.");

        // Assert that the game's summary properties match the provided configurations
        Assert.AreEqual(summary.GalaxySize, game.Summary.GalaxySize, "GalaxySize should match.");
        Assert.AreEqual(summary.Difficulty, game.Summary.Difficulty, "Difficulty should match.");
        Assert.AreEqual(
            summary.VictoryCondition,
            game.Summary.VictoryCondition,
            "VictoryCondition should match."
        );
        Assert.AreEqual(
            summary.ResourceAvailability,
            game.Summary.ResourceAvailability,
            "ResourceAvailability should match."
        );
        Assert.AreEqual(
            summary.PlayerFactionID,
            game.Summary.PlayerFactionID,
            "PlayerFactionID should match."
        );
    }

    [Test]
    public void TestAllFactionsHaveHQs()
    {
        // Create a new GameSummary object with specific configurations
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        // Create a new GameBuilder instance with the summary
        GameBuilder builder = new GameBuilder(summary);

        // Build the game using the GameBuilder
        Game game = builder.BuildGame();

        Assert.IsNotNull(game, "Game should not be null.");

        // Assert that the game's factions and galaxy map are not null
        Assert.IsNotNull(game.Factions, "Factions should not be null.");
        Assert.IsNotNull(game.Galaxy, "GalaxyMap should not be null.");

        // Iterate through each faction in the game
        foreach (Faction faction in game.Factions)
        {
            // Check if the faction has a headquarters on any planet in the galaxy map
            bool hasHQ = game
                .Galaxy.PlanetSystems.SelectMany(ps => ps.Planets)
                .Any(planet => planet.OwnerTypeID == faction.TypeID && planet.IsHeadquarters);

            // Assert that the faction has a headquarters
            Assert.IsTrue(hasHQ, $"Faction {faction.TypeID} should have a headquarters.");
        }
    }
}
