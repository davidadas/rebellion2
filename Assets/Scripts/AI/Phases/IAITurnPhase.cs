using System.Collections.Generic;
using Rebellion.AI.Director;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Phase that runs once during a faction AI turn.
    /// </summary>
    public interface IAITurnPhase
    {
        /// <summary>
        /// Runs this phase against the current turn context.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        void Execute(AITurnContext context);
    }

    /// <summary>
    /// AI phase that can divide its work into independently scheduled steps.
    /// </summary>
    internal interface IAIIncrementalTurnPhase
    {
        /// <summary>
        /// Runs the phase and yields after each completed unit of work.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>A sequence containing one marker per completed unit of work.</returns>
        IEnumerable<object> ExecuteIncrementally(AITurnContext context);
    }
}
