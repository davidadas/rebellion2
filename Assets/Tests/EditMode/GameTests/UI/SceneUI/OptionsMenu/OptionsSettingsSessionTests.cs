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
        private GameObject _inputRoot;
        private InputManager _inputManager;
        private string _settingsPath;
        private OptionsSettingsSession _session;

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
                (_, _, _) => { }
            );
            _settingsPath = Path.Combine(
                Path.GetTempPath(),
                $"rebellion2-options-session-{Guid.NewGuid():N}.json"
            );
            UserSettingsManager settings = new UserSettingsManager(
                _audioManager,
                _displayManager,
                _inputManager,
                _settingsPath
            );
            settings.Load();
            _session = new OptionsSettingsSession(
                settings,
                _displayManager,
                _audioManager,
                _inputManager
            );
            _session.Begin();
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

        /// <summary>
        /// Verifies returning an audio channel to its opening value clears pending state.
        /// </summary>
        [Test]
        public void SetVolume_ReturnedToSnapshot_ClearsDirtyState()
        {
            _session.SetVolume(2, 0.25f);
            Assert.IsTrue(_session.IsDirty);

            _session.SetVolume(2, 1f);

            Assert.IsFalse(_session.IsDirty);
        }

        /// <summary>
        /// Verifies returning display and tactical choices to their opening values clears pending state.
        /// </summary>
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

        /// <summary>
        /// Verifies returning gameplay choices to their opening values clears pending state.
        /// </summary>
        [Test]
        public void GameplayChanges_ReturnedToSnapshot_ClearDirtyState()
        {
            _session.ToggleGameplay(UserGameplayOption.PauseAfterEnemyBombardment);
            Assert.IsTrue(_session.IsDirty);

            _session.ToggleGameplay(UserGameplayOption.PauseAfterEnemyBombardment);

            Assert.IsFalse(_session.IsDirty);
        }

        /// <summary>
        /// Verifies autosave cadence and retention changes participate in staged settings.
        /// </summary>
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

        /// <summary>
        /// Verifies removing a newly staged binding override clears pending state.
        /// </summary>
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
        private static void DeleteIfPresent(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
    }
}
