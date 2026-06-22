using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public sealed class SelectableListSelection
{
    private readonly HashSet<int> selectedIndexes = new HashSet<int>();

    public int SelectedIndex { get; private set; } = -1;

    public IReadOnlyCollection<int> SelectedIndexes => selectedIndexes;

    public void SelectOnly(int index)
    {
        selectedIndexes.Clear();
        SelectedIndex = index;
        if (index >= 0)
            selectedIndexes.Add(index);
    }

    public void SelectAll(int count)
    {
        selectedIndexes.Clear();
        for (int i = 0; i < count; i++)
            selectedIndexes.Add(i);
    }

    public void Clear()
    {
        selectedIndexes.Clear();
        SelectedIndex = -1;
    }

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

    public void UseInitialSelection(int initialSelectedIndex)
    {
        if (SelectedIndex < 0 && initialSelectedIndex >= 0)
            SelectOnly(initialSelectedIndex);
    }

    public bool Move(int count, int direction)
    {
        int nextIndex = GetMovedIndex(SelectedIndex, count, direction);
        if (nextIndex == SelectedIndex)
            return false;

        SelectOnly(nextIndex);
        return true;
    }

    public static int GetMovedIndex(int selectedIndex, int count, int direction)
    {
        if (count <= 0)
            return -1;

        if (selectedIndex < 0 || selectedIndex >= count)
            return direction < 0 ? count - 1 : 0;

        return System.Math.Max(0, System.Math.Min(count - 1, selectedIndex + direction));
    }

    public bool Contains(int index)
    {
        return selectedIndexes.Contains(index);
    }

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

    public static bool CanDragExistingSelection(HashSet<int> selection, int index)
    {
        return selection != null
            && selection.Contains(index)
            && !GetSelectionModifiers().HasAnyModifier;
    }

    private static void FillSelectionRange(HashSet<int> selection, int count)
    {
        if (!TryGetSelectionBounds(selection, out int start, out int end))
            return;

        for (int i = start; i <= end && i < count; i++)
            selection.Add(i);
    }

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

    private static bool IsPressed(KeyControl key)
    {
        return key != null && key.isPressed;
    }

    private readonly struct SelectionModifiers
    {
        public SelectionModifiers(bool control, bool shift, bool alt)
        {
            Control = control;
            Shift = shift;
            Alt = alt;
        }

        public bool Control { get; }
        public bool Shift { get; }
        public bool Alt { get; }
        public bool HasAnyModifier => Control || Shift || Alt;
    }
}
