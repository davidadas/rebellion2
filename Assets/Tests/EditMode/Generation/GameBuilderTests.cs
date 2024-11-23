using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class GameBuilderTests
{
    private Game game;

    [OneTimeSetUp]
    public void OneTimeSetUp()
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
        game = builder.BuildGame();
    }

    [Test]
    public void BuildGame_WithSummary_SetsCorrectly()
    {
        Assert.IsNotNull(game, "Game should not be null.");

        // Assert that the game's summary properties match the provided configurations
        Assert.AreEqual(GameSize.Large, game.Summary.GalaxySize, "GalaxySize should match.");
        Assert.AreEqual(GameDifficulty.Easy, game.Summary.Difficulty, "Difficulty should match.");
        Assert.AreEqual(
            GameVictoryCondition.Headquarters,
            game.Summary.VictoryCondition,
            "VictoryCondition should match."
        );
        Assert.AreEqual(
            GameResourceAvailability.Abundant,
            game.Summary.ResourceAvailability,
            "ResourceAvailability should match."
        );
        Assert.AreEqual("FNALL1", game.Summary.PlayerFactionID, "PlayerFactionID should match.");
    }

    [Test]
    public void BuildGame_WithFactions_SetsHQs()
    {
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
                .Any(planet =>
                    planet.OwnerInstanceID == faction.InstanceID && planet.IsHeadquarters
                );

            // Assert that the faction has a headquarters
            Assert.IsTrue(hasHQ, $"Faction {faction.InstanceID} should have a headquarters.");
        }
    }

    [Test]
    public void BuildGame_WithOfficers_SetsOfficerValues()
    {
        List<Officer> officers = new List<Officer>();

        game.Galaxy.Traverse(node =>
        {
            if (node is Officer officer)
            {
                officers.Add(officer);

                // Ensure at least one skill is non-zero
                bool hasNonZeroSkill = officer.Skills.Values.Any(skillValue => skillValue > 0);
                Assert.IsTrue(
                    hasNonZeroSkill,
                    $"Officer {officer.InstanceID} should have at least one non-zero skill."
                );
            }
        });

        // Ensure the game has at least two officers
        Assert.Greater(officers.Count, 2, "Game should have at least two officers.");
    }
}
