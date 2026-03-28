using System.Collections.Generic;

namespace Rebellion.Game.Results
{
    public class GenericResult : GameResult
    {
        public string ResultType { get; set; }
        public Dictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }
}
