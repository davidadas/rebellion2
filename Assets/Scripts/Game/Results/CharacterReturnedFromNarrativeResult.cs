namespace Rebellion.Game.Results
{
    public class CharacterReturnedFromNarrativeResult : GameResult
    {
        public string CharacterInstanceID { get; set; }
        public string ToLocationInstanceID { get; set; }
        public string NarrativeType { get; set; }
    }
}
