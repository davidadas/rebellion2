using System.Collections.Generic;
using Rebellion.SceneGraph;

/// <summary>
/// Contains the domain projection used to build one status-window presentation.
/// </summary>
internal sealed class StrategyStatusInfo
{
    /// <summary>
    /// Gets or sets the owner faction ID.
    /// </summary>
    public string OwnerFactionId { get; set; }

    /// <summary>
    /// Gets or sets the header.
    /// </summary>
    public string Header { get; set; }

    /// <summary>
    /// Gets or sets the label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether center image is enabled.
    /// </summary>
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

    /// <summary>
    /// Gets the status image items.
    /// </summary>
    public List<ISceneNode> StatusImageItems { get; } = new List<ISceneNode>();
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
    Enroute,
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

    /// <summary>
    /// Gets the left.
    /// </summary>
    public string Left { get; }

    /// <summary>
    /// Gets the right.
    /// </summary>
    public string Right { get; }
}
