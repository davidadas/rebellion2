using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Game.Rules;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Holds source attribution, a triggering result, and bindings for one game-rule execution.
    /// </summary>
    public sealed class GameEvaluationContext
    {
        private readonly Dictionary<string, object> _bindings = new Dictionary<string, object>(
            StringComparer.Ordinal
        );
        private readonly List<GameResult> _results = new List<GameResult>();

        public string SourceID { get; }
        public string SourceEventInstanceID { get; }
        public string SourceKind { get; }
        public GameResult TriggerResult { get; }
        public IReadOnlyList<GameResult> Results => _results;

        /// <summary>
        /// Creates an execution scope independently of event scheduling and activation history.
        /// </summary>
        /// <param name="sourceID">The stable identifier owning this execution and its grants.</param>
        /// <param name="triggerResult">The triggering result, when execution reacts to a result.</param>
        /// <param name="trigger">The trigger whose authored arguments should be bound.</param>
        /// <param name="sourceEventInstanceID">Optional event attribution for produced results.</param>
        /// <param name="sourceKind">The source label used in action diagnostics.</param>
        public GameEvaluationContext(
            string sourceID,
            GameResult triggerResult = null,
            GameTrigger trigger = null,
            string sourceEventInstanceID = null,
            string sourceKind = "Rule"
        )
        {
            SourceID = sourceID;
            SourceEventInstanceID = sourceEventInstanceID;
            SourceKind = sourceKind;
            TriggerResult = triggerResult;
            if (trigger != null)
                GameBindingResolver.Bind(trigger, this, triggerResult);
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
        /// <param name="name">The name.</param>
        /// <param name="value">Receives the value.</param>
        /// <returns>True when the named binding exists; otherwise false.</returns>
        public bool TryGetBinding(string name, out object value) =>
            _bindings.TryGetValue(name, out value);

        /// <summary>
        /// Attempts to resolve a binding reference as the requested type.
        /// </summary>
        /// <typeparam name="T">The expected binding type.</typeparam>
        /// <param name="reference">The exact binding name.</param>
        /// <param name="value">Receives the typed binding when found.</param>
        /// <returns>True when a compatible binding exists.</returns>
        public bool TryGetBindingReference<T>(string reference, out T value)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ArgumentException("A binding reference is required.", nameof(reference));
            if (TryGetBinding(reference, out object resolved) && resolved is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to resolve a binding reference without a type constraint.
        /// </summary>
        /// <param name="reference">The exact binding name.</param>
        /// <param name="value">Receives the binding when found.</param>
        /// <returns>True when the binding exists.</returns>
        public bool TryGetBindingReference(string reference, out object value)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ArgumentException("A binding reference is required.", nameof(reference));
            return TryGetBinding(reference, out value);
        }

        /// <summary>
        /// Resolves a binding reference as the requested type.
        /// </summary>
        /// <typeparam name="T">The expected binding type.</typeparam>
        /// <param name="reference">The exact binding name.</param>
        /// <returns>The typed binding, or the default value when it is absent or incompatible.</returns>
        public T GetBindingReference<T>(string reference)
        {
            return TryGetBindingReference(reference, out T value) ? value : default;
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
