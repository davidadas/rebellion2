using System.Collections.Generic;
using NUnit.Framework;
using Rebellion.AI.Director;
using Rebellion.AI.Phases;
using Rebellion.AI.Proposals;
using Rebellion.Tests.AI.Helpers;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public class AIExecutionPhaseTests
    {
        /// <summary>
        /// Verifies execute with selected executable proposal executes proposal.
        /// </summary>
        [Test]
        public void Execute_WithSelectedExecutableProposal_ExecutesProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: true);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(1, proposal.ExecuteCount);
        }

        /// <summary>
        /// Verifies execute with selected non executable proposal does not execute proposal.
        /// </summary>
        [Test]
        public void Execute_WithSelectedNonExecutableProposal_DoesNotExecuteProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: false);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            new AIExecutionPhase().Execute(context);

            Assert.AreEqual(0, proposal.ExecuteCount);
        }

        /// <summary>
        /// Verifies execute incrementally with selected proposals yields after each proposal.
        /// </summary>
        [Test]
        public void ExecuteIncrementally_WithSelectedProposals_YieldsAfterEachProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal first = new TestAIProposal(canExecute: true);
            TestAIProposal second = new TestAIProposal(canExecute: true);
            context.SetSelectedProposals(new List<AIProposal> { first, second });

            IEnumerator<object> execution = new AIExecutionPhase()
                .ExecuteIncrementally(context)
                .GetEnumerator();

            Assert.IsTrue(execution.MoveNext());
            Assert.AreEqual(1, first.ExecuteCount);
            Assert.AreEqual(0, second.ExecuteCount);
            Assert.IsTrue(execution.MoveNext());
            Assert.AreEqual(1, second.ExecuteCount);
            Assert.IsFalse(execution.MoveNext());
        }

        /// <summary>
        /// Verifies execute incrementally with invalid proposal does not execute proposal.
        /// </summary>
        [Test]
        public void ExecuteIncrementally_WithInvalidProposal_DoesNotExecuteProposal()
        {
            AITurnContext context = CreateContext();
            TestAIProposal proposal = new TestAIProposal(canExecute: false);
            context.SetSelectedProposals(new List<AIProposal> { proposal });

            foreach (object _ in new AIExecutionPhase().ExecuteIncrementally(context)) { }

            Assert.AreEqual(0, proposal.ExecuteCount);
        }

        /// <summary>
        /// Creates context.
        /// </summary>
        /// <returns>The created context.</returns>
        private static AITurnContext CreateContext()
        {
            return new AITurnContext(null, null, null, null, null, null, null, null);
        }
    }
}
