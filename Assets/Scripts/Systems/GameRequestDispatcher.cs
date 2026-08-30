using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Requests;
using Rebellion.Game.Results;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Handles authoritative gameplay requests of one concrete type.
    /// </summary>
    /// <typeparam name="T">The request type handled by the subscriber.</typeparam>
    public interface IGameRequestHandler<T>
        where T : GameRequest
    {
        /// <summary>
        /// Handles one request batch and returns the factual results it produced.
        /// </summary>
        /// <param name="requests">The requests to execute.</param>
        /// <returns>The factual results produced by the requests.</returns>
        List<GameResult> HandleRequests(IReadOnlyList<T> requests);
    }

    /// <summary>
    /// Routes gameplay requests to their authoritative systems without exposing requests as results.
    /// </summary>
    public sealed class GameRequestDispatcher
    {
        private readonly Dictionary<Type, Func<GameRequest, List<GameResult>>> _handlers =
            new Dictionary<Type, Func<GameRequest, List<GameResult>>>();

        /// <summary>
        /// Registers the sole authoritative handler for one concrete request type.
        /// </summary>
        /// <typeparam name="T">The request type delivered to the handler.</typeparam>
        /// <param name="handler">The handler to invoke for matching requests.</param>
        public void Subscribe<T>(IGameRequestHandler<T> handler)
            where T : GameRequest
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            if (
                !_handlers.TryAdd(
                    typeof(T),
                    request => handler.HandleRequests(new[] { (T)request })
                )
            )
                throw new InvalidOperationException(
                    $"A request handler is already registered for '{typeof(T).Name}'."
                );
        }

        /// <summary>
        /// Executes requests in authored order, logging failed requests before continuing, and
        /// returns only their factual results.
        /// </summary>
        /// <param name="requests">The requests to dispatch.</param>
        /// <returns>The factual results produced by all matching handlers.</returns>
        public List<GameResult> Process(IEnumerable<GameRequest> requests)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (
                GameRequest request in requests?.Where(request => request != null)
                    ?? Enumerable.Empty<GameRequest>()
            )
            {
                try
                {
                    if (
                        !_handlers.TryGetValue(
                            request.GetType(),
                            out Func<GameRequest, List<GameResult>> handler
                        )
                    )
                        throw new InvalidOperationException(
                            $"No request handler is registered for '{request.GetType().Name}'."
                        );

                    List<GameResult> produced = handler(request);
                    if (produced == null)
                        continue;

                    foreach (GameResult result in produced.Where(result => result != null))
                    {
                        if (string.IsNullOrWhiteSpace(result.SourceEventInstanceID))
                            result.SourceEventInstanceID = request.SourceEventInstanceID;
                        results.Add(result);
                    }
                }
                catch (Exception exception)
                {
                    string eventInstanceId = request.SourceEventInstanceID ?? "unknown";
                    GameLogger.Log(
                        $"Event '{eventInstanceId}' request '{request.GetType().Name}' failed: {exception}",
                        GameLogger.LogLevel.Error
                    );
                }
            }
            return results;
        }
    }
}
