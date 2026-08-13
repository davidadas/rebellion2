using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class AppBootstrapTests
    {
        private AppBootstrap bootstrap;
        private GameObject gameObject;

        [SetUp]
        public void SetUp()
        {
            DestroyAudioManagers();
            GameLaunchContext.Reset(TestContent.Pack);
            GameLaunchContext.Summary.PlayerFactionID = null;
            GameLaunchContext.Summary.PackID = null;
            GameLaunchContext.Summary.PackVersion = null;
            GameLaunchContext.Summary.ScenarioID = null;

            gameObject = new GameObject("AppBootstrapUnderTest");
            gameObject.SetActive(false);
            bootstrap = gameObject.AddComponent<AppBootstrap>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
            DestroyAudioManagers();
            GameLaunchContext.Reset(TestContent.Pack);
        }

        [Test]
        public void InitializeRuntime_BlankLaunchContext_SetsActiveContentDefaults()
        {
            UIComponentTestHelper.InvokeLifecycle(bootstrap, "InitializeRuntime");

            Assert.AreEqual(
                TestContent.Pack.Scenario.DefaultPlayerFactionID,
                GameLaunchContext.Summary.PlayerFactionID
            );
            Assert.AreEqual(TestContent.Pack.Definition.ID, GameLaunchContext.Summary.PackID);
            Assert.AreEqual(
                TestContent.Pack.Definition.Version,
                GameLaunchContext.Summary.PackVersion
            );
            Assert.AreEqual(TestContent.Pack.Scenario.ID, GameLaunchContext.Summary.ScenarioID);
        }

        /// <summary>
        /// Verifies the synchronized cursor uses Unity's required hardware-cursor import settings.
        /// </summary>
        [Test]
        public void DefaultCursor_SynchronizedResource_UsesCursorImportSettings()
        {
            Texture2D cursor = Resources.Load<Texture2D>("UI/DefaultCursor");
            Assert.IsNotNull(cursor);

            string assetPath = AssetDatabase.GetAssetPath(cursor);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

            Assert.IsNotNull(importer);
            Assert.IsTrue(cursor.isReadable);
            Assert.AreEqual(TextureFormat.RGBA32, cursor.format);
            Assert.AreEqual(1, cursor.mipmapCount);
            Assert.AreEqual(TextureImporterType.Cursor, importer.textureType);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
        }

        private static void DestroyAudioManagers()
        {
            foreach (
                AudioManager manager in Object.FindObjectsByType<AudioManager>(
                    FindObjectsInactive.Include
                )
            )
            {
                Object.DestroyImmediate(manager.gameObject);
            }
        }
    }
}
