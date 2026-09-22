using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Demands;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.AI.Selectors;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

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
    }
}
