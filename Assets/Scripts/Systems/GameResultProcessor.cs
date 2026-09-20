using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Systems
{
    /// <summary>
    /// Dispatches game results to ordered, type-specific subscribers and drains their reactions.
    /// </summary>
    public sealed class GameResultProcessor
    {
        private readonly List<Func<IReadOnlyList<GameResult>, List<GameResult>>> _subscriptions =
            new List<Func<IReadOnlyList<GameResult>, List<GameResult>>>();
        private readonly List<Action<IReadOnlyList<GameResult>>> _observers =
            new List<Action<IReadOnlyList<GameResult>>>();

        /// <summary>
        /// Adds a result handler at the end of the ordered subscription list.
        /// </summary>
        /// <typeparam name="T">The result type delivered to the handler.</typeparam>
        /// <param name="handler">The handler to invoke for matching results.</param>
        public void Subscribe<T>(IGameResultHandler<T> handler)
            where T : GameResult
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _subscriptions.Add(results =>
            {
                List<T> matchingResults = results.OfType<T>().ToList();
                return matchingResults.Count == 0 ? null : handler.HandleResults(matchingResults);
            });
        }

        /// <summary>
        /// Adds an observer that receives all matching results after reaction processing completes.
        /// </summary>
        /// <typeparam name="T">The result type delivered to the observer.</typeparam>
        /// <param name="observer">The observer to invoke for matching results.</param>
        public void Observe<T>(Action<IReadOnlyList<T>> observer)
            where T : GameResult
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            _observers.Add(results =>
            {
                List<T> matchingResults = results.OfType<T>().ToList();
                if (matchingResults.Count > 0)
                    observer(matchingResults);
            });
        }

        /// <summary>
        /// Processes an initial result batch and every reaction wave it produces.
        /// </summary>
        /// <param name="results">The initial results to process.</param>
        /// <returns>The initial results followed by all ordered reaction waves.</returns>
        public List<GameResult> Process(IEnumerable<GameResult> results)
        {
            List<GameResult> resolvedResults = ResolveReactions(results);

            foreach (Action<IReadOnlyList<GameResult>> observer in _observers)
                observer(resolvedResults);

            return resolvedResults;
        }

        /// <summary>
        /// Drains result reactions without notifying observers, allowing a caller to finish an
        /// operation before publishing its completed result batch.
        /// </summary>
        /// <param name="results">The initial results to resolve.</param>
        /// <returns>The initial results followed by all ordered reaction waves.</returns>
        public List<GameResult> ResolveReactions(IEnumerable<GameResult> results)
        {
            List<GameResult> resolvedResults =
                results?.Where(result => result != null).ToList() ?? new List<GameResult>();
            List<GameResult> pendingResults = resolvedResults
                .Where(result => !result.ReactionsResolved)
                .ToList();

            while (pendingResults.Count > 0)
            {
                foreach (GameResult pendingResult in pendingResults)
                    pendingResult.ReactionsResolved = true;

                List<GameResult> reactionResults = new List<GameResult>();
                foreach (
                    Func<IReadOnlyList<GameResult>, List<GameResult>> subscription in _subscriptions
                )
                {
                    List<GameResult> subscriptionResults = subscription(pendingResults);
                    if (subscriptionResults != null)
                    {
                        reactionResults.AddRange(
                            subscriptionResults.Where(result => result != null)
                        );
                    }
                }

                resolvedResults.AddRange(reactionResults);
                pendingResults = reactionResults
                    .Where(result => !result.ReactionsResolved)
                    .ToList();
            }

            return resolvedResults;
        }
    }
}
