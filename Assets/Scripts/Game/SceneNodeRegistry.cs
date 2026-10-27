using System;
using System.Collections;
using System.Collections.Generic;
using Rebellion.SceneGraph;
using Rebellion.Util.Serialization;

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
        private readonly List<ISceneNode> _entries = new();
        private readonly List<int> _freeSlots = new();

        public bool HasSerializedOrder { get; private set; }
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
                }
                else
                {
                    position = _entries.Count;
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
            _entries[position] = null;
            _freeSlots.Add(position);
            return true;
        }

        /// <summary>
        /// Clears an uninitialized registry before its initial hierarchy registration.
        /// </summary>
        public void Reset()
        {
            _nodes.Clear();
            _positions.Clear();
            _entries.Clear();
            _freeSlots.Clear();
            HasSerializedOrder = false;
        }

        /// <summary>
        /// Captures existing object references and logical positions once at a save boundary.
        /// </summary>
        /// <returns>The live objects and their positions, without copied identity strings.</returns>
        public IReadOnlyList<OrderedObject> CaptureObjectOrder()
        {
            List<OrderedObject> objects = new(_nodes.Count);
            for (int index = 0; index < _entries.Count; index++)
                if (_entries[index] != null)
                    objects.Add(new OrderedObject(_entries[index], index));
            return objects;
        }

        /// <summary>
        /// Captures the vacant-position reuse stack at a save boundary.
        /// </summary>
        /// <returns>An independent reuse stack with the next reused position last.</returns>
        public IReadOnlyList<int> CaptureVacantPositions() => _freeSlots.ToArray();

        /// <summary>
        /// Restores the collection from positions carried by the decoded object records.
        /// Validation finishes before any existing live registration is replaced.
        /// </summary>
        /// <param name="objects">The decoded objects and positions.</param>
        /// <param name="vacancies">The captured vacant-position reuse stack.</param>
        public void RestoreObjectOrder(
            IReadOnlyList<OrderedObject> objects,
            IReadOnlyList<int> vacancies
        )
        {
            if (objects == null || vacancies == null)
                throw new ArgumentException("Serialized registration order is incomplete.");
            int capacity = checked(objects.Count + vacancies.Count);
            ISceneNode[] entries = new ISceneNode[capacity];
            Dictionary<string, int> positions = new(StringComparer.Ordinal);
            foreach (OrderedObject entry in objects)
            {
                if (
                    entry.Value is not ISceneNode node
                    || entry.Position < 0
                    || entry.Position >= capacity
                    || entries[entry.Position] != null
                )
                    throw new ArgumentException(
                        "Serialized registration contains an invalid or repeated position."
                    );
                string identity = node.InstanceID;
                if (string.IsNullOrEmpty(identity) || positions.ContainsKey(identity))
                    throw new ArgumentException(
                        "Serialized registration contains an invalid or repeated identity."
                    );
                positions.Add(identity, entry.Position);
                entries[entry.Position] = node;
            }
            HashSet<int> free = new();
            foreach (int position in vacancies)
                if (
                    position < 0
                    || position >= capacity
                    || entries[position] != null
                    || !free.Add(position)
                )
                    throw new ArgumentException(
                        "Serialized registration contains an invalid or repeated vacancy."
                    );
            Reset();
            _entries.AddRange(entries);
            _freeSlots.AddRange(vacancies);
            foreach (KeyValuePair<string, int> entry in positions)
            {
                _positions.Add(entry.Key, entry.Value);
                _nodes.Add(entry.Key, entries[entry.Value]);
            }
            HasSerializedOrder = true;
        }

        /// <summary>
        /// Rebinds decoded recovery objects without changing their serialized positions.
        /// Legacy graphs retain the supplied live-node sequence.
        /// </summary>
        /// <param name="nodes">The decoded nodes belonging to the restored graph.</param>
        public void RestoreRegisteredNodes(IEnumerable<ISceneNode> nodes)
        {
            if (nodes == null)
                throw new ArgumentNullException(nameof(nodes));
            if (HasSerializedOrder)
            {
                List<OrderedObject> objects = new();
                foreach (ISceneNode node in nodes)
                {
                    if (node == null || !_positions.TryGetValue(node.InstanceID, out int position))
                        throw new ArgumentException(
                            "A decoded registration has no serialized position.",
                            nameof(nodes)
                        );
                    objects.Add(new OrderedObject(node, position));
                }
                RestoreObjectOrder(objects, CaptureVacantPositions());
                return;
            }
            SceneNodeRegistry restored = new();
            foreach (ISceneNode node in nodes)
                restored.Add(node);
            Reset();
            foreach (ISceneNode node in restored.Values)
                Add(node);
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
                        _entries[index].InstanceID,
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
                    yield return _entries[index].InstanceID;
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
