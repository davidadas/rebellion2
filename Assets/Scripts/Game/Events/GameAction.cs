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
        public GameAction() { }

        /// <summary>
        /// Executes the action, modifying the game state.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>Results describing what changed.</returns>
        public abstract List<GameResult> Execute(GameRoot game);

        /// <summary>
        /// Executes the action with the event engine's deterministic random source.
        /// Composite or stochastic actions override this method to pass the source onward.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">The random provider for this execution chain.</param>
        /// <returns>Results describing what changed.</returns>
        public virtual List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider) =>
            Execute(game);

        /// <summary>
        /// Executes with the concrete scheduling context for a scoped event.
        /// Existing actions remain context-independent unless they override this overload.
        /// </summary>
        public virtual List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        ) => Execute(game, provider);
    }
}
