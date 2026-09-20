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
}
