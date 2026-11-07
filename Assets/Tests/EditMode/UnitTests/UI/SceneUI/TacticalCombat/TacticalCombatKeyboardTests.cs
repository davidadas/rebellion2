using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatKeyboardTests
    {
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
    }
}
