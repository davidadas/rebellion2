using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Presents authored sectors through the gameplay sector window with neutral preview statistics.
/// </summary>
[ExecuteAlways]
public sealed class ContentEditorSectorPreview : UIBehaviour
{
    [SerializeField]
    private PlanetSectorWindowView window;
    public float AspectRatio =>
        ((RectTransform)window.transform).rect.width
        / ((RectTransform)window.transform).rect.height;
    public event Action DragEnded;

    public event Action<ContentPackEditorRecord> Selected;
    public event Action<ContentPackEditorRecord> DragStarted;
    public event Action<ContentPackEditorRecord, Vector2Int> Moved;

    /// <summary>
    /// Renders exact authored planet positions using maximum authored resource capacities.
    /// </summary>
    /// <param name="project">The working pack.</param>
    /// <param name="sector">The authored sector.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <param name="theme">The optional faction whose overlay icons and colors are previewed.</param>
    public void Render(
        ContentPackEditorProject project,
        ContentPackEditorRecord sector,
        ContentAssets assets,
        FactionTheme theme = null
    )
    {
        ContentPackEditorRecord[] planets = sector.Planets.ToArray();
        int energy = MaximumCapacity(project, "EnergyMax");
        int materials = MaximumCapacity(project, "RawMaterialsMax");
        Color neutral = (
            theme ?? ContentEditorPresentation.GetNeutralTheme(project)
        ).GetPrimaryColor();
        window.Render(
            new PlanetSectorWindowRenderData(
                sector.Name,
                planets
                    .Select(
                        (planet, index) =>
                            CreatePlanet(
                                planet,
                                sector,
                                index,
                                assets,
                                energy,
                                materials,
                                neutral,
                                theme?.PlanetOverlayTheme
                            )
                    )
                    .ToArray()
            )
        );
        PlanetSectorPlanetView[] views = window.GetComponentsInChildren<PlanetSectorPlanetView>();
        for (int i = 0; i < planets.Length; i++)
        {
            ContentEditorPlanetDrag drag =
                views[i].GetComponent<ContentEditorPlanetDrag>()
                ?? views[i].gameObject.AddComponent<ContentEditorPlanetDrag>();
            drag.Bind(
                planets[i],
                sector,
                window,
                record => Selected?.Invoke(record),
                record => DragStarted?.Invoke(record),
                (record, position) => Moved?.Invoke(record, position),
                () => DragEnded?.Invoke()
            );
        }
        Fit();
    }

    /// <summary>
    /// Hides the sector window while the galaxy overview is displayed.
    /// </summary>
    public void Hide() => window.gameObject.SetActive(false);

    /// <summary>
    /// Releases project references held by the reused gameplay planet views.
    /// </summary>
    public void Clear()
    {
        foreach (
            ContentEditorPlanetDrag drag in window.GetComponentsInChildren<ContentEditorPlanetDrag>(
                true
            )
        )
            drag.Clear();
        Hide();
    }

    /// <summary>
    /// Converts the preview's center into the same coordinate offset used by planet dragging.
    /// </summary>
    /// <returns>The sector-relative placement coordinate for a new planet.</returns>
    public Vector2Int GetPlacementOffset()
    {
        Rect bounds = ((RectTransform)window.transform).rect;
        return window.UnprojectPlanetPosition(
            new Vector2(bounds.width * 0.5f, bounds.height * 0.5f)
        );
    }

    /// <summary>
    /// Captures complete planet presentations in the sector's saved integer-coordinate frame.
    /// </summary>
    /// <param name="sector">The displayed sector and its coordinate origin.</param>
    /// <returns>The planet records and their quantization-safe occupied geometry.</returns>
    internal ContentEditorMapLayout CreateLayout(ContentPackEditorRecord sector)
    {
        ContentPackEditorRecord[] planets = sector.Planets.ToArray();
        PlanetSectorPlanetView[] views = window.GetComponentsInChildren<PlanetSectorPlanetView>();
        var ordered = planets
            .Select((planet, i) => (Planet: planet, View: views[i]))
            .OrderBy(item => item.Planet.Key, StringComparer.Ordinal)
            .ToArray();
        Rect bounds = window.UnprojectPlanetBounds(window.GetPlanetAreaSourceRect());
        Rect unit = window.UnprojectPlanetBounds(new Rect(0, 0, 1, 1));
        RectangleLayoutItem[] items = ordered
            .Select(item =>
            {
                Vector2Int position = new(
                    checked(item.Planet.X - sector.X),
                    checked(item.Planet.Y - sector.Y)
                );
                RectInt image = item.View.GetRenderedPlanetImageSourceRect();
                return new RectangleLayoutItem(
                    position,
                    item.View.GetLayoutSourceRects()
                        .Select(rect =>
                        {
                            return new Rect(
                                (rect.x - image.x - 1) * unit.width,
                                (rect.y - image.y - 1) * unit.height,
                                (rect.width + 1) * unit.width,
                                (rect.height + 1) * unit.height
                            );
                        })
                );
            })
            .ToArray();
        // Projection floors the image anchor, so reserve its possible one-pixel shift toward the top-left.
        return new ContentEditorMapLayout(
            ordered.Select(item => item.Planet),
            items,
            bounds,
            unit.size * 2,
            new Vector2(1 / unit.width, 1 / unit.height),
            new Vector2Int(sector.X, sector.Y)
        );
    }

    /// <summary>
    /// Refits the source-sized window after the map pane is resized.
    /// </summary>
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        Fit();
    }

    /// <summary>
    /// Fits the unchanged gameplay geometry to the preview surface.
    /// </summary>
    private void Fit()
    {
        if (window == null)
            return;
        RectTransform rect = (RectTransform)window.transform;
        Rect bounds = ((RectTransform)transform).rect;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale =
            Vector3.one
            * Mathf.Min(bounds.width / rect.rect.width, bounds.height / rect.rect.height);
    }

    /// <summary>
    /// Reads the greatest capacity allowed by the authored generation presets.
    /// </summary>
    /// <param name="project">The working pack.</param>
    /// <param name="member">The capacity limit.</param>
    /// <returns>The greatest authored limit, or zero when no limit is defined.</returns>
    private static int MaximumCapacity(ContentPackEditorProject project, string member)
    {
        return project
            .GetCatalogs()
            .SelectMany(catalog => catalog.Records)
            .Where(record => record.Kind == "GameGenerationConfig")
            .SelectMany(record => record.Element.Descendants(member))
            .Select(element => int.TryParse(element.Value, out int value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    /// <summary>
    /// Supplies preview-only statistics without changing the authored planet definition.
    /// </summary>
    /// <param name="planet">The authored planet.</param>
    /// <param name="sector">Its sector origin.</param>
    /// <param name="index">Its presentation index.</param>
    /// <param name="assets">The artwork source.</param>
    /// <param name="energy">The maximum available energy.</param>
    /// <param name="materials">The maximum available raw materials.</param>
    /// <param name="neutral">The pack's neutral support color.</param>
    /// <param name="overlays">The optional faction overlay artwork.</param>
    /// <returns>The gameplay renderer's planet presentation.</returns>
    private static PlanetSectorPlanetRenderData CreatePlanet(
        ContentPackEditorRecord planet,
        ContentPackEditorRecord sector,
        int index,
        ContentAssets assets,
        int energy,
        int materials,
        Color neutral,
        PlanetOverlayTheme overlays
    )
    {
        string image = planet.Element.Element("PlanetIconPath")?.Value ?? planet.ImageAddress;
        return new PlanetSectorPlanetRenderData(
            planetIndex: index,
            galaxyOffset: new Vector2Int(planet.X - sector.X, planet.Y - sector.Y),
            planetTexture: string.IsNullOrEmpty(image) ? null : assets.GetTexture(image),
            uprisingTexture: LoadTexture(assets, overlays?.PlanetSectorUprisingImagePath),
            facilityTexture: LoadTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Buildings?.NormalImagePath
            ),
            facilityPressedTexture: LoadTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Buildings?.HoverImagePath
            ),
            defenseTexture: LoadTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Defenses?.NormalImagePath
            ),
            defensePressedTexture: LoadTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Defenses?.HoverImagePath
            ),
            fleetTexture: LoadTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Fleets?.NormalImagePath
            ),
            fleetPressedTexture: LoadTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Fleets?.HoverImagePath
            ),
            missionTexture: LoadTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Missions?.NormalImagePath
            ),
            missionPressedTexture: LoadTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Missions?.HoverImagePath
            ),
            headquartersTexture: LoadTexture(assets, overlays?.PlanetSectorHeadquartersImagePath),
            name: planet.Name,
            nameColor: Color.white,
            selectedIcon: PlanetIcon.None,
            hoveredIcon: PlanetIcon.None,
            energyBar: new PlanetSectorBarRenderData(
                true,
                energy,
                0,
                0,
                Color.white,
                new Color32(64, 132, 255, 255),
                new Color32(160, 160, 160, 255),
                "Energy Consumption 0/" + energy
            ),
            rawResourceBar: new PlanetSectorBarRenderData(
                true,
                materials,
                0,
                0,
                new Color32(255, 255, 84, 255),
                new Color32(236, 106, 46, 255),
                new Color32(160, 160, 160, 255),
                "Raw Materials 0/" + materials
            ),
            supportBar: new PlanetSectorBarRenderData(
                true,
                0,
                0,
                0,
                neutral,
                Color.clear,
                neutral,
                "Popular Support 0/100"
            )
        );
    }

    /// <summary>
    /// Resolves optional preview artwork without turning an absent overlay into an invalid address.
    /// </summary>
    /// <param name="assets">The current asset source.</param>
    /// <param name="path">The optional content address.</param>
    /// <returns>The resolved image, or null when no overlay is configured.</returns>
    private static Texture2D LoadTexture(ContentAssets assets, string path) =>
        string.IsNullOrEmpty(path) ? null : assets.GetTexture(path);
}
