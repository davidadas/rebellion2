using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class GameConfigTests
    {
        [Test]
        public void CreateCopy_ConfiguredRules_PreservesAllValues()
        {
            GameConfig source = TestContent.Data.GameConfig;

            GameConfig copy = source.CreateCopy();

            Assert.AreEqual(
                SerializationHelper.Serialize(source),
                SerializationHelper.Serialize(copy)
            );
        }

        [Test]
        public void CreateCopy_EditingNestedRules_DoesNotChangeSource()
        {
            GameConfig source = TestContent.Data.GameConfig.CreateCopy();
            string before = SerializationHelper.Serialize(source);
            GameConfig copy = source.CreateCopy();

            copy.AI.Selection.TechnologyUtility.Building.Capability.Curve.Exponent += 1;
            copy.Captive.EscapeAttemptInterval.Minimum += 1;
            copy.Captive.EscapeTable.Clear();
            copy.Jedi.MissionEncounterPriorityOfficerInstanceIDs.Clear();
            copy.DifficultyModifiers[GameDifficulty.Hard].ManufacturingSpeedPercent += 1;
            copy.Movement.DistanceDivisor += 1;
            copy.Uprising.SuppressionRegiments.Clear();
            copy.Uprising.IncidentIntelligence.Clear();

            Assert.AreEqual(before, SerializationHelper.Serialize(source));
        }

        [Test]
        public void Serialize_ConfiguredIncidentIntelligence_PreservesCombinedCategories()
        {
            GameConfig config = TestConfig.Create();
            config.Uprising.IncidentIntelligence.Clear();
            config.Uprising.IncidentIntelligence.Add(
                PlanetIntelligenceCategory.Officers | PlanetIntelligenceCategory.SpecialForces
            );

            GameConfig loaded = SerializationHelper.Deserialize<GameConfig>(
                SerializationHelper.Serialize(config)
            );

            CollectionAssert.AreEqual(
                config.Uprising.IncidentIntelligence,
                loaded.Uprising.IncidentIntelligence
            );
        }

        [Test]
        public void Serialize_ConfiguredSuppressionRegiments_PreservesMappings()
        {
            GameConfig config = TestConfig.Create();
            config.Uprising.SuppressionRegiments.Clear();
            config.Uprising.SuppressionRegiments.Add("guard", "owner");
            string xml = SerializationHelper.Serialize(config);

            Assert.AreEqual(
                "owner",
                SerializationHelper.Deserialize<GameConfig>(xml).Uprising.SuppressionRegiments[
                    "guard"
                ]
            );
        }

        [Test]
        public void CreateCopy_SpaceCombatMetadata_DetachesNestedCollections()
        {
            GameConfig.SpaceCombatConfig source = new GameConfig.SpaceCombatConfig
            {
                HyperdriveSystemCounts = new Dictionary<string, int> { ["cruiser"] = 2 },
            };
            GameConfig.SpaceCombatConfig copy = source.CreateCopy();

            CollectionAssert.AreEquivalent(
                source.HyperdriveSystemCounts,
                copy.HyperdriveSystemCounts
            );
            copy.HyperdriveSystemCounts["cruiser"] = 1;

            Assert.AreEqual(2, source.HyperdriveSystemCounts["cruiser"]);
        }

        [Test]
        public void Serialize_SpaceCombatMetadata_PreservesSystemCounts()
        {
            GameConfig config = new GameConfig();
            config.Combat.SpaceCombat.HyperdriveSystemCounts.Add("immobile", 0);
            config.Combat.SpaceCombat.HyperdriveSystemCounts.Add("cruiser", 3);

            GameConfig loaded = SerializationHelper.Deserialize<GameConfig>(
                SerializationHelper.Serialize(config)
            );

            CollectionAssert.AreEquivalent(
                config.Combat.SpaceCombat.HyperdriveSystemCounts,
                loaded.Combat.SpaceCombat.HyperdriveSystemCounts
            );
        }

        [Test]
        public void Deserialize_OmittedSpaceCombatMetadata_UsesEmptyCollections()
        {
            GameConfig config = SerializationHelper.Deserialize<GameConfig>(
                "<GameConfig><Combat><SpaceCombat/></Combat></GameConfig>"
            );

            CollectionAssert.IsEmpty(config.Combat.SpaceCombat.HyperdriveSystemCounts);
        }

        [Test]
        public void CreateCopy_OmittedRules_PreservesNullSettings()
        {
            GameConfig source = new GameConfig
            {
                AI = null,
                DifficultyModifiers = null,
                Movement = null,
            };

            GameConfig copy = source.CreateCopy();

            Assert.AreEqual(
                SerializationHelper.Serialize(source),
                SerializationHelper.Serialize(copy)
            );
        }

        [Test]
        public void RecoveryConfig_LegacyRepairAmountElements_AcceptsWithoutWritingThem()
        {
            const string xml =
                "<GameConfig><Recovery><FastRepairAmount>2</FastRepairAmount>"
                + "<NormalRepairAmount>1</NormalRepairAmount></Recovery></GameConfig>";

            GameConfig config = SerializationHelper.Deserialize<GameConfig>(xml);
            string serialized = SerializationHelper.Serialize(config);

            Assert.AreEqual(0, config.Recovery.FastRepairIntervalTicks);
            Assert.AreEqual(0, config.Recovery.NormalRepairIntervalTicks);
            StringAssert.DoesNotContain("FastRepairAmount", serialized);
            StringAssert.DoesNotContain("NormalRepairAmount", serialized);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4096)]
        public void Deserialize_LegacyCombatIterationLimit_AcceptsWithoutWritingIt(int limit)
        {
            string xml =
                "<GameConfig><Combat><SpaceCombat>"
                + $"<AutoResolveMaximumIterations>{limit}</AutoResolveMaximumIterations>"
                + "<AutoResolveStagnationIterations>5</AutoResolveStagnationIterations>"
                + "</SpaceCombat></Combat></GameConfig>";

            GameConfig config = SerializationHelper.Deserialize<GameConfig>(xml);

            Assert.AreEqual(5, config.Combat.SpaceCombat.AutoResolveStagnationIterations);
            StringAssert.DoesNotContain(
                "AutoResolveMaximumIterations",
                SerializationHelper.Serialize(config)
            );
        }

        [TestCase("FighterIonCannonFighterDamageMultiplier")]
        [TestCase("AutoResolveTargetScanDivisor")]
        [TestCase("AutoResolveStartingDistance")]
        [TestCase("AutoResolveWithdrawalDistance")]
        [TestCase("AutoResolveComponentDamageInterval")]
        [TestCase("AutoResolveComponentDamageRollMaximum")]
        [TestCase("AutoResolveComponentDelayMinimum")]
        [TestCase("AutoResolveComponentDelayMaximum")]
        [TestCase("AutoResolveComponentDelayRecovery")]
        public void Deserialize_LegacyVolleySetting_AcceptsWithoutWritingIt(string setting)
        {
            string xml =
                "<GameConfig><Combat><SpaceCombat>"
                + $"<{setting}>3</{setting}>"
                + "<AutoResolveStagnationIterations>5</AutoResolveStagnationIterations>"
                + "</SpaceCombat></Combat></GameConfig>";

            GameConfig config = SerializationHelper.Deserialize<GameConfig>(xml);

            Assert.AreEqual(5, config.Combat.SpaceCombat.AutoResolveStagnationIterations);
            StringAssert.DoesNotContain(setting, SerializationHelper.Serialize(config));
        }

        [TestCase(0, ForceRankLabel.None)]
        [TestCase(9, ForceRankLabel.None)]
        [TestCase(10, ForceRankLabel.Novice)]
        [TestCase(19, ForceRankLabel.Novice)]
        [TestCase(20, ForceRankLabel.Trainee)]
        [TestCase(79, ForceRankLabel.Trainee)]
        [TestCase(80, ForceRankLabel.ForceStudent)]
        [TestCase(99, ForceRankLabel.ForceStudent)]
        [TestCase(100, ForceRankLabel.ForceKnight)]
        [TestCase(119, ForceRankLabel.ForceKnight)]
        [TestCase(120, ForceRankLabel.ForceMaster)]
        [TestCase(-1, ForceRankLabel.ForceMaster)]
        [TestCase(-32768, ForceRankLabel.ForceMaster)]
        [TestCase(65536, ForceRankLabel.None)]
        public void GetRankLabel_StoredRatingBoundary_UsesDisplayRepresentation(
            int forceRank,
            ForceRankLabel expected
        )
        {
            Assert.AreEqual(expected, TestContent.Data.GameConfig.Jedi.GetRankLabel(forceRank));
        }
    }
}
