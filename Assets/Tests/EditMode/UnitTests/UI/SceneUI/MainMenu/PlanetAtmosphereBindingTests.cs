using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UI.SceneUI.MainMenu
{
    /// <summary>
    /// Verifies runtime material creation for the main-menu planet atmosphere.
    /// </summary>
    public sealed class PlanetAtmosphereBindingTests
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
        public void InitializeContent_ConfiguredAtmosphere_AssignsScatteringMaterialParameters()
        {
            GameObject atmosphere = CreateGameObject("Atmosphere");
            MeshRenderer renderer = atmosphere.AddComponent<MeshRenderer>();
            PlanetAtmosphereBinding binding = atmosphere.AddComponent<PlanetAtmosphereBinding>();
            Vector3 sunDirection = new Vector3(2f, 3f, -1f);
            binding.Configure("Custom/PlanetAtmosphere", 1f, 1.04f, sunDirection);

            binding.InitializeContent(new EmptyContentAssetSource());

            Material material = renderer.sharedMaterial;
            Assert.IsNotNull(material);
            Assert.AreEqual("Custom/PlanetAtmosphere", material.shader.name);
            Assert.AreEqual(1f / 1.04f, material.GetFloat("_PlanetRadiusRatio"), 0.0001f);
            Assert.AreEqual((Vector4)sunDirection.normalized, material.GetVector("_SunDirection"));
        }

        [Test]
        public void Configure_AtmosphereInsideSurface_ThrowsArgumentOutOfRangeException()
        {
            GameObject atmosphere = CreateGameObject("Atmosphere");
            atmosphere.AddComponent<MeshRenderer>();
            PlanetAtmosphereBinding binding = atmosphere.AddComponent<PlanetAtmosphereBinding>();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                binding.Configure("Custom/PlanetAtmosphere", 1f, 1f, Vector3.right)
            );
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
        /// Supplies no assets because the atmosphere material uses only authored numeric data.
        /// </summary>
        private sealed class EmptyContentAssetSource : IContentAssetSource
        {
            /// <summary>
            /// Returns no texture.
            /// </summary>
            /// <param name="address">The requested address.</param>
            /// <returns>Always null.</returns>
            public Texture2D GetTexture(string address)
            {
                return null;
            }

            /// <summary>
            /// Returns no sprite.
            /// </summary>
            /// <param name="address">The requested address.</param>
            /// <returns>Always null.</returns>
            public Sprite GetSprite(string address)
            {
                return null;
            }

            /// <summary>
            /// Returns no bordered sprite.
            /// </summary>
            /// <param name="address">The requested address.</param>
            /// <param name="border">The requested border.</param>
            /// <returns>Always null.</returns>
            public Sprite GetSprite(string address, Vector4 border)
            {
                return null;
            }
        }
    }
}
