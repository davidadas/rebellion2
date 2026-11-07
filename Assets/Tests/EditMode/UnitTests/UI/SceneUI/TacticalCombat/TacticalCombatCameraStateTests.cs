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
