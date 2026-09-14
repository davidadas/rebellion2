using Rebellion.Game;

/// <summary>
/// Stores one controller-owned bookmark and its current galaxy-map projection.
/// </summary>
public sealed class BookmarkEntry
{
    public PlanetBookmark State { get; }

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
            new PlanetBookmark
            {
                PlanetInstanceID = planet?.Planet?.InstanceID,
                Type = ToBookmarkType(icon),
            },
            x,
            y,
            planet
        ) { }

    /// <summary>
    /// Creates a projected entry for persisted bookmark state.
    /// </summary>
    public BookmarkEntry(PlanetBookmark state, GalaxyMapPlanet planet = null)
        : this(state, 0, 0, planet) { }

    /// <summary>
    /// Creates a bookmark projection with its transient window placement.
    /// </summary>
    /// <param name="state">The durable bookmark state.</param>
    /// <param name="x">The source-space horizontal window coordinate.</param>
    /// <param name="y">The source-space vertical window coordinate.</param>
    /// <param name="planet">The current galaxy-map planet projection.</param>
    private BookmarkEntry(PlanetBookmark state, int x, int y, GalaxyMapPlanet planet)
    {
        State = state;
        Icon = ToPlanetIcon(state.Type);
        X = x;
        Y = y;
        Planet = planet;
    }

    /// <summary>
    /// Converts a strategy planet icon into its durable bookmark category.
    /// </summary>
    /// <param name="icon">The strategy planet icon.</param>
    /// <returns>The matching durable bookmark category.</returns>
    private static PlanetBookmarkType ToBookmarkType(PlanetIcon icon) =>
        icon switch
        {
            PlanetIcon.Facility => PlanetBookmarkType.Facility,
            PlanetIcon.Defense => PlanetBookmarkType.Defense,
            PlanetIcon.Fleet => PlanetBookmarkType.Fleet,
            PlanetIcon.Mission => PlanetBookmarkType.Mission,
            _ => throw new System.ArgumentOutOfRangeException(nameof(icon)),
        };

    /// <summary>
    /// Converts a durable bookmark category into its strategy planet icon.
    /// </summary>
    /// <param name="type">The durable bookmark category.</param>
    /// <returns>The matching strategy planet icon.</returns>
    private static PlanetIcon ToPlanetIcon(PlanetBookmarkType type) =>
        type switch
        {
            PlanetBookmarkType.Facility => PlanetIcon.Facility,
            PlanetBookmarkType.Defense => PlanetIcon.Defense,
            PlanetBookmarkType.Fleet => PlanetIcon.Fleet,
            PlanetBookmarkType.Mission => PlanetIcon.Mission,
            _ => throw new System.ArgumentOutOfRangeException(nameof(type)),
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
