using NUnit.Framework;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using GameFleet = Rebellion.Game.Units.Fleet;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Combat
{
    [TestFixture]
    public class BattleResultPresentationTests
    {
        private const string _attackerId = "attacker";
        private const string _defenderId = "defender";

        [Test]
        public void GetFleetForOwner_ResultOwnerIds_ReturnsMatchingFleet()
        {
            GameFleet attacker = CreateFleet("attacking-fleet", "fleet-owner-a");
            GameFleet defender = CreateFleet("defending-fleet", "fleet-owner-b");
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = _attackerId,
                DefenderOwnerInstanceID = _defenderId,
            };

            GameFleet attackerResult = BattleResultPresentation.GetFleetForOwner(
                result,
                _attackerId
            );
            GameFleet defenderResult = BattleResultPresentation.GetFleetForOwner(
                result,
                _defenderId
            );

            Assert.AreSame(attacker, attackerResult);
            Assert.AreSame(defender, defenderResult);
        }

        [Test]
        public void GetFleetForOwner_MissingResultOwnerIds_UsesFleetOwners()
        {
            GameFleet attacker = CreateFleet("attacking-fleet", _attackerId);
            GameFleet defender = CreateFleet("defending-fleet", _defenderId);
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
            };

            GameFleet attackerResult = BattleResultPresentation.GetFleetForOwner(
                result,
                _attackerId
            );
            GameFleet defenderResult = BattleResultPresentation.GetFleetForOwner(
                result,
                _defenderId
            );

            Assert.AreSame(attacker, attackerResult);
            Assert.AreSame(defender, defenderResult);
        }

        [Test]
        public void GetFleetForOwner_UnknownOrInvalidOwner_ReturnsNull()
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = CreateFleet("attacking-fleet", _attackerId),
                DefenderFleet = CreateFleet("defending-fleet", _defenderId),
            };

            GameFleet unknown = BattleResultPresentation.GetFleetForOwner(result, "unknown");
            GameFleet missingOwner = BattleResultPresentation.GetFleetForOwner(result, null);
            GameFleet missingResult = BattleResultPresentation.GetFleetForOwner(null, _attackerId);

            Assert.IsNull(unknown);
            Assert.IsNull(missingOwner);
            Assert.IsNull(missingResult);
        }

        [Test]
        public void GetSideForOwner_ResultAndFleetOwners_ReturnsRepresentedSide()
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerFleet = CreateFleet("attacking-fleet", "fleet-attacker"),
                DefenderFleet = CreateFleet("defending-fleet", _defenderId),
                AttackerOwnerInstanceID = _attackerId,
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

        private static GameFleet CreateFleet(string instanceId, string ownerId)
        {
            return new GameFleet(ownerId, instanceId) { InstanceID = instanceId };
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
