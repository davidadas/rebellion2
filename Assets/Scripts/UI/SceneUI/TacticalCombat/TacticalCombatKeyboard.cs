using UnityEngine.InputSystem;

/// <summary>
/// Translates tactical keys into camera commands and visible inspection controls.
/// </summary>
public static class TacticalCombatKeyboard
{
    /// <summary>
    /// Resolves inspection traversal from the currently visible panel and the current Shift state.
    /// </summary>
    /// <param name="panel">The visible right-panel surface.</param>
    /// <param name="key">The keyboard key whose state changed.</param>
    /// <param name="keyDown">Whether this is a press rather than a release.</param>
    /// <param name="shiftDown">Whether Shift is down at this transition.</param>
    /// <param name="control">Receives the addressed previous or next inspection control.</param>
    /// <returns>Whether this panel consumes the transition as an activation or press-state clear.</returns>
    public static bool TryGetPanelControl(
        TacticalCombatHudPanel panel,
        Key key,
        bool keyDown,
        bool shiftDown,
        out TacticalCombatControl control
    )
    {
        control = shiftDown
            ? TacticalCombatControl.PreviousObject
            : TacticalCombatControl.NextObject;
        return key == Key.Tab
            && (
                panel == TacticalCombatHudPanel.SingleObject
                || panel == TacticalCombatHudPanel.ObjectGroup && keyDown
            );
    }

    /// <summary>
    /// Resolves one camera shortcut while leaving contextual battle commands to their selection owner.
    /// </summary>
    /// <param name="key">The pressed keyboard key.</param>
    /// <param name="command">Receives the camera operation.</param>
    /// <returns>Whether the key addresses a primary-camera control.</returns>
    public static bool TryGetCameraCommand(Key key, out TacticalCombatCameraCommand command)
    {
        command = key switch
        {
            Key.PageUp or Key.Numpad9 or Key.NumpadPlus => TacticalCombatCameraCommand.ZoomIn,
            Key.PageDown or Key.Numpad3 or Key.NumpadMinus => TacticalCombatCameraCommand.ZoomOut,
            Key.LeftArrow or Key.Numpad4 => TacticalCombatCameraCommand.RotateLeft,
            Key.RightArrow or Key.Numpad6 => TacticalCombatCameraCommand.RotateRight,
            Key.UpArrow or Key.Numpad8 => TacticalCombatCameraCommand.TiltUp,
            Key.DownArrow or Key.Numpad2 => TacticalCombatCameraCommand.TiltDown,
            Key.Home or Key.Numpad7 => TacticalCombatCameraCommand.Save,
            Key.End or Key.Numpad1 => TacticalCombatCameraCommand.Restore,
            Key.Enter or Key.NumpadPeriod => TacticalCombatCameraCommand.Focus,
            _ => default,
        };
        return key
            is Key.PageUp
                or Key.Numpad9
                or Key.NumpadPlus
                or Key.PageDown
                or Key.Numpad3
                or Key.NumpadMinus
                or Key.LeftArrow
                or Key.Numpad4
                or Key.RightArrow
                or Key.Numpad6
                or Key.UpArrow
                or Key.Numpad8
                or Key.DownArrow
                or Key.Numpad2
                or Key.Home
                or Key.Numpad7
                or Key.End
                or Key.Numpad1
                or Key.Enter
                or Key.NumpadPeriod;
    }
}
