using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Describes one concrete execution of a data-defined event.
    /// Scoped events receive the entity whose independent schedule activated them.
    /// </summary>
    public sealed class GameEventExecutionContext
    {
        private readonly List<GameResult> _results = new List<GameResult>();

        public GameEvent Event { get; }
        public GameEventState State { get; }
        public ISceneNode Target { get; }
        public GameResult TriggerResult { get; }
        public GameEventBindings Bindings { get; }
        public IReadOnlyList<GameResult> Results => _results;

        /// <summary>
        /// Creates the runtime context for one event activation.
        /// </summary>
        /// <param name="gameEvent">The event definition being executed.</param>
        /// <param name="state">Persistent scheduling state for this activation scope.</param>
        /// <param name="target">The selected planet or other targeted scene node.</param>
        /// <param name="triggerResult">The result that activated this event, if any.</param>
        /// <param name="trigger">The trigger definition that matched the result, if any.</param>
        public GameEventExecutionContext(
            GameEvent gameEvent,
            GameEventState state,
            ISceneNode target,
            GameResult triggerResult = null,
            GameEventTrigger trigger = null
        )
        {
            Event = gameEvent;
            State = state;
            Target = target;
            TriggerResult = triggerResult;
            Bindings = new GameEventBindings();
            Bind("target", target);
            Bind("trigger", triggerResult);
            trigger?.Bind(this, triggerResult);
        }

        /// <summary>
        /// Gets the scope target when it has the requested scene-node type.
        /// </summary>
        /// <typeparam name="T">The expected scene-node type.</typeparam>
        /// <returns>The typed target, or null when the target has another type.</returns>
        public T GetTarget<T>()
            where T : class, ISceneNode => Target as T;

        /// <summary>
        /// Stores a named value for subsequent actions in this activation.
        /// </summary>
        /// <param name="name">The stable binding name.</param>
        /// <param name="value">The value to expose; null values are ignored.</param>
        public void Bind(string name, object value)
        {
            Bindings.Set(name, value);
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
            return Bindings.TryGet(name, out value);
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
            Bindings.TryGet(name, out value);

        public bool TryGetBindingReference<T>(string reference, out T value)
        {
            return TryGetBinding(GetBindingName(reference), out value);
        }

        public bool TryGetBindingReference(string reference, out object value)
        {
            return TryGetBinding(GetBindingName(reference), out value);
        }

        public T GetBindingReference<T>(string reference)
        {
            return TryGetBindingReference(reference, out T value) ? value : default;
        }

        private static string GetBindingName(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) || reference[0] != '$')
                throw new InvalidOperationException("Binding references must begin with '$'.");
            return reference.Substring(1);
        }

        /// <summary>
        /// Records a result emitted during this activation for later actions to inspect.
        /// </summary>
        /// <param name="result">The emitted result; null values are ignored.</param>
        public void AddResult(GameResult result)
        {
            if (result != null)
                _results.Add(result);
        }
    }
}
