using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.Tests.Systems
{
    [TestFixture]
    public class MissionDefectionSystemTests
    {
        [Test]
        public void TryResolveDefection_LowLoyaltyOfficer_FoilsWithoutRevealingIdentity()
        {
            GameRoot game = BuildScene(out Planet planet, out Officer officer);
            officer.CanBetray = true;
            officer.Loyalty = 0;
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new MissionDefectionSystem(game).TryResolveDefection(
                mission,
                new StubRNG(),
                out List<GameResult> results
            );

            Assert.IsTrue(betrayed);
            Assert.IsEmpty(results);
            Assert.IsFalse(officer.IsTraitor);
        }

        [Test]
        public void TryResolveDefection_ForceCapableCompanion_DiscoversTraitor()
        {
            GameRoot game = BuildScene(out Planet planet, out Officer traitor);
            traitor.CanBetray = true;
            traitor.Loyalty = 0;
            Officer discoverer = new Officer
            {
                InstanceID = "discoverer",
                OwnerInstanceID = traitor.OwnerInstanceID,
                ForceValue = 100,
            };
            game.AttachNode(discoverer, planet);
            StubMission mission = CreateMission(game, planet, traitor);
            mission.MainParticipants.Add(discoverer);

            bool betrayed = new MissionDefectionSystem(game).TryResolveDefection(
                mission,
                new StubRNG(),
                out List<GameResult> results
            );

            TraitorDiscoveredResult result = results.OfType<TraitorDiscoveredResult>().Single();
            Assert.IsTrue(betrayed);
            Assert.IsTrue(traitor.IsTraitor);
            Assert.AreSame(traitor, result.Officer);
            Assert.AreSame(discoverer, result.DiscoveredBy);
            Assert.AreSame(planet, result.Context);
        }

        [TestCase(80, 19, true)]
        [TestCase(80, 20, false)]
        public void TryResolveDefection_BoundaryRoll_UsesOneHundredMinusLoyalty(
            int loyalty,
            int roll,
            bool expectedBetrayal
        )
        {
            GameRoot game = BuildScene(out Planet planet, out Officer officer);
            officer.CanBetray = true;
            officer.Loyalty = loyalty;
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new MissionDefectionSystem(game).TryResolveDefection(
                mission,
                new SequenceRNG(new[] { roll }),
                out _
            );

            Assert.AreEqual(expectedBetrayal, betrayed);
        }

        [Test]
        public void TryResolveDefection_CommandOfficer_DoesNotBetray()
        {
            GameRoot game = BuildScene(out Planet planet, out Officer officer);
            officer.CanBetray = true;
            officer.Loyalty = 0;
            officer.CurrentRank = OfficerRank.Admiral;
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new MissionDefectionSystem(game).TryResolveDefection(
                mission,
                new StubRNG(),
                out _
            );

            Assert.IsFalse(betrayed);
        }

        private static GameRoot BuildScene(out Planet planet, out Officer officer)
        {
            GameRoot game = new GameRoot(TestConfig.Create());
            game.Factions.Add(new Faction { InstanceID = "empire" });
            PlanetSystem system = new PlanetSystem { InstanceID = "system" };
            game.AttachNode(system, game.Galaxy);
            planet = new Planet
            {
                InstanceID = "planet",
                OwnerInstanceID = "empire",
                IsColonized = true,
            };
            game.AttachNode(planet, system);
            officer = EntityFactory.CreateOfficer("officer", "empire");
            game.AttachNode(officer, planet);
            return game;
        }

        private static StubMission CreateMission(GameRoot game, Planet planet, Officer officer)
        {
            StubMission mission = new StubMission("empire", planet.InstanceID);
            game.AttachNode(mission, planet);
            mission.MainParticipants.Add(officer);
            return mission;
        }
    }
}
