using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.FogOfWar;
using Rebellion.Game.SpaceCombat;
using Rebellion.Game.Units;

namespace Rebellion.Tests.Game
{
    [TestFixture]
    public class GameConfigTests
    {
        [TestCase(SpaceCombatSupportContext.FirstFactionEasy, 400, 200)]
        [TestCase(SpaceCombatSupportContext.FirstFactionMedium, 400, 150)]
        [TestCase(SpaceCombatSupportContext.FirstFactionHard, 400, 100)]
        [TestCase(SpaceCombatSupportContext.SecondFactionEasy, 200, 400)]
        [TestCase(SpaceCombatSupportContext.SecondFactionMedium, 150, 400)]
        [TestCase(SpaceCombatSupportContext.SecondFactionHard, 100, 400)]
        [TestCase(SpaceCombatSupportContext.Multiplayer, 400, 400)]
        public void BattleSupport_AuthoredContext_RetainsCompleteSideParameters(
            SpaceCombatSupportContext context,
            int firstGapScale,
            int secondGapScale
        )
        {
            SpaceCombatSupportConfig config = TestContent
                .Data
                .GameConfig
                .SupportShift
                .BattleSupport;
            SpaceCombatSupportProfile profile = config.Profiles[context];

            Assert.AreEqual(7, config.Profiles.Count);
            Assert.AreEqual(10, profile.Divisor);
            Assert.AreEqual(firstGapScale, profile.FirstSide.LeadingGapScale);
            Assert.AreEqual(secondGapScale, profile.SecondSide.LeadingGapScale);
            foreach (
                SpaceCombatSupportSideConfig side in new[] { profile.FirstSide, profile.SecondSide }
            )
            {
                Assert.AreEqual(100, side.LeadingFlat);
                Assert.AreEqual(0, side.TrailingFlat);
                Assert.AreEqual(100, side.TrailingGapScale);
                Assert.AreEqual(20, side.MagnitudeCap);
                Assert.AreEqual(101u, side.SupportThreshold);
            }
        }

        [Test]
        public void CreateCopy_EditingBattleSupportProfile_DoesNotChangeSource()
        {
            GameConfig source = TestContent.Data.GameConfig.CreateCopy();
            string before = SerializationHelper.Serialize(source);

            GameConfig copy = source.CreateCopy();
            copy.SupportShift
                .BattleSupport
                .Profiles[SpaceCombatSupportContext.FirstFactionEasy]
                .FirstSide
                .LeadingGapScale = 999;

            Assert.AreEqual(before, SerializationHelper.Serialize(source));
        }

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
