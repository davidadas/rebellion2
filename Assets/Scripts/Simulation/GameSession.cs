using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.DependencyInjection;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Publishes fully prepared game runtimes while preserving the session's scheduling and event identity.
    /// </summary>
    public sealed class GameSession : IServiceLocator, IDisposable
    {
        private readonly GameDataCatalog _gameData;
        private GameSessionRuntime _runtime;

        public GameRoot Game => _runtime.Game;
        public GameResultPipeline Pipeline { get; }
        public GameTickProcessor Tick { get; }

        internal MessageObserver MessageObserver => _runtime.MessageObserver;
        internal GameEventCommands GameEventCommands => _runtime.GameEventCommands;
        internal GameResultBus Results => _runtime.Results;

        /// <summary>
        /// Prepares a game graph and constructs its connected runtime components.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="gameData">The composed content catalog used to construct runtime dependencies.</param>
        public GameSession(GameRoot game, GameDataCatalog gameData)
        {
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            Pipeline = new GameResultPipeline(() => Results, () => MessageObserver);
            Tick = new GameTickProcessor(
                (results, processMessages) => Pipeline.ProcessResults(results, processMessages),
                results => Pipeline.ProcessMessageReactions(results)
            );
            ReplaceGame(game);
        }

        /// <summary>
        /// Resolves a runtime service from the current game's scope.
        /// </summary>
        /// <typeparam name="T">The requested runtime service type.</typeparam>
        /// <returns>The current game's service instance.</returns>
        public T GetService<T>() => _runtime.Services.GetService<T>();

        /// <summary>
        /// Resolves a runtime service by its type.
        /// </summary>
        /// <param name="serviceType">The requested runtime service type.</param>
        /// <returns>The current game's service instance.</returns>
        public object GetService(Type serviceType) => _runtime.Services.GetService(serviceType);

        /// <summary>
        /// Prepares and connects a replacement before publishing it as the active runtime.
        /// Failed preparation leaves the active graph, tick state, and subscriptions intact.
        /// </summary>
        /// <param name="game">The replacement graph to prepare.</param>
        internal void ReplaceGame(GameRoot game)
        {
            GameSessionRuntime replacement = new GameSessionRuntime(game, _gameData);
            try
            {
                replacement.ConnectResults(ProcessImmediateResults);
            }
            catch
            {
                replacement.Dispose();
                throw;
            }

            GameSessionRuntime previous = _runtime;
            _runtime = replacement;
            Tick.ConnectRuntime(replacement.TickRuntime);
            Tick.Reset();
            previous?.Dispose();
        }

        /// <summary>
        /// Releases the subscriptions and services owned by the active runtime.
        /// </summary>
        public void Dispose()
        {
            _runtime?.Dispose();
        }

        /// <summary>
        /// Routes results produced outside the tick loop and refreshes visible intelligence.
        /// </summary>
        /// <param name="results">The immediately produced game results.</param>
        private void ProcessImmediateResults(IReadOnlyList<GameResult> results)
        {
            Pipeline.ProcessImmediate(results);
            GetService<FogOfWarObserver>().RefreshVisibleKnowledge();
        }
    }
}
