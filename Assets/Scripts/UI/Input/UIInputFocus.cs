using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Reports whether the current UI selection owns keyboard text entry.
/// </summary>
internal static class UIInputFocus
{
    /// <summary>
    /// Returns whether a focused TMP input field should receive keyboard input exclusively.
    /// </summary>
    /// <returns>True while the selected UI hierarchy contains a focused text field.</returns>
    internal static bool IsTextEntryActive()
    {
        return IsTextEntryActive(EventSystem.current);
    }

    /// <summary>
    /// Returns whether a supplied event system currently routes input to a focused TMP field.
    /// </summary>
    /// <param name="eventSystem">The event system whose selected hierarchy should be inspected.</param>
    /// <returns>True while the selected UI hierarchy contains a focused text field.</returns>
    internal static bool IsTextEntryActive(EventSystem eventSystem)
    {
        if (eventSystem == null)
            return false;

        GameObject selectedObject = eventSystem.currentSelectedGameObject;
        if (selectedObject == null)
            return false;

        TMP_InputField inputField = selectedObject.GetComponentInParent<TMP_InputField>();
        return inputField?.isActiveAndEnabled == true
            && inputField.interactable
            && inputField.isFocused;
    }
}
