using System.Collections.Generic;

namespace Rebellion.Game.Results
{
    public class DuelTriggeredResult : GameResult
    {
        public List<string> AttackerInstanceIDs { get; set; } = new List<string>();
        public List<string> DefenderInstanceIDs { get; set; } = new List<string>();
    }
}
