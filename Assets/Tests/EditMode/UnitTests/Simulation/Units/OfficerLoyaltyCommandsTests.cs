using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
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

            new OfficerLoyaltyCommands(game, new ThrowingRNG()).ApplyGlobalShift(favoredFaction, 7);

            Assert.AreEqual(57, favoredOfficer.Loyalty);
            Assert.AreEqual(43, opposingOfficer.Loyalty);
            Assert.AreEqual(50, fixedOfficer.Loyalty);
        }

        [Test]
        public void TryResolveMissionBetrayal_LowLoyaltyOfficer_FoilsWithoutRevealingIdentity()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer officer,
                canBetray: true,
                loyalty: 0
            );
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new OfficerLoyaltyCommands(
                game,
                new StubRNG()
            ).TryResolveMissionBetrayal(mission, out List<GameResult> results);

            Assert.IsTrue(betrayed);
            Assert.IsEmpty(results);
        }

        [Test]
        public void TryResolveMissionBetrayal_ForceCapableCompanion_DoesNotRevealTraitor()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer traitor,
                canBetray: true,
                loyalty: 0
            );
            Officer discoverer = new Officer
            {
                InstanceID = "discoverer",
                OwnerInstanceID = traitor.OwnerInstanceID,
                ForceValue = 100,
            };
            game.AttachNode(discoverer, planet);
            StubMission mission = CreateMission(game, planet, traitor);
            mission.AddChild(discoverer);

            bool betrayed = new OfficerLoyaltyCommands(
                game,
                new StubRNG()
            ).TryResolveMissionBetrayal(mission, out List<GameResult> results);

            Assert.IsTrue(betrayed);
            Assert.IsEmpty(results);
        }

        [TestCase(80, 19, true)]
        [TestCase(80, 20, false)]
        public void TryResolveMissionBetrayal_BoundaryRoll_UsesOneHundredMinusLoyalty(
            int loyalty,
            int roll,
            bool expectedBetrayal
        )
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer officer,
                canBetray: true,
                loyalty: loyalty
            );
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new OfficerLoyaltyCommands(
                game,
                new SequenceRNG(new[] { roll })
            ).TryResolveMissionBetrayal(mission, out _);

            Assert.AreEqual(expectedBetrayal, betrayed);
        }

        [Test]
        public void TryResolveMissionBetrayal_CommandOfficer_CanBetray()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer officer,
                canBetray: true,
                loyalty: 0
            );
            officer.CurrentRank = OfficerRank.Admiral;
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new OfficerLoyaltyCommands(
                game,
                new StubRNG()
            ).TryResolveMissionBetrayal(mission, out _);

            Assert.IsTrue(betrayed);
        }

        /// <summary>
        /// Builds scene.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <param name="officer">Receives the officer.</param>
        /// <param name="canBetray">Whether the officer's loyalty can change and permit betrayal.</param>
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

        /// <summary>
        /// Creates mission.
        /// </summary>
        /// <param name="game">The game.</param>
        /// <param name="planet">The planet.</param>
        /// <param name="officer">The officer.</param>
        /// <returns>The created mission.</returns>
        private static StubMission CreateMission(GameRoot game, Planet planet, Officer officer)
        {
            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.AddChild(officer);
            return mission;
        }
    }
}
