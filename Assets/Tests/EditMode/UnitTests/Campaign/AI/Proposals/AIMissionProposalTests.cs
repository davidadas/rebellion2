using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.Campaign.AI;
using Rebellion.Campaign.AI.Demands;
using Rebellion.Campaign.AI.Planners;
using Rebellion.Campaign.AI.Proposals;
using Rebellion.Campaign.AI.Scorers;
using Rebellion.Campaign.AI.Selectors;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Tests.Campaign.AI.Helpers;

namespace Rebellion.Tests.Campaign.AI.Proposals
{
    [TestFixture]
    public class AIMissionProposalTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Constructor_MutableInputRosters_PreservesOrderedNonNullPersonnel(
            bool singleMain
        )
        {
            Officer first = EntityFactory.CreateOfficer("first", "empire");
            Officer second = EntityFactory.CreateOfficer("second", "empire");
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            List<IMissionParticipant> mains = singleMain
                ? new() { first }
                : new() { first, null, second };
            List<IMissionParticipant> decoys = new() { null, decoy };
            AIMissionProposal proposal = new(
                mains,
                MissionTypeIDs.Espionage,
                new Planet { InstanceID = "target" },
                decoyParticipants: decoys
            );
            IMissionParticipant[] expectedMains = singleMain
                ? new IMissionParticipant[] { first }
                : new IMissionParticipant[] { first, second };

            mains.Clear();
            decoys.Clear();

            CollectionAssert.AreEqual(expectedMains, proposal.MainParticipants);
            CollectionAssert.AreEqual(new[] { decoy }, proposal.DecoyParticipants);
            CollectionAssert.AreEqual(expectedMains.Concat(new[] { decoy }), proposal.Participants);
        }

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
            fleet.Movement = TestMovement.Create();
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
