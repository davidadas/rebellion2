using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Selects the interaction state displayed by a composed faction-interface preview.
/// </summary>
public enum ContentEditorArtworkPreviewState
{
    Default,
    Disabled,
}

/// <summary>
/// Displays exact gameplay-window prefabs for one faction-artwork surface.
/// </summary>
[ExecuteAlways]
public sealed class ContentEditorArtworkWindowPreview
    : UIBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerMoveHandler,
        IPointerClickHandler,
        IContentEditorArtworkDragPreview,
        IScrollHandler
{
    private const float _maximumScale = 3f;
    private const int _surfacePadding = 12;
    private const int _windowGap = 12;
    private const float _maximumStrategyZoom = 4f;
    private const float _zoomStep = 0.05f;
    internal const string ConfirmationCategory = "Confirmation Window";

    private static readonly IReadOnlyDictionary<string, string[]> _windowNames = new Dictionary<
        string,
        string[]
    >(StringComparer.Ordinal)
    {
        ["Defense window"] = new[] { "DefenseWindow" },
        ["Fleet window"] = new[] { "FleetWindow" },
        ["Missions"] = new[] { "MissionsWindow", "MissionCreateWindow" },
        ["Facilities and construction"] = new[] { "FacilityWindow" },
        ["Status window"] = new[] { "StatusWindow" },
        ["Messages"] = new[] { "MessagesWindow" },
        ["Combat"] = new[] { "BattleAlertWindow" },
        [ConfirmationCategory] = new[] { "ConfirmationDialog" },
        ["Encyclopedia"] = new[] { "EncyclopediaWindow", EncyclopediaDetailWindowName },
        ["Galactic information"] = new[] { "FinderWindow" },
        ["Advisor"] = new[] { "AdvisorReportWindow" },
        ["Strategy view"] = new[] { "StrategyView" },
    };

    public const string EncyclopediaDetailWindowName = "EncyclopediaDetailWindow";

    [SerializeField]
    private RectTransform zoomToolbar;

    [SerializeField]
    private Button zoomIn;

    [SerializeField]
    private Button zoomOut;

    [SerializeField]
    private Button fitZoom;

    [SerializeField]
    private TMP_Text zoomLabel;

    private Texture2D grabCursor;
    private Texture2D grabbingCursor;
    private Texture2D appliedCursor;
    private ContentEditorMacMagnification macMagnification;
    private float strategyZoom = 1f;
    private Vector2 strategyPan;
    private Vector2 panPointer;
    private bool panning;
    private bool pointerInPreview;
    private Vector2 pointerPosition;
    private Camera pointerCamera;
    internal bool CanZoom => activeCategory == "Strategy view" && zoomToolbar != null;

    private string activeCategory;
    private Vector2Int artworkDragDelta;
    private Vector2 artworkDragOrigin;
    private ContentEditorArtworkEditTarget draggedTarget;
    private RectTransform draggedWindow;
    private ContentEditorArtworkEditTarget hoveredTarget;
    private readonly List<ContentPackEditorField> renderedFields = new();

    /// <summary>
    /// Raised when editable artwork is intentionally opened from the composed window.
    /// </summary>
    public event Action<ContentPackEditorField> ArtworkOpened;

    /// <summary>
    /// Raised after a click-drag gesture produces authored source-coordinate replacements.
    /// </summary>
    public event Action<IReadOnlyList<ContentEditorArtworkPlacementChange>> ArtworkMoved;

    /// <summary>
    /// Gets the faction-theme fields currently represented by literal editable images.
    /// </summary>
    internal IReadOnlyList<ContentPackEditorField> RenderedFields => renderedFields;

    /// <summary>
    /// Reports whether an exact gameplay-window preview is authored for a category.
    /// </summary>
    /// <param name="category">The faction-artwork category.</param>
    /// <returns>True when at least one matching window exists.</returns>
    public bool Supports(string category) => GetWindows(category).Count > 0;

    /// <summary>
    /// Gets shared header buttons from the authored window previews for a category.
    /// </summary>
    /// <param name="category">The category supplying the header artwork.</param>
    /// <returns>The press visuals from the windows' button layers.</returns>
    internal IEnumerable<RawImagePressVisual> GetWindowControls(string category) =>
        GetWindows(category)
            .Select(window => window.Find("Buttons"))
            .Where(buttons => buttons != null)
            .SelectMany(buttons => buttons.GetComponentsInChildren<RawImagePressVisual>(true));

    /// <summary>
    /// Binds the Strategy view's own zoom controls.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        if (zoomToolbar == null)
            return;
        zoomIn.onClick.AddListener(() => ChangeZoom(strategyZoom + _zoomStep, Vector2.zero));
        zoomOut.onClick.AddListener(() => ChangeZoom(strategyZoom - _zoomStep, Vector2.zero));
        fitZoom.onClick.AddListener(FitStrategyView);
        grabCursor = ContentEditorHandCursor.Create(closed: false);
        grabbingCursor = ContentEditorHandCursor.Create(closed: true);
        if (Application.platform == RuntimePlatform.OSXPlayer)
            macMagnification = new ContentEditorMacMagnification();
    }

    /// <summary>
    /// Shows the zoom controls above the Strategy view.
    /// </summary>
    /// <param name="top">The toolbar's top offset in the inspector.</param>
    /// <param name="width">The available inspector width.</param>
    internal void PlaceZoomControls(int top, int width)
    {
        if (!CanZoom)
            return;
        const int toolbarWidth = 254;
        UILayout.SetSourceRect(
            zoomToolbar,
            Mathf.Max(4, (width - toolbarWidth) / 2),
            top,
            toolbarWidth,
            34
        );
        zoomToolbar.gameObject.SetActive(true);
        RefreshZoomControls();
    }

    /// <summary>
    /// Restores the full unpanned Strategy view.
    /// </summary>
    private void FitStrategyView()
    {
        strategyZoom = 1f;
        strategyPan = Vector2.zero;
        Layout(GetWindows(activeCategory));
    }

    /// <summary>
    /// Changes the Strategy zoom while keeping an inspection point under the pointer.
    /// </summary>
    /// <param name="scale">The requested zoom relative to fit.</param>
    /// <param name="anchor">The viewport point measured from the center.</param>
    private void ChangeZoom(float scale, Vector2 anchor)
    {
        if (!CanZoom || draggedTarget != null || panning)
            return;
        float next = Mathf.Clamp(scale, 1f, _maximumStrategyZoom);
        strategyPan = anchor + (strategyPan - anchor) * (next / strategyZoom);
        strategyZoom = next;
        ResetHover();
        Layout(GetWindows(activeCategory));
    }

    /// <summary>
    /// Updates the visible zoom percentage and enables only applicable controls.
    /// </summary>
    private void RefreshZoomControls()
    {
        if (zoomToolbar == null)
            return;
        zoomLabel.text = Mathf.RoundToInt(strategyZoom * 100f) + "%";
        zoomOut.interactable = strategyZoom > 1f;
        fitZoom.interactable = strategyZoom > 1f;
        zoomIn.interactable = strategyZoom < _maximumStrategyZoom;
    }

    /// <summary>
    /// Reports whether a primary-button drag will move the targeted artwork.
    /// </summary>
    /// <param name="target">The artwork hit by the pointer.</param>
    /// <returns>True for movable artwork and for the galaxy backdrop while Alt is held.</returns>
    private static bool CanDragArtwork(ContentEditorArtworkEditTarget target) =>
        target?.CanMove == true
        && (
            !target.IsGalaxyBackdrop
            || Input.GetKey(KeyCode.LeftAlt)
            || Input.GetKey(KeyCode.RightAlt)
        );

    /// <summary>
    /// Shows the hand over panning areas and the regular pointer over movable artwork.
    /// </summary>
    private void RefreshCursor()
    {
        if (!Application.isPlaying)
            return;
        Texture2D cursor =
            panning ? grabbingCursor
            : CanZoom && pointerInPreview && draggedTarget == null && !CanDragArtwork(hoveredTarget)
                ? grabCursor
            : null;
        if (cursor == appliedCursor)
            return;
        appliedCursor = cursor;
        Cursor.SetCursor(
            cursor,
            cursor == null ? Vector2.zero : new Vector2(16, 16),
            CursorMode.Auto
        );
    }

    /// <summary>
    /// Refreshes the cursor during modifier changes and applies macOS trackpad zoom.
    /// </summary>
    private void LateUpdate()
    {
        if (!pointerInPreview || !CanZoom)
            return;
        RefreshCursor();
        if (macMagnification == null)
            return;
        if (!Application.isFocused)
        {
            macMagnification.Detach();
            return;
        }
        if (
            macMagnification.TryRead(out float factor)
            && TryGetPreviewPoint(pointerPosition, pointerCamera, out Vector2 point)
        )
            ChangeZoom(strategyZoom * factor, point);
    }

    /// <summary>
    /// Releases cursor textures created for this preview.
    /// </summary>
    protected override void OnDestroy()
    {
        ContentEditorHandCursor.Release(grabCursor);
        ContentEditorHandCursor.Release(grabbingCursor);
        base.OnDestroy();
    }

    /// <summary>
    /// Reports whether the currently rendered controls can visibly represent another artwork state.
    /// </summary>
    /// <param name="surfaceFields">The theme fields owned by the selected interface surface.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <param name="state">The candidate state.</param>
    /// <returns>True when at least one visible target has a distinct, loadable field for the state.</returns>
    internal bool CanRenderState(
        IReadOnlyList<ContentPackEditorField> surfaceFields,
        ContentAssets assets,
        ContentEditorArtworkPreviewState state
    )
    {
        if (surfaceFields == null)
            throw new ArgumentNullException(nameof(surfaceFields));
        if (assets == null)
            throw new ArgumentNullException(nameof(assets));
        if (state == ContentEditorArtworkPreviewState.Default)
            return true;

        return GetComponentsInChildren<ContentEditorArtworkEditTarget>()
            .SelectMany(target => target.Fields)
            .Any(field =>
            {
                ContentPackEditorField stateField = ResolvePreviewStateField(
                    field,
                    surfaceFields,
                    state
                );
                Texture2D stateTexture = ResolveFieldTexture(stateField, assets);
                Texture2D defaultTexture = ResolveFieldTexture(field, assets);
                return stateField != field
                    && stateTexture != null
                    && stateTexture != defaultTexture;
            });
    }

    /// <summary>
    /// Fits the full gameplay window gallery within the available browser space.
    /// </summary>
    /// <param name="category">The faction-artwork category.</param>
    /// <param name="availableWidth">The available browser width.</param>
    /// <param name="availableHeight">The available height for the gallery.</param>
    /// <returns>The preferred displayed surface size.</returns>
    public Vector2Int GetPreferredSize(
        string category,
        int availableWidth,
        int availableHeight = int.MaxValue
    )
    {
        IReadOnlyList<RectTransform> windows = GetWindows(category);
        if (windows.Count == 0)
            return Vector2Int.zero;

        GetSourceBounds(windows, out int sourceWidth, out int sourceHeight);
        float widthScale = (availableWidth - _surfacePadding * 2f) / sourceWidth;
        float heightScale = (availableHeight - _surfacePadding * 2f) / sourceHeight;
        float scale = Mathf.Min(
            _maximumScale,
            Mathf.Max(0.1f, widthScale),
            Mathf.Max(0.1f, heightScale)
        );
        return new Vector2Int(
            Mathf.CeilToInt(sourceWidth * scale) + _surfacePadding * 2,
            Mathf.CeilToInt(sourceHeight * scale) + _surfacePadding * 2
        );
    }

    /// <summary>
    /// Displays and re-themes the exact gameplay windows assigned to one artwork category.
    /// </summary>
    /// <param name="category">The faction-artwork category.</param>
    /// <param name="project">The working pack whose records supply representative content.</param>
    /// <param name="theme">The selected faction theme.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <param name="themeFields">Every editable interface field on the selected faction theme.</param>
    /// <param name="surfaceFields">The theme fields represented by the selected surface.</param>
    /// <param name="themeRecord">The working theme document that owns the layouts.</param>
    /// <param name="state">The interaction state to project across compatible controls.</param>
    public void Render(
        string category,
        ContentPackEditorProject project,
        FactionTheme theme,
        ContentAssets assets,
        IReadOnlyList<ContentPackEditorField> themeFields,
        IReadOnlyList<ContentPackEditorField> surfaceFields,
        ContentPackEditorRecord themeRecord,
        ContentEditorArtworkPreviewState state = ContentEditorArtworkPreviewState.Default
    )
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));
        if (theme == null)
            throw new ArgumentNullException(nameof(theme));
        if (assets == null)
            throw new ArgumentNullException(nameof(assets));
        if (themeFields == null)
            throw new ArgumentNullException(nameof(themeFields));
        if (surfaceFields == null)
            throw new ArgumentNullException(nameof(surfaceFields));

        Clear();
        ContentEditorArtworkPreviewContent content = new(project, theme, assets);
        IReadOnlyList<RectTransform> windows = GetWindows(category);
        gameObject.SetActive(windows.Count > 0);
        foreach (RectTransform window in windows)
            window.gameObject.SetActive(true);
        foreach (RectTransform window in windows)
        {
            foreach (
                ContentEditorArtworkEditTarget target in window.GetComponentsInChildren<ContentEditorArtworkEditTarget>(
                    true
                )
            )
                target.Clear();
            foreach (
                ContentEditorThemeTextureBinding binding in window.GetComponentsInChildren<ContentEditorThemeTextureBinding>(
                    true
                )
            )
            {
                binding.Apply(theme, assets);
            }
            ContentEditorArtworkWindowPreviewRenderer.Render(window, content, theme, assets);
            DisableGameplayInput(window);
            AttachEditCommands(window, themeFields, surfaceFields, assets, state);
            if (category == "Strategy view")
                AttachHudCounterPlacement(window, themeRecord);
        }
        activeCategory = category;
        Layout(windows);
        RefreshCursor();
    }

    /// <summary>
    /// Shows the standard non-faction confirmation dialog in the shared preview surface.
    /// </summary>
    /// <param name="assets">The installed content supplying the dialog artwork.</param>
    internal void RenderConfirmation(ContentAssets assets)
    {
        Clear();
        IReadOnlyList<RectTransform> windows = GetWindows(ConfirmationCategory);
        gameObject.SetActive(windows.Count > 0);
        foreach (RectTransform window in windows)
        {
            ConfirmationDialogView dialog = window.GetComponent<ConfirmationDialogView>();
            dialog.InitializeContent(assets);
            dialog.Show("Are you sure you want to quit?");
            DisableGameplayInput(window);
        }
        activeCategory = ConfirmationCategory;
        Layout(windows);
    }

    /// <summary>
    /// Prevents copied gameplay controls from consuming authoring clicks inside the preview.
    /// </summary>
    /// <param name="window">The copied gameplay window used only for artwork authoring.</param>
    private static void DisableGameplayInput(RectTransform window)
    {
        foreach (Selectable selectable in window.GetComponentsInChildren<Selectable>(true))
            selectable.enabled = false;
    }

    /// <summary>
    /// Makes the rendered HUD counts movable through the same source layouts the game reads.
    /// </summary>
    /// <param name="window">The rendered strategy view.</param>
    /// <param name="themeRecord">The faction theme document that owns the counter layouts.</param>
    private static void AttachHudCounterPlacement(
        RectTransform window,
        ContentPackEditorRecord themeRecord
    )
    {
        StrategyHudView hud = window.GetComponentInChildren<StrategyHudView>(true);
        if (hud == null)
            return;

        string hudMember = nameof(FactionTheme.TacticalHUDLayout);
        XElement hudLayout = themeRecord.Element.Element(hudMember) ?? new XElement(hudMember);
        foreach (TextMeshProUGUI counter in hud.GetComponentsInChildren<TextMeshProUGUI>())
        {
            string layoutName = counter.name switch
            {
                "TickTextField" => nameof(TacticalHUDLayout.TickCounterSourceLayout),
                "RawMaterialsTextField" => nameof(TacticalHUDLayout.RawMaterialsSourceLayout),
                "RefinedMaterialsTextField" => nameof(
                    TacticalHUDLayout.RefinedMaterialsSourceLayout
                ),
                "MaintenanceTextField" => nameof(TacticalHUDLayout.MaintenanceSourceLayout),
                _ => null,
            };
            if (layoutName == null)
                continue;

            RectInt bounds = UILayout.GetSourceRect(counter.rectTransform);
            XElement layout =
                hudLayout.Element(layoutName)
                ?? new XElement(
                    layoutName,
                    new XElement(nameof(SourceRectLayout.X), bounds.x),
                    new XElement(nameof(SourceRectLayout.Y), bounds.y),
                    new XElement(nameof(SourceRectLayout.Width), bounds.width),
                    new XElement(nameof(SourceRectLayout.Height), bounds.height)
                );
            Action attach = () =>
            {
                if (hudLayout.Parent == null)
                    themeRecord.Element.Add(hudLayout);
                if (layout.Parent == null)
                    hudLayout.Add(layout);
            };

            ContentEditorArtworkEditTarget target =
                counter.GetComponent<ContentEditorArtworkEditTarget>()
                ?? counter.gameObject.AddComponent<ContentEditorArtworkEditTarget>();
            target.BindPlacement(
                new ContentEditorArtworkPlacement(
                    ContentPackEditorField.CreateOptional(
                        themeRecord,
                        layout,
                        nameof(SourceRectLayout.X),
                        typeof(int),
                        attachOwner: attach
                    ),
                    ContentPackEditorField.CreateOptional(
                        themeRecord,
                        layout,
                        nameof(SourceRectLayout.Y),
                        typeof(int),
                        attachOwner: attach
                    )
                )
            );
        }
    }

    /// <summary>
    /// Publishes navigation from one rendered editable image.
    /// </summary>
    /// <param name="field">The exact authored field supplying the image.</param>
    private void OpenArtwork(ContentPackEditorField field) => ArtworkOpened?.Invoke(field);

    /// <summary>
    /// Hides every window preview and forgets the active category.
    /// </summary>
    public void Clear()
    {
        CancelArtworkMove();
        panning = false;
        pointerInPreview = false;
        macMagnification?.Detach();
        if (zoomToolbar != null)
            zoomToolbar.gameObject.SetActive(false);
        renderedFields.Clear();
        activeCategory = null;
        RefreshCursor();
        foreach (Transform child in transform)
            child.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Clears transient pointer state before the preview surface becomes inactive.
    /// </summary>
    protected override void OnDisable()
    {
        CancelArtworkMove();
        panning = false;
        pointerInPreview = false;
        macMagnification?.Detach();
        RefreshCursor();
        ResetHover();
        base.OnDisable();
    }

    /// <summary>
    /// Refits the active window gallery after the artwork browser changes size.
    /// </summary>
    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (!string.IsNullOrEmpty(activeCategory))
            Layout(GetWindows(activeCategory));
    }

    /// <summary>
    /// Finds the authored gameplay windows assigned to one category.
    /// </summary>
    /// <param name="category">The faction-artwork category.</param>
    /// <returns>The matching window transforms in display order.</returns>
    private List<RectTransform> GetWindows(string category)
    {
        List<RectTransform> windows = new();
        if (category == null || !_windowNames.TryGetValue(category, out string[] names))
            return windows;

        foreach (string windowName in names)
        {
            if (transform.Find(windowName) is RectTransform window)
                windows.Add(window);
        }
        return windows;
    }

    /// <summary>
    /// Arranges the active windows at an enlarged uniform scale within the preview surface.
    /// </summary>
    /// <param name="windows">The windows to arrange.</param>
    private void Layout(IReadOnlyList<RectTransform> windows)
    {
        if (windows.Count == 0)
            return;

        GetSourceBounds(windows, out int sourceWidth, out int sourceHeight);
        Rect bounds = ((RectTransform)transform).rect;
        float scale = Mathf.Min(
            _maximumScale,
            (bounds.width - _surfacePadding * 2f) / sourceWidth,
            (bounds.height - _surfacePadding * 2f) / sourceHeight
        );
        scale = Mathf.Max(0.1f, scale);
        Vector2 offset = Vector2.zero;
        if (CanZoom)
        {
            scale *= strategyZoom;
            Vector2 overflow = Vector2.Max(
                Vector2.zero,
                (new Vector2(sourceWidth, sourceHeight) * scale - bounds.size) * 0.5f
            );
            strategyPan = new Vector2(
                Mathf.Clamp(strategyPan.x, -overflow.x, overflow.x),
                Mathf.Clamp(strategyPan.y, -overflow.y, overflow.y)
            );
            offset = strategyPan;
            RefreshZoomControls();
        }
        float displayedWidth = sourceWidth * scale;
        float x = (bounds.width - displayedWidth) * 0.5f + offset.x;
        foreach (RectTransform window in windows)
        {
            RectInt source = GetSourceRect(window);
            float centered = (bounds.height - source.height * scale) * 0.5f;
            float y = CanZoom ? centered - offset.y : Mathf.Max(_surfacePadding, centered);
            window.anchorMin = window.anchorMax = window.pivot = Vector2.up;
            window.anchoredPosition = new Vector2(x, -y);
            window.localScale = Vector3.one * scale;
            x += (source.width + _windowGap) * scale;
        }
    }

    /// <summary>
    /// Restores normal artwork for all gameplay controls.
    /// </summary>
    private void ResetHover()
    {
        hoveredTarget = null;
        foreach (
            ContentEditorArtworkEditTarget target in GetComponentsInChildren<ContentEditorArtworkEditTarget>(
                true
            )
        )
            target.SetHovered(false);
    }

    /// <summary>
    /// Adds hover-outline and double-click editing to every active image backed by a pack field.
    /// </summary>
    /// <param name="window">The rendered gameplay surface.</param>
    /// <param name="themeFields">Every editable interface field on the selected faction theme.</param>
    /// <param name="surfaceFields">The selected interface surface's theme fields.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <param name="state">The interaction state displayed by compatible controls.</param>
    private void AttachEditCommands(
        RectTransform window,
        IReadOnlyList<ContentPackEditorField> themeFields,
        IReadOnlyList<ContentPackEditorField> surfaceFields,
        ContentAssets assets,
        ContentEditorArtworkPreviewState state
    )
    {
        Dictionary<string, ContentPackEditorField> fieldsByAddress = new(StringComparer.Ordinal);
        foreach (ContentPackEditorField field in surfaceFields)
            RegisterFieldAddress(field, assets, fieldsByAddress);
        foreach (RawImage image in window.GetComponentsInChildren<RawImage>(true))
        {
            if (!image.gameObject.activeInHierarchy || !image.enabled || image.texture == null)
                continue;
            ContentPackEditorField colorField = ResolveTintColorField(
                image.texture,
                themeFields,
                assets
            );
            if (colorField != null)
            {
                ContentEditorArtworkEditTarget colorTarget =
                    image.GetComponent<ContentEditorArtworkEditTarget>()
                    ?? image.gameObject.AddComponent<ContentEditorArtworkEditTarget>();
                RectInt bounds = assets.GetTextureContentBounds(image.texture as Texture2D);
                colorTarget.Bind(
                    colorField,
                    colorField,
                    OpenArtwork,
                    bounds,
                    bounds,
                    null,
                    Array.Empty<ContentEditorArtworkPlacement>(),
                    null
                );
                if (!renderedFields.Contains(colorField))
                    renderedFields.Add(colorField);
                continue;
            }
            Texture releasedTexture = image.texture;
            ContentPackEditorField releasedField = ResolveBoundField(image, surfaceFields);
            ContentEditorArtworkPlacement advisorPlacement = ResolveAdvisorPlacement(
                image,
                surfaceFields
            );
            releasedField ??= advisorPlacement?.XField;
            releasedField ??= ResolveTextureField(releasedTexture, fieldsByAddress, assets);
            RawImagePressVisual visual = image.GetComponent<RawImagePressVisual>();
            Texture authoredHoverTexture = visual?.PressedTexture;
            ContentPackEditorField hoverField = ResolveCompanionStateField(
                releasedField,
                themeFields
            );
            hoverField ??= ResolveTextureField(authoredHoverTexture, fieldsByAddress, assets);
            hoverField ??= releasedField;
            if (releasedField == null && hoverField == null)
                continue;
            if (state != ContentEditorArtworkPreviewState.Default)
            {
                ContentPackEditorField stateField = ResolvePreviewStateField(
                    releasedField ?? hoverField,
                    surfaceFields,
                    state
                );
                Texture2D stateTexture = ResolveFieldTexture(stateField, assets);
                if (stateField != null && stateTexture != null)
                {
                    releasedField = stateField;
                    hoverField = stateField;
                    releasedTexture = stateTexture;
                    authoredHoverTexture = stateTexture;
                    image.texture = stateTexture;
                }
            }
            Texture hoverTexture = ResolveFieldTexture(hoverField, assets) ?? authoredHoverTexture;
            if (visual != null && hoverTexture != null)
                visual.SetTextures(releasedTexture, hoverTexture);
            ContentEditorArtworkEditTarget target =
                image.GetComponent<ContentEditorArtworkEditTarget>()
                ?? image.gameObject.AddComponent<ContentEditorArtworkEditTarget>();
            target.Bind(
                releasedField,
                hoverField,
                OpenArtwork,
                assets.GetTextureContentBounds(releasedTexture as Texture2D),
                assets.GetTextureContentBounds(hoverTexture as Texture2D),
                visual == null && hoverTexture != releasedTexture ? hoverTexture : null,
                advisorPlacement == null
                    ? ResolvePlacementFields(releasedField, hoverField, themeFields)
                    : new[] { advisorPlacement },
                ResolveInteractionArea(image, window),
                requiresPress: visual != null
            );
            if (advisorPlacement != null && !renderedFields.Contains(advisorPlacement.YField))
                renderedFields.Add(advisorPlacement.YField);
            foreach (ContentPackEditorField field in target.Fields)
                if (!renderedFields.Contains(field))
                    renderedFields.Add(field);
        }
    }

    /// <summary>
    /// Resolves the faction color that produced one rendered shared-mask texture.
    /// </summary>
    /// <param name="texture">The tinted texture rendered by the gameplay surface.</param>
    /// <param name="themeFields">Every editable field on the selected faction theme.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The color field responsible for the tint, or null for ordinary artwork.</returns>
    private static ContentPackEditorField ResolveTintColorField(
        Texture texture,
        IReadOnlyList<ContentPackEditorField> themeFields,
        ContentAssets assets
    )
    {
        if (
            texture is not Texture2D mask
            || !assets.TryGetTintRole(mask, out FactionMaskColor role)
        )
            return null;
        string member = role.ToString();
        return themeFields.FirstOrDefault(field =>
            field.Group == nameof(FactionTheme.ColorScheme) && field.MemberName == member
        );
    }

    /// <summary>
    /// Associates artwork with the larger authored gameplay hit area that represents the same control.
    /// </summary>
    /// <param name="image">The rendered artwork image.</param>
    /// <param name="window">The composed gameplay surface containing the control.</param>
    /// <returns>The matching interaction rectangle, or null when the image itself is the interaction area.</returns>
    private static RectTransform ResolveInteractionArea(RawImage image, RectTransform window)
    {
        if (image == null || window == null || image.name != "SpeedIndicatorImage")
            return null;
        return window
            .GetComponentsInChildren<RectTransform>(true)
            .FirstOrDefault(rect => rect.name == "GameSpeedButton");
    }

    /// <summary>
    /// Binds the animated strategy portraits to the coordinates used to place them in gameplay.
    /// </summary>
    /// <param name="image">The rendered preview image.</param>
    /// <param name="fields">The selected surface's fields.</param>
    /// <returns>The portrait's coordinates, or null for other images.</returns>
    private static ContentEditorArtworkPlacement ResolveAdvisorPlacement(
        RawImage image,
        IReadOnlyList<ContentPackEditorField> fields
    )
    {
        string layout = image.name switch
        {
            "ProtocolImage" => nameof(StrategyAdvisorTheme.ProtocolSourceLayout),
            "DroidImage" => nameof(StrategyAdvisorTheme.DroidSourceLayout),
            _ => null,
        };
        if (layout == null)
            return null;
        var coordinates = fields.Where(field =>
            ContentEditorContentSections.IsAdvisorPlacementField(field)
            && field.XmlElement.Parent.Name.LocalName == layout
        );
        ContentPackEditorField x = coordinates.FirstOrDefault(field =>
            field.MemberName == nameof(SourceRectLayout.X)
        );
        ContentPackEditorField y = coordinates.FirstOrDefault(field =>
            field.MemberName == nameof(SourceRectLayout.Y)
        );
        return x != null && y != null ? new ContentEditorArtworkPlacement(x, y) : null;
    }

    /// <summary>
    /// Resolves the persisted source-coordinate layouts moved with a rendered image.
    /// </summary>
    /// <param name="releasedField">The field supplying the image's released artwork.</param>
    /// <param name="hoverField">The field supplying its hover or pressed artwork.</param>
    /// <param name="fields">Every editable field on the selected faction theme.</param>
    /// <returns>The distinct coordinate pairs that place the rendered image and its hit area.</returns>
    private static IReadOnlyList<ContentEditorArtworkPlacement> ResolvePlacementFields(
        ContentPackEditorField releasedField,
        ContentPackEditorField hoverField,
        IReadOnlyList<ContentPackEditorField> fields
    )
    {
        HashSet<XElement> layouts = new();
        foreach (
            ContentPackEditorField imageField in new[] { releasedField, hoverField }.Where(field =>
                field != null
            )
        )
        {
            XElement owner = imageField.XmlElement.Parent;
            if (owner == null)
                continue;
            AddPlacementLayout(layouts, owner.Element(nameof(GalaxyBackground.SourcePosition)));
            AddPlacementLayout(layouts, owner.Element("SourceLayout"));
            if (owner.Name.LocalName == nameof(StrategyHudButtonTheme))
            {
                AddPlacementLayout(
                    layouts,
                    owner.Element(nameof(StrategyHudButtonTheme.PressedImageLayout))
                );
                AddPlacementLayout(layouts, owner.Element(nameof(StrategyHudButtonTheme.HitArea)));
            }
            if (owner.Name.LocalName == nameof(TacticalHUDLayout.SpeedIndicators))
            {
                AddPlacementLayout(
                    layouts,
                    owner.Parent?.Element(nameof(TacticalHUDLayout.SpeedIndicatorSourceLayout))
                );
                AddPlacementLayout(
                    layouts,
                    owner.Parent?.Element(nameof(TacticalHUDLayout.SpeedContextSourceLayout))
                );
            }
        }

        List<ContentEditorArtworkPlacement> placements = new();
        foreach (XElement layout in layouts)
        {
            XElement xElement = layout.Element(nameof(SourceRectLayout.X));
            XElement yElement = layout.Element(nameof(SourceRectLayout.Y));
            ContentPackEditorField xField = fields.FirstOrDefault(field =>
                field.XmlElement == xElement && field.XmlAttributeName == null
            );
            ContentPackEditorField yField = fields.FirstOrDefault(field =>
                field.XmlElement == yElement && field.XmlAttributeName == null
            );
            if (xField != null && yField != null)
                placements.Add(new ContentEditorArtworkPlacement(xField, yField));
        }
        return placements;
    }

    /// <summary>
    /// Adds one complete X/Y layout to a placement set.
    /// </summary>
    /// <param name="layouts">The distinct layout elements collected for an image.</param>
    /// <param name="layout">The candidate source layout.</param>
    private static void AddPlacementLayout(ISet<XElement> layouts, XElement layout)
    {
        if (
            layout?.Element(nameof(SourceRectLayout.X)) != null
            && layout.Element(nameof(SourceRectLayout.Y)) != null
        )
            layouts.Add(layout);
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
    /// Resolves an already loaded texture to its exact field on the selected theme surface.
    /// </summary>
    /// <param name="texture">The rendered texture.</param>
    /// <param name="fieldsByAddress">The indexed faction-interface fields.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The supplying field, or null when the texture is not owned by this theme surface.</returns>
    private static ContentPackEditorField ResolveTextureField(
        Texture texture,
        IReadOnlyDictionary<string, ContentPackEditorField> fieldsByAddress,
        ContentAssets assets
    )
    {
        if (
            texture is Texture2D texture2D
            && assets.TryGetTextureAddress(texture2D, out string address)
            && fieldsByAddress.TryGetValue(address, out ContentPackEditorField field)
        )
            return field;
        return null;
    }

    /// <summary>
    /// Selects the authored disabled sibling represented by one preview control.
    /// </summary>
    /// <param name="field">The control's default image field.</param>
    /// <param name="fields">The fields owned by the current interface surface.</param>
    /// <param name="state">The requested preview state.</param>
    /// <returns>The matching state field, or the default field when no sibling is configured.</returns>
    private static ContentPackEditorField ResolvePreviewStateField(
        ContentPackEditorField field,
        IReadOnlyList<ContentPackEditorField> fields,
        ContentEditorArtworkPreviewState state
    )
    {
        if (field == null || state == ContentEditorArtworkPreviewState.Default)
            return field;

        string[] members = new[] { "DisabledImagePath" };
        foreach (string member in members)
        {
            ContentPackEditorField candidate = fields.FirstOrDefault(item =>
                item.MemberName == member && item.XmlElement.Parent == field.XmlElement.Parent
            );
            if (candidate != null)
                return candidate;
        }
        return field;
    }

    /// <summary>
    /// Finds the sibling field supplying the literal hover or pressed state of a rendered control.
    /// </summary>
    /// <param name="releasedField">The field supplying the released state.</param>
    /// <param name="fields">Every editable interface field on the selected faction theme.</param>
    /// <returns>The exact companion-state field, or the released field when the artwork is static.</returns>
    private static ContentPackEditorField ResolveCompanionStateField(
        ContentPackEditorField releasedField,
        IReadOnlyList<ContentPackEditorField> fields
    )
    {
        if (releasedField == null)
            return null;
        string[] members = releasedField.MemberName switch
        {
            "InactiveImagePath" => new[] { "ActiveImagePath" },
            "UpImagePath" => new[] { "PressedImagePath", "DownImagePath" },
            "DefaultImagePath" => new[] { "HighlightedImagePath" },
            "NormalImagePath" => new[] { "HoverImagePath", "SelectedImagePath" },
            _ => Array.Empty<string>(),
        };
        foreach (string member in members)
        {
            ContentPackEditorField companion = fields.FirstOrDefault(candidate =>
                candidate.MemberName == member
                && candidate.XmlElement.Parent == releasedField.XmlElement.Parent
            );
            if (companion != null)
                return companion;
        }
        return releasedField;
    }

    /// <summary>
    /// Loads the texture for one already selected authored field.
    /// </summary>
    /// <param name="field">The selected image field.</param>
    /// <param name="assets">The working pack's resolved artwork.</param>
    /// <returns>The texture, or null when the field is empty or invalid.</returns>
    private static Texture2D ResolveFieldTexture(ContentPackEditorField field, ContentAssets assets)
    {
        if (
            field?.MediaType != ContentPackEditorMediaType.Image
            || string.IsNullOrWhiteSpace(field.Value)
        )
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
    /// Routes entry through transparent gameplay hit areas to the literal rendered artwork below.
    /// </summary>
    /// <param name="eventData">The pointer-enter event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        OnPointerMove(eventData);
        RefreshCursor();
    }

    /// <summary>
    /// Keeps the outlined edit target synchronized while the pointer crosses composed controls.
    /// </summary>
    /// <param name="eventData">The pointer-move event.</param>
    public void OnPointerMove(PointerEventData eventData)
    {
        if (eventData == null)
            return;
        pointerInPreview = true;
        pointerPosition = eventData.position;
        pointerCamera = eventData.enterEventCamera;
        UpdatePointerTarget(eventData);
    }

    /// <summary>
    /// Clears preview-routed hover state after the pointer leaves the composed surface.
    /// </summary>
    /// <param name="eventData">The pointer-exit event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        hoveredTarget?.SetHovered(false);
        hoveredTarget = null;
        pointerInPreview = false;
        macMagnification?.Detach();
        RefreshCursor();
    }

    /// <summary>
    /// Opens artwork beneath transparent gameplay hit areas on a primary-button double click.
    /// </summary>
    /// <param name="eventData">The completed pointer gesture.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData?.used != false)
            return;
        UpdatePointerTarget(eventData);
        hoveredTarget?.TryOpen(eventData);
    }

    /// <summary>
    /// Pans from the preview's backdrop or moves the artwork beneath a primary-button grab.
    /// </summary>
    /// <param name="eventData">The initial drag event.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData == null)
            return;
        bool primary = eventData.button == PointerEventData.InputButton.Left;
        ContentEditorArtworkEditTarget directTarget = primary
            ? eventData.pointerDrag?.GetComponent<ContentEditorArtworkEditTarget>()
            : null;
        if (primary && !CanDragArtwork(directTarget))
            UpdatePointerTarget(eventData, fromPress: true);
        ContentEditorArtworkEditTarget target = CanDragArtwork(directTarget)
            ? directTarget
            : hoveredTarget;
        if (CanZoom && (!primary || !CanDragArtwork(target)))
        {
            if (
                strategyZoom > 1f
                && TryGetPreviewPointer(eventData, out panPointer, fromPress: true)
            )
            {
                panning = true;
                ResetHover();
                RefreshCursor();
            }
            eventData.Use();
            return;
        }
        if (!primary)
            return;
        RectTransform window = GetWindows(activeCategory)
            .FirstOrDefault(candidate => target?.transform.IsChildOf(candidate) == true);
        if (
            target == null
            || window == null
            || !target.BeginMove(eventData)
            || !TryGetSourcePoint(window, eventData, out artworkDragOrigin, fromPress: true)
        )
        {
            target?.CancelMove();
            return;
        }

        draggedTarget = target;
        draggedWindow = window;
        artworkDragDelta = Vector2Int.zero;
        RefreshCursor();
        eventData.Use();
    }

    /// <summary>
    /// Moves grabbed artwork in the preview using the Strategy surface's source-coordinate scale.
    /// </summary>
    /// <param name="eventData">The current drag event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        if (panning && TryGetPreviewPointer(eventData, out Vector2 local))
        {
            strategyPan += local - panPointer;
            panPointer = local;
            Layout(GetWindows(activeCategory));
            eventData.Use();
            return;
        }
        if (
            draggedTarget == null
            || draggedWindow == null
            || eventData?.button != PointerEventData.InputButton.Left
            || !TryGetSourcePoint(draggedWindow, eventData, out Vector2 point)
        )
            return;
        artworkDragDelta = new Vector2Int(
            Mathf.RoundToInt(point.x - artworkDragOrigin.x),
            Mathf.RoundToInt(point.y - artworkDragOrigin.y)
        );
        draggedTarget.PreviewMove(artworkDragDelta);
        eventData.Use();
    }

    /// <summary>
    /// Completes a placement gesture and publishes the underlying theme-field changes.
    /// </summary>
    /// <param name="eventData">The final drag event.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (panning)
        {
            panning = false;
            if (pointerInPreview)
                UpdatePointerTarget(eventData);
            RefreshCursor();
            eventData?.Use();
            return;
        }
        if (draggedTarget == null)
            return;
        if (eventData?.button == PointerEventData.InputButton.Left)
            OnDrag(eventData);
        IReadOnlyList<ContentEditorArtworkPlacementChange> changes = draggedTarget.CompleteMove(
            artworkDragDelta
        );
        draggedTarget = null;
        draggedWindow = null;
        artworkDragDelta = Vector2Int.zero;
        if (changes.Count > 0)
            ArtworkMoved?.Invoke(changes);
        if (pointerInPreview)
            UpdatePointerTarget(eventData);
        RefreshCursor();
        eventData?.Use();
    }

    /// <summary>
    /// Zooms the Strategy preview under the pointer or forwards scrolling to the inspector.
    /// </summary>
    /// <param name="eventData">The pointer wheel gesture.</param>
    public void OnScroll(PointerEventData eventData)
    {
        if (CanZoom && TryGetPreviewPointer(eventData, out Vector2 point))
        {
            if (eventData.scrollDelta.y != 0f)
            {
                ChangeZoom(strategyZoom + _zoomStep * eventData.scrollDelta.y, point);
                OnPointerMove(eventData);
            }
            eventData.Use();
            return;
        }
        ExecuteEvents.ExecuteHierarchy(
            transform.parent.gameObject,
            eventData,
            ExecuteEvents.scrollHandler
        );
    }

    /// <summary>
    /// Locates a pointer relative to the preview center in viewport coordinates.
    /// </summary>
    /// <param name="eventData">The pointer gesture.</param>
    /// <param name="point">Receives the editable viewport point.</param>
    /// <param name="fromPress">Whether to use the initial button-down position.</param>
    /// <returns>Whether the pointer position could be projected.</returns>
    private bool TryGetPreviewPointer(
        PointerEventData eventData,
        out Vector2 point,
        bool fromPress = false
    ) =>
        TryGetPreviewPoint(
            fromPress ? eventData.pressPosition : eventData.position,
            eventData.pressEventCamera ?? eventData.enterEventCamera,
            out point
        );

    /// <summary>
    /// Converts a screen point to an offset from the center of the preview viewport.
    /// </summary>
    /// <param name="position">The point in screen coordinates.</param>
    /// <param name="camera">The camera used to project the pointer.</param>
    /// <param name="point">The resulting viewport offset.</param>
    /// <returns>Whether the screen point could be projected.</returns>
    private bool TryGetPreviewPoint(Vector2 position, Camera camera, out Vector2 point)
    {
        RectTransform viewport = (RectTransform)transform;
        bool relative = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            viewport,
            position,
            camera,
            out point
        );
        point -= viewport.rect.center;
        return relative;
    }

    /// <summary>
    /// Converts a pointer event into top-left coordinates within one unscaled gameplay window.
    /// </summary>
    /// <param name="window">The source-sized gameplay window.</param>
    /// <param name="eventData">The pointer event to convert.</param>
    /// <param name="point">Receives the source-space point.</param>
    /// <param name="fromPress">Whether to use the initial button-down position.</param>
    /// <returns>True when the pointer can be converted.</returns>
    private static bool TryGetSourcePoint(
        RectTransform window,
        PointerEventData eventData,
        out Vector2 point,
        bool fromPress = false
    )
    {
        point = default;
        if (
            window == null
            || eventData == null
            || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                window,
                fromPress ? eventData.pressPosition : eventData.position,
                eventData.pressEventCamera ?? eventData.enterEventCamera,
                out Vector2 local
            )
        )
            return false;
        Rect rect = window.rect;
        point = new Vector2(local.x - rect.xMin, rect.yMax - local.y);
        return true;
    }

    /// <summary>
    /// Restores an interrupted placement gesture without committing coordinates.
    /// </summary>
    private void CancelArtworkMove()
    {
        draggedTarget?.CancelMove();
        draggedTarget = null;
        draggedWindow = null;
        artworkDragDelta = Vector2Int.zero;
    }

    /// <summary>
    /// Selects the highest visible authored image beneath the pointer.
    /// </summary>
    /// <param name="eventData">The current pointer event.</param>
    /// <param name="fromPress">Whether to locate the image beneath the original press.</param>
    private void UpdatePointerTarget(PointerEventData eventData, bool fromPress = false)
    {
        if (eventData == null || panning || draggedTarget != null)
            return;
        Camera camera = eventData.pressEventCamera ?? eventData.enterEventCamera;
        Vector2 position = fromPress ? eventData.pressPosition : eventData.position;
        ContentEditorArtworkEditTarget next =
            GetComponentsInChildren<ContentEditorArtworkEditTarget>()
                .Where(target => target.Field != null || target.CanMove)
                .Where(target => target.ContainsScreenPoint(position, camera))
                .OrderByDescending(target => target.GetComponent<Graphic>().depth)
                .ThenBy(target =>
                {
                    Rect rect = ((RectTransform)target.transform).rect;
                    return rect.width * rect.height;
                })
                .FirstOrDefault();
        if (next == hoveredTarget)
            return;
        hoveredTarget?.SetHovered(false);
        hoveredTarget = next;
        hoveredTarget?.SetHovered(true);
    }

    /// <summary>
    /// Resolves a semantic prefab binding to the exact field on the selected faction page.
    /// </summary>
    /// <param name="image">The rendered image.</param>
    /// <param name="fields">The selected surface's fields.</param>
    /// <returns>The exact bound field, or null when the image has no semantic binding.</returns>
    private static ContentPackEditorField ResolveBoundField(
        RawImage image,
        IReadOnlyList<ContentPackEditorField> fields
    )
    {
        string memberPath = image.GetComponent<ContentEditorThemeTextureBinding>()?.MemberPath;
        if (string.IsNullOrWhiteSpace(memberPath))
            return null;
        string[] members = memberPath.Split('.');
        return fields.FirstOrDefault(candidate =>
        {
            if (candidate.MemberName != members[^1])
                return false;
            string[] candidatePath = candidate
                .XmlElement.AncestorsAndSelf()
                .TakeWhile(element => element != candidate.Record.Element)
                .Reverse()
                .Select(element => element.Name.LocalName)
                .ToArray();
            return candidatePath.Length <= members.Length
                && candidatePath.SequenceEqual(members.Skip(members.Length - candidatePath.Length));
        });
    }

    /// <summary>
    /// Measures the combined source-space window bounds before scaling.
    /// </summary>
    /// <param name="windows">The windows to measure.</param>
    /// <param name="width">Receives the combined width.</param>
    /// <param name="height">Receives the greatest height.</param>
    private static void GetSourceBounds(
        IReadOnlyList<RectTransform> windows,
        out int width,
        out int height
    )
    {
        width = 0;
        height = 0;
        for (int index = 0; index < windows.Count; index++)
        {
            RectInt source = GetSourceRect(windows[index]);
            width += source.width;
            height = Mathf.Max(height, source.height);
        }
        width += Mathf.Max(0, windows.Count - 1) * _windowGap;
    }

    /// <summary>
    /// Reads a stable positive source rectangle from an authored window root.
    /// </summary>
    /// <param name="window">The authored window.</param>
    /// <returns>The window's source rectangle.</returns>
    private static RectInt GetSourceRect(RectTransform window)
    {
        RectInt source = UILayout.GetSourceRect(window);
        return new RectInt(
            source.x,
            source.y,
            Mathf.Max(1, source.width),
            Mathf.Max(1, source.height)
        );
    }
}
