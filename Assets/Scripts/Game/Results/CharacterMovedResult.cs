namespace Rebellion.Game.Results
{
    public class CharacterMovedResult : GameResult
    {
        public string CharacterInstanceID { get; set; }
        public string FromLocationInstanceID { get; set; }
        public string ToLocationInstanceID { get; set; }
    }
}
