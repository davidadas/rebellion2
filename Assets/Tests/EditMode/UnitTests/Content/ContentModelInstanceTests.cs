using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Content
{
    [TestFixture]
    public sealed class ContentModelInstanceTests
    {
        private GameObject pivot;
        private ContentModelInstance instance;
        private Transform mesh;

        /// <summary>Creates a model hierarchy with a nonuniform, off-center mesh.</summary>
        [SetUp]
        public void SetUp()
        {
            pivot = new GameObject("Pivot");
            Transform scene = new GameObject("Model Scene").transform;
            scene.SetParent(pivot.transform, false);
            Transform model = new GameObject("Model").transform;
            model.SetParent(scene, false);
            mesh = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            mesh.SetParent(model, false);
            mesh.localPosition = new Vector3(3f, 1f, -2f);
            mesh.localScale = new Vector3(4f, 2f, 10f);
            instance = new ContentModelInstance(scene, model);
        }

        /// <summary>Disposes the model and its isolated scene objects.</summary>
        [TearDown]
        public void TearDown()
        {
            instance.Dispose();
            Object.DestroyImmediate(pivot);
        }

        /// <summary>Checks that heading cannot alter authored local model dimensions.</summary>
        /// <param name="yaw">The parent heading in degrees.</param>
        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(180f)]
        public void ApplyPose_RotatedPivot_PreservesAuthoredModelDimensions(float yaw)
        {
            pivot.transform.SetPositionAndRotation(
                new Vector3(120f, 3f, -1700f),
                Quaternion.Euler(12f, yaw, 0f)
            );

            instance.ApplyPose(pivot.transform, 5f, Vector3.zero, false, true, true, -1);

            Vector3 axisX = pivot.transform.InverseTransformVector(
                mesh.TransformVector(Vector3.right)
            );
            Vector3 axisY = pivot.transform.InverseTransformVector(
                mesh.TransformVector(Vector3.up)
            );
            Vector3 axisZ = pivot.transform.InverseTransformVector(
                mesh.TransformVector(Vector3.forward)
            );
            Assert.That(axisX.magnitude, Is.EqualTo(4f).Within(0.001f));
            Assert.That(axisY.magnitude, Is.EqualTo(2f).Within(0.001f));
            Assert.That(axisZ.magnitude, Is.EqualTo(10f).Within(0.001f));
            Assert.That(
                Vector3.Distance(mesh.position, pivot.transform.position),
                Is.LessThan(0.001f)
            );
        }

        /// <summary>Checks that repeated posing with normalization does not accumulate scale.</summary>
        [Test]
        public void ApplyPose_RepeatedNormalizedPose_PreservesScale()
        {
            instance.ApplyPose(pivot.transform, 5f, Vector3.zero, false, true, true, -1);
            Vector3 scale = instance.ModelRoot.localScale;

            instance.ApplyPose(pivot.transform, 5f, Vector3.zero, false, true, true, -1);

            Assert.That(
                Vector3.Distance(scale, instance.ModelRoot.localScale),
                Is.LessThan(0.001f)
            );
        }
    }
}
