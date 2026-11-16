using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalCombat
{
    [TestFixture]
    public sealed class TacticalCombatCameraStateTests
    {
        [TestCase(0, -30)]
        [TestCase(1, 150)]
        public void Constructor_PlayerFaction_UsesFacingAngle(int faction, int yaw)
        {
            TacticalCombatCameraState camera = new(170, faction);

            Assert.AreEqual(yaw, camera.Yaw);
            Assert.AreEqual(30, camera.Pitch);
            Assert.AreEqual(0.2f, camera.Field);
            Assert.AreEqual(5, camera.AngularStep);
        }

        [Test]
        public void Apply_Zooming_ChangesFieldAndRotationIncrementTogether()
        {
            TacticalCombatCameraState camera = new(170, 0);

            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            camera.Apply(TacticalCombatCameraCommand.RotateRight);

            Assert.AreEqual((float)(0.2f * 0.9), camera.Field);
            Assert.AreEqual(4, camera.ZoomLevel);
            Assert.AreEqual(4, camera.AngularStep);
            Assert.AreEqual(-26, camera.Yaw);
        }

        [Test]
        public void Apply_ZoomLevelUnderflow_RetainsFullCounterAndMinimumIncrement()
        {
            TacticalCombatCameraState camera = new(170, 0);

            Repeat(camera, TacticalCombatCameraCommand.ZoomIn, 6);

            Assert.AreEqual(uint.MaxValue, camera.ZoomLevel);
            Assert.AreEqual(1, camera.AngularStep);
        }

        [TestCase(TacticalCombatCameraCommand.ZoomIn, 0.005f)]
        [TestCase(TacticalCombatCameraCommand.ZoomOut, 1.5f)]
        public void Apply_ZoomLimit_RetainsFieldBound(
            TacticalCombatCameraCommand command,
            float expected
        )
        {
            TacticalCombatCameraState camera = new(170, 0);

            Repeat(camera, command, 200);

            Assert.AreEqual(expected, camera.Field);
        }

        [Test]
        public void Apply_PitchCrossingLimit_KeepsFinalIncrementThenStops()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            Repeat(camera, TacticalCombatCameraCommand.TiltUp, 14);
            Assert.AreEqual(86, camera.Pitch);

            camera.Apply(TacticalCombatCameraCommand.ZoomOut);
            camera.Apply(TacticalCombatCameraCommand.TiltUp);
            Assert.AreEqual(91, camera.Pitch);
            camera.Apply(TacticalCombatCameraCommand.TiltUp);

            Assert.AreEqual(91, camera.Pitch);
        }

        [Test]
        public void GetPose_YawCrossesHalfTurn_WrapsBeforeRendering()
        {
            TacticalCombatCameraState camera = new(170, 1);
            Repeat(camera, TacticalCombatCameraCommand.RotateRight, 7);
            Assert.AreEqual(185, camera.Yaw);

            camera.GetPose(Vector3.zero, out _, out _);

            Assert.AreEqual(-175, camera.Yaw);
        }

        [Test]
        public void Track_MovingParticipant_UpdatesAimWithoutMovingOrbit()
        {
            TacticalCombatCameraState camera = new(170, 0);
            Vector3 target = new(20, 10, 30);
            camera.GetPose(Vector3.zero, out Vector3 originalPosition, out _);
            camera.Track(() => target);
            camera.GetPose(Vector3.zero, out Vector3 position, out Quaternion firstRotation);
            Assert.AreEqual(originalPosition, position);
            AssertAimsAt(position, firstRotation, target);

            target = new Vector3(-40, 5, -10);
            camera.GetPose(Vector3.zero, out position, out Quaternion secondRotation);

            Assert.AreEqual(originalPosition, position);
            AssertAimsAt(position, secondRotation, target);
            Assert.Greater(Quaternion.Angle(firstRotation, secondRotation), 0);
        }

        [Test]
        public void Apply_SavedView_RestoresAnglesZoomAndLiveTrackedObject()
        {
            TacticalCombatCameraState camera = new(170, 0);
            Vector3 target = new(10, 5, 20);
            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            camera.Apply(TacticalCombatCameraCommand.RotateLeft);
            camera.Track(() => target);
            camera.Apply(TacticalCombatCameraCommand.Save);
            camera.Apply(TacticalCombatCameraCommand.ZoomOut);
            camera.Apply(TacticalCombatCameraCommand.TiltDown);
            camera.Track(null);
            target = new Vector3(80, 30, 10);

            camera.Apply(TacticalCombatCameraCommand.Restore);
            camera.GetPose(Vector3.zero, out Vector3 position, out Quaternion rotation);

            Assert.AreEqual(-34, camera.Yaw);
            Assert.AreEqual(30, camera.Pitch);
            Assert.AreEqual((float)(0.2f * 0.9), camera.Field);
            Assert.AreEqual(4, camera.AngularStep);
            Assert.AreEqual(4, camera.ZoomLevel);
            AssertAimsAt(position, rotation, target);
        }

        [Test]
        public void Apply_RestoreWithoutSavedView_ResetsAnglesAndFieldWhileRetainingTrackingAndIncrement()
        {
            TacticalCombatCameraState camera = new(170, 1);
            Vector3 target = new(10, 5, 20);
            camera.Track(() => target);
            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            camera.Apply(TacticalCombatCameraCommand.RotateLeft);
            camera.Apply(TacticalCombatCameraCommand.TiltDown);

            camera.Apply(TacticalCombatCameraCommand.Restore);
            camera.GetPose(Vector3.zero, out Vector3 position, out Quaternion rotation);

            Assert.AreEqual(150, camera.Yaw);
            Assert.AreEqual(30, camera.Pitch);
            Assert.AreEqual(0.2f, camera.Field);
            Assert.AreEqual(4, camera.AngularStep);
            Assert.AreEqual(4, camera.ZoomLevel);
            AssertAimsAt(position, rotation, target);
        }

        [Test]
        public void Apply_RestoringTrackedView_RestoresItsObjectIdentity()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.Track(() => Vector3.zero, 21);
            camera.Apply(TacticalCombatCameraCommand.Save);
            camera.Track(() => Vector3.one, 42);

            camera.Apply(TacticalCombatCameraCommand.Restore);

            Assert.AreEqual(21, camera.TrackedObjectIndex);
        }

        [Test]
        public void Track_ReplacingObjectWithPoint_ClearsObjectIdentity()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.Track(() => Vector3.zero, 21);

            camera.Track(() => Vector3.one);

            Assert.IsNull(camera.TrackedObjectIndex);
        }

        [Test]
        public void OpenCategory_InitialCloseUp_PreservesAndAutomaticallySavesPrimaryView()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            camera.OpenCategory(
                3,
                () => new TacticalCombatCameraFocus(21, Vector3.zero, Quaternion.identity, 4, null)
            );

            Assert.AreEqual(3, camera.Category);
            Assert.AreEqual(0.02f, camera.Field);
            Assert.AreEqual(0.1f, camera.NearPlane);
            camera.Apply(TacticalCombatCameraCommand.Restore);

            Assert.IsFalse(camera.IsCategoryView);
            Assert.AreEqual((float)(0.2f * 0.9), camera.Field);
            Assert.AreEqual(1, camera.NearPlane);
            Assert.AreEqual(4, camera.ZoomLevel);
        }

        [TestCase(TacticalCombatCameraCommand.ZoomIn, 0.005f)]
        [TestCase(TacticalCombatCameraCommand.ZoomOut, 0.2f)]
        public void Apply_CategoryZoomLimit_UsesIndependentFieldWithoutChangingPrimaryStep(
            TacticalCombatCameraCommand command,
            float expected
        )
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.OpenCategory(0, () => null);

            Repeat(camera, command, 200);

            Assert.AreEqual(expected, camera.Field);
            Assert.AreEqual(5, camera.ZoomLevel);
            Assert.AreEqual(5, camera.AngularStep);
        }

        [TestCase(TacticalCombatCameraCommand.RotateLeft)]
        [TestCase(TacticalCombatCameraCommand.RotateRight)]
        [TestCase(TacticalCombatCameraCommand.TiltUp)]
        [TestCase(TacticalCombatCameraCommand.TiltDown)]
        public void Apply_CategoryAngularCommand_LeavesPrimaryAnglesUnchanged(
            TacticalCombatCameraCommand command
        )
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.OpenCategory(0, () => null);

            camera.Apply(command);

            Assert.AreEqual(-30, camera.Yaw);
            Assert.AreEqual(30, camera.Pitch);
            Assert.IsTrue(camera.IsCategoryView);
        }

        [Test]
        public void Apply_SaveDuringCategory_SavesPrimaryFieldInsteadOfCloseUpField()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            camera.OpenCategory(0, () => null);
            camera.Apply(TacticalCombatCameraCommand.ZoomOut);

            camera.Apply(TacticalCombatCameraCommand.Save);
            camera.Apply(TacticalCombatCameraCommand.Restore);

            Assert.AreEqual((float)(0.2f * 0.9), camera.Field);
            Assert.IsFalse(camera.IsCategoryView);
        }

        [Test]
        public void Track_CategoryView_RestoresPrimaryBeforeReplacingItsTrackedObject()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.Track(() => Vector3.zero, 21);
            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            camera.OpenCategory(0, () => null);
            camera.Apply(TacticalCombatCameraCommand.ZoomOut);

            camera.Track(() => Vector3.one, 42);

            Assert.IsFalse(camera.IsCategoryView);
            Assert.AreEqual(42, camera.TrackedObjectIndex);
            Assert.AreEqual((float)(0.2f * 0.9), camera.Field);
        }

        [Test]
        public void Track_CategoryWithEarlierSavedPrimary_RestoresEarlierSnapshot()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.Apply(TacticalCombatCameraCommand.Save);
            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            camera.Apply(TacticalCombatCameraCommand.RotateRight);
            camera.OpenCategory(0, () => null);

            camera.Track(() => Vector3.one, 42);

            Assert.AreEqual(0.2f, camera.Field);
            Assert.AreEqual(-30, camera.Yaw);
            Assert.AreEqual(42, camera.TrackedObjectIndex);
        }

        [Test]
        public void GetPose_CategoryWithoutSecondary_UsesPrimaryLocalOffsetAndNegativeForward()
        {
            TacticalCombatCameraState camera = new(170, 0);
            Vector3 primary = new(10, 20, 30);
            Quaternion facing = Quaternion.Euler(0, 90, 0);
            camera.OpenCategory(
                0,
                () => new TacticalCombatCameraFocus(21, primary, facing, 4, null)
            );

            camera.GetPose(Vector3.zero, out Vector3 position, out Quaternion rotation);

            Assert.That(
                Vector3.Distance(primary + facing * new Vector3(-4, 4, 8), position),
                Is.LessThan(0.00001f)
            );
            Assert.That(
                Vector3.Dot(rotation * Vector3.forward, -(facing * Vector3.forward)),
                Is.EqualTo(1).Within(0.00001f)
            );
            Assert.AreEqual(21, camera.TrackedObjectIndex);
        }

        [Test]
        public void GetPose_SecondaryWithRelativeHorizontalAndVerticalOffset_UsesNormalizedPlaneDirection()
        {
            TacticalCombatCameraState camera = new(170, 0);
            Vector3 primary = new(10, 20, 30);
            Quaternion facing = Quaternion.Euler(0, 90, 0);
            Vector3 secondary = primary + facing * new Vector3(3, 4, 12);
            camera.OpenCategory(
                0,
                () => new TacticalCombatCameraFocus(21, primary, facing, 4, secondary)
            );

            camera.GetPose(Vector3.zero, out Vector3 position, out Quaternion rotation);

            Assert.That(
                Vector3.Distance(primary + facing * new Vector3(2.4f, 3.2f, 8), position),
                Is.LessThan(0.00001f)
            );
            AssertAimsAt(position, rotation, secondary);
        }

        [TestCase(0)]
        [TestCase(0.0009f)]
        public void GetPose_SecondaryWithoutResolvedPlaneDirection_RetainsDefaultLocalOffset(
            float relativeX
        )
        {
            TacticalCombatCameraState camera = new(170, 0);
            Vector3 primary = new(10, 20, 30);
            camera.OpenCategory(
                0,
                () =>
                    new TacticalCombatCameraFocus(
                        21,
                        primary,
                        Quaternion.identity,
                        4,
                        primary + new Vector3(relativeX, 0, 12)
                    )
            );

            camera.GetPose(Vector3.zero, out Vector3 position, out _);

            Assert.AreEqual(primary + new Vector3(-4, 4, 8), position);
        }

        [Test]
        public void GetPose_ZeroModelSpan_DoesNotInventCameraDistance()
        {
            TacticalCombatCameraState camera = new(170, 0);
            Vector3 primary = new(10, 20, 30);
            camera.OpenCategory(
                8,
                () => new TacticalCombatCameraFocus(21, primary, Quaternion.identity, 0, null)
            );

            camera.GetPose(Vector3.zero, out Vector3 position, out Quaternion rotation);

            Assert.AreEqual(primary, position);
            Assert.That(
                Vector3.Dot(rotation * Vector3.forward, Vector3.back),
                Is.EqualTo(1).Within(0.00001f)
            );
        }

        [Test]
        public void GetPose_CoincidentSecondaryWithZeroSpan_RetainsPreviousOrientation()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.GetPose(Vector3.zero, out _, out Quaternion previous);
            Vector3 primary = new(10, 20, 30);
            camera.OpenCategory(
                8,
                () => new TacticalCombatCameraFocus(21, primary, Quaternion.identity, 0, primary)
            );

            camera.GetPose(Vector3.zero, out Vector3 position, out Quaternion rotation);

            Assert.AreEqual(primary, position);
            Assert.That(Quaternion.Angle(previous, rotation), Is.LessThan(0.0001f));
        }

        [Test]
        public void GetPose_CategoryPrimaryDisappears_ReturnsToCurrentPrimaryView()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            camera.GetPose(Vector3.zero, out Vector3 before, out Quaternion beforeRotation);
            TacticalCombatCameraFocus? focus = new(21, Vector3.one, Quaternion.identity, 4, null);
            camera.OpenCategory(0, () => focus);
            camera.GetPose(Vector3.zero, out _, out _);
            focus = null;

            camera.GetPose(Vector3.zero, out Vector3 position, out Quaternion rotation);

            Assert.IsFalse(camera.IsCategoryView);
            Assert.IsNull(camera.Category);
            Assert.AreEqual(before, position);
            Assert.That(Quaternion.Angle(beforeRotation, rotation), Is.LessThan(0.0001f));
            Assert.AreEqual((float)(0.2f * 0.9), camera.Field);
        }

        [Test]
        public void OpenCategory_ReplacingCloseUp_RestartsOnlyCategoryField()
        {
            TacticalCombatCameraState camera = new(170, 0);
            camera.Apply(TacticalCombatCameraCommand.ZoomIn);
            camera.OpenCategory(0, () => null);
            camera.Apply(TacticalCombatCameraCommand.ZoomOut);

            camera.OpenCategory(8, () => null);

            Assert.AreEqual(8, camera.Category);
            Assert.AreEqual(0.02f, camera.Field);
            camera.Apply(TacticalCombatCameraCommand.Restore);
            Assert.AreEqual((float)(0.2f * 0.9), camera.Field);
        }

        /// <summary>
        /// Applies a repeated camera operation without introducing a frame-rate dependency.
        /// </summary>
        /// <param name="camera">The primary view.</param>
        /// <param name="command">The operation to repeat.</param>
        /// <param name="count">The number of operations.</param>
        private static void Repeat(
            TacticalCombatCameraState camera,
            TacticalCombatCameraCommand command,
            int count
        )
        {
            for (int index = 0; index < count; index++)
                camera.Apply(command);
        }

        /// <summary>
        /// Checks the public camera pose against its live tracked point.
        /// </summary>
        /// <param name="position">The camera's world position.</param>
        /// <param name="rotation">The camera's world orientation.</param>
        /// <param name="target">The expected focus point.</param>
        private static void AssertAimsAt(Vector3 position, Quaternion rotation, Vector3 target) =>
            Assert.That(
                Vector3.Dot(rotation * Vector3.forward, (target - position).normalized),
                Is.EqualTo(1).Within(0.00001f)
            );
    }
}
