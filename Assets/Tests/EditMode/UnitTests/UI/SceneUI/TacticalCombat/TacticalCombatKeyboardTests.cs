using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatKeyboardTests
    {
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
