using Rebellion.Game;

namespace Rebellion.Game.Results
{
    public enum JediEventType
    {
        TierAdvanced,
        TrainingComplete,
        JediDiscovered,
    }

    public class JediResult : GameResult
    {
        public JediEventType EventType { get; set; }
        public Officer Officer { get; set; }
        public ForceTier OldTier { get; set; }
        public ForceTier NewTier { get; set; }

        public override string ToString()
        {
            string name = Officer?.GetDisplayName() ?? "Unknown";
            return EventType switch
            {
                JediEventType.TierAdvanced => $"{name} advanced to {NewTier} at tick {Tick}",
                JediEventType.TrainingComplete => $"{name} completed Jedi training at tick {Tick}",
                JediEventType.JediDiscovered => $"{name} discovered as Force user at tick {Tick}",
                _ => $"{name} unknown Jedi event at tick {Tick}",
            };
        }
    }
}
