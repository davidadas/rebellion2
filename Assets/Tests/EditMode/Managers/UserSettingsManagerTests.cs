using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class UserSettingsManagerTests
    {
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
                UserSettingsManager firstSettings = new UserSettingsManager(null, firstInput, path);
                firstSettings.Load();
                firstInput.ApplyBindingSlotOverride("Strategy/ShowTroopers", 0, "<Keyboard>/n");
                firstSettings.Save();

                UnityEngine.Object.DestroyImmediate(firstRoot);
                firstRoot = null;
                secondRoot = new GameObject("SecondInputManager");
                InputManager secondInput = secondRoot.AddComponent<InputManager>();
                UserSettingsManager secondSettings = new UserSettingsManager(
                    null,
                    secondInput,
                    path
                );
                secondSettings.Load();

                Assert.AreEqual(
                    "<Keyboard>/n",
                    secondInput.GetEffectiveBindingSlotPath("Strategy/ShowTroopers", 0)
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
    }
}
