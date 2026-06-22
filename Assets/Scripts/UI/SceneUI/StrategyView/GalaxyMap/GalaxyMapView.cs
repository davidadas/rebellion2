using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class GalaxyMapView : MonoBehaviour
{
    [SerializeField]
    private RectTransform background;

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RectTransform planetSystemClusters;

    [SerializeField]
    private PlanetSystemClusterView planetSystemClusterPrefab;

    private readonly Dictionary<string, PlanetSystemClusterView> clusterViews = new Dictionary<
        string,
        PlanetSystemClusterView
    >(StringComparer.Ordinal);
    private readonly List<string> visibleClusterKeys = new List<string>();
    private UIContext uiContext;
    private PlanetSystem hoveredSystem;

    public RectTransform Background => background;
    public RectTransform PlanetSystemClusters => planetSystemClusters;

    public void Initialize(UIContext uiContext)
    {
        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));

        this.uiContext = uiContext;
        foreach (PlanetSystemClusterView view in clusterViews.Values)
            view.Initialize(uiContext);
    }

    internal void Render(IReadOnlyList<GalaxyMapSector> sectors, string playerFactionId)
    {
        VerifyReferences();
        Vector2Int starOffset = GetStarOffset();
        Vector2Int backgroundPosition = GetBackgroundPosition();
        RenderBackground(backgroundPosition.x, backgroundPosition.y);
        List<GalaxyMapSystemCluster> clusters = CreateClusters(
            starOffset,
            sectors,
            playerFactionId
        );
        RenderClusters(clusters);
    }

    internal Vector2Int GetSystemSourcePosition(PlanetSystem system)
    {
        if (system == null)
            return Vector2Int.zero;

        Vector2Int offset = GetStarOffset();
        return new Vector2Int(
            offset.x + GetGalaxyCoordinateX(system.PositionX),
            offset.y + GetGalaxyCoordinateY(system.PositionY)
        );
    }

    private void Awake()
    {
        VerifyReferences();
    }

    private void RenderBackground(int galaxyX, int galaxyY)
    {
        backgroundImage.raycastTarget = false;
        backgroundImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        Texture2D texture = uiContext?.GetTexture(
            uiContext.GetPlayerFactionTheme()?.GalaxyBackground?.ImagePath
        );
        backgroundImage.texture = texture;
        backgroundImage.enabled = texture != null;

        if (texture != null)
            SetSourceRect(background, galaxyX, galaxyY, texture.width, texture.height);
    }

    private void RenderClusters(IReadOnlyList<GalaxyMapSystemCluster> clusters)
    {
        visibleClusterKeys.Clear();

        if (clusters != null)
        {
            for (int i = 0; i < clusters.Count; i++)
            {
                GalaxyMapSystemCluster cluster = clusters[i];
                visibleClusterKeys.Add(cluster.Key);
                PlanetSystemClusterView clusterView = GetClusterView(cluster.Key);
                clusterView.Render(
                    cluster.System,
                    cluster.Stars,
                    cluster.X,
                    cluster.Y,
                    cluster.Label,
                    cluster.ShowLabel || cluster.System == hoveredSystem
                );
                clusterView.gameObject.SetActive(true);
            }
        }

        foreach (KeyValuePair<string, PlanetSystemClusterView> entry in clusterViews)
        {
            if (!visibleClusterKeys.Contains(entry.Key))
                entry.Value.gameObject.SetActive(false);
        }
    }

    private static List<GalaxyMapSystemCluster> CreateClusters(
        Vector2Int offset,
        IReadOnlyList<GalaxyMapSector> sectors,
        string playerFactionId
    )
    {
        List<GalaxyMapSystemCluster> clusters = new List<GalaxyMapSystemCluster>();
        if (sectors == null)
            return clusters;

        foreach (GalaxyMapSector sector in sectors)
        {
            List<GalaxyMapSystemStar> stars = new List<GalaxyMapSystemStar>();
            foreach (GalaxyMapPlanet planet in sector.Planets)
            {
                stars.Add(
                    new GalaxyMapSystemStar(
                        offset.x + GetGalaxyCoordinateX(planet.Planet.PositionX),
                        offset.y + GetGalaxyCoordinateY(planet.Planet.PositionY),
                        planet.Planet.OwnerInstanceID,
                        GetPlayerSupport(planet.Planet, playerFactionId),
                        planet.Planet.IsHeadquarters ? planet.Planet.OwnerInstanceID : null,
                        planet.Planet.IsUnexploredView
                    )
                );
            }

            clusters.Add(
                new GalaxyMapSystemCluster(
                    sector.System.DisplayName,
                    sector.System,
                    offset.x + GetGalaxyCoordinateX(sector.System.PositionX),
                    offset.y + GetGalaxyCoordinateY(sector.System.PositionY),
                    sector.System.DisplayName,
                    false,
                    stars
                )
            );
        }

        return clusters;
    }

    private static int GetPlayerSupport(Planet planet, string playerFactionId)
    {
        return !string.IsNullOrEmpty(playerFactionId)
            ? planet.GetPopularSupport(playerFactionId)
            : 50;
    }

    public bool ClearHover()
    {
        return SetHoveredSystem(null);
    }

    private bool SetHoveredSystem(PlanetSystem system)
    {
        if (hoveredSystem == system)
            return false;

        hoveredSystem = system;
        UpdateClusterLabels();
        return true;
    }

    private void UpdateClusterLabels()
    {
        foreach (string key in visibleClusterKeys)
        {
            if (clusterViews.TryGetValue(key, out PlanetSystemClusterView view))
                view.SetLabelVisible(view.System == hoveredSystem);
        }
    }

    private PlanetSystemClusterView GetClusterView(string key)
    {
        if (clusterViews.TryGetValue(key, out PlanetSystemClusterView view))
            return view;

        if (planetSystemClusters == null)
            throw new MissingReferenceException("PlanetSystemClusters is missing.");

        if (planetSystemClusterPrefab == null)
            throw new MissingReferenceException("PlanetSystemCluster prefab is missing.");

        view = Instantiate(planetSystemClusterPrefab, planetSystemClusters);
        view.name = key;
        if (uiContext != null)
            view.Initialize(uiContext);
        view.Hovered += HandleClusterHovered;
        view.HoverCleared += HandleClusterHoverCleared;
        view.OpenRequested += HandleClusterOpenRequested;
        clusterViews[key] = view;
        return view;
    }

    private void HandleClusterHovered(PlanetSystemClusterView view)
    {
        if (SetHoveredSystem(view?.System))
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandleClusterHoverCleared(PlanetSystemClusterView view)
    {
        if (ClearHover())
            uiContext?.Dispatcher.Send(new StrategyUIRequests.RequestRender());
    }

    private void HandleClusterOpenRequested(
        PlanetSystemClusterView view,
        PointerEventData eventData
    )
    {
        if (
            view?.System == null
            || !TryGetSourcePosition(eventData, out int sourceX, out int sourceY)
        )
            return;

        uiContext?.Dispatcher.Send(
            new StrategyUIRequests.OpenPlanetSystemWindow(view.System, sourceX, sourceY)
        );
    }

    private bool TryGetSourcePosition(PointerEventData eventData, out int x, out int y)
    {
        x = 0;
        y = 0;
        if (eventData == null || transform is not RectTransform rect)
            return false;

        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 local
            )
        )
        {
            return false;
        }

        int width = Mathf.RoundToInt(rect.sizeDelta.x);
        int height = Mathf.RoundToInt(rect.sizeDelta.y);
        if (width <= 0)
            width = Mathf.RoundToInt(rect.rect.width);
        if (height <= 0)
            height = Mathf.RoundToInt(rect.rect.height);
        if (width <= 0 || height <= 0)
            return false;

        x = Mathf.RoundToInt(local.x + width / 2f);
        y = Mathf.RoundToInt(height / 2f - local.y);
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    private void VerifyReferences()
    {
        if (background == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing RawImage.");
        if (planetSystemClusters == null)
            throw new MissingReferenceException($"{name}/PlanetSystemClusters is missing.");
        if (planetSystemClusterPrefab == null)
            throw new MissingReferenceException("PlanetSystemCluster prefab is missing.");
    }

    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    private Vector2Int GetBackgroundPosition()
    {
        return uiContext?.GetPlayerFactionTheme()?.GalaxyBackground?.SourcePosition?.ToVector2Int()
            ?? Vector2Int.zero;
    }

    private Vector2Int GetStarOffset()
    {
        return uiContext?.GetPlayerFactionTheme()?.GalaxyBackground?.StarOffset?.ToVector2Int()
            ?? Vector2Int.zero;
    }

    private static int GetGalaxyCoordinateX(int x)
    {
        return Mathf.FloorToInt((x * 315f) / 512f);
    }

    private static int GetGalaxyCoordinateY(int y)
    {
        return Mathf.FloorToInt((y * 215f) / 512f);
    }
}

public readonly struct GalaxyMapSystemCluster
{
    public GalaxyMapSystemCluster(
        string key,
        PlanetSystem system,
        int x,
        int y,
        string label,
        bool showLabel,
        IReadOnlyList<GalaxyMapSystemStar> stars
    )
    {
        Key = key;
        System = system;
        X = x;
        Y = y;
        Label = label;
        ShowLabel = showLabel;
        Stars = stars;
    }

    public string Key { get; }
    public PlanetSystem System { get; }
    public int X { get; }
    public int Y { get; }
    public string Label { get; }
    public bool ShowLabel { get; }
    public IReadOnlyList<GalaxyMapSystemStar> Stars { get; }
}

public readonly struct GalaxyMapSystemStar
{
    public GalaxyMapSystemStar(
        int x,
        int y,
        string ownerFactionId,
        int popularSupport,
        string headquartersFactionId,
        bool isUnexplored
    )
    {
        X = x;
        Y = y;
        OwnerFactionId = ownerFactionId;
        PopularSupport = popularSupport;
        HeadquartersFactionId = headquartersFactionId;
        IsUnexplored = isUnexplored;
    }

    public int X { get; }
    public int Y { get; }
    public string OwnerFactionId { get; }
    public int PopularSupport { get; }
    public string HeadquartersFactionId { get; }
    public bool IsUnexplored { get; }
}
