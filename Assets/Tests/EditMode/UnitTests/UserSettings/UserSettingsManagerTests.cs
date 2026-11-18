using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UserSettingsModel = global::UserSettings;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class UserSettingsManagerTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Save_UnrecoverableRebind_PreservesSavedBindings(bool reapply)
        {
            InputTestFixture fixture = new();
            fixture.Setup();
            GameObject root = new("UnrecoverableInputManager");
            InputManager input = root.AddComponent<InputManager>();
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            string directory = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-input-recovery-" + Guid.NewGuid().ToString("N")
            );
            string path = Path.Combine(directory, "user-settings.json");
            try
            {
                UserSettingsManager settings = new(null, CreateDisplayManager(), input, path);
                settings.Load();
                InputAction oldAction = input.Asset.FindAction("Strategy/ShowTroopers", true);
                oldAction.ApplyBindingOverride(0, "<Keyboard>/t");
                settings.Save();
                string savedBindings = settings.Settings.Input.BindingOverridesJson;
                InputAction action = input.Asset.FindAction("Strategy/IncreaseGameSpeed", true);
                using OptionsBindingSession session = new(input);
                session.Rebuild();
                int row = session
                    .Rows.Select((binding, index) => (binding, index))
                    .Single(item => item.binding.Action == "Increase Game Speed")
                    .index;
                session.BeginRebind(row, false);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.T));
                InputSystem.Update();
                fixture.currentTime += 0.1;
                InputSystem.Update();
                InputSystem.Update();
                session.Changed += () =>
                {
                    action.ChangeBinding(0).Erase();
                    throw new InvalidOperationException("Injected unrecoverable shortcut edit.");
                };
                LogAssert.Expect(LogType.Error, new Regex("Shortcut rebinding failed:"));
                session.ResolveConflict(true);
                settings.Settings.Gameplay.ShowMissionOdds = false;

                settings.Save();
                if (reapply)
                    settings.Apply();

                UserSettingsModel persisted = JsonUtility.FromJson<UserSettingsModel>(
                    File.ReadAllText(path)
                );
                Assert.AreEqual(savedBindings, persisted.Input.BindingOverridesJson);
                Assert.IsFalse(persisted.Gameplay.ShowMissionOdds);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                fixture.TearDown();
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Save_LegacyTemporaryFileIsExclusivelyOwned_PublishesSettingsWithoutTouchingOtherWriter()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "rebellion2-settings-" + Guid.NewGuid().ToString("N")
            );
            string path = Path.Combine(directory, "user-settings.json");
            string legacyTemporaryPath = path + ".tmp";
            try
            {
                Directory.CreateDirectory(directory);
                byte[] sentinel = Encoding.UTF8.GetBytes("another writer's pending settings");
                using FileStream otherWriter = new FileStream(
                    legacyTemporaryPath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.None
                );
                otherWriter.Write(sentinel, 0, sentinel.Length);
                otherWriter.Flush(true);
                UserSettingsManager manager = new UserSettingsManager(
                    null,
                    CreateDisplayManager(),
                    null,
                    path
                );
                manager.Load();
                manager.Settings.Gameplay.ShowMissionOdds = false;

                manager.Save();

                UserSettingsModel restored = JsonUtility.FromJson<UserSettingsModel>(
                    File.ReadAllText(path)
                );
                Assert.That(restored.Gameplay.ShowMissionOdds, Is.False);
                otherWriter.Position = 0;
                byte[] unchanged = new byte[sentinel.Length];
                Assert.That(
                    otherWriter.Read(unchanged, 0, unchanged.Length),
                    Is.EqualTo(sentinel.Length)
                );
                CollectionAssert.AreEqual(sentinel, unchanged);
                CollectionAssert.AreEqual(
                    new[] { legacyTemporaryPath },
                    Directory.GetFiles(directory, "*.tmp")
                );
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Load_LegacyInterfaceLaunchSpeed_MigratesToGameplaySettings()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                $"rebellion2-settings-{Guid.NewGuid():N}"
            );
            string path = Path.Combine(directory, "user-settings.json");
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, "{\"UserInterface\":{\"GameLaunchSpeed\":5}}\n");
                UserSettingsManager settings = new UserSettingsManager(
                    null,
                    CreateDisplayManager(),
                    null,
                    path
                );

                settings.Load();

                Assert.AreEqual(GameLaunchSpeed.Fast, settings.Settings.Gameplay.GameLaunchSpeed);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SaveThenLoad_Default_RestoresRuntimeBindingOverridesFromDisk()
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
                InputAction firstAction = firstInput.Asset.FindAction(
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
        public void SaveThenLoad_Default_RestoresMissionOddsVisibilityFromDisk()
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

        [Test]
        public void SaveThenLoad_Default_RestoresDisabledModsFromDisk()
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
                firstSettings.Settings.Content.DisabledModIDs = new[] { "dummy-mod" };
                firstSettings.Save();

                UserSettingsManager secondSettings = new UserSettingsManager(
                    null,
                    display,
                    null,
                    path
                );
                secondSettings.Load();

                CollectionAssert.AreEqual(
                    new[] { "dummy-mod" },
                    secondSettings.Settings.Content.DisabledModIDs
                );
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SaveThenLoad_AnalyticsDenied_RestoresConsentFromDisk()
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
                firstSettings.Settings.Analytics.Consent = UserAnalyticsConsent.Denied;
                firstSettings.Save();

                UserSettingsManager secondSettings = new UserSettingsManager(
                    null,
                    display,
                    null,
                    path
                );
                secondSettings.Load();

                Assert.AreEqual(
                    UserAnalyticsConsent.Denied,
                    secondSettings.Settings.Analytics.Consent
                );
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void SaveThenLoad_AgreementAccepted_RestoresAcceptanceWithoutEnablingAnalytics()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                $"rebellion2-agreement-{Guid.NewGuid():N}"
            );
            string path = Path.Combine(directory, "user-settings.json");
            try
            {
                PlayerAgreement agreement = new PlayerAgreement("Agreement", "Privacy");
                UserSettingsManager first = new UserSettingsManager(
                    null,
                    CreateDisplayManager(),
                    null,
                    path
                );
                first.Load();
                first.AcceptAgreement(agreement);

                UserSettingsManager second = new UserSettingsManager(
                    null,
                    CreateDisplayManager(),
                    null,
                    path
                );
                second.Load();

                Assert.IsTrue(agreement.IsAccepted(second.Settings));
                Assert.AreEqual(UserAnalyticsConsent.Undecided, second.Settings.Analytics.Consent);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Save_OtherInstanceAcceptedDocuments_PreservesLatestAcceptance(
            bool previouslyAccepted
        )
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                $"rebellion2-agreement-{Guid.NewGuid():N}"
            );
            string path = Path.Combine(directory, "user-settings.json");
            try
            {
                UserSettingsManager stale = new UserSettingsManager(
                    null,
                    CreateDisplayManager(),
                    null,
                    path
                );
                stale.Load();
                if (previouslyAccepted)
                    stale.AcceptAgreement(new PlayerAgreement("Earlier agreement", "Privacy"));
                UserSettingsManager active = new UserSettingsManager(
                    null,
                    CreateDisplayManager(),
                    null,
                    path
                );
                active.Load();
                PlayerAgreement agreement = new PlayerAgreement("Current agreement", "Privacy");
                active.AcceptAgreement(agreement);

                stale.Settings.Gameplay.ShowMissionOdds = false;
                stale.Save();
                active.Load();

                Assert.IsTrue(agreement.IsAccepted(active.Settings));
                Assert.IsFalse(active.Settings.Gameplay.ShowMissionOdds);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void AcceptAgreement_SaveFails_DoesNotRetainAcceptance()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                $"rebellion2-agreement-{Guid.NewGuid():N}"
            );
            Directory.CreateDirectory(directory);
            try
            {
                UserSettingsManager manager = new UserSettingsManager(
                    null,
                    CreateDisplayManager(),
                    null,
                    directory
                );
                manager.Load();
                PlayerAgreement agreement = new PlayerAgreement("Agreement", "Privacy");

                Assert.Catch<SystemException>(() => manager.AcceptAgreement(agreement));

                Assert.IsFalse(agreement.IsAccepted(manager.Settings));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Apply_AnalyticsGranted_AppliesConsentToRuntime()
        {
            RecordingAnalyticsConsentRuntime analyticsRuntime =
                new RecordingAnalyticsConsentRuntime();
            UserSettingsManager settings = new UserSettingsManager(
                null,
                CreateDisplayManager(),
                null,
                null,
                analyticsRuntime
            );
            settings.Load();
            settings.Settings.Analytics.Consent = UserAnalyticsConsent.Granted;

            settings.Apply();

            Assert.AreEqual(UserAnalyticsConsent.Granted, analyticsRuntime.AppliedConsent);
        }

        /// <summary>
        /// Creates a deterministic display manager that does not mutate the test runner display.
        /// </summary>
        /// <returns>The created display manager.</returns>
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

        private sealed class RecordingAnalyticsConsentRuntime : IAnalyticsConsentRuntime
        {
            internal UserAnalyticsConsent AppliedConsent { get; private set; }

            /// <summary>
            /// Records the applied analytics consent choice.
            /// </summary>
            /// <param name="settings">The analytics settings being applied.</param>
            public void ApplyConsent(UserAnalyticsSettings settings)
            {
                AppliedConsent = settings.Consent;
            }
        }
    }
}
