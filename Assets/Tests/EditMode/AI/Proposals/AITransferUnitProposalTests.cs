using System.Collections.Generic;
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
    public class AITransferUnitProposalTests
    {
        [Test]
        public void Execute_WithValidCapitalShipTransfer_MovesUnitToTargetFleet()
        {
            GameRoot game = AITestSceneBuilder.CreateGame(out Faction empire, out Faction rebels);
            PlanetSystem system = AITestSceneBuilder.AddSystem(game, "sys1");
            Planet staging = AITestSceneBuilder.AddPlanet(
                game,
                system,
                "staging",
                empire.InstanceID
            );
            Planet target = AITestSceneBuilder.AddPlanet(game, system, "target", rebels.InstanceID);
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
        public void GetClaimKeys_WithSourceAndTargetFleet_ReturnsTransferClaims()
        {
            Fleet sourceFleet = EntityFactory.CreateFleet("source", "empire");
            Fleet targetFleet = EntityFactory.CreateFleet("targetFleet", "empire");
            CapitalShip ship = AITestSceneBuilder.CreateCapitalShip("ship", "empire");
            Planet target = new Planet { InstanceID = "target" };
            AITransferUnitProposal proposal = new AITransferUnitProposal(
                sourceFleet,
                targetFleet,
                ship,
                targetFleet,
                target
            );

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys();

            Assert.Contains("unit:transfer:ship", (System.Collections.ICollection)claimKeys);
            Assert.Contains(
                "container:transfer-source:source",
                (System.Collections.ICollection)claimKeys
            );
            Assert.Contains(
                "container:transfer-target:targetFleet",
                (System.Collections.ICollection)claimKeys
            );
        }
    }
}
