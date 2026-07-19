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

    public FinderWindowTabKind Kind { get; }

    public bool IsAll => Kind == FinderWindowTabKind.All;

    public bool IsNeutral => Kind == FinderWindowTabKind.Neutral;

    public bool IsUnexplored => Kind == FinderWindowTabKind.Unexplored;

    public string FactionInstanceId { get; }

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
