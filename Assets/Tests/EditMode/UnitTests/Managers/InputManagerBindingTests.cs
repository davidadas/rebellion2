using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class InputManagerBindingTests
    {
        [Test]
        public void SetShortcutModifier_MacOS_UsesResolvableCommandKey()
        {
            InputTestFixture inputFixture = new InputTestFixture();
            inputFixture.Setup();
            InputActionAsset asset = CreateModifierActions();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();

            try
            {
                InputManager.SetShortcutModifier(asset, true);
                InputAction action = asset.FindAction("Test/Shortcut", true);
                asset.Enable();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftMeta));
                InputSystem.Update();

                AssertModifierPath(action, "<Keyboard>/leftMeta");
                CollectionAssert.Contains(action.controls, keyboard.leftMetaKey);
                Assert.IsTrue(action.IsPressed());
                Assert.IsNotEmpty(action.GetBindingDisplayString(0));
            }
            finally
            {
                Object.DestroyImmediate(asset);
                inputFixture.TearDown();
            }
        }

        [Test]
        public void SetShortcutModifier_Windows_UsesControl()
        {
            InputActionAsset asset = CreateModifierActions();

            InputManager.SetShortcutModifier(asset, false);

            AssertModifierPath(asset.FindAction("Test/Shortcut", true), "<Keyboard>/ctrl");
        }

        [Test]
        public void BindingOverrides_AuthoredSlots_RestoreAcrossManagerRestart()
        {
            GameObject firstRoot = new GameObject("FirstInputManager");
            GameObject secondRoot = null;
            try
            {
                InputManager first = firstRoot.AddComponent<InputManager>();
                InputAction firstAction = first.Asset.FindAction("Strategy/ShowTroopers", true);
                int firstPrimary = FindBinding(firstAction, "Primary");
                firstAction.ApplyBindingOverride(firstPrimary, "<Keyboard>/n");
                string json = first.SaveBindingOverrides();

                Object.DestroyImmediate(firstRoot);
                firstRoot = null;
                secondRoot = new GameObject("SecondInputManager");
                InputManager second = secondRoot.AddComponent<InputManager>();
                second.LoadBindingOverrides(json);

                InputAction secondAction = second.Asset.FindAction("Strategy/ShowTroopers", true);
                Assert.AreEqual(
                    "<Keyboard>/n",
                    secondAction.bindings[FindBinding(secondAction, "Primary")].effectivePath
                );
            }
            finally
            {
                if (firstRoot != null)
                    Object.DestroyImmediate(firstRoot);
                if (secondRoot != null)
                    Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void LoadBindingOverrides_CancelPrimary_RestoresEscapeAndKeepsSecondary()
        {
            GameObject firstRoot = new GameObject("FirstInputManager");
            GameObject secondRoot = null;
            try
            {
                InputManager first = firstRoot.AddComponent<InputManager>();
                InputAction firstAction = first.Asset.FindAction("Global/CancelOrSettings", true);
                firstAction.ApplyBindingOverride(
                    FindBinding(firstAction, "Primary"),
                    "<Keyboard>/n"
                );
                firstAction.ApplyBindingOverride(
                    FindBinding(firstAction, "Secondary"),
                    "<Keyboard>/m"
                );
                string json = first.Asset.SaveBindingOverridesAsJson();

                Object.DestroyImmediate(firstRoot);
                firstRoot = null;
                secondRoot = new GameObject("SecondInputManager");
                InputManager second = secondRoot.AddComponent<InputManager>();
                second.LoadBindingOverrides(json);
                InputAction restored = second.Asset.FindAction("Global/CancelOrSettings", true);

                Assert.AreEqual(
                    "<Keyboard>/escape",
                    restored.bindings[FindBinding(restored, "Primary")].effectivePath
                );
                Assert.AreEqual(
                    "<Keyboard>/m",
                    restored.bindings[FindBinding(restored, "Secondary")].effectivePath
                );
            }
            finally
            {
                if (firstRoot != null)
                    Object.DestroyImmediate(firstRoot);
                if (secondRoot != null)
                    Object.DestroyImmediate(secondRoot);
            }
        }

        [Test]
        public void LoadBindingOverrides_OpenGameMenuPrimary_PreservesShiftEscapeShortcut()
        {
            GameObject root = new GameObject("InputManager");
            try
            {
                InputManager inputManager = root.AddComponent<InputManager>();
                InputAction action = inputManager.Asset.FindAction("Global/OpenGameMenu", true);
                int chord = FindBinding(action, "PrimaryChord");
                action.ApplyBindingOverride(chord + 1, "<Keyboard>/ctrl");
                action.ApplyBindingOverride(chord + 2, "<Keyboard>/m");
                string json = inputManager.Asset.SaveBindingOverridesAsJson();

                inputManager.LoadBindingOverrides(json);

                Assert.AreEqual("<Keyboard>/shift", action.bindings[chord + 1].effectivePath);
                Assert.AreEqual("<Keyboard>/escape", action.bindings[chord + 2].effectivePath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Finds a top-level authored binding by name.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <param name="name">The name.</param>
        /// <returns>The matching binding.</returns>
        private static int FindBinding(InputAction action, string name)
        {
            for (int index = 0; index < action.bindings.Count; index++)
            {
                if (
                    !action.bindings[index].isPartOfComposite
                    && action.bindings[index].name == name
                )
                    return index;
            }
            Assert.Fail($"Binding '{name}' was not found on {action}.");
            return -1;
        }

        /// <summary>
        /// Creates an action containing the authored cross-platform modifier alternatives.
        /// </summary>
        /// <returns>The created input action asset.</returns>
        private static InputActionAsset CreateModifierActions()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputAction action = asset.AddActionMap("Test").AddAction("Shortcut");
            action.AddBinding("<Keyboard>/ctrl");
            action.AddBinding("<Keyboard>/leftMeta");
            return asset;
        }

        /// <summary>
        /// Verifies that an action uses only the expected platform modifier default.
        /// </summary>
        /// <param name="action">The action whose bindings are inspected.</param>
        /// <param name="expectedPath">The expected active modifier path.</param>
        private static void AssertModifierPath(InputAction action, string expectedPath)
        {
            Assert.AreEqual(expectedPath, action.bindings[0].path);
            Assert.AreEqual(string.Empty, action.bindings[1].path);
        }
    }
}
