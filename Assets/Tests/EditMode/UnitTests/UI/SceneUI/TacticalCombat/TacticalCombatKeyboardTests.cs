using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatKeyboardTests
    {
        [TestCase(KeyCode.Alpha1, TacticalCombatControl.ObjectGroup1)]
        [TestCase(KeyCode.Alpha2, TacticalCombatControl.ObjectGroup2)]
        [TestCase(KeyCode.Alpha3, TacticalCombatControl.ObjectGroup3)]
        [TestCase(KeyCode.Alpha4, TacticalCombatControl.ObjectGroup4)]
        [TestCase(KeyCode.Alpha5, TacticalCombatControl.ObjectGroup5)]
        [TestCase(KeyCode.Alpha6, TacticalCombatControl.ObjectGroup6)]
        [TestCase(KeyCode.Alpha7, TacticalCombatControl.ObjectGroup7)]
        [TestCase(KeyCode.Alpha8, TacticalCombatControl.ObjectGroup8)]
        public void TryGetObjectCategoryControl_TopRowDigit_AddressesMatchingImage(
            KeyCode key,
            TacticalCombatControl expected
        )
        {
            Assert.IsTrue(TacticalCombatKeyboard.TryGetObjectCategoryControl(key, out var control));
            Assert.AreEqual(expected, control);
        }

        [TestCase(KeyCode.Alpha0)]
        [TestCase(KeyCode.Alpha9)]
        [TestCase(KeyCode.Keypad1)]
        public void TryGetObjectCategoryControl_OtherKey_DoesNotAddressCategory(KeyCode key) =>
            Assert.IsFalse(TacticalCombatKeyboard.TryGetObjectCategoryControl(key, out _));

        [TestCase(
            TacticalCombatHudPanel.SingleObject,
            true,
            false,
            TacticalCombatControl.NextObject
        )]
        [TestCase(
            TacticalCombatHudPanel.SingleObject,
            true,
            true,
            TacticalCombatControl.PreviousObject
        )]
        [TestCase(
            TacticalCombatHudPanel.SingleObject,
            false,
            false,
            TacticalCombatControl.NextObject
        )]
        [TestCase(
            TacticalCombatHudPanel.SingleObject,
            false,
            true,
            TacticalCombatControl.PreviousObject
        )]
        [TestCase(
            TacticalCombatHudPanel.ObjectGroup,
            true,
            false,
            TacticalCombatControl.NextObject
        )]
        [TestCase(
            TacticalCombatHudPanel.ObjectGroup,
            true,
            true,
            TacticalCombatControl.PreviousObject
        )]
        public void TryGetPanelControl_InspectionTransition_UsesCurrentShiftState(
            TacticalCombatHudPanel panel,
            bool keyDown,
            bool shiftDown,
            TacticalCombatControl expected
        )
        {
            Assert.IsTrue(
                TacticalCombatKeyboard.TryGetPanelControl(
                    panel,
                    Key.Tab,
                    keyDown,
                    shiftDown,
                    out var actual
                )
            );
            Assert.AreEqual(expected, actual);
        }

        [TestCase(TacticalCombatHudPanel.Empty, true)]
        [TestCase(TacticalCombatHudPanel.FighterGroup, true)]
        [TestCase(TacticalCombatHudPanel.Orders, true)]
        [TestCase(TacticalCombatHudPanel.Tactics, true)]
        [TestCase(TacticalCombatHudPanel.Commands, true)]
        [TestCase(TacticalCombatHudPanel.WithdrawalConfirmation, true)]
        [TestCase(TacticalCombatHudPanel.ObjectGroup, false)]
        public void TryGetPanelControl_PanelWithoutTabAction_DoesNotActivateControl(
            TacticalCombatHudPanel panel,
            bool keyDown
        ) =>
            Assert.IsFalse(
                TacticalCombatKeyboard.TryGetPanelControl(panel, Key.Tab, keyDown, false, out _)
            );

        [Test]
        public void TryGetPanelControl_ShiftReleasedBeforeTabRelease_ClearsNextInsteadOfPrevious()
        {
            Assert.IsTrue(
                TacticalCombatKeyboard.TryGetPanelControl(
                    TacticalCombatHudPanel.SingleObject,
                    Key.Tab,
                    true,
                    true,
                    out var pressed
                )
            );
            Assert.IsTrue(
                TacticalCombatKeyboard.TryGetPanelControl(
                    TacticalCombatHudPanel.SingleObject,
                    Key.Tab,
                    false,
                    false,
                    out var released
                )
            );

            Assert.AreEqual(TacticalCombatControl.PreviousObject, pressed);
            Assert.AreEqual(TacticalCombatControl.NextObject, released);
        }

        [Test]
        public void TryGetPanelControl_OtherKey_DoesNotTraverseInspection() =>
            Assert.IsFalse(
                TacticalCombatKeyboard.TryGetPanelControl(
                    TacticalCombatHudPanel.SingleObject,
                    Key.Space,
                    true,
                    false,
                    out _
                )
            );

        [TestCase(Key.PageUp, TacticalCombatCameraCommand.ZoomIn)]
        [TestCase(Key.Numpad9, TacticalCombatCameraCommand.ZoomIn)]
        [TestCase(Key.NumpadPlus, TacticalCombatCameraCommand.ZoomIn)]
        [TestCase(Key.PageDown, TacticalCombatCameraCommand.ZoomOut)]
        [TestCase(Key.Numpad3, TacticalCombatCameraCommand.ZoomOut)]
        [TestCase(Key.NumpadMinus, TacticalCombatCameraCommand.ZoomOut)]
        [TestCase(Key.LeftArrow, TacticalCombatCameraCommand.RotateLeft)]
        [TestCase(Key.Numpad4, TacticalCombatCameraCommand.RotateLeft)]
        [TestCase(Key.RightArrow, TacticalCombatCameraCommand.RotateRight)]
        [TestCase(Key.Numpad6, TacticalCombatCameraCommand.RotateRight)]
        [TestCase(Key.UpArrow, TacticalCombatCameraCommand.TiltUp)]
        [TestCase(Key.Numpad8, TacticalCombatCameraCommand.TiltUp)]
        [TestCase(Key.DownArrow, TacticalCombatCameraCommand.TiltDown)]
        [TestCase(Key.Numpad2, TacticalCombatCameraCommand.TiltDown)]
        [TestCase(Key.Home, TacticalCombatCameraCommand.Save)]
        [TestCase(Key.Numpad7, TacticalCombatCameraCommand.Save)]
        [TestCase(Key.End, TacticalCombatCameraCommand.Restore)]
        [TestCase(Key.Numpad1, TacticalCombatCameraCommand.Restore)]
        [TestCase(Key.Enter, TacticalCombatCameraCommand.Focus)]
        [TestCase(Key.NumpadEnter, TacticalCombatCameraCommand.Focus)]
        [TestCase(Key.NumpadPeriod, TacticalCombatCameraCommand.Focus)]
        public void TryGetCameraCommand_CameraKey_ResolvesSemanticOperation(
            Key key,
            TacticalCombatCameraCommand expected
        )
        {
            Assert.IsTrue(TacticalCombatKeyboard.TryGetCameraCommand(key, out var actual));
            Assert.AreEqual(expected, actual);
        }

        [TestCase(Key.Space)]
        [TestCase(Key.Escape)]
        [TestCase(Key.G)]
        [TestCase(Key.F)]
        public void TryGetCameraCommand_OtherKey_DoesNotChangeCamera(Key key) =>
            Assert.IsFalse(TacticalCombatKeyboard.TryGetCameraCommand(key, out _));

        [TestCase(KeyCode.PageUp, TacticalCombatControl.CameraZoomIn)]
        [TestCase(KeyCode.Keypad9, TacticalCombatControl.CameraZoomIn)]
        [TestCase(KeyCode.KeypadPlus, TacticalCombatControl.CameraZoomIn)]
        [TestCase(KeyCode.PageDown, TacticalCombatControl.CameraZoomOut)]
        [TestCase(KeyCode.Keypad3, TacticalCombatControl.CameraZoomOut)]
        [TestCase(KeyCode.KeypadMinus, TacticalCombatControl.CameraZoomOut)]
        [TestCase(KeyCode.LeftArrow, TacticalCombatControl.CameraRotateLeft)]
        [TestCase(KeyCode.Keypad4, TacticalCombatControl.CameraRotateLeft)]
        [TestCase(KeyCode.RightArrow, TacticalCombatControl.CameraRotateRight)]
        [TestCase(KeyCode.Keypad6, TacticalCombatControl.CameraRotateRight)]
        [TestCase(KeyCode.UpArrow, TacticalCombatControl.CameraTiltUp)]
        [TestCase(KeyCode.Keypad8, TacticalCombatControl.CameraTiltUp)]
        [TestCase(KeyCode.DownArrow, TacticalCombatControl.CameraTiltDown)]
        [TestCase(KeyCode.Keypad2, TacticalCombatControl.CameraTiltDown)]
        [TestCase(KeyCode.Home, TacticalCombatControl.SaveCamera)]
        [TestCase(KeyCode.Keypad7, TacticalCombatControl.SaveCamera)]
        [TestCase(KeyCode.End, TacticalCombatControl.RestoreCamera)]
        [TestCase(KeyCode.Keypad1, TacticalCombatControl.RestoreCamera)]
        [TestCase(KeyCode.Return, TacticalCombatControl.FocusCamera)]
        [TestCase(KeyCode.KeypadEnter, TacticalCombatControl.FocusCamera)]
        [TestCase(KeyCode.KeypadPeriod, TacticalCombatControl.FocusCamera)]
        public void TryGetCameraControl_NativeCameraKey_ResolvesAuthoredImage(
            KeyCode key,
            TacticalCombatControl expected
        )
        {
            Assert.IsTrue(
                TacticalCombatKeyboard.TryGetCameraControl(key, out TacticalCombatControl control)
            );
            Assert.AreEqual(expected, control);
        }

        [TestCase(KeyCode.F1, 0)]
        [TestCase(KeyCode.F2, 1)]
        [TestCase(KeyCode.F3, 2)]
        [TestCase(KeyCode.F4, 3)]
        [TestCase(KeyCode.F5, 4)]
        [TestCase(KeyCode.F6, 5)]
        [TestCase(KeyCode.F7, 6)]
        [TestCase(KeyCode.F8, 7)]
        [TestCase(KeyCode.F9, 8)]
        [TestCase(KeyCode.F10, 9)]
        [TestCase(KeyCode.F11, 10)]
        [TestCase(KeyCode.F12, 11)]
        public void TryGetCameraCategory_FunctionKey_ResolvesZeroBasedCategory(
            KeyCode key,
            int expected
        )
        {
            Assert.IsTrue(TacticalCombatKeyboard.TryGetCameraCategory(key, out int category));
            Assert.AreEqual(expected, category);
        }

        [TestCase(KeyCode.Space)]
        [TestCase(KeyCode.Tab)]
        [TestCase(KeyCode.F13)]
        [TestCase(KeyCode.Alpha1)]
        public void TryGetCameraCategory_NonCategoryKey_RejectsShortcut(KeyCode key) =>
            Assert.IsFalse(TacticalCombatKeyboard.TryGetCameraCategory(key, out _));

        [TestCase(KeyCode.Space)]
        [TestCase(KeyCode.Tab)]
        [TestCase(KeyCode.F1)]
        public void TryGetCameraControl_NonCameraKey_RejectsImageActivation(KeyCode key) =>
            Assert.IsFalse(TacticalCombatKeyboard.TryGetCameraControl(key, out _));
    }
}
