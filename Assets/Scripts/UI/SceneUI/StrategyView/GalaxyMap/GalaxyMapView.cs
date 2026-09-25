using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents the authored galaxy map and translates cluster input into semantic requests.
/// </summary>
public sealed class GalaxyMapView : MonoBehaviour
{
    [SerializeField]
    private RectTransform background;

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RectTransform planetSectorClusters;

    [SerializeField]
    private TextMeshProUGUI activeFilterLabel;

    [SerializeField]
    private PlanetSectorClusterView planetSectorClusterPrefab;

    private readonly Dictionary<string, PlanetSectorClusterView> clusterViews = new Dictionary<
        string,
        PlanetSectorClusterView
    >(StringComparer.Ordinal);
    private readonly HashSet<string> visibleClusterKeys = new HashSet<string>(
        StringComparer.Ordinal
    );
    private WaypointRouteOverlay waypointOverlay;

    /// <summary>
    /// Raised when Unity destroys the authored galaxy-map view.
    /// </summary>
    public event Action<GalaxyMapView> Destroyed;

    /// <summary>
    /// Raised when the pointer leaves a rendered sector cluster.
    /// </summary>
    public event Action SectorHoverCleared;

    /// <summary>
    /// Raised when the pointer enters a rendered sector cluster.
    /// </summary>
    public event Action<string> SectorHovered;

    /// <summary>
    /// Raised when a rendered sector cluster is double-clicked.
    /// </summary>
    public event Action<string, int, int> SectorOpenRequested;

    public RectTransform Background => background;

    public RectTransform PlanetSectorClusters => planetSectorClusters;

    /// <summary>
    /// Validates authored references when Unity creates the view.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        EnsureWaypointOverlay();
    }

    /// <summary>
    /// Releases child subscriptions and informs the feature controller of destruction.
    /// </summary>
    private void OnDestroy()
    {
        foreach (PlanetSectorClusterView clusterView in clusterViews.Values)
            UnbindClusterView(clusterView);

        clusterViews.Clear();
        visibleClusterKeys.Clear();
        waypointOverlay = null;
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies a complete immutable galaxy-map presentation snapshot.
    /// </summary>
    /// <param name="data">The galaxy-map presentation snapshot.</param>
    public void Render(GalaxyMapRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        EnsureWaypointOverlay();
        RenderBackground(data.BackgroundTexture, data.BackgroundBounds, data.BackgroundColor);
        RenderWaypointRoutes(data.WaypointRoutes);
        RenderClusters(data.Clusters);
        waypointOverlay.SetPresentationOrder();
        RenderActiveFilterLabel(data.ActiveFilterLabel);
    }

    /// <summary>
    /// Applies the active galactic-information label presentation.
    /// </summary>
    /// <param name="data">The active filter label presentation.</param>
    private void RenderActiveFilterLabel(GalaxyMapActiveFilterLabelRenderData data)
    {
        activeFilterLabel.gameObject.SetActive(data.Visible);
        if (!data.Visible)
            return;

        activeFilterLabel.text = data.Text;
        activeFilterLabel.color = data.Color;
        activeFilterLabel.fontSize = data.FontSize;
        RectInt bounds = data.Bounds;
        UILayout.SetSourceRect(
            activeFilterLabel.rectTransform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
    }

    /// <summary>
    /// Resolves a pointer hit to the rendered planet identifier beneath it.
    /// </summary>
    /// <param name="eventData">The current pointer event.</param>
    /// <param name="planetInstanceId">Receives the rendered planet identifier.</param>
    /// <returns>True when the pointer is over a visible planet marker.</returns>
    internal bool TryGetPlanetInstanceID(PointerEventData eventData, out string planetInstanceId)
    {
        planetInstanceId = null;
        if (eventData == null)
            return false;

        foreach (PlanetSectorClusterView clusterView in clusterViews.Values)
        {
            if (
                clusterView != null
                && clusterView.TryGetPlanetInstanceID(eventData, out planetInstanceId)
            )
                return true;
        }

        return false;
    }

    /// <summary>
    /// Converts a pointer event to strategy source-space coordinates.
    /// </summary>
    /// <param name="eventData">The current pointer event.</param>
    /// <param name="sourceX">Receives the source-space horizontal coordinate.</param>
    /// <param name="sourceY">Receives the source-space vertical coordinate.</param>
    /// <returns>True when the pointer can be resolved inside the map bounds.</returns>
    internal bool TryGetSourcePosition(PointerEventData eventData, out int sourceX, out int sourceY)
    {
        sourceX = 0;
        sourceY = 0;
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

        sourceX = Mathf.RoundToInt(local.x + width / 2f);
        sourceY = Mathf.RoundToInt(height / 2f - local.y);
        return sourceX >= 0 && sourceX < width && sourceY >= 0 && sourceY < height;
    }

    /// <summary>
    /// Applies the resolved background texture and optional source-space bounds.
    /// </summary>
    /// <param name="texture">The resolved background texture.</param>
    /// <param name="bounds">The optional source-space background bounds.</param>
    /// <param name="color">The background-only color multiplier.</param>
    private void RenderBackground(Texture2D texture, RectInt? bounds, Color color)
    {
        backgroundImage.texture = texture;
        backgroundImage.color = color;
        backgroundImage.enabled = texture != null;
        backgroundImage.raycastTarget = false;
        backgroundImage.uvRect = new Rect(0f, 0f, 1f, 1f);

        if (texture == null || bounds == null)
            return;

        RectInt sourceRect = bounds.Value;
        UILayout.SetSourceRect(
            background,
            sourceRect.x,
            sourceRect.y,
            sourceRect.width,
            sourceRect.height
        );
        UILayout.SetSourceRect(
            planetSectorClusters,
            sourceRect.x,
            sourceRect.y,
            sourceRect.width,
            sourceRect.height
        );
    }

    /// <summary>
    /// Renders visible clusters and hides pooled cluster views absent from the snapshot.
    /// </summary>
    /// <param name="clusters">The visible cluster presentations.</param>
    private void RenderClusters(IReadOnlyList<GalaxyMapClusterRenderData> clusters)
    {
        visibleClusterKeys.Clear();
        if (clusters != null)
        {
            foreach (GalaxyMapClusterRenderData cluster in clusters)
            {
                visibleClusterKeys.Add(cluster.SectorInstanceId);
                PlanetSectorClusterView clusterView = GetOrCreateClusterView(
                    cluster.SectorInstanceId
                );
                clusterView.Render(cluster);
                clusterView.gameObject.SetActive(true);
            }
        }

        foreach (KeyValuePair<string, PlanetSectorClusterView> entry in clusterViews)
        {
            if (!visibleClusterKeys.Contains(entry.Key))
                entry.Value.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Renders numbered white route segments for player-controlled fleet waypoints.
    /// </summary>
    /// <param name="routes">The projected waypoint routes.</param>
    internal void RenderWaypointRoutes(IReadOnlyList<GalaxyMapWaypointRouteRenderData> routes)
    {
        EnsureWaypointOverlay();
        List<WaypointRouteLineRenderData> lines = new List<WaypointRouteLineRenderData>();
        List<WaypointRouteMarkerRenderData> markers = new List<WaypointRouteMarkerRenderData>();
        foreach (
            GalaxyMapWaypointRouteRenderData route in routes
                ?? Array.Empty<GalaxyMapWaypointRouteRenderData>()
        )
        {
            Vector2Int previous = route.Origin;
            foreach (GalaxyMapWaypointRenderData waypoint in route.Waypoints)
            {
                lines.Add(new WaypointRouteLineRenderData(previous, waypoint.Position));
                markers.Add(new WaypointRouteMarkerRenderData(waypoint.Order, waypoint.Position));
                previous = waypoint.Position;
            }
        }

        waypointOverlay.Render(lines, markers);
        waypointOverlay.SetPresentationOrder();
    }

    /// <summary>
    /// Creates the non-interactive route layer beneath planet-sector clusters when needed.
    /// </summary>
    private void EnsureWaypointOverlay()
    {
        if (waypointOverlay == null)
            waypointOverlay = new WaypointRouteOverlay(planetSectorClusters, activeFilterLabel);
    }

    /// <summary>
    /// Gets or creates the pooled authored view for one stable cluster identity.
    /// </summary>
    /// <param name="key">The stable cluster identity.</param>
    /// <returns>The reusable cluster view.</returns>
    private PlanetSectorClusterView GetOrCreateClusterView(string key)
    {
        if (clusterViews.TryGetValue(key, out PlanetSectorClusterView clusterView))
            return clusterView;

        clusterView = Instantiate(planetSectorClusterPrefab, planetSectorClusters);
        clusterView.name = key;
        clusterView.HoverCleared += HandleClusterHoverCleared;
        clusterView.Hovered += HandleClusterHovered;
        clusterView.OpenRequested += HandleClusterOpenRequested;
        clusterViews.Add(key, clusterView);
        return clusterView;
    }

    /// <summary>
    /// Forwards a child cluster hover entry as a semantic sector event.
    /// </summary>
    /// <param name="clusterView">The hovered cluster view.</param>
    private void HandleClusterHovered(PlanetSectorClusterView clusterView)
    {
        if (!string.IsNullOrEmpty(clusterView?.SectorInstanceId))
            SectorHovered?.Invoke(clusterView.SectorInstanceId);
    }

    /// <summary>
    /// Forwards a child cluster hover exit as a semantic map event.
    /// </summary>
    /// <param name="clusterView">The cluster view that lost hover.</param>
    private void HandleClusterHoverCleared(PlanetSectorClusterView clusterView)
    {
        SectorHoverCleared?.Invoke();
    }

    /// <summary>
    /// Converts a child cluster double-click to a semantic sector-open request.
    /// </summary>
    /// <param name="clusterView">The requested cluster view.</param>
    /// <param name="eventData">The originating pointer event.</param>
    private void HandleClusterOpenRequested(
        PlanetSectorClusterView clusterView,
        PointerEventData eventData
    )
    {
        if (
            string.IsNullOrEmpty(clusterView?.SectorInstanceId)
            || !TryGetSourcePosition(eventData, out int sourceX, out int sourceY)
        )
        {
            return;
        }

        SectorOpenRequested?.Invoke(clusterView.SectorInstanceId, sourceX, sourceY);
    }

    /// <summary>
    /// Releases subscriptions from one pooled cluster view.
    /// </summary>
    /// <param name="clusterView">The cluster view to release.</param>
    private void UnbindClusterView(PlanetSectorClusterView clusterView)
    {
        if (clusterView == null)
            return;

        clusterView.HoverCleared -= HandleClusterHoverCleared;
        clusterView.Hovered -= HandleClusterHovered;
        clusterView.OpenRequested -= HandleClusterOpenRequested;
    }

    /// <summary>
    /// Verifies every authored reference required for map presentation and input.
    /// </summary>
    private void VerifyReferences()
    {
        if (background == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing RawImage.");
        if (planetSectorClusters == null)
            throw new MissingReferenceException($"{name}/PlanetSectorClusters is missing.");
        if (activeFilterLabel == null)
            throw new MissingReferenceException($"{name}/ActiveFilterLabel is missing.");
        if (planetSectorClusterPrefab == null)
            throw new MissingReferenceException($"{name}/PlanetSectorCluster prefab is missing.");
    }
}
