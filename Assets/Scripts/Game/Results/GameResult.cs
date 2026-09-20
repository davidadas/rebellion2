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
        internal bool ReactionsResolved { get; set; }
    }
}
