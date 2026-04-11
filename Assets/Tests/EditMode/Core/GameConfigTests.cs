using System;
using System.IO;
using System.Xml.Schema;
using NUnit.Framework;
using Rebellion.Game;

namespace Rebellion.Tests.Core
{
    [TestFixture]
    public class GameConfigTests
    {
        [Test]
        public void GetConfig_LoadsValidXML_Successfully()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();

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
        public void GetConfig_LoadsDefaultValues_Correctly()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();

            // AI defaults
            Assert.AreEqual(7, config.AI.TickInterval);
            Assert.IsNotNull(config.AI.MissionTables, "MissionTables should not be null");
            Assert.Greater(
                config.AI.MissionTables.Diplomacy.Count,
                0,
                "Diplomacy dispatch table should have entries"
            );
            Assert.Greater(
                config.AI.MissionTables.SubdueUprising.Count,
                0,
                "SubdueUprising dispatch table should have entries"
            );

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
        public void GetConfig_ConfigNotSet_ThrowsException()
        {
            GameRoot game = new GameRoot();

            Assert.Throws<InvalidOperationException>(
                () => game.GetConfig(),
                "GetConfig should throw when config not set"
            );
        }

        [Test]
        public void GameRoot_ConfigConstructor_SetsConfig()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
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
        public void SetConfig_ValidConfig_SetsConfig()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();
            GameRoot game = new GameRoot();

            game.SetConfig(config);

            Assert.AreEqual(config, game.GetConfig(), "Config should be set correctly");
        }

        [Test]
        public void GameManager_NoConfigSet_InjectsConfig()
        {
            GameRoot game = new GameRoot();
            GameManager manager = new GameManager(game);

            Assert.IsNotNull(game.Config, "GameManager should inject config");
            Assert.AreEqual(7, game.Config.AI.TickInterval, "Config should have default values");
        }

        [Test]
        public void GetConfig_LoadsProbabilityTables_Correctly()
        {
            GameConfig config = ResourceManager.GetConfig<GameConfig>();

            Assert.IsNotNull(config.ProbabilityTables, "ProbabilityTables should not be null");
            Assert.IsNotNull(
                config.ProbabilityTables.UprisingStart,
                "UprisingStart table should not be null"
            );
            Assert.Greater(
                config.ProbabilityTables.UprisingStart.Count,
                0,
                "UprisingStart should have entries"
            );

            Assert.IsNotNull(
                config.ProbabilityTables.Mission,
                "Mission probability tables should not be null"
            );

            Assert.Greater(
                config.ProbabilityTables.Mission.Abduction.Count,
                0,
                "Abduction should have entries"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Assassination.Count,
                0,
                "Assassination should have entries"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Diplomacy.Count,
                0,
                "Diplomacy should have entries"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.DeathStarSabotage.Count,
                0,
                "DeathStarSabotage should have entries"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Espionage.Count,
                0,
                "Espionage should have entries"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.InciteUprising.Count,
                0,
                "InciteUprising should have entries"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Recruitment.Count,
                0,
                "Recruitment should have entries"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Rescue.Count,
                0,
                "Rescue should have entries"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.Sabotage.Count,
                0,
                "Sabotage should have entries"
            );
            Assert.Greater(
                config.ProbabilityTables.Mission.SubdueUprising.Count,
                0,
                "SubdueUprising should have entries"
            );
        }

        [Test]
        public void GetConfig_SchemaValidation_RejectsNonPositiveDistanceScale()
        {
            string configPath = Path.Combine(
                UnityEngine.Application.dataPath,
                "Resources",
                "Configs",
                "GameConfig.xml"
            );
            string xml = File.ReadAllText(configPath)
                .Replace("<DistanceScale>2</DistanceScale>", "<DistanceScale>0</DistanceScale>");

            Assert.Throws<XmlSchemaValidationException>(() =>
                TestConfig.DeserializeWithSchema(xml)
            );
        }

        [Test]
        public void GetConfig_SchemaValidation_AcceptsValidConfig()
        {
            Assert.DoesNotThrow(() => TestConfig.CreateWithSchema());
        }
    }
}
