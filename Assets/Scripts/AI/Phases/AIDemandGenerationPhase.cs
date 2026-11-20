using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Generates assessed domain demands for the current faction turn.
    /// </summary>
    public sealed class AIDemandGenerationPhase : IAIIncrementalTurnPhase
    {
        private readonly List<IAIDemandGenerator> _generators;

        /// <summary>
        /// Creates a demand-generation phase with the default generators.
        /// </summary>
        public AIDemandGenerationPhase()
            : this(
                new IAIDemandGenerator[]
                {
                    new AIAttackDemandGenerator(),
                    new AIProductionDemandGenerator(),
                }
            ) { }

        /// <summary>
        /// Creates a demand-generation phase with the supplied generators.
        /// </summary>
        /// <param name="generators">Demand generators run by this phase.</param>
        internal AIDemandGenerationPhase(IEnumerable<IAIDemandGenerator> generators)
        {
            if (generators == null)
                throw new ArgumentNullException(nameof(generators));

            _generators = generators.ToList();
            if (_generators.Any(generator => generator == null))
                throw new ArgumentException(
                    "Demand generator list cannot contain null entries.",
                    nameof(generators)
                );
        }

        /// <summary>
        /// Generates all demands for the current turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            foreach (object _ in ExecuteIncrementally(context)) { }
        }

        /// <summary>
        /// Runs demand generators one at a time.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>A sequence containing one marker per completed generator.</returns>
        public IEnumerable<object> ExecuteIncrementally(AITurnContext context)
        {
            if (context == null)
                yield break;

            foreach (IAIDemandGenerator generator in _generators)
            {
                generator.Generate(context);
                yield return generator;
            }
        }
    }
}
