using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class InputManagerBindingTests
    {
        /// <summary>
        /// Verifies that overrides attached to authored binding IDs survive a manager restart.
        /// </summary>
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

        /// <summary>
        /// Finds a top-level authored binding by name.
        /// </summary>
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
    }
}
