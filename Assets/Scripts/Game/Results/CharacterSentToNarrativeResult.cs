namespace Rebellion.Game.Results
{
    public class CharacterSentToNarrativeResult : GameResult
    {
        public string CharacterInstanceID { get; set; }
        public string FromLocationInstanceID { get; set; }
        public string NarrativeType { get; set; }
    }
}
