using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class VerticalGradientEffectTests
    {
        private GameObject _gameObject;
        private VerticalGradientEffect _effect;

        /// <summary>
        /// Creates the graphic and gradient effect used by each test.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject(
                "Gradient",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(VerticalGradientEffect)
            );
            _effect = _gameObject.GetComponent<VerticalGradientEffect>();
        }

        /// <summary>
        /// Destroys the test graphic after each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void ModifyMesh_Quad_AppliesConfiguredColorsAtVerticalEdges()
        {
            _effect.SetColors(Color.red, Color.blue);
            using VertexHelper vertices = CreateQuad();

            _effect.ModifyMesh(vertices);

            UIVertex vertex = default;
            vertices.PopulateUIVertex(ref vertex, 0);
            Assert.AreEqual((Color32)Color.blue, vertex.color);
            vertices.PopulateUIVertex(ref vertex, 1);
            Assert.AreEqual((Color32)Color.red, vertex.color);
            vertices.PopulateUIVertex(ref vertex, 2);
            Assert.AreEqual((Color32)Color.red, vertex.color);
            vertices.PopulateUIVertex(ref vertex, 3);
            Assert.AreEqual((Color32)Color.blue, vertex.color);
        }

        /// <summary>
        /// Creates a white unit quad for direct mesh-effect verification.
        /// </summary>
        /// <returns>The populated quad mesh.</returns>
        private static VertexHelper CreateQuad()
        {
            VertexHelper vertices = new VertexHelper();
            vertices.AddVert(new Vector3(0f, 0f), Color.white, Vector2.zero);
            vertices.AddVert(new Vector3(0f, 10f), Color.white, Vector2.up);
            vertices.AddVert(new Vector3(10f, 10f), Color.white, Vector2.one);
            vertices.AddVert(new Vector3(10f, 0f), Color.white, Vector2.right);
            vertices.AddTriangle(0, 1, 2);
            vertices.AddTriangle(2, 3, 0);
            return vertices;
        }
    }
}
