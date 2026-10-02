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
        public void ApplyControlShift_FactionGainsPlanet_ShiftsBetrayableOfficerLoyalty()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer empireOfficer,
                canBetray: true,
                loyalty: 50
            );
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(alliance);
            planet.PopularSupport = new Dictionary<string, int>
            {
                { alliance.InstanceID, 100 },
                { empireOfficer.OwnerInstanceID, 0 },
            };
            Planet alliancePlanet = new Planet
            {
                InstanceID = "alliance-planet",
                OwnerInstanceID = alliance.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(alliancePlanet, planet.GetParent());
            Officer allianceOfficer = EntityFactory.CreateOfficer(
                "alliance-free",
                alliance.InstanceID,
                canBetray: true,
                loyalty: 50
            );
            game.AttachNode(allianceOfficer, alliancePlanet);
            Officer commander = EntityFactory.CreateOfficer(
                "alliance-command",
                alliance.InstanceID,
                canBetray: true,
                loyalty: 50
            );
            commander.CurrentRank = OfficerRank.General;
            game.AttachNode(commander, alliancePlanet);
            Officer captive = EntityFactory.CreateOfficer(
                "empire-captive",
                "empire",
                canBetray: true,
                loyalty: 50
            );
            captive.IsCaptured = true;
            game.AttachNode(captive, alliancePlanet);
            OfficerLoyaltyCommands system = new OfficerLoyaltyCommands(game, new ThrowingRNG());

            system.ApplyControlShift(planet, alliance);

            Assert.AreEqual(52, allianceOfficer.Loyalty);
            Assert.AreEqual(48, empireOfficer.Loyalty);
            Assert.AreEqual(52, commander.Loyalty);
            Assert.AreEqual(48, captive.Loyalty);
        }

        [Test]
        public void ApplyControlShift_OfficerCannotBetray_DoesNotShiftLoyalty()
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer officer,
                canBetray: false,
                loyalty: 50
            );
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(alliance);
            planet.PopularSupport = new Dictionary<string, int>
            {
                { alliance.InstanceID, 100 },
                { officer.OwnerInstanceID, 0 },
            };
            OfficerLoyaltyCommands commands = new OfficerLoyaltyCommands(game, new ThrowingRNG());

            commands.ApplyControlShift(planet, alliance);

            Assert.AreEqual(50, officer.Loyalty);
        }

        [TestCase(100, 0, 2)]
        [TestCase(80, 20, 1)]
        [TestCase(60, 40, 0)]
        [TestCase(10, 90, -1)]
        public void ApplyControlShift_SupportLevels_DeriveExpectedShift(
            int incomingSupport,
            int opposingSupport,
            int expectedShift
        )
        {
            GameRoot game = BuildScene(
                out Planet planet,
                out Officer officer,
                canBetray: true,
                loyalty: 50
            );
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.GetFactions().Add(alliance);
            officer.OwnerInstanceID = alliance.InstanceID;
            planet.PopularSupport = new Dictionary<string, int>
            {
                { alliance.InstanceID, incomingSupport },
                { "empire", opposingSupport },
            };

            new OfficerLoyaltyCommands(game, new ThrowingRNG()).ApplyControlShift(planet, alliance);

            Assert.AreEqual(50 + expectedShift, officer.Loyalty);
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
        public void TryResolveMissionBetrayal_ForceCapableCompanion_RevealsTraitor()
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

            bool betrayed = new OfficerLoyaltyCommands(
                game,
                new StubRNG()
            ).TryResolveMissionBetrayal(mission, out List<GameResult> results);

            TraitorDiscoveredResult result = results.OfType<TraitorDiscoveredResult>().Single();
            Assert.IsTrue(betrayed);
            Assert.AreSame(traitor, result.Officer);
            Assert.AreSame(discoverer, result.DiscoveredBy);
            Assert.AreSame(planet, result.Context);
        }

        [Test]
        public void TryResolveMissionBetrayal_MultipleTraitors_RevealsLastTraitor()
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

            bool betrayed = new OfficerLoyaltyCommands(
                game,
                new SequenceRNG(new[] { 0, 0, 99, 0 })
            ).TryResolveMissionBetrayal(mission, out List<GameResult> results);

            TraitorDiscoveredResult result = results.OfType<TraitorDiscoveredResult>().Single();
            Assert.IsTrue(betrayed);
            Assert.AreSame(lastTraitor, result.Officer);
            Assert.AreSame(discoverer, result.DiscoveredBy);
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
            config.OfficerLoyalty.PlanetAcquisitionSupportDivisor = 80;
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
