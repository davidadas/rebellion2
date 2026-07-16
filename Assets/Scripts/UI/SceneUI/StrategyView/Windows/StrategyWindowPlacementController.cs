using System;
using UnityEngine;

/// <summary>
/// Resolves faction-authored strategy window placement and movement geometry.
/// </summary>
public sealed class StrategyWindowPlacementController
{
    private readonly UIContext uiContext;
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;

    /// <summary>
    /// Creates a placement controller for the active presentation context and window layer.
    /// </summary>
    /// <param name="uiContext">The active strategy presentation context.</param>
    /// <param name="windowLayer">The authored strategy window layer.</param>
    /// <param name="windowManager">The authoritative strategy-window registry.</param>
    public StrategyWindowPlacementController(
        UIContext uiContext,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager
    )
    {
        this.uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        ApplyMovementBounds();
    }

    /// <summary>
    /// Gets the authored position for one sector-window slot.
    /// </summary>
    /// <param name="side">The sector-window slot identifier.</param>
    /// <returns>The source-space window position.</returns>
    public Vector2Int GetSectorWindowPosition(int side)
    {
        StrategyWindowPlacements placements = GetPlacements();
        return side switch
        {
            SectorWindowPositions.Left => GetPlacement(
                placements.SectorLeftPosition,
                "SectorLeftPosition"
            ),
            SectorWindowPositions.Middle => GetPlacement(
                placements.SectorMiddlePosition,
                "SectorMiddlePosition"
            ),
            SectorWindowPositions.Right => GetPlacement(
                placements.SectorRightPosition,
                "SectorRightPosition"
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(side),
                side,
                "Unknown sector-window position."
            ),
        };
    }

    /// <summary>
    /// Gets the authored utility-window position.
    /// </summary>
    /// <returns>The source-space utility-window position.</returns>
    public Vector2Int GetUtilityWindowPosition()
    {
        return GetPlacement(GetPlacements().UtilityWindowPosition, "UtilityWindowPosition");
    }

    /// <summary>
    /// Gets the centered messages-window position from its authored prefab geometry.
    /// </summary>
    /// <returns>The source-space messages-window position.</returns>
    public Vector2Int GetMessagesWindowPosition()
    {
        return GetCenteredWindowPosition(windowLayer.MessagesWindowPrefab);
    }

    /// <summary>
    /// Gets the centered Finder-window position from its authored prefab geometry.
    /// </summary>
    /// <returns>The source-space Finder-window position.</returns>
    public Vector2Int GetFinderWindowPosition()
    {
        return GetCenteredWindowPosition(windowLayer.FinderWindowPrefab);
    }

    /// <summary>
    /// Gets the centered status-window position from its authored prefab geometry.
    /// </summary>
    /// <returns>The source-space status-window position.</returns>
    public Vector2Int GetStatusWindowPosition()
    {
        return GetCenteredWindowPosition(windowLayer.StatusWindowPrefab);
    }

    /// <summary>
    /// Gets the centered advisor-report position from its authored prefab geometry.
    /// </summary>
    /// <returns>The source-space advisor-report position.</returns>
    public Vector2Int GetAdvisorReportWindowPosition()
    {
        return GetCenteredWindowPosition(windowLayer.AdvisorReportWindowPrefab);
    }

    /// <summary>
    /// Gets the centered Encyclopedia position from its authored prefab geometry.
    /// </summary>
    /// <returns>The source-space Encyclopedia position.</returns>
    public Vector2Int GetEncyclopediaWindowPosition()
    {
        return GetCenteredWindowPosition(windowLayer.EncyclopediaWindowPrefab);
    }

    /// <summary>
    /// Gets the centered confirmation-dialog position from its authored prefab geometry.
    /// </summary>
    /// <returns>The source-space confirmation-dialog position.</returns>
    public Vector2Int GetConfirmDialogWindowPosition()
    {
        return GetCenteredWindowPosition(windowLayer.ConfirmDialogWindowPrefab);
    }

    /// <summary>
    /// Gets the centered battle-alert position from its authored prefab geometry.
    /// </summary>
    /// <returns>The source-space battle-alert position.</returns>
    public Vector2Int GetBattleAlertWindowPosition()
    {
        return GetCenteredWindowPosition(windowLayer.BattleAlertWindowPrefab);
    }

    /// <summary>
    /// Gets the centered Mission Create position plus its faction-authored offset.
    /// </summary>
    /// <returns>The source-space Mission Create position.</returns>
    public Vector2Int GetMissionCreateWindowPosition()
    {
        Vector2Int windowSize = windowLayer.GetWindowSize(windowLayer.MissionCreateWindowPrefab);
        Vector2Int offset = GetPlacement(
            GetPlacements().MissionCreateOffset,
            "MissionCreateOffset"
        );
        Vector2Int surfaceSize = windowLayer.GetSurfaceSize();
        return new Vector2Int(
            Mathf.RoundToInt(surfaceSize.x / 2f - windowSize.x / 2f + offset.x),
            Mathf.RoundToInt(surfaceSize.y / 2f - windowSize.y / 2f + offset.y)
        );
    }

    /// <summary>
    /// Offsets and clamps a construction window from its source window.
    /// </summary>
    /// <param name="sourceX">The source window's horizontal position.</param>
    /// <param name="sourceY">The source window's vertical position.</param>
    /// <returns>The clamped construction-window position.</returns>
    public Vector2Int GetConstructionWindowPosition(int sourceX, int sourceY)
    {
        Vector2Int offset = windowLayer.ConstructionWindowOffset;
        return ClampWindowPosition(
            windowLayer.ConstructionWindowPrefab,
            sourceX + offset.x,
            sourceY + offset.y
        );
    }

    /// <summary>
    /// Clamps a planet window to its category-specific authored bounds.
    /// </summary>
    /// <param name="icon">The planet-window category.</param>
    /// <param name="sourceX">The requested horizontal position.</param>
    /// <param name="sourceY">The requested vertical position.</param>
    /// <returns>The clamped planet-window position.</returns>
    public Vector2Int ClampPlanetWindowPosition(PlanetIcon icon, int sourceX, int sourceY)
    {
        MonoBehaviour prefab = icon switch
        {
            PlanetIcon.Facility => windowLayer.FacilityWindowPrefab,
            PlanetIcon.Defense => windowLayer.DefenseWindowPrefab,
            PlanetIcon.Fleet => windowLayer.FleetWindowPrefab,
            PlanetIcon.Mission => windowLayer.MissionsWindowPrefab,
            _ => null,
        };
        return prefab == null
            ? new Vector2Int(sourceX, sourceY)
            : ClampWindowPosition(prefab, sourceX, sourceY);
    }

    /// <summary>
    /// Gets the active faction's required strategy window placements.
    /// </summary>
    /// <returns>The required placement configuration.</returns>
    private StrategyWindowPlacements GetPlacements()
    {
        StrategyWindowPlacements placements = uiContext
            .GetPlayerFactionTheme()
            ?.StrategyWindowPlacements;
        if (placements == null)
            throw new MissingReferenceException("Player StrategyWindowPlacements are missing.");

        return placements;
    }

    /// <summary>
    /// Applies the active faction's movement bounds to the authoritative window registry.
    /// </summary>
    private void ApplyMovementBounds()
    {
        windowManager.SetMovementBounds(GetWindowBounds());
    }

    /// <summary>
    /// Centers an authored window prefab within the configured movement bounds.
    /// </summary>
    /// <param name="prefab">The authored window prefab.</param>
    /// <returns>The centered source-space position.</returns>
    private Vector2Int GetCenteredWindowPosition(MonoBehaviour prefab)
    {
        Vector2Int windowSize = windowLayer.GetWindowSize(prefab);
        RectInt bounds = GetWindowBounds();
        return new Vector2Int(
            bounds.xMin + Mathf.RoundToInt((bounds.width - windowSize.x) / 2f),
            bounds.yMin + Mathf.RoundToInt((bounds.height - windowSize.y) / 2f)
        );
    }

    /// <summary>
    /// Clamps an authored window prefab to the configured desktop movement bounds.
    /// </summary>
    /// <param name="prefab">The authored window prefab.</param>
    /// <param name="sourceX">The requested source-space horizontal position.</param>
    /// <param name="sourceY">The requested source-space vertical position.</param>
    /// <returns>The clamped source-space position.</returns>
    private Vector2Int ClampWindowPosition(MonoBehaviour prefab, int sourceX, int sourceY)
    {
        return windowManager.ClampPosition(sourceX, sourceY, windowLayer.GetWindowSize(prefab));
    }

    /// <summary>
    /// Gets the active faction's required strategy-window movement bounds.
    /// </summary>
    /// <returns>The validated source-space movement bounds.</returns>
    private RectInt GetWindowBounds()
    {
        SourceRectLayout bounds = GetPlacements().WindowBounds;
        if (bounds == null)
            throw new MissingReferenceException(
                "StrategyWindowPlacements/WindowBounds is missing."
            );

        return new RectInt(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>
    /// Converts one required authored placement to source-space coordinates.
    /// </summary>
    /// <param name="layout">The authored point layout.</param>
    /// <param name="configurationName">The configuration name used in validation errors.</param>
    /// <returns>The source-space position.</returns>
    private static Vector2Int GetPlacement(SourcePointLayout layout, string configurationName)
    {
        if (layout == null)
        {
            throw new MissingReferenceException(
                $"StrategyWindowPlacements/{configurationName} is missing."
            );
        }

        return layout.ToVector2Int();
    }
}
