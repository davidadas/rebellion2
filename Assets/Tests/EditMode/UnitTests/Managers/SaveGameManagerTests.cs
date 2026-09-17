using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Galaxy;
using Rebellion.Game.UIState;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public class SaveGameManagerTests
    {
        private const string _defaultSaveFileName = "SaveGameManagerTest";

        private string _saveDirectoryPath;
        private string _saveFileName;
        private SaveGameManager _saveGameManager;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _saveDirectoryPath = Path.Combine(
                Path.GetTempPath(),
                nameof(SaveGameManagerTests),
                Guid.NewGuid().ToString("N")
            );
            _saveFileName = _defaultSaveFileName;
            _saveGameManager = new SaveGameManager(_saveDirectoryPath);
        }

        /// <summary>
        /// Executes teardown.
        /// </summary>
        [TearDown]
        public void Teardown()
        {
            // Cleanup code after each test.
            if (Directory.Exists(_saveDirectoryPath))
                Directory.Delete(_saveDirectoryPath, true);
        }

        /// <summary>
        /// Verifies save game data valid game state saves to file.
        /// </summary>
        [Test]
        public void SaveGameData_ValidGameState_SavesToFile()
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
            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            _saveGameManager.SaveGameData(game, _saveFileName);

            // Check if the file was created.
            string filePath = _saveGameManager.GetSaveFilePath(_saveFileName);
            bool fileExists = File.Exists(filePath);
            Assert.IsTrue(fileExists, "Save file was not created.");
        }

        /// <summary>
        /// Verifies listeners can synchronize state immediately before serialization.
        /// </summary>
        [Test]
        public void SaveGameData_SavingListenerMutatesGame_SerializesUpdatedState()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary(), Galaxy = new GalaxyMap() };
            _saveGameManager.Saving += () => game.CurrentTick = 42;

            _saveGameManager.SaveGameData(game, _saveFileName);

            GameRoot loaded = _saveGameManager.LoadGameData(_saveFileName);
            Assert.AreEqual(42, loaded.CurrentTick);
        }

        /// <summary>
        /// Verifies saving a game serializes its player records.
        /// </summary>
        [Test]
        public void SaveGameData_GameWithPlayers_SerializesPlayers()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary(), Galaxy = new GalaxyMap() };
            game.SetFactionController("FNALL1", "PLAYER1", PlayerControllerType.Human);

            _saveGameManager.SaveGameData(game, _saveFileName);

            StringAssert.Contains(
                "<Players>",
                File.ReadAllText(_saveGameManager.GetSaveFilePath(_saveFileName))
            );
        }

        /// <summary>
        /// Verifies that loading saved player records restores them.
        /// </summary>
        [Test]
        public void LoadGameData_SavedPlayers_RestoresPlayers()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary(), Galaxy = new GalaxyMap() };
            game.SetFactionController("FNALL1", "PLAYER1", PlayerControllerType.Human);
            _saveGameManager.SaveGameData(game, _saveFileName);

            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Player player = loadedGame.GetFactionPlayer("FNALL1");
            Assert.AreEqual("PLAYER1", player.PlayerID);
            Assert.AreEqual(PlayerControllerType.Human, player.ControllerType);
        }

        /// <summary>
        /// Verifies that replacing a save is atomic and leaves no temporary files.
        /// </summary>
        [Test]
        public void LoadGameData_SavedStrategyWindows_RestoresWindowState()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary(), Galaxy = new GalaxyMap() };
            game.SetFactionController("FNALL1", "PLAYER1", PlayerControllerType.Human);
            game.GetFactionPlayer("FNALL1")
                .UIState.GetOrCreateSection("Strategy")
                .Windows.Add(new WindowState("Planet.Fleet", "PLANET1", 123, 45, 0, 0, 2));
            _saveGameManager.SaveGameData(game, _saveFileName);

            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            WindowState state = loadedGame
                .GetFactionPlayer("FNALL1")
                .UIState.GetOrCreateSection("Strategy")
                .Windows.Single();
            Assert.AreEqual("Planet.Fleet", state.GetWindowTypeID());
            Assert.AreEqual("PLANET1", state.GetTargetInstanceID());
            Assert.AreEqual(123, state.GetX());
            Assert.AreEqual(45, state.GetY());
            Assert.AreEqual(2, state.GetZOrder());
        }

        /// <summary>
        /// Verifies replacing an existing save leaves the updated save without temporary files.
        /// </summary>
        [Test]
        public void SaveGameData_ExistingSave_AtomicallyReplacesWithoutTemporaryFiles()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary(),
                Galaxy = new GalaxyMap(),
                CurrentTick = 10,
            };
            _saveGameManager.SaveGameData(game, _saveFileName);

            game.CurrentTick = 20;
            _saveGameManager.SaveGameData(game, _saveFileName);

            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);
            Assert.AreEqual(20, loadedGame.CurrentTick);
            CollectionAssert.IsEmpty(Directory.GetFiles(_saveDirectoryPath, "*.tmp"));
        }

        /// <summary>
        /// Verifies saving truncates an overlong display name at the domain boundary.
        /// </summary>
        [Test]
        public void SaveGameData_OverlongDisplayName_TruncatesStoredName()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary(), Galaxy = new GalaxyMap() };
            string overlongName = new string('N', SaveGameManager.MaxDisplayNameLength + 10);

            _saveGameManager.SaveGameData(game, _saveFileName, overlongName);

            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);
            Assert.AreEqual(
                SaveGameManager.MaxDisplayNameLength,
                loadedGame.Metadata.SaveDisplayName.Length
            );
        }

        /// <summary>
        /// Verifies save game data game with summary stores player faction in metadata.
        /// </summary>
        [Test]
        public void SaveGameData_GameWithSummary_StoresPlayerFactionInMetadata()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNEMP1",
            };

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual("FNEMP1", loadedGame.Metadata.PlayerFactionID);
        }

        /// <summary>
        /// Verifies save game data quick save file name stores quicksave display name.
        /// </summary>
        [Test]
        public void SaveGameData_QuickSaveFileName_StoresQuicksaveDisplayName()
        {
            _saveFileName = SaveGameManager.QuickSaveFileName;
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
            };

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual("Quicksave", loadedGame.Metadata.SaveDisplayName);
        }

        /// <summary>
        /// Verifies save game data new save stamps current schema version.
        /// </summary>
        [Test]
        public void SaveGameData_NewSave_StampsCurrentSchemaVersion()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
            };

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(GameMetadata.CurrentSaveVersion, loadedGame.Metadata.SaveVersion);
        }

        /// <summary>
        /// Verifies save game data metadata with existing version overwrites with current schema version.
        /// </summary>
        [Test]
        public void SaveGameData_MetadataWithExistingVersion_OverwritesWithCurrentSchemaVersion()
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
                Metadata = new GameMetadata { SaveVersion = GameMetadata.CurrentSaveVersion + 99 },
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(GameMetadata.CurrentSaveVersion, loadedGame.Metadata.SaveVersion);
        }

        /// <summary>
        /// Verifies process autosave tick exceeds retention prunes oldest restore points.
        /// </summary>
        [Test]
        public void ProcessAutosaveTick_ExceedsRetention_PrunesOldestRestorePoints()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary() };
            UserGameplaySettings settings = new UserGameplaySettings();
            settings.SetAutosavesToKeep(2);
            for (int tick = 100; tick <= 400; tick += 100)
            {
                game.CurrentTick = tick;
                Assert.IsTrue(_saveGameManager.ProcessAutosaveTick(game, settings));
            }

            string[] autosaves = Directory.GetFiles(
                _saveDirectoryPath,
                $"{SaveGameManager.AutosaveFilePrefix}*.sav"
            );

            Assert.AreEqual(2, autosaves.Length);
            Assert.IsTrue(
                File.Exists(
                    _saveGameManager.GetSaveFilePath(
                        SaveGameManager.AutosaveFilePrefix + "0000000300"
                    )
                )
            );
            Assert.IsTrue(
                File.Exists(
                    _saveGameManager.GetSaveFilePath(
                        SaveGameManager.AutosaveFilePrefix + "0000000400"
                    )
                )
            );
        }

        /// <summary>
        /// Verifies process autosave tick only writes at configured tick cadence.
        /// </summary>
        [Test]
        public void ProcessAutosaveTick_OnlyWritesAtConfiguredTickCadence()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary(), CurrentTick = 99 };
            UserGameplaySettings settings = new UserGameplaySettings();

            Assert.IsFalse(_saveGameManager.ProcessAutosaveTick(game, settings));
            Assert.IsFalse(Directory.Exists(_saveDirectoryPath));

            game.CurrentTick = 100;

            Assert.IsTrue(_saveGameManager.ProcessAutosaveTick(game, settings));
            Assert.AreEqual(
                1,
                Directory
                    .GetFiles(_saveDirectoryPath, $"{SaveGameManager.AutosaveFilePrefix}*.sav")
                    .Length
            );
        }

        /// <summary>
        /// Verifies save slot count default configuration returns six.
        /// </summary>
        [Test]
        public void SaveSlotCount_DefaultConfiguration_ReturnsSix()
        {
            int count = _saveGameManager.SaveSlotCount;

            Assert.AreEqual(6, count);
        }

        /// <summary>
        /// Verifies is valid save slot boundary values validates bounds.
        /// </summary>
        [Test]
        public void IsValidSaveSlot_BoundaryValues_ValidatesBounds()
        {
            Assert.IsFalse(_saveGameManager.IsValidSaveSlot(-1));
            Assert.IsTrue(_saveGameManager.IsValidSaveSlot(0));
            Assert.IsTrue(_saveGameManager.IsValidSaveSlot(5));
            Assert.IsFalse(_saveGameManager.IsValidSaveSlot(6));
        }

        /// <summary>
        /// Verifies get save slot file name valid slots returns canonical names.
        /// </summary>
        [Test]
        public void GetSaveSlotFileName_ValidSlots_ReturnsCanonicalNames()
        {
            Assert.AreEqual("save_slot_1", _saveGameManager.GetSaveSlotFileName(0));
            Assert.AreEqual("save_slot_6", _saveGameManager.GetSaveSlotFileName(5));
        }

        /// <summary>
        /// Verifies get save slot file name invalid slot throws argument out of range exception.
        /// </summary>
        [Test]
        public void GetSaveSlotFileName_InvalidSlot_ThrowsArgumentOutOfRangeException()
        {
            TestDelegate getName = () => _saveGameManager.GetSaveSlotFileName(-1);

            Assert.Throws<ArgumentOutOfRangeException>(getName);
        }

        /// <summary>
        /// Verifies get save slot display name valid slots returns canonical names.
        /// </summary>
        [Test]
        public void GetSaveSlotDisplayName_ValidSlots_ReturnsCanonicalNames()
        {
            Assert.AreEqual("Save Slot 1", _saveGameManager.GetSaveSlotDisplayName(0));
            Assert.AreEqual("Save Slot 6", _saveGameManager.GetSaveSlotDisplayName(5));
        }

        /// <summary>
        /// Verifies get save slot display name invalid slot throws argument out of range exception.
        /// </summary>
        [Test]
        public void GetSaveSlotDisplayName_InvalidSlot_ThrowsArgumentOutOfRangeException()
        {
            TestDelegate getName = () => _saveGameManager.GetSaveSlotDisplayName(6);

            Assert.Throws<ArgumentOutOfRangeException>(getName);
        }

        /// <summary>
        /// Verifies save slot game data custom display name stores display name.
        /// </summary>
        [Test]
        public void SaveSlotGameData_CustomDisplayName_StoresDisplayName()
        {
            SaveGameManager manager = _saveGameManager;
            _saveFileName = manager.GetSaveSlotFileName(0);
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { PlayerFactionID = "FNALL1" },
                Galaxy = new GalaxyMap(),
            };

            manager.SaveSlotGameData(game, 0, "Coruscant Campaign");
            GameRoot loadedGame = manager.LoadGameData(_saveFileName);

            Assert.AreEqual("Coruscant Campaign", loadedGame.Metadata.SaveDisplayName);
        }

        /// <summary>
        /// Verifies save slot game data valid game writes metadata sidecar.
        /// </summary>
        [Test]
        public void SaveSlotGameData_ValidGame_WritesMetadataSidecar()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { PlayerFactionID = "FNALL1" },
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveSlotGameData(game, 0, "Core Worlds");

            string fileName = _saveGameManager.GetSaveSlotFileName(0);
            Assert.IsTrue(File.Exists(_saveGameManager.GetSaveMetadataFilePath(fileName)));
            SaveGameEntry entry = _saveGameManager.GetSaveSlotEntries().Single();
            Assert.AreEqual(fileName, entry.FileName);
            Assert.AreEqual("Core Worlds", entry.Metadata.SaveDisplayName);
            Assert.AreEqual("FNALL1", entry.Metadata.PlayerFactionID);
        }

        /// <summary>
        /// Verifies save display names use the product's 64-character limit.
        /// </summary>
        [Test]
        public void MaxDisplayNameLength_Is64Characters()
        {
            Assert.AreEqual(64, SaveGameManager.MaxDisplayNameLength);
        }

        /// <summary>
        /// Verifies renaming truncates an overlong display name before metadata is persisted.
        /// </summary>
        [Test]
        public void SetSaveDisplayName_OverlongName_TruncatesMetadataName()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary(), Galaxy = new GalaxyMap() };
            _saveGameManager.SaveGameData(game, _saveFileName, "Initial Name");
            string overlongName = new string('N', SaveGameManager.MaxDisplayNameLength + 10);

            _saveGameManager.SetSaveDisplayName(_saveFileName, overlongName);

            SaveGameEntry entry = _saveGameManager.GetSavedGames().Single();
            Assert.AreEqual(
                SaveGameManager.MaxDisplayNameLength,
                entry.Metadata.SaveDisplayName.Length
            );
        }

        /// <summary>
        /// Verifies a missing derived metadata sidecar is rebuilt from its canonical save.
        /// </summary>
        [Test]
        public void GetSaveSlotEntries_SaveWithoutSidecar_RecreatesSidecar()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { PlayerFactionID = "FNALL1" },
                Galaxy = new GalaxyMap(),
            };
            _saveGameManager.SaveSlotGameData(game, 1, "Recovered Slot");
            string fileName = _saveGameManager.GetSaveSlotFileName(1);
            string metadataPath = _saveGameManager.GetSaveMetadataFilePath(fileName);
            File.Delete(metadataPath);

            SaveGameEntry entry = _saveGameManager.GetSaveSlotEntries().Single();

            Assert.AreEqual("Recovered Slot", entry.Metadata.SaveDisplayName);
            Assert.IsTrue(File.Exists(metadataPath));
        }

        /// <summary>
        /// Verifies get save slot entries unrelated save ignores save.
        /// </summary>
        [Test]
        public void GetSaveSlotEntries_UnrelatedSave_IgnoresSave()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { PlayerFactionID = "FNALL1" },
                Galaxy = new GalaxyMap(),
            };
            _saveGameManager.SaveGameData(game, "simulation_seed_1");

            Assert.IsEmpty(_saveGameManager.GetSaveSlotEntries());
        }

        /// <summary>
        /// Verifies get save slot entries corrupt sidecar reads main save.
        /// </summary>
        [Test]
        public void GetSaveSlotEntries_CorruptSidecar_ReadsMainSave()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { PlayerFactionID = "FNALL1" },
                Galaxy = new GalaxyMap(),
            };
            _saveGameManager.SaveSlotGameData(game, 2, "Recovered Slot");
            string fileName = _saveGameManager.GetSaveSlotFileName(2);
            string metadataPath = _saveGameManager.GetSaveMetadataFilePath(fileName);
            File.WriteAllText(metadataPath, "<Metadata>");
            File.SetLastWriteTimeUtc(metadataPath, DateTime.UtcNow.AddMinutes(1));

            SaveGameEntry entry = _saveGameManager.GetSaveSlotEntries().Single();

            Assert.AreEqual("Recovered Slot", entry.Metadata.SaveDisplayName);
        }

        /// <summary>
        /// Verifies load game data valid saved file loads game.
        /// </summary>
        [Test]
        public void LoadGameData_ValidSavedFile_LoadsGame()
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
            _saveGameManager.SaveGameData(game, _saveFileName);

            // Load the game from file.
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

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

        /// <summary>
        /// Verifies load game data valid saved game reconstitutes scene graph.
        /// </summary>
        [Test]
        public void LoadGameData_ValidSavedGame_ReconstitutesSceneGraph()
        {
            // Create planet sectors.
            PlanetSector planetSector = new PlanetSector { DisplayName = "Planet Sector" };
            List<PlanetSector> planetSectors = new List<PlanetSector> { planetSector };

            // Create galaxy map.
            GalaxyMap galaxy = new GalaxyMap();
            galaxy.AddChildren(planetSectors);

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
            GameRoot game = new GameRoot { Summary = summary, Galaxy = galaxy };

            // Create planets.
            Planet planet = new Planet { DisplayName = "Planet" };
            game.AttachNode(planet, planetSector);

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
            _saveGameManager.SaveGameData(game, _saveFileName);

            // Load the game from file.
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            // // Verify the scene graph is reconstituted.
            PlanetSector loadedPlanetSector = loadedGame.Galaxy.GetChildren<PlanetSector>()[0];
            Planet loadedPlanet = loadedPlanetSector.GetChildren<Planet>()[0];
            Fleet loadedFleet = loadedPlanet.GetChildren<Fleet>()[0];
            CapitalShip loadedCapitalShip = loadedFleet.GetChildren<CapitalShip>()[0];
            Officer loadedOfficer = loadedCapitalShip.GetChildren<Officer>()[0];

            Assert.AreEqual(planetSector.InstanceID, loadedPlanet.GetParent().InstanceID);
            Assert.AreEqual(fleet.InstanceID, loadedCapitalShip.GetParent().InstanceID);
            Assert.AreEqual(capitalShip.InstanceID, loadedOfficer.GetParent().InstanceID);
        }

        /// <summary>
        /// Verifies load game data save version mismatch loads without version gate.
        /// </summary>
        [Test]
        public void LoadGameData_SaveVersionMismatch_LoadsWithoutVersionGate()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
            };

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            _saveGameManager.SaveGameData(game, _saveFileName);

            string saveFilePath = _saveGameManager.GetSaveFilePath(_saveFileName);
            string xml = File.ReadAllText(saveFilePath);
            int futureVersion = GameMetadata.CurrentSaveVersion + 99;
            string bumped = xml.Replace(
                $"<SaveVersion>{GameMetadata.CurrentSaveVersion}</SaveVersion>",
                $"<SaveVersion>{futureVersion}</SaveVersion>"
            );
            File.WriteAllText(saveFilePath, bumped);

            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(futureVersion, loadedGame.Metadata.SaveVersion);
        }

        /// <summary>
        /// Verifies load game data save version missing defaults version to zero.
        /// </summary>
        [Test]
        public void LoadGameData_SaveVersionMissing_DefaultsVersionToZero()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
            };

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            _saveGameManager.SaveGameData(game, _saveFileName);

            string saveFilePath = _saveGameManager.GetSaveFilePath(_saveFileName);
            string xml = File.ReadAllText(saveFilePath);
            string stripped = System.Text.RegularExpressions.Regex.Replace(
                xml,
                @"\s*<SaveVersion>\d+</SaveVersion>",
                string.Empty
            );
            File.WriteAllText(saveFilePath, stripped);

            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(0, loadedGame.Metadata.SaveVersion);
        }

        /// <summary>
        /// Verifies load game data unknown element skips element.
        /// </summary>
        [Test]
        public void LoadGameData_UnknownElement_SkipsElement()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary(),
                Galaxy = new GalaxyMap(),
                CurrentTick = 20,
            };
            _saveGameManager.SaveGameData(game, _saveFileName);
            string savePath = _saveGameManager.GetSaveFilePath(_saveFileName);
            string xml = File.ReadAllText(savePath)
                .Replace(
                    "<GameRoot>",
                    "<GameRoot><FutureField><Nested>future value</Nested></FutureField>"
                );
            File.WriteAllText(savePath, xml);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(20, loadedGame.CurrentTick);
        }

        /// <summary>
        /// Verifies save and load game game with faction and planet preserves game state.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithFactionAndPlanet_PreservesGameState()
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
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(150, loadedGame.CurrentTick);
            Assert.AreEqual(TickSpeed.Fast, loadedGame.GameSpeed);
        }

        /// <summary>
        /// Verifies save and load game game with event pool preserves event pool.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithEventPool_PreservesEventPool()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
            };

            GameEvent event1 = new GameEvent
            {
                InstanceID = "EVENT1",
                Schedule = new GameEventScheduler
                {
                    RandomInterval = new RandomInterval { MinimumTicks = 300, MaximumTicks = 400 },
                },
            };
            GameEvent event2 = new GameEvent { InstanceID = "EVENT2" };

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            game.GetEventPool().AddRange(new[] { event1, event2 });

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(2, loadedGame.GetEventPool().Count);
            Assert.AreEqual("EVENT1", loadedGame.GetEventPool()[0].InstanceID);
            Assert.AreEqual("EVENT2", loadedGame.GetEventPool()[1].InstanceID);
            Assert.AreEqual(300, loadedGame.GetEventPool()[0].Schedule.RandomInterval.MinimumTicks);
            Assert.AreEqual(400, loadedGame.GetEventPool()[0].Schedule.RandomInterval.MaximumTicks);
        }

        /// <summary>
        /// Verifies save and load game game with completed events preserves event states.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithCompletedEvents_PreservesEventStates()
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
                EventRuntime = new GameEventRuntimeState
                {
                    States = new Dictionary<string, GameEventState>
                    {
                        ["EVENT1"] = new GameEventState { IsComplete = true },
                        ["EVENT2"] = new GameEventState { IsComplete = true },
                        ["EVENT3"] = new GameEventState { IsComplete = true },
                    },
                },
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(3, loadedGame.EventRuntime.States.Count);
            Assert.IsTrue(loadedGame.EventRuntime.States["EVENT1"].IsComplete);
            Assert.IsTrue(loadedGame.EventRuntime.States["EVENT2"].IsComplete);
            Assert.IsTrue(loadedGame.EventRuntime.States["EVENT3"].IsComplete);
        }

        /// <summary>
        /// Verifies save and load game game with event state preserves schedule and history.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithEventState_PreservesScheduleAndHistory()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { PlayerFactionID = "FNALL1" },
                EventRuntime = new GameEventRuntimeState
                {
                    States = new Dictionary<string, GameEventState>
                    {
                        {
                            "STORY_EVENT",
                            new GameEventState
                            {
                                IsInitialized = true,
                                NextEligibleTick = 412,
                                ActivationCount = 3,
                                LastActivationTick = 400,
                            }
                        },
                    },
                },
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            GameEventState state = loadedGame.EventRuntime.States["STORY_EVENT"];
            Assert.IsTrue(state.IsInitialized);
            Assert.AreEqual(412, state.NextEligibleTick);
            Assert.AreEqual(3, state.ActivationCount);
            Assert.AreEqual(400, state.LastActivationTick);
        }

        /// <summary>
        /// Verifies save and load game game with event variables preserves story state.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithEventVariables_PreservesStoryState()
        {
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { PlayerFactionID = "FNALL1" },
                EventRuntime = new GameEventRuntimeState
                {
                    Variables = new Dictionary<string, int>
                    {
                        { "luke.dagobah.stage", 8 },
                        { "luke.heritage.revealed", 1 },
                    },
                },
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(8, loadedGame.EventRuntime.GetVariable("luke.dagobah.stage"));
            Assert.AreEqual(1, loadedGame.EventRuntime.GetVariable("luke.heritage.revealed"));
        }

        // TODO: Officer serialization needs investigation - officers have complex initialization requirements
        // [Test]
        // public void SaveAndLoadGame_PreservesUnrecruitedOfficers() { ... }

        /// <summary>
        /// Verifies save and load game game with factions preserves factions.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithFactions_PreservesFactions()
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

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            game.GetFactions().Add(faction1);

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(1, loadedGame.GetFactions().Count);
            Assert.AreEqual("FNALL1", loadedGame.GetFactions()[0].InstanceID);
            Assert.AreEqual("Alliance", loadedGame.GetFactions()[0].DisplayName);
        }

        /// <summary>
        /// Verifies save and load game game with metadata preserves metadata.
        /// </summary>
        [Test]
        public void SaveGameData_PlayerWithUIState_WritesUIState()
        {
            GameRoot game = BuildGameWithIgnoredItems();

            _saveGameManager.SaveGameData(game, _saveFileName);
            string xml = File.ReadAllText(_saveGameManager.GetSaveFilePath(_saveFileName));

            StringAssert.Contains("<UIStateSections>", xml);
            StringAssert.Contains("<SectionID>Strategy</SectionID>", xml);
            StringAssert.Contains("<Values>", xml);
            StringAssert.Contains("<Key>GalacticInformationFilter</Key>", xml);
            StringAssert.Contains("<Value>IdleConstructionYards</Value>", xml);
            StringAssert.Contains("<IgnoredItems>", xml);
            StringAssert.Contains("<TargetInstanceID>OFFICER1</TargetInstanceID>", xml);
            StringAssert.Contains("<ItemTypeID>Entity</ItemTypeID>", xml);
            StringAssert.Contains("<TargetInstanceID>PLANET1</TargetInstanceID>", xml);
            StringAssert.Contains("<ItemTypeID>Ship</ItemTypeID>", xml);
            StringAssert.Contains("<ItemTypeID>Troop</ItemTypeID>", xml);
            StringAssert.Contains("<BookmarkedItems>", xml);
            StringAssert.Contains("<TargetInstanceID>PLANET2</TargetInstanceID>", xml);
            StringAssert.Contains("<ItemTypeID>Fleet</ItemTypeID>", xml);
            StringAssert.Contains("<X>45</X>", xml);
            StringAssert.Contains("<Y>55</Y>", xml);
        }

        /// <summary>
        /// Verifies that loading a save restores persisted player UI state.
        /// </summary>
        [Test]
        public void LoadGameData_SaveWithPlayerUIState_RestoresUIState()
        {
            GameRoot game = BuildGameWithIgnoredItems();
            PlayerUIState uiState = game.GetPlayers().Single().UIState;
            UIStateSection section = uiState.GetOrCreateSection("Strategy");
            _saveGameManager.SaveGameData(game, _saveFileName);

            PlayerUIState loadedUIState = _saveGameManager
                .LoadGameData(_saveFileName)
                .GetPlayers()
                .Single()
                .UIState;
            UIStateSection loadedSection = loadedUIState.GetOrCreateSection("Strategy");

            Assert.AreEqual("Strategy", loadedSection.SectionID);
            CollectionAssert.AreEquivalent(section.Values, loadedSection.Values);
            CollectionAssert.AreEqual(
                section.IgnoredItems.Select(item => (item.TargetInstanceID, item.ItemTypeID)),
                loadedSection.IgnoredItems.Select(item => (item.TargetInstanceID, item.ItemTypeID))
            );
            CollectionAssert.AreEqual(
                section.BookmarkedItems.Select(item =>
                    (item.SlotIndex, item.TargetInstanceID, item.ItemTypeID, item.X, item.Y)
                ),
                loadedSection.BookmarkedItems.Select(item =>
                    (item.SlotIndex, item.TargetInstanceID, item.ItemTypeID, item.X, item.Y)
                )
            );
        }

        /// <summary>
        /// Verifies that saving and loading preserves game metadata.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithMetadata_PreservesMetadata()
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
                OpeningBriefingCompleted = true,
                LastSavedUtc = new System.DateTime(2025, 1, 1, 12, 0, 0, System.DateTimeKind.Utc),
            };

            GameRoot game = new GameRoot
            {
                Summary = summary,
                Metadata = metadata,
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.IsNotNull(loadedGame.Metadata);
            Assert.AreEqual("Test Save", loadedGame.Metadata.SaveDisplayName);
            Assert.AreEqual("FNALL1", loadedGame.Metadata.PlayerFactionID);
            Assert.IsTrue(loadedGame.Metadata.OpeningBriefingCompleted);
        }

        /// <summary>
        /// Verifies save and load game game with summary fields preserves all game summary fields.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithSummaryFields_PreservesAllGameSummaryFields()
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

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(GameSize.Large, loadedGame.Summary.GalaxySize);
            Assert.AreEqual(GameDifficulty.Hard, loadedGame.Summary.Difficulty);
            Assert.AreEqual(GameVictoryCondition.Conquest, loadedGame.Summary.VictoryCondition);
            Assert.AreEqual(
                GameResourceAvailability.Limited,
                loadedGame.Summary.ResourceAvailability
            );
            Assert.AreEqual("FNEMP1", loadedGame.Summary.PlayerFactionID);
            Assert.AreEqual(2, loadedGame.Summary.StartingFactionIDs.Length);
            Assert.AreEqual("FNALL1", loadedGame.Summary.StartingFactionIDs[0]);
            Assert.AreEqual("FNEMP1", loadedGame.Summary.StartingFactionIDs[1]);
            Assert.AreEqual(3, loadedGame.Summary.StartingResearchLevel);
        }

        /// <summary>
        /// Verifies save and load game game with empty collections preserves empty collections.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithEmptyCollections_PreservesEmptyCollections()
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
                EventRuntime = new GameEventRuntimeState(),
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.IsNotNull(loadedGame.GetFactions());
            Assert.AreEqual(0, loadedGame.GetFactions().Count);
            Assert.IsNotNull(loadedGame.GetEventPool());
            Assert.AreEqual(0, loadedGame.GetEventPool().Count);
            Assert.IsNotNull(loadedGame.EventRuntime.States);
            Assert.AreEqual(0, loadedGame.EventRuntime.States.Count);
            Assert.IsNotNull(loadedGame.GetUnrecruitedOfficers());
            Assert.AreEqual(0, loadedGame.GetUnrecruitedOfficers().Count);
        }

        /// <summary>
        /// Verifies save and load game game with multiple events preserves multiple events.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithMultipleEvents_PreservesMultipleEvents()
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
                events.Add(new GameEvent { InstanceID = $"EVENT{i}" });
            }

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            game.GetEventPool().AddRange(events);

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(10, loadedGame.GetEventPool().Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual($"EVENT{i}", loadedGame.GetEventPool()[i].InstanceID);
            }
        }

        /// <summary>
        /// Verifies save and load game game with large event set preserves large completed event set.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithLargeEventSet_PreservesLargeCompletedEventSet()
        {
            GameSummary summary = new GameSummary
            {
                GalaxySize = GameSize.Medium,
                Difficulty = GameDifficulty.Medium,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Normal,
                PlayerFactionID = "FNALL1",
            };

            Dictionary<string, GameEventState> eventStates =
                new Dictionary<string, GameEventState>();
            for (int i = 0; i < 50; i++)
            {
                eventStates.Add($"EVENT{i}", new GameEventState { IsComplete = true });
            }

            GameRoot game = new GameRoot
            {
                Summary = summary,
                EventRuntime = new GameEventRuntimeState { States = eventStates },
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(50, loadedGame.EventRuntime.States.Count);
            for (int i = 0; i < 50; i++)
            {
                Assert.IsTrue(loadedGame.EventRuntime.States[$"EVENT{i}"].IsComplete);
            }
        }

        /// <summary>
        /// Verifies save and load game game with all tick speeds preserves tick speed enum values.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithAllTickSpeeds_PreservesTickSpeedEnumValues()
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
                TickSpeed.VerySlow,
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
                    Galaxy = new GalaxyMap(),
                };

                _saveGameManager.SaveGameData(game, _saveFileName);
                GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

                Assert.AreEqual(
                    speed,
                    loadedGame.GameSpeed,
                    $"GameSpeed {speed} was not preserved"
                );
            }
        }

        /// <summary>
        /// Verifies save and load game game with high tick count preserves high tick count.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithHighTickCount_PreservesHighTickCount()
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
                Galaxy = new GalaxyMap(),
            };

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Assert.AreEqual(999999, loadedGame.CurrentTick);
        }

        /// <summary>
        /// Verifies save and load game game with fog snapshots preserves fog state.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithFogSnapshots_PreservesFogState()
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

            alliance.Fog.Snapshots["SECTOR1"] = new PlanetSectorSnapshot
            {
                Planets = new Dictionary<string, PlanetSnapshot>
                {
                    {
                        "CORUSCANT",
                        new PlanetSnapshot
                        {
                            TickCaptured = 100,
                            OwnerInstanceID = "FNEMP1",
                            NumRawResourceNodes = 7,
                            HasManufacturingIntelligence = true,
                            ManufacturingQueueItems = new List<IManufacturable>
                            {
                                new Building
                                {
                                    InstanceID = "BUILDING_1",
                                    DisplayName = "Queued Shipyard",
                                    ManufacturingProgress = 25,
                                    ManufacturingStatus = ManufacturingStatus.Building,
                                },
                            },
                            PopularSupport = new Dictionary<string, int>
                            {
                                { "FNEMP1", 90 },
                                { "FNALL1", 10 },
                            },
                        }
                    },
                },
            };

            alliance.Fog.PlanetToSector["CORUSCANT"] = "SECTOR1";

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            game.GetFactions().Add(alliance);

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Faction loadedAlliance = loadedGame.GetFactions().Find(f => f.InstanceID == "FNALL1");
            Assert.IsNotNull(loadedAlliance);

            Assert.AreEqual(1, loadedAlliance.Fog.Snapshots.Count);
            Assert.IsTrue(loadedAlliance.Fog.Snapshots.ContainsKey("SECTOR1"));

            PlanetSectorSnapshot loadedPlanetSectorSnapshot = loadedAlliance.Fog.Snapshots[
                "SECTOR1"
            ];
            Assert.AreEqual(1, loadedPlanetSectorSnapshot.Planets.Count);

            PlanetSnapshot loadedPlanetSnapshot = loadedPlanetSectorSnapshot.Planets["CORUSCANT"];
            Assert.AreEqual(100, loadedPlanetSnapshot.TickCaptured);
            Assert.AreEqual("FNEMP1", loadedPlanetSnapshot.OwnerInstanceID);
            Assert.AreEqual(7, loadedPlanetSnapshot.NumRawResourceNodes);
            Assert.AreEqual(2, loadedPlanetSnapshot.PopularSupport.Count);
            Assert.AreEqual(90, loadedPlanetSnapshot.PopularSupport["FNEMP1"]);
            Assert.AreEqual(10, loadedPlanetSnapshot.PopularSupport["FNALL1"]);
            Assert.IsTrue(loadedPlanetSnapshot.HasManufacturingIntelligence);
            Assert.AreEqual(1, loadedPlanetSnapshot.ManufacturingQueueItems.Count);
            Building queuedBuilding = loadedPlanetSnapshot.ManufacturingQueueItems[0] as Building;
            Assert.IsNotNull(queuedBuilding);
            Assert.AreEqual("BUILDING_1", queuedBuilding.InstanceID);
            Assert.AreEqual("Queued Shipyard", queuedBuilding.DisplayName);
            Assert.AreEqual(25, queuedBuilding.ManufacturingProgress);
            Assert.AreEqual(ManufacturingStatus.Building, queuedBuilding.ManufacturingStatus);

            Assert.AreEqual(1, loadedAlliance.Fog.PlanetToSector.Count);
            Assert.AreEqual("SECTOR1", loadedAlliance.Fog.PlanetToSector["CORUSCANT"]);
        }

        /// <summary>
        /// Verifies save and load game game with empty fog state preserves fog state.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithEmptyFogState_PreservesFogState()
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

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            game.GetFactions().Add(alliance);

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Faction loadedAlliance = loadedGame.GetFactions().Find(f => f.InstanceID == "FNALL1");
            Assert.IsNotNull(loadedAlliance);
            Assert.IsNotNull(loadedAlliance.Fog);
            Assert.AreEqual(0, loadedAlliance.Fog.Snapshots.Count);
            Assert.AreEqual(0, loadedAlliance.Fog.EntityLastSeenAt.Count);
            Assert.AreEqual(0, loadedAlliance.Fog.PlanetToSector.Count);
        }

        /// <summary>
        /// Verifies save and load game game with fog entity tracking preserves fog state.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithFogEntityTracking_PreservesFogState()
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

            alliance.Fog.Snapshots["SECTOR1"] = new PlanetSectorSnapshot
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
            alliance.Fog.PlanetToSector["PLANET1"] = "SECTOR1";

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            game.GetFactions().Add(alliance);

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Faction loadedAlliance = loadedGame.GetFactions().Find(f => f.InstanceID == "FNALL1");
            PlanetSnapshot loadedSnapshot = loadedAlliance.Fog.Snapshots["SECTOR1"].Planets[
                "PLANET1"
            ];

            Assert.AreEqual(50, loadedSnapshot.TickCaptured);
            Assert.AreEqual("FNEMP1", loadedSnapshot.OwnerInstanceID);
            Assert.AreEqual(3, loadedAlliance.Fog.EntityLastSeenAt.Count);
            Assert.AreEqual("PLANET1", loadedAlliance.Fog.EntityLastSeenAt["OFF1"]);
            Assert.AreEqual("PLANET1", loadedAlliance.Fog.EntityLastSeenAt["FLEET1"]);
            Assert.AreEqual("PLANET1", loadedAlliance.Fog.EntityLastSeenAt["REG1"]);
        }

        /// <summary>
        /// Verifies save and load game game with multiple fog snapshots preserves fog state.
        /// </summary>
        [Test]
        public void SaveAndLoadGame_GameWithMultipleFogSnapshots_PreservesFogState()
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

            alliance.Fog.Snapshots["SECTOR1"] = new PlanetSectorSnapshot
            {
                Planets = new Dictionary<string, PlanetSnapshot>
                {
                    {
                        "PLANET1",
                        new PlanetSnapshot { TickCaptured = 10, OwnerInstanceID = "FNEMP1" }
                    },
                },
            };

            alliance.Fog.Snapshots["SECTOR2"] = new PlanetSectorSnapshot
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

            alliance.Fog.PlanetToSector["PLANET1"] = "SECTOR1";
            alliance.Fog.PlanetToSector["PLANET2"] = "SECTOR2";
            alliance.Fog.PlanetToSector["PLANET3"] = "SECTOR2";

            GameRoot game = new GameRoot { Summary = summary, Galaxy = new GalaxyMap() };
            game.GetFactions().Add(alliance);

            _saveGameManager.SaveGameData(game, _saveFileName);
            GameRoot loadedGame = _saveGameManager.LoadGameData(_saveFileName);

            Faction loadedAlliance = loadedGame.GetFactions().Find(f => f.InstanceID == "FNALL1");

            Assert.AreEqual(2, loadedAlliance.Fog.Snapshots.Count);
            Assert.AreEqual(1, loadedAlliance.Fog.Snapshots["SECTOR1"].Planets.Count);
            Assert.AreEqual(2, loadedAlliance.Fog.Snapshots["SECTOR2"].Planets.Count);
            Assert.AreEqual(
                10,
                loadedAlliance.Fog.Snapshots["SECTOR1"].Planets["PLANET1"].TickCaptured
            );
            Assert.AreEqual(
                20,
                loadedAlliance.Fog.Snapshots["SECTOR2"].Planets["PLANET2"].TickCaptured
            );
            Assert.AreEqual(
                30,
                loadedAlliance.Fog.Snapshots["SECTOR2"].Planets["PLANET3"].TickCaptured
            );

            Assert.AreEqual(3, loadedAlliance.Fog.PlanetToSector.Count);
            Assert.AreEqual("SECTOR1", loadedAlliance.Fog.PlanetToSector["PLANET1"]);
            Assert.AreEqual("SECTOR2", loadedAlliance.Fog.PlanetToSector["PLANET2"]);
            Assert.AreEqual("SECTOR2", loadedAlliance.Fog.PlanetToSector["PLANET3"]);
        }

        /// <summary>
        /// Creates a saveable game containing independently excluded idle-bar identities.
        /// </summary>
        /// <returns>The configured saveable game.</returns>
        private static GameRoot BuildGameWithIgnoredItems()
        {
            Faction faction = new Faction { InstanceID = "FNALL1" };
            PlayerUIState uiState = new PlayerUIState
            {
                UIStateSections = new List<UIStateSection>
                {
                    new UIStateSection
                    {
                        SectionID = "Strategy",
                        Values = new Dictionary<string, string>
                        {
                            { "GalacticInformationFilter", "IdleConstructionYards" },
                        },
                        BookmarkedItems = new List<BookmarkedItem>
                        {
                            new BookmarkedItem
                            {
                                SlotIndex = 2,
                                TargetInstanceID = "PLANET2",
                                ItemTypeID = "Fleet",
                                X = 45,
                                Y = 55,
                            },
                        },
                        IgnoredItems = new List<IgnoredItem>
                        {
                            new IgnoredItem
                            {
                                TargetInstanceID = "OFFICER1",
                                ItemTypeID = "Entity",
                            },
                            new IgnoredItem { TargetInstanceID = "PLANET1", ItemTypeID = "Ship" },
                            new IgnoredItem { TargetInstanceID = "PLANET1", ItemTypeID = "Troop" },
                        },
                    },
                },
            };
            GameRoot game = new GameRoot
            {
                Summary = new GameSummary { PlayerFactionID = faction.InstanceID },
                Galaxy = new GalaxyMap(),
            };
            game.GetFactions().Add(faction);
            game.SetFactionController(faction.InstanceID, "PLAYER1", PlayerControllerType.Human);
            game.GetPlayers().Single().UIState = uiState;
            return game;
        }
    }
} // namespace Rebellion.Tests.Managers
