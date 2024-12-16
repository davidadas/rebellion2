using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class GameTests
{
    private Game game;
    private GameSummary summary;
    private Faction faction1;
    private Faction faction2;
    private GalaxyMap galaxyMap;
    private PlanetSystem planetSystem;
    private Planet planet;
    private Fleet fleet;

    [SetUp]
    public void SetUp()
    {
        // Initialize game summary.
        summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FACTION1",
        };

        // Create factions.
        faction1 = new Faction { InstanceID = "FACTION1", DisplayName = "Alliance" };
        faction2 = new Faction { InstanceID = "FACTION2", DisplayName = "Empire" };

        // Create game objects.
        galaxyMap = new GalaxyMap();
        planetSystem = new PlanetSystem { InstanceID = "SYSTEM1" };
        planet = new Planet { InstanceID = "PLANET1", OwnerInstanceID = "FACTION1" };
        fleet = new Fleet { InstanceID = "FLEET1", OwnerInstanceID = "FACTION1" };

        // Initialize the game.
        game = new Game(summary);
        game.Factions.Add(faction1);
        game.Factions.Add(faction2);
    }

    [Test]
    public void Constructor_WithSummary_InitializesCorrectly()
    {
        // Verify game initialization.
        Assert.AreEqual(summary, game.Summary, "Game summary should match the provided summary");
        Assert.IsNotNull(game.Galaxy, "Galaxy should be initialized");
        Assert.AreEqual(0, game.CurrentTick, "Current tick should be initialized to 0");
        Assert.IsEmpty(game.EventPool, "Event pool should be empty initially");
        Assert.IsEmpty(game.CompletedEventIDs, "Completed event IDs should be empty initially");
    }

    [Test]
    public void GetFactions_ReturnsCorrectFactions()
    {
        // Get factions and verify the count and contents.
        List<Faction> factions = game.GetFactions();
        Assert.AreEqual(2, factions.Count, "Should return two factions");
        Assert.Contains(faction1, factions, "Should contain faction1");
        Assert.Contains(faction2, factions, "Should contain faction2");
    }

    [Test]
    public void GetFactionByOwnerInstanceID_ReturnsCorrectFaction()
    {
        // Get faction by ID and verify.
        Faction retrievedFaction = game.GetFactionByOwnerInstanceID("FACTION1");
        Assert.AreEqual(faction1, retrievedFaction, "Should return the correct faction");
    }

    [Test]
    public void GetFactionByOwnerInstanceID_ThrowsException_WhenFactionNotFound()
    {
        // Attempt to get non-existent faction.
        Assert.Throws<SceneNodeNotFoundException>(
            () => game.GetFactionByOwnerInstanceID("NONEXISTENT"),
            "Should throw exception for non-existent faction"
        );
    }

    [Test]
    public void GetGalaxyMap_ReturnsCorrectGalaxyMap()
    {
        // Get galaxy map and verify.
        GalaxyMap retrievedGalaxyMap = game.GetGalaxyMap();
        Assert.AreEqual(game.Galaxy, retrievedGalaxyMap, "Should return the correct galaxy map");
    }

    [Test]
    public void AttachNode_AddsNodeCorrectly()
    {
        // Attach node and verify.
        game.AttachNode(planet, planetSystem);

        Assert.AreEqual(
            planetSystem,
            planet.GetParent(),
            "Planet should have planetSystem as parent"
        );
        Assert.Contains(
            planet,
            planetSystem.GetChildren().ToList(),
            "PlanetSystem should contain planet as child"
        );
        Assert.IsTrue(
            game.NodesByInstanceID.ContainsKey(planet.InstanceID),
            "Game should contain planet in NodesByInstanceID"
        );
        Assert.IsTrue(
            game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID)
                .GetAllOwnedNodes()
                .Contains(planet),
            "Faction should contain planet in owned units"
        );
    }

    [Test]
    public void AttachNode_ThrowsException_WhenNodeAlreadyHasParent()
    {
        // Attach node to a parent.
        game.AttachNode(planet, planetSystem);

        // Attempt to attach the same node to another parent.
        Assert.Throws<InvalidSceneOperationException>(
            () => game.AttachNode(planet, new PlanetSystem()),
            "Should throw exception when attaching a node that already has a parent"
        );
    }

    [Test]
    public void DetachNode_RemovesNodeCorrectly()
    {
        // Attach and then detach node.
        game.AttachNode(planet, planetSystem);
        game.DetachNode(planet);

        // Verify detachment is successful and node is removed from all relevant structures.
        Assert.IsNull(planet.GetParent(), "Planet should have no parent after detachment");
        Assert.IsFalse(
            planetSystem.GetChildren().Contains(planet),
            "PlanetSystem should not contain planet as child"
        );
        Assert.IsFalse(
            game.NodesByInstanceID.ContainsKey(planet.InstanceID),
            "Game should not contain planet in NodesByInstanceID"
        );
        Assert.IsFalse(
            game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID)
                .GetAllOwnedNodes()
                .Contains(planet),
            "Faction should not contain planet in owned units"
        );
    }

    [Test]
    public void DetachNode_ThrowsException_WhenNodeHasNoParent()
    {
        // Attempt to detach a node with no parent.
        Assert.Throws<InvalidSceneOperationException>(
            () => game.DetachNode(planet),
            "Should throw exception when detaching a node with no parent"
        );
    }

    [Test]
    public void AddSceneNodeByInstanceID_AddsNodeCorrectly()
    {
        // Add node and verify that it is added to the game.
        game.AddSceneNodeByInstanceID(planet);
        Assert.IsTrue(
            game.NodesByInstanceID.ContainsKey(planet.InstanceID),
            "Game should contain planet in NodesByInstanceID"
        );
    }

    [Test]
    public void AddSceneNodeByInstanceID_ThrowsException_WhenDuplicateNodeAdded()
    {
        // Add node to the game.
        game.AddSceneNodeByInstanceID(planet);

        // Attempt to add the same node again.
        Assert.Throws<GameException>(
            () => game.AddSceneNodeByInstanceID(planet),
            "Should throw exception when adding a duplicate node"
        );
    }

    [Test]
    public void RemoveSceneNodeByInstanceID_RemovesNodeCorrectly()
    {
        // Add and then remove node from the game.
        game.AddSceneNodeByInstanceID(planet);
        game.RemoveSceneNodeByInstanceID(planet);

        // Verify removal from all relevant structures.
        Assert.IsFalse(
            game.NodesByInstanceID.ContainsKey(planet.InstanceID),
            "Game should not contain planet in NodesByInstanceID after removal"
        );
    }

    [Test]
    public void GetSceneNodeByInstanceID_ReturnsCorrectNode()
    {
        // Add node and retrieve it.
        game.AddSceneNodeByInstanceID(planet);
        Planet retrievedNode = game.GetSceneNodeByInstanceID<Planet>(planet.InstanceID);

        // Verify retrieval of the correct node.
        Assert.AreEqual(planet, retrievedNode, "Should return the correct node");
    }

    [Test]
    public void GetSceneNodeByInstanceID_ReturnsNull_WhenNodeNotFound()
    {
        // Attempt to retrieve non-existent node.
        Planet retrievedNode = game.GetSceneNodeByInstanceID<Planet>("NONEXISTENT");
        Assert.IsNull(retrievedNode, "Should return null for non-existent node");
    }

    [Test]
    public void GetSceneNodesByInstanceIDs_ReturnsCorrectNodes()
    {
        // Add nodes to the game.
        game.AddSceneNodeByInstanceID(planet);
        game.AddSceneNodeByInstanceID(fleet);

        // Retrieve nodes by IDs.
        List<ISceneNode> retrievedNodes = game.GetSceneNodesByInstanceIDs(
            new List<string> { planet.InstanceID, fleet.InstanceID }
        );

        // Verify retrieval of planets and fleets.
        Assert.AreEqual(2, retrievedNodes.Count, "Should return two nodes");
        Assert.Contains(planet, retrievedNodes, "Should contain planet");
        Assert.Contains(fleet, retrievedNodes, "Should contain fleet");
    }

    [Test]
    public void GetSceneNodesByOwnerInstanceID_ReturnsCorrectNodes()
    {
        // Add nodes to the game.
        game.AddSceneNodeByInstanceID(planet);
        game.AddSceneNodeByInstanceID(fleet);

        // Retrieve nodes by owner ID.
        List<ISceneNode> retrievedNodes = game.GetSceneNodesByOwnerInstanceID<ISceneNode>(
            "FACTION1"
        );

        // Verify retrieval of planets and fleets.
        Assert.AreEqual(2, retrievedNodes.Count, "Should return two nodes");
        Assert.Contains(planet, retrievedNodes, "Should contain planet");
        Assert.Contains(fleet, retrievedNodes, "Should contain fleet");
    }

    [Test]
    public void GetSceneNodesByType_ReturnsCorrectNodes()
    {
        // Set up galaxy structure.
        game.Galaxy = galaxyMap;
        game.AttachNode(planetSystem, galaxyMap);
        game.AttachNode(planet, planetSystem);
        game.AttachNode(fleet, planet);

        // Retrieve planets and verify the count and contents.
        List<Planet> retrievedPlanets = game.GetSceneNodesByType<Planet>();
        Assert.AreEqual(1, retrievedPlanets.Count, "Should return one planet");
        Assert.Contains(planet, retrievedPlanets, "Should contain the specific planet");

        // Retrieve fleets and verify the count and contents.
        List<Fleet> retrievedFleets = game.GetSceneNodesByType<Fleet>();
        Assert.AreEqual(1, retrievedFleets.Count, "Should return one fleet");
        Assert.Contains(fleet, retrievedFleets, "Should contain the specific fleet");
    }

    [Test]
    public void RegisterOwnedUnit_AddsUnitToFaction()
    {
        // Register unit.
        game.RegisterOwnedUnit(planet);

        // Verify registration is successful.
        Assert.IsTrue(
            game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID)
                .GetAllOwnedNodes()
                .Contains(planet),
            "Faction should contain planet in owned units after registration"
        );
    }

    [Test]
    public void DeregsiterOwnedUnit_RemovesUnitFromFaction()
    {
        // Register and then deregister unit.
        game.RegisterOwnedUnit(planet);
        game.DeregsiterOwnedUnit(planet);

        // Verify deregistration was successful.
        Assert.IsFalse(
            game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID)
                .GetAllOwnedNodes()
                .Contains(planet),
            "Faction should not contain planet in owned units after deregistration"
        );
    }

    [Test]
    public void GetEventPool_ReturnsCorrectEventPool()
    {
        // Add events to pool.
        GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
        GameEvent event2 = new GameEvent { InstanceID = "EVENT2" };
        game.EventPool.Add(event1);
        game.EventPool.Add(event2);

        // Retrieve event pool and verify the count and contents.
        List<GameEvent> eventPool = game.GetEventPool();
        Assert.AreEqual(2, eventPool.Count, "Should return two events");
        Assert.Contains(event1, eventPool, "Should contain event1");
        Assert.Contains(event2, eventPool, "Should contain event2");
    }

    [Test]
    public void RemoveEvent_RemovesEventCorrectly()
    {
        // Add event and then remove it.
        GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
        game.EventPool.Add(event1);
        game.RemoveEvent(event1);

        // Verify removal from the event pool.
        Assert.IsFalse(
            game.EventPool.Contains(event1),
            "Event pool should not contain the removed event"
        );
    }

    [Test]
    public void GetEventByInstanceID_ReturnsCorrectEvent()
    {
        // Add event and retrieve it.
        GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
        game.EventPool.Add(event1);

        // Retrieve event and verify.
        GameEvent retrievedEvent = game.GetEventByInstanceID("EVENT1");
        Assert.AreEqual(event1, retrievedEvent, "Should return the correct event");
    }

    [Test]
    public void AddCompletedEvent_AddsEventToCompletedList()
    {
        // Add completed event to the completed list.
        GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
        game.AddCompletedEvent(event1);

        // Verify addition to the completed list.
        Assert.IsTrue(
            game.CompletedEventIDs.Contains(event1.InstanceID),
            "Completed event IDs should contain the added event's ID"
        );
    }

    [Test]
    public void IsEventComplete_ReturnsCorrectStatus()
    {
        // Add completed event to the completed list.
        GameEvent event1 = new GameEvent { InstanceID = "EVENT1" };
        game.AddCompletedEvent(event1);

        // Check completion status.
        Assert.IsTrue(game.IsEventComplete("EVENT1"), "EVENT1 should be marked as complete");
        Assert.IsFalse(game.IsEventComplete("EVENT2"), "EVENT2 should not be marked as complete");
    }

    [Test]
    public void Galaxy_Setter_InitializesGalaxyCorrectly()
    {
        // Set up galaxy structure.
        game.Galaxy = galaxyMap;
        game.AttachNode(planetSystem, galaxyMap);
        game.AttachNode(planet, planetSystem);
        game.AttachNode(fleet, planetSystem);

        // Verify galaxy initialization.
        Assert.IsTrue(
            game.NodesByInstanceID.ContainsKey(galaxyMap.InstanceID),
            "Game should contain galaxyMap in NodesByInstanceID"
        );
        Assert.IsTrue(
            game.NodesByInstanceID.ContainsKey(planetSystem.InstanceID),
            "Game should contain planetSystem in NodesByInstanceID"
        );
        Assert.IsTrue(
            game.NodesByInstanceID.ContainsKey(planet.InstanceID),
            "Game should contain planet in NodesByInstanceID"
        );
        Assert.IsTrue(
            game.NodesByInstanceID.ContainsKey(fleet.InstanceID),
            "Game should contain fleet in NodesByInstanceID"
        );

        Assert.AreEqual(
            galaxyMap,
            planetSystem.GetParent(),
            "PlanetSystem should have galaxyMap as parent"
        );
        Assert.AreEqual(
            planetSystem,
            planet.GetParent(),
            "Planet should have planetSystem as parent"
        );
        Assert.AreEqual(
            planetSystem,
            fleet.GetParent(),
            "Fleet should have planetSystem as parent"
        );

        Assert.IsTrue(
            game.GetFactionByOwnerInstanceID(planet.OwnerInstanceID)
                .GetAllOwnedNodes()
                .Contains(planet),
            "Faction should contain planet in owned units"
        );
        Assert.IsTrue(
            game.GetFactionByOwnerInstanceID(fleet.OwnerInstanceID)
                .GetAllOwnedNodes()
                .Contains(fleet),
            "Faction should contain fleet in owned units"
        );
    }
}
