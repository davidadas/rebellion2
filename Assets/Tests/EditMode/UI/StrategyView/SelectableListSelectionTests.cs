using System.Linq;
using NUnit.Framework;

public sealed class SelectableListSelectionTests
{
    [Test]
    public void SelectOnlyReplacesPreviousSelection()
    {
        SelectableListSelection selection = new SelectableListSelection();

        selection.SelectAll(4);
        selection.SelectOnly(2);

        Assert.AreEqual(2, selection.SelectedIndex);
        CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
    }

    [Test]
    public void SelectAllKeepsCurrentPrimarySelection()
    {
        SelectableListSelection selection = new SelectableListSelection();

        selection.SelectOnly(1);
        selection.SelectAll(3);

        Assert.AreEqual(1, selection.SelectedIndex);
        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, selection.SelectedIndexes.ToArray());
    }

    [Test]
    public void ClampToCountMovesPrimarySelectionInsideRange()
    {
        SelectableListSelection selection = new SelectableListSelection();

        selection.SelectOnly(5);
        selection.ClampToCount(3);

        Assert.AreEqual(2, selection.SelectedIndex);
        CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
    }

    [Test]
    public void ClampToCountRemovesIndexesOutsideRange()
    {
        SelectableListSelection selection = new SelectableListSelection();

        selection.SelectAll(5);
        selection.ClampToCount(3);

        CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, selection.SelectedIndexes.ToArray());
    }

    [Test]
    public void ClampToZeroClearsSelection()
    {
        SelectableListSelection selection = new SelectableListSelection();

        selection.SelectOnly(1);
        selection.ClampToCount(0);

        Assert.AreEqual(-1, selection.SelectedIndex);
        Assert.IsEmpty(selection.SelectedIndexes);
    }

    [Test]
    public void GetMovedIndex_NoSelectionMovingDown_SelectsFirstRow()
    {
        Assert.AreEqual(0, SelectableListSelection.GetMovedIndex(-1, 3, 1));
    }

    [Test]
    public void GetMovedIndex_NoSelectionMovingUp_SelectsLastRow()
    {
        Assert.AreEqual(2, SelectableListSelection.GetMovedIndex(-1, 3, -1));
    }

    [Test]
    public void GetMovedIndex_ClampsAtLastRow()
    {
        Assert.AreEqual(2, SelectableListSelection.GetMovedIndex(2, 3, 1));
    }

    [Test]
    public void GetMovedIndex_ClampsAtFirstRow()
    {
        Assert.AreEqual(0, SelectableListSelection.GetMovedIndex(0, 3, -1));
    }

    [Test]
    public void GetMovedIndex_EmptyRows_ReturnsNoSelection()
    {
        Assert.AreEqual(-1, SelectableListSelection.GetMovedIndex(0, 0, 1));
    }

    [Test]
    public void MoveUpdatesPrimarySelection()
    {
        SelectableListSelection selection = new SelectableListSelection();

        selection.SelectOnly(1);

        Assert.IsTrue(selection.Move(3, 1));
        Assert.AreEqual(2, selection.SelectedIndex);
        CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
    }

    [Test]
    public void MoveReturnsFalseWhenSelectionCannotMove()
    {
        SelectableListSelection selection = new SelectableListSelection();

        selection.SelectOnly(2);

        Assert.IsFalse(selection.Move(3, 1));
        Assert.AreEqual(2, selection.SelectedIndex);
        CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
    }
}
