using System;
using System.IO;
using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class GameRuntimeTests
    {
        private ContentPack contentPack;
        private GameRuntime runtime;
        private string saveDirectoryPath;

        [SetUp]
        public void SetUp()
        {
            contentPack = TestContent.Pack;
            saveDirectoryPath = Path.Combine(
                Path.GetTempPath(),
                nameof(GameRuntimeTests),
                Guid.NewGuid().ToString("N")
            );
            runtime = new GameRuntime(
                _ => { },
                contentPack,
                new SaveGameManager(saveDirectoryPath)
            );
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(saveDirectoryPath))
                Directory.Delete(saveDirectoryPath, true);
        }

        [Test]
        public void QuickSaveThenQuickLoad_ReplacesMutatedGameWithSavedState()
        {
            GameRoot game = CreateGame();
            game.CurrentTick = 123;
            GameManager manager = runtime.StartGame(game);
            GameRoot replacement = null;
            manager.GameReplaced += loadedGame => replacement = loadedGame;

            runtime.QuickSave();
            game.CurrentTick = 999;
            runtime.QuickLoad();

            Assert.IsNotNull(replacement);
            Assert.AreNotSame(game, replacement);
            Assert.AreSame(replacement, runtime.GetActiveGame());
            Assert.AreEqual(123, replacement.CurrentTick);
        }

        [Test]
        public void ValidateGameContent_MatchingIdentity_DoesNotThrow()
        {
            GameRoot game = CreateGame();

            Assert.DoesNotThrow(() => runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_MissingIdentity_ThrowsInvalidOperationException()
        {
            GameRoot game = new GameRoot { Summary = new GameSummary() };

            Assert.Throws<InvalidOperationException>(() => runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentPack_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.PackID = "different-pack";

            Assert.Throws<InvalidOperationException>(() => runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentVersion_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.PackVersion = "different-version";

            Assert.Throws<InvalidOperationException>(() => runtime.ValidateGameContent(game));
        }

        [Test]
        public void ValidateGameContent_DifferentScenario_ThrowsInvalidOperationException()
        {
            GameRoot game = CreateGame();
            game.Summary.ScenarioID = "different-scenario";

            Assert.Throws<InvalidOperationException>(() => runtime.ValidateGameContent(game));
        }

        private GameRoot CreateGame()
        {
            return new GameRoot
            {
                Summary = new GameSummary
                {
                    PackID = contentPack.Definition.ID,
                    PackVersion = contentPack.Definition.Version,
                    ScenarioID = contentPack.Scenario.ID,
                },
            };
        }
    }
}
