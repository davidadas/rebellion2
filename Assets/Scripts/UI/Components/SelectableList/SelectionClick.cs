using System;

/// <summary>
/// Retains selection modifiers for one pointer click on a stable item identity.
/// </summary>
public sealed class SelectionClick
{
    private string pressedItemId;
    private int pressedPointerId;
    private SelectionModifierState? pressedModifiers;

    /// <summary>
    /// Captures the modifiers at the start of a new click, replacing an unfinished press.
    /// </summary>
    /// <param name="itemId">The pressed item's stable identity.</param>
    /// <param name="pointerId">The pointer responsible for the press.</param>
    /// <param name="modifiers">The modifiers held when the press began.</param>
    public void Capture(string itemId, int pointerId, SelectionModifierState modifiers)
    {
        pressedItemId = itemId;
        pressedPointerId = pointerId;
        pressedModifiers = modifiers;
    }

    /// <summary>
    /// Consumes the press and returns its modifiers only for the matching item and pointer.
    /// </summary>
    /// <param name="itemId">The released item's stable identity.</param>
    /// <param name="pointerId">The pointer completing the click.</param>
    /// <returns>The captured modifiers, or null when no matching press exists.</returns>
    public SelectionModifierState? Complete(string itemId, int pointerId)
    {
        SelectionModifierState? result =
            pressedPointerId == pointerId
            && string.Equals(pressedItemId, itemId, StringComparison.Ordinal)
                ? pressedModifiers
                : null;
        pressedModifiers = null;
        pressedItemId = null;
        return result;
    }
}
