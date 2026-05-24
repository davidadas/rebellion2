using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Proposals;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Game.World;

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
                officer,
                MissionType.Recruitment,
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
                officer,
                MissionType.Diplomacy,
                planet
            );

            bool canSelect = proposal.CanSelect(null);

            Assert.IsFalse(canSelect);
        }
    }
}
