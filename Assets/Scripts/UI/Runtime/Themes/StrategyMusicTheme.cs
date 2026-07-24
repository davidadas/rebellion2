using System.Collections.Generic;
using Rebellion.Util.Serialization;

[PersistableObject]
public sealed class StrategyMusicTheme
{
    public List<string> NeutralTrackPaths { get; set; } = new List<string>();

    public string StrongAdvantageTrackPath { get; set; }

    public string AdvantageTrackPath { get; set; }

    public string DisadvantageTrackPath { get; set; }

    public int NeutralTracksBetweenStrategicTracks { get; set; }

    public int PlanetRatioScale { get; set; }

    public int NoOpponentPlanetMultiplier { get; set; }

    public int StrongAdvantageMinimumRatio { get; set; }

    public int AdvantageMinimumRatio { get; set; }

    public int DisadvantageMaximumRatio { get; set; }
}
