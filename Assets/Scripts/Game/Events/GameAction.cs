using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines a contract for actions that modify the game state when executed.
    /// Each action returns a list of results describing what changed, which the
    /// caller can use for notifications, logging, or AI reactions.
    /// </summary>
    [PersistableObject]
    public abstract class GameAction
    {
        /// <summary>
        /// Executes the action within one event activation.
        /// </summary>
        /// <param name="context">The game, random source, and activation data.</param>
        /// <returns>Results describing what changed.</returns>
        public abstract List<GameResult> Execute(GameActionContext context);

        /// <summary>
        /// Executes an action outside an event activation using the game's random source.
        /// </summary>
        public List<GameResult> Execute(GameRoot game) =>
            Execute(new GameActionContext(game, game?.Random));

        /// <summary>
        /// Executes an action outside an event activation using an injected random source.
        /// </summary>
        public List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider) =>
            Execute(new GameActionContext(game, provider));

        internal List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => Execute(new GameActionContext(game, provider, context));
    }

    /// <summary>
    /// Supplies every dependency available to one action execution.
    /// </summary>
    public sealed class GameActionContext
    {
        public GameRoot Game { get; }
        public IRandomNumberProvider Random { get; }
        public GameEventExecutionContext Activation { get; }

        public GameActionContext(
            GameRoot game,
            IRandomNumberProvider random,
            GameEventExecutionContext activation = null
        )
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Random = random ?? throw new ArgumentNullException(nameof(random));
            Activation = activation;
        }
    }
}
