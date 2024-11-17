using System;
using System.Collections.Generic;
using System.Linq;
using IEnumerableExtensions;
using NUnit.Framework;

[TestFixture]
public class IEnumerableExtensionsTests
{
    [Test]
    public void TestShuffleRandomizesCollection()
    {
        List<int> originalList = new List<int> { 1, 2, 3, 4, 5 };
        List<int> originalListCopy = new List<int>(originalList);

        List<int> shuffledList = originalList.Shuffle().ToList();

        Assert.AreEqual(
            originalList.Count,
            shuffledList.Count,
            "Shuffled list should have the same count as the original list."
        );
        CollectionAssert.AreEquivalent(
            originalList,
            shuffledList,
            "Shuffled list should contain the same elements as the original list."
        );
        CollectionAssert.AreNotEqual(
            originalList,
            shuffledList,
            "Shuffled list should not be in the same order as the original list."
        );
    }

    [Test]
    public void TestRandomElementReturnsElementFromList()
    {
        List<int> list = new List<int> { 1, 2, 3, 4, 5 };

        int randomElement = list.RandomElement();

        Assert.Contains(
            randomElement,
            list,
            "Random element should be one of the elements in the list."
        );
    }

    [Test]
    public void TestRandomElementThrowsExceptionWhenListIsEmpty()
    {
        List<int> emptyList = new List<int>();

        Assert.Throws<InvalidOperationException>(
            () => emptyList.RandomElement(),
            "Should throw InvalidOperationException when the list is empty."
        );
    }

    [Test]
    public void TestRandomElementThrowsExceptionWhenListIsNull()
    {
        List<int> nullList = null;

        Assert.Throws<InvalidOperationException>(
            () => nullList.RandomElement(),
            "Should throw InvalidOperationException when the list is null."
        );
    }
}
