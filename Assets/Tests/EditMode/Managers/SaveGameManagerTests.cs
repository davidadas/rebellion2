using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class SaveGameManagerTests
{
    private string saveFileName = "SaveGameManagerTest";
    private List<Faction> factions = new List<Faction>
    {
        new Faction { InstanceID = "FNALL1" },
        new Faction { InstanceID = "FNEMP1" },
    };

    [TearDown]
    public void Teardown()
    {
        // Cleanup code after each test.
        string filePath = SaveGameManager.Instance.GetSaveFilePath(saveFileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    [Test]
    public void TestSaveGame()
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

        // Save the file to disk for testing
        Game game = new Game
        {
            Summary = summary,
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };
        SaveGameManager.Instance.SaveGameData(game, saveFileName);

        // Check if the file was created.
        string filePath = SaveGameManager.Instance.GetSaveFilePath(saveFileName);
        bool fileExists = File.Exists(filePath);
        Assert.IsTrue(fileExists, "Save file was not created.");
    }

    [Test]
    public void TestLoadGame()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        // Save the game to disk.
        Game game = new Game { Summary = summary, Galaxy = new GalaxyMap() };
        string serializedShit = SerializationHelper.Serialize(game);
        SaveGameManager.Instance.SaveGameData(game, saveFileName);

        // Load the game from file.
        Game loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        // Assert that the loaded game's summary matches the original summary.
        Assert.AreEqual(
            loadedGame.Summary.GalaxySize,
            summary.GalaxySize,
            "Galaxy size does not match."
        );
        Assert.AreEqual(
            loadedGame.Summary.Difficulty,
            summary.Difficulty,
            "Difficulty does not match."
        );
        Assert.AreEqual(
            loadedGame.Summary.VictoryCondition,
            summary.VictoryCondition,
            "Victory condition does not match."
        );
        Assert.AreEqual(
            loadedGame.Summary.ResourceAvailability,
            summary.ResourceAvailability,
            "Resource availability does not match."
        );
        Assert.AreEqual(
            loadedGame.Summary.PlayerFactionID,
            summary.PlayerFactionID,
            "Player faction ID does not match."
        );
    }

    [Test]
    public void TestBasicSceneGraphLoaded()
    {
        // Create planet systems.
        PlanetSystem planetSystem = new PlanetSystem { DisplayName = "Planet System" };
        List<PlanetSystem> planetSystems = new List<PlanetSystem> { planetSystem };

        // Create galaxy map.
        GalaxyMap galaxy = new GalaxyMap { PlanetSystems = planetSystems };

        // Generate the game summary.
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Easy,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        // Create the game.
        Game game = new Game
        {
            Summary = summary,
            Factions = factions,
            Galaxy = galaxy,
        };

        // Create planets.
        Planet planet = new Planet { DisplayName = "Planet", OwnerInstanceID = "FNALL1" };
        // planetSystem.Planets.Add(planet);
        game.AttachNode(planet, planetSystem);

        // Create fleets.
        Fleet fleet = new Fleet { OwnerInstanceID = "FNALL1" };
        game.AttachNode(fleet, planet);

        // Create capital ships.
        CapitalShip capitalShip = new CapitalShip { OwnerInstanceID = "FNALL1" };
        game.AttachNode(capitalShip, fleet);

        // Create officers.
        Officer officer = new Officer { OwnerInstanceID = "FNALL1" };
        game.AttachNode(officer, capitalShip);

        // Save the game to disk.
        SaveGameManager.Instance.SaveGameData(game, saveFileName);

        // Load the game from file.
        Game loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        // // Verify the scene graph is reconstituted.
        PlanetSystem loadedPlanetSystem = loadedGame.Galaxy.PlanetSystems[0];
        Planet loadedPlanet = loadedPlanetSystem.Planets[0];
        Fleet loadedFleet = loadedPlanet.Fleets[0];
        CapitalShip loadedCapitalShip = loadedFleet.CapitalShips[0];
        Officer loadedOfficer = loadedCapitalShip.Officers[0];

        // Assert that children are correctly parented.
        Assert.AreEqual(planetSystem.InstanceID, loadedPlanet.GetParent().InstanceID);
        Assert.AreEqual(fleet.InstanceID, loadedCapitalShip.GetParent().InstanceID);
        Assert.AreEqual(capitalShip.InstanceID, loadedOfficer.GetParent().InstanceID);
    }
}
