using System;
using System.Collections.Generic;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Holds named values exposed during one event activation.
    /// </summary>
    public sealed class GameEventBindings
    {
        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(
            StringComparer.Ordinal
        );

        public void Set(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A binding name is required.", nameof(name));
            if (value != null)
                _values[name] = value;
        }

        public bool TryGet<T>(string name, out T value)
        {
            if (_values.TryGetValue(name, out object binding) && binding is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGet(string name, out object value) => _values.TryGetValue(name, out value);
    }
}
