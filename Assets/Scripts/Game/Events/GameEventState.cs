using System;
using System.Collections.Generic;
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
        public bool IsScopeActive { get; set; }
        public int NextEligibleTick { get; set; }
        public int ExecutionCount { get; set; }
        public int LastExecutionTick { get; set; } = -1;

        public Dictionary<string, GameEventState> TargetStates { get; set; } =
            new Dictionary<string, GameEventState>(StringComparer.Ordinal);

        public GameEventState GetTargetState(string targetInstanceID)
        {
            if (string.IsNullOrWhiteSpace(targetInstanceID))
                throw new ArgumentException(
                    "Event target instance ID is required.",
                    nameof(targetInstanceID)
                );
            if (!TargetStates.TryGetValue(targetInstanceID, out GameEventState state))
            {
                state = new GameEventState();
                TargetStates.Add(targetInstanceID, state);
            }
            return state;
        }

        public bool TryGetTargetState(string targetInstanceID, out GameEventState state) =>
            TargetStates.TryGetValue(targetInstanceID, out state);
    }
}
