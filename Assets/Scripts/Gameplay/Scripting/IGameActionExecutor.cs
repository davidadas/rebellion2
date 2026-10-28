using Rebellion.Game.Scripting;

namespace Rebellion.Gameplay.Scripting
{
    /// <summary>
    /// Executes individual gameplay actions selected by the shared interpreter.
    /// </summary>
    public interface IGameActionExecutor
    {
        /// <summary>
        /// Executes one gameplay action in the current script activation.
        /// </summary>
        /// <param name="action">The authored action to execute.</param>
        /// <param name="context">The bindings, random source, and results for this activation.</param>
        void Execute(GameAction action, GameActionContext context);
    }
}
