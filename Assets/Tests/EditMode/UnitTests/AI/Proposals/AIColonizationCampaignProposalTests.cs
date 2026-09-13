using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIColonizationCampaignProposalTests
    {
        [Test]
        public void Execute_WithUnexploredPlanets_StartsNearestNeighborSurveyRoute()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector core = AITestSceneBuilder.AddSector(game, "core");
            Planet origin = AITestSceneBuilder.AddPlanet(game, core, "origin", empire.InstanceID);
            PlanetSector outerRim = AITestSceneBuilder.AddSector(game, "outer-rim");
            outerRim.SectorType = PlanetSectorType.OuterRim;
            Planet near = AITestSceneBuilder.AddPlanet(game, outerRim, "near", null, positionX: 2);
            near.IsColonized = false;
            Planet far = AITestSceneBuilder.AddPlanet(game, outerRim, "far", null, positionX: 10);
            far.IsColonized = false;
            Fleet fleet = AddColonizationFleet(game, origin, empire.InstanceID);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIColonizationCampaignProposal proposal = new AIColonizationCampaignProposal(
                fleet,
                outerRim.InstanceID,
                new[]
                {
                    context
                        .FactionView.GetChildren<PlanetSector>()
                        .Single(system => system.InstanceID == outerRim.InstanceID)
                        .GetChildren<Planet>()
                        .Single(planet => planet.InstanceID == far.InstanceID),
                    context
                        .FactionView.GetChildren<PlanetSector>()
                        .Single(system => system.InstanceID == outerRim.InstanceID)
                        .GetChildren<Planet>()
                        .Single(planet => planet.InstanceID == near.InstanceID),
                }
            );

            proposal.Execute(context);

            Assert.AreEqual(FleetOrderType.Colonize, fleet.Order.OrderType);
            Assert.AreEqual(outerRim.InstanceID, fleet.Order.TargetSystemId);
            CollectionAssert.AreEqual(new[] { near.InstanceID, far.InstanceID }, fleet.Waypoints);
            Assert.IsNotNull(fleet.Movement);
        }

        [Test]
        public void Execute_WithCompletedSurvey_AssignsSelectedColony()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "outer-rim");
            system.SectorType = PlanetSectorType.OuterRim;
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", null);
            target.IsColonized = false;
            AITestSceneBuilder.RevealPlanet(game, empire, target);
            Fleet fleet = AddColonizationFleet(game, target, empire.InstanceID);
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = FleetOrderStatus.Readying,
                TargetSystemId = system.InstanceID,
            };
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AIColonizationCampaignProposal proposal = new AIColonizationCampaignProposal(
                fleet,
                system.InstanceID,
                System.Array.Empty<Planet>(),
                context.Assessment.GetKnownPlanet(target.InstanceID)
            );

            proposal.Execute(context);

            Assert.AreEqual(FleetOrderType.Colonize, fleet.Order.OrderType);
            Assert.AreEqual(FleetOrderStatus.Ready, fleet.Order.Status);
            Assert.AreEqual(target.InstanceID, fleet.Order.TargetPlanetId);
            Assert.AreEqual(system.InstanceID, fleet.Order.TargetSystemId);
        }

        private static Fleet AddColonizationFleet(
            GameRoot game,
            Planet planet,
            string ownerInstanceId
        )
        {
            Fleet fleet = EntityFactory.CreateFleet("fleet", ownerInstanceId);
            fleet.RoleType = FleetRoleType.Colonization;
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "transport",
                ownerInstanceId,
                combatStrength: 1,
                regimentCapacity: 2
            );
            game.AttachNode(fleet, planet);
            game.AttachNode(ship, fleet);
            return fleet;
        }
    }
}
