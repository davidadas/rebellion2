using System;

/// <summary>
/// The player's content selection — which pack and scenario to load. Kept in user
/// settings (persistentDataPath), not the patcher-managed <c>catalog.xml</c>, so a
/// content update never resets the player's choice. Empty values fall back to the
/// catalog defaults.
/// </summary>
[Serializable]
public sealed class UserContentSettings
{
    /// <summary>Selected content pack ID; empty means use the catalog default.</summary>
    public string ActivePackID = "";

    /// <summary>Selected scenario ID; empty means use the pack/catalog default.</summary>
    public string ActiveScenarioID = "";

    /// <summary>Ensures fields are non-null.</summary>
    public void Normalize()
    {
        ActivePackID ??= "";
        ActiveScenarioID ??= "";
    }
}
