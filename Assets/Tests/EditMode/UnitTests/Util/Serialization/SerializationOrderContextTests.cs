using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Rebellion.Util.Serialization;

namespace Rebellion.Tests.Util.Serialization
{
    [TestFixture]
    public sealed class SerializationOrderContextTests
    {
        [Test]
        public void Serialize_LogicalOrderDiffersFromMemberOrder_PreservesBothOrders()
        {
            OrderedValue first = new() { Name = "first" };
            OrderedValue second = new() { Name = "second" };
            OrderedGraph graph = new()
            {
                Items = new List<OrderedValue> { second, first },
                ObjectOrder = new List<OrderedObject> { new(first, 0), new(second, 1) },
            };
            OrderedGraph restored = RoundTrip(graph);
            CollectionAssert.AreEqual(
                new[] { "second", "first" },
                restored.Items.Select(item => item.Name)
            );
            CollectionAssert.AreEqual(
                new[] { "first", "second" },
                restored
                    .ObjectOrder.OrderBy(entry => entry.Position)
                    .Select(entry => ((OrderedValue)entry.Value).Name)
            );
        }

        [Test]
        public void Deserialize_SharedOrderedObject_PreservesSameInstance()
        {
            OrderedValue item = new() { Name = "one-record" };
            OrderedGraph graph = new()
            {
                Items = new List<OrderedValue> { item, item },
                ObjectOrder = new List<OrderedObject> { new(item, 0) },
            };
            string xml = Serialize(graph);
            Assert.AreEqual(1, XDocument.Parse(xml).Descendants("Name").Count());
            OrderedGraph restored = Deserialize(xml);
            Assert.AreSame(restored.Items[0], restored.Items[1]);
            Assert.AreSame(restored.Items[0], restored.ObjectOrder[0].Value);
        }

        [Test]
        public void Deserialize_VacantPositions_PreservesReuseStackOrder()
        {
            OrderedValue first = new() { Name = "first" };
            OrderedValue second = new() { Name = "second" };
            OrderedGraph graph = new()
            {
                Items = new List<OrderedValue> { first, second },
                ObjectOrder = new List<OrderedObject> { new(first, 1), new(second, 3) },
                Vacancies = new List<int> { 2, 0 },
            };
            OrderedGraph restored = RoundTrip(graph);
            CollectionAssert.AreEqual(
                new[] { 1, 3 },
                restored.ObjectOrder.Select(entry => entry.Position)
            );
            CollectionAssert.AreEqual(new[] { 2, 0 }, restored.Vacancies);
        }

        [Test]
        public void Deserialize_OutOfRangePosition_RejectsInvalidOrder()
        {
            OrderedValue item = new() { Name = "first" };
            OrderedGraph graph = new()
            {
                Items = new List<OrderedValue> { item },
                ObjectOrder = new List<OrderedObject> { new(item, 0) },
            };
            XDocument document = XDocument.Parse(Serialize(graph));
            XAttribute position = document
                .Descendants()
                .Attributes(XName.Get("position", "urn:rebellion2:object-order"))
                .Single();
            position.Value = int.MaxValue.ToString();
            Assert.Throws<InvalidDataException>(() => Deserialize(document.ToString()));
        }

        [Test]
        public void Deserialize_MissingRootSlotCount_RejectsIncompleteOrder()
        {
            OrderedValue item = new() { Name = "first" };
            OrderedGraph graph = new()
            {
                Items = new List<OrderedValue> { item },
                ObjectOrder = new List<OrderedObject> { new(item, 0) },
            };
            XDocument document = XDocument.Parse(Serialize(graph));
            document.Root.Attribute(XName.Get("slots", "urn:rebellion2:object-order")).Remove();
            Assert.Throws<InvalidDataException>(() => Deserialize(document.ToString()));
        }

        /// <summary>
        /// Writes the fixture graph through the public serializer.
        /// </summary>
        /// <param name="graph">The ordered fixture graph.</param>
        /// <returns>The serialized document.</returns>
        private static string Serialize(OrderedGraph graph)
        {
            using StringWriter writer = new();
            new GameSerializer(typeof(OrderedGraph)).Serialize(writer, graph);
            return writer.ToString();
        }

        /// <summary>
        /// Reads the fixture graph through the public serializer.
        /// </summary>
        /// <param name="xml">The serialized document.</param>
        /// <returns>The independently decoded graph.</returns>
        private static OrderedGraph Deserialize(string xml) =>
            (OrderedGraph)
                new GameSerializer(typeof(OrderedGraph)).Deserialize(new StringReader(xml));

        /// <summary>
        /// Writes and reads the fixture graph.
        /// </summary>
        /// <param name="graph">The ordered fixture graph.</param>
        /// <returns>The independently decoded graph.</returns>
        private static OrderedGraph RoundTrip(OrderedGraph graph) => Deserialize(Serialize(graph));

        [PersistableObject]
        public sealed class OrderedValue
        {
            public string Name { get; set; }
        }

        [PersistableObject]
        public sealed class OrderedGraph : IOrderedObjectGraph
        {
            public List<OrderedValue> Items { get; set; } = new();

            [PersistableIgnore]
            public List<OrderedObject> ObjectOrder { get; set; } = new();

            [PersistableIgnore]
            public List<int> Vacancies { get; set; } = new();

            /// <summary>
            /// Captures the fixture's explicitly configured logical order.
            /// </summary>
            /// <returns>The configured order.</returns>
            public IReadOnlyList<OrderedObject> CaptureObjectOrder() => ObjectOrder;

            /// <summary>
            /// Captures the fixture's vacancy reuse stack.
            /// </summary>
            /// <returns>The configured vacancies.</returns>
            public IReadOnlyList<int> CaptureVacantPositions() => Vacancies;

            /// <summary>
            /// Records the order restored through the serializer's public contract.
            /// </summary>
            /// <param name="objects">The decoded records.</param>
            /// <param name="vacancies">The decoded vacancies.</param>
            public void RestoreObjectOrder(
                IReadOnlyList<OrderedObject> objects,
                IReadOnlyList<int> vacancies
            )
            {
                ObjectOrder = objects.ToList();
                Vacancies = vacancies.ToList();
            }
        }
    }
}
