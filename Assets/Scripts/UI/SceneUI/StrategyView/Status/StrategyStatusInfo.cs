using System.Collections.Generic;
using Rebellion.SceneGraph;

/// <summary>
/// Contains the domain projection used to build one status-window presentation.
/// </summary>
internal sealed class StrategyStatusInfo
{
    public string OwnerFactionId { get; set; }

    public string Header { get; set; }

    public string Label { get; set; }

    public bool CenterImage { get; set; }

    /// <summary>
    /// Gets the images.
    /// </summary>
    public List<StatusWindowImage> Images { get; } = new List<StatusWindowImage>();

    /// <summary>
    /// Gets the image items.
    /// </summary>
    public List<ISceneNode> ImageItems { get; } = new List<ISceneNode>();

    /// <summary>
    /// Gets the overlay image items.
    /// </summary>
    public List<ISceneNode> OverlayImageItems { get; } = new List<ISceneNode>();

    /// <summary>
    /// Gets the rows.
    /// </summary>
    public List<StrategyStatusRow> Rows { get; } = new List<StrategyStatusRow>();
}

/// <summary>
/// Identifies a themed image role in the status-window image stack.
/// </summary>
internal enum StatusWindowImage
{
    Shipyard,
    Construction,
    Training,
    FleetBanner,
    FleetBannerEnroute,
    FleetBannerDamaged,
}

/// <summary>
/// Contains one immutable pair of status values before visual text wrapping.
/// </summary>
public sealed class StrategyStatusRow
{
    /// <summary>
    /// Creates one paired status value.
    /// </summary>
    /// <param name="left">The left-column value.</param>
    /// <param name="right">The right-column value.</param>
    public StrategyStatusRow(string left, string right)
    {
        Left = left ?? string.Empty;
        Right = right ?? string.Empty;
    }

    public string Left { get; }

    public string Right { get; }
}
