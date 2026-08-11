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
    public class OfficerLoyaltySystemTests
    {
        [Test]
        public void HandleResults_FactionGainsPlanet_ShiftsOnlyFreeLivingOfficerLoyalty()
        {
            GameRoot game = BuildScene(out Planet planet, out Officer empireOfficer);
            Faction alliance = new Faction { InstanceID = "alliance" };
            game.Factions.Add(alliance);
            empireOfficer.Loyalty = 50;
            Planet alliancePlanet = new Planet
            {
                InstanceID = "alliance-planet",
                OwnerInstanceID = alliance.InstanceID,
                IsColonized = true,
            };
            game.AttachNode(alliancePlanet, planet.GetParent());
            Officer allianceOfficer = EntityFactory.CreateOfficer(
                "alliance-free",
                alliance.InstanceID
            );
            allianceOfficer.Loyalty = 50;
            game.AttachNode(allianceOfficer, alliancePlanet);
            Officer commander = EntityFactory.CreateOfficer(
                "alliance-command",
                alliance.InstanceID
            );
            commander.Loyalty = 50;
            commander.CurrentRank = OfficerRank.General;
            game.AttachNode(commander, alliancePlanet);
            Officer captive = EntityFactory.CreateOfficer("empire-captive", "empire");
            captive.Loyalty = 50;
            captive.IsCaptured = true;
            game.AttachNode(captive, alliancePlanet);
            OfficerLoyaltySystem system = new OfficerLoyaltySystem(
                game,
                new SequenceRNG(new[] { 5 })
            );

            system.HandleResults(
                new[]
                {
                    new PlanetOwnershipChangedResult
                    {
                        Planet = planet,
                        PreviousOwner = game.Factions.Single(faction =>
                            faction.InstanceID == "empire"
                        ),
                        NewOwner = alliance,
                    },
                }
            );

            Assert.AreEqual(55, allianceOfficer.Loyalty);
            Assert.AreEqual(45, empireOfficer.Loyalty);
            Assert.AreEqual(50, commander.Loyalty);
            Assert.AreEqual(50, captive.Loyalty);
        }

        [Test]
        public void TryResolveMissionBetrayal_LowLoyaltyOfficer_FoilsWithoutRevealingIdentity()
        {
            GameRoot game = BuildScene(out Planet planet, out Officer officer);
            officer.CanBetray = true;
            officer.Loyalty = 0;
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new OfficerLoyaltySystem(game, new StubRNG()).TryResolveMissionBetrayal(
                mission,
                out List<GameResult> results
            );

            Assert.IsTrue(betrayed);
            Assert.IsEmpty(results);
            Assert.IsFalse(officer.IsTraitor);
        }

        [Test]
        public void TryResolveMissionBetrayal_ForceCapableCompanion_DiscoversTraitor()
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

            bool betrayed = new OfficerLoyaltySystem(game, new StubRNG()).TryResolveMissionBetrayal(
                mission,
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
        public void TryResolveMissionBetrayal_BoundaryRoll_UsesOneHundredMinusLoyalty(
            int loyalty,
            int roll,
            bool expectedBetrayal
        )
        {
            GameRoot game = BuildScene(out Planet planet, out Officer officer);
            officer.CanBetray = true;
            officer.Loyalty = loyalty;
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new OfficerLoyaltySystem(
                game,
                new SequenceRNG(new[] { roll })
            ).TryResolveMissionBetrayal(mission, out _);

            Assert.AreEqual(expectedBetrayal, betrayed);
        }

        [Test]
        public void TryResolveMissionBetrayal_CommandOfficer_DoesNotBetray()
        {
            GameRoot game = BuildScene(out Planet planet, out Officer officer);
            officer.CanBetray = true;
            officer.Loyalty = 0;
            officer.CurrentRank = OfficerRank.Admiral;
            StubMission mission = CreateMission(game, planet, officer);

            bool betrayed = new OfficerLoyaltySystem(game, new StubRNG()).TryResolveMissionBetrayal(
                mission,
                out _
            );

            Assert.IsFalse(betrayed);
        }

        private static GameRoot BuildScene(out Planet planet, out Officer officer)
        {
            GameConfig config = TestConfig.Create();
            config.OfficerLoyalty.IncomingControlShift.Minimum = 0;
            config.OfficerLoyalty.IncomingControlShift.Maximum = 5;
            GameRoot game = new GameRoot(config);
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
