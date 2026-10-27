using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Rebellion.Tests.UI.SceneUI.TacticalView
{
    [TestFixture]
    public sealed class TacticalMenuControllerTests
    {
        private GameObject _instance;
        private AppBootstrap _bootstrap;
        private TacticalMenuController _menu;
        private InputTestFixture _input;

        /// <summary>
        /// Creates the existing menu services in an isolated tactical presentation.
        /// </summary>
        /// <returns>The play-mode transition.</returns>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            _input = new InputTestFixture();
            _input.Setup();
            _bootstrap = AppBootstrap.EnsureExists(TestContent.Pack);
            _bootstrap.GetInputManager().Asset.RemoveAllBindingOverrides();
            _instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/UI/TacticalView/TacticalViewRoot.prefab"
                )
            );
            _instance.GetComponent<TacticalViewController>().enabled = false;
            _menu = _instance.GetComponent<TacticalMenuController>();
        }

        /// <summary>
        /// Closes the menu before releasing its application-owned services.
        /// </summary>
        /// <returns>The edit-mode transition.</returns>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.DestroyImmediate(_instance);
            LoadingScreenController loading = Object.FindAnyObjectByType<LoadingScreenController>(
                FindObjectsInactive.Include
            );
            if (loading)
                Object.DestroyImmediate(loading.gameObject);
            if (_bootstrap)
                Object.DestroyImmediate(_bootstrap.gameObject);
            _input.TearDown();
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// Checks cancellation is unhandled before the scene services are connected.
        /// </summary>
        /// <returns>The completed interaction.</returns>
        [UnityTest]
        public IEnumerator TryCancel_UninitializedMenu_DoesNotHandleCancellation()
        {
            Assert.IsFalse(_menu.TryCancel());
            yield return null;
        }

        /// <summary>
        /// Checks plain Escape does not open the shared menu when no units are selected.
        /// </summary>
        /// <returns>The completed cancellation interaction.</returns>
        [UnityTest]
        public IEnumerator TryCancel_InitializedMenuWithoutSelection_DoesNotOpenMenu()
        {
            _menu.Initialize(_bootstrap);

            Assert.IsFalse(_bootstrap.GetCancelStack().TryCancel());

            Assert.IsFalse(_instance.transform.Find("OptionsOverlayCanvas").gameObject.activeSelf);
            Assert.IsNull(_instance.GetComponentInChildren<OptionsMenuView>());
            yield return null;
        }

        /// <summary>
        /// Checks Shift+Escape opens the shared menu and Escape closes it while restoring input.
        /// </summary>
        /// <returns>The completed open-and-close interaction.</returns>
        [UnityTest]
        public IEnumerator OpenGameMenu_ShiftEscape_OpensAndRestoresInput()
        {
            _menu.Initialize(_bootstrap);
            TacticalSelectionController selection =
                _instance.GetComponent<TacticalSelectionController>();
            TacticalViewCameraController camera =
                _instance.GetComponentInChildren<TacticalViewCameraController>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            OpenGameMenu(keyboard);

            Assert.IsTrue(_instance.transform.Find("OptionsOverlayCanvas").gameObject.activeSelf);
            Assert.IsNotNull(_instance.GetComponentInChildren<OptionsMenuView>());
            Assert.IsFalse(selection.enabled);
            Assert.IsFalse(camera.enabled);

            Assert.IsTrue(_bootstrap.GetCancelStack().TryCancel());

            Assert.IsFalse(_instance.transform.Find("OptionsOverlayCanvas").gameObject.activeSelf);
            Assert.IsTrue(selection.enabled);
            Assert.IsTrue(camera.enabled);
            InputSystem.RemoveDevice(keyboard);
            yield return null;
        }

        /// <summary>
        /// Checks closing a menu does not enable previously disabled camera controls.
        /// </summary>
        /// <returns>The completed open-and-close interaction.</returns>
        [UnityTest]
        public IEnumerator TryCancel_PreviouslyDisabledCamera_KeepsCameraDisabled()
        {
            _menu.Initialize(_bootstrap);
            TacticalViewCameraController camera =
                _instance.GetComponentInChildren<TacticalViewCameraController>();
            camera.enabled = false;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();

            OpenGameMenu(keyboard);
            _menu.TryCancel();

            Assert.IsFalse(camera.enabled);
            InputSystem.RemoveDevice(keyboard);
            yield return null;
        }

        /// <summary>
        /// Checks disabling the scene menu removes its cancellation handler and modal surface.
        /// </summary>
        /// <returns>The completed disable interaction.</returns>
        [UnityTest]
        public IEnumerator TryCancel_DisabledController_RemovesModalAndCancelHandler()
        {
            _menu.Initialize(_bootstrap);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            OpenGameMenu(keyboard);

            _menu.enabled = false;

            Assert.IsFalse(_instance.transform.Find("OptionsOverlayCanvas").gameObject.activeSelf);
            Assert.IsTrue(_instance.GetComponent<TacticalSelectionController>().enabled);
            Assert.IsFalse(_bootstrap.GetCancelStack().TryCancel());
            InputSystem.RemoveDevice(keyboard);
            yield return null;
        }

        /// <summary>
        /// Sends the tactical menu shortcut through the active input system.
        /// </summary>
        /// <param name="keyboard">The test keyboard receiving the shortcut.</param>
        private static void OpenGameMenu(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.Escape));
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
        }
    }
}
