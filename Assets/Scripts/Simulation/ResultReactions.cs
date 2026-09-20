using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Drains ordered, type-specific reaction waves for the session result pipeline.
    /// </summary>
    internal sealed class ResultReactions
    {
        private readonly List<Func<IReadOnlyList<GameResult>, List<GameResult>>> _handlers =
            new List<Func<IReadOnlyList<GameResult>, List<GameResult>>>();
        private readonly List<Action<IReadOnlyList<GameResult>>> _observers =
            new List<Action<IReadOnlyList<GameResult>>>();

        /// <summary>
        /// Registers an ordered reaction handler.
        /// </summary>
        /// <typeparam name="T">The factual result type handled.</typeparam>
        /// <param name="handler">The reaction handler.</param>
        internal void Subscribe<T>(IGameResultHandler<T> handler)
            where T : GameResult
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            _handlers.Add(results =>
            {
                List<T> matching = results.OfType<T>().ToList();
                return matching.Count == 0 ? null : handler.HandleResults(matching);
            });
        }

        /// <summary>
        /// Registers a passive observer notified after all reactions resolve.
        /// </summary>
        /// <typeparam name="T">The factual result type observed.</typeparam>
        /// <param name="observer">The observer.</param>
        internal void Observe<T>(Action<IReadOnlyList<T>> observer)
            where T : GameResult
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));
            _observers.Add(results =>
            {
                List<T> matching = results.OfType<T>().ToList();
                if (matching.Count > 0)
                    observer(matching);
            });
        }

        /// <summary>
        /// Resolves initial facts and breadth-first reaction waves.
        /// </summary>
        /// <param name="results">The initial factual results.</param>
        /// <returns>The initial facts followed by every reaction wave.</returns>
        internal List<GameResult> Resolve(IEnumerable<GameResult> results)
        {
            List<GameResult> pending =
                results?.Where(result => result != null).ToList() ?? new List<GameResult>();
            List<GameResult> resolved = new List<GameResult>(pending);
            while (pending.Count > 0)
            {
                List<GameResult> next = new List<GameResult>();
                foreach (Func<IReadOnlyList<GameResult>, List<GameResult>> handler in _handlers)
                {
                    List<GameResult> produced = handler(pending);
                    if (produced != null)
                        next.AddRange(produced.Where(result => result != null));
                }
                resolved.AddRange(next);
                pending = next;
            }

            foreach (Action<IReadOnlyList<GameResult>> observer in _observers)
                observer(resolved);
            return resolved;
        }
    }
}
