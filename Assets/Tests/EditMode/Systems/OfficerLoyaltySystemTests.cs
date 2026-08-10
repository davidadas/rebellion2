using System.Linq;
using NUnit.Framework;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
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
    }
}
