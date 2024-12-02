using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class GameBuilderTests
{
    private static readonly Lazy<Game[]> LazyGameTestCases = new Lazy<Game[]>(
        () =>
            new[]
            {
                CreateGame(GameSize.Small, GameDifficulty.Medium, GameVictoryCondition.Conquest),
                CreateGame(GameSize.Medium, GameDifficulty.Medium, GameVictoryCondition.Conquest),
                CreateGame(GameSize.Large, GameDifficulty.Medium, GameVictoryCondition.Conquest),
            }
    );

    private static Game[] GameTestCases => LazyGameTestCases.Value;

    private static Game CreateGame(
        GameSize size,
        GameDifficulty difficulty,
        GameVictoryCondition victoryCondition
    )
    {
        // Create a new GameSummary object with specific configurations.
        GameSummary summary = new GameSummary
        {
            GalaxySize = size,
            Difficulty = difficulty,
            VictoryCondition = victoryCondition,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        // Create a new GameBuilder instance with the summary.
        GameBuilder builder = new GameBuilder(summary);

        // Build the game using the GameBuilder.
        return builder.BuildGame();
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_SetsConsistentOwners(Game game)
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
                    $"Child \"{child.GetDisplayName()}\" should have the same owner as its parent, \"{node.GetDisplayName()}\"."
                );
            }
        });
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_SetsChildParentRelationships(Game game)
    {
        game.Galaxy.Traverse(node =>
        {
            List<ISceneNode> children = node.GetChildren().ToList();

            foreach (ISceneNode child in children)
            {
                // Ensure the child has the parent as its parent.
                Assert.AreEqual(
                    node,
                    child.GetParent(),
                    "Child should have the parent as its parent."
                );
            }
        });
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_SetsGameSummary(Game game)
    {
        Assert.IsNotNull(game, "Game should not be null.");
        Assert.IsNotNull(game.Summary, "Game summary should not be null.");

        // Check that the game's summary properties are within expected ranges.
        Assert.IsTrue(
            Enum.IsDefined(typeof(GameSize), game.Summary.GalaxySize),
            "GalaxySize should be a valid enum value."
        );
        Assert.IsTrue(
            Enum.IsDefined(typeof(GameDifficulty), game.Summary.Difficulty),
            "Difficulty should be a valid enum value."
        );
        Assert.IsTrue(
            Enum.IsDefined(typeof(GameVictoryCondition), game.Summary.VictoryCondition),
            "VictoryCondition should be a valid enum value."
        );
        Assert.IsTrue(
            Enum.IsDefined(typeof(GameResourceAvailability), game.Summary.ResourceAvailability),
            "ResourceAvailability should be a valid enum value."
        );

        // Check that PlayerFactionID is not null or empty.
        Assert.IsFalse(
            string.IsNullOrEmpty(game.Summary.PlayerFactionID),
            "PlayerFactionID should not be null or empty."
        );
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_SetsFactions(Game game)
    {
        Assert.IsNotNull(game.Factions, "Factions should not be null.");

        // Ensure the game has at least two factions.
        Assert.GreaterOrEqual(game.Factions.Count, 2, "Game should have at least two factions.");
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_SetsFactionTechnologies(Game game)
    {
        foreach (Faction faction in game.Factions)
        {
            // Ensure the faction has technology levels.
            Assert.IsNotEmpty(faction.TechnologyLevels, "Faction should have technology levels.");

            // Ensure the faction has at least one technology level for each manufacturing type.
            foreach (
                KeyValuePair<
                    ManufacturingType,
                    SortedDictionary<int, List<Technology>>
                > manufacturingTypeTechLevels in faction.TechnologyLevels
            )
            {
                ManufacturingType manufacturingType = manufacturingTypeTechLevels.Key;
                SortedDictionary<int, List<Technology>> techLevels =
                    manufacturingTypeTechLevels.Value;

                Assert.IsNotEmpty(
                    techLevels,
                    $"Faction should have technology levels for {manufacturingType}."
                );

                foreach (KeyValuePair<int, List<Technology>> levelTechnologies in techLevels)
                {
                    int level = levelTechnologies.Key;
                    List<Technology> technologies = levelTechnologies.Value;

                    Assert.IsNotEmpty(
                        technologies,
                        $"Faction should have at least one technology for {manufacturingType} at level {level}."
                    );
                }
            }
        }
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_SetsHQs(Game game)
    {
        // Assert that the game's factions and galaxy map are not null.
        Assert.IsNotNull(game.Factions, "Factions should not be null.");
        Assert.IsNotNull(game.Galaxy, "GalaxyMap should not be null.");

        foreach (Faction faction in game.Factions)
        {
            // Check if the faction has a headquarters on any planet in the galaxy map.
            bool hasHQ = game
                .Galaxy.PlanetSystems.SelectMany(ps => ps.Planets)
                .Any(planet =>
                    planet.OwnerInstanceID == faction.InstanceID && planet.IsHeadquarters
                );

            // Assert that the faction has a headquarters
            Assert.IsTrue(hasHQ, $"Faction {faction.GetDisplayName()} should have a headquarters.");
        }
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_AssignsFactionsPlanets(Game game)
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

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_DeploysOfficers(Game game)
    {
        List<Officer> officers = new List<Officer>();

        // Traverse the galaxy map to find officers.
        game.Galaxy.Traverse(node =>
        {
            if (node is Officer officer)
            {
                officers.Add(officer);
            }
        });

        // Ensure the game has at least two officers.
        Assert.Greater(officers.Count, 2, "Game should have at least two officers.");
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_InitializesOfficers(Game game)
    {
        // Traverse the galaxy map to find officers.
        game.Galaxy.Traverse(node =>
        {
            if (node is Officer officer)
            {
                // Ensure at least one skill is non-zero.
                bool hasNonZeroSkill = officer.Skills.Values.Any(skillValue => skillValue > 0);
                Assert.IsTrue(
                    hasNonZeroSkill,
                    $"Officer {officer.GetDisplayName()} should have at least one non-zero skill."
                );
            }
        });
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_DeploysFleets(Game game)
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

        foreach (var faction in game.Factions)
        {
            // Ensure the faction has at least one fleet.
            Assert.IsTrue(
                fleetsPerFaction.ContainsKey(faction.GetInstanceID()),
                $"Faction {faction.GetDisplayName()} should have at least one fleet."
            );
        }
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_DeploysMaxOneFleet(Game game)
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
                    $"Planet {planet.GetDisplayName()} should have at most one fleet."
                );
            }
        });
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_DeploysCapitalShips(Game game)
    {
        // Traverse the galaxy map to find fleets.
        game.Galaxy.Traverse(node =>
        {
            if (node is Fleet fleet)
            {
                bool hasCapitalShips = fleet.GetChildren().Count() > 0;

                // Ensure the fleet has at least one capital ship.
                Assert.IsTrue(
                    hasCapitalShips,
                    $"Fleet {fleet.InstanceID} should have at least one capital ship."
                );
            }
        });
    }

    [Test, TestCaseSource(nameof(GameTestCases))]
    public void BuildGame_SetsGameEvents(Game game)
    {
        // Ensure the game has at least one event in the event pool.
        Assert.GreaterOrEqual(
            game.GetEventPool().Count(),
            1,
            "Game should have at most one event in the event pool."
        );
    }
}
