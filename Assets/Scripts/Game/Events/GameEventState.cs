using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Persists the runtime scheduling state for one data-defined game event.
    /// Event definitions remain content; this state is the save-game-owned execution history.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventState
    {
        public bool IsInitialized { get; set; }
        public int NextEligibleTick { get; set; }
        public int ExecutionCount { get; set; }
        public int LastExecutionTick { get; set; } = -1;
    }
}
