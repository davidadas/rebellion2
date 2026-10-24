using System;
using UnityFullScreenMode = UnityEngine.FullScreenMode;

/// <summary>
/// Identifies a persisted tactical presentation option.
/// </summary>
public enum UserTacticalOption
{
    Starfield,

    Planet,

    Pyro,

    HighDetail,

    Holocube,
}

/// <summary>
/// Identifies a persisted graphics setting presented by the Graphics options page.
/// </summary>
public enum UserGraphicsSetting
{
    QualityPreset,

    AntiAliasing,

    VerticalSync,

    FrameRateLimit,

    RayTracing,

    AmbientOcclusion,

    Bloom,

    MotionBlur,

    HdrOutput,
}

/// <summary>
/// Identifies the anti-aliasing algorithm applied to primary scene cameras.
/// </summary>
public enum UserAntiAliasingMode
{
    Off,

    Fxaa,

    Smaa,

    Taa,
}

/// <summary>
/// Stores user-configurable video settings.
/// </summary>
[Serializable]
public sealed class UserVideoSettings
{
    public const int DefaultQualityLevel = 5;
    public int ResolutionWidth;
    public int ResolutionHeight;
    public int FullScreenMode = (int)UnityFullScreenMode.ExclusiveFullScreen;
    public int QualityLevel = DefaultQualityLevel;
    public bool GraphicsPresetCustomized;
    public int AntiAliasingMode = (int)UserAntiAliasingMode.Taa;
    public int FrameRateLimit;
    public bool VerticalSync = true;
    public bool RayTracing;
    public bool AmbientOcclusion = true;
    public bool Bloom = true;
    public bool MotionBlur = true;
    public bool HdrOutput;
    public bool ShowStarfield = true;
    public bool ShowPlanet = true;
    public bool ShowPyro = true;
    public bool HighDetail = true;
    public bool ShowHolocube = true;

    /// <summary>
    /// Normalizes persisted display values without replacing the native-resolution sentinel.
    /// </summary>
    public void Normalize()
    {
        if (
            (ResolutionWidth != 0 || ResolutionHeight != 0)
            && !DisplayManager.IsSixteenByNine(ResolutionWidth, ResolutionHeight)
        )
        {
            ResolutionWidth = 0;
            ResolutionHeight = 0;
        }
        if (!Enum.IsDefined(typeof(UnityFullScreenMode), FullScreenMode))
            FullScreenMode = (int)UnityFullScreenMode.ExclusiveFullScreen;
        QualityLevel = Math.Clamp(QualityLevel, 0, DefaultQualityLevel);
        if (!Enum.IsDefined(typeof(UserAntiAliasingMode), AntiAliasingMode))
            AntiAliasingMode = (int)UserAntiAliasingMode.Off;
        FrameRateLimit = NormalizeFrameRateLimit(FrameRateLimit);
        if (!AmbientOcclusion)
            RayTracing = false;
    }

    /// <summary>
    /// Gets whether a tactical presentation option is enabled.
    /// </summary>
    /// <param name="option">The tactical presentation option.</param>
    /// <returns>True when the option is enabled.</returns>
    public bool IsEnabled(UserTacticalOption option)
    {
        return option switch
        {
            UserTacticalOption.Starfield => ShowStarfield,
            UserTacticalOption.Planet => ShowPlanet,
            UserTacticalOption.Pyro => ShowPyro,
            UserTacticalOption.HighDetail => HighDetail,
            UserTacticalOption.Holocube => ShowHolocube,
            _ => throw new ArgumentOutOfRangeException(nameof(option), option, null),
        };
    }

    /// <summary>
    /// Sets whether a tactical presentation option is enabled.
    /// </summary>
    /// <param name="option">The tactical presentation option.</param>
    /// <param name="enabled">Whether the option is enabled.</param>
    public void SetEnabled(UserTacticalOption option, bool enabled)
    {
        switch (option)
        {
            case UserTacticalOption.Starfield:
                ShowStarfield = enabled;
                break;
            case UserTacticalOption.Planet:
                ShowPlanet = enabled;
                break;
            case UserTacticalOption.Pyro:
                ShowPyro = enabled;
                break;
            case UserTacticalOption.HighDetail:
                HighDetail = enabled;
                break;
            case UserTacticalOption.Holocube:
                ShowHolocube = enabled;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(option), option, null);
        }
    }

    /// <summary>
    /// Restores the tactical presentation options to their enabled defaults.
    /// </summary>
    public void RestoreTacticalDefaults()
    {
        ShowStarfield = true;
        ShowPlanet = true;
        ShowPyro = true;
        HighDetail = true;
        ShowHolocube = true;
    }

    /// <summary>
    /// Restores graphics-quality settings without changing the selected display mode.
    /// </summary>
    public void RestoreGraphicsDefaults()
    {
        ApplyGraphicsPreset(DefaultQualityLevel);
        FrameRateLimit = 0;
        VerticalSync = true;
        RayTracing = false;
        AmbientOcclusion = true;
        Bloom = true;
        MotionBlur = true;
        HdrOutput = false;
    }

    /// <summary>
    /// Applies the subordinate rendering values owned by one quality preset.
    /// </summary>
    /// <param name="qualityLevel">The zero-based project quality tier.</param>
    public void ApplyGraphicsPreset(int qualityLevel)
    {
        QualityLevel = Math.Clamp(qualityLevel, 0, DefaultQualityLevel);
        switch (QualityLevel)
        {
            case 0:
                AntiAliasingMode = (int)UserAntiAliasingMode.Off;
                AmbientOcclusion = false;
                Bloom = false;
                MotionBlur = false;
                break;
            case 1:
                AntiAliasingMode = (int)UserAntiAliasingMode.Fxaa;
                AmbientOcclusion = false;
                Bloom = true;
                MotionBlur = false;
                break;
            case 2:
                AntiAliasingMode = (int)UserAntiAliasingMode.Fxaa;
                AmbientOcclusion = true;
                Bloom = true;
                MotionBlur = false;
                break;
            case 3:
                AntiAliasingMode = (int)UserAntiAliasingMode.Smaa;
                AmbientOcclusion = true;
                Bloom = true;
                MotionBlur = false;
                break;
            case 4:
                AntiAliasingMode = (int)UserAntiAliasingMode.Taa;
                AmbientOcclusion = true;
                Bloom = true;
                MotionBlur = false;
                break;
            default:
                AntiAliasingMode = (int)UserAntiAliasingMode.Taa;
                AmbientOcclusion = true;
                Bloom = true;
                MotionBlur = true;
                break;
        }

        if (!AmbientOcclusion)
            RayTracing = false;
        GraphicsPresetCustomized = false;
    }

    /// <summary>
    /// Normalizes a persisted frame-rate limit to a supported value.
    /// </summary>
    /// <param name="frameRateLimit">The persisted frame-rate limit.</param>
    /// <returns>The supported frame-rate limit, or zero for unlimited.</returns>
    private static int NormalizeFrameRateLimit(int frameRateLimit)
    {
        return frameRateLimit switch
        {
            30 => 30,
            60 => 60,
            120 => 120,
            144 => 144,
            165 => 165,
            240 => 240,
            _ => 0,
        };
    }
}
