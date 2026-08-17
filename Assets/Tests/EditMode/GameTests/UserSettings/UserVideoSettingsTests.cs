using NUnit.Framework;
using UnityEngine;

namespace Rebellion.Tests.UserSettings
{
    [TestFixture]
    public sealed class UserVideoSettingsTests
    {
        [TestCase(UserTacticalOption.Starfield)]
        [TestCase(UserTacticalOption.Planet)]
        [TestCase(UserTacticalOption.Pyro)]
        [TestCase(UserTacticalOption.HighDetail)]
        [TestCase(UserTacticalOption.Holocube)]
        public void SetEnabled_OptionDisabled_UpdatesRequestedOption(UserTacticalOption option)
        {
            UserVideoSettings settings = new UserVideoSettings();

            settings.SetEnabled(option, false);

            Assert.IsFalse(settings.IsEnabled(option));
        }

        [Test]
        public void Normalize_InvalidDisplayValues_RestoresNativeExclusiveDefaults()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                ResolutionWidth = -1,
                ResolutionHeight = -1,
                FullScreenMode = 99,
            };

            settings.Normalize();

            Assert.AreEqual(0, settings.ResolutionWidth);
            Assert.AreEqual(0, settings.ResolutionHeight);
            Assert.AreEqual((int)FullScreenMode.ExclusiveFullScreen, settings.FullScreenMode);
        }

        [Test]
        public void Normalize_ValidDisplayValues_PreservesSelection()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                ResolutionWidth = 1920,
                ResolutionHeight = 1080,
                FullScreenMode = (int)FullScreenMode.Windowed,
            };

            settings.Normalize();

            Assert.AreEqual(1920, settings.ResolutionWidth);
            Assert.AreEqual(1080, settings.ResolutionHeight);
            Assert.AreEqual((int)FullScreenMode.Windowed, settings.FullScreenMode);
        }

        /// <summary>
        /// Verifies normalization clears a persisted resolution outside the supported aspect ratio.
        /// </summary>
        [Test]
        public void Normalize_NonSixteenByNineResolution_ClearsUnsupportedSelection()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                ResolutionWidth = 3840,
                ResolutionHeight = 1600,
            };

            settings.Normalize();

            Assert.AreEqual(0, settings.ResolutionWidth);
            Assert.AreEqual(0, settings.ResolutionHeight);
        }

        /// <summary>
        /// Verifies an ultrawide target falls back to the largest fitting 16:9 mode.
        /// </summary>
        [Test]
        public void Resolve_UltrawideTarget_SelectsLargestFittingSixteenByNineMode()
        {
            Vector2Int[] supported =
            {
                new Vector2Int(1280, 720),
                new Vector2Int(1920, 1080),
                new Vector2Int(2560, 1440),
                new Vector2Int(3840, 2160),
            };

            Vector2Int selected = DisplayManager.ResolveResolution(
                supported,
                3840,
                1600,
                3840,
                1600
            );

            Assert.AreEqual(new Vector2Int(2560, 1440), selected);
            Assert.IsTrue(DisplayManager.IsSixteenByNine(selected.x, selected.y));
        }

        [TestCase(1920, 1080, true)]
        [TestCase(2560, 1440, true)]
        [TestCase(3840, 1600, false)]
        /// <summary>
        /// Verifies the aspect-ratio predicate accepts only 16:9 dimensions.
        /// </summary>
        [TestCase(1366, 768, true)]
        [TestCase(1920, 1200, false)]
        public void IsSixteenByNine_AcceptsOnlySixteenByNineModes(
            int width,
            int height,
            bool expected
        )
        {
            Assert.AreEqual(expected, DisplayManager.IsSixteenByNine(width, height));
        }

        [Test]
        public void JsonUtility_ExplicitTacticalOptions_RoundTripsState()
        {
            global::UserSettings settings = new global::UserSettings();
            settings.Video.SetEnabled(UserTacticalOption.Starfield, false);
            settings.Video.SetEnabled(UserTacticalOption.HighDetail, false);

            string json = JsonUtility.ToJson(settings);
            global::UserSettings restored = JsonUtility.FromJson<global::UserSettings>(json);
            restored.Normalize();

            Assert.IsFalse(restored.Video.ShowStarfield);
            Assert.IsTrue(restored.Video.ShowPlanet);
            Assert.IsTrue(restored.Video.ShowPyro);
            Assert.IsFalse(restored.Video.HighDetail);
            Assert.IsTrue(restored.Video.ShowHolocube);
        }

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
    }
}
