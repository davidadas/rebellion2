using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatHudLayoutTests
    {
        [Test]
        public void Controls_CategoryStrips_ExposeEightObjectAndFourFighterSlots()
        {
            var controls = TacticalCombatHudLayout.Controls;
            Assert.AreEqual(
                8,
                controls.Count(value =>
                    value.Control >= TacticalCombatControl.ObjectGroup1
                    && value.Control <= TacticalCombatControl.ObjectGroup8
                )
            );
            Assert.AreEqual(
                4,
                controls.Count(value =>
                    value.Control >= TacticalCombatControl.FighterGroup1
                    && value.Control <= TacticalCombatControl.FighterGroup4
                )
            );
        }

        [Test]
        public void Controls_SourceBounds_FitTheirOwningSurfacesWithoutCoveringViewport()
        {
            foreach (TacticalCombatHudControl control in TacticalCombatHudLayout.Controls)
            {
                RectInt parent =
                    control.Panel == TacticalCombatHudPanel.Frame
                        ? new RectInt(0, 0, 640, 480)
                        : new RectInt(0, 0, 149, 236);
                Assert.IsTrue(parent.Contains(control.Bounds.min), control.Control.ToString());
                Assert.LessOrEqual(control.Bounds.xMax, parent.xMax);
                Assert.LessOrEqual(control.Bounds.yMax, parent.yMax);
                if (control.Panel == TacticalCombatHudPanel.Frame)
                    Assert.IsFalse(
                        control.Bounds.Overlaps(TacticalCombatHudLayout.Viewport),
                        control.Control.ToString()
                    );
            }
        }

        [Test]
        public void Controls_WithdrawalConfirmation_UsesPanelLocalAcceptAndCancelImages()
        {
            TacticalCombatHudControl[] controls = TacticalCombatHudLayout
                .Controls.Where(value =>
                    value.Panel == TacticalCombatHudPanel.WithdrawalConfirmation
                )
                .ToArray();

            Assert.AreEqual(2, controls.Length);
            Assert.AreEqual(TacticalCombatControl.ConfirmWithdrawal, controls[0].Control);
            Assert.AreEqual(new RectInt(66, 202, 27, 25), controls[0].Bounds);
            Assert.AreEqual("confirm", controls[0].ArtworkKey);
            Assert.AreEqual(TacticalCombatControl.CancelWithdrawal, controls[1].Control);
            Assert.AreEqual(new RectInt(106, 202, 27, 25), controls[1].Bounds);
            Assert.AreEqual("cancel", controls[1].ArtworkKey);
            Assert.IsTrue(controls.All(value => !value.BitmapHitTest));
        }

        [Test]
        public void Controls_HeldRepeat_IsLimitedToSixCameraMotionImages()
        {
            TacticalCombatHudControl[] repeated = TacticalCombatHudLayout
                .Controls.Where(value => value.RepeatMilliseconds != 0)
                .ToArray();
            Assert.AreEqual(6, repeated.Length);
            Assert.IsTrue(repeated.All(value => value.RepeatMilliseconds == 100));
            Assert.IsTrue(repeated.All(value => value.Panel == TacticalCombatHudPanel.Frame));
        }
    }
}
