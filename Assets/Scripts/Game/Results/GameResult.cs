namespace Rebellion.Game.Results
{
    /// <summary>
    /// Base record for simulation output emitted by systems during tick processing.
    /// </summary>
    public abstract class GameResult
    {
        public int Tick { get; set; }
        public string SourceEventInstanceID { get; set; }

        /// <summary>
        /// Gets or sets the encompassing result whose report includes this consequence.
        /// Observers still process the consequence, while message selection belongs to its context.
        /// </summary>
        public GameResult ReportContext { get; set; }
    }
}
