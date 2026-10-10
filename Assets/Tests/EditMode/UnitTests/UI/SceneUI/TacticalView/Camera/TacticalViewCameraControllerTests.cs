using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.TacticalView.Camera
{
    [TestFixture]
    public sealed class TacticalViewCameraControllerTests
    {
        private GameObject cameraObject;
        private TacticalViewCameraController controller;

        /// <summary>
        /// Creates a configured tactical camera for each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Tactical Camera", typeof(UnityEngine.Camera));
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 100f, -500f),
                Quaternion.LookRotation(new Vector3(0f, -100f, 500f))
            );
            controller = cameraObject.AddComponent<TacticalViewCameraController>();
            controller.Configure(CreatePlayableBounds(), Vector3.zero);
        }

        /// <summary>
        /// Releases the tactical camera after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void Orbit_PointerMovement_MovesCameraAroundStablePivot()
        {
            Vector3 originalPosition = cameraObject.transform.position;
            float originalDistance = controller.Distance;

            controller.Orbit(new Vector2(100f, 0f));

            Assert.AreEqual(Vector3.zero, controller.Pivot);
            Assert.That(controller.Distance, Is.EqualTo(originalDistance).Within(0.001f));
            Assert.That(cameraObject.transform.position, Is.Not.EqualTo(originalPosition));
        }

        [Test]
        public void Pan_ForwardInput_MovesPivotForwardInsidePlayableBounds()
        {
            controller.Pan(Vector2.up, 1f, false);

            Assert.That(controller.Pivot.z, Is.GreaterThan(0f));
            Assert.That(controller.Pivot.z, Is.LessThanOrEqualTo(3000f));
        }

        [Test]
        public void ZoomByScroll_PositiveWindowsWheelUnit_ReducesCameraDistance()
        {
            float originalDistance = controller.Distance;

            controller.ZoomByScroll(120f, RuntimePlatform.WindowsPlayer);

            Assert.That(controller.Distance, Is.EqualTo(originalDistance * 0.85f).Within(0.001f));
        }

        [Test]
        public void Turn_FullPositiveInputForOneSecond_OrbitsNinetyDegrees()
        {
            controller.Turn(1f, 1f);

            Vector3 planarOffset = cameraObject.transform.position - controller.Pivot;
            planarOffset.y = 0f;
            Assert.That(planarOffset.normalized.z, Is.EqualTo(0f).Within(0.001f));
            Assert.That(planarOffset.normalized.x, Is.EqualTo(1f).Within(0.001f));
        }

        /// <summary>
        /// Creates the authored playable volume used by camera navigation tests.
        /// </summary>
        /// <returns>The playable volume.</returns>
        private static BattleMapBounds CreatePlayableBounds()
        {
            return new BattleMapBounds
            {
                MinimumX = -2500f,
                MaximumX = 2500f,
                MinimumY = -1000f,
                MaximumY = 1000f,
                MinimumZ = -3000f,
                MaximumZ = 3000f,
            };
        }
    }
}
