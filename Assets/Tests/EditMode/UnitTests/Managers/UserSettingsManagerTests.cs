using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class UserSettingsManagerTests
    {
        /// <summary>
        /// Verifies settings persistence restores Unity binding overrides by authored binding ID.
        /// </summary>
        [Test]
        public void SaveThenLoad_RestoresRuntimeBindingOverridesFromDisk()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                $"rebellion2-settings-{Guid.NewGuid():N}"
            );
            string path = Path.Combine(directory, "user-settings.json");
            GameObject firstRoot = new GameObject("FirstInputManager");
            GameObject secondRoot = null;
            try
            {
                InputManager firstInput = firstRoot.AddComponent<InputManager>();
                DisplayManager display = CreateDisplayManager();
                UserSettingsManager firstSettings = new UserSettingsManager(
                    null,
                    display,
                    firstInput,
                    path
                );
                firstSettings.Load();
                UnityEngine.InputSystem.InputAction firstAction = firstInput.Asset.FindAction(
                    "Strategy/ShowTroopers",
                    true
                );
                int firstPrimary = FindBinding(firstAction, "Primary");
                firstAction.ApplyBindingOverride(firstPrimary, "<Keyboard>/n");
                firstSettings.Save();

                UnityEngine.Object.DestroyImmediate(firstRoot);
                firstRoot = null;
                secondRoot = new GameObject("SecondInputManager");
                InputManager secondInput = secondRoot.AddComponent<InputManager>();
                UserSettingsManager secondSettings = new UserSettingsManager(
                    null,
                    display,
                    secondInput,
                    path
                );
                secondSettings.Load();

                Assert.AreEqual(
                    "<Keyboard>/n",
                    secondInput
                        .Asset.FindAction("Strategy/ShowTroopers", true)
                        .bindings[
                            FindBinding(
                                secondInput.Asset.FindAction("Strategy/ShowTroopers", true),
                                "Primary"
                            )
                        ]
                        .effectivePath
                );
            }
            finally
            {
                if (firstRoot != null)
                    UnityEngine.Object.DestroyImmediate(firstRoot);
                if (secondRoot != null)
                    UnityEngine.Object.DestroyImmediate(secondRoot);
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SaveThenLoad_RestoresMissionOddsVisibilityFromDisk()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                $"rebellion2-settings-{Guid.NewGuid():N}"
            );
            string path = Path.Combine(directory, "user-settings.json");
            try
            {
                DisplayManager display = CreateDisplayManager();
                UserSettingsManager firstSettings = new UserSettingsManager(
                    null,
                    display,
                    null,
                    path
                );
                firstSettings.Load();
                firstSettings.Settings.Gameplay.ShowMissionOdds = false;
                firstSettings.Save();

                UserSettingsManager secondSettings = new UserSettingsManager(
                    null,
                    display,
                    null,
                    path
                );
                secondSettings.Load();

                Assert.IsFalse(secondSettings.Settings.Gameplay.ShowMissionOdds);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        /// <summary>
        /// Creates a deterministic display manager that does not mutate the test runner display.
        /// </summary>
        private static DisplayManager CreateDisplayManager()
        {
            return new DisplayManager(
                () => new[] { new Vector2Int(1920, 1080) },
                () => new Vector2Int(1920, 1080),
                (_, _, _) => { }
            );
        }

        /// <summary>
        /// Finds a top-level authored binding by name.
        /// </summary>
        private static int FindBinding(UnityEngine.InputSystem.InputAction action, string name)
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
