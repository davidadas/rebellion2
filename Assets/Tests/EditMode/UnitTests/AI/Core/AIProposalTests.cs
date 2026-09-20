using NUnit.Framework;
using Rebellion.Tests.AI.Core.Helpers;

namespace Rebellion.Tests.AI.Core
{
    [TestFixture]
    public class AIProposalTests
    {
        [Test]
        public void SetScore_WithScore_MarksProposalAsScored()
        {
            TestAIProposal proposal = new TestAIProposal();

            proposal.SetScore(42.5);

            Assert.IsTrue(proposal.HasScore);
            Assert.AreEqual(42.5, proposal.Score);
        }
    }
}
