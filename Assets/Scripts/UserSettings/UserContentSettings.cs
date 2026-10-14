using System;
using System.Collections.Generic;
using System.Linq;

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
    /// Replaces disabled-mod settings for one content pack while retaining settings belonging to
    /// other packs.
    /// </summary>
    /// <param name="availableModIDs">Every mod compatible with the selected pack.</param>
    /// <param name="disabledModIDs">The selected pack's disabled mods.</param>
    public void SetDisabledModsForPack(
        IEnumerable<string> availableModIDs,
        IEnumerable<string> disabledModIDs
    )
    {
        HashSet<string> available = new HashSet<string>(
            availableModIDs ?? Array.Empty<string>(),
            StringComparer.Ordinal
        );
        DisabledModIDs = (DisabledModIDs ?? Array.Empty<string>())
            .Where(id => !available.Contains(id))
            .Concat((disabledModIDs ?? Array.Empty<string>()).Where(id => available.Contains(id)))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

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
