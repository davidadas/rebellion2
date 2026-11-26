using System;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
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

        [TestCase(CombatSide.Attacker, SpaceCombatResult.SideOutcome.Withdrawn)]
        [TestCase(CombatSide.Defender, SpaceCombatResult.SideOutcome.Destroyed)]
        [TestCase(null, SpaceCombatResult.SideOutcome.Unknown)]
        public void GetOutcome_CombatSide_ReturnsConfiguredOutcome(
            CombatSide? side,
            SpaceCombatResult.SideOutcome expected
        )
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                AttackerOutcome = SpaceCombatResult.SideOutcome.Withdrawn,
                DefenderOutcome = SpaceCombatResult.SideOutcome.Destroyed,
            };

            SpaceCombatResult.SideOutcome outcome = result.GetOutcome(side);

            Assert.AreEqual(expected, outcome);
        }

        [TestCase(CombatSide.Attacker, CombatSide.Defender)]
        [TestCase(CombatSide.Defender, CombatSide.Attacker)]
        public void GetOpposingSide_CombatantSide_ReturnsOtherSide(
            CombatSide side,
            CombatSide expected
        )
        {
            CombatSide? opposingSide = SpaceCombatResult.OpposingSide(side);

            Assert.AreEqual(expected, opposingSide);
        }

        [Test]
        public void GetOpposingSide_Draw_ReturnsNull()
        {
            CombatSide? opposingSide = SpaceCombatResult.OpposingSide(null);

            Assert.IsNull(opposingSide);
        }

        [Test]
        public void GetFleetPlanetInstanceID_WithdrawnLiveFleet_ReturnsRetreatPlanet()
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                Planet = new Planet { InstanceID = "battle-planet" },
                AttackerOwnerInstanceID = _attackerId,
                DefenderOwnerInstanceID = _defenderId,
                AttackerOutcome = SpaceCombatResult.SideOutcome.Withdrawn,
                AttackerRetreatPlanetInstanceID = "retreat-planet",
            };

            string planetInstanceId = BattleResultPresentation
                .Create(result)
                .GetFleetPlanetInstanceID(_attackerId);

            Assert.AreEqual("retreat-planet", planetInstanceId);
        }

        [Test]
        public void GetFleetPlanetInstanceID_WithdrawnSavedFleet_ReturnsRetreatPlanet()
        {
            CombatReport report = new CombatReport
            {
                CombatType = CombatReportType.SpaceBattle,
                PerspectiveOwnerInstanceID = _defenderId,
                PlanetInstanceID = "battle-planet",
                AttackerOwnerInstanceID = _attackerId,
                DefenderOwnerInstanceID = _defenderId,
                DefenderOutcome = SpaceCombatResult.SideOutcome.Withdrawn,
                DefenderRetreatPlanetInstanceID = "retreat-planet",
            };

            string planetInstanceId = BattleResultPresentation
                .Create(report)
                .GetFleetPlanetInstanceID(_defenderId);

            Assert.AreEqual("retreat-planet", planetInstanceId);
        }

        [Test]
        public void GetFleetPlanetInstanceID_DestroyedFleet_ReturnsNull()
        {
            SpaceCombatResult result = new SpaceCombatResult
            {
                Planet = new Planet { InstanceID = "battle-planet" },
                AttackerOwnerInstanceID = _attackerId,
                DefenderOwnerInstanceID = _defenderId,
                AttackerOutcome = SpaceCombatResult.SideOutcome.Destroyed,
            };

            string planetInstanceId = BattleResultPresentation
                .Create(result)
                .GetFleetPlanetInstanceID(_attackerId);

            Assert.IsNull(planetInstanceId);
        }

        [Test]
        public void GetSummaryImagePath_DestroyedDefender_ReturnsAttackerVictoryArtwork()
        {
            BattleAlertWindowTheme theme = CreateTheme();
            SpaceCombatResult result = CreateResult(
                SpaceCombatResult.BattleOutcome.Attacker,
                SpaceCombatResult.SideOutcome.Active,
                SpaceCombatResult.SideOutcome.Destroyed
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
                SpaceCombatResult.BattleOutcome.Attacker,
                SpaceCombatResult.SideOutcome.Active,
                SpaceCombatResult.SideOutcome.Withdrawn
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
                SpaceCombatResult.BattleOutcome.Draw,
                SpaceCombatResult.SideOutcome.Active,
                SpaceCombatResult.SideOutcome.Active
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
                SpaceCombatResult.BattleOutcome.Attacker,
                SpaceCombatResult.SideOutcome.Active,
                SpaceCombatResult.SideOutcome.Withdrawn
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

        /// <summary>
        /// Creates theme.
        /// </summary>
        /// <returns>The created theme.</returns>
        private static BattleAlertWindowTheme CreateTheme()
        {
            return new BattleAlertWindowTheme { ResultSummaryImagePath = "summary" };
        }

        /// <summary>
        /// Creates context.
        /// </summary>
        /// <returns>The created context.</returns>
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
        /// Creates result.
        /// </summary>
        /// <param name="winner">The winner.</param>
        /// <param name="attackerOutcome">The attacker outcome.</param>
        /// <param name="defenderOutcome">The defender outcome.</param>
        /// <returns>The created result.</returns>
        private static SpaceCombatResult CreateResult(
            SpaceCombatResult.BattleOutcome winner,
            SpaceCombatResult.SideOutcome attackerOutcome,
            SpaceCombatResult.SideOutcome defenderOutcome
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
