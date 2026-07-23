using System;
using System.IO;
using System.Reflection;
using System.Xml.Schema;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Generation;

namespace Rebellion.Tests.Game
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
            Assert.IsNotNull(config.GameSpeed, "GameSpeedConfig should not be null");
            Assert.IsNotNull(config.Messages, "MessageConfig should not be null");
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
            Assert.AreEqual(0f, config.AI.Selection.MinimumSelectableScore);

            // Movement defaults
            Assert.AreEqual(12, config.Movement.DistanceScale);
            Assert.AreEqual(10, config.Movement.MinTransitTicks);
            Assert.AreEqual(1, config.Movement.SameSystemMinTransitTicks);
            Assert.AreEqual(60, config.Movement.DefaultFighterHyperdrive);

            Assert.AreEqual(2, config.SupportShift.WeakSupportPenaltyDivisor);
            Assert.AreEqual(10, config.SupportShift.GarrisonRemovalSupportShift);
            Assert.AreEqual(1, config.SupportShift.ControlChangeSupportShift);
            Assert.AreEqual(6, config.Combat.PlanetaryAssault.CaptureGarrisonCount);
            Assert.AreEqual(-2, config.Combat.Bombardment.DestroySystemOuterRimSupportPenalty);
            Assert.AreEqual(90, config.Combat.Bombardment.DestroySystemOuterRimSupportThreshold);
            CollectionAssert.Contains(
                config.Combat.Bombardment.PlanetDestroyingCapitalShipTypeIDs,
                "CSEM015"
            );
            Assert.AreEqual(1f, config.GameSpeed.FastTickIntervalSeconds);
            Assert.AreEqual(10f, config.GameSpeed.MediumTickIntervalSeconds);
            Assert.AreEqual(60f, config.GameSpeed.SlowTickIntervalSeconds);
            Assert.AreEqual(120f, config.GameSpeed.VerySlowTickIntervalSeconds);
            Assert.AreEqual(300, config.Messages.RetentionTicks);
            Assert.AreEqual(50, config.ProbabilityTables.Mission.DefaultKillOrCaptureProbability);
            Assert.AreEqual(1, config.SupportShift.DiplomacyCompletionSupportBonus);
            Assert.AreEqual(1, config.SupportShift.DiplomacyOwnedPlanetSupportBase);
            Assert.AreEqual(19, config.SupportShift.DiplomacyOwnedPlanetSupportRange);
            Assert.AreEqual(1, config.SupportShift.DiplomacyNeutralPlanetSupportBase);
            Assert.AreEqual(9, config.SupportShift.DiplomacyNeutralPlanetSupportRange);
            Assert.AreEqual(2, config.Production.ScrapRefundDivisor);
            Assert.AreEqual(20, config.Production.ResourceMaintenanceLoadPercent);
            Assert.AreEqual(100, config.Production.ResourceCollectionBasePercent);
            Assert.AreEqual(50, config.Production.ResourceStartupBasePercent);
            Assert.AreEqual(100, config.Production.ResourceStartupRandomPercent);
            Assert.AreEqual(5, config.Blockade.CapitalShipProductionPenaltyPercent);
            Assert.AreEqual(2, config.Blockade.FighterProductionPenaltyPercent);
        }

        [Test]
        public void GetGenerationConfig_LoadsBudgetDifficultyMapping_Correctly()
        {
            GameGenerationConfig config = ResourceManager.GetConfig<GameGenerationConfig>();

            Assert.IsNotNull(config.UnitDeployment.BudgetDifficultyMappings);
            Assert.AreEqual(1, config.UnitDeployment.BudgetDifficultyMappings.Count);
            Assert.AreEqual(2, config.UnitDeployment.BudgetDifficultyMappings[0].Difficulty);
            Assert.AreEqual(1, config.UnitDeployment.BudgetDifficultyMappings[0].BudgetDifficulty);
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
        public void GameManager_SetGameSpeed_UsesConfiguredIntervals()
        {
            GameConfig config = TestConfig.Create();
            config.GameSpeed.FastTickIntervalSeconds = 2.5f;
            config.GameSpeed.MediumTickIntervalSeconds = 12.5f;
            config.GameSpeed.SlowTickIntervalSeconds = 90.5f;
            config.GameSpeed.VerySlowTickIntervalSeconds = 120.5f;
            GameRoot game = new GameRoot(config);
            GameManager manager = new GameManager(game);

            manager.SetGameSpeed(TickSpeed.Fast);
            Assert.AreEqual(2.5f, GetTickInterval(manager));

            manager.SetGameSpeed(TickSpeed.Medium);
            Assert.AreEqual(12.5f, GetTickInterval(manager));

            manager.SetGameSpeed(TickSpeed.Slow);
            Assert.AreEqual(90.5f, GetTickInterval(manager));

            manager.SetGameSpeed(TickSpeed.VerySlow);
            Assert.AreEqual(120.5f, GetTickInterval(manager));
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
            Assert.AreEqual(35, config.ProbabilityTables.Mission.FoilDefenderScalingPercent);
            Assert.AreEqual(-1, config.ProbabilityTables.Mission.FoilFlatScoreAdjustment);

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
                .Replace("<DistanceScale>12</DistanceScale>", "<DistanceScale>0</DistanceScale>");

            Assert.Throws<XmlSchemaValidationException>(() =>
                TestConfig.DeserializeWithSchema(xml)
            );
        }

        [Test]
        public void GetConfig_SchemaValidation_AcceptsValidConfig()
        {
            Assert.DoesNotThrow(() => TestConfig.CreateWithSchema());
        }

        private static float? GetTickInterval(GameManager manager)
        {
            FieldInfo field = typeof(GameManager).GetField(
                "_tickInterval",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            return (float?)field.GetValue(manager);
        }
    }
}
