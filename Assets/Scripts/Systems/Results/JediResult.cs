using Rebellion.Game;

namespace Rebellion.Systems.Results
{
    /// <summary>
    /// Event types emitted by JediSystem.ProcessTick().
    /// </summary>
    public enum JediEventType
    {
        /// <summary>Officer's Force tier advanced (Aware → Training or Training → Experienced)</summary>
        TierAdvanced,

        /// <summary>Officer completed Jedi training (reached Experienced tier)</summary>
        TrainingComplete,

        /// <summary>Opposing faction detected this officer's Force ability</summary>
        JediDiscovered,
    }

    /// <summary>
    /// Result event from Jedi system tier advancement or detection checks.
    /// Returned by JediSystem.ProcessTick() for logging and UI notifications.
    /// Effects are already applied to officer state before event is returned.
    /// </summary>
    public struct JediResult
    {
        /// <summary>Type of Jedi event that occurred</summary>
        public JediEventType EventType { get; set; }

        /// <summary>Officer affected by this event</summary>
        public Officer Officer { get; set; }

        /// <summary>Force tier before this event (for TierAdvanced events)</summary>
        public ForceTier OldTier { get; set; }

        /// <summary>Force tier after this event (for TierAdvanced events)</summary>
        public ForceTier NewTier { get; set; }

        /// <summary>Game tick when this event occurred</summary>
        public int Tick { get; set; }

        /// <summary>Returns string representation of the Jedi event</summary>
        public override string ToString()
        {
            string officerName = Officer?.GetDisplayName() ?? "Unknown";
            switch (EventType)
            {
                case JediEventType.TierAdvanced:
                    return $"{officerName} advanced to {NewTier} tier at tick {Tick}";
                case JediEventType.TrainingComplete:
                    return $"{officerName} completed Jedi training at tick {Tick}";
                case JediEventType.JediDiscovered:
                    return $"{officerName} discovered as Force user at tick {Tick}";
                default:
                    return $"{officerName} unknown Jedi event at tick {Tick}";
            }
        }
    }
}
