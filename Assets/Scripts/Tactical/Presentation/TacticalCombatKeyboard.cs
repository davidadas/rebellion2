using UnityEngine;
using UnityEngine.InputSystem;

namespace Rebellion.Tactical.Presentation
{
    /// <summary>
    /// Translates tactical keys into camera commands and visible inspection controls.
    /// </summary>
    public static class TacticalCombatKeyboard
    {
        /// <summary>
        /// Resolves the eight top-row object-category keys to their frame image controls.
        /// </summary>
        /// <param name="key">The native keyboard key.</param>
        /// <param name="control">Receives the addressed category control.</param>
        /// <returns>Whether the key addresses an object-category image.</returns>
        public static bool TryGetObjectCategoryControl(
            KeyCode key,
            out TacticalCombatControl control
        )
        {
            if (key is >= KeyCode.Alpha1 and <= KeyCode.Alpha8)
            {
                control = (TacticalCombatControl)(
                    (int)TacticalCombatControl.ObjectGroup1 + (int)key - (int)KeyCode.Alpha1
                );
                return true;
            }
            control = default;
            return false;
        }

        /// <summary>
        /// Resolves a native key transition to its camera image button without introducing a repeat timer.
        /// </summary>
        /// <param name="key">The native keyboard key.</param>
        /// <param name="control">Receives the addressed camera image button.</param>
        /// <returns>Whether the key addresses a camera image button.</returns>
        public static bool TryGetCameraControl(KeyCode key, out TacticalCombatControl control)
        {
            control = key switch
            {
                KeyCode.PageUp or KeyCode.Keypad9 or KeyCode.KeypadPlus =>
                    TacticalCombatControl.CameraZoomIn,
                KeyCode.PageDown or KeyCode.Keypad3 or KeyCode.KeypadMinus =>
                    TacticalCombatControl.CameraZoomOut,
                KeyCode.A or KeyCode.LeftArrow or KeyCode.Keypad4 =>
                    TacticalCombatControl.CameraRotateLeft,
                KeyCode.D or KeyCode.RightArrow or KeyCode.Keypad6 =>
                    TacticalCombatControl.CameraRotateRight,
                KeyCode.W or KeyCode.UpArrow or KeyCode.Keypad8 =>
                    TacticalCombatControl.CameraTiltUp,
                KeyCode.S or KeyCode.DownArrow or KeyCode.Keypad2 =>
                    TacticalCombatControl.CameraTiltDown,
                KeyCode.Home or KeyCode.Keypad7 => TacticalCombatControl.SaveCamera,
                KeyCode.End or KeyCode.Keypad1 => TacticalCombatControl.RestoreCamera,
                _ => TacticalCombatControl.FocusCamera,
            };
            return key
                is KeyCode.PageUp
                    or KeyCode.Keypad9
                    or KeyCode.KeypadPlus
                    or KeyCode.PageDown
                    or KeyCode.Keypad3
                    or KeyCode.KeypadMinus
                    or KeyCode.LeftArrow
                    or KeyCode.A
                    or KeyCode.Keypad4
                    or KeyCode.RightArrow
                    or KeyCode.D
                    or KeyCode.Keypad6
                    or KeyCode.UpArrow
                    or KeyCode.W
                    or KeyCode.Keypad8
                    or KeyCode.DownArrow
                    or KeyCode.S
                    or KeyCode.Keypad2
                    or KeyCode.Home
                    or KeyCode.Keypad7
                    or KeyCode.End
                    or KeyCode.Keypad1
                    or KeyCode.Return
                    or KeyCode.KeypadEnter
                    or KeyCode.KeypadPeriod;
        }

        /// <summary>
        /// Resolves the twelve live category-view shortcuts without changing command selection.
        /// </summary>
        /// <param name="key">The native keyboard key.</param>
        /// <param name="category">Receives the zero-based object or fighter category.</param>
        /// <returns>Whether the key addresses a category camera.</returns>
        public static bool TryGetCameraCategory(KeyCode key, out int category)
        {
            category = key - KeyCode.F1;
            return key is >= KeyCode.F1 and <= KeyCode.F12;
        }

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
    }
}
