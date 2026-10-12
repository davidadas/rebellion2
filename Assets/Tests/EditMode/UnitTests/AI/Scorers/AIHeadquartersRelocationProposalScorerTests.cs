using NUnit.Framework;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;

namespace Rebellion.Tests.AI.Scorers
{
    [TestFixture]
    public sealed class AIHeadquartersRelocationProposalScorerTests
    {
        [Test]
        public void Score_LargerDefenseDeficit_ReturnsHigherUrgency()
        {
            AIHeadquartersRelocationProposalScorer scorer = new();
            AIHeadquartersRelocationProposal lowerDeficit = new(
                null,
                null,
                null,
                null,
                threatStrength: 200,
                defenseStrength: 150
            );
            AIHeadquartersRelocationProposal higherDeficit = new(
                null,
                null,
                null,
                null,
                threatStrength: 200,
                defenseStrength: 50
            );

            Assert.Greater(scorer.Score(null, higherDeficit), scorer.Score(null, lowerDeficit));
        }
    }
}
