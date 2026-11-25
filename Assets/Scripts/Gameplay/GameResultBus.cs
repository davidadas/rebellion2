using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Delivers result batches and their queued reactions within one running game.
    /// </summary>
    public sealed class GameResultBus
    {
        private readonly List<Subscription> _subscribers = new();
        private readonly List<Subscription> _afterReactions = new();
        private readonly List<Subscription> _observers = new();
        private readonly List<GameResult> _pendingResults = new();
        private PendingResultSource[] _pendingSources = Array.Empty<PendingResultSource>();
        private bool _isPublishing;

        /// <summary>
        /// Gets whether a result delivery or reaction wave is currently executing.
        /// </summary>
        internal bool IsPublishing => _isPublishing;

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
        /// Subscribes a reaction after ordinary reaction waves settle and before observers run.
        /// Each callback receives only newly delivered facts. All callbacks for that batch finish
        /// before their queued follow-ups are delivered.
        /// </summary>
        /// <typeparam name="T">The result type delivered to the subscriber.</typeparam>
        /// <param name="subscriber">The callback receiving each matching settled reaction batch.</param>
        /// <returns>A handle that detaches the subscriber when disposed.</returns>
        public IDisposable SubscribeAfterReactions<T>(Action<IReadOnlyList<T>> subscriber)
            where T : GameResult
        {
            return Register(subscriber, _afterReactions);
        }

        /// <summary>
        /// Subscribes an operation after ordinary reactions settle, queuing its returned facts
        /// before terminal observation without delivering the completed facts again.
        /// </summary>
        /// <typeparam name="T">The result type delivered to the subscriber.</typeparam>
        /// <param name="subscriber">The callback returning follow-up results.</param>
        /// <returns>A handle that detaches the subscriber when disposed.</returns>
        public IDisposable SubscribeAfterReactions<T>(
            Func<IReadOnlyList<T>, List<GameResult>> subscriber
        )
            where T : GameResult
        {
            if (subscriber == null)
                throw new ArgumentNullException(nameof(subscriber));

            return SubscribeAfterReactions<T>(results =>
            {
                Publish(subscriber(results));
            });
        }

        /// <summary>
        /// Includes an operation's command-owned facts in ordinary reaction delivery until disposed.
        /// Sources run when ordinary waves settle, before after-reaction callbacks and observers.
        /// Each source must return and clear its pending facts, returning null or an empty list
        /// when no work remains. Registration snapshots change only when scopes open or close.
        /// </summary>
        /// <param name="source">The operation-owned provider of completed pending facts.</param>
        /// <returns>A scope that stops collecting from this provider when disposed.</returns>
        public IDisposable UsePendingResultSource(Func<IReadOnlyList<GameResult>> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            PendingResultSource registration = new(this, source);
            PendingResultSource[] updated = new PendingResultSource[_pendingSources.Length + 1];
            Array.Copy(_pendingSources, updated, _pendingSources.Length);
            updated[updated.Length - 1] = registration;
            _pendingSources = updated;
            return registration;
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

            Subscription subscription = new Subscription(observer, _observers);
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
        /// Reactions registered after ordinary delivery settle before terminal observers run.
        /// Exceptions propagate, abandoning the unfinished delivery as in the existing processor.
        /// </summary>
        /// <param name="results">The completed changes; null collections and entries are ignored.</param>
        /// <returns>The settled results, or an empty list when queued by an active delivery.</returns>
        public List<GameResult> Publish(IEnumerable<GameResult> results)
        {
            List<GameResult> incomingResults =
                results?.Where(result => result != null).ToList() ?? new List<GameResult>();
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
                    int completedReactionCount = 0;
                    while (true)
                    {
                        while (_pendingResults.Count > 0)
                        {
                            List<GameResult> currentResults = new(_pendingResults);
                            _pendingResults.Clear();
                            settledResults.AddRange(currentResults);
                            foreach (Subscription subscriber in _subscribers.ToArray())
                                subscriber.Deliver(currentResults);
                        }

                        if (CollectPendingResultSources())
                            continue;

                        if (
                            _afterReactions.Count == 0
                            || completedReactionCount == settledResults.Count
                        )
                            break;

                        IReadOnlyList<GameResult> completedResults =
                            completedReactionCount == 0
                                ? settledResults
                                : settledResults.GetRange(
                                    completedReactionCount,
                                    settledResults.Count - completedReactionCount
                                );
                        completedReactionCount = settledResults.Count;
                        foreach (Subscription subscriber in _afterReactions.ToArray())
                            subscriber.Deliver(completedResults);
                    }

                    resolvedResults.AddRange(settledResults);
                    foreach (Subscription observer in _observers.ToArray())
                        observer.Deliver(settledResults);
                } while (_pendingResults.Count > 0 || CollectPendingResultSources());

                return resolvedResults;
            }
            finally
            {
                _pendingResults.Clear();
                _isPublishing = false;
            }
        }

        /// <summary>
        /// Collects operation-owned facts without recursively delivering another result wave.
        /// </summary>
        /// <returns>Whether facts or registration changes require another ordinary settlement pass.</returns>
        private bool CollectPendingResultSources()
        {
            PendingResultSource[] sources = _pendingSources;
            foreach (PendingResultSource source in sources)
                source.Collect(_pendingResults);
            return _pendingResults.Count > 0 || !ReferenceEquals(sources, _pendingSources);
        }

        /// <summary>
        /// Removes a disposed source while retaining the other operation scopes in order.
        /// </summary>
        /// <param name="source">The registration being detached.</param>
        private void RemovePendingResultSource(PendingResultSource source)
        {
            int index = Array.IndexOf(_pendingSources, source);
            if (index < 0)
                return;
            if (_pendingSources.Length == 1)
            {
                _pendingSources = Array.Empty<PendingResultSource>();
                return;
            }
            PendingResultSource[] updated = new PendingResultSource[_pendingSources.Length - 1];
            Array.Copy(_pendingSources, 0, updated, 0, index);
            Array.Copy(_pendingSources, index + 1, updated, index, updated.Length - index);
            _pendingSources = updated;
        }

        /// <summary>
        /// Registers a type-filtered callback in the selected delivery phase.
        /// </summary>
        /// <typeparam name="T">The result type accepted by the callback.</typeparam>
        /// <param name="callback">The callback invoked for nonempty matching batches.</param>
        /// <param name="subscriptions">The ordered subscriptions for the selected phase.</param>
        /// <returns>The disposable registration.</returns>
        private static IDisposable Register<T>(
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
                        callback(matchingResults);
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

        /// <summary>
        /// Owns a pending-fact provider for one explicitly scoped operation.
        /// </summary>
        private sealed class PendingResultSource : IDisposable
        {
            private readonly GameResultBus _owner;
            private readonly Func<IReadOnlyList<GameResult>> _source;
            private bool _isDisposed;

            /// <summary>
            /// Creates one operation-owned registration.
            /// </summary>
            /// <param name="owner">The bus that owns this registration.</param>
            /// <param name="source">The provider returning completed pending facts.</param>
            internal PendingResultSource(
                GameResultBus owner,
                Func<IReadOnlyList<GameResult>> source
            )
            {
                _owner = owner;
                _source = source;
            }

            /// <summary>
            /// Appends completed facts unless the operation has detached its source.
            /// </summary>
            /// <param name="pending">The next ordinary reaction wave.</param>
            internal void Collect(List<GameResult> pending)
            {
                if (_isDisposed)
                    return;
                IReadOnlyList<GameResult> results = _source();
                if (results == null)
                    return;
                for (int index = 0; index < results.Count; index++)
                    if (results[index] != null)
                        pending.Add(results[index]);
            }

            /// <summary>
            /// Detaches this source; repeated disposal leaves the remaining scopes unchanged.
            /// </summary>
            public void Dispose()
            {
                if (_isDisposed)
                    return;
                _isDisposed = true;
                _owner.RemovePendingResultSource(this);
            }
        }
    }
}
