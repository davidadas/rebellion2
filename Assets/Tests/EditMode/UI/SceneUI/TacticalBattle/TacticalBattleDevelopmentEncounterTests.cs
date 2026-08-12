using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game.Units;

namespace Rebellion.Tests.UI.SceneUI.TacticalBattle
{
    [TestFixture]
    public sealed class TacticalBattleDevelopmentEncounterTests
    {
        [Test]
        public void Create_ConfiguredFactions_AddsEveryEligibleCapitalShip()
        {
            GameDataCatalog gameData = TestContent.Data;
            List<FactionTheme> themes = GetPreviewThemes(gameData);

            TacticalBattleDevelopmentEncounterResult result =
                TacticalBattleDevelopmentEncounter.Create(gameData);

            Assert.That(
                result.Encounter.AttackerFleet.CapitalShips.Select(ship => ship.TypeID),
                Is.EquivalentTo(GetEligibleCapitalShipIds(gameData, themes[0].FactionInstanceID))
            );
            Assert.That(
                result.Encounter.DefenderFleet.CapitalShips.Select(ship => ship.TypeID),
                Is.EquivalentTo(GetEligibleCapitalShipIds(gameData, themes[1].FactionInstanceID))
            );
        }

        [Test]
        public void Create_ConfiguredFactions_AddsEveryEligibleStarfighter()
        {
            GameDataCatalog gameData = TestContent.Data;
            List<FactionTheme> themes = GetPreviewThemes(gameData);

            TacticalBattleDevelopmentEncounterResult result =
                TacticalBattleDevelopmentEncounter.Create(gameData);

            Assert.That(
                GetFighterIds(result, themes[0].FactionInstanceID),
                Is.EquivalentTo(GetEligibleStarfighterIds(gameData, themes[0].FactionInstanceID))
            );
            Assert.That(
                GetFighterIds(result, themes[1].FactionInstanceID),
                Is.EquivalentTo(GetEligibleStarfighterIds(gameData, themes[1].FactionInstanceID))
            );
        }

        [Test]
        public void Create_ConfiguredFactions_ControlsFirstFaction()
        {
            GameDataCatalog gameData = TestContent.Data;
            string expectedFactionId = GetPreviewThemes(gameData)[0].FactionInstanceID;

            TacticalBattleDevelopmentEncounterResult result =
                TacticalBattleDevelopmentEncounter.Create(gameData);

            Assert.That(result.PlayerFactionInstanceID, Is.EqualTo(expectedFactionId));
        }

        /// <summary>
        /// Gets the two faction themes used to build the development encounter.
        /// </summary>
        /// <param name="gameData">The active game data.</param>
        /// <returns>The source-ordered preview themes.</returns>
        private static List<FactionTheme> GetPreviewThemes(GameDataCatalog gameData)
        {
            return new FactionThemeLibrary(gameData.FactionThemes)
                .GetAllThemes()
                .Where(theme => theme.TacticalBattle != null)
                .Take(2)
                .ToList();
        }

        /// <summary>
        /// Gets the capital-ship types eligible for one faction.
        /// </summary>
        /// <param name="gameData">The active game data.</param>
        /// <param name="factionId">The faction to inspect.</param>
        /// <returns>The eligible capital-ship type identifiers.</returns>
        private static IEnumerable<string> GetEligibleCapitalShipIds(
            GameDataCatalog gameData,
            string factionId
        )
        {
            return gameData
                .CapitalShips.Where(ship => ship.HasAllowedOwnerInstanceID(factionId))
                .Select(ship => ship.TypeID);
        }

        /// <summary>
        /// Gets the starfighter types eligible for one faction.
        /// </summary>
        /// <param name="gameData">The active game data.</param>
        /// <param name="factionId">The faction to inspect.</param>
        /// <returns>The eligible starfighter type identifiers.</returns>
        private static IEnumerable<string> GetEligibleStarfighterIds(
            GameDataCatalog gameData,
            string factionId
        )
        {
            return gameData
                .Starfighters.Where(fighter => fighter.HasAllowedOwnerInstanceID(factionId))
                .Select(fighter => fighter.TypeID);
        }

        /// <summary>
        /// Gets the preview planet's starfighter types owned by one faction.
        /// </summary>
        /// <param name="result">The generated development encounter.</param>
        /// <param name="factionId">The faction to inspect.</param>
        /// <returns>The generated starfighter type identifiers.</returns>
        private static IEnumerable<string> GetFighterIds(
            TacticalBattleDevelopmentEncounterResult result,
            string factionId
        )
        {
            return result
                .Encounter.Planet.Starfighters.Where(fighter =>
                    fighter.GetOwnerInstanceID() == factionId
                )
                .Select(fighter => fighter.TypeID);
        }
    }
}
