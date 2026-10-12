using Rebellion.Util.Serialization;

/// <summary>Faction-specific presentation colors for the tactical battle dock.</summary>
[PersistableObject]
public sealed class BattleHudTheme
{
    public string HousingColorHex { get; set; } = "#10151C";
    public string KeyColorHex { get; set; } = "#202A36";
    public string ContactColorHex { get; set; } = "#8CD9B8";
}
