using System;
using System.Collections;
using System.Collections.Generic;
using Rebellion.SceneGraph;

namespace Rebellion.Game
{
    /// <summary>
    /// Owns entity lookup and the logical slot order used by registered-node enumeration.
    /// Deleted slots are reused in last-removed order independently of dictionary storage.
    /// </summary>
    internal sealed class SceneNodeRegistry : IReadOnlyDictionary<string, ISceneNode>
    {
        private readonly Dictionary<string, ISceneNode> _nodes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _positions = new(StringComparer.Ordinal);
        private readonly List<string> _slotIDs = new();
        private readonly List<ISceneNode> _entries = new();
        private readonly List<int> _freeSlots = new();

        public int Count => _nodes.Count;
        public ISceneNode this[string key] => _nodes[key];
        public IEnumerable<string> Keys => EnumerateKeys();
        public IEnumerable<ISceneNode> Values => EnumerateValues();

        /// <summary>
        /// Registers a live entity in its restored slot or the next reusable logical slot.
        /// </summary>
        /// <param name="node">The entity whose assigned identity owns the slot.</param>
        public void Add(ISceneNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            string identity = node.InstanceID;
            if (string.IsNullOrEmpty(identity))
                throw new ArgumentException(
                    "A registered entity requires an identity.",
                    nameof(node)
                );
            _nodes.Add(identity, node);
            if (!_positions.TryGetValue(identity, out int position))
            {
                if (_freeSlots.Count > 0)
                {
                    position = _freeSlots[_freeSlots.Count - 1];
                    _freeSlots.RemoveAt(_freeSlots.Count - 1);
                    _slotIDs[position] = identity;
                }
                else
                {
                    position = _slotIDs.Count;
                    _slotIDs.Add(identity);
                    _entries.Add(null);
                }
                _positions.Add(identity, position);
            }
            _entries[position] = node;
        }

        /// <summary>
        /// Removes a live entity and records its slot for deterministic subsequent reuse.
        /// </summary>
        /// <param name="identity">The registered identity to remove.</param>
        /// <returns>True when a live registration was removed.</returns>
        public bool Remove(string identity)
        {
            if (!_nodes.Remove(identity))
                return false;
            int position = _positions[identity];
            _positions.Remove(identity);
            _slotIDs[position] = string.Empty;
            _entries[position] = null;
            _freeSlots.Add(position);
            return true;
        }

        /// <summary>
        /// Clears live references and initializes the supplied saved logical layout.
        /// </summary>
        /// <param name="state">The saved layout, or null for a fresh hierarchy registration.</param>
        public void Reset(SceneNodeRegistryState state = null)
        {
            SceneNodeRegistryState copy = CopyValidatedState(state);
            _nodes.Clear();
            _positions.Clear();
            _slotIDs.Clear();
            _entries.Clear();
            _freeSlots.Clear();
            if (copy == null)
                return;
            for (int index = 0; index < copy.SlotIDs.Count; index++)
            {
                string identity = copy.SlotIDs[index];
                _slotIDs.Add(identity);
                _entries.Add(null);
                if (!string.IsNullOrEmpty(identity))
                    _positions.Add(identity, index);
            }
            _freeSlots.AddRange(copy.FreeSlots);
        }

        /// <summary>
        /// Captures logical slot identities and the exact continuation of deleted-slot reuse.
        /// </summary>
        /// <returns>An independent serializable layout without live entity references.</returns>
        public SceneNodeRegistryState CaptureState()
        {
            return new SceneNodeRegistryState
            {
                SlotIDs = new List<string>(_slotIDs),
                FreeSlots = new List<int>(_freeSlots),
            };
        }

        /// <summary>
        /// Copies a complete layout after checking unique identities and reusable-slot consistency.
        /// </summary>
        /// <param name="state">The layout to validate, or null when no layout was persisted.</param>
        /// <returns>The validated independent layout, or null for legacy data.</returns>
        internal static SceneNodeRegistryState CopyValidatedState(SceneNodeRegistryState state)
        {
            if (state == null)
                return null;
            if (state.SlotIDs == null || state.FreeSlots == null)
                throw new ArgumentException(
                    "The scene registry layout is incomplete.",
                    nameof(state)
                );
            HashSet<string> identities = new(StringComparer.Ordinal);
            HashSet<int> vacancies = new();
            for (int index = 0; index < state.SlotIDs.Count; index++)
            {
                string identity = state.SlotIDs[index];
                if (string.IsNullOrEmpty(identity))
                    vacancies.Add(index);
                else if (!identities.Add(identity))
                    throw new ArgumentException(
                        "The scene registry repeats an identity.",
                        nameof(state)
                    );
            }
            foreach (int position in state.FreeSlots)
            {
                if (!vacancies.Remove(position))
                    throw new ArgumentException(
                        "The scene registry has an invalid reusable slot.",
                        nameof(state)
                    );
            }
            if (vacancies.Count != 0)
                throw new ArgumentException(
                    "The scene registry omits a reusable slot.",
                    nameof(state)
                );
            return new SceneNodeRegistryState
            {
                SlotIDs = new List<string>(state.SlotIDs),
                FreeSlots = new List<int>(state.FreeSlots),
            };
        }

        /// <summary>
        /// Determines whether an identity currently resolves to a live registration.
        /// </summary>
        /// <param name="key">The identity to locate.</param>
        /// <returns>True when a live registration exists.</returns>
        public bool ContainsKey(string key) => _nodes.ContainsKey(key);

        /// <summary>
        /// Resolves a live registration without depending on its logical position.
        /// </summary>
        /// <param name="key">The identity to locate.</param>
        /// <param name="value">The registered entity when found.</param>
        /// <returns>True when the identity was registered.</returns>
        public bool TryGetValue(string key, out ISceneNode value) =>
            _nodes.TryGetValue(key, out value);

        /// <summary>
        /// Enumerates live registrations in their explicit logical order.
        /// </summary>
        /// <returns>The ordered registrations.</returns>
        public IEnumerator<KeyValuePair<string, ISceneNode>> GetEnumerator()
        {
            for (int index = 0; index < _entries.Count; index++)
                if (_entries[index] != null)
                    yield return new KeyValuePair<string, ISceneNode>(
                        _slotIDs[index],
                        _entries[index]
                    );
        }

        /// <summary>
        /// Enumerates live registrations through the non-generic collection contract.
        /// </summary>
        /// <returns>The ordered registrations.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Enumerates live identities without exposing storage collections to callers.
        /// </summary>
        /// <returns>The identities in logical order.</returns>
        private IEnumerable<string> EnumerateKeys()
        {
            for (int index = 0; index < _entries.Count; index++)
                if (_entries[index] != null)
                    yield return _slotIDs[index];
        }

        /// <summary>
        /// Enumerates live references without an additional lookup for every entity.
        /// </summary>
        /// <returns>The entities in logical order.</returns>
        private IEnumerable<ISceneNode> EnumerateValues()
        {
            foreach (ISceneNode entry in _entries)
                if (entry != null)
                    yield return entry;
        }
    }
}
