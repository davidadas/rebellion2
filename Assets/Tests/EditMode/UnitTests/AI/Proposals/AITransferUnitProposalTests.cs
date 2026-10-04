using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Fleets
{
    [TestFixture]
    public class AITransferUnitProposalTests
    {
        [Test]
        public void Execute_WithSamePlanetCapitalShipTransfer_ReparentsUnitToTargetFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector planetSector = AITestSceneBuilder.AddSector(game, "sector1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(
                game,
                planetSector,
                "target",
                rebels.InstanceID
            );
            Fleet sourceFleet = EntityFactory.CreateFleet("source", empire.InstanceID);
            Fleet targetFleet = EntityFactory.CreateFleet("targetFleet", empire.InstanceID);
            targetFleet.RoleType = FleetRoleType.Battle;
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID);
            game.AttachNode(sourceFleet, staging);
            game.AttachNode(ship, sourceFleet);
            game.AttachNode(targetFleet, staging);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AITransferUnitProposal proposal = new AITransferUnitProposal(
                sourceFleet,
                targetFleet,
                ship,
                targetFleet,
                target
            );

            proposal.Execute(context);

            Assert.AreEqual(targetFleet, ship.GetParent());
            Assert.IsNull(ship.Movement);
        }

        [Test]
        public void Execute_WithHeadquartersDefenseTransfer_ReparentsUnitToDefenseFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction _);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "sys1");
            Planet headquarters = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "headquarters",
                empire.InstanceID
            );
            headquarters.IsHeadquarters = true;
            empire.HQInstanceID = headquarters.InstanceID;
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Fleet sourceFleet = EntityFactory.CreateFleet("source", empire.InstanceID);
            Fleet targetFleet = EntityFactory.CreateFleet("defense", empire.InstanceID);
            targetFleet.RoleType = FleetRoleType.Battle;
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = headquarters.InstanceID,
            };
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", empire.InstanceID);
            game.AttachNode(sourceFleet, staging);
            game.AttachNode(ship, sourceFleet);
            game.AttachNode(targetFleet, staging);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AITransferUnitProposal proposal = new AITransferUnitProposal(
                sourceFleet,
                targetFleet,
                ship,
                targetFleet,
                headquarters
            );

            proposal.Execute(context);

            Assert.AreSame(targetFleet, ship.GetParent());
            Assert.IsNull(ship.Movement);
        }

        [Test]
        public void Execute_WithPlanetRegimentTransfer_LoadsRegimentIntoTargetFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSector system = AITestSceneBuilder.AddSector(game, "system");
            Planet source = AITestSceneBuilder.AddPlanet(game, system, "source", empire.InstanceID);
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
            Fleet targetFleet = EntityFactory.CreateFleet("target-fleet", empire.InstanceID);
            targetFleet.RoleType = FleetRoleType.Battle;
            targetFleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Attack,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = target.InstanceID,
            };
            CapitalShip transport = AITestSceneBuilder.CreateCapitalShip(
                "transport",
                empire.InstanceID,
                regimentCapacity: 1
            );
            Regiment regiment = AITestSceneBuilder.CreateRegiment("regiment", empire.InstanceID);
            game.AttachNode(targetFleet, source);
            game.AttachNode(transport, targetFleet);
            game.AttachNode(regiment, source);
            AITurnContext context = AITestSceneBuilder.CreateContext(game, empire);
            AITransferUnitProposal proposal = new AITransferUnitProposal(
                source,
                targetFleet,
                regiment,
                targetFleet,
                target
            );

            proposal.Execute(context);

            Assert.AreSame(transport, regiment.GetParent());
        }
    }
}
