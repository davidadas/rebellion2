namespace Rebellion.Game.Results
{
    public class CharacterCapturedResult : GameResult
    {
        public string CharacterInstanceID { get; set; }
        public string CapturingFactionInstanceID { get; set; }
        public string LocationInstanceID { get; set; }
    }
}
