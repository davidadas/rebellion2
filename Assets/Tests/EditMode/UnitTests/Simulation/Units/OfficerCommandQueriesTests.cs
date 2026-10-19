using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class OfficerCommandQueriesTests
    {
        private const string _ownerId = "alliance";

        [Test]
        public void CanChangeCommand_AdmiralAtPlanet_ReturnsFalse()
        {
            GameRoot game = BuildScene(out Planet planet);
            Officer officer = CreateOfficer(game, planet, OfficerRank.Admiral);

            bool allowed = OfficerCommandQueries.CanChangeCommand(officer, OfficerRank.Admiral);

            Assert.IsFalse(allowed);
        }

        [TestCase(true, false, false, 0)]
        [TestCase(false, true, false, 0)]
        [TestCase(false, false, true, 0)]
        [TestCase(false, false, false, 1)]
        public void CanChangeCommand_UnavailableOfficer_ReturnsFalse(
            bool captured,
            bool killed,
            bool retired,
            int injuryPoints
        )
        {
            GameRoot game = BuildScene(out Planet planet);
            Officer officer = CreateOfficer(game, planet, OfficerRank.Commander);
            officer.IsCaptured = captured;
            officer.IsKilled = killed;
            officer.IsRetired = retired;
            officer.InjuryPoints = injuryPoints;

            bool allowed = OfficerCommandQueries.CanChangeCommand(officer, OfficerRank.Commander);

            Assert.IsFalse(allowed);
        }

        /// <summary>
        /// Builds a minimal deployed game graph.
        /// </summary>
        /// <param name="planet">Receives the command planet.</param>
        /// <returns>The game graph.</returns>
        private static GameRoot BuildScene(out Planet planet)
        {
            GameRoot game = TestGame.Create(TestConfig.Create());
            game.GetFactions().Add(new Faction { InstanceID = _ownerId });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = _ownerId,
                IsColonized = true,
            };
            game.AttachNode(sector, game.Galaxy);
            game.AttachNode(planet, sector);
            return game;
        }

        /// <summary>
        /// Creates and deploys an officer with one allowed command post.
        /// </summary>
        /// <param name="game">The game graph.</param>
        /// <param name="planet">The deployment planet.</param>
        /// <param name="allowedRank">The allowed command post.</param>
        /// <returns>The deployed officer.</returns>
        private static Officer CreateOfficer(GameRoot game, Planet planet, OfficerRank allowedRank)
        {
            Officer officer = EntityFactory.CreateOfficer("candidate", _ownerId);
            officer.AllowedRanks = new[] { allowedRank };
            game.AttachNode(officer, planet);
            return officer;
        }
    }
}
