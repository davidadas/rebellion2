using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Rebellion.AI;
using Rebellion.AI.Demands;
using Rebellion.AI.Phases;

namespace Rebellion.Tests.AI.Phases
{
    [TestFixture]
    public sealed class AIDemandGenerationPhaseTests
    {
        [Test]
        public void Execute_WithInjectedGenerator_RunsGenerator()
        {
            TestDemandGenerator generator = new TestDemandGenerator();
            AIDemandGenerationPhase phase = new AIDemandGenerationPhase(
                new IAIDemandGenerator[] { generator }
            );
            AITurnContext context = CreateContext();

            phase.Execute(context);

            Assert.AreEqual(1, generator.ExecutionCount);
        }

        [Test]
        public void ExecuteIncrementally_WithInjectedGenerators_YieldsAfterEachGenerator()
        {
            TestDemandGenerator first = new TestDemandGenerator();
            TestDemandGenerator second = new TestDemandGenerator();
            AIDemandGenerationPhase phase = new AIDemandGenerationPhase(
                new IAIDemandGenerator[] { first, second }
            );
            AITurnContext context = CreateContext();

            List<object> steps = phase.ExecuteIncrementally(context).ToList();

            CollectionAssert.AreEqual(new object[] { first, second }, steps);
            Assert.AreEqual(1, first.ExecutionCount);
            Assert.AreEqual(1, second.ExecutionCount);
        }

        /// <summary>
        /// Creates an empty context for phase orchestration tests.
        /// </summary>
        /// <returns>An empty AI turn context.</returns>
        private static AITurnContext CreateContext()
        {
            return new AITurnContext(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null
            );
        }

        private sealed class TestDemandGenerator : IAIDemandGenerator
        {
            public int ExecutionCount { get; private set; }

            /// <summary>
            /// Records one demand-generation invocation.
            /// </summary>
            /// <param name="context">The current AI turn context.</param>
            public void Generate(AITurnContext context)
            {
                ExecutionCount++;
            }
        }
    }
}
