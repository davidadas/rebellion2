using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.Input.Integration
{
    [TestFixture]
    [Category("Integration")]
    public sealed class AppInputControllerTests
    {
        private GameObject root;

        /// <summary>
        /// Enters play mode in an empty scene to exercise Unity lifecycle callbacks.
        /// </summary>
        /// <returns>The runtime setup sequence.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
        }

        /// <summary>
        /// Releases input components before leaving play mode.
        /// </summary>
        /// <returns>The runtime cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            yield return null;
            yield return new ExitPlayMode();
        }

        [Test]
        public void EnabledState_InitializedController_DisablesAndRestoresGlobalActions()
        {
            root = new GameObject("AppInputControllerUnderTest");
            InputManager inputManager = root.AddComponent<InputManager>();
            AppInputController controller = root.AddComponent<AppInputController>();
            controller.Initialize(inputManager, new CancelStack(), null);
            InputActionMap globalActions = inputManager.Asset.FindActionMap("Global", true);
            InputActionMap strategyActions = inputManager.Asset.FindActionMap("Strategy", true);
            strategyActions.Enable();
            Assert.IsTrue(globalActions.enabled);

            controller.enabled = false;
            Assert.IsFalse(globalActions.enabled);
            Assert.IsTrue(strategyActions.enabled);

            controller.enabled = true;
            Assert.IsTrue(globalActions.enabled);

            root.SetActive(false);
            Assert.IsFalse(globalActions.enabled);
            Assert.IsFalse(strategyActions.enabled);

            root.SetActive(true);
            Assert.IsTrue(globalActions.enabled);
        }
    }
}
