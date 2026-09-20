using System.Collections.Generic;
using Rebellion.AI.Director;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// AI phase that exposes explicit scheduling boundaries within its work.
    /// </summary>
    internal interface IAIIncrementalTurnPhase : IAITurnPhase
    {
        /// <summary>
        /// Runs the phase and yields at each boundary chosen by the implementation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>A sequence containing one marker per scheduling boundary.</returns>
        IEnumerable<object> ExecuteIncrementally(AITurnContext context);
    }
}
