using System;
using Rebellion.Game;
using UnityEngine;

/// <summary>
/// Responsible for rendering and handling interaction
/// for all PlanetSystem icons on the galaxy map.
/// </summary>
public sealed class GalaxyView : MonoBehaviour
{
    private GalaxyMap galaxyMap;
    private UIContext context;
    private RectTransform viewport;
    private RectTransform renderLayer;
    private GalaxyCoordinateMapper mapper;

    public event Action<PlanetSystem> OnSystemSelected;
    public event Action<PlanetSystem> OnSystemOpened;

    /// <summary>
    /// Initializes the map view with galaxy data.
    /// </summary>
    public void Initialize(
        GalaxyMap galaxyMap,
        UIContext context,
        RectTransform viewport,
        GalaxyCoordinateMapper mapper
    )
    {
        if (galaxyMap == null)
            throw new ArgumentNullException(nameof(galaxyMap));

        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (viewport == null)
            throw new ArgumentNullException(nameof(viewport));

        if (mapper == null)
            throw new ArgumentNullException(nameof(mapper));

        this.galaxyMap = galaxyMap;
        this.context = context;
        this.viewport = viewport;
        this.mapper = mapper;

        EnsureRenderLayer();
        Clear();
        BuildSystems();
    }

    /// <summary>
    /// Ensures the icon layer exists inside the active map viewport.
    /// </summary>
    private void EnsureRenderLayer()
    {
        if (renderLayer != null)
            return;

        Transform existing = viewport.Find("GalaxyIcons");
        if (existing != null)
        {
            renderLayer = existing as RectTransform;
            return;
        }

        GameObject go = new GameObject("GalaxyIcons");
        renderLayer = go.AddComponent<RectTransform>();
        renderLayer.SetParent(viewport, false);
        renderLayer.anchorMin = Vector2.zero;
        renderLayer.anchorMax = Vector2.one;
        renderLayer.offsetMin = Vector2.zero;
        renderLayer.offsetMax = Vector2.zero;
        renderLayer.pivot = new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// Removes all existing system icons.
    /// </summary>
    private void Clear()
    {
        if (renderLayer == null)
            return;

        for (int i = renderLayer.childCount - 1; i >= 0; i--)
        {
            Destroy(renderLayer.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// Builds all planet systems in the galaxy.
    /// </summary>
    private void BuildSystems()
    {
        foreach (PlanetSystem system in galaxyMap.GetChildren())
        {
            CreateSystem(system);
        }
    }

    /// <summary>
    /// Creates a single system icon and wires interaction.
    /// </summary>
    private void CreateSystem(PlanetSystem system)
    {
        GameObject go = new GameObject(system.DisplayName);
        go.transform.SetParent(renderLayer, false);

        PlanetSystemIcon icon = go.AddComponent<PlanetSystemIcon>();
        icon.Initialize(system, context, renderLayer, mapper);

        icon.OnClicked += HandleSystemClicked;
        icon.OnDoubleClicked += HandleSystemDoubleClicked;
    }

    /// <summary>
    /// Single click selects system.
    /// </summary>
    private void HandleSystemClicked(PlanetSystem system)
    {
        OnSystemSelected?.Invoke(system);
    }

    /// <summary>
    /// Double click opens system panel.
    /// </summary>
    private void HandleSystemDoubleClicked(PlanetSystem system)
    {
        OnSystemOpened?.Invoke(system);
    }
}

/// <summary>
/// Maps original Rebellion galaxy coordinates into the active map viewport.
/// </summary>
public sealed class GalaxyCoordinateMapper
{
    private const float LogicalMapSize = 1024.0f;

    private readonly RectTransform viewport;

    public GalaxyCoordinateMapper(RectTransform viewport)
    {
        this.viewport = viewport;
    }

    /// <summary>
    /// Converts original map coordinates into viewport-local UI coordinates.
    /// </summary>
    public Vector2 Map(float x, float y)
    {
        Rect rect = viewport.rect;

        return new Vector2(
            (x / LogicalMapSize) * rect.width,
            (1.0f - (y / LogicalMapSize)) * rect.height
        );
    }
}
