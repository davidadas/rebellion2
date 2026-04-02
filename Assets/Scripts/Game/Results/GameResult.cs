namespace Rebellion.Game.Results
{
    public abstract class GameResult
    {
        public int Tick { get; set; }
        public string SourceEventInstanceID { get; set; }
    }
}
