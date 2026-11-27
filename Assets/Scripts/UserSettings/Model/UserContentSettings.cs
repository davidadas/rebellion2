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
    public bool HasModLoadOrderPreferences;
    public ContentModOrderPreference[] ModLoadOrders = Array.Empty<ContentModOrderPreference>();

    /// <summary>
    /// Gets the last applied order for a pack, or null when the legacy order must be migrated.
    /// </summary>
    /// <param name="packID">The base pack identity.</param>
    /// <returns>The ordered mod IDs. An empty list indicates a pack without a saved order.</returns>
    public IReadOnlyList<string> GetModLoadOrder(string packID) =>
        !HasModLoadOrderPreferences
            ? null
            : ModLoadOrders?.FirstOrDefault(order => order?.PackID == packID)?.ModIDs
                ?? Array.Empty<string>();

    /// <summary>
    /// Gets the applied mod versions in load order, or null for preferences from older releases.
    /// </summary>
    /// <param name="packID">The selected base pack.</param>
    /// <returns>The recorded versions for the applied stack.</returns>
    public IReadOnlyList<string> GetModVersions(string packID) =>
        ModLoadOrders?.FirstOrDefault(order => order?.PackID == packID)?.ModVersions;

    /// <summary>
    /// Records the successfully applied mod order for one pack.
    /// </summary>
    /// <param name="packID">The base pack identity.</param>
    /// <param name="modIDs">The active mod IDs in application order.</param>
    /// <param name="modVersions">The applied versions in the same order.</param>
    public void SetModLoadOrder(
        string packID,
        IEnumerable<string> modIDs,
        IEnumerable<string> modVersions = null
    )
    {
        HasModLoadOrderPreferences = true;
        var preference = new ContentModOrderPreference
        {
            PackID = packID,
            ModIDs = modIDs.ToArray(),
            ModVersions = modVersions?.ToArray(),
        };
        ModLoadOrders = (ModLoadOrders ?? Array.Empty<ContentModOrderPreference>())
            .Where(order => order != null && order.PackID != packID)
            .Append(preference)
            .ToArray();
    }

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
        ModLoadOrders ??= Array.Empty<ContentModOrderPreference>();
    }
}

/// <summary>
/// Stores the installed mods in the last applied order for one base pack.
/// </summary>
[Serializable]
public sealed class ContentModOrderPreference
{
    public string PackID;
    public string[] ModIDs;
    public string[] ModVersions;
}
