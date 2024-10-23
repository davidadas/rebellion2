using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using IEnumerableExtensions;

[TestFixture]
public class IEnumerableExtensionsTests
{
    [Test]
    public void TestShuffleRandomizesCollection()
    {
        // Arrange
        var originalList = new List<int> { 1, 2, 3, 4, 5 };
        var originalListCopy = new List<int>(originalList);

        // Act
        var shuffledList = originalList.Shuffle().ToList();

        // Assert
        Assert.AreEqual(originalList.Count, shuffledList.Count, "Shuffled list should have the same count as the original list.");
        CollectionAssert.AreEquivalent(originalList, shuffledList, "Shuffled list should contain the same elements as the original list.");
        CollectionAssert.AreNotEqual(originalList, shuffledList, "Shuffled list should not be in the same order as the original list.");
    }

    [Test]
    public void TestRandomElementReturnsElementFromList()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var randomElement = list.RandomElement();

        // Assert
        Assert.Contains(randomElement, list, "Random element should be one of the elements in the list.");
    }

    [Test]
    public void TestRandomElementThrowsExceptionWhenListIsEmpty()
    {
        // Arrange
        var emptyList = new List<int>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => emptyList.RandomElement(), "Should throw InvalidOperationException when the list is empty.");
    }

    [Test]
    public void TestRandomElementThrowsExceptionWhenListIsNull()
    {
        // Arrange
        List<int> nullList = null;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => nullList.RandomElement(), "Should throw InvalidOperationException when the list is null.");
    }
}
