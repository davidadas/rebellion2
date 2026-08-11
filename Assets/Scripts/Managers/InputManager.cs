using System;
using Rebellion.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Processors;

/// <summary>
/// Owns the generated input action asset and binding override persistence.
/// </summary>
public sealed class InputManager : MonoBehaviour
{
    private PlayerInputActions _actions;

    /// <summary>
    /// Gets the generated input action wrapper.
    /// </summary>
    public PlayerInputActions Actions
    {
        get
        {
            KeyboardChordProcessor.EnsureRegistered();
            return _actions ??= new PlayerInputActions();
        }
    }

    /// <summary>
    /// Gets the generated input action asset.
    /// </summary>
    public InputActionAsset Asset => Actions.asset;

    /// <summary>
    /// Attempts to return the generated input action wrapper without creating it.
    /// </summary>
    /// <param name="actions">The generated input action wrapper when one has been created.</param>
    /// <returns>True when input actions have been created; otherwise false.</returns>
    public bool TryGetActions(out PlayerInputActions actions)
    {
        actions = _actions;
        return actions != null;
    }

    /// <summary>
    /// Disables and disposes the generated input action wrapper.
    /// </summary>
    private void OnDestroy()
    {
        if (_actions == null)
            return;

        DisableAllActionMaps();
        _actions.Dispose();
        _actions = null;
    }

    /// <summary>
    /// Disables every action map on the generated input action asset.
    /// </summary>
    private void DisableAllActionMaps()
    {
        foreach (InputActionMap actionMap in _actions.asset.actionMaps)
            actionMap.Disable();
    }

    /// <summary>
    /// Saves all runtime binding overrides.
    /// </summary>
    /// <returns>The serialized binding override data.</returns>
    public string SaveBindingOverrides()
    {
        return InputActionRebindingExtensions.SaveBindingOverridesAsJson(Actions.asset);
    }

    /// <summary>
    /// Replaces runtime binding overrides with the supplied override data.
    /// </summary>
    /// <param name="bindingOverrides">The serialized binding override data.</param>
    public void LoadBindingOverrides(string bindingOverrides)
    {
        InputActionRebindingExtensions.RemoveAllBindingOverrides(Actions.asset);

        if (string.IsNullOrWhiteSpace(bindingOverrides))
            return;

        InputActionRebindingExtensions.LoadBindingOverridesFromJson(
            Actions.asset,
            bindingOverrides
        );
    }
}

/// <summary>
/// Gates a button binding behind the keyboard modifiers captured by the Options rebind workflow.
/// The processor is stored in Input System's normal binding-override JSON, so chords persist with
/// the rest of the player's bindings without a second settings format.
/// </summary>
internal sealed class KeyboardChordProcessor : InputProcessor<float>
{
    internal const int Shift = 1 << 0;
    internal const int Control = 1 << 1;
    internal const int Alt = 1 << 2;
    internal const int Meta = 1 << 3;

    private static bool registered;

    public int modifiers;

    /// <summary>Registers the processor before an action asset containing it is resolved.</summary>
    internal static void EnsureRegistered()
    {
        if (registered)
            return;

        InputSystem.RegisterProcessor<KeyboardChordProcessor>("keyboardChord");
        registered = true;
    }

    /// <summary>Returns zero until every required modifier is held.</summary>
    public override float Process(float value, InputControl control)
    {
        return value != 0f && ArePressed(Keyboard.current, modifiers) ? value : 0f;
    }

    /// <summary>Captures the currently held modifier families as a stable bit mask.</summary>
    internal static int GetPressedModifiers(Keyboard keyboard)
    {
        if (keyboard == null)
            return 0;

        int result = 0;
        if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            result |= Shift;
        if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
            result |= Control;
        if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)
            result |= Alt;
        if (keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed)
            result |= Meta;
        return result;
    }

    /// <summary>Gets whether a control is itself a modifier rather than the chord's main key.</summary>
    internal static bool IsModifier(InputControl control)
    {
        if (control is not KeyControl key)
            return false;

        return key.keyCode
            is Key.LeftShift
                or Key.RightShift
                or Key.LeftCtrl
                or Key.RightCtrl
                or Key.LeftAlt
                or Key.RightAlt
                or Key.LeftMeta
                or Key.RightMeta;
    }

    /// <summary>Builds the processor override serialized by the Input System.</summary>
    internal static string GetProcessorOverride(int modifierMask)
    {
        return modifierMask == 0 ? string.Empty : $"keyboardChord(modifiers={modifierMask})";
    }

    /// <summary>Formats a modifier mask for the compact Options binding badge.</summary>
    internal static string GetDisplayPrefix(int modifierMask)
    {
        string result = string.Empty;
        if ((modifierMask & Control) != 0)
            result += "CTRL+";
        if ((modifierMask & Shift) != 0)
            result += "SHIFT+";
        if ((modifierMask & Alt) != 0)
            result += "ALT+";
        if ((modifierMask & Meta) != 0)
            result += "META+";
        return result;
    }

    /// <summary>Reads this processor's modifier mask from an effective processor string.</summary>
    internal static int ParseModifierMask(string processors)
    {
        const string marker = "keyboardChord(modifiers=";
        int start = processors?.IndexOf(marker, StringComparison.OrdinalIgnoreCase) ?? -1;
        if (start < 0)
            return 0;

        start += marker.Length;
        int end = processors.IndexOf(')', start);
        return end > start && int.TryParse(processors.Substring(start, end - start), out int mask)
            ? mask
            : 0;
    }

    private static bool ArePressed(Keyboard keyboard, int required)
    {
        return required == 0
            || keyboard != null && (GetPressedModifiers(keyboard) & required) == required;
    }
}
