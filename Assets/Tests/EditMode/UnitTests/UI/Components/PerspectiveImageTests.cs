using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public class PerspectiveImageTests
    {
        private GameObject _root;
        private PerspectiveImage _image;
        private Mesh _mesh;
        private Texture2D _texture;
        private Sprite _normal;
        private Sprite _pressed;

        /// <summary>
        /// Creates a projected image and sprites for its public rendering contract.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _root = new GameObject(
                "Projected control",
                typeof(RectTransform),
                typeof(PerspectiveImage)
            );
            _image = _root.GetComponent<PerspectiveImage>();
            _image.rectTransform.pivot = Vector2.zero;
            _image.rectTransform.sizeDelta = new Vector2(100, 80);
            _mesh = new Mesh();
            _texture = new Texture2D(8, 8);
            _normal = Sprite.Create(_texture, new Rect(0, 0, 8, 8), Vector2.one / 2);
            _pressed = Sprite.Create(_texture, new Rect(2, 2, 4, 4), Vector2.one / 2);
            _image.sprite = _normal;
        }

        /// <summary>
        /// Releases the native objects owned by each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_mesh);
            Object.DestroyImmediate(_normal);
            Object.DestroyImmediate(_pressed);
            Object.DestroyImmediate(_texture);
        }

        [Test]
        public void WriteMesh_FittedSurface_ReachesAllFourAuthoredCorners()
        {
            UIQuadrilateral surface = CreateSurface();
            _image.Configure(surface, surface);

            _image.WriteMesh(_mesh);

            Vector3[] positions = _mesh.vertices;
            Vector2[] uv = _mesh.uv;
            for (int corner = 0; corner < 4; corner++)
            {
                int index = Array.IndexOf(uv, UIQuadrilateral.Unit[corner]);
                Assert.That(index, Is.GreaterThanOrEqualTo(0));
                Vector2 expected = Vector2.Scale(surface[corner], new Vector2(100, 80));
                Assert.That(Vector2.Distance(positions[index], expected), Is.LessThan(0.0001f));
            }
        }

        [Test]
        public void WriteMesh_AnimationFrameChanges_PreservesProjection()
        {
            _image.Configure(CreateSurface(), CreateSurface());
            _image.WriteMesh(_mesh);
            Vector3[] first = _mesh.vertices;

            _image.sprite = _pressed;
            _image.WriteMesh(_mesh);

            CollectionAssert.AreEqual(first, _mesh.vertices);
        }

        [Test]
        public void WriteMesh_PressedOverride_UsesOverrideTextureCoordinates()
        {
            _image.overrideSprite = _pressed;

            _image.WriteMesh(_mesh);

            Assert.AreEqual(new Vector2(0.25f, 0.25f), _mesh.uv[0]);
            Assert.AreEqual(new Vector2(0.75f, 0.75f), _mesh.uv[_mesh.vertexCount - 1]);
        }

        [Test]
        public void IsRaycastLocationValid_PointInsideVisibleSurface_ReturnsTrue()
        {
            _image.Configure(CreateSurface(), CreateSurface());

            Assert.IsTrue(_image.IsRaycastLocationValid(new Vector2(50, 40), null));
        }

        [Test]
        public void IsRaycastLocationValid_PointOutsideSurfaceInsideRectangle_ReturnsFalse()
        {
            _image.Configure(CreateSurface(), CreateSurface());

            Assert.IsFalse(_image.IsRaycastLocationValid(new Vector2(95, 75), null));
        }

        [Test]
        public void IsRaycastLocationValid_RotatedControl_UsesTransformedSurface()
        {
            _image.Configure(CreateSurface(), CreateSurface());
            _image.rectTransform.position = new Vector3(300, 200);
            _image.rectTransform.localRotation = Quaternion.Euler(0, 0, 35);
            Vector2 screen = _image.rectTransform.TransformPoint(new Vector3(50, 40));

            Assert.IsTrue(_image.IsRaycastLocationValid(screen, null));
        }

        [Test]
        public void Configure_InvalidTextureCorners_RejectsChange()
        {
            Assert.Throws<ArgumentException>(() => _image.Configure(default, UIQuadrilateral.Unit));
            Assert.AreEqual(UIQuadrilateral.Unit, _image.Corners);
        }

        [Test]
        public void Configure_InvalidInteractiveSurface_RejectsChange()
        {
            Assert.Throws<ArgumentException>(() => _image.Configure(UIQuadrilateral.Unit, default));
            Assert.AreEqual(UIQuadrilateral.Unit, _image.HitRegion);
        }

        [Test]
        public void AuthoredPrefab_MultiplayerButton_UsesProjectedImageAsTarget()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/MainMenu/MainMenuRoot.prefab"
            );
            PerspectiveImage image = prefab.GetComponentInChildren<PerspectiveImage>(true);

            Assert.IsNotNull(image);
            Assert.AreSame(image, image.GetComponent<Button>().targetGraphic);
        }

        /// <summary>
        /// Creates a convex surface whose bounds include points outside its visible area.
        /// </summary>
        /// <returns>The test surface.</returns>
        private static UIQuadrilateral CreateSurface()
        {
            return new UIQuadrilateral(
                new Vector2(0.15f, 0),
                Vector2.up,
                new Vector2(0.7f, 0.8f),
                Vector2.right
            );
        }
    }
}
