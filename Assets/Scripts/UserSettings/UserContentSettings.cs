using System;

/// <summary>
/// Stores the player's selected content pack and scenario.
/// </summary>
[Serializable]
public sealed class UserContentSettings
{
    public string ActivePackID = "";
    public string ActiveScenarioID = "";
    public string[] DisabledModIDs = Array.Empty<string>();

    /// <summary>
    /// Ensures selection fields are non-null.
    /// </summary>
    public void Normalize()
    {
        ActivePackID ??= "";
        ActiveScenarioID ??= "";
        DisabledModIDs ??= Array.Empty<string>();
    }
}
