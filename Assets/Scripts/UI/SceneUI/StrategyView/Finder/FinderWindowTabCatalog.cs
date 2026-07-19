using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;

/// <summary>
/// Defines the authoritative semantic tab order for every Finder category.
/// </summary>
public static class FinderWindowTabCatalog
{
    /// <summary>
    /// Creates the ordered tabs for one Finder category and faction roster.
    /// </summary>
    /// <param name="mode">The Finder category being projected.</param>
    /// <param name="factions">The factions available to the current game.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>The semantic tabs in authored display order.</returns>
    public static List<FinderWindowTab> Create(
        FinderMode mode,
        IReadOnlyList<Faction> factions,
        string playerFactionId
    )
    {
        List<FinderWindowTab> tabs = new List<FinderWindowTab>();
        if (mode is FinderMode.Systems or FinderMode.Fleets)
            tabs.Add(FinderWindowTab.All());

        tabs.AddRange(CreateFactionTabs(factions, playerFactionId));
        if (mode == FinderMode.Systems)
        {
            tabs.Add(FinderWindowTab.Neutral());
            tabs.Add(FinderWindowTab.Unexplored());
        }

        return tabs;
    }

    /// <summary>
    /// Creates playable faction tabs with the player faction first.
    /// </summary>
    /// <param name="factions">The factions available to the current game.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>The ordered playable-faction tabs.</returns>
    private static IEnumerable<FinderWindowTab> CreateFactionTabs(
        IReadOnlyList<Faction> factions,
        string playerFactionId
    )
    {
        return (factions ?? Array.Empty<Faction>())
            .Where(faction => faction != null && !string.IsNullOrEmpty(faction.InstanceID))
            .OrderBy(faction =>
                !string.Equals(faction.InstanceID, playerFactionId, StringComparison.Ordinal)
            )
            .Select(faction =>
                FinderWindowTab.Faction(faction.InstanceID, faction.GetDisplayName())
            );
    }
}
