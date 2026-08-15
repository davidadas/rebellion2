using System;
using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Stores the persisted execution history owned by the game-event subsystem.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventRuntimeState
    {
        public Dictionary<string, GameEventState> States { get; set; } =
            new Dictionary<string, GameEventState>(StringComparer.Ordinal);
        public Dictionary<string, int> Variables { get; set; } =
            new Dictionary<string, int>(StringComparer.Ordinal);

        public GameEventState GetState(string eventInstanceID)
        {
            if (string.IsNullOrWhiteSpace(eventInstanceID))
                throw new ArgumentException(
                    "Event instance ID is required.",
                    nameof(eventInstanceID)
                );
            if (!States.TryGetValue(eventInstanceID, out GameEventState state))
            {
                state = new GameEventState();
                States.Add(eventInstanceID, state);
            }
            return state;
        }

        public int GetVariable(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Event variable key is required.", nameof(key));
            return Variables.TryGetValue(key, out int value) ? value : 0;
        }

        public void SetVariable(string key, int value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Event variable key is required.", nameof(key));
            Variables[key] = value;
        }
    }
}
