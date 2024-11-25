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
    public void BuildGame_CreatesGameSummary()
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
    public void BuildGame_SetsHQs()
    {
        // Assert that the game's factions and galaxy map are not null.
        Assert.IsNotNull(game.Factions, "Factions should not be null.");
        Assert.IsNotNull(game.Galaxy, "GalaxyMap should not be null.");

        // Iterate through each faction in the game.
        foreach (Faction faction in game.Factions)
        {
            // Check if the faction has a headquarters on any planet in the galaxy map.
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
    public void BuildGame_AssignsFactionsPlanets()
    {
        Dictionary<string, List<Planet>> factionPlanets = new Dictionary<string, List<Planet>>();

        // Traverse the galaxy map to find planets owned by each faction.
        game.Galaxy.Traverse(node =>
        {
            // Check if the node is a planet and has an owner.
            // If so, add the planet to the faction's list of planets.
            if (node is Planet planet && planet.GetOwnerInstanceID() != null)
            {
                if (factionPlanets.ContainsKey(planet.OwnerInstanceID))
                {
                    factionPlanets[planet.OwnerInstanceID].Add(planet);
                }
                else
                {
                    factionPlanets[planet.OwnerInstanceID] = new List<Planet> { planet };
                }
            }
        });

        foreach (List<Planet> planets in factionPlanets.Values)
        {
            // Ensure the faction has at least one planet.
            Assert.Greater(planets.Count, 1, "Faction should have at least one planet.");
        }
    }

    [Test]
    public void BuildGame_SetsCorrectOwners()
    {
        // Traverse the galaxy map to find planets.
        game.Galaxy.Traverse(node =>
        {
            // Skip nodes without an owner.
            if (node.GetOwnerInstanceID() == null)
            {
                return;
            }

            List<ISceneNode> children = node.GetChildren().ToList();

            // Ensure each child has the same owner as its parent.
            foreach (ISceneNode child in children)
            {
                Assert.AreEqual(
                    node.GetOwnerInstanceID(),
                    child.GetOwnerInstanceID(),
                    "Child should have the same owner as its parent."
                );
            }
        });
    }

    [Test]
    public void BuildGame_DeploysOfficers()
    {
        List<Officer> officers = new List<Officer>();

        // Traverse the galaxy map to find officers.
        game.Galaxy.Traverse(node =>
        {
            if (node is Officer officer)
            {
                officers.Add(officer);

                // Ensure at least one skill is non-zero.
                bool hasNonZeroSkill = officer.Skills.Values.Any(skillValue => skillValue > 0);
                Assert.IsTrue(
                    hasNonZeroSkill,
                    $"Officer {officer.InstanceID} should have at least one non-zero skill."
                );
            }
        });

        // Ensure the game has at least two officers.
        Assert.Greater(officers.Count, 2, "Game should have at least two officers.");
    }

    [Test]
    public void BuildGame_DeploysFleets()
    {
        Dictionary<string, int> fleetsPerFaction = new Dictionary<string, int>();

        // Traverse the galaxy map to find fleets.
        game.Galaxy.Traverse(node =>
        {
            if (node is Fleet fleet)
            {
                string ownerInstanceID = fleet.GetOwnerInstanceID();
                if (fleetsPerFaction.ContainsKey(ownerInstanceID))
                {
                    fleetsPerFaction[ownerInstanceID]++;
                }
                else
                {
                    fleetsPerFaction[ownerInstanceID] = 1;
                }
            }
        });

        foreach (var factionID in game.Factions)
        {
            // Ensure the faction has at least one fleet.
            Assert.IsTrue(
                fleetsPerFaction.ContainsKey(factionID.InstanceID),
                $"Faction {factionID.InstanceID} should have at least one fleet."
            );
        }
    }

    [Test]
    public void BuildGame_DeploysMaxOneFleet()
    {
        // Traverse the galaxy map to find planets.
        game.Galaxy.Traverse(node =>
        {
            if (node is Planet planet)
            {
                // Ensure the planet has at most one fleet.
                Assert.LessOrEqual(
                    planet.GetFleets().Count(),
                    1,
                    $"Planet {planet.InstanceID} should have at most one fleet."
                );
            }
        });
    }

    [Test]
    public void BuildGame_DeploysCapitalShips()
    {
        // Traverse the galaxy map to find fleets.
        game.Galaxy.Traverse(node =>
        {
            if (node is Fleet fleet)
            {
                bool hasCapitalShips = fleet.GetChildren().Count() > 0;
                GameLogger.Log(
                    $"Fleet {fleet.InstanceID} has {fleet.GetChildren().Count()} capital ships. Parent {fleet.GetParent().GetDisplayName()}"
                );
                // Ensure the fleet has at least one capital ship.
                Assert.IsTrue(
                    hasCapitalShips,
                    $"Fleet {fleet.InstanceID} should have at least one capital ship."
                );
            }
        });
    }
}
