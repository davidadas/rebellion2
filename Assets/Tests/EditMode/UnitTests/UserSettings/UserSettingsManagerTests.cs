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
        [TestCase("{broken")]
        [TestCase("")]
        public void Save_InvalidSettings_BacksUpOriginalAndWritesCurrentValues(string invalid)
        {
            WithSettingsFile(path =>
            {
                File.WriteAllText(path, invalid);
                var manager = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                manager.Load();
                manager.Settings.Gameplay.ShowMissionOdds = false;

                manager.Save();

                string backup = Directory
                    .GetFiles(Path.GetDirectoryName(path), "user-settings.json.corrupt-*")
                    .Single();
                Assert.That(File.ReadAllText(backup), Is.EqualTo(invalid));
                var reopened = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                Assert.That(reopened.Load().Gameplay.ShowMissionOdds, Is.False);
            });
        }

        [Test]
        public void AcceptAgreement_InvalidSettings_BacksUpOriginalAndRecordsAcceptance()
        {
            WithSettingsFile(path =>
            {
                const string invalid = "{broken";
                File.WriteAllText(path, invalid);
                var manager = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                manager.Load();
                PlayerAgreement agreement = new("Agreement", "Privacy");

                manager.AcceptAgreement(agreement);

                string backup = Directory
                    .GetFiles(Path.GetDirectoryName(path), "user-settings.json.corrupt-*")
                    .Single();
                Assert.That(File.ReadAllText(backup), Is.EqualTo(invalid));
                var reopened = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                Assert.That(agreement.IsAccepted(reopened.Load()), Is.True);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Save_SettingsUnavailableAfterLoading_PreservesLoadedContentAndAgreement(
            bool deleted
        )
        {
            WithSettingsFile(path =>
            {
                var manager = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                manager.Load();
                manager.Settings.Content.ActivePackID = "LoadedPack";
                manager.Settings.Content.ActiveScenarioID = "LoadedScenario";
                manager.Settings.Content.DisabledModIDs = new[] { "Addon" };
                PlayerAgreement agreement = new("Agreement", "Privacy");
                manager.AcceptAgreement(agreement);
                const string invalid = "{broken";
                if (deleted)
                    File.Delete(path);
                else
                    File.WriteAllText(path, invalid);

                manager.Save();

                var reopened = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                UserSettingsModel saved = reopened.Load();
                Assert.That(saved.Content.ActivePackID, Is.EqualTo("LoadedPack"));
                Assert.That(saved.Content.ActiveScenarioID, Is.EqualTo("LoadedScenario"));
                Assert.That(saved.Content.DisabledModIDs, Is.EqualTo(new[] { "Addon" }));
                Assert.That(agreement.IsAccepted(saved), Is.True);
                string[] backups = Directory.GetFiles(
                    Path.GetDirectoryName(path),
                    "user-settings.json.corrupt-*"
                );
                Assert.That(backups.Length, Is.EqualTo(deleted ? 0 : 1));
                if (!deleted)
                    Assert.That(File.ReadAllText(backups.Single()), Is.EqualTo(invalid));
            });
        }

        [Test]
        public void Save_SettingsRepairedAfterFallback_PreservesNewerContentAndAgreement()
        {
            WithSettingsFile(path =>
            {
                File.WriteAllText(path, "{broken");
                var manager = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                manager.Load();
                UserSettingsModel latest = new()
                {
                    AcceptedAgreementVersion = "New agreement",
                    Content = new UserContentSettings
                    {
                        ActivePackID = "NewPack",
                        ActiveScenarioID = "NewScenario",
                        DisabledModIDs = new[] { "Addon" },
                    },
                };
                File.WriteAllText(path, JsonUtility.ToJson(latest));
                manager.Settings.Gameplay.ShowMissionOdds = false;

                manager.Save();

                var reopened = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                UserSettingsModel saved = reopened.Load();
                Assert.That(saved.AcceptedAgreementVersion, Is.EqualTo("New agreement"));
                Assert.That(saved.Content.ActivePackID, Is.EqualTo("NewPack"));
                Assert.That(saved.Content.ActiveScenarioID, Is.EqualTo("NewScenario"));
                Assert.That(saved.Content.DisabledModIDs, Is.EqualTo(new[] { "Addon" }));
                Assert.That(saved.Gameplay.ShowMissionOdds, Is.False);
                Assert.That(
                    Directory.GetFiles(Path.GetDirectoryName(path), "user-settings.json.corrupt-*"),
                    Is.Empty
                );
            });
        }

        [Test]
        public void Save_AnotherInstanceChangedContent_PreservesPackAndAddOnsAcrossRepeatedSaves()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "shared-content-settings-" + Guid.NewGuid().ToString("N")
            );
            string path = Path.Combine(directory, "user-settings.json");
            try
            {
                var game = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                game.Load();
                game.Settings.Content.ActivePackID = "Original";
                game.Settings.Content.ActiveScenarioID = "OriginalScenario";
                game.Settings.Content.DisabledModIDs = new[] { "EnableMe", "KeepDisabled" };
                game.Save();
                var toolkit = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                toolkit.Load();
                toolkit.Settings.Content.ActivePackID = "NewPack";
                toolkit.Settings.Content.ActiveScenarioID = "NewScenario";
                toolkit.Settings.Content.DisabledModIDs = new[] { "KeepDisabled", "NewDisabled" };
                toolkit.Save();

                game.Settings.Gameplay.ShowMissionOdds = false;
                game.Save();
                game.Settings.Gameplay.ShowMissionOdds = true;
                game.Save();
                var restarted = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                restarted.Load();

                Assert.That(restarted.Settings.Content.ActivePackID, Is.EqualTo("NewPack"));
                Assert.That(restarted.Settings.Content.ActiveScenarioID, Is.EqualTo("NewScenario"));
                CollectionAssert.AreEquivalent(
                    new[] { "KeepDisabled", "NewDisabled" },
                    restarted.Settings.Content.DisabledModIDs
                );
                Assert.That(restarted.Settings.Gameplay.ShowMissionOdds, Is.True);
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Save_BothInstancesChangeDifferentEraOrders_MergesTheOrdersAndVersions()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "era-orders-" + Guid.NewGuid().ToString("N")
            );
            string path = Path.Combine(directory, "user-settings.json");
            try
            {
                var game = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                var toolkit = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                game.Load();
                toolkit.Load();
                game.Settings.Content.SetModLoadOrder(
                    "classic",
                    new[] { "balance", "art" },
                    new[] { "1.1", "2.0" }
                );
                toolkit.Settings.Content.SetModLoadOrder(
                    "frontier",
                    new[] { "ships" },
                    new[] { "3.0" }
                );
                toolkit.Save();

                game.Save();

                toolkit.Load();
                Assert.That(
                    toolkit.Settings.Content.GetModVersions("classic"),
                    Is.EqualTo(new[] { "1.1", "2.0" })
                );
                Assert.That(
                    toolkit.Settings.Content.GetModVersions("frontier"),
                    Is.EqualTo(new[] { "3.0" })
                );
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Save_BothInstancesChangeDifferentAddOns_MergesTheirChanges()
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "shared-content-settings-" + Guid.NewGuid().ToString("N")
            );
            string path = Path.Combine(directory, "user-settings.json");
            try
            {
                var first = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                first.Load();
                first.Settings.Content.ActivePackID = "Pack";
                first.Settings.Content.DisabledModIDs = new[] { "EnableMe" };
                first.Save();
                var second = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                second.Load();
                second.Settings.Content.DisabledModIDs = new[] { "EnableMe", "DisableMe" };
                second.Save();

                first.Settings.Content.DisabledModIDs = Array.Empty<string>();
                first.Save();

                var restarted = new UserSettingsManager(null, CreateDisplayManager(), null, path);
                restarted.Load();
                CollectionAssert.AreEqual(
                    new[] { "DisableMe" },
                    restarted.Settings.Content.DisabledModIDs
                );
            }
            finally
            {
                if (Directory.Exists(directory))
                    Directory.Delete(directory, true);
            }
        }

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
        /// Runs a persistence check with a settings file and backups owned by this fixture.
        /// </summary>
        /// <param name="scenario">The operation using the isolated settings path.</param>
        private static void WithSettingsFile(Action<string> scenario)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "user-settings-recovery-" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(directory);
            try
            {
                scenario(Path.Combine(directory, "user-settings.json"));
            }
            finally
            {
                Directory.Delete(directory, true);
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
