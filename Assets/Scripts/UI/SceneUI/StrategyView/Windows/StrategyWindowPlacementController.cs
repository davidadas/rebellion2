using UnityEngine;

public sealed class StrategyWindowPlacementController
{
    private readonly StrategyWindowLayerView windowLayer;
    private UIContext uiContext;

    public StrategyWindowPlacementController(
        UIContext uiContext,
        StrategyWindowLayerView windowLayer
    )
    {
        this.uiContext = uiContext ?? throw new System.ArgumentNullException(nameof(uiContext));
        this.windowLayer =
            windowLayer ?? throw new System.ArgumentNullException(nameof(windowLayer));
    }

    public void SetContext(UIContext uiContext)
    {
        this.uiContext = uiContext ?? throw new System.ArgumentNullException(nameof(uiContext));
    }

    public Vector2Int GetSectorWindowPosition(int side)
    {
        if (side == SectorWindowPositions.Left)
            return GetPlacement(GetPlacements().SectorLeftPosition, "SectorLeftPosition");

        return GetPlacement(GetPlacements().SectorRightPosition, "SectorRightPosition");
    }

    public Vector2Int GetSectorWindowOpenThresholds()
    {
        int rightX = GetSectorWindowPosition(SectorWindowPositions.Right).x;
        return new Vector2Int(
            rightX + windowLayer.SectorLeftOpenThresholdOffset,
            rightX + windowLayer.SectorRightOpenThresholdOffset
        );
    }

    public Vector2Int GetUtilityWindowPosition()
    {
        return GetPlacement(GetPlacements().UtilityWindowPosition, "UtilityWindowPosition");
    }

    public Vector2Int GetStatusWindowPosition()
    {
        return GetPlacement(GetPlacements().StatusWindowPosition, "StatusWindowPosition");
    }

    public Vector2Int GetConfirmWindowPosition()
    {
        return GetPlacement(GetPlacements().ConfirmWindowPosition, "ConfirmWindowPosition");
    }

    public Vector2Int GetMissionCreateWindowPosition()
    {
        Vector2Int windowSize = windowLayer.GetMissionCreateWindowSize();
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

    public Vector2Int GetConstructionWindowPosition(int sourceX, int sourceY)
    {
        Vector2Int offset = windowLayer.ConstructionWindowOffset;
        return windowLayer.ClampConstructionWindowPosition(sourceX + offset.x, sourceY + offset.y);
    }

    public Vector2Int ClampPlanetWindowPosition(PlanetIcon icon, int sourceX, int sourceY)
    {
        return windowLayer.ClampPlanetWindowPosition(icon, sourceX, sourceY);
    }

    private StrategyWindowPlacements GetPlacements()
    {
        StrategyWindowPlacements placements = uiContext
            ?.GetPlayerFactionTheme()
            ?.StrategyWindowPlacements;
        if (placements == null)
            throw new MissingReferenceException("Player StrategyWindowPlacements are missing.");

        return placements;
    }

    private static Vector2Int GetPlacement(SourcePointLayout layout, string name)
    {
        if (layout == null)
            throw new MissingReferenceException($"StrategyWindowPlacements/{name} is missing.");

        return layout.ToVector2Int();
    }
}
