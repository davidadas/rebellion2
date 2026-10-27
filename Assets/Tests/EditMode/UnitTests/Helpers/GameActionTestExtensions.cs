using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Game.Rules;
using Rebellion.Game.Units;
using Rebellion.Simulation;
using Rebellion.Util.Random;

namespace Rebellion.Tests
{
    internal static class GameActionTestExtensions
    {
        /// <summary>
        /// Executes the requested operation.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="game">The game.</param>
        /// <returns>The result of execute.</returns>
        internal static List<GameResult> Execute(this GameAction action, GameRoot game)
        {
            GameActionContext context = new GameActionContext(game, game.Random);
            GameActionCommands.ExecuteAction(action, context);
            return context.Results;
        }

        /// <summary>
        /// Executes the requested operation.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="game">The game.</param>
        /// <param name="random">The random.</param>
        /// <returns>The result of execute.</returns>
        internal static List<GameResult> Execute(
            this GameAction action,
            GameRoot game,
            IRandomNumberProvider random
        )
        {
            GameActionContext context = new GameActionContext(game, random);
            GameActionCommands.ExecuteAction(action, context);
            return context.Results;
        }

        /// <summary>
        /// Executes the requested operation.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="game">The game.</param>
        /// <param name="random">The random.</param>
        /// <param name="evaluation">The evaluation.</param>
        /// <returns>The result of execute.</returns>
        internal static List<GameResult> Execute(
            this GameAction action,
            GameRoot game,
            IRandomNumberProvider random,
            GameEvaluationContext evaluation
        )
        {
            GameActionContext context = new GameActionContext(game, random, evaluation);
            GameActionCommands.ExecuteAction(action, context);
            return context.Results;
        }

        /// <summary>
        /// Executes the requested operation.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="game">The game.</param>
        /// <param name="unitFactory">The unit factory.</param>
        /// <returns>The result of execute.</returns>
        internal static List<GameResult> Execute(
            this GameAction action,
            GameRoot game,
            UnitFactory unitFactory
        )
        {
            GameActionContext context = new GameActionContext(game, game.Random, null, unitFactory);
            GameActionCommands.ExecuteAction(action, context);
            return context.Results;
        }
    }
}
