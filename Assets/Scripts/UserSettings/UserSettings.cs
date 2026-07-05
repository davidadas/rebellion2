using System;

/// <summary>
/// Stores user-configurable application settings.
/// </summary>
[Serializable]
public sealed class UserSettings
{
    public int Version = 1;
    public UserAudioSettings Audio = new UserAudioSettings();
    public UserVideoSettings Video = new UserVideoSettings();
    public UserInputSettings Input = new UserInputSettings();

    /// <summary>
    /// Ensures nested settings are present and normalized.
    /// </summary>
    public void Normalize()
    {
        Audio ??= new UserAudioSettings();
        Video ??= new UserVideoSettings();
        Input ??= new UserInputSettings();

        Audio.Normalize();
    }
}
