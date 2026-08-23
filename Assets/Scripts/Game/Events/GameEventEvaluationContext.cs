using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Holds the trigger result, state, and bindings available during one event evaluation.
    /// Scoped events receive the entity whose independent schedule activated them.
    /// </summary>
    public sealed class GameEventEvaluationContext
    {
        private readonly Dictionary<string, object> _bindings = new Dictionary<string, object>(
            StringComparer.Ordinal
        );
        private readonly List<GameResult> _results = new List<GameResult>();

        public GameEvent Event { get; }
        public GameEventState State { get; }
        public GameResult TriggerResult { get; }
        public IReadOnlyList<GameResult> Results => _results;

        /// <summary>
        /// Creates the runtime context for one event evaluation.
        /// </summary>
        /// <param name="gameEvent">The event definition being activated.</param>
        /// <param name="state">Persistent scheduling state for this event.</param>
        /// <param name="triggerResult">The result that activated this event, if any.</param>
        /// <param name="trigger">The trigger definition that matched the result, if any.</param>
        public GameEventEvaluationContext(
            GameEvent gameEvent,
            GameEventState state,
            GameResult triggerResult = null,
            GameEventTrigger trigger = null
        )
        {
            Event = gameEvent;
            State = state;
            TriggerResult = triggerResult;
            trigger?.Bind(this, triggerResult);
        }

        /// <summary>
        /// Stores a named value for subsequent evaluation stages.
        /// </summary>
        /// <param name="name">The stable binding name.</param>
        /// <param name="value">The value to expose, including null when a typed trigger argument has no value.</param>
        public void Bind(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A binding name is required.", nameof(name));
            if (!_bindings.TryAdd(name, value))
                throw new InvalidOperationException($"Binding '{name}' is already defined.");
        }

        /// <summary>
        /// Attempts to read a named binding with the requested reference type.
        /// </summary>
        /// <typeparam name="T">The expected binding type.</typeparam>
        /// <param name="name">The binding name.</param>
        /// <param name="value">Receives the typed binding when found.</param>
        /// <returns>True when a compatible binding exists.</returns>
        public bool TryGetBinding<T>(string name, out T value)
        {
            if (_bindings.TryGetValue(name, out object binding) && binding is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Gets a named binding with the requested reference type.
        /// </summary>
        /// <typeparam name="T">The expected binding type.</typeparam>
        /// <param name="name">The binding name.</param>
        /// <returns>The typed binding, or null when it is absent or incompatible.</returns>
        public T GetBinding<T>(string name) => TryGetBinding(name, out T value) ? value : default;

        /// <summary>
        /// Attempts to read a binding without imposing a compile-time value type.
        /// </summary>
        public bool TryGetBinding(string name, out object value) =>
            _bindings.TryGetValue(name, out value);

        /// <summary>Attempts to resolve one explicit binding reference as the requested type.</summary>
        public bool TryGetBindingReference<T>(string reference, out T value)
        {
            object resolved = ResolveBindingReference(reference);
            if (resolved is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>Attempts to resolve one explicit binding reference without a type constraint.</summary>
        public bool TryGetBindingReference(string reference, out object value)
        {
            value = ResolveBindingReference(reference);
            return value != null;
        }

        /// <summary>Gets one explicit binding reference as the requested type.</summary>
        public T GetBindingReference<T>(string reference)
        {
            return TryGetBindingReference(reference, out T value) ? value : default;
        }

        /// <summary>Removes the required dollar-sign prefix from a binding reference.</summary>
        private static string GetBindingName(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) || reference[0] != '$')
                throw new InvalidOperationException("Binding references must begin with '$'.");
            return reference.Substring(1);
        }

        /// <summary>Resolves one explicitly authored binding from the evaluation context.</summary>
        private object ResolveBindingReference(string reference)
        {
            string name = GetBindingName(reference);
            if (name.Contains("."))
                throw new InvalidOperationException(
                    "Binding references cannot traverse object properties. Bind the required trigger argument explicitly."
                );
            return _bindings.TryGetValue(name, out object value) ? value : null;
        }

        /// <summary>
        /// Records a result emitted during this evaluation for later actions to inspect.
        /// </summary>
        /// <param name="result">The emitted result; null values are ignored.</param>
        public void AddResult(GameResult result)
        {
            if (result != null)
                _results.Add(result);
        }
    }
}
