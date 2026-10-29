using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Rebellion.Util.Serialization
{
    /// <summary>
    /// Owns order metadata and reference identity for one serialization operation.
    /// No context or live graph is shared between operations or threads.
    /// </summary>
    public sealed class SerializationOrderContext
    {
        private const string _namespace = "urn:rebellion2:object-order";
        private const string _prefix = "o";
        private readonly IOrderedObjectGraph _graph;
        private readonly Dictionary<object, int> _writeOrder;
        private readonly HashSet<int> _written = new();
        private readonly Dictionary<int, object> _readObjects = new();
        private readonly IReadOnlyList<int> _writeVacancies;
        private IReadOnlyList<int> _readVacancies = Array.Empty<int>();
        private int? _readSlotCount;
        private bool _hasReadVacancies;

        public int SlotCount => _writeOrder.Count + _writeVacancies.Count;
        public IReadOnlyList<int> Vacancies => _writeVacancies;

        /// <summary>
        /// Captures write order once, or prepares an independent read operation.
        /// </summary>
        /// <param name="graph">The stable graph to write, or null when decoding.</param>
        public SerializationOrderContext(IOrderedObjectGraph graph = null)
        {
            _graph = graph;
            _writeOrder = new Dictionary<object, int>(ObjectReferences.Instance);
            _writeVacancies = graph?.CaptureVacantPositions() ?? Array.Empty<int>();
            if (graph == null)
                return;
            foreach (OrderedObject entry in graph.CaptureObjectOrder())
                _writeOrder.Add(entry.Value, entry.Position);
            ValidatePositions(_writeOrder.Values, _writeVacancies, SlotCount);
        }

        /// <summary>
        /// Resolves the captured position of an existing object without an identity lookup or scan.
        /// </summary>
        /// <param name="value">The existing graph object.</param>
        /// <param name="position">Its captured logical position when present.</param>
        /// <returns>True when the object belongs to the ordered graph.</returns>
        public bool TryGetPosition(object value, out int position) =>
            _writeOrder.TryGetValue(value, out position);

        /// <summary>
        /// Marks an existing object record as written by a contract-based serializer.
        /// </summary>
        /// <param name="value">The existing object being written.</param>
        /// <returns>Its captured position, or null when it is outside the ordered graph.</returns>
        public int? WritePosition(object value)
        {
            if (!TryGetPosition(value, out int position))
                return null;
            _written.Add(position);
            return position;
        }

        /// <summary>
        /// Writes a reference when an ordered object has already been written in this operation.
        /// </summary>
        /// <param name="value">The object about to be written.</param>
        /// <param name="writer">The current XML writer.</param>
        /// <param name="name">The existing element name used for this value.</param>
        /// <returns>True when an existing object was written as a reference.</returns>
        public bool WriteReference(object value, XmlWriter writer, string name)
        {
            if (!TryGetPosition(value, out int position) || _written.Add(position))
                return false;
            writer.WriteStartElement(name ?? value.GetType().Name);
            writer.WriteAttributeString(
                _prefix,
                "ref",
                _namespace,
                position.ToString(CultureInfo.InvariantCulture)
            );
            writer.WriteEndElement();
            return true;
        }

        /// <summary>
        /// Adds order metadata to the current record without writing an additional identity list.
        /// </summary>
        /// <param name="value">The object whose existing record is open.</param>
        /// <param name="writer">The current XML writer.</param>
        public void WriteMetadata(object value, XmlWriter writer)
        {
            if (ReferenceEquals(value, _graph))
            {
                writer.WriteAttributeString(
                    _prefix,
                    "slots",
                    _namespace,
                    SlotCount.ToString(CultureInfo.InvariantCulture)
                );
                if (_writeVacancies.Count > 0)
                    writer.WriteAttributeString(
                        _prefix,
                        "vacancies",
                        _namespace,
                        string.Join(",", _writeVacancies)
                    );
            }
            if (TryGetPosition(value, out int position))
                writer.WriteAttributeString(
                    _prefix,
                    "position",
                    _namespace,
                    position.ToString(CultureInfo.InvariantCulture)
                );
        }

        /// <summary>
        /// Resolves a previously decoded ordered object using its numeric position.
        /// </summary>
        /// <param name="type">The declared type required by the containing member.</param>
        /// <param name="reader">The XML reader at the potential reference element.</param>
        /// <param name="value">The existing decoded object when a reference is present.</param>
        /// <returns>True when an ordered reference was consumed.</returns>
        public bool ReadReference(Type type, XmlReader reader, out object value)
        {
            value = null;
            string reference = reader.GetAttribute("ref", _namespace);
            if (reference == null)
                return false;
            int position = int.Parse(reference, CultureInfo.InvariantCulture);
            if (
                !reader.IsEmptyElement
                || !_readObjects.TryGetValue(position, out value)
                || !type.IsInstanceOfType(value)
            )
                throw new InvalidDataException("An ordered object reference is invalid.");
            reader.Read();
            return true;
        }

        /// <summary>
        /// Records the metadata on a newly created object before its children are decoded.
        /// </summary>
        /// <param name="value">The object being populated.</param>
        /// <param name="reader">The XML reader at its existing record.</param>
        public void ReadMetadata(object value, XmlReader reader)
        {
            if (!reader.HasAttributes)
                return;
            string position = reader.GetAttribute("position", _namespace);
            if (position != null)
                ReadPosition(value, int.Parse(position, CultureInfo.InvariantCulture));
            string slots = reader.GetAttribute("slots", _namespace);
            if (slots != null)
            {
                if (value is not IOrderedObjectGraph)
                    throw new InvalidDataException(
                        "Ordered graph metadata requires an ordered graph root."
                    );
                ReadSlotCount(int.Parse(slots, CultureInfo.InvariantCulture));
            }
            string vacancies = reader.GetAttribute("vacancies", _namespace);
            if (vacancies != null)
            {
                List<int> positions = new();
                foreach (string item in vacancies.Split(','))
                    positions.Add(int.Parse(item, CultureInfo.InvariantCulture));
                ReadVacancies(positions);
            }
        }

        /// <summary>
        /// Associates a decoded record with the position written on that record.
        /// </summary>
        /// <param name="value">The decoded object instance.</param>
        /// <param name="position">The saved logical position.</param>
        public void ReadPosition(object value, int position)
        {
            if (value == null || position < 0 || _readObjects.ContainsKey(position))
                throw new InvalidDataException(
                    "The serialized object order contains an invalid or repeated position."
                );
            _readObjects.Add(position, value);
        }

        /// <summary>
        /// Records the root's declared logical slot count for completeness validation.
        /// </summary>
        /// <param name="count">The declared live-plus-vacant slot count.</param>
        public void ReadSlotCount(int count)
        {
            if (count < 0 || _readSlotCount.HasValue)
                throw new InvalidDataException(
                    "The serialized object slot count is invalid or repeated."
                );
            _readSlotCount = count;
        }

        /// <summary>
        /// Records the sparse vacancy reuse stack without repeating live object identities.
        /// </summary>
        /// <param name="positions">The saved vacant-position reuse stack.</param>
        public void ReadVacancies(IReadOnlyList<int> positions)
        {
            if (positions == null || _hasReadVacancies)
                throw new InvalidDataException(
                    "The serialized vacancy order is invalid or repeated."
                );
            _readVacancies = new List<int>(positions);
            _hasReadVacancies = true;
        }

        /// <summary>
        /// Reinstates complete order only after all object records have been populated.
        /// Documents without order metadata retain their existing legacy reconstruction path.
        /// </summary>
        /// <param name="graph">The independently decoded graph receiving its original object order.</param>
        public void Restore(IOrderedObjectGraph graph)
        {
            if (!_readSlotCount.HasValue)
            {
                if (_readObjects.Count != 0 || _hasReadVacancies)
                    throw new InvalidDataException(
                        "Serialized object order is missing its root slot count."
                    );
                return;
            }
            if (graph == null || _readSlotCount != _readObjects.Count + _readVacancies.Count)
                throw new InvalidDataException("Serialized object order is incomplete.");
            ValidatePositions(_readObjects.Keys, _readVacancies, _readSlotCount.Value);
            List<OrderedObject> objects = new(_readObjects.Count);
            foreach (KeyValuePair<int, object> entry in _readObjects)
                objects.Add(new OrderedObject(entry.Value, entry.Key));
            graph.RestoreObjectOrder(objects, _readVacancies);
        }

        /// <summary>
        /// Ensures persistence included every ordered object captured at the operation boundary.
        /// </summary>
        public void CompleteWrite()
        {
            if (_written.Count != _writeOrder.Count)
                throw new InvalidDataException("The persisted graph omitted registered objects.");
        }

        /// <summary>
        /// Validates complete position coverage before a provider can allocate its restored storage.
        /// The declared capacity must be supported by the actual number of decoded records.
        /// </summary>
        /// <param name="positions">The live object positions.</param>
        /// <param name="vacancies">The vacant-position reuse stack.</param>
        /// <param name="capacity">The declared live-plus-vacant slot count.</param>
        private static void ValidatePositions(
            IEnumerable<int> positions,
            IReadOnlyList<int> vacancies,
            int capacity
        )
        {
            HashSet<int> occupied = new();
            foreach (int position in positions)
                if (position < 0 || position >= capacity || !occupied.Add(position))
                    throw new InvalidDataException(
                        "Serialized object order has an invalid or repeated position."
                    );
            foreach (int position in vacancies)
                if (position < 0 || position >= capacity || !occupied.Add(position))
                    throw new InvalidDataException(
                        "Serialized object order has an invalid or repeated vacancy."
                    );
            if (occupied.Count != capacity)
                throw new InvalidDataException("Serialized object order is incomplete.");
        }

        /// <summary>
        /// Compares actual graph instances independently of model identity or value equality.
        /// </summary>
        private sealed class ObjectReferences : IEqualityComparer<object>
        {
            public static readonly ObjectReferences Instance = new();

            /// <summary>
            /// Compares the actual object references.
            /// </summary>
            /// <param name="x">The first object.</param>
            /// <param name="y">The second object.</param>
            /// <returns>True when both references identify the same object.</returns>
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            /// <summary>
            /// Returns a stable hash for one object reference during the operation.
            /// </summary>
            /// <param name="obj">The object reference.</param>
            /// <returns>The runtime reference hash.</returns>
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
