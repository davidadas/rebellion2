using System;
using System.Collections;
using System.IO;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.SceneUI.LoadingScreen
{
    [TestFixture]
    public sealed class LoadingScreenControllerTests
    {
        private string _packDirectory;
        private string _settingsPath;
        private byte[] _settingsBytes;

        /// <summary>
        /// Enters runtime execution with graphics available for the loading presentation.
        /// </summary>
        /// <returns>The runtime setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore(
                    "The runtime content-reload presentation requires a graphics device."
                );
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            AppBootstrap bootstrap = AppBootstrap.EnsureExists();
            _settingsPath = bootstrap.GetUserSettingsManager().GetSettingsFilePath();
            _settingsBytes = File.Exists(_settingsPath) ? File.ReadAllBytes(_settingsPath) : null;
        }

        /// <summary>
        /// Restores settings and removes the test pack before leaving runtime execution.
        /// </summary>
        /// <returns>The runtime cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (AppBootstrap.Instance != null)
            {
                UnityEngine.Object.Destroy(AppBootstrap.Instance.gameObject);
                yield return null;
            }
            if (_settingsPath != null)
            {
                if (_settingsBytes == null)
                    File.Delete(_settingsPath);
                else
                    File.WriteAllBytes(_settingsPath, _settingsBytes);
            }
            if (_packDirectory != null && Directory.Exists(_packDirectory))
                Directory.Delete(_packDirectory, true);
            _packDirectory = null;
            _settingsPath = null;
            _settingsBytes = null;
            if (Application.isPlaying)
                yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator ReloadContent_SelectedPack_RebuildsMainMenuWithoutRestart()
        {
            AppBootstrap outgoing = AppBootstrap.Instance;
            ContentPack active = outgoing.GetContentPack();
            string packID = CreateDerivedPack(active);
            ContentAssets outgoingAssets = outgoing.GetContentAssets();
            int processID = System.Diagnostics.Process.GetCurrentProcess().Id;

            Assert.IsTrue(outgoing.ReloadContent(packID, Array.Empty<string>()));
            float deadline = Time.realtimeSinceStartup + 120f;
            while (
                LoadingScreenController.IsLoadInProgress
                || AppBootstrap.Instance == outgoing
                || SceneManager.GetActiveScene().name != "MainMenu"
            )
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "Content reload did not finish.");
                yield return null;
            }

            AppBootstrap replacement = AppBootstrap.Instance;
            Assert.AreEqual(packID, replacement.GetContentPack().Definition.ID);
            Assert.AreEqual(packID, GameLaunchContext.Summary.PackID);
            Assert.AreNotSame(outgoingAssets, replacement.GetContentAssets());
            Assert.AreEqual(processID, System.Diagnostics.Process.GetCurrentProcess().Id);
            Assert.IsFalse(GameLaunchContext.IsLoadGame);
            Assert.IsNotNull(UnityEngine.Object.FindAnyObjectByType<MainMenuController>());
        }

        /// <summary>
        /// Installs a distinct pack definition that inherits the current content files.
        /// </summary>
        /// <param name="active">The installed base pack.</param>
        /// <returns>The derived pack's identifier.</returns>
        private string CreateDerivedPack(ContentPack active)
        {
            string packID = $"reload-test-{Guid.NewGuid():N}";
            _packDirectory = Path.Combine(active.ContentRootPath, "Packs", packID);
            Directory.CreateDirectory(_packDirectory);
            XDocument definition = XDocument.Load(Path.Combine(active.PackRootPath, "pack.xml"));
            definition.Root.SetElementValue("ID", packID);
            definition.Root.SetElementValue("DisplayName", "Reload test pack");
            definition.Root.SetElementValue("BasePackID", active.Definition.ID);
            definition.Save(Path.Combine(_packDirectory, "pack.xml"));
            return packID;
        }
    }
}
