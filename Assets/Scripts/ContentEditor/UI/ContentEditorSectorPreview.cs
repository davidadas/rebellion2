using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents authored sectors through the gameplay sector window with neutral preview statistics.
/// </summary>
[ExecuteAlways]
public sealed class ContentEditorSectorPreview : UIBehaviour
{
    private readonly List<ContentPackEditorField> renderedFields = new();

    [SerializeField]
    private PlanetSectorWindowView window;
    public float AspectRatio =>
        ((RectTransform)window.transform).rect.width
        / ((RectTransform)window.transform).rect.height;
    public event Action DragEnded;

    public event Action<ContentPackEditorRecord> Selected;
    public event Action<ContentPackEditorRecord> DragStarted;
    public event Action<ContentPackEditorRecord, Vector2Int> Moved;
    public event Action<ContentPackEditorField> ArtworkOpened;

    /// <summary>
    /// Gets the faction-theme fields currently represented by literal editable images.
    /// </summary>
    internal IReadOnlyList<ContentPackEditorField> RenderedFields => renderedFields;

    /// <summary>
    /// Renders exact authored planet positions using the default resource profile.
    /// </summary>
    /// <param name="project">The working pack.</param>
    /// <param name="sector">The authored sector.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <param name="theme">The optional faction whose overlay icons and colors are previewed.</param>
    /// <param name="editableFields">The optional faction-interface fields rendered in the preview.</param>
    public void Render(
        ContentPackEditorProject project,
        ContentPackEditorRecord sector,
        ContentAssets assets,
        FactionTheme theme = null,
        IReadOnlyList<ContentPackEditorField> editableFields = null
    )
    {
        ContentPackEditorRecord[] planets = sector.Planets.ToArray();
        int energy = DefaultCapacity(project, nameof(PlanetResourceProfile.EnergyMax));
        int materials = DefaultCapacity(project, nameof(PlanetResourceProfile.RawMaterialsMax));
        ContentPackEditorField destroyedPlanetField = editableFields?.FirstOrDefault(field =>
            field.MemberName == nameof(GalaxyBackground.DestroyedPlanetIconPath)
        );
        Texture2D destroyedPlanetTexture = ResolveFieldTexture(destroyedPlanetField, assets);
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
                                theme?.PlanetOverlayTheme,
                                index == planets.Length - 1 ? destroyedPlanetTexture : null
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
        AttachEditCommands(editableFields ?? Array.Empty<ContentPackEditorField>(), assets);
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
        renderedFields.Clear();
        foreach (
            ContentEditorPlanetDrag drag in window.GetComponentsInChildren<ContentEditorPlanetDrag>(
                true
            )
        )
            drag.Clear();
        foreach (
            ContentEditorArtworkEditTarget target in GetComponentsInChildren<ContentEditorArtworkEditTarget>(
                true
            )
        )
            target.Clear();
        Hide();
    }

    /// <summary>
    /// Adds hover-outline and double-click editing to each displayed image with an exact field.
    /// </summary>
    /// <param name="editableFields">The faction-interface fields available on the selected page.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    private void AttachEditCommands(
        IReadOnlyList<ContentPackEditorField> editableFields,
        ContentAssets assets
    )
    {
        renderedFields.Clear();
        foreach (
            ContentEditorArtworkEditTarget target in GetComponentsInChildren<ContentEditorArtworkEditTarget>(
                true
            )
        )
            target.Clear();

        Dictionary<string, ContentPackEditorField> fieldsByAddress = new(StringComparer.Ordinal);
        foreach (ContentPackEditorField field in editableFields)
            RegisterFieldAddress(field, assets, fieldsByAddress);

        foreach (RawImage image in GetComponentsInChildren<RawImage>(true))
        {
            ContentPackEditorField releasedField = ResolvePlanetOverlayField(
                image,
                editableFields,
                "NormalImagePath"
            );
            if (!image.gameObject.activeInHierarchy || !image.enabled || image.texture == null)
                continue;
            if (
                releasedField == null
                && (
                    !assets.TryGetTextureAddress(image.texture as Texture2D, out string address)
                    || !fieldsByAddress.TryGetValue(address, out releasedField)
                )
            )
                continue;
            ContentPackEditorField hoverField = ResolvePlanetOverlayField(
                image,
                editableFields,
                "HoverImagePath"
            );
            hoverField ??= releasedField;
            Texture2D hoverTexture =
                ResolveFieldTexture(hoverField, assets) ?? image.texture as Texture2D;
            ContentEditorArtworkEditTarget target =
                image.GetComponent<ContentEditorArtworkEditTarget>()
                ?? image.gameObject.AddComponent<ContentEditorArtworkEditTarget>();
            target.Bind(
                releasedField,
                hoverField,
                opened => ArtworkOpened?.Invoke(opened),
                assets.GetTextureContentBounds(image.texture as Texture2D),
                assets.GetTextureContentBounds(hoverTexture),
                hoverTexture == image.texture ? null : hoverTexture
            );
            foreach (ContentPackEditorField field in target.Fields)
                if (!renderedFields.Contains(field))
                    renderedFields.Add(field);
        }
    }

    /// <summary>
    /// Resolves a planet-overlay image by the gameplay control it decorates instead of by a
    /// potentially shared texture address.
    /// </summary>
    /// <param name="image">The rendered planet control image.</param>
    /// <param name="fields">The selected planet-sector fields.</param>
    /// <param name="memberName">The requested normal or hover member.</param>
    /// <returns>The exact overlay field, or null for a non-overlay image.</returns>
    private static ContentPackEditorField ResolvePlanetOverlayField(
        RawImage image,
        IReadOnlyList<ContentPackEditorField> fields,
        string memberName
    )
    {
        string owner = image.name switch
        {
            "FacilityImage" => nameof(PlanetOverlayIcons.Buildings),
            "DefenseImage" => nameof(PlanetOverlayIcons.Defenses),
            "FleetImage" => nameof(PlanetOverlayIcons.Fleets),
            "MissionImage" => nameof(PlanetOverlayIcons.Missions),
            _ => null,
        };
        if (owner == null)
            return null;
        return fields.FirstOrDefault(field =>
            field.MemberName == memberName
            && field.XmlElement.Parent?.Name.LocalName == owner
            && field
                .XmlElement.Ancestors()
                .Any(element =>
                    element.Name.LocalName == nameof(PlanetOverlayTheme.PlanetOverlayIcons)
                )
        );
    }

    /// <summary>
    /// Loads a selected editable field without treating an empty optional path as an error.
    /// </summary>
    /// <param name="field">The image field.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The resolved texture, or null when the field is empty or invalid.</returns>
    private static Texture2D ResolveFieldTexture(ContentPackEditorField field, ContentAssets assets)
    {
        if (field == null || string.IsNullOrWhiteSpace(field.Value))
            return null;
        try
        {
            return assets.GetTexture(field.GetMediaAddress(field.Value));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Indexes one authored image field by the normalized address used by loaded textures.
    /// </summary>
    /// <param name="field">The exact authored image field.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <param name="fieldsByAddress">The address-to-field index to populate.</param>
    private static void RegisterFieldAddress(
        ContentPackEditorField field,
        ContentAssets assets,
        IDictionary<string, ContentPackEditorField> fieldsByAddress
    )
    {
        if (
            field.MediaType != ContentPackEditorMediaType.Image
            || string.IsNullOrWhiteSpace(field.Value)
        )
            return;
        string address = field.GetMediaAddress(field.Value);
        try
        {
            address = assets.GetNormalizedTextureAddress(address);
        }
        catch (ArgumentException)
        {
            return;
        }
        if (!string.IsNullOrEmpty(address) && !fieldsByAddress.ContainsKey(address))
            fieldsByAddress.Add(address, field);
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
    /// Reads a capacity from the normal resource profile, falling back to the first profile.
    /// </summary>
    /// <param name="project">The working pack.</param>
    /// <param name="member">The capacity limit.</param>
    /// <returns>The default authored limit, or zero when no limit is defined.</returns>
    private static int DefaultCapacity(ContentPackEditorProject project, string member)
    {
        XElement[] profiles = project
            .GetCatalogs()
            .SelectMany(catalog => catalog.Records)
            .Where(record => record.Kind == "GameGenerationConfig")
            .SelectMany(record => record.Element.Descendants(nameof(PlanetResourceProfile)))
            .ToArray();
        XElement profile =
            profiles.FirstOrDefault(candidate =>
                candidate.Element(nameof(PlanetResourceProfile.Availability))?.Value
                == GameResourceAvailability.Normal.ToString()
            ) ?? profiles.FirstOrDefault();
        return int.TryParse(profile?.Element(member)?.Value, out int value) ? value : 0;
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
    /// <param name="destroyedPlanetTexture">The optional destroyed state shown on the representative final planet.</param>
    /// <returns>The gameplay renderer's planet presentation.</returns>
    private static PlanetSectorPlanetRenderData CreatePlanet(
        ContentPackEditorRecord planet,
        ContentPackEditorRecord sector,
        int index,
        ContentAssets assets,
        int energy,
        int materials,
        Color neutral,
        PlanetOverlayTheme overlays,
        Texture2D destroyedPlanetTexture
    )
    {
        string image = planet.Element.Element("PlanetIconPath")?.Value ?? planet.ImageAddress;
        return new PlanetSectorPlanetRenderData(
            planetIndex: index,
            galaxyOffset: new Vector2Int(planet.X - sector.X, planet.Y - sector.Y),
            planetTexture: destroyedPlanetTexture
                ?? (string.IsNullOrEmpty(image) ? null : assets.GetTexture(image)),
            uprisingTexture: LoadTexture(assets, overlays?.PlanetSectorUprisingImagePath),
            facilityTexture: LoadSmoothTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Buildings?.NormalImagePath
            ),
            facilityPressedTexture: LoadSmoothTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Buildings?.HoverImagePath
            ),
            defenseTexture: LoadSmoothTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Defenses?.NormalImagePath
            ),
            defensePressedTexture: LoadSmoothTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Defenses?.HoverImagePath
            ),
            fleetTexture: LoadSmoothTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Fleets?.NormalImagePath
            ),
            fleetPressedTexture: LoadSmoothTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Fleets?.HoverImagePath
            ),
            missionTexture: LoadSmoothTexture(
                assets,
                overlays?.PlanetOverlayIcons?.Missions?.NormalImagePath
            ),
            missionPressedTexture: LoadSmoothTexture(
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

    /// <summary>
    /// Resolves optional preview artwork through the smooth minification cache.
    /// </summary>
    /// <param name="assets">The current asset source.</param>
    /// <param name="path">The optional content address.</param>
    /// <returns>The resolved image, or null when no overlay is configured.</returns>
    private static Texture2D LoadSmoothTexture(ContentAssets assets, string path) =>
        string.IsNullOrEmpty(path) ? null : assets.GetSmoothTexture(path);
}
