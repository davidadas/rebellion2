using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.UI.Components.SelectableList
{
    [TestFixture]
    public class SelectableListSelectionTests
    {
        [Test]
        public void SelectOnly_WithPreviousSelection_ReplacesSelection()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectAll(4);
            selection.SelectOnly(2);

            Assert.AreEqual(2, selection.SelectedIndex);
            CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
        }

        [Test]
        public void SelectAll_WithPrimarySelection_PreservesPrimarySelection()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(1);
            selection.SelectAll(3);

            Assert.AreEqual(1, selection.SelectedIndex);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, selection.SelectedIndexes.ToArray());
        }

        [Test]
        public void ClampToCount_PrimarySelectionOutsideRange_MovesSelectionInsideRange()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(5);
            selection.ClampToCount(3);

            Assert.AreEqual(2, selection.SelectedIndex);
            CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
        }

        [Test]
        public void ClampToCount_SelectedIndexesOutsideRange_RemovesIndexes()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectAll(5);
            selection.ClampToCount(3);

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, selection.SelectedIndexes.ToArray());
        }

        [Test]
        public void ClampToCount_Zero_ClearsSelection()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(1);
            selection.ClampToCount(0);

            Assert.AreEqual(-1, selection.SelectedIndex);
            Assert.IsEmpty(selection.SelectedIndexes);
        }

        [Test]
        public void GetMovedIndex_NoSelectionMovingDown_ReturnsFirstRow()
        {
            Assert.AreEqual(0, SelectableListSelection.GetMovedIndex(-1, 3, 1));
        }

        [Test]
        public void GetMovedIndex_NoSelectionMovingUp_ReturnsLastRow()
        {
            Assert.AreEqual(2, SelectableListSelection.GetMovedIndex(-1, 3, -1));
        }

        [Test]
        public void GetMovedIndex_LastRowMovingDown_ReturnsLastRow()
        {
            Assert.AreEqual(2, SelectableListSelection.GetMovedIndex(2, 3, 1));
        }

        [Test]
        public void GetMovedIndex_FirstRowMovingUp_ReturnsFirstRow()
        {
            Assert.AreEqual(0, SelectableListSelection.GetMovedIndex(0, 3, -1));
        }

        [Test]
        public void GetMovedIndex_EmptyRows_ReturnsNoSelection()
        {
            Assert.AreEqual(-1, SelectableListSelection.GetMovedIndex(0, 0, 1));
        }

        [Test]
        public void Move_DestinationExists_UpdatesPrimarySelection()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(1);

            Assert.IsTrue(selection.Move(3, 1));
            Assert.AreEqual(2, selection.SelectedIndex);
            CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
        }

        [Test]
        public void Move_SelectionAtBoundary_ReturnsFalse()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(2);

            Assert.IsFalse(selection.Move(3, 1));
            Assert.AreEqual(2, selection.SelectedIndex);
            CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
        }

        [Test]
        public void SelectIndexedItem_MultiSelectModifier_TogglesRequestedItem()
        {
            HashSet<int> selection = new HashSet<int> { 1 };
            SelectionModifierState modifiers = new SelectionModifierState(true, false);

            SelectableListSelection.SelectIndexedItem(selection, 2, 5, modifiers);
            SelectableListSelection.SelectIndexedItem(selection, 1, 5, modifiers);

            CollectionAssert.AreEquivalent(new[] { 2 }, selection);
        }

        [Test]
        public void SelectIndexedItem_RangeSelectModifier_SelectsContiguousRange()
        {
            HashSet<int> selection = new HashSet<int> { 1 };
            SelectionModifierState modifiers = new SelectionModifierState(false, true);

            SelectableListSelection.SelectIndexedItem(selection, 4, 6, modifiers);

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, selection);
        }

        [Test]
        public void SelectIndexedItem_NoModifier_ReplacesSelection()
        {
            HashSet<int> selection = new HashSet<int> { 1, 2 };

            SelectableListSelection.SelectIndexedItem(selection, 4, 6);

            CollectionAssert.AreEquivalent(new[] { 4 }, selection);
        }
    }
}
