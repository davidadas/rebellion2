using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Delivers result batches and their queued reactions within one running game.
    /// </summary>
    public sealed class GameResultBus
    {
        private readonly List<Subscription> _subscribers = new();
        private readonly List<Subscription> _observers = new();
        private readonly List<GameResult> _pendingResults = new();
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
        /// Registers a type-filtered callback in the selected delivery phase.
        /// </summary>
        /// <typeparam name="T">The result type accepted by the callback.</typeparam>
        /// <param name="callback">The callback invoked for nonempty matching batches.</param>
        /// <param name="subscriptions">The ordered reaction or observer subscriptions.</param>
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
    }
}
