using System;

/// <summary>
/// Stores user-configurable video settings.
/// </summary>
[Serializable]
public sealed class UserVideoSettings
{
    public int ResolutionWidth;
    public int ResolutionHeight;
    public int FullScreenMode;
}
