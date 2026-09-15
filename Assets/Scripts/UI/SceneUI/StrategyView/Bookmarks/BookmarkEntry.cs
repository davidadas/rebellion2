using Rebellion.Game.UIState;

/// <summary>
/// Stores one controller-owned bookmark and its current galaxy-map projection.
/// </summary>
public sealed class BookmarkEntry
{
    public BookmarkedItem State { get; }

    public PlanetIcon Icon { get; }

    public int X { get; }

    public int Y { get; }

    public GalaxyMapPlanet Planet { get; private set; }

    /// <summary>
    /// Creates a bookmark for one planet feature window.
    /// </summary>
    /// <param name="icon">The bookmarked feature category.</param>
    /// <param name="x">The source-space horizontal window coordinate.</param>
    /// <param name="y">The source-space vertical window coordinate.</param>
    /// <param name="planet">The bookmarked galaxy-map planet.</param>
    public BookmarkEntry(PlanetIcon icon, int x, int y, GalaxyMapPlanet planet)
        : this(
            new BookmarkedItem
            {
                TargetInstanceID = planet?.Planet?.InstanceID,
                ItemTypeID = icon.ToString(),
                X = x,
                Y = y,
            },
            planet
        ) { }

    /// <summary>
    /// Creates a projected entry for persisted bookmark state.
    /// </summary>
    /// <param name="state">The durable bookmark state.</param>
    /// <param name="planet">The current galaxy-map planet projection.</param>
    public BookmarkEntry(BookmarkedItem state, GalaxyMapPlanet planet = null)
    {
        State = state;
        Icon = ToPlanetIcon(state.ItemTypeID);
        X = state.X;
        Y = state.Y;
        Planet = planet;
    }

    /// <summary>
    /// Converts a durable item type into its strategy planet icon.
    /// </summary>
    /// <param name="itemTypeID">The durable item type identifier.</param>
    /// <returns>The matching strategy planet icon.</returns>
    private static PlanetIcon ToPlanetIcon(string itemTypeID) =>
        itemTypeID switch
        {
            nameof(PlanetIcon.Facility) => PlanetIcon.Facility,
            nameof(PlanetIcon.Defense) => PlanetIcon.Defense,
            nameof(PlanetIcon.Fleet) => PlanetIcon.Fleet,
            nameof(PlanetIcon.Mission) => PlanetIcon.Mission,
            _ => throw new System.ArgumentOutOfRangeException(nameof(itemTypeID)),
        };

    /// <summary>
    /// Replaces the stale galaxy-map projection while preserving bookmark identity and placement.
    /// </summary>
    /// <param name="planet">The fresh galaxy-map planet projection.</param>
    public void ReconcilePlanet(GalaxyMapPlanet planet)
    {
        Planet = planet;
    }
}
