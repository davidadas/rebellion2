using System;
using System.IO;
using NUnit.Framework;
using Rebellion.Game;

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
        public void QuickSaveThenQuickLoad_ReplacesMutatedGameWithSavedState()
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
    }
}
