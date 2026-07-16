using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Owns list selection state and applies the shared modifier-key selection rules.
/// </summary>
public sealed class SelectableListSelection
{
    private readonly HashSet<int> selectedIndexes = new HashSet<int>();

    /// <summary>
    /// Gets or sets the selected index.
    /// </summary>
    public int SelectedIndex { get; private set; } = -1;

    /// <summary>
    /// Gets the selected indexes.
    /// </summary>
    public IReadOnlyCollection<int> SelectedIndexes => selectedIndexes;

    /// <summary>
    /// Replaces the current selection with one valid source index.
    /// </summary>
    /// <param name="index">The source index, or a negative value to clear selection.</param>
    public void SelectOnly(int index)
    {
        selectedIndexes.Clear();
        SelectedIndex = index;
        if (index >= 0)
            selectedIndexes.Add(index);
    }

    /// <summary>
    /// Selects every source index within a non-negative item count.
    /// </summary>
    /// <param name="count">The number of selectable items.</param>
    public void SelectAll(int count)
    {
        selectedIndexes.Clear();
        for (int i = 0; i < count; i++)
            selectedIndexes.Add(i);
    }

    /// <summary>
    /// Clears primary and multi-selection state.
    /// </summary>
    public void Clear()
    {
        selectedIndexes.Clear();
        SelectedIndex = -1;
    }

    /// <summary>
    /// Removes indexes outside the current item count and clamps the primary selection.
    /// </summary>
    /// <param name="count">The current item count.</param>
    public void ClampToCount(int count)
    {
        if (count <= 0)
        {
            Clear();
            return;
        }

        if (SelectedIndex >= count)
            SelectOnly(count - 1);

        selectedIndexes.RemoveWhere(index => index >= count);
    }

    /// <summary>
    /// Applies an initial primary selection only when no selection exists.
    /// </summary>
    /// <param name="initialSelectedIndex">The initial source index.</param>
    public void UseInitialSelection(int initialSelectedIndex)
    {
        if (SelectedIndex < 0 && initialSelectedIndex >= 0)
            SelectOnly(initialSelectedIndex);
    }

    /// <summary>
    /// Moves primary selection by a signed offset.
    /// </summary>
    /// <param name="count">The current item count.</param>
    /// <param name="direction">The signed selection offset.</param>
    /// <returns>True when primary selection changed.</returns>
    public bool Move(int count, int direction)
    {
        int nextIndex = GetMovedIndex(SelectedIndex, count, direction);
        if (nextIndex == SelectedIndex)
            return false;

        SelectOnly(nextIndex);
        return true;
    }

    /// <summary>
    /// Calculates a bounded moved index, including entry from an empty selection.
    /// </summary>
    /// <param name="selectedIndex">The current primary index.</param>
    /// <param name="count">The current item count.</param>
    /// <param name="direction">The signed selection offset.</param>
    /// <returns>The bounded destination index, or negative one for an empty list.</returns>
    public static int GetMovedIndex(int selectedIndex, int count, int direction)
    {
        if (count <= 0)
            return -1;

        if (selectedIndex < 0 || selectedIndex >= count)
            return direction < 0 ? count - 1 : 0;

        return System.Math.Max(0, System.Math.Min(count - 1, selectedIndex + direction));
    }

    /// <summary>
    /// Reports whether an index belongs to the current selection.
    /// </summary>
    /// <param name="index">The source index.</param>
    /// <returns>True when the index is selected.</returns>
    public bool Contains(int index)
    {
        return selectedIndexes.Contains(index);
    }

    /// <summary>
    /// Applies shared control, shift-grid, alt-range, or replacement selection rules.
    /// </summary>
    /// <param name="selection">The selection to update.</param>
    /// <param name="index">The requested source index.</param>
    /// <param name="count">The current item count.</param>
    /// <param name="itemsPerRow">The number of items in each visual row.</param>
    public static void SelectIndexedItem(
        HashSet<int> selection,
        int index,
        int count,
        int itemsPerRow = 1
    )
    {
        if (selection == null || index < 0 || index >= count)
            return;

        SelectionModifiers modifiers = GetSelectionModifiers();
        if (modifiers.Control)
        {
            if (!selection.Add(index))
                selection.Remove(index);
        }
        else if (modifiers.Shift)
        {
            selection.Add(index);
            FillSelectionGrid(selection, count, itemsPerRow);
        }
        else if (modifiers.Alt)
        {
            selection.Add(index);
            FillSelectionRange(selection, count);
        }
        else
        {
            selection.Clear();
            selection.Add(index);
        }
    }

    /// <summary>
    /// Preserves an existing unmodified drag selection or applies normal selection rules.
    /// </summary>
    /// <param name="selection">The selection to update.</param>
    /// <param name="index">The dragged source index.</param>
    /// <param name="count">The current item count.</param>
    /// <param name="itemsPerRow">The number of items in each visual row.</param>
    public static void SelectIndexedItemForDrag(
        HashSet<int> selection,
        int index,
        int count,
        int itemsPerRow = 1
    )
    {
        if (CanDragExistingSelection(selection, index))
            return;

        SelectIndexedItem(selection, index, count, itemsPerRow);
    }

    /// <summary>
    /// Applies shared control, shift-range, alt-range, or replacement selection rules.
    /// </summary>
    /// <param name="selection">The selection to update.</param>
    /// <param name="index">The requested source index.</param>
    /// <param name="count">The current item count.</param>
    public static void SelectRangeItem(HashSet<int> selection, int index, int count)
    {
        if (selection == null || index < 0 || index >= count)
            return;

        SelectionModifiers modifiers = GetSelectionModifiers();
        if (modifiers.Control)
        {
            if (!selection.Add(index))
                selection.Remove(index);
        }
        else if (modifiers.Shift || modifiers.Alt)
        {
            selection.Add(index);
            FillSelectionRange(selection, count);
        }
        else
        {
            selection.Clear();
            selection.Add(index);
        }
    }

    /// <summary>
    /// Reports whether a drag may retain the current selection.
    /// </summary>
    /// <param name="selection">The current selection.</param>
    /// <param name="index">The dragged source index.</param>
    /// <returns>True when the index is selected and no selection modifier is held.</returns>
    public static bool CanDragExistingSelection(HashSet<int> selection, int index)
    {
        return selection != null
            && selection.Contains(index)
            && !GetSelectionModifiers().HasAnyModifier;
    }

    /// <summary>
    /// Reports whether any supported selection modifier is currently held.
    /// </summary>
    /// <returns>True when control, shift, or alt is held.</returns>
    public static bool HasSelectionModifier()
    {
        return GetSelectionModifiers().HasAnyModifier;
    }

    /// <summary>
    /// Fills every valid index between the current selection bounds.
    /// </summary>
    /// <param name="selection">The selection to update.</param>
    /// <param name="count">The current item count.</param>
    private static void FillSelectionRange(HashSet<int> selection, int count)
    {
        if (!TryGetSelectionBounds(selection, out int start, out int end))
            return;

        for (int i = start; i <= end && i < count; i++)
            selection.Add(i);
    }

    /// <summary>
    /// Fills the rectangular item grid bounded by the current selection.
    /// </summary>
    /// <param name="selection">The selection to update.</param>
    /// <param name="count">The current item count.</param>
    /// <param name="itemsPerRow">The number of items in each visual row.</param>
    private static void FillSelectionGrid(HashSet<int> selection, int count, int itemsPerRow)
    {
        if (!TryGetSelectionBounds(selection, out int start, out int end))
            return;

        if (itemsPerRow <= 1)
        {
            FillSelectionRange(selection, count);
            return;
        }

        int startRow = start / itemsPerRow;
        int startColumn = start % itemsPerRow;
        int endRow = end / itemsPerRow;
        int endColumn = end % itemsPerRow;
        int rowMin = System.Math.Min(startRow, endRow);
        int rowMax = System.Math.Max(startRow, endRow);
        int columnMin = System.Math.Min(startColumn, endColumn);
        int columnMax = System.Math.Max(startColumn, endColumn);

        for (int row = rowMin; row <= rowMax; row++)
        {
            int baseIndex = row * itemsPerRow;
            for (int column = columnMin; column <= columnMax; column++)
            {
                int itemIndex = baseIndex + column;
                if (itemIndex >= 0 && itemIndex < count)
                    selection.Add(itemIndex);
            }
        }
    }

    /// <summary>
    /// Resolves the minimum and maximum selected source indexes.
    /// </summary>
    /// <param name="selection">The selection to inspect.</param>
    /// <param name="start">The minimum selected index.</param>
    /// <param name="end">The maximum selected index.</param>
    /// <returns>True when the selection contains at least one index.</returns>
    private static bool TryGetSelectionBounds(HashSet<int> selection, out int start, out int end)
    {
        start = int.MaxValue;
        end = int.MinValue;
        if (selection == null || selection.Count == 0)
            return false;

        foreach (int index in selection)
        {
            if (index < start)
                start = index;
            if (index > end)
                end = index;
        }

        return start <= end;
    }

    /// <summary>
    /// Reads supported selection modifiers from the current keyboard.
    /// </summary>
    /// <returns>The current modifier state.</returns>
    private static SelectionModifiers GetSelectionModifiers()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return default;

        return new SelectionModifiers(
            IsPressed(keyboard.leftCtrlKey) || IsPressed(keyboard.rightCtrlKey),
            IsPressed(keyboard.leftShiftKey) || IsPressed(keyboard.rightShiftKey),
            IsPressed(keyboard.leftAltKey) || IsPressed(keyboard.rightAltKey)
        );
    }

    /// <summary>
    /// Safely reads a keyboard key's held state.
    /// </summary>
    /// <param name="key">The optional key control.</param>
    /// <returns>True when the key exists and is held.</returns>
    private static bool IsPressed(KeyControl key)
    {
        return key != null && key.isPressed;
    }

    /// <summary>
    /// Captures the supported keyboard modifiers for one selection operation.
    /// </summary>
    private readonly struct SelectionModifiers
    {
        /// <summary>
        /// Creates one immutable modifier snapshot.
        /// </summary>
        /// <param name="control">Whether control is held.</param>
        /// <param name="shift">Whether shift is held.</param>
        /// <param name="alt">Whether alt is held.</param>
        public SelectionModifiers(bool control, bool shift, bool alt)
        {
            Control = control;
            Shift = shift;
            Alt = alt;
        }

        /// <summary>
        /// Gets a value indicating whether the Control modifier is held.
        /// </summary>
        public bool Control { get; }

        /// <summary>
        /// Gets a value indicating whether the Shift modifier is held.
        /// </summary>
        public bool Shift { get; }

        /// <summary>
        /// Gets a value indicating whether the Alt modifier is held.
        /// </summary>
        public bool Alt { get; }

        /// <summary>
        /// Gets a value indicating whether any modifier is present.
        /// </summary>
        public bool HasAnyModifier => Control || Shift || Alt;
    }
}
