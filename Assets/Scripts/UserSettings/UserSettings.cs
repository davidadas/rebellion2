using System;

/// <summary>
/// Stores user-configurable application settings.
/// </summary>
[Serializable]
public sealed class UserSettings
{
    public UserGameplaySettings Gameplay = new UserGameplaySettings();
    public UserAudioSettings Audio = new UserAudioSettings();
    public UserVideoSettings Video = new UserVideoSettings();
    public UserInputSettings Input = new UserInputSettings();
    public UserContentSettings Content = new UserContentSettings();

    /// <summary>
    /// Ensures nested settings are present and normalized.
    /// </summary>
    public void Normalize()
    {
        Gameplay ??= new UserGameplaySettings();
        Audio ??= new UserAudioSettings();
        Video ??= new UserVideoSettings();
        Input ??= new UserInputSettings();
        Content ??= new UserContentSettings();

        Gameplay.Normalize();
        Audio.Normalize();
        Video.Normalize();
        Content.Normalize();
    }
}
