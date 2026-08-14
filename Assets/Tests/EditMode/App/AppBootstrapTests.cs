using NUnit.Framework;
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
        public void InitializeRuntimeCore_BlankLaunchContext_SetsActiveContentDefaults()
        {
            UIComponentTestHelper.InvokeLifecycle(bootstrap, "InitializeRuntimeCore");

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
        /// Verifies the external cursor loads with Unity's runtime cursor requirements.
        /// </summary>
        [Test]
        public void DefaultCursor_ExternalContent_LoadsReadableTexture()
        {
            using ContentAssets assets = new ContentAssets(
                TestContent.Pack.ContentRootPath,
                TestContent.Pack.PackRootPath
            );

            Texture2D cursor = assets.GetCursor("Application/Common/UI/ui_common_cursor_default");

            Assert.IsNotNull(cursor);
            Assert.IsTrue(cursor.isReadable);
            Assert.AreEqual(TextureFormat.RGBA32, cursor.format);
            Assert.AreEqual(1, cursor.mipmapCount);
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
