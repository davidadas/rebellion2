namespace Rebellion.Game.Results
{
    /// <summary>
    /// Base record for simulation output emitted by systems during tick processing.
    /// </summary>
    public abstract class GameResult
    {
        public int Tick { get; set; }
        public string SourceEventInstanceID { get; set; }
        public string MissionInstanceID { get; set; }

        /// <summary>
        /// Gets or sets whether an authored reaction replaces this result's automatic message.
        /// </summary>
        public bool SuppressDefaultMessage { get; set; }
    }
}
