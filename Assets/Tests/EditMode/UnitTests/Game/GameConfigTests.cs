using NUnit.Framework;
using Rebellion.Game;
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

            Assert.AreEqual(before, SerializationHelper.Serialize(source));
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
