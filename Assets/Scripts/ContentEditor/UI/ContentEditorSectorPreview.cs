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
public sealed class ContentEditorSectorPreview
    : UIBehaviour,
        IContentEditorArtworkDragPreview,
        IPointerMoveHandler,
        IPointerExitHandler
{
    private readonly List<ContentPackEditorField> renderedFields = new();
    private RawImagePressVisual[] windowControls;
    private ContentEditorArtworkEditTarget draggedControl;
    private ContentEditorArtworkEditTarget hoveredControl;
    private Vector2 dragOrigin;
    private Rect draggedControlBounds;

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
    internal event Action<IReadOnlyList<ContentEditorArtworkPlacementChange>> ArtworkMoved;

    internal Color DefaultBackgroundColor => window.DefaultBackgroundColor;

    /// <summary>
    /// Applies the pack's presentation to the same sector view used by the game.
    /// </summary>
    /// <param name="layout">The optional sector-window overrides.</param>
    internal void ApplyWindowLayout(PlanetSectorWindowLayout layout) => window.ApplyLayout(layout);

    /// <summary>
    /// Gets the faction-theme fields currently represented by literal editable images.
    /// </summary>
    internal IReadOnlyList<ContentPackEditorField> RenderedFields => renderedFields;

    /// <summary>
    /// Gets the shared press visuals attached directly to the sector window.
    /// </summary>
    /// <returns>The authored window header controls.</returns>
    internal IReadOnlyList<RawImagePressVisual> GetWindowControls() =>
        windowControls ??= window
            .GetComponentsInChildren<RawImagePressVisual>(true)
            .Where(control => control.transform.parent == window.transform)
            .ToArray();

    /// <summary>
    /// Shows the shared header buttons when the sector is used to preview window controls.
    /// </summary>
    /// <param name="visible">Whether the window header buttons should be shown.</param>
    internal void SetWindowControlsVisible(bool visible)
    {
        foreach (RawImagePressVisual control in GetWindowControls())
            control.gameObject.SetActive(visible);
    }

    /// <summary>
    /// Enables dragging the displayed header art using the shared artwork gesture target.
    /// </summary>
    /// <param name="control">The authored press visual used in the sector window.</param>
    /// <param name="placement">The two pack values updated on release.</param>
    internal void BindWindowControl(
        RawImagePressVisual control,
        ContentEditorArtworkPlacement placement
    )
    {
        ContentEditorArtworkEditTarget target =
            control.GetComponent<ContentEditorArtworkEditTarget>()
            ?? control.gameObject.AddComponent<ContentEditorArtworkEditTarget>();
        target.BindPlacement(placement);
    }

    /// <summary>
    /// Starts a header-control drag in the window's unscaled coordinates.
    /// </summary>
    /// <param name="eventData">The initial pointer event.</param>
    void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
    {
        ContentEditorArtworkEditTarget target =
            eventData.button == PointerEventData.InputButton.Left
                ? GetWindowControlAt(eventData.pressPosition, eventData.pressEventCamera)
                : null;
        if (
            target == null
            || !TryGetWindowPoint(eventData.pressPosition, eventData, out dragOrigin)
            || !target.BeginMove()
        )
        {
            ExecuteEvents.ExecuteHierarchy(
                transform.parent.gameObject,
                eventData,
                ExecuteEvents.beginDragHandler
            );
            return;
        }
        draggedControl = target;
        RectTransform button = (RectTransform)target.transform;
        draggedControlBounds = button.rect;
        draggedControlBounds.position += (Vector2)button.localPosition;
        eventData.Use();
    }

    /// <summary>
    /// Outlines the movable header button under the pointer in the sector preview.
    /// </summary>
    /// <param name="eventData">The current pointer position and camera.</param>
    void IPointerMoveHandler.OnPointerMove(PointerEventData eventData)
    {
        if (draggedControl != null)
            return;
        ContentEditorArtworkEditTarget control = GetWindowControlAt(
            eventData.position,
            eventData.enterEventCamera
        );
        if (hoveredControl == control)
            return;
        hoveredControl?.SetHovered(false);
        hoveredControl = control;
        hoveredControl?.SetHovered(true);
    }

    /// <summary>
    /// Clears the movable header outline after the pointer leaves the preview.
    /// </summary>
    /// <param name="eventData">The pointer exit event.</param>
    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        hoveredControl?.SetHovered(false);
        hoveredControl = null;
    }

    /// <summary>
    /// Finds the actionable header image at a pointer position within the rendered window.
    /// </summary>
    /// <param name="position">The screen-space pointer position.</param>
    /// <param name="camera">The event camera for the canvas.</param>
    /// <returns>The corresponding movable edit target, when present.</returns>
    private ContentEditorArtworkEditTarget GetWindowControlAt(Vector2 position, Camera camera) =>
        GetWindowControls()
            .Where(control => control.isActiveAndEnabled)
            .Select(control => control.GetComponent<ContentEditorArtworkEditTarget>())
            .LastOrDefault(target =>
                target?.CanMove == true && target.ContainsScreenPoint(position, camera)
            );

    /// <summary>
    /// Moves the grabbed control within the window preview.
    /// </summary>
    /// <param name="eventData">The current pointer event.</param>
    void IDragHandler.OnDrag(PointerEventData eventData)
    {
        if (draggedControl == null)
        {
            ExecuteEvents.ExecuteHierarchy(
                transform.parent.gameObject,
                eventData,
                ExecuteEvents.dragHandler
            );
            return;
        }
        draggedControl.PreviewMove(GetControlDragDelta(eventData));
        eventData.Use();
    }

    /// <summary>
    /// Submits the finished control drag through the workspace's artwork edit history.
    /// </summary>
    /// <param name="eventData">The released pointer event.</param>
    void IEndDragHandler.OnEndDrag(PointerEventData eventData)
    {
        if (draggedControl == null)
        {
            ExecuteEvents.ExecuteHierarchy(
                transform.parent.gameObject,
                eventData,
                ExecuteEvents.endDragHandler
            );
            return;
        }
        var changes = draggedControl.CompleteMove(GetControlDragDelta(eventData));
        draggedControl = null;
        eventData.Use();
        ArtworkMoved?.Invoke(changes);
    }

    /// <summary>
    /// Translates movement into source pixels and keeps the complete button within the frame.
    /// </summary>
    /// <param name="eventData">The current pointer event.</param>
    /// <returns>The source-coordinate displacement from the start of the drag.</returns>
    private Vector2Int GetControlDragDelta(PointerEventData eventData)
    {
        if (!TryGetWindowPoint(eventData.position, eventData, out Vector2 point))
            return Vector2Int.zero;
        Vector2 delta = point - dragOrigin;
        Rect bounds = ((RectTransform)window.transform).rect;
        return new Vector2Int(
            Mathf.RoundToInt(
                Mathf.Clamp(
                    delta.x,
                    bounds.xMin - draggedControlBounds.xMin,
                    bounds.xMax - draggedControlBounds.xMax
                )
            ),
            -Mathf.RoundToInt(
                Mathf.Clamp(
                    delta.y,
                    bounds.yMin - draggedControlBounds.yMin,
                    bounds.yMax - draggedControlBounds.yMax
                )
            )
        );
    }

    /// <summary>
    /// Resolves a pointer position in the unscaled sector window.
    /// </summary>
    /// <param name="position">The screen-space pointer position.</param>
    /// <param name="eventData">The event that supplies the UI camera.</param>
    /// <param name="point">The corresponding window coordinates.</param>
    /// <returns>True when the position intersects the window plane.</returns>
    private bool TryGetWindowPoint(
        Vector2 position,
        PointerEventData eventData,
        out Vector2 point
    ) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)window.transform,
            position,
            eventData.pressEventCamera,
            out point
        );

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
        FactionTheme neutral = ContentEditorPresentation.GetNeutralTheme(project);
        FactionTheme activeTheme = theme ?? neutral;
        bool factionPreview =
            activeTheme != null
            && (neutral == null || activeTheme.FactionInstanceID != neutral.FactionInstanceID);
        int neutralIndex = -1;
        if (editableFields != null && neutral != null && factionPreview)
        {
            int available = planets.Length - (destroyedPlanetTexture != null ? 1 : 0);
            if (available > 1)
                neutralIndex = available - 1;
        }
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
                                index == neutralIndex ? neutral : activeTheme,
                                factionPreview && index != neutralIndex,
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
        AttachEditCommands(
            editableFields ?? Array.Empty<ContentPackEditorField>(),
            assets,
            neutralIndex >= 0 ? views[neutralIndex].transform : null
        );
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
        draggedControl = null;
        hoveredControl = null;
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
    /// <param name="neutralSample">A preview borrowing artwork owned by the separate neutral theme.</param>
    private void AttachEditCommands(
        IReadOnlyList<ContentPackEditorField> editableFields,
        ContentAssets assets,
        Transform neutralSample
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
            if (neutralSample != null && image.transform.IsChildOf(neutralSample))
                continue;
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
    /// <param name="theme">The theme providing text colors and overlay icons.</param>
    /// <param name="hasOwner">Whether to show faction-owned overlays on this sample planet.</param>
    /// <param name="destroyedPlanetTexture">The optional destroyed state shown on the representative final planet.</param>
    /// <returns>The gameplay renderer's planet presentation.</returns>
    private static PlanetSectorPlanetRenderData CreatePlanet(
        ContentPackEditorRecord planet,
        ContentPackEditorRecord sector,
        int index,
        ContentAssets assets,
        int energy,
        int materials,
        FactionTheme theme,
        bool hasOwner,
        Texture2D destroyedPlanetTexture
    )
    {
        string image = planet.Element.Element("PlanetIconPath")?.Value ?? planet.ImageAddress;
        Color factionColor = theme?.GetPrimaryColor() ?? Color.white;
        PlanetOverlayTheme overlays = theme?.PlanetOverlayTheme;
        PlanetOverlayIcons factionIcons = hasOwner ? overlays?.PlanetOverlayIcons : null;
        return new PlanetSectorPlanetRenderData(
            planetIndex: index,
            galaxyOffset: new Vector2Int(planet.X - sector.X, planet.Y - sector.Y),
            planetTexture: destroyedPlanetTexture
                ?? (string.IsNullOrEmpty(image) ? null : assets.GetTexture(image)),
            uprisingTexture: hasOwner
                ? LoadTexture(assets, overlays?.PlanetSectorUprisingImagePath)
                : null,
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
            fleetTexture: LoadSmoothTexture(assets, factionIcons?.Fleets?.NormalImagePath),
            fleetPressedTexture: LoadSmoothTexture(assets, factionIcons?.Fleets?.HoverImagePath),
            missionTexture: LoadSmoothTexture(assets, factionIcons?.Missions?.NormalImagePath),
            missionPressedTexture: LoadSmoothTexture(
                assets,
                factionIcons?.Missions?.HoverImagePath
            ),
            headquartersTexture: hasOwner
                ? LoadTexture(assets, overlays?.PlanetSectorHeadquartersImagePath)
                : null,
            name: planet.Name,
            nameColor: factionColor,
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
                factionColor,
                Color.clear,
                factionColor,
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
