using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;
using Rebellion.Util.Logging;

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
            return Register(subscriber, _subscribers, "Result reaction callback");
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

            return Register<T>(
                results =>
                {
                    Publish(
                        InvokeMeasured(
                            subscriber,
                            results,
                            "Result reaction callback",
                            results.Count
                        )
                    );
                },
                _subscribers,
                "Result reaction forwarding",
                subscriber.Method.DeclaringType
            );
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
            return Register(observer, _observers, "Result observer callback");
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
                observer,
                _observers,
                "Result observer callback",
                null
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
            using var measurement = PerformanceLogger.Measure("Result bus publication");
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
        /// <param name="operation">The operation identifying the callback phase.</param>
        /// <param name="subjectType">The original callback's declaring type, when wrapped.</param>
        /// <returns>The disposable registration.</returns>
        private static IDisposable Register<T>(
            Action<IReadOnlyList<T>> callback,
            List<Subscription> subscriptions,
            string operation,
            Type subjectType = null
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
                    {
                        InvokeMeasured<IReadOnlyList<T>>(
                            callback,
                            matchingResults,
                            operation,
                            matchingResults.Count,
                            subjectType
                        );
                    }
                },
                subscriptions,
                "Result subscription dispatch",
                subjectType ?? callback.Method.DeclaringType
            );
            subscriptions.Add(subscription);
            return subscription;
        }

        /// <summary>
        /// Invokes an action snapshot in order with optional timing for each handler.
        /// </summary>
        /// <typeparam name="T">The callback argument type.</typeparam>
        /// <param name="callbacks">The nonnull multicast callback snapshot.</param>
        /// <param name="value">The existing callback argument.</param>
        /// <param name="operation">The operation identifying these callbacks.</param>
        /// <param name="items">The already known number of delivered results.</param>
        /// <param name="subjectType">The original declaring type for an internal forwarding wrapper.</param>
        private static void InvokeMeasured<T>(
            Action<T> callbacks,
            T value,
            string operation,
            long items,
            Type subjectType = null
        )
        {
            if (!PerformanceLogger.IsEnabled)
            {
                callbacks(value);
                return;
            }

            foreach (Action<T> callback in callbacks.GetInvocationList())
            {
                using var measurement = PerformanceLogger.Measure(
                    operation,
                    items: items,
                    subjectType: subjectType ?? callback.Method.DeclaringType
                );
                callback(value);
            }
        }

        /// <summary>
        /// Invokes a reaction snapshot in order and returns only its last handler's result.
        /// </summary>
        /// <typeparam name="T">The callback argument type.</typeparam>
        /// <param name="callbacks">The nonnull multicast reaction snapshot.</param>
        /// <param name="value">The existing callback argument.</param>
        /// <param name="operation">The operation identifying these callbacks.</param>
        /// <param name="items">The already known number of delivered results.</param>
        /// <returns>The last handler's returned results, without inspecting any earlier return.</returns>
        private static List<GameResult> InvokeMeasured<T>(
            Func<T, List<GameResult>> callbacks,
            T value,
            string operation,
            long items
        )
        {
            if (!PerformanceLogger.IsEnabled)
                return callbacks(value);

            List<GameResult> results = null;
            foreach (Func<T, List<GameResult>> callback in callbacks.GetInvocationList())
            {
                using var measurement = PerformanceLogger.Measure(
                    operation,
                    items: items,
                    subjectType: callback.Method.DeclaringType
                );
                results = callback(value);
            }
            return results;
        }

        /// <summary>
        /// Owns one callback registration without exposing its delivery internals to subscribers.
        /// </summary>
        private sealed class Subscription : IDisposable
        {
            private readonly Action<IReadOnlyList<GameResult>> _callback;
            private readonly List<Subscription> _subscriptions;
            private readonly string _operation;
            private readonly Type _subjectType;
            private bool _isDisposed;

            /// <summary>
            /// Creates a registration for one ordered delivery phase.
            /// </summary>
            /// <param name="callback">The type-filtered delivery callback.</param>
            /// <param name="subscriptions">The collection owning this registration.</param>
            /// <param name="operation">The operation identifying this delivery.</param>
            /// <param name="subjectType">The declaring type for a forwarding wrapper, or null to use each handler's type.</param>
            internal Subscription(
                Action<IReadOnlyList<GameResult>> callback,
                List<Subscription> subscriptions,
                string operation,
                Type subjectType
            )
            {
                _callback = callback;
                _subscriptions = subscriptions;
                _operation = operation;
                _subjectType = subjectType;
            }

            /// <summary>
            /// Delivers a batch unless the subscriber has detached.
            /// </summary>
            /// <param name="results">The current reaction wave or settled batch.</param>
            internal void Deliver(IReadOnlyList<GameResult> results)
            {
                if (!_isDisposed)
                {
                    InvokeMeasured(_callback, results, _operation, results.Count, _subjectType);
                }
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
