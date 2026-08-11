using Rebellion.Util.Serialization;

/// <summary>
/// Defines the faction-specific artwork and shared asset root for tactical space battles.
/// </summary>
[PersistableObject]
public sealed class TacticalBattleTheme
{
    public string SharedUIRoot { get; set; }

    public string SharedEffectsRoot { get; set; }

    public string TaskForceHeaderImagePath { get; set; }

    public string FighterGroupHeaderImagePath { get; set; }

    public string FighterOrderVariant { get; set; }

    public string CapitalShipArrivalAudioPath { get; set; }

    public string CapitalShipWithdrawalAudioPath { get; set; }

    public string FighterArrivalAudioPath { get; set; }

    public string FighterWithdrawalAudioPath { get; set; }

    public string LaserCannonFireAudioPath { get; set; }

    public string FighterLaserCannonFireAudioPath { get; set; }

    public string TurbolaserFireAudioPath { get; set; }

    public string IonCannonFireAudioPath { get; set; }

    public string FighterIonCannonFireAudioPath { get; set; }

    public string TorpedoFireAudioPath { get; set; }

    public string TractorLockAudioPath { get; set; }

    public string TractorReleaseAudioPath { get; set; }

    public string SuperlaserAudioPath { get; set; }

    public string EnergyShieldHitAudioPath { get; set; }

    public string EnergyShieldPenetrationAudioPath { get; set; }

    public string ProjectileShieldHitAudioPath { get; set; }

    public string ProjectileShieldPenetrationAudioPath { get; set; }

    public string IonShieldHitAudioPath { get; set; }

    public string IonShieldPenetrationAudioPath { get; set; }

    public string LeftShipHighlightFactionInstanceID { get; set; }

    public string LeftShipHighlightColorHex { get; set; }

    public string RightShipHighlightFactionInstanceID { get; set; }

    public string RightShipHighlightColorHex { get; set; }

    public float InitialCameraYaw { get; set; }
}
