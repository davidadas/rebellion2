using System.Collections.Generic;

/// <summary>
/// Captures the configured selection-modifier actions for one selection operation.
/// </summary>
public readonly struct SelectionModifierState
{
    /// <summary>
    /// Creates an immutable selection-modifier snapshot.
    /// </summary>
    /// <param name="multiSelect">Whether individual selection toggling is active.</param>
    /// <param name="rangeSelect">Whether contiguous range selection is active.</param>
    public SelectionModifierState(bool multiSelect, bool rangeSelect)
    {
        MultiSelect = multiSelect;
        RangeSelect = rangeSelect;
    }

    public bool MultiSelect { get; }

    public bool RangeSelect { get; }

    public bool HasAnyModifier => MultiSelect || RangeSelect;
}

/// <summary>
/// Owns list selection state and applies the shared modifier-key selection rules.
/// </summary>
public sealed class SelectableListSelection
{
    private readonly HashSet<int> selectedIndexes = new HashSet<int>();

    public int SelectedIndex { get; private set; } = -1;

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
    /// Applies shared toggle, contiguous-range, or replacement selection rules.
    /// </summary>
    /// <param name="selection">The selection to update.</param>
    /// <param name="index">The requested source index.</param>
    /// <param name="count">The current item count.</param>
    /// <param name="modifiers">The configured selection modifiers currently held.</param>
    public static void SelectIndexedItem(
        HashSet<int> selection,
        int index,
        int count,
        SelectionModifierState modifiers = default
    )
    {
        if (selection == null || index < 0 || index >= count)
            return;

        if (modifiers.MultiSelect)
        {
            if (!selection.Add(index))
                selection.Remove(index);
        }
        else if (modifiers.RangeSelect)
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
    /// <param name="modifiers">The configured selection modifiers currently held.</param>
    public static void SelectIndexedItemForDrag(
        HashSet<int> selection,
        int index,
        int count,
        SelectionModifierState modifiers = default
    )
    {
        if (CanDragExistingSelection(selection, index, modifiers))
            return;

        SelectIndexedItem(selection, index, count, modifiers);
    }

    /// <summary>
    /// Applies shared toggle, contiguous-range, or replacement selection rules.
    /// </summary>
    /// <param name="selection">The selection to update.</param>
    /// <param name="index">The requested source index.</param>
    /// <param name="count">The current item count.</param>
    /// <param name="modifiers">The configured selection modifiers currently held.</param>
    public static void SelectRangeItem(
        HashSet<int> selection,
        int index,
        int count,
        SelectionModifierState modifiers = default
    )
    {
        SelectIndexedItem(selection, index, count, modifiers);
    }

    /// <summary>
    /// Reports whether a drag may retain the current selection.
    /// </summary>
    /// <param name="selection">The current selection.</param>
    /// <param name="index">The dragged source index.</param>
    /// <param name="modifiers">The configured selection modifiers currently held.</param>
    /// <returns>True when the index is selected and no selection modifier is held.</returns>
    public static bool CanDragExistingSelection(
        HashSet<int> selection,
        int index,
        SelectionModifierState modifiers = default
    )
    {
        return selection?.Contains(index) == true && !modifiers.HasAnyModifier;
    }

    /// <summary>
    /// Reports whether any supported selection modifier is currently held.
    /// </summary>
    /// <param name="modifiers">The configured selection modifiers currently held.</param>
    /// <returns>True when a configured selection modifier is held.</returns>
    public static bool HasSelectionModifier(SelectionModifierState modifiers = default)
    {
        return modifiers.HasAnyModifier;
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
}
