using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Gameplay;

namespace Rebellion.Tests.Gameplay
{
    [TestFixture]
    public class VictoryCommandsTests
    {
        [Test]
        public void TryDeclareVictory_RegisteredWinner_ReturnsOutcome()
        {
            (GameRoot game, Faction winner, Faction loser, VictoryCommands commands) = BuildGame();

            VictoryResult result = commands.TryDeclareVictory(winner);

            Assert.IsNotNull(result);
            Assert.AreSame(winner, result.Winner);
            CollectionAssert.AreEqual(new[] { loser }, result.DefeatedFactions);
            Assert.AreEqual(game.CurrentTick, result.Tick);
        }

        [Test]
        public void TryDeclareVictory_UnregisteredWinner_ReturnsNull()
        {
            (_, _, _, VictoryCommands commands) = BuildGame();

            VictoryResult result = commands.TryDeclareVictory(
                new Faction { InstanceID = "unknown" }
            );

            Assert.IsNull(result);
        }

        [Test]
        public void TryDeclareVictory_AfterDeclaration_ReturnsNull()
        {
            (_, Faction winner, _, VictoryCommands commands) = BuildGame();
            commands.TryDeclareVictory(winner);

            VictoryResult result = commands.TryDeclareVictory(winner);

            Assert.IsNull(result);
        }

        /// <summary>
        /// Creates a registered winner, loser, and victory command service.
        /// </summary>
        /// <returns>The configured game and victory collaborators.</returns>
        private static (
            GameRoot game,
            Faction winner,
            Faction loser,
            VictoryCommands commands
        ) BuildGame()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.Summary = new GameSummary { VictoryCondition = GameVictoryCondition.Headquarters };
            game.CurrentTick = 200;
            Faction winner = new Faction { InstanceID = "winner" };
            Faction loser = new Faction { InstanceID = "loser" };
            game.GetFactions().Add(winner);
            game.GetFactions().Add(loser);
            return (game, winner, loser, new VictoryCommands(game));
        }
    }
}
