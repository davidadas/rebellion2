using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
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

            string path = BattleResultPresentation.GetSummaryImagePath(
                CreateContext(),
                theme,
                result
            );

            Assert.AreEqual("attacker-victory", path);
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

            string path = BattleResultPresentation.GetSummaryImagePath(
                CreateContext(),
                theme,
                result
            );

            Assert.AreEqual("defender-defeat", path);
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

            string path = BattleResultPresentation.GetSummaryImagePath(
                CreateContext(),
                theme,
                result
            );

            Assert.AreEqual("summary", path);
        }

        [Test]
        public void GetSummaryImagePath_MissingPreferredArtwork_UsesOrderedFallback()
        {
            BattleAlertWindowTheme theme = CreateTheme();
            UIContext context = CreateContext();
            context.GetTheme(_defenderId).BattleParticipant.DefeatedImagePath = null;
            SpaceCombatResult result = CreateResult(
                CombatSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Withdrawn
            );

            string path = BattleResultPresentation.GetSummaryImagePath(context, theme, result);

            Assert.AreEqual("attacker-victory", path);
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
            return new BattleAlertWindowTheme { ResultSummaryImagePath = "summary" };
        }

        private static UIContext CreateContext()
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = _attackerId });
            game.Factions.Add(new Faction { InstanceID = _defenderId });
            FactionThemes themes = new FactionThemes
            {
                new FactionTheme { FactionInstanceID = "DEFAULT" },
                new FactionTheme
                {
                    FactionInstanceID = _attackerId,
                    BattleParticipant = new BattleParticipantTheme
                    {
                        DefeatedImagePath = "attacker-defeat",
                        VictoriousImagePath = "attacker-victory",
                    },
                },
                new FactionTheme
                {
                    FactionInstanceID = _defenderId,
                    BattleParticipant = new BattleParticipantTheme
                    {
                        DefeatedImagePath = "defender-defeat",
                        VictoriousImagePath = "defender-victory",
                    },
                },
            };
            return new UIContext(
                game,
                new FactionThemeLibrary(themes),
                new EncyclopediaCatalog(Array.Empty<EncyclopediaEntry>()),
                _ => null
            );
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
