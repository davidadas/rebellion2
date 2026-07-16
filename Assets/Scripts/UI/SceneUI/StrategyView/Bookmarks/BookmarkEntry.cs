/// <summary>
/// Stores one controller-owned bookmark and its current galaxy-map projection.
/// </summary>
public sealed class BookmarkEntry
{
    /// <summary>
    /// Creates a bookmark for one planet feature window.
    /// </summary>
    /// <param name="icon">The bookmarked feature category.</param>
    /// <param name="x">The source-space horizontal window coordinate.</param>
    /// <param name="y">The source-space vertical window coordinate.</param>
    /// <param name="planet">The bookmarked galaxy-map planet.</param>
    public BookmarkEntry(PlanetIcon icon, int x, int y, GalaxyMapPlanet planet)
    {
        Icon = icon;
        X = x;
        Y = y;
        Planet = planet;
    }

    /// <summary>
    /// Gets the icon.
    /// </summary>
    public PlanetIcon Icon { get; }

    /// <summary>
    /// Gets the horizontal coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the vertical coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets or sets the planet.
    /// </summary>
    public GalaxyMapPlanet Planet { get; private set; }

    /// <summary>
    /// Replaces the stale galaxy-map projection while preserving bookmark identity and placement.
    /// </summary>
    /// <param name="planet">The fresh galaxy-map planet projection.</param>
    public void ReconcilePlanet(GalaxyMapPlanet planet)
    {
        Planet = planet;
    }
}
