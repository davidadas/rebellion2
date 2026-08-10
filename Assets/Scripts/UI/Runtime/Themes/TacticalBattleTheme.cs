using Rebellion.Util.Serialization;

/// <summary>
/// Defines the faction-specific artwork and shared asset root for tactical space battles.
/// </summary>
[PersistableObject]
public sealed class TacticalBattleTheme
{
    public string SharedUIRoot { get; set; }

    public string TaskForceHeaderImagePath { get; set; }

    public string FighterGroupHeaderImagePath { get; set; }

    public string FighterOrderVariant { get; set; }

    public float InitialCameraYaw { get; set; }
}
