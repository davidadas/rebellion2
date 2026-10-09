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
    public class MissionBetrayalResolverTests
    {
        [Test]
        public void TryResolve_LowLoyaltyOfficer_FoilsWithoutRevealingIdentity()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer officer,
                canBetray: true,
                loyalty: 0
            );
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new MissionBetrayalResolver(game, new StubRNG()).TryResolve(
                mission,
                out List<GameResult> results
            );

            Assert.IsTrue(betrayed);
            Assert.IsEmpty(results);
        }

        [Test]
        public void TryResolve_ForceCapableCompanion_RevealsTraitor()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer traitor,
                canBetray: true,
                loyalty: 0
            );
            Officer discoverer = EntityFactory.CreateOfficer(
                "discoverer",
                traitor.OwnerInstanceID,
                canBetray: true,
                loyalty: 100
            );
            discoverer.ForceValue = 100;
            game.AttachNode(discoverer, planet);
            StubMission mission = CreateMission(game, planet, traitor);
            mission.AddChild(discoverer);

            bool betrayed = new MissionBetrayalResolver(game, new StubRNG()).TryResolve(
                mission,
                out List<GameResult> results
            );

            TraitorDiscoveredResult result = results.OfType<TraitorDiscoveredResult>().Single();
            Assert.IsTrue(betrayed);
            Assert.AreSame(traitor, result.Officer);
            Assert.AreSame(discoverer, result.DiscoveredBy);
            Assert.AreSame(planet, result.Context);
        }

        [Test]
        public void TryResolve_MultipleTraitors_RevealsLastTraitor()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer firstTraitor,
                canBetray: true,
                loyalty: 0
            );
            Officer lastTraitor = EntityFactory.CreateOfficer(
                "last-traitor",
                firstTraitor.OwnerInstanceID,
                canBetray: true,
                loyalty: 0
            );
            Officer discoverer = EntityFactory.CreateOfficer(
                "discoverer",
                firstTraitor.OwnerInstanceID,
                canBetray: true,
                loyalty: 100
            );
            discoverer.ForceValue = 100;
            game.AttachNode(lastTraitor, planet);
            game.AttachNode(discoverer, planet);
            StubMission mission = CreateMission(game, planet, firstTraitor);
            mission.AddChild(lastTraitor);
            mission.AddChild(discoverer);

            bool betrayed = new MissionBetrayalResolver(
                game,
                new SequenceRNG(new[] { 0, 0, 99, 0 })
            ).TryResolve(mission, out List<GameResult> results);

            TraitorDiscoveredResult result = results.OfType<TraitorDiscoveredResult>().Single();
            Assert.IsTrue(betrayed);
            Assert.AreSame(lastTraitor, result.Officer);
            Assert.AreSame(discoverer, result.DiscoveredBy);
        }

        [TestCase(80, 19, true)]
        [TestCase(80, 20, false)]
        public void TryResolve_BoundaryRoll_UsesOneHundredMinusLoyalty(
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

            bool betrayed = new MissionBetrayalResolver(
                game,
                new SequenceRNG(new[] { roll })
            ).TryResolve(mission, out _);

            Assert.AreEqual(expectedBetrayal, betrayed);
        }

        [Test]
        public void TryResolve_CommandOfficer_CanBetray()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer officer,
                canBetray: true,
                loyalty: 0
            );
            officer.CurrentRank = OfficerRank.Admiral;
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new MissionBetrayalResolver(game, new StubRNG()).TryResolve(
                mission,
                out _
            );

            Assert.IsTrue(betrayed);
        }

        /// <summary>
        /// Builds a mission-betrayal test scene.
        /// </summary>
        /// <param name="planet">Receives the planet.</param>
        /// <param name="officer">Receives the officer.</param>
        /// <param name="canBetray">Whether the officer can betray a mission.</param>
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
        /// Creates a mission containing one officer.
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
