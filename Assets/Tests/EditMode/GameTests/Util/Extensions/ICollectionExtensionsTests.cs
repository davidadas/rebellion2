using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Util.Extensions;

namespace Rebellion.Tests.Util.Extensions
{
    [TestFixture]
    public class ICollectionExtensionsTests
    {
        [Test]
        public void AddAll_EmptyCollection_AddsAllElements()
        {
            ICollection<int> collection = new List<int>();
            IEnumerable<int> enumerable1 = new List<int> { 1, 2, 3 };
            IEnumerable<int> enumerable2 = new List<int> { 4, 5, 6 };
            IEnumerable<int> enumerable3 = new List<int> { 7, 8, 9 };

            collection.AddAll(enumerable1, enumerable2, enumerable3);

            Assert.AreEqual(9, collection.Count, "Collection should contain 9 elements.");
            CollectionAssert.AreEquivalent(
                new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
                collection,
                "Collection should contain all elements from the enumerables."
            );
        }

        [Test]
        public void AddAll_EmptySourceArray_DoesNotModifyCollection()
        {
            ICollection<int> collection = new List<int>();
            IEnumerable<int> emptyEnumerable = new List<int>();

            collection.AddAll(emptyEnumerable);

            Assert.AreEqual(0, collection.Count, "Collection should be empty.");
        }

        [Test]
        public void AddAll_NonEmptyCollection_AddsAllElements()
        {
            ICollection<int> collection = new List<int> { 0 };
            IEnumerable<int> enumerable = new List<int> { 1, 2, 3 };

            collection.AddAll(enumerable);

            Assert.AreEqual(4, collection.Count, "Collection should contain 4 elements.");
            CollectionAssert.AreEquivalent(
                new List<int> { 0, 1, 2, 3 },
                collection,
                "Collection should contain the initial element and all elements from the enumerable."
            );
        }

        [Test]
        public void AddAll_NullSourceCollection_ThrowsNullReferenceException()
        {
            ICollection<int> collection = null;
            IEnumerable<int> enumerable = new List<int> { 1, 2, 3 };

            Assert.Throws<NullReferenceException>(() => collection.AddAll(enumerable));
        }

        [Test]
        public void AddAll_NullEnumerableInParams_ThrowsNullReferenceException()
        {
            ICollection<int> collection = new List<int>();
            IEnumerable<int> enumerable1 = new List<int> { 1, 2, 3 };
            IEnumerable<int> enumerable2 = null;
            IEnumerable<int> enumerable3 = new List<int> { 4, 5, 6 };

            Assert.Throws<NullReferenceException>(() =>
                collection.AddAll(enumerable1, enumerable2, enumerable3)
            );
        }

        [Test]
        public void AddAll_DuplicateElements_AddsAllDuplicates()
        {
            ICollection<int> collection = new List<int>();
            IEnumerable<int> enumerable1 = new List<int> { 1, 2, 3 };
            IEnumerable<int> enumerable2 = new List<int> { 2, 3, 4 };

            collection.AddAll(enumerable1, enumerable2);

            Assert.AreEqual(
                6,
                collection.Count,
                "Collection should contain 6 elements including duplicates."
            );
            CollectionAssert.AreEquivalent(
                new List<int> { 1, 2, 2, 3, 3, 4 },
                collection,
                "Collection should contain all elements including duplicates."
            );
        }

        [Test]
        public void AddAll_NullElementsInEnumerables_AddsNullElements()
        {
            ICollection<string> collection = new List<string>();
            IEnumerable<string> enumerable1 = new List<string> { "a", null, "b" };
            IEnumerable<string> enumerable2 = new List<string> { null, "c" };

            collection.AddAll(enumerable1, enumerable2);

            Assert.AreEqual(
                5,
                collection.Count,
                "Collection should contain 5 elements including nulls."
            );
            CollectionAssert.AreEquivalent(
                new List<string> { "a", null, "b", null, "c" },
                collection,
                "Collection should contain all elements including null values."
            );
        }

        [Test]
        public void AddAll_WithHashSet_AddsUniqueElements()
        {
            ICollection<int> collection = new HashSet<int>();
            IEnumerable<int> enumerable1 = new List<int> { 1, 2, 3 };
            IEnumerable<int> enumerable2 = new List<int> { 2, 3, 4 };

            collection.AddAll(enumerable1, enumerable2);

            Assert.AreEqual(4, collection.Count, "HashSet should contain 4 unique elements.");
            CollectionAssert.AreEquivalent(
                new List<int> { 1, 2, 3, 4 },
                collection,
                "HashSet should contain only unique elements."
            );
        }

        [Test]
        public void AddAll_WithSortedSet_AddsSortedElements()
        {
            ICollection<int> collection = new SortedSet<int>();
            IEnumerable<int> enumerable1 = new List<int> { 3, 1, 2 };
            IEnumerable<int> enumerable2 = new List<int> { 6, 4, 5 };

            collection.AddAll(enumerable1, enumerable2);

            Assert.AreEqual(6, collection.Count, "SortedSet should contain 6 unique elements.");
            CollectionAssert.AreEqual(
                new List<int> { 1, 2, 3, 4, 5, 6 },
                collection,
                "SortedSet should maintain sorted order."
            );
        }

        [Test]
        public void AddAll_WithLinkedList_MaintainsInsertionOrder()
        {
            ICollection<int> collection = new LinkedList<int>();
            IEnumerable<int> enumerable1 = new List<int> { 1, 2, 3 };
            IEnumerable<int> enumerable2 = new List<int> { 4, 5, 6 };

            collection.AddAll(enumerable1, enumerable2);

            Assert.AreEqual(6, collection.Count, "LinkedList should contain 6 elements.");
            CollectionAssert.AreEqual(
                new List<int> { 1, 2, 3, 4, 5, 6 },
                collection,
                "LinkedList should maintain insertion order."
            );
        }
    }
} // namespace Rebellion.Tests.Util.Extensions
