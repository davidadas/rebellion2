using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;

namespace Rebellion.Tests.UI.SceneUI.StrategyView.Combat
{
    [TestFixture]
    public class BattleResultPresentationTests
    {
        private const string _attackerId = "attacker";
        private const string _defenderId = "defender";

        [Test]
        public void GetSummaryImagePath_DestroyedDefender_ReturnsAttackerVictoryArtwork()
        {
            BattleResultPresentation presentation = CreatePresentation(
                CombatSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Destroyed
            );

            string path = presentation.GetSummaryImagePath(CreateContext(), CreateTheme());

            Assert.AreEqual("attacker-victory", path);
        }

        [Test]
        public void GetSummaryImagePath_WithdrawnDefender_ReturnsDefenderDefeatArtwork()
        {
            BattleResultPresentation presentation = CreatePresentation(
                CombatSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Withdrawn
            );

            string path = presentation.GetSummaryImagePath(CreateContext(), CreateTheme());

            Assert.AreEqual("defender-defeat", path);
        }

        [Test]
        public void GetSummaryImagePath_Draw_ReturnsDefaultSummaryArtwork()
        {
            BattleResultPresentation presentation = CreatePresentation(
                CombatSide.Draw,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Active
            );

            string path = presentation.GetSummaryImagePath(CreateContext(), CreateTheme());

            Assert.AreEqual("summary", path);
        }

        [Test]
        public void GetSummaryImagePath_MissingPreferredArtwork_UsesOrderedFallback()
        {
            UIContext context = CreateContext();
            context.GetTheme(_defenderId).BattleParticipant.DefeatedImagePath = null;
            BattleResultPresentation presentation = CreatePresentation(
                CombatSide.Attacker,
                SpaceCombatSideOutcome.Active,
                SpaceCombatSideOutcome.Withdrawn
            );

            string path = presentation.GetSummaryImagePath(context, CreateTheme());

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

        /// <summary>
        /// Creates a battle-result theme.
        /// </summary>
        /// <returns>The configured theme.</returns>
        private static BattleAlertWindowTheme CreateTheme()
        {
            return new BattleAlertWindowTheme { ResultSummaryImagePath = "summary" };
        }

        /// <summary>
        /// Creates a UI context with participant artwork.
        /// </summary>
        /// <returns>The configured UI context.</returns>
        private static UIContext CreateContext()
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _attackerId });
            game.GetFactions().Add(new Faction { InstanceID = _defenderId });
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

        /// <summary>
        /// Creates a detached space-combat presentation.
        /// </summary>
        /// <param name="winner">The winning side.</param>
        /// <param name="attackerOutcome">The attacker outcome.</param>
        /// <param name="defenderOutcome">The defender outcome.</param>
        /// <returns>The report presentation.</returns>
        private static BattleResultPresentation CreatePresentation(
            CombatSide winner,
            SpaceCombatSideOutcome attackerOutcome,
            SpaceCombatSideOutcome defenderOutcome
        )
        {
            return BattleResultPresentation.Create(
                new CombatReport
                {
                    CombatType = CombatReportType.SpaceBattle,
                    AttackerOwnerInstanceID = _attackerId,
                    DefenderOwnerInstanceID = _defenderId,
                    Winner = winner,
                    AttackerOutcome = attackerOutcome,
                    DefenderOutcome = defenderOutcome,
                }
            );
        }
    }
}
