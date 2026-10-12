using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalMenuControllerTests
    {
        private GameObject _instance;
        private AppBootstrap _bootstrap;
        private TacticalMenuController _menu;

        /// <summary>Creates the existing menu services in an isolated tactical presentation.</summary>
        /// <returns>The play-mode transition.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _bootstrap = AppBootstrap.EnsureExists();
            _instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab"
                )
            );
            _instance.GetComponent<TacticalViewController>().enabled = false;
            _menu = _instance.GetComponent<TacticalMenuController>();
        }

        /// <summary>Closes the menu before releasing its application-owned services.</summary>
        /// <returns>The edit-mode transition.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.DestroyImmediate(_instance);
            if (_bootstrap)
                Object.DestroyImmediate(_bootstrap.gameObject);
            yield return new ExitPlayMode();
        }

        /// <summary>Checks cancellation is unhandled before the scene services are connected.</summary>
        /// <returns>The completed interaction.</returns>
        [UnityTest]
        public IEnumerator TryCancel_UninitializedMenu_DoesNotHandleCancellation()
        {
            Assert.IsFalse(_menu.TryCancel());
            yield return null;
        }

        /// <summary>Checks Escape opens the shared menu and closing restores the prior input state.</summary>
        /// <returns>The completed open-and-close interaction.</returns>
        [UnityTest]
        public IEnumerator TryCancel_InitializedMenu_OpensAndRestoresInput()
        {
            _menu.Initialize(_bootstrap);
            TacticalSelectionController selection =
                _instance.GetComponent<TacticalSelectionController>();
            TacticalViewCameraController camera =
                _instance.GetComponentInChildren<TacticalViewCameraController>();
            Assert.IsTrue(_bootstrap.GetCancelStack().TryCancel());

            Assert.IsTrue(_instance.transform.Find("OptionsOverlayCanvas").gameObject.activeSelf);
            Assert.IsNotNull(_instance.GetComponentInChildren<OptionsMenuView>());
            Assert.IsFalse(selection.enabled);
            Assert.IsFalse(camera.enabled);

            Assert.IsTrue(_bootstrap.GetCancelStack().TryCancel());

            Assert.IsFalse(_instance.transform.Find("OptionsOverlayCanvas").gameObject.activeSelf);
            Assert.IsTrue(selection.enabled);
            Assert.IsTrue(camera.enabled);
            yield return null;
        }

        /// <summary>Checks closing a menu does not enable previously disabled camera controls.</summary>
        /// <returns>The completed open-and-close interaction.</returns>
        [UnityTest]
        public IEnumerator TryCancel_PreviouslyDisabledCamera_KeepsCameraDisabled()
        {
            _menu.Initialize(_bootstrap);
            TacticalViewCameraController camera =
                _instance.GetComponentInChildren<TacticalViewCameraController>();
            camera.enabled = false;

            _menu.TryCancel();
            _menu.TryCancel();

            Assert.IsFalse(camera.enabled);
            yield return null;
        }

        /// <summary>Checks disabling the scene menu removes its cancellation handler and modal surface.</summary>
        /// <returns>The completed disable interaction.</returns>
        [UnityTest]
        public IEnumerator TryCancel_DisabledController_RemovesModalAndCancelHandler()
        {
            _menu.Initialize(_bootstrap);
            _menu.TryCancel();

            _menu.enabled = false;

            Assert.IsFalse(_instance.transform.Find("OptionsOverlayCanvas").gameObject.activeSelf);
            Assert.IsTrue(_instance.GetComponent<TacticalSelectionController>().enabled);
            Assert.IsFalse(_bootstrap.GetCancelStack().TryCancel());
            yield return null;
        }
    }
}
