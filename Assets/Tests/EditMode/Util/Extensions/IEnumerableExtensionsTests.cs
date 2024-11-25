using System;
using System.Collections.Generic;
using System.Linq;
using IEnumerableExtensions;
using NUnit.Framework;

[TestFixture]
public class IEnumerableExtensionsTests
{
    [Test]
    public void Shuffle_ReturnsCollectionWithSameCount()
    {
        List<int> numbers = Enumerable.Range(1, 10).ToList();
        var shuffled = numbers.Shuffle();
        Assert.AreEqual(
            numbers.Count,
            shuffled.Count(),
            "Shuffled collection should have the same count as the original."
        );
    }

    [Test]
    public void Shuffle_ContainsAllOriginalElements()
    {
        List<string> items = new List<string> { "apple", "banana", "cherry" };
        var shuffled = items.Shuffle();
        CollectionAssert.AreEquivalent(
            items,
            shuffled,
            "Shuffled collection should contain all the original elements."
        );
    }

    [Test]
    public void Shuffle_DoesNotModifyOriginalCollection()
    {
        List<int> numbers = Enumerable.Range(1, 10).ToList();
        var shuffled = numbers.Shuffle();
        CollectionAssert.AreEqual(
            Enumerable.Range(1, 10),
            numbers,
            "Original collection should remain unchanged."
        );
    }

    [Test]
    public void Shuffle_ReturnsShuffledOrder()
    {
        List<int> numbers = Enumerable.Range(1, 100).ToList();
        var shuffled = numbers.Shuffle();

        bool orderChanged = !shuffled.SequenceEqual(numbers);
        Assert.IsTrue(
            orderChanged,
            "Shuffled collection should have a different order from the original."
        );
    }

    [Test]
    public void Shuffle_OnEmptyCollection_ReturnsEmpty()
    {
        List<int> emptyList = new List<int>();
        var shuffled = emptyList.Shuffle();
        Assert.IsEmpty(
            shuffled,
            "Shuffling an empty collection should return an empty collection."
        );
    }

    [Test]
    public void Shuffle_OnNullCollection_ThrowsArgumentNullException()
    {
        IEnumerable<int> nullCollection = null;
        Assert.Throws<ArgumentException>(
            () => nullCollection.Shuffle(),
            "Shuffling a null collection should throw an ArgumentException."
        );
    }

    [Test]
    public void RandomElement_ReturnsAnElementFromCollection()
    {
        List<string> items = new List<string> { "apple", "banana", "cherry" };
        var randomItem = items.RandomElement();
        Assert.Contains(
            randomItem,
            items,
            "RandomElement should return an element from the collection."
        );
    }

    [Test]
    public void RandomElement_FromSingleElementCollection_ReturnsThatElement()
    {
        List<int> singleItem = new List<int> { 42 };
        var randomItem = singleItem.RandomElement();
        Assert.AreEqual(
            42,
            randomItem,
            "RandomElement should return the only element from a single-element collection."
        );
    }

    [Test]
    public void RandomElement_FromEmptyCollection_ThrowsArgumentException()
    {
        List<int> emptyList = new List<int>();
        Assert.Throws<ArgumentException>(
            () => emptyList.RandomElement(),
            "Selecting a random element from an empty collection should throw an ArgumentExceptionException."
        );
    }

    [Test]
    public void RandomElement_FromNullCollection_ThrowsArgumentException()
    {
        IEnumerable<int> nullCollection = null;
        Assert.Throws<ArgumentException>(
            () => nullCollection.RandomElement(),
            "Selecting a random element from a null collection should throw an ArgumentException."
        );
    }

    [Test]
    public void RandomElement_VariesOverMultipleCalls()
    {
        List<int> numbers = Enumerable.Range(1, 10).ToList();
        HashSet<int> randomResults = new HashSet<int>();

        for (int i = 0; i < 50; i++)
        {
            randomResults.Add(numbers.RandomElement());
        }

        Assert.IsTrue(
            randomResults.Count > 1,
            "RandomElement should produce varying results over multiple calls."
        );
    }
}
