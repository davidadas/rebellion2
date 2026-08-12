using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// Handles modifier keys for keyboard bindings.
/// </summary>
internal sealed class KeyboardChordProcessor : InputProcessor<float>
{
    internal const int Shift = 1 << 0;
    internal const int Control = 1 << 1;
    internal const int Alt = 1 << 2;
    internal const int Meta = 1 << 3;

    private static bool _registered;

    public int modifiers;

    internal static void EnsureRegistered()
    {
        if (_registered)
            return;

        InputSystem.RegisterProcessor<KeyboardChordProcessor>("keyboardChord");
        _registered = true;
    }

    public override float Process(float value, InputControl control)
    {
        return value != 0f && ArePressed(Keyboard.current, modifiers) ? value : 0f;
    }

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

    internal static bool IsModifier(InputControl control)
    {
        if (control?.device is not Keyboard)
            return false;

        if (control is KeyControl key)
        {
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

        return IsModifierControlName(control.name);
    }

    internal static bool IsModifierControlName(string controlName)
    {
        return controlName is "ctrl" or "shift" or "alt" or "leftMeta" or "rightMeta";
    }

    internal static string GetProcessorOverride(int modifierMask)
    {
        return modifierMask == 0 ? string.Empty : $"keyboardChord(modifiers={modifierMask})";
    }

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
