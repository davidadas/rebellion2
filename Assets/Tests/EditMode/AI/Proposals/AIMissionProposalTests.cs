using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.Tests.AI.Proposals
{
    [TestFixture]
    public class AIMissionProposalTests
    {
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
    }
}
