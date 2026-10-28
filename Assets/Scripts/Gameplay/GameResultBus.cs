using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Delivers result batches and their queued reactions within one running game.
    /// </summary>
    public sealed class GameResultBus
    {
        private readonly List<Subscription> _subscribers = new();
        private readonly List<Subscription> _observers = new();
        private readonly List<GameResult> _pendingResults = new();
        private readonly ConditionalWeakTable<GameResult, HashSet<object>> _reactionOrigins = new();
        private HashSet<object> _currentReactionOrigins;
        private bool _isPublishing;

        /// <summary>
        /// Subscribes a reaction that publishes any follow-up changes through this bus.
        /// </summary>
        /// <typeparam name="T">The result type delivered to the subscriber.</typeparam>
        /// <param name="subscriber">The callback receiving each matching result batch.</param>
        /// <returns>A handle that detaches the subscriber when disposed.</returns>
        public IDisposable Subscribe<T>(Action<IReadOnlyList<T>> subscriber)
            where T : GameResult
        {
            return Register(subscriber, _subscribers);
        }

        /// <summary>
        /// Subscribes an existing reaction operation whose returned facts form the next wave.
        /// </summary>
        /// <typeparam name="T">The result type delivered to the subscriber.</typeparam>
        /// <param name="subscriber">The callback returning follow-up results.</param>
        /// <returns>A handle that detaches the subscriber when disposed.</returns>
        public IDisposable Subscribe<T>(Func<IReadOnlyList<T>, List<GameResult>> subscriber)
            where T : GameResult
        {
            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            return Subscribe<T>(results =>
            {
                Publish(subscriber(results));
            });
        }

        /// <summary>
        /// Subscribes an observer of the complete batch after all its reactions have settled.
        /// </summary>
        /// <typeparam name="T">The result type delivered to the observer.</typeparam>
        /// <param name="observer">The callback receiving matching settled results.</param>
        /// <returns>A handle that detaches the observer when disposed.</returns>
        public IDisposable Observe<T>(Action<IReadOnlyList<T>> observer)
            where T : GameResult
        {
            return Register(observer, _observers);
        }

        /// <summary>
        /// Observes every completed result batch after all reactions have settled.
        /// </summary>
        /// <param name="observer">The callback receiving each complete settled batch.</param>
        /// <returns>A handle that detaches the observer when disposed.</returns>
        public IDisposable ObserveAll(Action<IReadOnlyList<GameResult>> observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            Subscription subscription = new Subscription(
                results => Deliver(observer, results),
                _observers
            );
            _observers.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// Publishes one completed change and drains its reactions before returning.
        /// Nested publication queues the change until the current wave finishes.
        /// </summary>
        /// <param name="result">The completed change; null is ignored.</param>
        /// <returns>The settled results, or an empty list when queued by an active delivery.</returns>
        public List<GameResult> Publish(GameResult result)
        {
            return Publish(new[] { result });
        }

        /// <summary>
        /// Publishes a batch and drains breadth-first reaction waves without recursive delivery.
        /// Exceptions propagate, abandoning the unfinished delivery as in the existing processor.
        /// </summary>
        /// <param name="results">The completed changes; null collections and entries are ignored.</param>
        /// <returns>The settled results, or an empty list when queued by an active delivery.</returns>
        public List<GameResult> Publish(IEnumerable<GameResult> results)
        {
            List<GameResult> incomingResults =
                results?.Where(result => result != null).ToList() ?? new List<GameResult>();
            TrackReactionResults(incomingResults, _currentReactionOrigins);
            _pendingResults.AddRange(incomingResults);
            if (_isPublishing)
                return new List<GameResult>();

            _isPublishing = true;
            List<GameResult> resolvedResults = new();
            try
            {
                do
                {
                    List<GameResult> settledResults = new();
                    while (_pendingResults.Count > 0)
                    {
                        List<GameResult> currentResults = new(_pendingResults);
                        _pendingResults.Clear();
                        settledResults.AddRange(currentResults);
                        foreach (Subscription subscriber in _subscribers.ToArray())
                            subscriber.Deliver(currentResults);
                    }

                    resolvedResults.AddRange(settledResults);
                    foreach (Subscription observer in _observers.ToArray())
                        observer.Deliver(settledResults);
                } while (_pendingResults.Count > 0);

                return resolvedResults;
            }
            finally
            {
                _pendingResults.Clear();
                _isPublishing = false;
            }
        }

        /// <summary>
        /// Executes a reaction source once within its own causal chain without limiting independent results.
        /// </summary>
        /// <param name="source">The identity of the reaction operation.</param>
        /// <param name="cause">The triggering result, or null for an independently scheduled operation.</param>
        /// <param name="reaction">The operation producing follow-up facts.</param>
        /// <returns>The resulting facts, or an empty list for a self-caused reaction.</returns>
        internal List<GameResult> RunReaction(
            object source,
            GameResult cause,
            Func<List<GameResult>> reaction
        )
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (reaction == null)
                throw new ArgumentNullException(nameof(reaction));
            HashSet<object> inherited = null;
            if (cause != null)
                _reactionOrigins.TryGetValue(cause, out inherited);
            if (inherited?.Contains(source) == true)
                return new List<GameResult>();
            HashSet<object> previous = _currentReactionOrigins;
            _currentReactionOrigins = inherited == null ? new() : new(inherited);
            _currentReactionOrigins.Add(source);
            try
            {
                List<GameResult> results = reaction() ?? new List<GameResult>();
                TrackReactionResults(results, _currentReactionOrigins);
                return results;
            }
            finally
            {
                _currentReactionOrigins = previous;
            }
        }

        /// <summary>
        /// Preserves the origins of a callback's input wave during synchronous follow-up publication.
        /// </summary>
        /// <typeparam name="T">The delivered result type.</typeparam>
        /// <param name="callback">The existing subscriber or observer.</param>
        /// <param name="results">The exact typed input batch delivered to that callback.</param>
        private void Deliver<T>(Action<IReadOnlyList<T>> callback, IReadOnlyList<T> results)
            where T : GameResult
        {
            HashSet<object> previous = _currentReactionOrigins;
            _currentReactionOrigins = null;
            foreach (T result in results)
            {
                if (!_reactionOrigins.TryGetValue(result, out HashSet<object> origins))
                    continue;
                _currentReactionOrigins ??= new();
                _currentReactionOrigins.UnionWith(origins);
            }
            try
            {
                callback(results);
            }
            finally
            {
                _currentReactionOrigins = previous;
            }
        }

        /// <summary>
        /// Associates new facts with their reaction origins while retaining more precise existing attribution.
        /// </summary>
        /// <param name="results">The facts returned or published by a reaction.</param>
        /// <param name="origins">The reaction sources already present in the causal chain.</param>
        private void TrackReactionResults(IEnumerable<GameResult> results, HashSet<object> origins)
        {
            if (origins == null)
                return;
            foreach (GameResult result in results)
                if (result != null && !_reactionOrigins.TryGetValue(result, out _))
                    _reactionOrigins.Add(result, new HashSet<object>(origins));
        }

        /// <summary>
        /// Registers a type-filtered callback in the selected delivery phase.
        /// </summary>
        /// <typeparam name="T">The result type accepted by the callback.</typeparam>
        /// <param name="callback">The callback invoked for nonempty matching batches.</param>
        /// <param name="subscriptions">The ordered reaction or observer subscriptions.</param>
        /// <returns>The disposable registration.</returns>
        private IDisposable Register<T>(
            Action<IReadOnlyList<T>> callback,
            List<Subscription> subscriptions
        )
            where T : GameResult
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            Subscription subscription = new(
                results =>
                {
                    List<T> matchingResults = results.OfType<T>().ToList();
                    if (matchingResults.Count > 0)
                        Deliver(callback, matchingResults);
                },
                subscriptions
            );
            subscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// Owns one callback registration without exposing its delivery internals to subscribers.
        /// </summary>
        private sealed class Subscription : IDisposable
        {
            private readonly Action<IReadOnlyList<GameResult>> _callback;
            private readonly List<Subscription> _subscriptions;
            private bool _isDisposed;

            /// <summary>
            /// Creates a registration for one ordered delivery phase.
            /// </summary>
            /// <param name="callback">The type-filtered delivery callback.</param>
            /// <param name="subscriptions">The collection owning this registration.</param>
            internal Subscription(
                Action<IReadOnlyList<GameResult>> callback,
                List<Subscription> subscriptions
            )
            {
                _callback = callback;
                _subscriptions = subscriptions;
            }

            /// <summary>
            /// Delivers a batch unless the subscriber has detached.
            /// </summary>
            /// <param name="results">The current reaction wave or settled batch.</param>
            internal void Deliver(IReadOnlyList<GameResult> results)
            {
                if (!_isDisposed)
                    _callback(results);
            }

            /// <summary>
            /// Detaches this callback; repeated disposal has no further effect.
            /// </summary>
            public void Dispose()
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;
                _subscriptions.Remove(this);
            }
        }
    }
}
