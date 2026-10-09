using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.MainMenu
{
    /// <summary>
    /// Verifies runtime material creation for the main-menu planet surface.
    /// </summary>
    public sealed class PlanetSurfaceBindingTests
    {
        private readonly List<UnityEngine.Object> _createdObjects = new List<UnityEngine.Object>();

        /// <summary>
        /// Releases Unity objects created by each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            for (int index = _createdObjects.Count - 1; index >= 0; index--)
            {
                if (_createdObjects[index] != null)
                    UnityEngine.Object.DestroyImmediate(_createdObjects[index]);
            }

            _createdObjects.Clear();
        }

        [Test]
        public void InitializeContent_ConfiguredSurface_AssignsUnifiedRuntimeMaterial()
        {
            Texture2D albedo = CreateTexture("Albedo");
            Texture2D clouds = CreateTexture("Clouds");
            Material importedMaterial = CreateMaterial(albedo);
            GameObject root = CreateGameObject("Planet");
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = importedMaterial;
            PlanetSurfaceBinding binding = root.AddComponent<PlanetSurfaceBinding>();
            binding.Configure("Custom/PlanetSurface", "planet/clouds", 0.75f);
            FakeContentAssetSource contentAssets = new FakeContentAssetSource(
                "planet/clouds",
                clouds
            );

            binding.InitializeContent(contentAssets);

            Material runtimeMaterial = renderer.sharedMaterial;
            Assert.AreEqual("Custom/PlanetSurface", runtimeMaterial.shader.name);
            Assert.AreSame(albedo, runtimeMaterial.GetTexture("_BaseMap"));
            Assert.AreSame(clouds, runtimeMaterial.GetTexture("_CloudMap"));
            Assert.AreEqual(0.75f, runtimeMaterial.GetFloat("_CloudRotationSpeed"));
            Assert.AreEqual(TextureWrapMode.Repeat, clouds.wrapModeU);
            Assert.AreEqual(TextureWrapMode.Clamp, clouds.wrapModeV);
            Assert.AreEqual(FilterMode.Bilinear, clouds.filterMode);
            Assert.AreEqual(8, clouds.anisoLevel);
        }

        [Test]
        public void Configure_MissingShader_ThrowsArgumentException()
        {
            GameObject root = CreateGameObject("Planet");
            PlanetSurfaceBinding binding = root.AddComponent<PlanetSurfaceBinding>();

            Assert.Throws<ArgumentException>(() => binding.Configure("", "planet/clouds", 0.5f));
        }

        /// <summary>
        /// Creates and tracks a one-pixel texture.
        /// </summary>
        /// <param name="name">The texture name.</param>
        /// <returns>The created texture.</returns>
        private Texture2D CreateTexture(string name)
        {
            Texture2D texture = new Texture2D(1, 1) { name = name };
            _createdObjects.Add(texture);
            return texture;
        }

        /// <summary>
        /// Creates and tracks an imported-style material with the supplied base texture.
        /// </summary>
        /// <param name="baseTexture">The imported base texture.</param>
        /// <returns>The created material.</returns>
        private Material CreateMaterial(Texture baseTexture)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.IsNotNull(shader);
            Material material = new Material(shader) { mainTexture = baseTexture };
            _createdObjects.Add(material);
            return material;
        }

        /// <summary>
        /// Creates and tracks a test game object.
        /// </summary>
        /// <param name="name">The object name.</param>
        /// <returns>The created object.</returns>
        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            return gameObject;
        }

        /// <summary>
        /// Provides one texture at one stable content address.
        /// </summary>
        private sealed class FakeContentAssetSource : IContentAssetSource
        {
            private readonly string _address;
            private readonly Texture2D _texture;

            /// <summary>
            /// Creates a source containing the supplied texture.
            /// </summary>
            /// <param name="address">The texture address.</param>
            /// <param name="texture">The texture value.</param>
            public FakeContentAssetSource(string address, Texture2D texture)
            {
                _address = address;
                _texture = texture;
            }

            /// <summary>
            /// Resolves the configured texture.
            /// </summary>
            /// <param name="address">The requested address.</param>
            /// <returns>The texture when the address matches; otherwise null.</returns>
            public Texture2D GetTexture(string address)
            {
                return string.Equals(address, _address, StringComparison.Ordinal) ? _texture : null;
            }

            /// <summary>
            /// Returns no sprite because this source only contains a texture.
            /// </summary>
            /// <param name="address">The requested address.</param>
            /// <returns>Always null.</returns>
            public Sprite GetSprite(string address)
            {
                return null;
            }

            /// <summary>
            /// Returns no bordered sprite because this source only contains a texture.
            /// </summary>
            /// <param name="address">The requested address.</param>
            /// <param name="border">The requested sprite border.</param>
            /// <returns>Always null.</returns>
            public Sprite GetSprite(string address, Vector4 border)
            {
                return null;
            }
        }
    }
}
