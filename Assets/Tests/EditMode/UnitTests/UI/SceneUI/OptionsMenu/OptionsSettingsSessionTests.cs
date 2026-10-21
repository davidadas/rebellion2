using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Rebellion.Tests.UI.SceneUI.OptionsMenu
{
    [TestFixture]
    public sealed class OptionsSettingsSessionTests
    {
        private AudioManager _audioManager;
        private DisplayManager _displayManager;
        private int _displayApplicationCount;
        private Vector2Int _appliedResolution;
        private FullScreenMode _appliedFullScreenMode;
        private GameObject _inputRoot;
        private InputManager _inputManager;
        private string _settingsPath;
        private OptionsSettingsSession _session;
        private UserSettingsManager _userSettings;

        /// <summary>
        /// Creates a settings session backed by deterministic display and input services.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DestroyAudioManagers();
            _audioManager = AudioManager.EnsureExists();
            _inputRoot = new GameObject("OptionsSettingsSessionTests.InputManager");
            _inputManager = _inputRoot.AddComponent<InputManager>();
            _displayManager = new DisplayManager(
                () =>
                    new List<Vector2Int> { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) },
                () => new Vector2Int(1920, 1080),
                (width, height, mode) =>
                {
                    _displayApplicationCount++;
                    _appliedResolution = new Vector2Int(width, height);
                    _appliedFullScreenMode = mode;
                }
            );
            _settingsPath = Path.Combine(
                Path.GetTempPath(),
                $"rebellion2-options-session-{Guid.NewGuid():N}.json"
            );
            _userSettings = new UserSettingsManager(
                _audioManager,
                _displayManager,
                _inputManager,
                _settingsPath
            );
            _userSettings.Load();
            _session = new OptionsSettingsSession(
                _userSettings,
                _displayManager,
                _audioManager,
                _inputManager
            );
            _session.Begin();
            _displayApplicationCount = 0;
        }

        /// <summary>
        /// Removes the runtime owners and temporary settings files created by each test.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_inputRoot != null)
                UnityEngine.Object.DestroyImmediate(_inputRoot);
            DestroyAudioManagers();
            DeleteIfPresent(_settingsPath);
            DeleteIfPresent(_settingsPath + ".tmp");
        }

        [Test]
        public void SetVolume_ReturnedToSnapshot_ClearsDirtyState()
        {
            _session.SetVolume(2, 0.25f);
            Assert.IsTrue(_session.IsDirty);

            _session.SetVolume(2, 1f);

            Assert.IsFalse(_session.IsDirty);
        }

        [Test]
        public void GraphicsChanges_ReturnedToSnapshot_ClearDirtyState()
        {
            _session.StepResolution(-1);
            Assert.IsTrue(_session.IsDirty);
            _session.StepResolution(1);
            Assert.IsFalse(_session.IsDirty);

            _session.StepFullScreen(1);
            Assert.IsTrue(_session.IsDirty);
            _session.StepFullScreen(-1);
            Assert.IsFalse(_session.IsDirty);

            _session.ToggleTactical(UserTacticalOption.Starfield);
            Assert.IsTrue(_session.IsDirty);
            _session.ToggleTactical(UserTacticalOption.Starfield);
            Assert.IsFalse(_session.IsDirty);
        }

        [TestCase(UserFrameGenerationMode.Off)]
        [TestCase(UserFrameGenerationMode.On)]
        public void StepFullScreen_ExclusiveMode_PreservesFrameGenerationPreference(
            UserFrameGenerationMode preference
        )
        {
            GameObject graphicsOwner = new GameObject("Options Graphics Test Owner");
            GraphicsManager graphicsManager = new GraphicsManager(
                graphicsOwner.transform,
                () => true,
                _ => { }
            );
            OptionsSettingsSession session = new OptionsSettingsSession(
                _userSettings,
                _displayManager,
                _audioManager,
                _inputManager,
                graphicsManager
            );
            try
            {
                session.Begin();
                session.Video.FullScreenMode = (int)FullScreenMode.FullScreenWindow;
                session.Video.FrameGenerationMode = (int)preference;

                session.StepFullScreen(-1);

                Assert.AreEqual(
                    (int)FullScreenMode.ExclusiveFullScreen,
                    session.Video.FullScreenMode
                );
                Assert.AreEqual("Fullscreen", session.FullScreenLabel);
                Assert.AreEqual((int)preference, session.Video.FrameGenerationMode);
                OptionsGraphicsValue exclusiveValue = session.GetGraphicsValues()[
                    UserGraphicsSetting.FrameGeneration
                ];
                Assert.IsFalse(exclusiveValue.Available);
                Assert.AreEqual("Off (Fullscreen)", exclusiveValue.Label);

                session.StepFullScreen(1);

                Assert.AreEqual((int)FullScreenMode.FullScreenWindow, session.Video.FullScreenMode);
                Assert.AreEqual("Borderless", session.FullScreenLabel);
                Assert.AreEqual((int)preference, session.Video.FrameGenerationMode);
            }
            finally
            {
                graphicsManager.Dispose();
                UnityEngine.Object.DestroyImmediate(graphicsOwner);
            }
        }

        /// <summary>
        /// Verifies returning a graphics setting to its snapshot clears the dirty state.
        /// </summary>
        [Test]
        public void StepGraphics_ReturnedToSnapshot_ClearsDirtyState()
        {
            _session.StepGraphics(UserGraphicsSetting.AntiAliasing, 1);
            Assert.IsTrue(_session.IsDirty);

            _session.StepGraphics(UserGraphicsSetting.AntiAliasing, -1);

            Assert.IsFalse(_session.IsDirty);
        }

        /// <summary>
        /// Verifies stepping the quality preset updates all preset-owned settings.
        /// </summary>
        [Test]
        public void StepGraphics_QualityPreset_UpdatesPresetOwnedSettings()
        {
            _session.StepGraphics(UserGraphicsSetting.QualityPreset, -1);

            Assert.AreEqual(4, _session.Video.QualityLevel);
            Assert.AreEqual((int)UserAntiAliasingMode.Taa, _session.Video.AntiAliasingMode);
            Assert.IsTrue(_session.Video.AmbientOcclusion);
            Assert.IsTrue(_session.Video.Bloom);
            Assert.IsFalse(_session.Video.MotionBlur);
            Assert.IsFalse(_session.Video.GraphicsPresetCustomized);
        }

        /// <summary>
        /// Verifies editing a preset-owned setting changes the preset label to Custom.
        /// </summary>
        [Test]
        public void StepGraphics_PresetOwnedSetting_MarksPresetCustom()
        {
            _session.StepGraphics(UserGraphicsSetting.AntiAliasing, -1);

            Assert.IsTrue(_session.Video.GraphicsPresetCustomized);
            Assert.AreEqual(
                "Custom",
                _session.GetGraphicsValues()[UserGraphicsSetting.QualityPreset].Label
            );
        }

        /// <summary>
        /// Verifies that VSync communicates and enforces its ownership of frame pacing.
        /// </summary>
        [Test]
        public void GetGraphicsValues_VSyncEnabled_DisablesFrameRateLimitControl()
        {
            OptionsGraphicsValue value = _session.GetGraphicsValues()[
                UserGraphicsSetting.FrameRateLimit
            ];

            Assert.IsFalse(value.Available);
            Assert.AreEqual("Controlled by VSync", value.Label);

            _session.StepGraphics(UserGraphicsSetting.FrameRateLimit, 1);

            Assert.AreEqual(0, _session.Video.FrameRateLimit);

            _session.StepGraphics(UserGraphicsSetting.VerticalSync, 1);
            value = _session.GetGraphicsValues()[UserGraphicsSetting.FrameRateLimit];

            Assert.IsTrue(value.Available);
            Assert.AreEqual("Unlimited", value.Label);
        }

        /// <summary>
        /// Verifies that changing a setting outside preset ownership preserves the preset label.
        /// </summary>
        [Test]
        public void StepGraphics_VerticalSyncChanged_PreservesPresetSelection()
        {
            _session.StepGraphics(UserGraphicsSetting.VerticalSync, 1);

            Assert.IsFalse(_session.Video.GraphicsPresetCustomized);
            Assert.AreEqual(
                "Ultra",
                _session.GetGraphicsValues()[UserGraphicsSetting.QualityPreset].Label
            );
        }

        /// <summary>
        /// Verifies settings that require runtime hardware support expose availability separately
        /// from their player-facing value.
        /// </summary>
        [Test]
        public void GetGraphicsValues_RuntimeManagerUnavailable_MarksHardwareOptionsUnavailable()
        {
            Dictionary<UserGraphicsSetting, OptionsGraphicsValue> values =
                _session.GetGraphicsValues();

            Assert.IsFalse(values[UserGraphicsSetting.RayTracing].Available);
            Assert.AreEqual("Unavailable", values[UserGraphicsSetting.RayTracing].Label);
            Assert.IsFalse(values[UserGraphicsSetting.FrameGeneration].Available);
            Assert.AreEqual("Unavailable", values[UserGraphicsSetting.FrameGeneration].Label);
            Assert.IsFalse(values[UserGraphicsSetting.HdrOutput].Available);
            Assert.AreEqual("Unavailable", values[UserGraphicsSetting.HdrOutput].Label);
            Assert.IsTrue(values[UserGraphicsSetting.Bloom].Available);

            _session.StepGraphics(UserGraphicsSetting.RayTracing, 1);

            Assert.IsFalse(_session.Video.RayTracing);
            Assert.IsFalse(_session.IsDirty);
        }

        /// <summary>
        /// Verifies Graphics-page defaults restore the complete default graphics profile.
        /// </summary>
        [Test]
        public void RestoreDefaults_GraphicsSelected_RestoresGraphicsDefaults()
        {
            _session.StepGraphics(UserGraphicsSetting.AntiAliasing, 1);
            _session.StepGraphics(UserGraphicsSetting.VerticalSync, 1);
            _session.StepGraphics(UserGraphicsSetting.FrameRateLimit, 1);

            _session.RestoreDefaults(OptionsMenuTab.Graphics);

            Assert.AreEqual((int)UserAntiAliasingMode.Taa, _session.Video.AntiAliasingMode);
            Assert.AreEqual(0, _session.Video.FrameRateLimit);
            Assert.IsTrue(_session.Video.VerticalSync);
        }

        [Test]
        public void DisplayChanges_Default_ApplyOnlyWhenCommitted()
        {
            _session.StepResolution(-1);
            _session.StepFullScreen(1);

            Assert.AreEqual(0, _displayApplicationCount);

            _session.Revert();

            Assert.AreEqual(0, _displayApplicationCount);

            _session.StepResolution(-1);
            _session.StepFullScreen(1);
            _session.Commit();

            Assert.AreEqual(1, _displayApplicationCount);
            Assert.AreEqual(new Vector2Int(1280, 720), _appliedResolution);
            Assert.AreEqual(FullScreenMode.FullScreenWindow, _appliedFullScreenMode);
        }

        [Test]
        public void GameplayChanges_ReturnedToSnapshot_ClearDirtyState()
        {
            _session.ToggleGameplay(UserGameplayOption.PauseAfterEnemyBombardment);
            Assert.IsTrue(_session.IsDirty);

            _session.ToggleGameplay(UserGameplayOption.PauseAfterEnemyBombardment);

            Assert.IsFalse(_session.IsDirty);
        }

        [Test]
        public void RestoreDefaults_GameplaySelected_RestoresGameplayAndPreservesUserInterfaceSettings()
        {
            _session.ToggleUserInterface(UserInterfaceOption.ShowUnitCardStats);
            _session.SetGameLaunchSpeed(GameLaunchSpeed.Fast);

            _session.RestoreDefaults(OptionsMenuTab.Gameplay);

            Assert.IsTrue(_session.UserInterface.ShowUnitCardStats);
            Assert.AreEqual(GameLaunchSpeed.Unchanged, _session.Gameplay.GameLaunchSpeed);
        }

        [Test]
        public void RestoreDefaults_UserInterfaceSelected_RestoresUserInterfaceAndPreservesGameplaySettings()
        {
            _session.ToggleUserInterface(UserInterfaceOption.ShowIdleBar);
            _session.ToggleUserInterface(UserInterfaceOption.KeepIdleBarOpen);
            _session.ToggleUserInterface(UserInterfaceOption.ShowUnitCardStats);
            _session.ToggleUserInterface(UserInterfaceOption.ShowFrameRate);
            _session.SetIdleBarLocation(IdleBarLocation.BottomLeft);
            _session.SetGameLaunchSpeed(GameLaunchSpeed.Fast);

            _session.RestoreDefaults(OptionsMenuTab.UserInterface);

            Assert.IsTrue(_session.UserInterface.ShowIdleBar);
            Assert.IsFalse(_session.UserInterface.KeepIdleBarOpen);
            Assert.IsFalse(_session.UserInterface.ShowUnitCardStats);
            Assert.IsFalse(_session.UserInterface.ShowFrameRate);
            Assert.AreEqual(IdleBarLocation.TopRight, _session.UserInterface.IdleBarLocation);
            Assert.AreEqual(GameLaunchSpeed.Fast, _session.Gameplay.GameLaunchSpeed);
        }

        [Test]
        public void IdleBarLocation_ReturnedToSnapshot_ClearsDirtyState()
        {
            _session.SetIdleBarLocation(IdleBarLocation.BottomRight);
            Assert.IsTrue(_session.IsDirty);

            _session.SetIdleBarLocation(IdleBarLocation.TopRight);

            Assert.IsFalse(_session.IsDirty);
        }

        [Test]
        public void GameLaunchSpeed_ReturnedToSnapshot_ClearsDirtyState()
        {
            _session.SetGameLaunchSpeed(GameLaunchSpeed.Medium);
            Assert.IsTrue(_session.IsDirty);

            _session.SetGameLaunchSpeed(GameLaunchSpeed.Unchanged);

            Assert.IsFalse(_session.IsDirty);
        }

        [Test]
        public void AutosaveChanges_ReturnedToSnapshot_ClearDirtyState()
        {
            _session.SetAutosaveInterval(125);
            Assert.IsTrue(_session.IsDirty);
            _session.SetAutosaveInterval(UserGameplaySettings.DefaultAutosaveIntervalTicks);
            Assert.IsFalse(_session.IsDirty);

            _session.SetAutosavesToKeep(6);
            Assert.IsTrue(_session.IsDirty);
            _session.SetAutosavesToKeep(UserGameplaySettings.DefaultAutosavesToKeep);
            Assert.IsFalse(_session.IsDirty);
        }

        [Test]
        public void BindingOverride_ReturnedToSnapshot_ClearsDirtyState()
        {
            InputAction action = _inputManager.Asset.FindAction("Strategy/ShowTroopers", true);
            int bindingIndex = FindBinding(action, "Primary");
            action.ApplyBindingOverride(bindingIndex, "<Keyboard>/n");
            _session.MarkInputChanged();
            Assert.IsTrue(_session.IsDirty);

            action.RemoveBindingOverride(bindingIndex);
            _session.MarkInputChanged();

            Assert.IsFalse(_session.IsDirty);
        }

        /// <summary>
        /// Finds one authored top-level binding by name.
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
        /// Removes all AudioManager instances so singleton state cannot leak between tests.
        /// </summary>
        private static void DestroyAudioManagers()
        {
            foreach (
                AudioManager manager in UnityEngine.Object.FindObjectsByType<AudioManager>(
                    FindObjectsInactive.Include
                )
            )
            {
                UnityEngine.Object.DestroyImmediate(manager.gameObject);
            }
        }

        /// <summary>
        /// Deletes a test file when it exists.
        /// </summary>
        /// <param name="path">The path.</param>
        private static void DeleteIfPresent(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
    }
}
