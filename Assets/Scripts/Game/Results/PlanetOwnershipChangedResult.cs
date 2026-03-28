namespace Rebellion.Game.Results
{
    public class PlanetOwnershipChangedResult : GameResult
    {
        public string PlanetInstanceID { get; set; }
        public string PreviousOwnerInstanceID { get; set; }
        public string NewOwnerInstanceID { get; set; }
    }
}
