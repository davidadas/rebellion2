using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIMissionProposalTests
    {
        [Test]
        public void CanSelect_WithCapturedOfficer_ReturnsFalse()
        {
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            officer.IsCaptured = true;
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "empire" };
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Diplomacy,
                planet
            );

            bool canSelect = proposal.CanSelect(null);

            Assert.IsFalse(canSelect);
        }

        [Test]
        public void CanSelect_WithParticipantInMovingFleet_ReturnsFalse()
        {
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            Fleet fleet = EntityFactory.CreateFleet("fleet", "empire");
            fleet.Movement = new MovementState();
            CapitalShip capitalShip = new CapitalShip
            {
                InstanceID = "ship",
                OwnerInstanceID = "empire",
            };
            fleet.AddChild(capitalShip);
            capitalShip.SetParent(fleet);
            capitalShip.AddChild(officer);
            officer.SetParent(capitalShip);
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "empire" };
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Diplomacy,
                planet
            );

            bool canSelect = proposal.CanSelect(null);

            Assert.IsFalse(canSelect);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CanSelect_HeldMainParticipantOrDecoy_RejectsMission(bool holdDecoy)
        {
            var (game, _, target, main, _) = MissionSceneBuilder.Build();
            Faction owner = game.GetFactions()
                .Single(faction => faction.InstanceID == main.OwnerInstanceID);
            Officer decoy = EntityFactory.CreateOfficer("decoy", owner.InstanceID);
            game.AttachNode(decoy, main.GetParent());
            target.AddVisitor(owner.InstanceID);
            AIMissionProposal proposal = new(
                new[] { main },
                MissionTypeIDs.Espionage,
                target,
                decoyParticipants: new[] { decoy }
            );
            AITurnContext context = AITestSceneBuilder.CreateContext(game, owner);
            Assert.IsTrue(proposal.CanSelect(context));
            Assert.IsTrue(proposal.CanExecute(context));
            Officer held = holdDecoy ? decoy : main;
            AIAssessment assessment = new(
                game,
                owner,
                game.Galaxy,
                new Dictionary<string, OfficerAIBehavior>
                {
                    [held.InstanceID] = new OfficerAIBehavior(),
                }
            );
            AITurnContext heldContext = new(
                game,
                owner,
                context.Missions,
                context.Movement,
                context.Manufacturing,
                context.Bombardment,
                context.PlanetaryAssault,
                context.Random,
                assessment,
                new AIStrategicPlan(game, assessment),
                context.FactionView
            );

            Assert.IsFalse(proposal.CanSelect(heldContext));
            Assert.IsFalse(proposal.CanExecute(heldContext));
        }
    }
}
