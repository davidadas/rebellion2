using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            UnityEngine.Object.DestroyImmediate(cameraObject);
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

        /// <summary>
        /// Checks normal and shift panning use half their preceding near-camera speeds.
        /// </summary>
        /// <param name="fast">Whether shift panning is active.</param>
        /// <param name="expected">The expected travel in a tenth of a second.</param>
        [TestCase(false, 50f)]
        [TestCase(true, 150f)]
        public void Pan_NearCamera_UsesHalvedMinimumSpeed(bool fast, float expected)
        {
            controller.Pan(Vector2.up, 0.1f, fast);

            Assert.That(controller.Pivot.z, Is.EqualTo(expected).Within(0.001f));
        }

        /// <summary>
        /// Checks distance-scaled pan speed is also halved for normal and shift navigation.
        /// </summary>
        /// <param name="fast">Whether shift panning is active.</param>
        /// <param name="expected">The expected travel in a tenth of a second.</param>
        [TestCase(false, 150f)]
        [TestCase(true, 450f)]
        public void Pan_DistantCamera_UsesHalvedDistanceSpeed(bool fast, float expected)
        {
            cameraObject.transform.position = new Vector3(0f, 0f, -2000f);
            controller.Configure(CreatePlayableBounds(), Vector3.zero);

            controller.Pan(Vector2.up, 0.1f, fast);

            Assert.That(controller.Pivot.z, Is.EqualTo(expected).Within(0.001f));
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

        [TestCase(4f / 3f)]
        [TestCase(16f / 9f)]
        [TestCase(21f / 9f)]
        [TestCase(9f / 16f)]
        public void Frame_MixedHullBounds_FitsEveryCornerInsideUsableViewport(float aspect)
        {
            UnityEngine.Camera camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.aspect = aspect;
            camera.fieldOfView = 48f;
            Bounds[] hulls = CreateFleetBounds();
            Rect viewport = new Rect(0.17f, 0.22f, 0.78f, 0.72f);
            Quaternion rotation = camera.transform.rotation;

            controller.Frame(hulls, viewport);

            AssertVisible(camera, hulls, viewport);
            Assert.That(Quaternion.Angle(rotation, camera.transform.rotation), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator Frame_AspectRatioChanges_RefitsOpeningView()
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            yield return new EnterPlayMode();
            SetUp();
            UnityEngine.Camera camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.aspect = 21f / 9f;
            Rect viewport = new Rect(0.05f, 0.05f, 0.9f, 0.9f);
            Bounds[] hulls = CreateFleetBounds();
            controller.Frame(hulls, viewport);
            float originalDistance = controller.Distance;

            camera.aspect = 0.6f;
            yield return null;

            AssertVisible(camera, hulls, viewport);
            Assert.That(controller.Distance, Is.GreaterThan(originalDistance));
            UnityEngine.Object.DestroyImmediate(cameraObject);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Frame_PlayerNavigates_StopsAutomaticReframing()
        {
            UnityEngine.Object.DestroyImmediate(cameraObject);
            yield return new EnterPlayMode();
            SetUp();
            UnityEngine.Camera camera = cameraObject.GetComponent<UnityEngine.Camera>();
            controller.Frame(CreateFleetBounds(), new Rect(0.05f, 0.05f, 0.9f, 0.9f));
            controller.Orbit(new Vector2(40f, 20f));
            Vector3 position = camera.transform.position;

            camera.aspect = 0.6f;
            yield return null;

            Assert.AreEqual(position, camera.transform.position);
            UnityEngine.Object.DestroyImmediate(cameraObject);
            yield return new ExitPlayMode();
        }

        [Test]
        public void Frame_FirstZoom_PreservesViewingAngle()
        {
            controller.Frame(CreateFleetBounds(), new Rect(0.05f, 0.05f, 0.9f, 0.9f));
            Quaternion rotation = cameraObject.transform.rotation;

            controller.ZoomByScroll(120f, RuntimePlatform.WindowsPlayer);

            Assert.That(
                Quaternion.Angle(rotation, cameraObject.transform.rotation),
                Is.LessThan(0.01f)
            );
        }

        [Test]
        public void Frame_EmptyFleet_PreservesCameraPose()
        {
            Vector3 position = cameraObject.transform.position;

            controller.Frame(Array.Empty<Bounds>(), new Rect(0f, 0f, 1f, 1f));

            Assert.AreEqual(position, cameraObject.transform.position);
        }

        [Test]
        public void Frame_ViewportExcludesProjectionCenter_RejectsInvalidViewport()
        {
            Assert.Throws<ArgumentException>(() =>
                controller.Frame(CreateFleetBounds(), new Rect(0.6f, 0f, 0.4f, 1f))
            );
        }

        /// <summary>
        /// Creates world envelopes with substantial width, height and depth variation.
        /// </summary>
        /// <returns>The hull envelopes.</returns>
        private static Bounds[] CreateFleetBounds() =>
            new[]
            {
                new Bounds(new Vector3(-180f, 20f, -120f), new Vector3(90f, 25f, 140f)),
                new Bounds(new Vector3(210f, 100f, 90f), new Vector3(40f, 70f, 80f)),
                new Bounds(new Vector3(50f, -80f, 300f), new Vector3(4f, 2f, 5f)),
            };

        /// <summary>
        /// Projects all corners through Unity's camera rather than duplicating fit calculations.
        /// </summary>
        /// <param name="camera">The configured camera.</param>
        /// <param name="hulls">The fleet envelopes.</param>
        /// <param name="viewport">The required visible viewport.</param>
        private static void AssertVisible(UnityEngine.Camera camera, Bounds[] hulls, Rect viewport)
        {
            foreach (Bounds hull in hulls)
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point =
                        hull.center
                        + Vector3.Scale(
                            hull.extents,
                            new Vector3(
                                (corner & 1) == 0 ? -1f : 1f,
                                (corner & 2) == 0 ? -1f : 1f,
                                (corner & 4) == 0 ? -1f : 1f
                            )
                        );
                    Vector3 projected = camera.WorldToViewportPoint(point);
                    Assert.That(
                        projected.x,
                        Is.InRange(viewport.xMin - 0.0001f, viewport.xMax + 0.0001f)
                    );
                    Assert.That(
                        projected.y,
                        Is.InRange(viewport.yMin - 0.0001f, viewport.yMax + 0.0001f)
                    );
                    Assert.That(projected.z, Is.InRange(camera.nearClipPlane, camera.farClipPlane));
                }
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
