using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Rebellion.Tests.UI.Components
{
    [TestFixture]
    public sealed class ModelPreviewAtlasTests
    {
        private GameObject root;
        private ModelPreviewAtlas subject;
        private Camera atlasCamera;
        private Transform rig;
        private Camera sourceCamera;
        private RawImage image;
        private RenderTexture sourceTexture;
        private RenderTexture atlasTexture;

        /// <summary>
        /// Creates an isolated source preview and an atlas with the menu's authored pixel density.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Preview test");
            subject = root.AddComponent<ModelPreviewAtlas>();
            sourceTexture = new RenderTexture(512, 360, 16);
            atlasTexture = new RenderTexture(4096, 2048, 16);
            rig = CreateChild("Source rig", root.transform).transform;
            rig.position = new Vector3(100f, 100f, 1000f);
            sourceCamera = CreateChild("Source camera", rig).AddComponent<Camera>();
            sourceCamera.transform.localPosition = new Vector3(0f, 0f, -4f);
            sourceCamera.orthographic = true;
            sourceCamera.orthographicSize = 1.3f;
            sourceCamera.targetTexture = sourceTexture;
            atlasCamera = CreateChild("Atlas camera", root.transform).AddComponent<Camera>();
            atlasCamera.transform.position = new Vector3(0f, 0f, -4f);
            atlasCamera.orthographic = true;
            atlasCamera.orthographicSize = 5.2f;
            atlasCamera.aspect = 2f;
            atlasCamera.targetTexture = atlasTexture;
            atlasCamera.enabled = false;
            GameObject imageObject = new GameObject(
                "Image",
                typeof(RectTransform),
                typeof(RawImage)
            );
            imageObject.transform.SetParent(root.transform, false);
            image = imageObject.GetComponent<RawImage>();
            image.texture = sourceTexture;
        }

        /// <summary>
        /// Releases all preview objects and render targets created by the test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(sourceTexture);
            Object.DestroyImmediate(atlasTexture);
        }

        [TestCase(512, 512, 1.3f)]
        [TestCase(512, 360, 1.3f)]
        [TestCase(256, 256, 1.3f)]
        [TestCase(256, 256, 0.7f)]
        public void Initialize_DifferentSourceProjections_PreservesPixelFraming(
            int width,
            int height,
            float size
        )
        {
            ResizeSourceTexture(width, height);
            sourceCamera.orthographicSize = size;
            sourceCamera.aspect = (float)width / height;
            Transform sample = CreateChild("Model point", rig).transform;
            sample.localPosition = new Vector3(0.4f, 0.2f, 0.3f);
            Vector3 original = sourceCamera.WorldToViewportPoint(sample.position);
            Vector2Int center = new Vector2Int(1700, 300);
            Vector2 expected = new Vector2(
                center.x + (original.x - 0.5f) * width,
                center.y + (original.y - 0.5f) * height
            );
            Configure(center);

            subject.Initialize();

            Vector3 actual = atlasCamera.WorldToViewportPoint(sample.position);
            Assert.AreEqual(expected.x, actual.x * atlasTexture.width, 0.01f);
            Assert.AreEqual(expected.y, actual.y * atlasTexture.height, 0.01f);
        }

        [Test]
        public void Initialize_SourcePreview_SwitchesRenderingToTheAtlas()
        {
            Configure(new Vector2Int(1700, 300));

            subject.Initialize();

            Assert.IsFalse(sourceCamera.enabled);
            Assert.IsTrue(atlasCamera.enabled);
            Assert.AreSame(atlasTexture, image.texture);
            Assert.AreEqual(
                new Rect(1444f / 4096f, 120f / 2048f, 512f / 4096f, 360f / 2048f),
                image.uvRect
            );
        }

        [Test]
        public void Initialize_DisabledSourcePreview_KeepsAtlasDisabled()
        {
            Configure(new Vector2Int(1700, 300));
            sourceCamera.enabled = false;

            subject.Initialize();

            Assert.IsFalse(sourceCamera.enabled);
            Assert.IsFalse(atlasCamera.enabled);
            Assert.AreSame(sourceTexture, image.texture);
            Assert.AreEqual(Vector3.one, rig.localScale);
        }

        [Test]
        public void Initialize_SourceRenderingResumed_ConvertsThePreviewOnce()
        {
            Configure(new Vector2Int(1700, 300));
            sourceCamera.enabled = false;
            subject.Initialize();
            sourceCamera.enabled = true;

            subject.Initialize();
            Vector3 scale = rig.localScale;
            subject.Initialize();

            Assert.IsFalse(sourceCamera.enabled);
            Assert.IsTrue(atlasCamera.enabled);
            Assert.AreSame(atlasTexture, image.texture);
            Assert.AreEqual(Vector3.one * 0.703125f, scale);
            Assert.AreEqual(scale, rig.localScale);
        }

        [TestCase(-240f, true)]
        [TestCase(240f, true)]
        [TestCase(-120f, false)]
        public void Initialize_SpinningOrPausedPreview_PreservesAnimationState(
            float speed,
            bool enabled
        )
        {
            Transform pivot = CreateChild("Pivot", rig).transform;
            pivot.localRotation = Quaternion.Euler(-18f, 37f, 0f);
            AutoRotate spinner = pivot.gameObject.AddComponent<AutoRotate>();
            spinner.Configure(speed, Vector3.up);
            spinner.enabled = enabled;
            Quaternion rotation = pivot.localRotation;
            Configure(new Vector2Int(1700, 300));

            subject.Initialize();

            Assert.AreEqual(rotation, pivot.localRotation);
            Assert.AreEqual(enabled, spinner.enabled);
            Assert.AreEqual(speed, spinner.DegreesPerSecond);
            Assert.AreEqual(Vector3.up, spinner.Axis);
        }

        [Test]
        public void Initialize_LocalLight_PreservesLightingRelativeToTheScaledModel()
        {
            ResizeSourceTexture(256, 256);
            Light light = CreateChild("Local light", rig).AddComponent<Light>();
            light.type = LightType.Point;
            light.lightUnit = LightUnit.Candela;
            light.intensity = 8f;
            light.range = 6f;
            light.shapeRadius = 0.4f;
            light.transform.localPosition = new Vector3(0f, -2f, -1f);
            Configure(new Vector2Int(300, 1500), light);

            subject.Initialize();

            Assert.AreEqual(2f, light.intensity, 0.0001f);
            Assert.AreEqual(3f, light.range, 0.0001f);
            Assert.AreEqual(0.2f, light.shapeRadius, 0.0001f);
            Assert.AreEqual(new Vector3(0f, -1f, -0.5f), light.transform.position - rig.position);
        }

        [Test]
        public void Initialize_RepeatedCall_DoesNotScaleTheRigAgain()
        {
            Configure(new Vector2Int(1700, 300));
            subject.Initialize();
            Vector3 scale = rig.localScale;
            Vector3 position = rig.position;

            subject.Initialize();

            Assert.AreEqual(scale, rig.localScale);
            Assert.AreEqual(position, rig.position);
        }

        [Test]
        public void Initialize_DecorativeImage_PreservesLayoutColorAndHitTesting()
        {
            image.raycastTarget = false;
            image.color = new Color(0.8f, 0.9f, 1f, 0.7f);
            image.rectTransform.offsetMin = new Vector2(12.39164f, 12.1074f);
            image.rectTransform.offsetMax = new Vector2(-10.42626f, 2.519201f);
            Vector2 min = image.rectTransform.offsetMin;
            Vector2 max = image.rectTransform.offsetMax;
            Color color = image.color;
            Configure(new Vector2Int(1300, 1500));

            subject.Initialize();

            Assert.IsFalse(image.raycastTarget);
            Assert.AreEqual(color, image.color);
            Assert.AreEqual(min, image.rectTransform.offsetMin);
            Assert.AreEqual(max, image.rectTransform.offsetMax);
        }

        /// <summary>
        /// Resizes the source target while it is detached from its camera.
        /// </summary>
        /// <param name="width">The source image width.</param>
        /// <param name="height">The source image height.</param>
        private void ResizeSourceTexture(int width, int height)
        {
            sourceCamera.targetTexture = null;
            sourceTexture.Release();
            sourceTexture.width = width;
            sourceTexture.height = height;
            sourceCamera.targetTexture = sourceTexture;
        }

        /// <summary>
        /// Configures the subject with the source rig and the supplied local lights.
        /// </summary>
        /// <param name="center">The atlas pixel center of the preview.</param>
        /// <param name="lights">The local lights belonging to the preview.</param>
        private void Configure(Vector2Int center, params Light[] lights)
        {
            subject.Configure(
                atlasCamera,
                new[]
                {
                    new ModelPreviewAtlas.Preview(
                        rig,
                        sourceCamera,
                        center,
                        new[] { image },
                        lights
                    ),
                }
            );
        }

        /// <summary>
        /// Creates a child with identity local transforms.
        /// </summary>
        /// <param name="name">The child name.</param>
        /// <param name="parent">The owning transform.</param>
        /// <returns>The newly created child.</returns>
        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }
    }
}
