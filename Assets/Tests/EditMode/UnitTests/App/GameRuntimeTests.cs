using System;
using System.IO;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

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

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_saveDirectoryPath))
                Directory.Delete(_saveDirectoryPath, true);
        }

        [Test]
        public void StartGame_PendingCombat_DefersAutosaveUntilResolution()
        {
            GameRoot game = CreateContestedGame();
            game.CurrentTick = 39;
            _gameplaySettings.AutosaveIntervalTicks = 40;
            GameManager manager = _runtime.StartGame(game);
            string autosavePath = _saveGameManager.GetSaveFilePath(
                SaveGameManager.AutosaveFilePrefix + "0000000040"
            );

            manager.ProcessTick();

            Assert.IsFalse(File.Exists(autosavePath));
            Assert.IsFalse(_runtime.CanSave);

            manager.ResolveCombat(true);

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
            GameManager manager = _runtime.StartLoadedGame(game);

            bool saved = _runtime.SaveGame("pending_combat", "Pending Combat");

            Assert.IsTrue(manager.SpaceCombatSystem.HasPendingDecision);
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
            GameManager manager = _runtime.StartGame(game);
            GameRoot replacement = null;
            manager.GameReplaced += loadedGame => replacement = loadedGame;

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
            GameRoot game = new GameRoot { Summary = new GameSummary() };

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentPack_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.PackID = "different-pack";

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentVersion_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.PackVersion = "different-version";

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentScenario_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.ScenarioID = "different-scenario";

            Assert.Throws<InvalidOperationException>(() => _runtime.ValidateGameContent(game));
        }

        private GameRoot CreateGame()
        {
            return new GameRoot
            {
                Summary = new GameSummary
                {
                    PackID = _contentPack.Definition.ID,
                    PackVersion = _contentPack.Definition.Version,
                    ScenarioID = _contentPack.Scenario.ID,
                },
            };
        }

        private GameRoot CreateContestedGame()
        {
            GameRoot game = CreateGame();
            Faction alliance = new Faction
            {
                InstanceID = "FNALL1",
                DisplayName = "Alliance",
                PlayerID = "player",
            };
            Faction empire = new Faction { InstanceID = "FNEMP1", DisplayName = "Empire" };
            game.GetFactions().Add(alliance);
            game.GetFactions().Add(empire);
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
