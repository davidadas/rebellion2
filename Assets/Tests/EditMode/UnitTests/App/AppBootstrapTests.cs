using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.App
{
    [TestFixture]
    public sealed class AppBootstrapTests
    {
        private AppBootstrap _bootstrap;
        private GameObject _gameObject;

        /// <summary>
        /// Sets up.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DestroyAudioManagers();
            GameLaunchContext.Reset(TestContent.Pack);
            GameLaunchContext.Summary.PlayerFactionID = null;
            GameLaunchContext.Summary.PackID = null;
            GameLaunchContext.Summary.PackVersion = null;
            GameLaunchContext.Summary.ScenarioID = null;

            _gameObject = new GameObject("AppBootstrapUnderTest");
            _gameObject.SetActive(false);
            _bootstrap = _gameObject.AddComponent<AppBootstrap>();
        }

        /// <summary>
        /// Executes tear down.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_gameObject);
            DestroyAudioManagers();
            GameLaunchContext.Reset(TestContent.Pack);
        }

        /// <summary>
        /// Verifies initialize runtime core blank launch context sets active content defaults.
        /// </summary>
        [Test]
        public void InitializeRuntimeCore_BlankLaunchContext_SetsActiveContentDefaults()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");

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
        /// Verifies destruction during main-menu preload completes without retaining the bootstrap.
        /// </summary>
        /// <returns>A task that completes after the pending preload continuation.</returns>
        [Test]
        public async Task InitializeMainMenuContentAsync_DestroyedDuringPreload_CompletesSafelyAsync()
        {
            UIComponentTestHelper.InvokeLifecycle(_bootstrap, "InitializeRuntimeCore");
            Task preload = _bootstrap.InitializeMainMenuContentAsync();

            Object.DestroyImmediate(_gameObject);
            _gameObject = null;

            await preload;

            Assert.IsNull(AppBootstrap.Instance);
        }

        /// <summary>
        /// Removes persistent audio managers created by bootstrap initialization.
        /// </summary>
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
