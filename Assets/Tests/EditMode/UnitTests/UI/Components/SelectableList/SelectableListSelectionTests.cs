using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Rebellion.Tests.UI.Components.SelectableList
{
    [TestFixture]
    public class SelectableListSelectionTests
    {
        /// <summary>
        /// Verifies select only with previous selection replaces selection.
        /// </summary>
        [Test]
        public void SelectOnly_WithPreviousSelection_ReplacesSelection()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectAll(4);
            selection.SelectOnly(2);

            Assert.AreEqual(2, selection.SelectedIndex);
            CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
        }

        /// <summary>
        /// Verifies select all with primary selection preserves primary selection.
        /// </summary>
        [Test]
        public void SelectAll_WithPrimarySelection_PreservesPrimarySelection()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(1);
            selection.SelectAll(3);

            Assert.AreEqual(1, selection.SelectedIndex);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, selection.SelectedIndexes.ToArray());
        }

        /// <summary>
        /// Verifies clamp to count primary selection outside range moves selection inside range.
        /// </summary>
        [Test]
        public void ClampToCount_PrimarySelectionOutsideRange_MovesSelectionInsideRange()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(5);
            selection.ClampToCount(3);

            Assert.AreEqual(2, selection.SelectedIndex);
            CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
        }

        /// <summary>
        /// Verifies clamp to count selected indexes outside range removes indexes.
        /// </summary>
        [Test]
        public void ClampToCount_SelectedIndexesOutsideRange_RemovesIndexes()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectAll(5);
            selection.ClampToCount(3);

            CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, selection.SelectedIndexes.ToArray());
        }

        /// <summary>
        /// Verifies clamp to count zero clears selection.
        /// </summary>
        [Test]
        public void ClampToCount_Zero_ClearsSelection()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(1);
            selection.ClampToCount(0);

            Assert.AreEqual(-1, selection.SelectedIndex);
            Assert.IsEmpty(selection.SelectedIndexes);
        }

        /// <summary>
        /// Verifies get moved index no selection moving down returns first row.
        /// </summary>
        [Test]
        public void GetMovedIndex_NoSelectionMovingDown_ReturnsFirstRow()
        {
            Assert.AreEqual(0, SelectableListSelection.GetMovedIndex(-1, 3, 1));
        }

        /// <summary>
        /// Verifies get moved index no selection moving up returns last row.
        /// </summary>
        [Test]
        public void GetMovedIndex_NoSelectionMovingUp_ReturnsLastRow()
        {
            Assert.AreEqual(2, SelectableListSelection.GetMovedIndex(-1, 3, -1));
        }

        /// <summary>
        /// Verifies get moved index last row moving down returns last row.
        /// </summary>
        [Test]
        public void GetMovedIndex_LastRowMovingDown_ReturnsLastRow()
        {
            Assert.AreEqual(2, SelectableListSelection.GetMovedIndex(2, 3, 1));
        }

        /// <summary>
        /// Verifies get moved index first row moving up returns first row.
        /// </summary>
        [Test]
        public void GetMovedIndex_FirstRowMovingUp_ReturnsFirstRow()
        {
            Assert.AreEqual(0, SelectableListSelection.GetMovedIndex(0, 3, -1));
        }

        /// <summary>
        /// Verifies get moved index empty rows returns no selection.
        /// </summary>
        [Test]
        public void GetMovedIndex_EmptyRows_ReturnsNoSelection()
        {
            Assert.AreEqual(-1, SelectableListSelection.GetMovedIndex(0, 0, 1));
        }

        /// <summary>
        /// Verifies move destination exists updates primary selection.
        /// </summary>
        [Test]
        public void Move_DestinationExists_UpdatesPrimarySelection()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(1);

            Assert.IsTrue(selection.Move(3, 1));
            Assert.AreEqual(2, selection.SelectedIndex);
            CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
        }

        /// <summary>
        /// Verifies move selection at boundary returns false.
        /// </summary>
        [Test]
        public void Move_SelectionAtBoundary_ReturnsFalse()
        {
            SelectableListSelection selection = new SelectableListSelection();

            selection.SelectOnly(2);

            Assert.IsFalse(selection.Move(3, 1));
            Assert.AreEqual(2, selection.SelectedIndex);
            CollectionAssert.AreEqual(new[] { 2 }, selection.SelectedIndexes.ToArray());
        }

        /// <summary>
        /// Verifies select indexed item multi select modifier toggles requested item.
        /// </summary>
        [Test]
        public void SelectIndexedItem_MultiSelectModifier_TogglesRequestedItem()
        {
            HashSet<int> selection = new HashSet<int> { 1 };
            SelectionModifierState modifiers = new SelectionModifierState(true, false);

            SelectableListSelection.SelectIndexedItem(selection, 2, 5, modifiers);
            SelectableListSelection.SelectIndexedItem(selection, 1, 5, modifiers);

            CollectionAssert.AreEquivalent(new[] { 2 }, selection);
        }

        /// <summary>
        /// Verifies select indexed item range select modifier selects contiguous range.
        /// </summary>
        [Test]
        public void SelectIndexedItem_RangeSelectModifier_SelectsContiguousRange()
        {
            HashSet<int> selection = new HashSet<int> { 1 };
            SelectionModifierState modifiers = new SelectionModifierState(false, true);

            SelectableListSelection.SelectIndexedItem(selection, 4, 6, modifiers);

            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, selection);
        }

        /// <summary>
        /// Verifies select indexed item range select modifier selects bounded grid.
        /// </summary>
        [Test]
        public void SelectIndexedItem_RangeSelectModifier_SelectsBoundedGrid()
        {
            HashSet<int> selection = new HashSet<int> { 1 };
            SelectionModifierState modifiers = new SelectionModifierState(false, true);

            SelectableListSelection.SelectIndexedItem(selection, 10, 12, modifiers, 4);

            CollectionAssert.AreEquivalent(new[] { 1, 2, 5, 6, 9, 10 }, selection);
        }

        /// <summary>
        /// Verifies select indexed item no modifier replaces selection.
        /// </summary>
        [Test]
        public void SelectIndexedItem_NoModifier_ReplacesSelection()
        {
            HashSet<int> selection = new HashSet<int> { 1, 2 };

            SelectableListSelection.SelectIndexedItem(selection, 4, 6);

            CollectionAssert.AreEquivalent(new[] { 4 }, selection);
        }
    }
}
