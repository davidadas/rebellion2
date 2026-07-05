using System;

/// <summary>
/// Stores user-configurable audio settings.
/// </summary>
[Serializable]
public sealed class UserAudioSettings
{
    public float MasterVolume = 1f;
    public float MusicVolume = 1f;
    public float SfxVolume = 1f;
    public float AmbienceVolume = 1f;

    /// <summary>
    /// Clamps volume settings to valid values.
    /// </summary>
    public void Normalize()
    {
        MasterVolume = Clamp01(MasterVolume);
        MusicVolume = Clamp01(MusicVolume);
        SfxVolume = Clamp01(SfxVolume);
        AmbienceVolume = Clamp01(AmbienceVolume);
    }

    /// <summary>
    /// Clamps a value to the valid volume range.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <returns>The clamped value.</returns>
    private static float Clamp01(float value)
    {
        if (value < 0f)
            return 0f;
        if (value > 1f)
            return 1f;

        return value;
    }
}
