using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents galaxy and sector previews and translates pointer gestures into authored coordinates.
/// </summary>
public sealed class ContentEditorMapView : MonoBehaviour
{
    [SerializeField]
    private RawImage mapImage;

    [SerializeField]
    private AspectRatioFitter mapAspect;

    [SerializeField]
    private ContentEditorGalaxyPreview galaxyPreview;

    [SerializeField]
    private ContentEditorSectorPreview sectorPreview;

    [SerializeField]
    private ContentEditorMapViewport mapViewport;

    [SerializeField]
    private Button mapBackButton;

    [SerializeField]
    private Button mapFitButton;

    [SerializeField]
    private Button mapArrangeButton;

    [SerializeField]
    private TMP_Dropdown mapFactionDropdown;

    [SerializeField]
    private TextMeshProUGUI mapHint;

    private IReadOnlyList<(string Name, FactionTheme Theme)> themes = Array.Empty<(
        string,
        FactionTheme
    )>();
    private ContentPackEditorProject project;
    private ContentAssets assets;
    private ContentPackEditorRecord record;
    private ContentPackEditorRecord sector;
    private Vector2Int sourceSize;
    private string visibleFrame;
    private bool bound;

    public bool HasOpenChoice => mapFactionDropdown.IsExpanded;

    public event Action BackRequested;
    public event Action ArrangeRequested;
    public event Action<ContentPackEditorRecord> Selected;
    public event Action<ContentPackEditorRecord> Opened;
    public event Action<ContentPackEditorRecord> DragStarted;
    public event Action<ContentPackEditorRecord, Vector2Int> Moved;
    public event Action DragEnded;

    /// <summary>
    /// Connects the authored map controls once when first displayed.
    /// </summary>
    private void Awake() => BindControls();

    /// <summary>
    /// Verifies the authored map and connects its interaction events.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;
        if (
            mapImage == null
            || mapAspect == null
            || galaxyPreview == null
            || sectorPreview == null
            || mapViewport == null
            || mapBackButton == null
            || mapFitButton == null
            || mapArrangeButton == null
            || mapFactionDropdown == null
            || mapHint == null
        )
            throw new MissingReferenceException(name + " has incomplete map references.");
        bound = true;
        mapBackButton.onClick.AddListener(RequestBack);
        mapFitButton.onClick.AddListener(mapViewport.ResetView);
        mapArrangeButton.onClick.AddListener(RequestArrange);
        mapFactionDropdown.onValueChanged.AddListener(ChangeFaction);
        mapViewport.ViewChanged += RefreshFitButton;
        galaxyPreview.Selected += SelectMarker;
        galaxyPreview.Opened += OpenMarker;
        galaxyPreview.DragStarted += BeginMarkerDrag;
        galaxyPreview.Moved += MoveMarker;
        galaxyPreview.DragEnded += EndDrag;
        sectorPreview.Selected += SelectPlanet;
        sectorPreview.DragStarted += BeginPlanetDrag;
        sectorPreview.Moved += MovePlanet;
        sectorPreview.DragEnded += EndDrag;
    }

    /// <summary>
    /// Releases control and preview subscriptions with the map.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        mapBackButton.onClick.RemoveListener(RequestBack);
        mapFitButton.onClick.RemoveListener(mapViewport.ResetView);
        mapArrangeButton.onClick.RemoveListener(RequestArrange);
        mapFactionDropdown.onValueChanged.RemoveListener(ChangeFaction);
        mapViewport.ViewChanged -= RefreshFitButton;
        galaxyPreview.Selected -= SelectMarker;
        galaxyPreview.Opened -= OpenMarker;
        galaxyPreview.DragStarted -= BeginMarkerDrag;
        galaxyPreview.Moved -= MoveMarker;
        galaxyPreview.DragEnded -= EndDrag;
        sectorPreview.Selected -= SelectPlanet;
        sectorPreview.DragStarted -= BeginPlanetDrag;
        sectorPreview.Moved -= MovePlanet;
        sectorPreview.DragEnded -= EndDrag;
    }

    /// <summary>
    /// Rebinds inherited presentation choices without retaining another project's assets.
    /// </summary>
    /// <param name="value">The opened authoring project.</param>
    /// <param name="contentAssets">Its owned content assets.</param>
    public void Bind(ContentPackEditorProject value, ContentAssets contentAssets)
    {
        BindControls();
        if (project != value)
            Clear();
        project = value;
        assets = contentAssets;
        themes = ContentEditorPresentation.GetFactionThemes(value);
        mapFactionDropdown.ClearOptions();
        mapFactionDropdown.AddOptions(
            new[] { "Icons: Neutral" }
                .Concat(themes.Select(theme => "Icons: " + theme.Name))
                .ToList()
        );
        mapFactionDropdown.SetValueWithoutNotify(0);
    }

    /// <summary>
    /// Releases project references and hides both preview pools.
    /// </summary>
    public void Clear()
    {
        galaxyPreview.Clear();
        sectorPreview.Clear();
        project = null;
        assets = null;
        record = sector = null;
        themes = Array.Empty<(string, FactionTheme)>();
        visibleFrame = null;
        mapImage.texture = null;
        mapFactionDropdown.ClearOptions();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Renders the galaxy or one sector through the gameplay presentation components.
    /// </summary>
    /// <param name="selected">The selected map definition.</param>
    /// <param name="parent">The open sector, or null for the galaxy.</param>
    public void Render(ContentPackEditorRecord selected, ContentPackEditorRecord parent)
    {
        record = selected;
        sector = parent;
        mapBackButton.gameObject.SetActive(sector != null);
        mapFactionDropdown.transform.parent.gameObject.SetActive(sector != null);
        mapHint.text =
            sector == null
                ? "Click to select; double-click to open a sector. Drag to move. Wheel to zoom; right-drag to pan."
                : "Select a planet to edit it. Drag planets to move them. Wheel to zoom; right-drag to pan.";
        string frame = sector?.Key ?? "galaxy";
        if (visibleFrame != frame)
            mapViewport.ResetView();
        visibleFrame = frame;
        if (sector != null)
        {
            galaxyPreview.Hide();
            mapImage.texture = null;
            mapImage.color = Color.black;
            mapAspect.aspectRatio = sectorPreview.AspectRatio;
            FactionTheme theme =
                mapFactionDropdown.value > 0 ? themes[mapFactionDropdown.value - 1].Theme : null;
            sectorPreview.Render(project, sector, assets, theme);
            return;
        }
        sectorPreview.Hide();
        FactionTheme neutral = ContentEditorPresentation.GetNeutralTheme(project);
        Texture2D star = assets.GetTexture(neutral.GalaxyBackground.PlanetIcons?.Small);
        Texture2D background = assets.GetTexture("Pack/Shared/Strategy/UI/Shell/galaxy-background");
        sourceSize = GetGalaxySourceSize();
        mapImage.texture = background;
        mapImage.color = new Color(0.6f, 0.6f, 0.6f, 1);
        mapAspect.aspectRatio = (float)sourceSize.x / sourceSize.y;
        galaxyPreview.Render(record.Catalog.Records.ToArray(), star, sourceSize);
    }

    /// <summary>
    /// Gets a default planet position in the sector's fixed source coordinates.
    /// </summary>
    /// <returns>The visible sector's center offset.</returns>
    public Vector2Int GetPlacementOffset() => sectorPreview.GetPlacementOffset();

    /// <summary>
    /// Gets an initial sector position independently of the currently displayed inspector.
    /// </summary>
    /// <returns>The center of the galaxy's authored coordinate frame.</returns>
    public Vector2Int GetGalaxyPlacementPosition() => GetGalaxySourceSize() / 2;

    /// <summary>
    /// Captures the currently visible frame without changing authored coordinates.
    /// </summary>
    /// <returns>The sector or galaxy geometry snapshot for a layout request.</returns>
    internal ContentEditorMapLayout CreateLayout() =>
        sector == null
            ? galaxyPreview.CreateLayout(sourceSize)
            : sectorPreview.CreateLayout(sector);

    /// <summary>
    /// Requests a coordinate layout independently of the viewport's zoom and pan.
    /// </summary>
    private void RequestArrange() => ArrangeRequested?.Invoke();

    /// <summary>
    /// Resolves the galaxy coordinate frame through the opened pack's background artwork.
    /// </summary>
    /// <returns>The background dimensions in source units.</returns>
    private Vector2Int GetGalaxySourceSize()
    {
        Texture2D background = assets.GetTexture("Pack/Shared/Strategy/UI/Shell/galaxy-background");
        return background == null
            ? new Vector2Int(640, 480)
            : UILayout.GetTextureSourceSize(background);
    }

    /// <summary>
    /// Requests the galaxy without editing the selected sector.
    /// </summary>
    private void RequestBack() => BackRequested?.Invoke();

    /// <summary>
    /// Updates the fit command after a viewport gesture.
    /// </summary>
    private void RefreshFitButton() => mapFitButton.interactable = !mapViewport.IsFitted;

    /// <summary>
    /// Changes only the displayed faction artwork.
    /// </summary>
    /// <param name="index">The selected presentation choice.</param>
    private void ChangeFaction(int index) => Render(record, sector);

    /// <summary>
    /// Publishes the selected galaxy definition.
    /// </summary>
    /// <param name="marker">The selected marker.</param>
    private void SelectMarker(ContentEditorMapMarker marker) => Selected?.Invoke(marker.Record);

    /// <summary>
    /// Requests the selected sector's interior.
    /// </summary>
    /// <param name="marker">The opened marker.</param>
    private void OpenMarker(ContentEditorMapMarker marker) => Opened?.Invoke(marker.Record);

    /// <summary>
    /// Begins one grouped sector move.
    /// </summary>
    /// <param name="marker">The dragged marker.</param>
    private void BeginMarkerDrag(ContentEditorMapMarker marker) =>
        DragStarted?.Invoke(marker.Record);

    /// <summary>
    /// Converts normalized galaxy movement to authored source coordinates.
    /// </summary>
    /// <param name="marker">The dragged marker.</param>
    /// <param name="position">Its normalized position.</param>
    private void MoveMarker(ContentEditorMapMarker marker, Vector2 position)
    {
        Moved?.Invoke(
            marker.Record,
            new Vector2Int(
                Mathf.RoundToInt(position.x * sourceSize.x),
                Mathf.RoundToInt((1 - position.y) * sourceSize.y)
            )
        );
        marker.SetPosition(position);
    }

    /// <summary>
    /// Publishes the selected planet.
    /// </summary>
    /// <param name="planet">The selected definition.</param>
    private void SelectPlanet(ContentPackEditorRecord planet) => Opened?.Invoke(planet);

    /// <summary>
    /// Begins one grouped planet move.
    /// </summary>
    /// <param name="planet">The dragged definition.</param>
    private void BeginPlanetDrag(ContentPackEditorRecord planet) => DragStarted?.Invoke(planet);

    /// <summary>
    /// Publishes coordinates and refreshes the fixed sector projection.
    /// </summary>
    /// <param name="planet">The dragged definition.</param>
    /// <param name="position">Its absolute authored coordinates.</param>
    private void MovePlanet(ContentPackEditorRecord planet, Vector2Int position)
    {
        Moved?.Invoke(planet, position);
        Render(record, sector);
    }

    /// <summary>
    /// Completes a gesture even when its view is disabled or its final pointer cannot resolve.
    /// </summary>
    private void EndDrag() => DragEnded?.Invoke();
}
