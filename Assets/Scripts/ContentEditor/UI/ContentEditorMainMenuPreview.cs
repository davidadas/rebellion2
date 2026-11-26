using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Rebellion.Util.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Displays the authored main menu and translates launch-button drags into layout edits.
/// </summary>
public sealed class ContentEditorMainMenuPreview
    : UIBehaviour,
        IContentEditorArtworkDragPreview,
        IPointerMoveHandler,
        IPointerExitHandler,
        IPointerClickHandler
{
    [SerializeField]
    private RectTransform viewport;

    [SerializeField]
    private Transform renderWorld;

    [SerializeField]
    private Vector2 referenceResolution;

    [SerializeField]
    private RectTransform[] controls;

    [SerializeField]
    private ContentModelBinding[] medallions;

    [SerializeField]
    private ModelPreviewAtlas atlas;

    private Vector2[] authoredPositions;
    private ContentAssets contentAssets;
    private string[] currentMediaAddresses;
    private MainMenuArtwork artwork;
    private RectTransform[] targets;
    private Image[] selections;
    private Texture2D[] selectionTextures;
    private ContentEditorArtworkEditTarget[] hotspots;
    private int hoveredSlot = -1;
    private ContentModelCache modelCache;
    private Camera[] renderCameras;
    private AutoRotate[] animations;
    private IReadOnlyList<ContentEditorArtworkPlacement> placements;
    private int draggingSlot = -1;
    private Vector2 dragOrigin;
    private Rect dragBounds;
    private Vector2Int dragOffset;

    /// <summary>
    /// Raised when the pointer releases a launch slot at a new position.
    /// </summary>
    public event Action<IReadOnlyList<ContentEditorArtworkPlacementChange>> PlacementChanged;

    internal event Action ArtworkReady;

    /// <summary>
    /// Gets whether the preview animates continuously.
    /// </summary>
    internal bool IsPlaying { get; private set; }

    internal IReadOnlyList<MainMenuArtwork.Control> EditableControls =>
        viewport.GetComponent<MainMenuArtwork>().Controls;

    /// <summary>
    /// Gets the cockpit image displaying a launch medallion.
    /// </summary>
    /// <param name="slot">The launch control in scenario faction order.</param>
    /// <returns>The image displaying the medallion's shared atlas crop.</returns>
    internal RawImage GetMedallionImage(int slot) =>
        controls[slot].Find("Icon3D").GetComponent<RawImage>();

    /// <summary>
    /// Binds the shared menu viewport, launch controls, and render atlas authored by the builder.
    /// </summary>
    /// <param name="surface">The actual gameplay viewport.</param>
    /// <param name="world">The isolated world-space rigs and volumes.</param>
    /// <param name="resolution">The gameplay reference size in source pixels.</param>
    /// <param name="launchControls">The two faction launch controls in slot order.</param>
    /// <param name="launchMedallions">The two models attached to those launch controls.</param>
    /// <param name="iconAtlas">The renderer shared by the menu's model previews.</param>
    public void Configure(
        RectTransform surface,
        Transform world,
        Vector2 resolution,
        RectTransform[] launchControls,
        ContentModelBinding[] launchMedallions,
        ModelPreviewAtlas iconAtlas
    )
    {
        viewport = surface;
        renderWorld = world;
        referenceResolution = resolution;
        controls = launchControls;
        medallions = launchMedallions;
        atlas = iconAtlas;
    }

    /// <summary>
    /// Reports whether the current rendered scene can be reused for the requested artwork.
    /// </summary>
    /// <param name="assets">The working content source.</param>
    /// <param name="addresses">The media address of each editable target.</param>
    /// <returns>True if the scene already uses the same source and artwork.</returns>
    internal bool Matches(ContentAssets assets, string[] addresses) =>
        ReferenceEquals(contentAssets, assets)
        && currentMediaAddresses?.SequenceEqual(addresses, StringComparer.Ordinal) == true;

    /// <summary>
    /// Initializes this preview instance with artwork from the working content pack.
    /// </summary>
    /// <param name="assets">The current content source, including unsaved media imports.</param>
    /// <param name="addresses">The replacement medallion address per playable faction.</param>
    /// <param name="controlArtwork">The configured media for the other menu controls.</param>
    /// <param name="mediaAddresses">All the currently configured asset addresses.</param>
    internal void Initialize(
        ContentAssets assets,
        string[] addresses,
        MainMenuLayout controlArtwork,
        string[] mediaAddresses
    )
    {
        contentAssets = assets;
        currentMediaAddresses = mediaAddresses;
        artwork = viewport.GetComponent<MainMenuArtwork>();
        MainMenuArtwork.Control[] selectionControls = artwork
            .Controls.Where(control => control.Selection != null)
            .ToArray();
        selections = selectionControls.Select(control => control.Selection).ToArray();
        selectionTextures = selectionControls
            .Select(control =>
            {
                string replacement = controlArtwork.FindControl(control.ID)?.SelectionImagePath;
                string address = string.IsNullOrWhiteSpace(replacement)
                    ? control.Selection.GetComponent<ContentSpriteBinding>().Address
                    : replacement;
                return assets.GetReadableTexture(address);
            })
            .ToArray();
        targets = controls
            .Concat(artwork.Controls.Select(control => control.Target))
            .Concat(selections.Select(selection => selection.rectTransform))
            .ToArray();
        authoredPositions = targets.Select(control => control.anchoredPosition).ToArray();
        hotspots = new ContentEditorArtworkEditTarget[targets.Length];
        viewport.GetComponent<AspectRatioFitter>().enabled = false;
        viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.anchoredPosition = Vector2.zero;
        viewport.sizeDelta = referenceResolution;
        artwork.Prepare(controlArtwork, applyLayoutPositions: false);
        foreach (Graphic graphic in viewport.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;
        foreach (Selectable selectable in viewport.GetComponentsInChildren<Selectable>(true))
            selectable.enabled = false;
        foreach (Image selection in selections)
        {
            selection.gameObject.SetActive(true);
            selection.CrossFadeAlpha(1f, 0f, true);
        }

        for (int slot = 0; slot < controls.Length; slot++)
        {
            bool populated = slot < addresses.Length;
            controls[slot].gameObject.SetActive(populated);
            if (populated && !string.IsNullOrWhiteSpace(addresses[slot]))
                medallions[slot].SetAddress(addresses[slot], normalize: true);
        }
        FitViewport();
        Camera[] cameras = renderWorld.GetComponentsInChildren<Camera>(true);
        Camera[] authoredCameras = cameras.Where(camera => camera.enabled).ToArray();
        foreach (Camera camera in authoredCameras)
            camera.enabled = false;
        animations = renderWorld
            .GetComponentsInChildren<AutoRotate>(true)
            .Where(animation => animation.enabled)
            .ToArray();
        foreach (AutoRotate animation in animations)
            animation.enabled = false;
        if (Application.isPlaying)
        {
            renderWorld.SetParent(null, false);
            _ = LoadArtworkAsync(assets, cameras, authoredCameras);
        }
    }

    /// <summary>
    /// Enables live rendering and animation, or keeps the last rendered still frame.
    /// </summary>
    /// <param name="playing">Whether the preview should update continuously.</param>
    internal void SetPlayback(bool playing)
    {
        IsPlaying = playing;
        if (renderCameras == null)
            return;
        foreach (Camera camera in renderCameras)
            camera.enabled = playing;
        foreach (AutoRotate animation in animations)
            animation.enabled = playing;
    }

    /// <summary>
    /// Places each menu item and binds its rendered hotspot to its inspector field.
    /// </summary>
    /// <param name="fields">The editable coordinate pair for each target.</param>
    /// <param name="media">The media field for each target that supports replacement.</param>
    /// <param name="openField">The callback that reveals the selected inspector field.</param>
    internal void Render(
        IReadOnlyList<ContentEditorArtworkPlacement> fields,
        IReadOnlyList<ContentPackEditorField> media,
        Action<ContentPackEditorField> openField
    )
    {
        SetHoveredSlot(-1);
        placements = fields;
        for (int slot = 0; slot < targets.Length; slot++)
        {
            Place(slot, ContentEditorMainMenuSettings.GetOffset(placements[slot]));
            if (targets[slot].name is "Planet" or "Starfield")
                continue;
            hotspots[slot] ??= CreateHotspot(targets[slot]);
            hotspots[slot].Bind(media[slot] ?? placements[slot].XField, openField, default);
            hotspots[slot].GetComponent<RawImage>().raycastTarget = false;
        }
    }

    /// <summary>
    /// Highlights the artwork under the preview pointer.
    /// </summary>
    /// <param name="eventData">The current pointer position.</param>
    public void OnPointerMove(PointerEventData eventData) =>
        SetHoveredSlot(FindHotspot(eventData.position, eventData.enterEventCamera));

    /// <summary>
    /// Clears the highlight when the pointer leaves the preview.
    /// </summary>
    /// <param name="eventData">The pointer-exit event.</param>
    public void OnPointerExit(PointerEventData eventData) => SetHoveredSlot(-1);

    /// <summary>
    /// Opens the exact artwork field under a double click.
    /// </summary>
    /// <param name="eventData">The pointer click.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        int slot = FindHotspot(eventData.position, eventData.pressEventCamera);
        if (slot >= 0 && hotspots[slot].Field.MediaType != ContentPackEditorMediaType.None)
            hotspots[slot].TryOpen(eventData);
    }

    /// <summary>
    /// Resolves controls ahead of the cockpit backdrop.
    /// </summary>
    /// <param name="position">The pointer's screen position.</param>
    /// <param name="camera">The canvas event camera, when one is used.</param>
    /// <returns>The target index, or -1 outside the artwork.</returns>
    private int FindHotspot(Vector2 position, Camera camera)
    {
        if (hotspots == null)
            return -1;
        int cockpit = -1;
        for (int slot = hotspots.Length - 1; slot >= 0; slot--)
        {
            ContentEditorArtworkEditTarget hotspot = hotspots[slot];
            if (
                hotspot?.isActiveAndEnabled != true
                || !hotspot.ContainsScreenPoint(position, camera)
            )
                continue;
            int selectionIndex = slot - controls.Length - artwork.Controls.Count;
            if (
                selectionIndex >= 0
                && !IsSelectionPixel(
                    selections[selectionIndex],
                    selectionTextures[selectionIndex],
                    position,
                    camera
                )
            )
                continue;
            if (targets[slot].name != "Cockpit")
                return slot;
            cockpit = slot;
        }
        return cockpit;
    }

    /// <summary>
    /// Hits only the visible part of the overlay so its transparent center leaves the icon editable.
    /// </summary>
    /// <param name="selection">The overlay drawing the selector.</param>
    /// <param name="texture">The readable artwork used for alpha testing.</param>
    /// <param name="position">The pointer's screen position.</param>
    /// <param name="camera">The canvas event camera, when one is used.</param>
    /// <returns>True when the pointer intersects visible selector artwork.</returns>
    private static bool IsSelectionPixel(
        Image selection,
        Texture2D texture,
        Vector2 position,
        Camera camera
    )
    {
        if (
            texture == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                selection.rectTransform,
                position,
                camera,
                out Vector2 point
            )
        )
            return false;
        Rect bounds = selection.GetPixelAdjustedRect();
        if (selection.preserveAspect)
        {
            Vector2 size = new(texture.width, texture.height);
            float scale = Mathf.Min(bounds.width / size.x, bounds.height / size.y);
            Vector2 fitted = size * scale;
            bounds.position += Vector2.Scale(bounds.size - fitted, selection.rectTransform.pivot);
            bounds.size = fitted;
        }
        if (!bounds.Contains(point))
            return false;
        Vector2 uv = (point - bounds.min) / bounds.size;
        return texture.GetPixelBilinear(uv.x, uv.y).a > 1f / 255f;
    }

    /// <summary>
    /// Updates the border for the artwork under the pointer.
    /// </summary>
    /// <param name="slot">The target index, or -1 to clear the border.</param>
    private void SetHoveredSlot(int slot)
    {
        if (hoveredSlot == slot)
            return;
        if (hoveredSlot >= 0)
            hotspots[hoveredSlot].SetHovered(false);
        hoveredSlot = slot;
        if (slot >= 0)
            hotspots[slot].SetHovered(true);
    }

    /// <summary>
    /// Creates an artwork target for a sprite or model without changing its gameplay graphic.
    /// </summary>
    /// <param name="target">The authored bounds of the artwork and its clickable control.</param>
    /// <returns>The inspector target drawn over the gameplay item.</returns>
    private static ContentEditorArtworkEditTarget CreateHotspot(RectTransform target)
    {
        GameObject overlay = new("Artwork edit target", typeof(RectTransform), typeof(RawImage));
        RectTransform rect = (RectTransform)overlay.transform;
        rect.SetParent(target, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        overlay.GetComponent<RawImage>().color = Color.clear;
        return overlay.AddComponent<ContentEditorArtworkEditTarget>();
    }

    /// <summary>
    /// Refits the 16:9 authored viewport when the inspector width changes.
    /// </summary>
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (authoredPositions != null)
            FitViewport();
    }

    /// <summary>
    /// Restores the detached render world when the authoring surface becomes visible.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        if (renderWorld != null && renderWorld.parent == null)
        {
            renderWorld.gameObject.SetActive(true);
            if (!IsPlaying && renderCameras?.Any(camera => camera.enabled) == true)
                StartCoroutine(PauseAfterFirstFrame());
        }
    }

    /// <summary>
    /// Suspends the world when its owning authoring surface is hidden.
    /// </summary>
    protected override void OnDisable()
    {
        if (renderWorld != null && renderWorld.parent == null)
            renderWorld.gameObject.SetActive(false);
        base.OnDisable();
    }

    /// <summary>
    /// Releases the model resources used by this preview instance.
    /// </summary>
    protected override void OnDestroy()
    {
        if (renderWorld != null && renderWorld.parent == null)
        {
            if (Application.isPlaying)
                Destroy(renderWorld.gameObject);
            else
                DestroyImmediate(renderWorld.gameObject);
        }
        modelCache?.Dispose();
        base.OnDestroy();
    }

    /// <summary>
    /// Finds the artwork under the initial left-button pointer.
    /// </summary>
    /// <param name="eventData">The current pointer gesture.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (placements == null || eventData.button != PointerEventData.InputButton.Left)
            return;
        int slot = FindHotspot(eventData.pressPosition, eventData.pressEventCamera);
        if (
            slot < 0
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                eventData.pressPosition,
                eventData.pressEventCamera,
                out dragOrigin
            )
        )
            return;
        draggingSlot = slot;
        dragOffset = ContentEditorMainMenuSettings.GetOffset(placements[slot]);
        RectTransform control = targets[slot];
        Vector2 pivot = viewport.InverseTransformPoint(control.position);
        Rect bounds = viewport.rect;
        Rect button = control.rect;
        dragBounds = Rect.MinMaxRect(
            bounds.xMin - pivot.x - button.xMin,
            bounds.yMin - pivot.y - button.yMin,
            bounds.xMax - pivot.x - button.xMax,
            bounds.yMax - pivot.y - button.yMax
        );
        if (dragBounds.width <= 0 || dragBounds.height <= 0)
            dragBounds = bounds;
    }

    /// <summary>
    /// Moves the launch button and its model together within the visible viewport.
    /// </summary>
    /// <param name="eventData">The current pointer gesture.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (draggingSlot >= 0)
            Place(draggingSlot, GetDraggedOffset(eventData));
    }

    /// <summary>
    /// Publishes two source-coordinate changes as one undoable move.
    /// </summary>
    /// <param name="eventData">The pointer release.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (draggingSlot < 0)
            return;
        int slot = draggingSlot;
        draggingSlot = -1;
        Vector2Int offset = GetDraggedOffset(eventData);
        Place(slot, offset);
        if (offset == dragOffset)
            return;
        ContentEditorArtworkPlacement fields = placements[slot];
        PlacementChanged?.Invoke(
            new[]
            {
                new ContentEditorArtworkPlacementChange(fields.XField, offset.x),
                new ContentEditorArtworkPlacementChange(fields.YField, offset.y),
            }
        );
    }

    /// <summary>
    /// Loads the authored backdrop and models, then activates the shared atlas.
    /// </summary>
    /// <param name="assets">The working content source.</param>
    /// <param name="cameras">Every camera in the preview.</param>
    /// <param name="authoredCameras">Cameras that render before atlas initialization.</param>
    /// <returns>A task that completes when the model framing is ready.</returns>
    private async Task LoadArtworkAsync(
        ContentAssets assets,
        Camera[] cameras,
        Camera[] authoredCameras
    )
    {
        try
        {
            ContentBindings.Apply(gameObject, assets);
            ContentBindings.Apply(renderWorld.gameObject, assets);
            artwork.ApplyImages(assets);
            modelCache = new ContentModelCache(assets);
            await Task.WhenAll(
                renderWorld
                    .GetComponentsInChildren<ContentModelBinding>(true)
                    .Select(binding => binding.LoadAsync(modelCache))
            );
            if (this == null)
                return;
            foreach (Camera camera in authoredCameras)
                camera.enabled = true;
            atlas.Initialize();
            ArtworkReady?.Invoke();
            renderCameras = cameras.Where(camera => camera.enabled).ToArray();
            if (IsPlaying)
                SetPlayback(true);
            else if (isActiveAndEnabled)
                StartCoroutine(PauseAfterFirstFrame());
        }
        catch (Exception exception)
        {
            if (this != null)
                GameLogger.Warning($"Main menu preview could not render: {exception.Message}");
        }
    }

    /// <summary>
    /// Stops the render cameras after they have filled the preview textures for the first time.
    /// </summary>
    /// <returns>A coroutine that waits until the still frame is rendered.</returns>
    private IEnumerator PauseAfterFirstFrame()
    {
        yield return new WaitForEndOfFrame();
        if (!IsPlaying)
            SetPlayback(false);
    }

    /// <summary>
    /// Translates the pointer position to a clamped reference-pixel offset.
    /// </summary>
    /// <param name="eventData">The pointer event in screen coordinates.</param>
    /// <returns>The launch slot offset with positive Y down.</returns>
    private Vector2Int GetDraggedOffset(PointerEventData eventData)
    {
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 point
            )
        )
            return dragOffset;
        Vector2 delta = point - dragOrigin;
        return dragOffset
            + new Vector2Int(
                Mathf.RoundToInt(Mathf.Clamp(delta.x, dragBounds.xMin, dragBounds.xMax)),
                -Mathf.RoundToInt(Mathf.Clamp(delta.y, dragBounds.yMin, dragBounds.yMax))
            );
    }

    /// <summary>
    /// Applies an authored offset to one slot relative to its generated position.
    /// </summary>
    /// <param name="slot">The launch slot index.</param>
    /// <param name="offset">The source offset, with positive Y down.</param>
    private void Place(int slot, Vector2Int offset)
    {
        Vector2 position = new(offset.x, -offset.y);
        int controlIndex = slot - controls.Length;
        if (controlIndex >= 0 && controlIndex < artwork.Controls.Count)
            artwork.Controls[controlIndex].Move(position);
        else
            targets[slot].anchoredPosition = authoredPositions[slot] + position;
    }

    /// <summary>
    /// Scales the exact gameplay layout into the editor's current preview area.
    /// </summary>
    private void FitViewport()
    {
        Vector2 size = ((RectTransform)transform).rect.size;
        float scale = Mathf.Min(size.x / referenceResolution.x, size.y / referenceResolution.y);
        viewport.localScale = new Vector3(scale, scale, 1f);
    }
}
