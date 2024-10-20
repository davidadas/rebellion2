using NUnit.Framework;
using System.Linq;
using System.Collections.Generic;

[TestFixture]
public class GameTests
{

    [Test]
    public void TestGameInitialization()
    {
        // Generate a game given a summary.
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        // Initialize the game with the summary.
        Game game = new Game(summary)
        {
            Summary = summary,
            Galaxy = new GalaxyMap(),
        };

        // Check if the game was initialized correctly.
        Assert.IsNotNull(game.Summary, "Game summary should not be null.");
        Assert.AreEqual(GameSize.Large, game.Summary.GalaxySize, "GalaxySize should match.");
        Assert.AreEqual(GameDifficulty.Easy, game.Summary.Difficulty, "Difficulty should match.");
        Assert.AreEqual(GameVictoryCondition.Headquarters, game.Summary.VictoryCondition, "VictoryCondition should match.");
        Assert.AreEqual(GameResourceAvailability.Abundant, game.Summary.ResourceAvailability, "ResourceAvailability should match.");
        Assert.AreEqual("FNALL1", game.Summary.PlayerFactionID, "PlayerFactionID should match.");
        Assert.IsNotNull(game.Galaxy, "Galaxy should not be null.");
    }

    [Test]
    public void TestSceneGraphReconstitutionAfterSerialization()
    {
        // Create officers.
        List<Officer> officers = new List<Officer>
        {
            new Officer { OwnerTypeID = "FNALL1" }
        };

        // Create capital ships.
        List<CapitalShip> capitalShips = new List<CapitalShip>
        {
            new CapitalShip
            {
                OwnerTypeID = "FNALL1",
                Officers = officers
            }
        };

        // Create fleets.
        List<Fleet> fleets = new List<Fleet>
        {
            new Fleet
            {
                OwnerTypeID = "FNALL1",
                CapitalShips = capitalShips
            }
        };

        // Create planets.
        List<Planet> planets = new List<Planet>
        {
            new Planet
            {
                OwnerTypeID = "FNALL1",
                Fleets = fleets
            }
        };

        // Create planet systems.
        List<PlanetSystem> planetSystems = new List<PlanetSystem>
        {
            new PlanetSystem
            {
                Planets = planets
            }
        };

        // Create the galaxy.
        GalaxyMap galaxy = new GalaxyMap
        {
            PlanetSystems = planetSystems
        };

        // Create the game.
        Game game = new Game
        {
            Galaxy = galaxy
        };

        game.Galaxy.Traverse((SceneNode node) => {
            string instanceID = node.InstanceID;
            SceneNode sceneNode = game.GetSceneNodeByInstanceID<SceneNode>(instanceID);

            // Check if the node is registered by instance ID.
            Assert.AreEqual(node.InstanceID, sceneNode.InstanceID, "Node should be registered by instance ID.");
            
            // Check if the node has a parent (except for the GalaxyMap).
            if (!(node is GalaxyMap))
            {
                Assert.IsNotNull(node.GetParent(), $"{node.GetType().Name} should have a parent.");
            }
        });
    }

    [Test]
    public void TestAttachAndDetachNode()
    {
        // Create planet systems
        PlanetSystem planetSystem = new PlanetSystem();
        List<PlanetSystem> planetSystems = new List<PlanetSystem>
        {
            planetSystem,
        };

        // Create galaxy map.
        GalaxyMap galaxy = new GalaxyMap
        {
            PlanetSystems = planetSystems,
        };
        
        // Create the game.
        Game game = new Game
        {
            Galaxy = galaxy,
        };

        // Create our scene.
        Planet planet = new Planet { OwnerTypeID = "FNALL1" };
        Fleet fleet = new Fleet { OwnerTypeID = "FNALL1" };

        // Attach the nodes.
        game.AttachNode(planetSystem, planet);
        game.AttachNode(planet, fleet);

        // Check if the fleet is attached to the planet.
        Assert.Contains(fleet, planet.GetChildren().ToList(), "Fleet should be attached to the planet.");

        // Detach the fleet from the planet.
        game.DetachNode(fleet);

        // Check if the fleet is detached from the planet.
        Assert.IsFalse(planet.GetChildren().Contains(fleet), "Fleet should be detached from the planet.");

        // Check if the fleet has no parent.
        Assert.IsNull(fleet.GetParent(), "Fleet should not have a parent.");
    }

    public void TestMoveNode()
    {        
        Game game = new Game();

        // Create our scene.
        PlanetSystem planetSystem = new PlanetSystem();
        Planet planet1 = new Planet { OwnerTypeID = "FNALL1" };
        Planet planet2 = new Planet { OwnerTypeID = "FNALL1" };
        Fleet fleet = new Fleet { OwnerTypeID = "FNALL1" };

        // Attach the nodes.
        game.AttachNode(planetSystem, planet1);
        game.AttachNode(planetSystem, planet2);
        game.AttachNode(planet1, fleet);

        // Check if the fleet is attached to planet1.
        Assert.Contains(fleet, planet1.GetChildren().ToList(), "Fleet should be attached to planet1.");

        // Move the fleet to planet2.
        game.MoveNode(fleet, planet2);

        // Check if the fleet is attached to planet2.
        Assert.Contains(fleet, planet2.GetChildren().ToList(), "Fleet should be moved to planet2.");

        // Check if the fleet is detached from planet1.
        Assert.IsFalse(planet1.GetChildren().Contains(fleet), "Fleet should not be attached to planet1.");
    }

    [Test]
    public void TestThrowsExceptionWhenAttachingNodeWithParent()
    {
        Game game = new Game();

        // Create our scene.
        Planet planet = new Planet { OwnerTypeID = "FNALL1" };
        Fleet fleet = new Fleet { OwnerTypeID = "FNALL1" };

        // Attach the fleet to the planet.
        game.AttachNode(fleet, planet);

        // Check if an exception is thrown when attaching a node with a parent.
        Assert.Throws<InvalidSceneOperationException>(() => game.AttachNode(fleet, planet), "Exception should be thrown when attaching a node with a parent.");
    }

    [Test]
    public void TestThrowsExceptionWhenDetachingNodeWithoutParent()
    {
        Game game = new Game();

        // Create our scene.
        Fleet fleet = new Fleet { OwnerTypeID = "FNALL1" };

        // Check if an exception is thrown when detaching a node without a parent.
        Assert.Throws<InvalidSceneOperationException>(() => game.DetachNode(fleet), "Exception should be thrown when detaching a node without a parent.");
    }

    [Test]
    public void TestRegisterAndRemoveNodeByInstanceID()
    {;
        Game game = new Game
        {
            Galaxy = new GalaxyMap(),
        };

        // Create our scene.
        Planet planet = new Planet { OwnerTypeID = "FNALL1" };
        PlanetSystem planetSystem = new PlanetSystem()
        {
            Planets = new List<Planet> { planet },
        };

        // Attach the fleet to the planet.
        game.AttachNode(game.Galaxy, planetSystem);

        // Check if the fleet is registered.
        Assert.AreEqual(game.GetSceneNodeByInstanceID<PlanetSystem>(planetSystem.InstanceID), planetSystem, "Planet System should be registered.");
        Assert.AreEqual(game.GetSceneNodeByInstanceID<Planet>(planet.InstanceID), planet, "Planet should be registered.");

        // Detach the fleet from the planet.
        game.DetachNode(planetSystem);

        // Check if the fleet is deregistered.
        Assert.IsNull(game.GetSceneNodeByInstanceID<PlanetSystem>(planetSystem.InstanceID), "Planet System should be deregistered.");
        Assert.IsNull(game.GetSceneNodeByInstanceID<Planet>(planet.InstanceID), "Planet should be deregistered.");

    }
}
