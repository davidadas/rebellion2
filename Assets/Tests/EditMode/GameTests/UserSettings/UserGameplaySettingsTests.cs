using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UserSettings
{
    [TestFixture]
    public sealed class UserGameplaySettingsTests
    {
        [Test]
        public void JsonUtility_GameplayPauseOptions_RoundTripState()
        {
            global::UserSettings settings = new global::UserSettings();
            settings.Gameplay.PauseAfterEnemyBombardment = true;
            settings.Gameplay.PauseWhenSpaceBattleBegins = true;

            string json = JsonUtility.ToJson(settings);
            global::UserSettings restored = JsonUtility.FromJson<global::UserSettings>(json);
            restored.Normalize();

            Assert.IsTrue(restored.Gameplay.PauseAfterEnemyBombardment);
            Assert.IsTrue(restored.Gameplay.PauseWhenSpaceBattleBegins);
        }

        [Test]
        public void GameplayPauseOptions_Defaults_AreEnabled()
        {
            UserGameplaySettings settings = new UserGameplaySettings();

            Assert.IsTrue(settings.PauseAfterEnemyBombardment);
            Assert.IsTrue(settings.PauseWhenSpaceBattleBegins);

            settings.PauseAfterEnemyBombardment = false;
            settings.PauseWhenSpaceBattleBegins = false;
            settings.RestoreDefaults();

            Assert.IsTrue(settings.PauseAfterEnemyBombardment);
            Assert.IsTrue(settings.PauseWhenSpaceBattleBegins);
        }

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
