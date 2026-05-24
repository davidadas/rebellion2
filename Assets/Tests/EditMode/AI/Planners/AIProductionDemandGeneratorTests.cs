using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Planners
{
    [TestFixture]
    public class AIProductionDemandGeneratorTests
    {
        [Test]
        public void Generate_WithUnminedResourcesAndBalancedEconomy_AddsMineAndRefineryDemand()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            AITestSceneBuilder.AddPlanet(
                game,
                system,
                "resource-world",
                empire.InstanceID,
                rawResourceNodes: 4
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Mine));
            Assert.IsTrue(demands.Any(demand => demand.Kind == AIProductionDemandKind.Refinery));
        }

        [Test]
        public void Generate_WithFleetCapacityGaps_AddsFleetReinforcementDemands()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            game.Config.AI.Infrastructure.StarfighterParentFillPercent = 100;
            game.Config.AI.Infrastructure.AssaultRegimentLoadPercent = 100;
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet owned = AITestSceneBuilder.AddPlanet(game, system, "owned", empire.InstanceID);
            Planet enemy = AITestSceneBuilder.AddPlanet(game, system, "enemy", rebels.InstanceID);
            Fleet fleet = EntityFactory.CreateFleet("fleet", empire.InstanceID);
            fleet.RoleType = FleetRoleType.Battle;
            fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = enemy.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip(
                "ship",
                empire.InstanceID,
                combatStrength: 10,
                regimentCapacity: 1,
                starfighterCapacity: 2
            );
            fleet.AddChild(ship);
            ship.SetParent(fleet);
            game.AttachNode(fleet, owned);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);

            List<AIProductionDemand> demands = new AIProductionDemandGenerator().Generate(context);

            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetStarfighter)
            );
            Assert.IsTrue(
                demands.Any(demand => demand.Kind == AIProductionDemandKind.FleetRegiment)
            );
        }
    }
}
