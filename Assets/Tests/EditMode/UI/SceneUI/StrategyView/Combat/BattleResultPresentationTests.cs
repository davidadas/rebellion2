using NUnit.Framework;
using Rebellion.Game.Results;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Combat
{
    [TestFixture]
    public class BattleResultPresentationTests
    {
        private const string _attackerId = "attacker";
        private const string _defenderId = "defender";

        [Test]
        public void GetSideForOwner_ResultOwnerIDs_ReturnsRepresentedSide()
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerOwnerInstanceID = _attackerId,
                DefenderOwnerInstanceID = _defenderId,
            };

            CombatSide? attacker = BattleResultPresentation.GetSideForOwner(result, _attackerId);
            CombatSide? defender = BattleResultPresentation.GetSideForOwner(result, _defenderId);
            CombatSide? unknown = BattleResultPresentation.GetSideForOwner(result, "unknown");

            Assert.AreEqual(CombatSide.Attacker, attacker);
            Assert.AreEqual(CombatSide.Defender, defender);
            Assert.IsNull(unknown);
        }

        [TestCase(CombatSide.Attacker, SpaceCombatSideOutcome.Withdrawn)]
        [TestCase(CombatSide.Defender, SpaceCombatSideOutcome.Destroyed)]
        [TestCase(CombatSide.Draw, SpaceCombatSideOutcome.Unknown)]
        public void GetOutcome_CombatSide_ReturnsConfiguredOutcome(
            CombatSide side,
            SpaceCombatSideOutcome expected
        )
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerOutcome = SpaceCombatSideOutcome.Withdrawn,
                DefenderOutcome = SpaceCombatSideOutcome.Destroyed,
            };

            SpaceCombatSideOutcome outcome = BattleResultPresentation.GetOutcome(result, side);

            Assert.AreEqual(expected, outcome);
        }

        [TestCase(CombatSide.Attacker, CombatSide.Defender)]
        [TestCase(CombatSide.Defender, CombatSide.Attacker)]
        public void GetOpposingSide_CombatantSide_ReturnsOtherSide(
            CombatSide side,
            CombatSide expected
        )
        {
            CombatSide? opposingSide = BattleResultPresentation.GetOpposingSide(side);

            Assert.AreEqual(expected, opposingSide);
        }

        [Test]
        public void GetOpposingSide_Draw_ReturnsNull()
        {
            CombatSide? opposingSide = BattleResultPresentation.GetOpposingSide(CombatSide.Draw);

            Assert.IsNull(opposingSide);
        }

        [Test]
        public void GetSummaryImagePath_DestroyedDefender_ReturnsAttackerVictoryArtwork()
        {
            BattleAlertWindowTheme theme = CreateTheme();
            SpaceCombatResult result = CreateResult(
                CombatSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Destroyed
            );

            string path = BattleResultPresentation.GetSummaryImagePath(theme, result);

            Assert.AreEqual("first-victory", path);
        }

        [Test]
        public void GetSummaryImagePath_WithdrawnDefender_ReturnsDefenderDefeatArtwork()
        {
            BattleAlertWindowTheme theme = CreateTheme();
            SpaceCombatResult result = CreateResult(
                CombatSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Withdrawn
            );

            string path = BattleResultPresentation.GetSummaryImagePath(theme, result);

            Assert.AreEqual("second-defeat", path);
        }

        [Test]
        public void GetSummaryImagePath_Draw_ReturnsDefaultSummaryArtwork()
        {
            BattleAlertWindowTheme theme = CreateTheme();
            SpaceCombatResult result = CreateResult(
                CombatSide.Draw,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Active
            );

            string path = BattleResultPresentation.GetSummaryImagePath(theme, result);

            Assert.AreEqual("summary", path);
        }

        [Test]
        public void GetSummaryImagePath_MissingPreferredArtwork_UsesOrderedFallback()
        {
            BattleAlertWindowTheme theme = CreateTheme();
            theme.SecondForcesDefeatedImagePath = null;
            SpaceCombatResult result = CreateResult(
                CombatSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Withdrawn
            );

            string path = BattleResultPresentation.GetSummaryImagePath(theme, result);

            Assert.AreEqual("first-victory", path);
        }

        [Test]
        public void FirstNonBlank_BlankCandidates_ReturnsFirstMeaningfulValue()
        {
            string value = BattleResultPresentation.FirstNonBlank(
                null,
                string.Empty,
                "  ",
                "value"
            );

            Assert.AreEqual("value", value);
        }

        private static BattleAlertWindowTheme CreateTheme()
        {
            return new BattleAlertWindowTheme
            {
                FirstForcesOwnerInstanceID = _attackerId,
                SecondForcesOwnerInstanceID = _defenderId,
                FirstForcesVictoriousImagePath = "first-victory",
                FirstForcesDefeatedImagePath = "first-defeat",
                SecondForcesVictoriousImagePath = "second-victory",
                SecondForcesDefeatedImagePath = "second-defeat",
                ResultSummaryImagePath = "summary",
            };
        }

        private static SpaceCombatResult CreateResult(
            CombatSide winner,
            SpaceCombatSideOutcome attackerOutcome,
            SpaceCombatSideOutcome defenderOutcome
        )
        {
            return new SpaceCombatResult
            {
                AttackerOwnerInstanceID = _attackerId,
                DefenderOwnerInstanceID = _defenderId,
                Winner = winner,
                AttackerOutcome = attackerOutcome,
                DefenderOutcome = defenderOutcome,
            };
        }
    }
}
