using System;
using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class GameRuntimeTests
    {
        private ContentPack contentPack;
        private GameRuntime runtime;

        [SetUp]
        public void SetUp()
        {
            contentPack = TestContent.Pack;
            runtime = new GameRuntime(_ => { }, contentPack);
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
