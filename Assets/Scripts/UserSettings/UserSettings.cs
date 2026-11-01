using System;

/// <summary>
/// Stores user-configurable application settings.
/// </summary>
[Serializable]
public sealed class UserSettings
{
    public UserGameplaySettings Gameplay = new UserGameplaySettings();
    public UserInterfaceSettings UserInterface = new UserInterfaceSettings();
    public UserAudioSettings Audio = new UserAudioSettings();
    public UserVideoSettings Video = new UserVideoSettings();
    public UserInputSettings Input = new UserInputSettings();
    public UserContentSettings Content = new UserContentSettings();
    public UserAnalyticsSettings Analytics = new UserAnalyticsSettings();

    /// <summary>
    /// Ensures nested settings are present and normalized.
    /// </summary>
    public void Normalize()
    {
        Gameplay ??= new UserGameplaySettings();
        UserInterface ??= new UserInterfaceSettings();
        Audio ??= new UserAudioSettings();
        Video ??= new UserVideoSettings();
        Input ??= new UserInputSettings();
        Content ??= new UserContentSettings();
        Analytics ??= new UserAnalyticsSettings();

        Gameplay.Normalize();
        UserInterface.Normalize();
        Audio.Normalize();
        Video.Normalize();
        Content.Normalize();
        Analytics.Normalize();
    }
}
