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
        /// Verifies unsupported persisted graphics values normalize to supported values.
        /// </summary>
        [Test]
        public void Normalize_InvalidGraphicsValues_RestoresSupportedValues()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                QualityLevel = -3,
                AntiAliasingMode = 99,
                FrameRateLimit = 75,
            };

            settings.Normalize();

            Assert.AreEqual(0, settings.QualityLevel);
            Assert.AreEqual((int)UserAntiAliasingMode.Off, settings.AntiAliasingMode);
            Assert.AreEqual(0, settings.FrameRateLimit);
        }

        /// <summary>
        /// Verifies a persisted quality tier above the configured project range clamps to the
        /// highest supported tier.
        /// </summary>
        [Test]
        public void Normalize_QualityLevelAboveMaximum_ClampsToHighestTier()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                QualityLevel = UserVideoSettings.DefaultQualityLevel + 20,
            };

            settings.Normalize();

            Assert.AreEqual(UserVideoSettings.DefaultQualityLevel, settings.QualityLevel);
        }

        /// <summary>
        /// Verifies settings files written before graphics fields existed receive the declared
        /// graphics defaults during Unity JSON deserialization.
        /// </summary>
        [Test]
        public void JsonUtility_OmittedGraphicsValues_UsesDeclaredDefaults()
        {
            UserVideoSettings settings = JsonUtility.FromJson<UserVideoSettings>("{}");

            Assert.AreEqual(UserVideoSettings.DefaultQualityLevel, settings.QualityLevel);
            Assert.AreEqual((int)UserAntiAliasingMode.Taa, settings.AntiAliasingMode);
            Assert.IsTrue(settings.VerticalSync);
            Assert.IsTrue(settings.AmbientOcclusion);
            Assert.IsTrue(settings.Bloom);
            Assert.IsTrue(settings.MotionBlur);
        }

        /// <summary>
        /// Verifies ray-traced ambient occlusion cannot remain enabled while ambient occlusion is
        /// disabled.
        /// </summary>
        [Test]
        public void Normalize_AmbientOcclusionDisabled_DisablesRayTracing()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                AmbientOcclusion = false,
                RayTracing = true,
            };

            settings.Normalize();

            Assert.IsFalse(settings.RayTracing);
        }

        /// <summary>
        /// Verifies restoring graphics defaults resets every graphics setting.
        /// </summary>
        [Test]
        public void RestoreGraphicsDefaults_ChangedGraphicsValues_RestoresDefaults()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                QualityLevel = 1,
                AntiAliasingMode = (int)UserAntiAliasingMode.Taa,
                FrameRateLimit = 60,
                VerticalSync = false,
                RayTracing = true,
                AmbientOcclusion = false,
                Bloom = false,
                MotionBlur = false,
                HdrOutput = true,
            };

            settings.RestoreGraphicsDefaults();

            Assert.AreEqual(UserVideoSettings.DefaultQualityLevel, settings.QualityLevel);
            Assert.AreEqual((int)UserAntiAliasingMode.Taa, settings.AntiAliasingMode);
            Assert.AreEqual(0, settings.FrameRateLimit);
            Assert.IsTrue(settings.VerticalSync);
            Assert.IsFalse(settings.RayTracing);
            Assert.IsTrue(settings.AmbientOcclusion);
            Assert.IsTrue(settings.Bloom);
            Assert.IsTrue(settings.MotionBlur);
            Assert.IsFalse(settings.HdrOutput);
        }

        /// <summary>
        /// Verifies a quality preset applies all rendering settings owned by that preset.
        /// </summary>
        [Test]
        public void ApplyGraphicsPreset_LowTier_UpdatesOwnedRenderingSettings()
        {
            UserVideoSettings settings = new UserVideoSettings
            {
                AntiAliasingMode = (int)UserAntiAliasingMode.Taa,
                AmbientOcclusion = true,
                Bloom = false,
                MotionBlur = true,
                RayTracing = true,
                GraphicsPresetCustomized = true,
            };

            settings.ApplyGraphicsPreset(1);

            Assert.AreEqual(1, settings.QualityLevel);
            Assert.AreEqual((int)UserAntiAliasingMode.Fxaa, settings.AntiAliasingMode);
            Assert.IsFalse(settings.AmbientOcclusion);
            Assert.IsTrue(settings.Bloom);
            Assert.IsFalse(settings.MotionBlur);
            Assert.IsFalse(settings.RayTracing);
            Assert.IsFalse(settings.GraphicsPresetCustomized);
        }

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
        [TestCase(1366, 768, true)]
        [TestCase(1920, 1200, false)]
        public void IsSixteenByNine_Default_AcceptsOnlySixteenByNineModes(
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
    }
}
