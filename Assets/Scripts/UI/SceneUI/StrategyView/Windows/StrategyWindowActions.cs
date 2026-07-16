/// <summary>
/// Defines semantic actions emitted by shared strategy window chrome.
/// </summary>
public static class StrategyWindowButtonActions
{
    /// <summary>
    /// Opens the sector containing the represented planet.
    /// </summary>
    public const int OpenSector = 6;

    /// <summary>
    /// Closes the requesting window.
    /// </summary>
    public const int CloseWindow = 8;

    /// <summary>
    /// Moves a sector window to its next authored slot.
    /// </summary>
    public const int SwapWindow = 9;

    /// <summary>
    /// Minimizes a planet window into a bookmark.
    /// </summary>
    public const int MinimizeWindow = 10;
}

/// <summary>
/// Defines the authored planet-system window slots.
/// </summary>
public static class SectorWindowPositions
{
    /// <summary>
    /// Identifies the left sector-window slot.
    /// </summary>
    public const int Left = 0;

    /// <summary>
    /// Identifies the middle sector-window slot.
    /// </summary>
    public const int Middle = 1;

    /// <summary>
    /// Identifies the right sector-window slot.
    /// </summary>
    public const int Right = 2;
}

/// <summary>
/// Defines semantic actions emitted by authored strategy dialogs.
/// </summary>
public static class StrategyDialogButtonActions
{
    /// <summary>
    /// Closes the requesting dialog.
    /// </summary>
    public const int Close = 952;

    /// <summary>
    /// Opens the ship Finder.
    /// </summary>
    public const int ShipFinder = 954;

    /// <summary>
    /// Opens the fleet Finder.
    /// </summary>
    public const int FleetFinder = 955;

    /// <summary>
    /// Opens the personnel Finder.
    /// </summary>
    public const int PersonnelFinder = 956;

    /// <summary>
    /// Opens the special-forces Finder.
    /// </summary>
    public const int SpecForcesFinder = 957;

    /// <summary>
    /// Displays the selected Encyclopedia topic.
    /// </summary>
    public const int EncyclopediaTopic = 958;

    /// <summary>
    /// Displays the Encyclopedia index.
    /// </summary>
    public const int EncyclopediaIndex = 959;
}
