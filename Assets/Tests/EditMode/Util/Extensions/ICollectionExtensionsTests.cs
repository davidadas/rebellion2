using System.Collections.Generic;
using ICollectionExtensions;
using NUnit.Framework;

[TestFixture]
public class ICollectionExtensionsTests
{
    [Test]
    public void TestAddAll()
    {
        ICollection<int> collection = new List<int>();
        IEnumerable<int> enumerable1 = new List<int> { 1, 2, 3 };
        IEnumerable<int> enumerable2 = new List<int> { 4, 5, 6 };
        IEnumerable<int> enumerable3 = new List<int> { 7, 8, 9 };

        collection.AddAll(enumerable1, enumerable2, enumerable3);

        // Assert collection contains all elements
        Assert.AreEqual(9, collection.Count, "Collection should contain 9 elements.");
        CollectionAssert.AreEquivalent(
            new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 },
            collection,
            "Collection should contain all elements from the enumerables."
        );
    }

    [Test]
    public void TestAddAllWithEmptyArray()
    {
        ICollection<int> collection = new List<int>();
        IEnumerable<int> emptyEnumerable = new List<int>();

        collection.AddAll(emptyEnumerable);

        // Assert collection is not empty
        Assert.AreEqual(0, collection.Count, "Collection should be empty.");
    }

    [Test]
    public void TestAddAllToNonEmptyCollection()
    {
        ICollection<int> collection = new List<int> { 0 };
        IEnumerable<int> enumerable = new List<int> { 1, 2, 3 };

        collection.AddAll(enumerable);

        // Assert collection contains new elements
        Assert.AreEqual(4, collection.Count, "Collection should contain 4 elements.");
        CollectionAssert.AreEquivalent(
            new List<int> { 0, 1, 2, 3 },
            collection,
            "Collection should contain the initial element and all elements from the enumerable."
        );
    }
}
