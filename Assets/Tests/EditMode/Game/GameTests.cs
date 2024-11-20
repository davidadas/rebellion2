using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class GameTests
{
    private List<Faction> factions = new List<Faction>
    {
        new Faction { InstanceID = "FNALL1" },
        new Faction { InstanceID = "FNEMP1" },
    };

    [Test]
    public void InitializeGame_WithSummary_SetsPropertiesCorrectly()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        Game game = new Game(summary)
        {
            Summary = summary,
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };

        Assert.IsNotNull(game.Summary, "Game summary should not be null.");
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
        Assert.IsNotNull(game.Galaxy, "Galaxy should not be null.");
    }

    [Test]
    public void AttachNode_ToParent_AddsNodeCorrectly()
    {
        PlanetSystem planetSystem = new PlanetSystem();
        GalaxyMap galaxy = new GalaxyMap
        {
            PlanetSystems = new List<PlanetSystem> { planetSystem },
        };
        Game game = new Game { Factions = factions, Galaxy = galaxy };
        Planet planet = new Planet { OwnerInstanceID = "FNALL1" };

        game.AttachNode(planet, planetSystem);

        Assert.Contains(
            planet,
            planetSystem.GetChildren().ToList(),
            "Planet should be attached to the PlanetSystem."
        );
    }

    [Test]
    public void DetachNode_FromParent_RemovesNodeCorrectly()
    {
        PlanetSystem planetSystem = new PlanetSystem();
        GalaxyMap galaxy = new GalaxyMap
        {
            PlanetSystems = new List<PlanetSystem> { planetSystem },
        };
        Game game = new Game { Factions = factions, Galaxy = galaxy };
        Planet planet = new Planet { OwnerInstanceID = "FNALL1" };

        game.AttachNode(planet, planetSystem);
        game.DetachNode(planet);

        Assert.IsFalse(
            planetSystem.GetChildren().Contains(planet),
            "Planet should be detached from the PlanetSystem."
        );
        Assert.IsNull(planet.GetParent(), "Planet should not have a parent after being detached.");
    }

    [Test]
    public void AttachNode_WithExistingParent_ThrowsException()
    {
        Game game = new Game { Factions = factions };
        Planet planet1 = new Planet { OwnerInstanceID = "FNALL1" };
        Planet planet2 = new Planet { OwnerInstanceID = "FNALL1" };
        Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };

        game.AttachNode(fleet, planet1);

        Assert.Throws<InvalidSceneOperationException>(
            () => game.AttachNode(fleet, planet2),
            "Exception should be thrown when attaching a node with a parent."
        );
    }

    [Test]
    public void DetachNode_WithoutParent_ThrowsException()
    {
        Game game = new Game();
        Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };

        Assert.Throws<InvalidSceneOperationException>(
            () => game.DetachNode(fleet),
            "Exception should be thrown when detaching a node without a parent."
        );
    }

    [Test]
    public void DetachNodeByInstanceID_RegistersAndDeregistersCorrectly()
    {
        Planet planet = new Planet { OwnerInstanceID = "FNALL1" };
        PlanetSystem planetSystem = new PlanetSystem { Planets = new List<Planet> { planet } };
        GalaxyMap galaxy = new GalaxyMap();

        Game game = new Game
        {
            Factions = new List<Faction> { new Faction { InstanceID = "FNALL1" } },
            Galaxy = galaxy,
        };

        game.AttachNode(planetSystem, game.Galaxy);

        Assert.AreEqual(
            game.GetSceneNodeByInstanceID<PlanetSystem>(planetSystem.InstanceID),
            planetSystem
        );
        Assert.AreEqual(game.GetSceneNodeByInstanceID<Planet>(planet.InstanceID), planet);

        game.DetachNode(planetSystem);

        Assert.IsNull(game.GetSceneNodeByInstanceID<PlanetSystem>(planetSystem.InstanceID));
        Assert.IsNull(game.GetSceneNodeByInstanceID<Planet>(planet.InstanceID));
    }

    [Test]
    public void SerializeAndDeserialize_GameObject_ReturnsEquivalentObject()
    {
        // Arrange: Create a game object with some data
        Game game = new Game
        {
            Summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Limited,
                PlayerFactionID = "FNALL1",
            },
            Factions = new List<Faction>
            {
                new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" },
                new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" },
            },
            UnrecruitedOfficers = new List<Officer>
            {
                new Officer { InstanceID = "OFC001", DisplayName = "Luke Skywalker" },
                new Officer { InstanceID = "OFC002", DisplayName = "Darth Vader" },
            },
        };

        // Act: Serialize and deserialize the object
        string serializedGame = SerializationHelper.Serialize(game);
        Game deserializedGame = SerializationHelper.Deserialize<Game>(serializedGame);

        // Assert: Verify the deserialized object matches the original
        Assert.IsNotNull(deserializedGame);
        Assert.AreEqual(game.Summary.GalaxySize, deserializedGame.Summary.GalaxySize);
        Assert.AreEqual(game.Summary.Difficulty, deserializedGame.Summary.Difficulty);
        Assert.AreEqual(game.Summary.VictoryCondition, deserializedGame.Summary.VictoryCondition);
        Assert.AreEqual(
            game.Summary.ResourceAvailability,
            deserializedGame.Summary.ResourceAvailability
        );
        Assert.AreEqual(game.Summary.PlayerFactionID, deserializedGame.Summary.PlayerFactionID);

        Assert.AreEqual(game.Factions.Count, deserializedGame.Factions.Count);
        for (int i = 0; i < game.Factions.Count; i++)
        {
            Assert.AreEqual(game.Factions[i].InstanceID, deserializedGame.Factions[i].InstanceID);
            Assert.AreEqual(game.Factions[i].DisplayName, deserializedGame.Factions[i].DisplayName);
        }

        Assert.AreEqual(game.UnrecruitedOfficers.Count, deserializedGame.UnrecruitedOfficers.Count);
        for (int i = 0; i < game.UnrecruitedOfficers.Count; i++)
        {
            Assert.AreEqual(
                game.UnrecruitedOfficers[i].InstanceID,
                deserializedGame.UnrecruitedOfficers[i].InstanceID
            );
            Assert.AreEqual(
                game.UnrecruitedOfficers[i].DisplayName,
                deserializedGame.UnrecruitedOfficers[i].DisplayName
            );
        }
    }
}
