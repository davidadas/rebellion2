using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.Tests.Simulation
{
    [TestFixture]
    public class OfficerLoyaltyCommandsTests
    {
        [Test]
        public void ApplyGlobalShift_AllRegisteredOfficers_UsesFactionRelativeSign()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer favoredOfficer,
                canBetray: true,
                loyalty: 50
            );
            Faction favoredFaction = game.GetFactions().Single();
            Faction opposingFaction = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(opposingFaction);
            Officer opposingOfficer = EntityFactory.CreateOfficer(
                "opposing",
                opposingFaction.InstanceID,
                canBetray: true,
                loyalty: 50
            );
            Officer fixedOfficer = EntityFactory.CreateOfficer(
                "fixed",
                favoredFaction.InstanceID,
                canBetray: false,
                loyalty: 50
            );
            Planet opposingPlanet = new Planet
            {
                InstanceID = "alliance-planet",
                OwnerInstanceID = opposingFaction.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(opposingPlanet, planet.GetParent());
            game.AttachNode(opposingOfficer, opposingPlanet);
            game.AttachNode(fixedOfficer, planet);
            opposingOfficer.IsEnabled = false;

            new OfficerLoyaltyCommands(game).ApplyGlobalShift(favoredFaction, 7);

            Assert.AreEqual(57, favoredOfficer.Loyalty);
            Assert.AreEqual(43, opposingOfficer.Loyalty);
            Assert.AreEqual(50, fixedOfficer.Loyalty);
        }

        /// <summary>
        /// Builds an officer-loyalty test scene.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <param name="officer">Receives the officer.</param>
        /// <param name="canBetray">Whether the officer's loyalty can change.</param>
        /// <param name="loyalty">The officer's starting loyalty.</param>
        /// <returns>The constructed scene.</returns>
        private static GameRoot BuildScene(
            out Planet planet,
            out Officer officer,
            bool canBetray = false,
            int loyalty = 100
        )
        {
            GameConfig config = new GameConfig();
            GameRoot game = TestGame.Create(config);
            game.GetFactions().Add(new Faction { InstanceID = "empire" });
            PlanetSector sector = new PlanetSector { InstanceID = "sector" };
            game.AttachNode(sector, game.Galaxy);
            planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(planet, sector);
            officer = EntityFactory.CreateOfficer("officer", "empire", canBetray, loyalty);
            game.AttachNode(officer, planet);
            return game;
        }
    }
}
