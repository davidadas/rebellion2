namespace Rebellion.Game.Results
{
    public abstract class GameResult
    {
        public int Tick { get; set; }

        /// <summary>
        /// Optional: which event produced this (if applicable).
        /// </summary>
        public string SourceEventInstanceID { get; set; }
    }
}
