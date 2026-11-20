namespace Rebellion.AI.Demands
{
    /// <summary>
    /// Generates turn-scoped domain demands from assessed game state.
    /// </summary>
    public interface IAIDemandGenerator
    {
        /// <summary>
        /// Generates domain demands and records them on the turn context.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        void Generate(AITurnContext context);
    }
}
