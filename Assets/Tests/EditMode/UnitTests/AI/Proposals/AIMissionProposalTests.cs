using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Movement;
using Rebellion.Game.Units;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIMissionProposalTests
    {
        /// <summary>
        /// Verifies get claim keys with recruitment adds faction recruitment claim.
        /// </summary>
        [Test]
        public void GetClaimKeys_WithRecruitment_AddsFactionRecruitmentClaim()
        {
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "empire" };
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.Recruitment,
                planet
            );

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys();

            CollectionAssert.Contains(claimKeys, "mission:actor:officer");
            CollectionAssert.Contains(claimKeys, "mission:recruitment:empire");
        }

        /// <summary>
        /// Verifies can select with captured officer returns false.
        /// </summary>
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

        /// <summary>
        /// Verifies can select with participant in moving fleet returns false.
        /// </summary>
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

        /// <summary>
        /// Verifies get claim keys with participant team claims every participant.
        /// </summary>
        [Test]
        public void GetClaimKeys_WithParticipantTeam_ClaimsEveryParticipant()
        {
            Officer trainer = EntityFactory.CreateOfficer("trainer", "empire");
            Officer student = EntityFactory.CreateOfficer("student", "empire");
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "empire" };
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { trainer, student },
                MissionTypeIDs.JediTraining,
                planet
            );

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys();

            CollectionAssert.Contains(claimKeys, "mission:actor:trainer");
            CollectionAssert.Contains(claimKeys, "mission:actor:student");
        }

        /// <summary>
        /// Verifies get claim keys with decoy claims main and decoy participants.
        /// </summary>
        [Test]
        public void GetClaimKeys_WithDecoy_ClaimsMainAndDecoyParticipants()
        {
            Officer main = EntityFactory.CreateOfficer("main", "empire");
            Officer decoy = EntityFactory.CreateOfficer("decoy", "empire");
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "rebels" };
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { main },
                MissionTypeIDs.Espionage,
                planet,
                decoyParticipants: new[] { decoy }
            );

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys();

            CollectionAssert.Contains(claimKeys, "mission:actor:main");
            CollectionAssert.Contains(claimKeys, "mission:actor:decoy");
        }

        /// <summary>
        /// Verifies get claim keys with hostile mission does not claim faction wide hostile slot.
        /// </summary>
        [Test]
        public void GetClaimKeys_WithHostileMission_DoesNotClaimFactionWideHostileSlot()
        {
            Officer officer = EntityFactory.CreateOfficer("officer", "empire");
            Planet planet = new Planet { InstanceID = "planet", OwnerInstanceID = "rebels" };
            AIMissionProposal proposal = new AIMissionProposal(
                new[] { officer },
                MissionTypeIDs.InciteUprising,
                planet
            );

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys();

            CollectionAssert.DoesNotContain(claimKeys, "mission:hostile:empire");
        }
    }
}
