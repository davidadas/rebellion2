using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Identifies the presentation mode currently rendered by the battle-alert window.
/// </summary>
internal enum BattleAlertWindowMode
{
    Hidden,
    Pending,
    Result,
}

/// <summary>
/// Identifies a pending battle-alert panel selected by the player.
/// </summary>
internal enum BattleAlertPanel
{
    Summary,
    FirstForces,
    SecondForces,
    SystemAssets,
}

/// <summary>
/// Identifies a completed battle-result panel selected by the player.
/// </summary>
internal enum BattleResultPanel
{
    Summary,
    FirstForces,
    SecondForces,
    Direct,
}

/// <summary>
/// Identifies a unit category available in a completed battle result.
/// </summary>
internal enum BattleResultCategory
{
    CapitalShips,
    Starfighters,
    Manufacturing,
    Defense,
    Troops,
    Personnel,
}

/// <summary>
/// Defines authored primary-panel ordering and pending-to-result panel semantics.
/// </summary>
internal static class BattleAlertPanelCatalog
{
    private static readonly IReadOnlyList<BattleAlertPanel> _panels = Array.AsReadOnly(
        new[]
        {
            BattleAlertPanel.Summary,
            BattleAlertPanel.FirstForces,
            BattleAlertPanel.SecondForces,
            BattleAlertPanel.SystemAssets,
        }
    );

    /// <summary>
    /// Returns primary battle-alert panels in authored display order.
    /// </summary>
    internal static IReadOnlyList<BattleAlertPanel> Ordered => _panels;

    /// <summary>
    /// Maps a primary battle-alert panel to its completed-result counterpart.
    /// </summary>
    /// <param name="panel">The primary panel.</param>
    /// <returns>The corresponding completed-result panel.</returns>
    internal static BattleResultPanel ToResultPanel(BattleAlertPanel panel)
    {
        return panel switch
        {
            BattleAlertPanel.FirstForces => BattleResultPanel.FirstForces,
            BattleAlertPanel.SecondForces => BattleResultPanel.SecondForces,
            BattleAlertPanel.SystemAssets => BattleResultPanel.Direct,
            _ => BattleResultPanel.Summary,
        };
    }
}

/// <summary>
/// Defines the authored display order for completed battle-result categories.
/// </summary>
internal static class BattleResultCategoryCatalog
{
    private static readonly IReadOnlyList<BattleResultCategory> _spaceCategories = Array.AsReadOnly(
        new[]
        {
            BattleResultCategory.CapitalShips,
            BattleResultCategory.Starfighters,
            BattleResultCategory.Troops,
            BattleResultCategory.Personnel,
        }
    );
    private static readonly IReadOnlyList<BattleResultCategory> _planetaryCategories =
        Array.AsReadOnly(
            new[]
            {
                BattleResultCategory.CapitalShips,
                BattleResultCategory.Starfighters,
                BattleResultCategory.Manufacturing,
                BattleResultCategory.Defense,
                BattleResultCategory.Troops,
                BattleResultCategory.Personnel,
            }
        );

    /// <summary>
    /// Returns completed battle-result categories in authored display order.
    /// </summary>
    internal static IReadOnlyList<BattleResultCategory> Ordered => _planetaryCategories;

    /// <summary>
    /// Returns the categories supported by one completed result.
    /// </summary>
    /// <param name="result">The completed result.</param>
    /// <returns>The supported categories in authored order.</returns>
    internal static IReadOnlyList<BattleResultCategory> GetForResult(
        BattleResultPresentation result
    )
    {
        return result?.UsesPlanetaryLayout == true ? _planetaryCategories : _spaceCategories;
    }
}

/// <summary>
/// Describes one themed button without exposing mutable theme configuration to the view.
/// </summary>
internal sealed class BattleAlertButtonRenderData
{
    /// <summary>
    /// Creates presentation data for one battle-alert button.
    /// </summary>
    /// <param name="interactable">Whether the button accepts input.</param>
    /// <param name="texture">The normal-state texture.</param>
    /// <param name="pressedTexture">The pressed-state texture.</param>
    /// <param name="bounds">The optional source-space button bounds.</param>
    public BattleAlertButtonRenderData(
        bool interactable,
        Texture2D texture,
        Texture2D pressedTexture,
        RectInt? bounds = null
    )
    {
        Interactable = interactable;
        Texture = texture;
        PressedTexture = pressedTexture;
        Bounds = bounds;
    }

    public RectInt? Bounds { get; }

    public bool Interactable { get; }

    public Texture2D Texture { get; }

    public Texture2D PressedTexture { get; }
}

/// <summary>
/// Describes one pending-combat list row.
/// </summary>
internal sealed class BattleAlertRowRenderData
{
    /// <summary>
    /// Creates presentation data for one pending-combat row.
    /// </summary>
    /// <param name="text">The displayed row label.</param>
    /// <param name="iconTexture">The optional row icon.</param>
    public BattleAlertRowRenderData(string text, Texture2D iconTexture)
    {
        Text = text ?? string.Empty;
        IconTexture = iconTexture;
    }

    public string Text { get; }

    public Texture2D IconTexture { get; }
}

/// <summary>
/// Describes one unit entry in a completed battle-result column.
/// </summary>
internal sealed class BattleResultItemRenderData
{
    /// <summary>
    /// Creates presentation data for one completed battle-result item.
    /// </summary>
    /// <param name="text">The displayed unit label.</param>
    /// <param name="baseTexture">The unit's base texture.</param>
    /// <param name="withdrawingOverlayTexture">The optional withdrawal overlay.</param>
    /// <param name="damagedOverlayTexture">The optional damage overlay.</param>
    /// <param name="capturedOverlayTexture">The optional captured-personnel overlay.</param>
    public BattleResultItemRenderData(
        string text,
        Texture2D baseTexture,
        Texture2D withdrawingOverlayTexture = null,
        Texture2D damagedOverlayTexture = null,
        Texture2D capturedOverlayTexture = null
    )
    {
        Text = text ?? string.Empty;
        BaseTexture = baseTexture;
        WithdrawingOverlayTexture = withdrawingOverlayTexture;
        DamagedOverlayTexture = damagedOverlayTexture;
        CapturedOverlayTexture = capturedOverlayTexture;
    }

    public string Text { get; }

    public Texture2D BaseTexture { get; }

    public Texture2D WithdrawingOverlayTexture { get; }

    public Texture2D DamagedOverlayTexture { get; }

    public Texture2D CapturedOverlayTexture { get; }
}

/// <summary>
/// Contains the two displayed columns for one completed battle-result category.
/// </summary>
internal sealed class BattleResultTableRenderData
{
    /// <summary>
    /// Creates immutable presentation data for the operational and destroyed columns.
    /// </summary>
    /// <param name="operational">The operational-column entries.</param>
    /// <param name="destroyed">The destroyed-column entries.</param>
    public BattleResultTableRenderData(
        IEnumerable<BattleResultItemRenderData> operational,
        IEnumerable<BattleResultItemRenderData> destroyed
    )
    {
        Operational = (operational ?? Enumerable.Empty<BattleResultItemRenderData>())
            .ToList()
            .AsReadOnly();
        Destroyed = (destroyed ?? Enumerable.Empty<BattleResultItemRenderData>())
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<BattleResultItemRenderData> Operational { get; }

    public IReadOnlyList<BattleResultItemRenderData> Destroyed { get; }
}

/// <summary>
/// Associates a result category with its themed filter-button presentation.
/// </summary>
internal sealed class BattleResultCategoryRenderData
{
    /// <summary>
    /// Creates presentation data for one completed battle-result category.
    /// </summary>
    /// <param name="category">The represented result category.</param>
    /// <param name="button">The category button presentation.</param>
    public BattleResultCategoryRenderData(
        BattleResultCategory category,
        BattleAlertButtonRenderData button
    )
    {
        Category = category;
        Button = button ?? throw new ArgumentNullException(nameof(button));
    }

    public BattleResultCategory Category { get; }

    public BattleAlertButtonRenderData Button { get; }
}

/// <summary>
/// Contains immutable presentation for one pending-combat panel.
/// </summary>
internal sealed class BattleAlertPendingRenderData
{
    /// <summary>
    /// Creates the presentation for one pending-combat panel.
    /// </summary>
    /// <param name="panel">The selected pending-combat panel.</param>
    /// <param name="title">The active title.</param>
    /// <param name="header">The active pending-list header.</param>
    /// <param name="summary">The active summary text.</param>
    /// <param name="rows">The pending-list rows.</param>
    /// <param name="commandButtons">The pending-combat command buttons.</param>
    public BattleAlertPendingRenderData(
        BattleAlertPanel panel,
        string title,
        string header,
        string summary,
        IEnumerable<BattleAlertRowRenderData> rows,
        IEnumerable<BattleAlertButtonRenderData> commandButtons
    )
    {
        Panel = panel;
        Title = title ?? string.Empty;
        Header = header ?? string.Empty;
        Summary = summary ?? string.Empty;
        Rows = (rows ?? Enumerable.Empty<BattleAlertRowRenderData>()).ToList().AsReadOnly();
        CommandButtons = (commandButtons ?? Enumerable.Empty<BattleAlertButtonRenderData>())
            .ToList()
            .AsReadOnly();
    }

    public BattleAlertPanel Panel { get; }

    public string Title { get; }

    public string Header { get; }

    public string Summary { get; }

    public IReadOnlyList<BattleAlertRowRenderData> Rows { get; }

    public IReadOnlyList<BattleAlertButtonRenderData> CommandButtons { get; }
}

/// <summary>
/// Contains the immutable presentation for one completed battle-result panel.
/// </summary>
internal sealed class BattleAlertResultRenderData
{
    /// <summary>
    /// Creates the presentation for one completed battle-result panel.
    /// </summary>
    /// <param name="panel">The selected completed-result panel.</param>
    /// <param name="category">The selected completed-result category.</param>
    /// <param name="title">The active result title.</param>
    /// <param name="summary">The active result summary.</param>
    /// <param name="resultCloseButton">The completed-result close button.</param>
    /// <param name="resultForceHeader">The completed-result force header.</param>
    /// <param name="resultForceHeaderColor">The completed-result force-header color.</param>
    /// <param name="resultTableTitle">The completed-result category title.</param>
    /// <param name="resultColumnHeaders">The completed-result column headers.</param>
    /// <param name="resultCategories">The completed-result filter buttons.</param>
    /// <param name="usesPlanetaryCategoryLayout">Whether the six-category planetary layout is active.</param>
    /// <param name="resultDirectButtons">The completed-result navigation buttons.</param>
    /// <param name="resultTable">The completed-result table.</param>
    public BattleAlertResultRenderData(
        BattleResultPanel panel,
        BattleResultCategory category,
        string title,
        string summary,
        BattleAlertButtonRenderData resultCloseButton,
        string resultForceHeader,
        Color resultForceHeaderColor,
        string resultTableTitle,
        IEnumerable<string> resultColumnHeaders,
        IEnumerable<BattleResultCategoryRenderData> resultCategories,
        bool usesPlanetaryCategoryLayout,
        IEnumerable<BattleAlertButtonRenderData> resultDirectButtons,
        BattleResultTableRenderData resultTable
    )
    {
        Panel = panel;
        Category = category;
        Title = title ?? string.Empty;
        Summary = summary ?? string.Empty;
        ResultCloseButton =
            resultCloseButton ?? throw new ArgumentNullException(nameof(resultCloseButton));
        ResultForceHeader = resultForceHeader ?? string.Empty;
        ResultForceHeaderColor = resultForceHeaderColor;
        ResultTableTitle = resultTableTitle ?? string.Empty;
        ResultColumnHeaders = (resultColumnHeaders ?? Enumerable.Empty<string>())
            .ToList()
            .AsReadOnly();
        ResultCategories = (resultCategories ?? Enumerable.Empty<BattleResultCategoryRenderData>())
            .ToList()
            .AsReadOnly();
        UsesPlanetaryCategoryLayout = usesPlanetaryCategoryLayout;
        ResultDirectButtons = (
            resultDirectButtons ?? Enumerable.Empty<BattleAlertButtonRenderData>()
        )
            .ToList()
            .AsReadOnly();
        ResultTable = resultTable;
    }

    public BattleResultPanel Panel { get; }

    public BattleResultCategory Category { get; }

    public string Title { get; }

    public string Summary { get; }

    public BattleAlertButtonRenderData ResultCloseButton { get; }

    public string ResultForceHeader { get; }

    public Color ResultForceHeaderColor { get; }

    public string ResultTableTitle { get; }

    public IReadOnlyList<string> ResultColumnHeaders { get; }

    public IReadOnlyList<BattleResultCategoryRenderData> ResultCategories { get; }

    public bool UsesPlanetaryCategoryLayout { get; }

    public IReadOnlyList<BattleAlertButtonRenderData> ResultDirectButtons { get; }

    public BattleResultTableRenderData ResultTable { get; }

    public bool UsesPersonnelColumns => Category == BattleResultCategory.Personnel;
}

/// <summary>
/// Contains the complete immutable presentation for one battle-alert window render.
/// </summary>
internal sealed class BattleAlertWindowRenderData
{
    /// <summary>
    /// Creates one complete battle-alert window presentation.
    /// </summary>
    /// <param name="mode">The window mode to display.</param>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <param name="backgroundTexture">The active panel background texture.</param>
    /// <param name="frameTexture">The active window frame texture.</param>
    /// <param name="titleColor">The faction-themed title color.</param>
    /// <param name="viewButtons">The four primary panel buttons.</param>
    /// <param name="pending">The pending-combat presentation, when active.</param>
    /// <param name="result">The completed battle-result presentation, when active.</param>
    public BattleAlertWindowRenderData(
        BattleAlertWindowMode mode,
        int x,
        int y,
        Texture2D backgroundTexture,
        Texture2D frameTexture,
        Color titleColor,
        IEnumerable<BattleAlertButtonRenderData> viewButtons,
        BattleAlertPendingRenderData pending,
        BattleAlertResultRenderData result
    )
    {
        if (mode == BattleAlertWindowMode.Pending && pending == null)
            throw new ArgumentNullException(nameof(pending));
        if (mode == BattleAlertWindowMode.Result && result == null)
            throw new ArgumentNullException(nameof(result));

        Mode = mode;
        X = x;
        Y = y;
        BackgroundTexture = backgroundTexture;
        FrameTexture = frameTexture;
        TitleColor = titleColor;
        ViewButtons = (viewButtons ?? Enumerable.Empty<BattleAlertButtonRenderData>())
            .ToList()
            .AsReadOnly();
        Pending = pending;
        Result = result;
    }

    public BattleAlertWindowMode Mode { get; }

    public int X { get; }

    public int Y { get; }

    public Texture2D BackgroundTexture { get; }

    public Texture2D FrameTexture { get; }

    public Color TitleColor { get; }

    public IReadOnlyList<BattleAlertButtonRenderData> ViewButtons { get; }

    public BattleAlertPendingRenderData Pending { get; }

    public BattleAlertResultRenderData Result { get; }
}
