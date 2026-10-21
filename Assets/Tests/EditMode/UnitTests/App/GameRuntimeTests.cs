using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Generation;
using Rebellion.Simulation;
using Rebellion.Util.Reflection;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class GameRuntimeTests
    {
        private ContentPack _contentPack;
        private UserGameplaySettings _gameplaySettings;
        private GameRuntime _runtime;
        private SaveGameManager _saveGameManager;
        private string _saveDirectoryPath;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _contentPack = TestContent.Pack;
            _saveDirectoryPath = Path.Combine(
                Path.GetTempPath(),
                nameof(GameRuntimeTests),
                Guid.NewGuid().ToString("N")
            );
            _gameplaySettings = new UserGameplaySettings();
            _saveGameManager = new SaveGameManager(_saveDirectoryPath);
            _runtime = new GameRuntime(_contentPack, _saveGameManager, () => _gameplaySettings);
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_saveDirectoryPath))
                Directory.Delete(_saveDirectoryPath, true);
        }

        [Test]
        public void StartGame_InvalidContent_EndsPreviousSession()
        {
            _runtime.StartGame(CreateGame());
            GameRoot replacement = CreateGame();
            replacement.Setup.PackID = "different-pack";

            Assert.Throws<InvalidOperationException>(() => _runtime.StartGame(replacement));

            Assert.IsFalse(_runtime.HasActiveGame);
        }

        [Test]
        public void LoadGame_InvalidContent_KeepsActiveManager()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            GameManager manager = _runtime.GetActiveGameManager();
            GameRoot replacement = CreateGame();
            replacement.Setup.PackID = "different-pack";
            _saveGameManager.SaveGameData(replacement, "invalid-content", "Invalid Content");

            Assert.Throws<InvalidOperationException>(() => _runtime.LoadGame("invalid-content"));

            Assert.AreSame(manager, _runtime.GetActiveGameManager());
        }

        [Test]
        public void LoadGame_ValidSave_KeepsManagerIdentity()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            GameManager manager = _runtime.GetActiveGameManager();
            _saveGameManager.SaveGameData(CreateGame(), "replacement", "Replacement");

            _runtime.LoadGame("replacement");

            Assert.AreSame(manager, _runtime.GetActiveGameManager());
        }

        /// <summary>
        /// Verifies that missing ship suppression values use content while explicit values survive loading.
        /// </summary>
        /// <param name="savedDivisor">The saved value, or null for a legacy unit without this property.</param>
        /// <param name="expected">The restored ship divisor.</param>
        [TestCase(null, 2)]
        [TestCase(0, 0)]
        [TestCase(3, 3)]
        public void StartLoadedGame_SectorSuppression_RestoresOnlyMissingValues(
            int? savedDivisor,
            int expected
        )
        {
            GameRoot game = CreateGame();
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            Planet planet = new Planet { InstanceID = "planet" };
            Fleet fleet = new Fleet { InstanceID = "fleet" };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = "retained-ship",
                TypeID = "CSEM015",
                CanDestroyPlanets = true,
                ManufacturingStatus = ManufacturingStatus.Complete,
            };
            if (savedDivisor.HasValue)
                ship.SectorSuppression = savedDivisor.Value;
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);

            _runtime.StartLoadedGame(game);

            Assert.AreEqual(expected, ship.SectorSuppression);
        }

        [Test]
        public void StartLoadedGame_LegacyFactionSettings_RestoresActiveContentSettings()
        {
            GameRoot legacyGame = CreateGameWithLegacyFactionSettings();

            GameSession session = _runtime.StartLoadedGame(legacyGame);

            AssertFactionSettingsMatchActiveContent(session.Game);
        }

        [Test]
        public void StartGame_FactionSettings_RestoresActiveContentSettings()
        {
            GameRoot game = CreateGameWithLegacyFactionSettings();

            GameSession session = _runtime.StartGame(game);

            AssertFactionSettingsMatchActiveContent(session.Game);
        }

        [Test]
        public void StartGame_FactionModifierOverride_RestoresConfiguredSettings()
        {
            GameRoot game = CreateGameWithLegacyFactionSettings();
            Faction template = _contentPack.GameData.Factions[0];
            FactionSettings configured = template.Settings.GetDeepCopy();
            configured.GarrisonEfficiency++;
            GameGenerationConfig config = _contentPack.GameData.GenerationConfig.GetDeepCopy();
            config.FactionModifiers.Add(
                new FactionModifierConfig { FactionID = template.InstanceID, Settings = configured }
            );
            game.Setup.CustomSettings = new CustomGameSettings { GenerationConfig = config };

            GameSession session = _runtime.StartGame(game);

            Faction loaded = session
                .Game.GetFactions()
                .Single(faction => faction.InstanceID == template.InstanceID);
            Assert.AreEqual(configured.GarrisonEfficiency, loaded.Settings.GarrisonEfficiency);
            Assert.AreNotSame(configured, loaded.Settings);
        }

        [Test]
        public void StartLoadedGame_SerializedGalaxyLayout_RestoresActiveContentCoordinates()
        {
            GameRoot game = CreateGameWithStaleGalaxyLayout();

            GameSession session = _runtime.StartLoadedGame(game);

            AssertGalaxyLayoutMatchesActiveContent(session.Game);
        }

        [Test]
        public void LoadGame_LegacyFactionSettings_RestoresActiveContentSettings()
        {
            _runtime.StartGame(CreateGame());
            GameRoot legacyGame = CreateGameWithLegacyFactionSettings();
            _saveGameManager.SaveGameData(legacyGame, "legacy-faction-settings", "Legacy");

            bool loaded = _runtime.LoadGame("legacy-faction-settings");

            Assert.IsTrue(loaded);
            AssertFactionSettingsMatchActiveContent(_runtime.GetActiveGame());
        }

        [Test]
        public void LoadGame_SerializedGalaxyLayout_RestoresActiveContentCoordinates()
        {
            _runtime.StartGame(CreateGame());
            GameRoot game = CreateGameWithStaleGalaxyLayout();
            _saveGameManager.SaveGameData(game, "stale-galaxy-layout", "Stale Galaxy Layout");

            bool loaded = _runtime.LoadGame("stale-galaxy-layout");

            Assert.IsTrue(loaded);
            AssertGalaxyLayoutMatchesActiveContent(_runtime.GetActiveGame());
        }

        [Test]
        public void StartLoadedGame_LegacyFactionSettings_BombardmentDeliversCombatReport()
        {
            GameRoot legacyGame = CreateGameWithLegacyFactionSettings();
            Faction alliance = legacyGame
                .GetFactions()
                .Single(faction => faction.InstanceID == "FNALL1");
            Faction empire = legacyGame
                .GetFactions()
                .Single(faction => faction.InstanceID == "FNEMP1");
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                DisplayName = "Planet",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
            };
            legacyGame.AttachNode(sector, legacyGame.GetGalaxyMap());
            legacyGame.AttachNode(planet, sector);
            GameSession session = _runtime.StartLoadedGame(legacyGame);
            BombardmentResult result = new BombardmentResult
            {
                Planet = planet,
                AttackingFaction = alliance,
                AttackerOwnerInstanceID = alliance.InstanceID,
                DefenderOwnerInstanceID = empire.InstanceID,
                DestroyedRegiments = { new Regiment { UprisingDefense = 80 } },
            };

            Assert.DoesNotThrow(() => session.Pipeline.ProcessResults(new[] { result }));

            CombatReport report = empire
                .Messages[MessageType.Conflict]
                .OfType<CombatReport>()
                .Single(message => message.CombatType == CombatReportType.Bombardment);
            Assert.AreEqual(planet.InstanceID, report.PlanetInstanceID);
        }

        [Test]
        public void EndGame_SpeedObserverThrows_KeepsActiveManager()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            GameManager manager = _runtime.GetActiveGameManager();
            Action fail = () => throw new InvalidOperationException("Pause observer failed.");
            manager.GameSpeedChanged += fail;

            try
            {
                Assert.Throws<InvalidOperationException>(_runtime.EndGame);
                Assert.AreSame(manager, _runtime.GetActiveGameManager());
            }
            finally
            {
                manager.GameSpeedChanged -= fail;
            }
        }

        [Test]
        public void EndGame_SpeedObserverThrows_DetachesAutosave()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 39;
            _gameplaySettings.AutosaveIntervalTicks = 40;
            GameSession session = _runtime.StartGame(game);
            GameManager manager = _runtime.GetActiveGameManager();
            Action fail = () => throw new InvalidOperationException("Pause observer failed.");
            manager.GameSpeedChanged += fail;

            try
            {
                Assert.Throws<InvalidOperationException>(_runtime.EndGame);
            }
            finally
            {
                manager.GameSpeedChanged -= fail;
            }
            manager.SetGameSpeed(TickSpeed.Fast);
            session.Tick.ProcessTick();

            Assert.IsFalse(
                File.Exists(
                    _saveGameManager.GetSaveFilePath(
                        SaveGameManager.AutosaveFilePrefix + "0000000040"
                    )
                )
            );
        }

        [Test]
        public void StartGame_PendingCombat_DefersAutosaveUntilResolution()
        {
            GameRoot game = CreateContestedGame();
            game.CurrentTick = 39;
            _gameplaySettings.AutosaveIntervalTicks = 40;
            GameSession session = _runtime.StartGame(game);
            GameManager manager = _runtime.GetActiveGameManager();
            string autosavePath = _saveGameManager.GetSaveFilePath(
                SaveGameManager.AutosaveFilePrefix + "0000000040"
            );

            session.Tick.ProcessTick();

            Assert.IsFalse(File.Exists(autosavePath));
            Assert.IsFalse(_runtime.CanSave);

            session.Tick.ResolveCombat(true);

            Assert.IsTrue(File.Exists(autosavePath));
            Assert.IsTrue(_runtime.CanSave);
        }

        [Test]
        public void QuickSave_PendingCombat_DoesNotWriteSave()
        {
            GameRoot game = CreateContestedGame();
            _runtime.StartLoadedGame(game);

            bool saved = _runtime.QuickSave();

            Assert.IsFalse(saved);
            Assert.IsFalse(
                File.Exists(_saveGameManager.GetSaveFilePath(SaveGameManager.QuickSaveFileName))
            );
        }

        [Test]
        public void SaveGame_PendingCombat_DoesNotWriteSave()
        {
            GameRoot game = CreateContestedGame();
            game.CurrentTick = 40;
            GameSession session = _runtime.StartLoadedGame(game);
            GameManager manager = _runtime.GetActiveGameManager();

            bool saved = _runtime.SaveGame("pending_combat", "Pending Combat");

            Assert.IsTrue(session.GetService<SpaceCombatCommands>().HasPendingDecision);
            Assert.IsFalse(_runtime.CanSave);
            Assert.IsFalse(saved);
            Assert.IsFalse(File.Exists(_saveGameManager.GetSaveFilePath("pending_combat")));
            Assert.AreEqual(40, game.CurrentTick);
        }

        [Test]
        public void QuickLoad_AfterQuickSave_ReplacesMutatedGameWithSavedState()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 123;
            GameSession session = _runtime.StartGame(game);
            GameManager manager = _runtime.GetActiveGameManager();
            GameRoot replacement = null;
            _runtime.GameReplaced += loadedGame => replacement = loadedGame;

            _runtime.QuickSave();
            game.CurrentTick = 999;
            _runtime.QuickLoad();

            Assert.IsNotNull(replacement);
            Assert.AreNotSame(game, replacement);
            Assert.AreSame(replacement, _runtime.GetActiveGame());
            Assert.AreEqual(123, replacement.CurrentTick);
        }

        [Test]
        public void ValidateGameContent_MatchingIdentity_DoesNotThrow()
        {
            GameRoot game = CreateGame();

            Assert.DoesNotThrow(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_MissingIdentity_ThrowsInvalidOperationException()
        {
            GameRoot game = new GameRoot { Setup = new GameSetup() };

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentPack_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Setup.PackID = "different-pack";

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentVersion_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Setup.PackVersion = "different-version";

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentScenario_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Setup.ScenarioID = "different-scenario";

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_SaveHasDifferentMods_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Setup.ModIDs = new[] { "missing-mod" };
            game.Setup.ModVersions = new[] { "1.0.0" };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
                _runtime.ValidateGameContent(game)
            );
            StringAssert.Contains("mods [missing-mod@1.0.0]", exception.Message);
            StringAssert.Contains("mods [] is active", exception.Message);
        }

        [Test]
        public void LoadGame_InvalidEvent_DoesNotNotifyPresentation()
        {
            _runtime.StartGame(CreateGame());
            GameRoot replacement = CreateGame();
            replacement
                .GetEventPool()
                .Add(new GameEvent { InstanceID = "INVALID", MaximumActivations = 0 });
            _saveGameManager.SaveGameData(replacement, "invalid-event", "Invalid Event");
            int announcements = 0;
            _runtime.GameReplaced += _ => announcements++;

            Assert.Throws<InvalidOperationException>(() => _runtime.LoadGame("invalid-event"));

            Assert.AreEqual(0, announcements);
        }

        [Test]
        public void LoadGame_ValidSave_KeepsSessionIdentity()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            _saveGameManager.SaveGameData(CreateGame(), "replacement", "Replacement");

            _runtime.LoadGame("replacement");

            Assert.AreSame(session, _runtime.GetActiveGameSession());
        }

        [Test]
        public void LoadGame_ValidSave_ResetsClockBeforeNotification()
        {
            _runtime.StartGame(CreateGame());
            GameManager clock = _runtime.GetActiveGameManager();
            clock.SetGameSpeed(TickSpeed.Fast);
            float interval = _runtime.GetActiveGame().Config.GameSpeed.FastTickIntervalSeconds;
            clock.TryAdvanceTickTimer(interval / 2f);
            GameRoot replacement = CreateGame();
            replacement.SetGameSpeed(TickSpeed.Fast);
            _saveGameManager.SaveGameData(replacement, "replacement", "Replacement");
            bool? ready = null;
            _runtime.GameReplaced += _ => ready = clock.TryAdvanceTickTimer(interval / 2f);

            _runtime.LoadGame("replacement");

            Assert.IsFalse(ready);
        }

        [Test]
        public void LoadGame_PresentationThrows_LeavesReconciliationPending()
        {
            GameSession session = _runtime.StartGame(CreateGame());
            _saveGameManager.SaveGameData(CreateContestedGame(), "contested", "Contested");
            _runtime.GameReplaced += _ =>
                throw new InvalidOperationException("Presentation failed.");

            Assert.Throws<InvalidOperationException>(() => _runtime.LoadGame("contested"));

            Assert.IsFalse(session.GetService<SpaceCombatCommands>().HasPendingDecision);
        }

        /// <summary>
        /// Creates game.
        /// </summary>
        /// <returns>The created game.</returns>
        private GameRoot CreateGame()
        {
            return new GameRoot
            {
                Setup = new GameSetup
                {
                    PackID = _contentPack.Definition.ID,
                    PackVersion = _contentPack.Definition.Version,
                    ScenarioID = _contentPack.Scenario.ID,
                },
            };
        }

        /// <summary>
        /// Creates a load-compatible game whose serialized faction settings predate current content.
        /// </summary>
        /// <returns>The prepared legacy game.</returns>
        private GameRoot CreateGameWithLegacyFactionSettings()
        {
            GameRoot game = CreateGame();
            foreach (Faction template in _contentPack.GameData.Factions)
            {
                game.GetFactions()
                    .Add(
                        new Faction
                        {
                            InstanceID = template.InstanceID,
                            DisplayName = template.DisplayName,
                            Settings = new FactionSettings
                            {
                                BattleLossLoyaltyDivisor = 0,
                                GarrisonEfficiency = 99,
                            },
                        }
                    );
            }

            return game;
        }

        /// <summary>
        /// Creates a load-compatible game with stale serialized galaxy coordinates.
        /// </summary>
        /// <returns>The prepared game.</returns>
        private GameRoot CreateGameWithStaleGalaxyLayout()
        {
            PlanetSector template = _contentPack.GameData.PlanetSectors.First(sector =>
                sector.GetChildren<Planet>().Count > 0
            );
            Planet planetTemplate = template.GetChildren<Planet>()[0];
            PlanetSector sector = new PlanetSector
            {
                InstanceID = template.InstanceID,
                TypeID = template.TypeID,
                PositionX = template.PositionX + 100,
                PositionY = template.PositionY + 100,
            };
            Planet planet = new Planet
            {
                InstanceID = planetTemplate.InstanceID,
                TypeID = planetTemplate.TypeID,
                PositionX = planetTemplate.PositionX + 100,
                PositionY = planetTemplate.PositionY + 100,
            };
            GameRoot game = CreateGame();
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            return game;
        }

        /// <summary>
        /// Asserts that loaded factions own independent copies of active authored settings.
        /// </summary>
        /// <param name="game">The loaded game to inspect.</param>
        private void AssertFactionSettingsMatchActiveContent(GameRoot game)
        {
            foreach (Faction template in _contentPack.GameData.Factions)
            {
                Faction loaded = game.GetFactions()
                    .Single(faction => faction.InstanceID == template.InstanceID);
                Assert.AreEqual(
                    template.Settings.BattleLossLoyaltyDivisor,
                    loaded.Settings.BattleLossLoyaltyDivisor
                );
                Assert.AreEqual(
                    template.Settings.GarrisonEfficiency,
                    loaded.Settings.GarrisonEfficiency
                );
                Assert.AreNotSame(template.Settings, loaded.Settings);
                Assert.AreNotSame(template.Settings.Headquarters, loaded.Settings.Headquarters);
            }
        }

        /// <summary>
        /// Asserts that loaded galaxy coordinates match the active authored layout.
        /// </summary>
        /// <param name="game">The loaded game to inspect.</param>
        private void AssertGalaxyLayoutMatchesActiveContent(GameRoot game)
        {
            PlanetSector loaded = game.GetGalaxyMap().GetChildren<PlanetSector>().Single();
            PlanetSector template = _contentPack.GameData.PlanetSectors.Single(sector =>
                sector.TypeID == loaded.TypeID
            );
            Planet loadedPlanet = loaded.GetChildren<Planet>().Single();
            Planet templatePlanet = template
                .GetChildren<Planet>()
                .Single(planet => planet.InstanceID == loadedPlanet.InstanceID);
            Assert.AreEqual(template.PositionX, loaded.PositionX);
            Assert.AreEqual(template.PositionY, loaded.PositionY);
            Assert.AreEqual(templatePlanet.PositionX, loadedPlanet.PositionX);
            Assert.AreEqual(templatePlanet.PositionY, loadedPlanet.PositionY);
        }

        /// <summary>
        /// Creates contested game.
        /// </summary>
        /// <returns>The created contested game.</returns>
        private GameRoot CreateContestedGame()
        {
            GameRoot game = CreateGame();
            Faction alliance = new Faction { InstanceID = "FNALL1", DisplayName = "Alliance" };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
            game.SetFactionController(alliance.InstanceID, "player", PlayerControllerType.Human);
            PlanetSector sector = new PlanetSector { InstanceID = "SECTOR" };
            Planet planet = new Planet
            {
                InstanceID = "PLANET",
                DisplayName = "Planet",
                OwnerInstanceID = empire.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(sector, game.GetGalaxyMap());
            game.AttachNode(planet, sector);
            AddFleet(game, planet, "ALLIANCE_FLEET", alliance.InstanceID);
            AddFleet(game, planet, "EMPIRE_FLEET", empire.InstanceID);
            return game;
        }

        /// <summary>
        /// Adds fleet.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="instanceId">The instance id.</param>
        /// <param name="ownerId">The owner id.</param>
        private static void AddFleet(
            GameRoot game,
            Planet planet,
            string instanceId,
            string ownerId
        )
        {
            Fleet fleet = new Fleet
            {
                InstanceID = instanceId,
                DisplayName = instanceId,
                OwnerInstanceID = ownerId,
            };
            CapitalShip ship = new CapitalShip
            {
                InstanceID = instanceId + "_SHIP",
                DisplayName = instanceId + " Ship",
                OwnerInstanceID = ownerId,
                ManufacturingStatus = ManufacturingStatus.Complete,
                CurrentHullStrength = 100,
                MaxHullStrength = 100,
            };
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
        }
    }
}
