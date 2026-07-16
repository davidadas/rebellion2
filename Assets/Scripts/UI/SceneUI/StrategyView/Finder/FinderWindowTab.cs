/// <summary>
/// Identifies the semantic category represented by a Finder tab.
/// </summary>
public enum FinderWindowTabKind
{
    All,
    Faction,
    Neutral,
    Unexplored,
}

/// <summary>
/// Represents one immutable Finder tab and its optional faction identity.
/// </summary>
public sealed class FinderWindowTab
{
    /// <summary>
    /// Creates one Finder tab.
    /// </summary>
    /// <param name="kind">The semantic tab category.</param>
    /// <param name="factionInstanceId">The optional faction identifier.</param>
    /// <param name="factionDisplayName">The optional faction display name.</param>
    private FinderWindowTab(
        FinderWindowTabKind kind,
        string factionInstanceId,
        string factionDisplayName
    )
    {
        Kind = kind;
        FactionInstanceId = factionInstanceId;
        FactionDisplayName = factionDisplayName;
    }

    /// <summary>
    /// Gets the kind.
    /// </summary>
    public FinderWindowTabKind Kind { get; }

    /// <summary>
    /// Gets a value indicating whether this tab represents all results.
    /// </summary>
    public bool IsAll => Kind == FinderWindowTabKind.All;

    /// <summary>
    /// Gets a value indicating whether this tab represents neutral systems.
    /// </summary>
    public bool IsNeutral => Kind == FinderWindowTabKind.Neutral;

    /// <summary>
    /// Gets a value indicating whether this tab represents unexplored systems.
    /// </summary>
    public bool IsUnexplored => Kind == FinderWindowTabKind.Unexplored;

    /// <summary>
    /// Gets the faction instance ID.
    /// </summary>
    public string FactionInstanceId { get; }

    /// <summary>
    /// Gets the faction display name.
    /// </summary>
    public string FactionDisplayName { get; }

    /// <summary>
    /// Creates the all-results tab.
    /// </summary>
    /// <returns>The all-results tab.</returns>
    public static FinderWindowTab All()
    {
        return new FinderWindowTab(FinderWindowTabKind.All, null, null);
    }

    /// <summary>
    /// Creates the neutral-system tab.
    /// </summary>
    /// <returns>The neutral-system tab.</returns>
    public static FinderWindowTab Neutral()
    {
        return new FinderWindowTab(FinderWindowTabKind.Neutral, null, null);
    }

    /// <summary>
    /// Creates the unexplored-system tab.
    /// </summary>
    /// <returns>The unexplored-system tab.</returns>
    public static FinderWindowTab Unexplored()
    {
        return new FinderWindowTab(FinderWindowTabKind.Unexplored, null, null);
    }

    /// <summary>
    /// Creates a faction-results tab.
    /// </summary>
    /// <param name="factionInstanceId">The faction identifier.</param>
    /// <param name="factionDisplayName">The faction display name.</param>
    /// <returns>The configured faction tab.</returns>
    public static FinderWindowTab Faction(string factionInstanceId, string factionDisplayName)
    {
        return new FinderWindowTab(
            FinderWindowTabKind.Faction,
            factionInstanceId,
            factionDisplayName
        );
    }
}
