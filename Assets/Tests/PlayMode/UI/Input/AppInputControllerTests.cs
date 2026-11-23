using System.Collections;
using NUnit.Framework;
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
        /// Releases input components after the runtime lifecycle assertions.
        /// </summary>
        /// <returns>The runtime cleanup sequence.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null)
                Object.Destroy(root);

            yield return null;
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
