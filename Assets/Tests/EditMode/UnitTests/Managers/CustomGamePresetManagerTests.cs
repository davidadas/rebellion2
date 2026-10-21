using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Util.Reflection;

namespace Rebellion.Tests.Managers
{
    [TestFixture]
    public sealed class CustomGamePresetManagerTests
    {
        private string directoryPath;
        private CustomGamePresetManager manager;

        /// <summary>
        /// Creates isolated custom-game preset storage.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            directoryPath = Path.Combine(
                Path.GetTempPath(),
                nameof(CustomGamePresetManagerTests),
                Guid.NewGuid().ToString("N")
            );
            manager = new CustomGamePresetManager(directoryPath);
        }

        /// <summary>
        /// Removes isolated custom-game preset storage.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, true);
        }

        [Test]
        public void SavePreset_CompleteSetup_RoundTripsLaunchConfiguration()
        {
            GameSetup setup = CreateSetup();

            manager.SavePreset("fleet-war", "Fleet War", setup);
            GameSetup loaded = manager.LoadPreset("fleet-war");

            Assert.AreEqual(setup.PlayerFactionID, loaded.PlayerFactionID);
            Assert.AreEqual(setup.Difficulty, loaded.Difficulty);
            Assert.AreEqual(setup.GalaxySize, loaded.GalaxySize);
            Assert.AreEqual(setup.VictoryCondition, loaded.VictoryCondition);
            Assert.AreEqual(setup.ResourceAvailability, loaded.ResourceAvailability);
            Assert.AreEqual(setup.Seed, loaded.Seed);
            CollectionAssert.AreEqual(
                setup.CustomSettings.GenerationConfig.DisabledEventCategories,
                loaded.CustomSettings.GenerationConfig.DisabledEventCategories
            );
            Assert.AreEqual(
                setup.CustomSettings.AIDifficultyModifiers.MineOutputPercent,
                loaded.CustomSettings.AIDifficultyModifiers.MineOutputPercent
            );
            Assert.AreEqual(
                setup.CustomSettings.GenerationConfig.FacilityGeneration.CoreMineMultiplier,
                loaded.CustomSettings.GenerationConfig.FacilityGeneration.CoreMineMultiplier
            );
        }

        [Test]
        public void RenamePreset_ExistingPreset_UpdatesDisplayNameAndPreservesSetup()
        {
            manager.SavePreset("fleet-war", "Fleet War", CreateSetup());

            manager.RenamePreset("fleet-war", "Long Campaign");

            CustomGamePresetEntry entry = manager.GetPresets()[0];
            Assert.AreEqual("Long Campaign", entry.DisplayName);
            Assert.AreEqual("FNEMP1", entry.Setup.PlayerFactionID);
            Assert.AreEqual(1729, entry.Setup.Seed);
        }

        [Test]
        public void DeletePreset_ExistingPreset_RemovesPreset()
        {
            manager.SavePreset("fleet-war", "Fleet War", CreateSetup());

            manager.DeletePreset("fleet-war");

            Assert.IsEmpty(manager.GetPresets());
        }

        /// <summary>
        /// Creates a launch setup containing every configurable value family.
        /// </summary>
        /// <returns>The populated launch setup.</returns>
        private static GameSetup CreateSetup()
        {
            return new GameSetup
            {
                PlayerFactionID = "FNEMP1",
                StartingFactionIDs = new[] { "FNALL1", "FNEMP1" },
                Difficulty = GameDifficulty.Hard,
                GalaxySize = GameSize.Small,
                VictoryCondition = GameVictoryCondition.Headquarters,
                ResourceAvailability = GameResourceAvailability.Abundant,
                Seed = 1729,
                PackID = "pack",
                PackVersion = "1.0",
                ScenarioID = "scenario",
                ModIDs = new[] { "mod" },
                ModVersions = new[] { "2.0" },
                CustomSettings = new CustomGameSettings
                {
                    PlayerDifficultyModifiers = new DifficultyModifiers { MineOutputPercent = 115 },
                    AIDifficultyModifiers = new DifficultyModifiers { MineOutputPercent = 135 },
                    GenerationConfig = CreateGenerationConfig(),
                },
            };
        }

        /// <summary>
        /// Creates a detached generation configuration with a recognizable override.
        /// </summary>
        /// <returns>The customized generation configuration.</returns>
        private static GameGenerationConfig CreateGenerationConfig()
        {
            GameGenerationConfig config = TestContent.Data.GenerationConfig.GetDeepCopy();
            config.FacilityGeneration.CoreMineMultiplier = 17;
            config.DisabledEventCategories = new List<GameEventCategory>
            {
                GameEventCategory.Disaster,
            };
            return config;
        }
    }
}
