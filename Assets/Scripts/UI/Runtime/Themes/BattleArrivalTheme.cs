using Rebellion.Util.Serialization;

/// <summary>
/// Defines the shared hyperspace arrival sound and effect colors.
/// </summary>
[PersistableObject]
public sealed class BattleArrivalTheme
{
    public string SoundPath { get; set; }
    public string TrailColorHex { get; set; }
    public string BurstColorHex { get; set; }
}
