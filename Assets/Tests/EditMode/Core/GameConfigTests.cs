using System;
using NUnit.Framework;
using Rebellion.Core.Configuration;
using Rebellion.Game;

namespace Rebellion.Tests.Core
{
    [TestFixture]
    public class GameConfigTests
    {
        [Test]
        public void ConfigLoader_LoadsValidXML_Successfully()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();

            Assert.IsNotNull(config, "GameConfig should not be null");
            Assert.IsNotNull(config.AI, "AIConfig should not be null");
            Assert.IsNotNull(config.Movement, "MovementConfig should not be null");
            Assert.IsNotNull(config.Production, "ProductionConfig should not be null");
            Assert.IsNotNull(config.Planet, "PlanetConfig should not be null");
            Assert.IsNotNull(config.Victory, "VictoryConfig should not be null");
            Assert.IsNotNull(
                config.ProbabilityTables,
                "ProbabilityTablesConfig should not be null"
            );
        }

        [Test]
        public void ConfigLoader_LoadsDefaultValues_Correctly()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();

            // AI defaults
            Assert.AreEqual(7, config.AI.TickInterval);
            Assert.AreEqual(60, config.AI.DiplomacySkillThreshold);
            Assert.AreEqual(0.8f, config.AI.DiplomacyTargetPopularityCap, 0.01f);
            Assert.AreEqual(30, config.AI.EspionageSkillThreshold);
            Assert.AreEqual(0.5, config.AI.CovertMinSuccessProbability, 0.01);
            Assert.AreEqual(3, config.AI.MaxAttackFronts);
            Assert.AreEqual(100f, config.AI.BattleCooldownTicks, 0.01f);
            Assert.AreEqual(100f, config.AI.ProximityDivisor, 0.01f);
            Assert.AreEqual(0.30f, config.AI.WeightWeakness, 0.01f);
            Assert.AreEqual(0.30f, config.AI.WeightProximity, 0.01f);
            Assert.AreEqual(0.25f, config.AI.WeightDeconfliction, 0.01f);
            Assert.AreEqual(0.15f, config.AI.WeightFreshness, 0.01f);
            Assert.AreEqual(0.3f, config.AI.CovertTargetPopularityThreshold, 0.01f);

            // Movement defaults
            Assert.AreEqual(2, config.Movement.DistanceScale);
            Assert.AreEqual(10, config.Movement.MinTransitTicks);
            Assert.AreEqual(60, config.Movement.DefaultFighterHyperdrive);

            // Production defaults
            Assert.AreEqual(50, config.Production.RefinementMultiplier);

            // Planet defaults
            Assert.AreEqual(5, config.Planet.DistanceDivisor);
            Assert.AreEqual(100, config.Planet.DistanceBase);
            Assert.AreEqual(100, config.Planet.MaxPopularSupport);

            // Victory defaults
            Assert.AreEqual(200, config.Victory.MinVictoryTick);
        }

        [Test]
        public void Game_ThrowsException_WhenConfigNotSet()
        {
            GameRoot game = new GameRoot();

            Assert.Throws<InvalidOperationException>(
                () => game.GetConfig(),
                "GetConfig should throw when config not set"
            );
        }

        [Test]
        public void Game_ConfigConstructor_SetsConfigCorrectly()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();
            GameRoot game = new GameRoot(config);

            Assert.IsNotNull(game.Config, "Game.Config should not be null");
            Assert.AreEqual(config, game.Config, "Config should be the same instance");
            Assert.AreEqual(
                7,
                game.Config.AI.TickInterval,
                "Config should have loaded default values"
            );
        }

        [Test]
        public void Game_SetConfig_ValidatesAndSets()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();
            GameRoot game = new GameRoot();

            game.SetConfig(config);

            Assert.AreEqual(config, game.GetConfig(), "Config should be set correctly");
        }

        [Test]
        public void Game_SetConfig_ThrowsOnInvalidConfig()
        {
            GameConfig invalidConfig = new GameConfig();
            invalidConfig.Movement.DistanceScale = -1; // Invalid value

            GameRoot game = new GameRoot();

            Assert.Throws<InvalidOperationException>(
                () => game.SetConfig(invalidConfig),
                "SetConfig should throw on invalid config"
            );
        }

        [Test]
        public void GameManager_InjectsConfig_WhenGameHasNone()
        {
            GameRoot game = new GameRoot();
            GameManager manager = new GameManager(game);

            Assert.IsNotNull(game.Config, "GameManager should inject config");
            Assert.AreEqual(7, game.Config.AI.TickInterval, "Config should have default values");
        }

        [Test]
        public void ConfigLoader_LoadsProbabilityTables_Correctly()
        {
            GameConfig config = ConfigLoader.LoadGameConfig();

            // Verify ProbabilityTables section loaded
            Assert.IsNotNull(config.ProbabilityTables, "ProbabilityTables should not be null");

            // Verify UprisingStart table has entries
            Assert.IsNotNull(
                config.ProbabilityTables.UprisingStart,
                "UprisingStart table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.UprisingStart.Count,
                0,
                "UprisingStart should have entries"
            );

            // Verify Mission section
            Assert.IsNotNull(
                config.ProbabilityTables.Mission,
                "Mission probability tables should not be null"
            );

            // Verify all mission tables are present and have entries
            Assert.IsNotNull(
                config.ProbabilityTables.Mission.Abduction,
                "Abduction table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Abduction.Count,
                0,
                "Abduction should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission.Assassination,
                "Assassination table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Assassination.Count,
                0,
                "Assassination should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission.Diplomacy,
                "Diplomacy table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Diplomacy.Count,
                0,
                "Diplomacy should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission.DeathStarSabotage,
                "DeathStarSabotage table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.DeathStarSabotage.Count,
                0,
                "DeathStarSabotage should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission.Espionage,
                "Espionage table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Espionage.Count,
                0,
                "Espionage should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission.InciteUprising,
                "InciteUprising table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.InciteUprising.Count,
                0,
                "InciteUprising should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission.Recruitment,
                "Recruitment table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Recruitment.Count,
                0,
                "Recruitment should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission.Rescue,
                "Rescue table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Rescue.Count,
                0,
                "Rescue should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission.Sabotage,
                "Sabotage table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Sabotage.Count,
                0,
                "Sabotage should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission.SubdueUprising,
                "SubdueUprising table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.SubdueUprising.Count,
                0,
                "SubdueUprising should have entries"
            );
        }
    }
}
