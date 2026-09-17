using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UserSettings
{
    [TestFixture]
    public sealed class UserGameplaySettingsTests
    {
        /// <summary>
        /// Verifies json utility gameplay options round trip state.
        /// </summary>
        [Test]
        public void JsonUtility_GameplayOptions_RoundTripState()
        {
            global::UserSettings settings = new global::UserSettings();
            settings.Gameplay.PauseAfterEnemyBombardment = true;
            settings.Gameplay.DisableBriefings = true;
            settings.Gameplay.PauseWhenSpaceBattleBegins = true;
            settings.UserInterface.ShowIdleBar = true;
            settings.UserInterface.KeepIdleBarOpen = true;
            settings.Gameplay.ShowMissionOdds = false;

            string json = JsonUtility.ToJson(settings);
            global::UserSettings restored = JsonUtility.FromJson<global::UserSettings>(json);
            restored.Normalize();

            Assert.IsTrue(restored.Gameplay.PauseAfterEnemyBombardment);
            Assert.IsTrue(restored.Gameplay.DisableBriefings);
            Assert.IsTrue(restored.Gameplay.PauseWhenSpaceBattleBegins);
            Assert.IsTrue(restored.UserInterface.ShowIdleBar);
            Assert.IsTrue(restored.UserInterface.KeepIdleBarOpen);
            Assert.IsFalse(restored.Gameplay.ShowMissionOdds);
        }

        /// <summary>
        /// Verifies that briefings remain enabled until the user disables them.
        /// </summary>
        [Test]
        public void DisableBriefings_DefaultAndRestore_RemainsDisabledOnlyWhenSelected()
        {
            UserGameplaySettings settings = new UserGameplaySettings();

            Assert.IsFalse(settings.DisableBriefings);

            settings.SetEnabled(UserGameplayOption.DisableBriefings, true);
            Assert.IsTrue(settings.IsEnabled(UserGameplayOption.DisableBriefings));

            settings.RestoreDefaults();
            Assert.IsFalse(settings.DisableBriefings);
        }

        /// <summary>
        /// Verifies gameplay pause options defaults are enabled.
        /// </summary>
        [Test]
        public void GameplayPauseOptions_Defaults_AreEnabled()
        {
            UserGameplaySettings settings = new UserGameplaySettings();

            Assert.IsTrue(settings.PauseAfterEnemyBombardment);
            Assert.IsTrue(settings.PauseWhenSpaceBattleBegins);
            Assert.IsTrue(settings.ShowMissionOdds);

            settings.PauseAfterEnemyBombardment = false;
            settings.PauseWhenSpaceBattleBegins = false;
            settings.ShowMissionOdds = false;
            settings.RestoreDefaults();

            Assert.IsTrue(settings.PauseAfterEnemyBombardment);
            Assert.IsTrue(settings.PauseWhenSpaceBattleBegins);
            Assert.IsTrue(settings.ShowMissionOdds);
        }

        /// <summary>
        /// Verifies json utility omitted mission odds preference defaults enabled.
        /// </summary>
        [Test]
        public void JsonUtility_OmittedMissionOddsPreference_DefaultsEnabled()
        {
            UserGameplaySettings settings = JsonUtility.FromJson<UserGameplaySettings>("{}");

            Assert.IsTrue(settings.ShowMissionOdds);
        }

        /// <summary>
        /// Verifies json utility omitted idle bar preference defaults enabled.
        /// </summary>
        [Test]
        public void UserInterfaceOptions_DefaultsAndRestore_AreApplied()
        {
            UserInterfaceSettings settings = new UserInterfaceSettings
            {
                ShowIdleBar = false,
                KeepIdleBarOpen = true,
            };

            settings.RestoreDefaults();

            Assert.IsTrue(settings.ShowIdleBar);
            Assert.IsFalse(settings.KeepIdleBarOpen);
        }

        /// <summary>
        /// Verifies gameplay autosave options defaults and normalization are applied.
        /// </summary>
        [Test]
        public void GameplayAutosaveOptions_DefaultsAndNormalization_AreApplied()
        {
            UserGameplaySettings settings = new UserGameplaySettings();

            Assert.IsTrue(settings.AutosaveEnabled);
            Assert.AreEqual(100, settings.AutosaveIntervalTicks);
            Assert.AreEqual(5, settings.AutosavesToKeep);

            settings.SetAutosaveIntervalTicks(5);
            settings.SetAutosavesToKeep(int.MinValue);

            Assert.AreEqual(5, settings.AutosaveIntervalTicks);
            Assert.AreEqual(UserGameplaySettings.MinimumAutosavesToKeep, settings.AutosavesToKeep);
        }
    }
}
