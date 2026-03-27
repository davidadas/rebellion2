using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rebellion.Game;
using UnityEngine;

[TestFixture]
public class SaveGameManagerTests
{
    private string saveFileName = "SaveGameManagerTest";

    // Empty factions list - tests that need factions create them locally
    private List<Faction> factions = new List<Faction>();

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
    public void SaveGameData_SavesGameToFile()
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

        // Save the file to disk for testing.
        GameRoot game = new GameRoot
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
    public void LoadGameData_LoadsGameFromFile()
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
        GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
        string serializedShit = SerializationHelper.Serialize(game);
        SaveGameManager.Instance.SaveGameData(game, saveFileName);

        // Load the game from file.
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

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
    public void LoadGameData_ReconstitutesSceneGraph()
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
        GameRoot game = new GameRoot
        {
            Summary = summary,
            Factions = new List<Faction>(),
            Galaxy = galaxy,
        };

        // Create planets.
        Planet planet = new Planet { DisplayName = "Planet" };
        game.AttachNode(planet, planetSystem);

        // Create fleets.
        Fleet fleet = new Fleet();
        game.AttachNode(fleet, planet);

        // Create capital ships.
        CapitalShip capitalShip = new CapitalShip();
        game.AttachNode(capitalShip, fleet);

        // Create officers.
        Officer officer = new Officer();
        game.AttachNode(officer, capitalShip);

        // Save the game to disk.
        SaveGameManager.Instance.SaveGameData(game, saveFileName);

        // Load the game from file.
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

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

    [Test]
    public void SaveAndLoadGame_PreservesGameState()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Hard,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Abundant,
            PlayerFactionID = "FNALL1",
        };

        GameRoot game = new GameRoot
        {
            Summary = summary,
            CurrentTick = 150,
            GameSpeed = TickSpeed.Fast,
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.AreEqual(150, loadedGame.CurrentTick);
        Assert.AreEqual(TickSpeed.Fast, loadedGame.GameSpeed);
    }

    [Test]
    public void SaveAndLoadGame_PreservesEventPool()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        GameEvent event1 = new GameEvent { InstanceID = "EVENT1", DisplayName = "Event1" };
        GameEvent event2 = new GameEvent { InstanceID = "EVENT2", DisplayName = "Event2" };

        GameRoot game = new GameRoot
        {
            Summary = summary,
            EventPool = new List<GameEvent> { event1, event2 },
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.AreEqual(2, loadedGame.EventPool.Count);
        Assert.AreEqual("EVENT1", loadedGame.EventPool[0].InstanceID);
        Assert.AreEqual("EVENT2", loadedGame.EventPool[1].InstanceID);
    }

    [Test]
    public void SaveAndLoadGame_PreservesCompletedEventIDs()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        GameRoot game = new GameRoot
        {
            Summary = summary,
            CompletedEventIDs = new HashSet<string> { "EVENT1", "EVENT2", "EVENT3" },
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.AreEqual(3, loadedGame.CompletedEventIDs.Count);
        Assert.IsTrue(loadedGame.CompletedEventIDs.Contains("EVENT1"));
        Assert.IsTrue(loadedGame.CompletedEventIDs.Contains("EVENT2"));
        Assert.IsTrue(loadedGame.CompletedEventIDs.Contains("EVENT3"));
    }

    // TODO: Officer serialization needs investigation - officers have complex initialization requirements
    // [Test]
    // public void SaveAndLoadGame_PreservesUnrecruitedOfficers() { ... }

    // TODO: Fix FogState serialization issue with single faction
    //[Test]
    public void SaveAndLoadGame_PreservesFactions_DISABLED()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        Faction faction1 = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };

        GameRoot game = new GameRoot
        {
            Summary = summary,
            Factions = new List<Faction> { faction1 },
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.AreEqual(1, loadedGame.Factions.Count);
        Assert.AreEqual("FNALL1", loadedGame.Factions[0].InstanceID);
        Assert.AreEqual("Alliance", loadedGame.Factions[0].DisplayName);
    }

    [Test]
    public void SaveAndLoadGame_PreservesMetadata()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        GameMetadata metadata = new GameMetadata
        {
            SaveDisplayName = "Test Save",
            PlayerFactionID = "FNALL1",
            LastSavedUtc = new System.DateTime(2025, 1, 1, 12, 0, 0, System.DateTimeKind.Utc),
            Version = "1.0.0",
        };

        GameRoot game = new GameRoot
        {
            Summary = summary,
            Metadata = metadata,
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.IsNotNull(loadedGame.Metadata);
        Assert.AreEqual("Test Save", loadedGame.Metadata.SaveDisplayName);
        Assert.AreEqual("FNALL1", loadedGame.Metadata.PlayerFactionID);
        Assert.AreEqual(
            new System.DateTime(2025, 1, 1, 12, 0, 0, System.DateTimeKind.Utc),
            loadedGame.Metadata.LastSavedUtc
        );
        Assert.AreEqual("1.0.0", loadedGame.Metadata.Version);
    }

    [Test]
    public void SaveAndLoadGame_PreservesAllGameSummaryFields()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Large,
            Difficulty = GameDifficulty.Hard,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Limited,
            PlayerFactionID = "FNEMP1",
            StartingFactionIDs = new string[] { "FNALL1", "FNEMP1" },
            StartingResearchLevel = 3,
        };

        GameRoot game = new GameRoot
        {
            Summary = summary,
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.AreEqual(GameSize.Large, loadedGame.Summary.GalaxySize);
        Assert.AreEqual(GameDifficulty.Hard, loadedGame.Summary.Difficulty);
        Assert.AreEqual(GameVictoryCondition.Conquest, loadedGame.Summary.VictoryCondition);
        Assert.AreEqual(GameResourceAvailability.Limited, loadedGame.Summary.ResourceAvailability);
        Assert.AreEqual("FNEMP1", loadedGame.Summary.PlayerFactionID);
        Assert.AreEqual(2, loadedGame.Summary.StartingFactionIDs.Length);
        Assert.AreEqual("FNALL1", loadedGame.Summary.StartingFactionIDs[0]);
        Assert.AreEqual("FNEMP1", loadedGame.Summary.StartingFactionIDs[1]);
        Assert.AreEqual(3, loadedGame.Summary.StartingResearchLevel);
    }

    [Test]
    public void SaveAndLoadGame_PreservesEmptyCollections()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        GameRoot game = new GameRoot
        {
            Summary = summary,
            Factions = new List<Faction>(),
            EventPool = new List<GameEvent>(),
            CompletedEventIDs = new HashSet<string>(),
            UnrecruitedOfficers = new List<Officer>(),
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.IsNotNull(loadedGame.Factions);
        Assert.AreEqual(0, loadedGame.Factions.Count);
        Assert.IsNotNull(loadedGame.EventPool);
        Assert.AreEqual(0, loadedGame.EventPool.Count);
        Assert.IsNotNull(loadedGame.CompletedEventIDs);
        Assert.AreEqual(0, loadedGame.CompletedEventIDs.Count);
        Assert.IsNotNull(loadedGame.UnrecruitedOfficers);
        Assert.AreEqual(0, loadedGame.UnrecruitedOfficers.Count);
    }

    [Test]
    public void SaveAndLoadGame_PreservesMultipleEvents()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        List<GameEvent> events = new List<GameEvent>();
        for (int i = 0; i < 10; i++)
        {
            events.Add(new GameEvent { InstanceID = $"EVENT{i}", DisplayName = $"Event {i}" });
        }

        GameRoot game = new GameRoot
        {
            Summary = summary,
            EventPool = events,
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.AreEqual(10, loadedGame.EventPool.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.AreEqual($"EVENT{i}", loadedGame.EventPool[i].InstanceID);
            Assert.AreEqual($"Event {i}", loadedGame.EventPool[i].DisplayName);
        }
    }

    [Test]
    public void SaveAndLoadGame_PreservesLargeCompletedEventSet()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        HashSet<string> completedEvents = new HashSet<string>();
        for (int i = 0; i < 50; i++)
        {
            completedEvents.Add($"EVENT{i}");
        }

        GameRoot game = new GameRoot
        {
            Summary = summary,
            CompletedEventIDs = completedEvents,
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.AreEqual(50, loadedGame.CompletedEventIDs.Count);
        for (int i = 0; i < 50; i++)
        {
            Assert.IsTrue(loadedGame.CompletedEventIDs.Contains($"EVENT{i}"));
        }
    }

    [Test]
    public void SaveAndLoadGame_PreservesTickSpeedEnumValues()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        TickSpeed[] speeds = new TickSpeed[]
        {
            TickSpeed.Paused,
            TickSpeed.Slow,
            TickSpeed.Medium,
            TickSpeed.Fast,
        };

        foreach (TickSpeed speed in speeds)
        {
            GameRoot game = new GameRoot
            {
                Summary = summary,
                GameSpeed = speed,
                Factions = factions,
                Galaxy = new GalaxyMap(),
            };

            SaveGameManager.Instance.SaveGameData(game, saveFileName);
            GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

            Assert.AreEqual(speed, loadedGame.GameSpeed, $"GameSpeed {speed} was not preserved");
        }
    }

    [Test]
    public void SaveAndLoadGame_PreservesHighTickCount()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Headquarters,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        GameRoot game = new GameRoot
        {
            Summary = summary,
            CurrentTick = 999999,
            Factions = factions,
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Assert.AreEqual(999999, loadedGame.CurrentTick);
    }

    [Test]
    public void SaveAndLoadGame_PreservesFogState_WithSnapshots()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };

        alliance.Fog.Snapshots["SYS1"] = new SystemSnapshot
        {
            Planets = new Dictionary<string, PlanetSnapshot>
            {
                {
                    "CORUSCANT",
                    new PlanetSnapshot
                    {
                        TickCaptured = 100,
                        OwnerInstanceID = "FNEMP1",
                        PopularSupport = new Dictionary<string, int>
                        {
                            { "FNEMP1", 90 },
                            { "FNALL1", 10 },
                        },
                    }
                },
            },
        };

        alliance.Fog.PlanetToSystem["CORUSCANT"] = "SYS1";

        GameRoot game = new GameRoot
        {
            Summary = summary,
            Factions = new List<Faction> { alliance },
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Faction loadedAlliance = loadedGame.Factions.Find(f => f.InstanceID == "FNALL1");
        Assert.IsNotNull(loadedAlliance);

        Assert.AreEqual(1, loadedAlliance.Fog.Snapshots.Count);
        Assert.IsTrue(loadedAlliance.Fog.Snapshots.ContainsKey("SYS1"));

        SystemSnapshot loadedSystemSnapshot = loadedAlliance.Fog.Snapshots["SYS1"];
        Assert.AreEqual(1, loadedSystemSnapshot.Planets.Count);

        PlanetSnapshot loadedPlanetSnapshot = loadedSystemSnapshot.Planets["CORUSCANT"];
        Assert.AreEqual(100, loadedPlanetSnapshot.TickCaptured);
        Assert.AreEqual("FNEMP1", loadedPlanetSnapshot.OwnerInstanceID);
        Assert.AreEqual(2, loadedPlanetSnapshot.PopularSupport.Count);
        Assert.AreEqual(90, loadedPlanetSnapshot.PopularSupport["FNEMP1"]);
        Assert.AreEqual(10, loadedPlanetSnapshot.PopularSupport["FNALL1"]);

        Assert.AreEqual(1, loadedAlliance.Fog.PlanetToSystem.Count);
        Assert.AreEqual("SYS1", loadedAlliance.Fog.PlanetToSystem["CORUSCANT"]);
    }

    // TODO: Fix FogState serialization issue
    //[Test]
    public void SaveAndLoadGame_PreservesFogState_Empty_DISABLED()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };

        GameRoot game = new GameRoot
        {
            Summary = summary,
            Factions = new List<Faction> { alliance },
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Faction loadedAlliance = loadedGame.Factions.Find(f => f.InstanceID == "FNALL1");
        Assert.IsNotNull(loadedAlliance);
        Assert.IsNotNull(loadedAlliance.Fog);
        Assert.AreEqual(0, loadedAlliance.Fog.Snapshots.Count);
        Assert.AreEqual(0, loadedAlliance.Fog.EntityLastSeenAt.Count);
        Assert.AreEqual(0, loadedAlliance.Fog.PlanetToSystem.Count);
    }

    [Test]
    public void SaveAndLoadGame_PreservesFogState_WithEntityTracking()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };

        alliance.Fog.Snapshots["SYS1"] = new SystemSnapshot
        {
            Planets = new Dictionary<string, PlanetSnapshot>
            {
                {
                    "PLANET1",
                    new PlanetSnapshot { TickCaptured = 50, OwnerInstanceID = "FNEMP1" }
                },
            },
        };

        alliance.Fog.EntityLastSeenAt["OFF1"] = "PLANET1";
        alliance.Fog.EntityLastSeenAt["FLEET1"] = "PLANET1";
        alliance.Fog.EntityLastSeenAt["REG1"] = "PLANET1";
        alliance.Fog.PlanetToSystem["PLANET1"] = "SYS1";

        GameRoot game = new GameRoot
        {
            Summary = summary,
            Factions = new List<Faction> { alliance },
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Faction loadedAlliance = loadedGame.Factions.Find(f => f.InstanceID == "FNALL1");
        PlanetSnapshot loadedSnapshot = loadedAlliance.Fog.Snapshots["SYS1"].Planets["PLANET1"];

        Assert.AreEqual(50, loadedSnapshot.TickCaptured);
        Assert.AreEqual("FNEMP1", loadedSnapshot.OwnerInstanceID);
        Assert.AreEqual(3, loadedAlliance.Fog.EntityLastSeenAt.Count);
        Assert.AreEqual("PLANET1", loadedAlliance.Fog.EntityLastSeenAt["OFF1"]);
        Assert.AreEqual("PLANET1", loadedAlliance.Fog.EntityLastSeenAt["FLEET1"]);
        Assert.AreEqual("PLANET1", loadedAlliance.Fog.EntityLastSeenAt["REG1"]);
    }

    [Test]
    public void SaveAndLoadGame_PreservesFogState_MultipleSystemSnapshots()
    {
        GameSummary summary = new GameSummary
        {
            GalaxySize = GameSize.Medium,
            Difficulty = GameDifficulty.Medium,
            VictoryCondition = GameVictoryCondition.Conquest,
            ResourceAvailability = GameResourceAvailability.Normal,
            PlayerFactionID = "FNALL1",
        };

        Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };

        alliance.Fog.Snapshots["SYS1"] = new SystemSnapshot
        {
            Planets = new Dictionary<string, PlanetSnapshot>
            {
                {
                    "PLANET1",
                    new PlanetSnapshot { TickCaptured = 10, OwnerInstanceID = "FNEMP1" }
                },
            },
        };

        alliance.Fog.Snapshots["SYS2"] = new SystemSnapshot
        {
            Planets = new Dictionary<string, PlanetSnapshot>
            {
                {
                    "PLANET2",
                    new PlanetSnapshot { TickCaptured = 20, OwnerInstanceID = "FNEMP1" }
                },
                {
                    "PLANET3",
                    new PlanetSnapshot { TickCaptured = 30, OwnerInstanceID = "FNALL1" }
                },
            },
        };

        alliance.Fog.PlanetToSystem["PLANET1"] = "SYS1";
        alliance.Fog.PlanetToSystem["PLANET2"] = "SYS2";
        alliance.Fog.PlanetToSystem["PLANET3"] = "SYS2";

        GameRoot game = new GameRoot
        {
            Summary = summary,
            Factions = new List<Faction> { alliance },
            Galaxy = new GalaxyMap(),
        };

        SaveGameManager.Instance.SaveGameData(game, saveFileName);
        GameRoot loadedGame = SaveGameManager.Instance.LoadGameData(saveFileName);

        Faction loadedAlliance = loadedGame.Factions.Find(f => f.InstanceID == "FNALL1");

        Assert.AreEqual(2, loadedAlliance.Fog.Snapshots.Count);
        Assert.AreEqual(1, loadedAlliance.Fog.Snapshots["SYS1"].Planets.Count);
        Assert.AreEqual(2, loadedAlliance.Fog.Snapshots["SYS2"].Planets.Count);
        Assert.AreEqual(10, loadedAlliance.Fog.Snapshots["SYS1"].Planets["PLANET1"].TickCaptured);
        Assert.AreEqual(20, loadedAlliance.Fog.Snapshots["SYS2"].Planets["PLANET2"].TickCaptured);
        Assert.AreEqual(30, loadedAlliance.Fog.Snapshots["SYS2"].Planets["PLANET3"].TickCaptured);

        Assert.AreEqual(3, loadedAlliance.Fog.PlanetToSystem.Count);
        Assert.AreEqual("SYS1", loadedAlliance.Fog.PlanetToSystem["PLANET1"]);
        Assert.AreEqual("SYS2", loadedAlliance.Fog.PlanetToSystem["PLANET2"]);
        Assert.AreEqual("SYS2", loadedAlliance.Fog.PlanetToSystem["PLANET3"]);
    }
}
