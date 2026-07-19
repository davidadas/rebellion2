using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Renders an authored planet-system window and reports semantic planet interaction.
/// </summary>
public sealed class PlanetSystemWindowView : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI systemNameTextField;

    [SerializeField]
    private RectTransform planetsRoot;

    [SerializeField]
    private PlanetSystemPlanetView planetPrefab;

    [SerializeField]
    private float sectorCoordinateRange;

    [SerializeField]
    private float sectorCoordinateScaleX;

    [SerializeField]
    private float sectorCoordinateScaleY;

    [SerializeField]
    private float galaxyProjectionSourceRange;

    [SerializeField]
    private float galaxyProjectionWidth;

    [SerializeField]
    private float galaxyProjectionHeight;

    [SerializeField]
    private int planetPositionOffsetY;

    private readonly List<PlanetSystemPlanetView> planetViews = new List<PlanetSystemPlanetView>();

    /// <summary>
    /// Occurs when the control is clicked.
    /// </summary>
    internal event Action<
        PlanetSystemWindowView,
        PlanetSystemWindowElement,
        PointerEventData
    > Clicked;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    internal event Action<PlanetSystemWindowView> Destroyed;

    /// <summary>
    /// Occurs when the pointer hover is cleared.
    /// </summary>
    internal event Action<PlanetSystemWindowView> HoverCleared;

    /// <summary>
    /// Occurs when the pointer hovers over the control.
    /// </summary>
    internal event Action<
        PlanetSystemWindowView,
        PlanetSystemWindowElement,
        PointerEventData
    > Hovered;

    /// <summary>
    /// Occurs when the control is pressed.
    /// </summary>
    internal event Action<
        PlanetSystemWindowView,
        PlanetSystemWindowElement,
        PointerEventData
    > Pressed;

    /// <summary>
    /// Occurs when the control is released.
    /// </summary>
    internal event Action<
        PlanetSystemWindowView,
        PlanetSystemWindowElement,
        PointerEventData
    > Released;

    /// <summary>
    /// Applies one complete planet-system presentation.
    /// </summary>
    /// <param name="data">The immutable window presentation.</param>
    public void Render(PlanetSystemWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetTextContent(systemNameTextField, data.Title);
        RectInt windowBounds = UILayout.GetSourceRect(transform as RectTransform);
        for (int index = 0; index < data.Planets.Count; index++)
        {
            PlanetSystemPlanetRenderData planet = data.Planets[index];
            GetPlanetView(index)
                .Render(
                    planet,
                    ProjectPlanetPosition(
                        planet.GalaxyOffset,
                        windowBounds.width,
                        windowBounds.height
                    )
                );
        }
        for (int index = data.Planets.Count; index < planetViews.Count; index++)
            planetViews[index].gameObject.SetActive(false);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Projects one galaxy offset through the prefab-authored window layout.
    /// </summary>
    /// <param name="galaxyOffset">The planet's projected galaxy offset.</param>
    /// <param name="windowWidth">The authored window width.</param>
    /// <param name="windowHeight">The authored window height.</param>
    /// <returns>The source-space planet image position.</returns>
    private Vector2Int ProjectPlanetPosition(
        Vector2Int galaxyOffset,
        int windowWidth,
        int windowHeight
    )
    {
        float localX = UnprojectGalaxyDelta(galaxyOffset.x, galaxyProjectionWidth);
        float localY = UnprojectGalaxyDelta(galaxyOffset.y, galaxyProjectionHeight);
        int x = Mathf.FloorToInt(
            localX / sectorCoordinateRange * sectorCoordinateScaleX * windowWidth
        );
        int y =
            Mathf.FloorToInt(localY / sectorCoordinateRange * sectorCoordinateScaleY * windowHeight)
            + planetPositionOffsetY;
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Reverses one projected galaxy coordinate delta using authored projection geometry.
    /// </summary>
    /// <param name="delta">The projected coordinate delta.</param>
    /// <param name="projectedExtent">The authored projected coordinate extent.</param>
    /// <returns>The corresponding source-space coordinate delta.</returns>
    private float UnprojectGalaxyDelta(int delta, float projectedExtent)
    {
        return projectedExtent == 0f
            ? delta
            : delta * galaxyProjectionSourceRange / projectedExtent;
    }

    /// <summary>
    /// Tries to resolve a semantic planet hit from a pointer event.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="element">Receives the semantic presentation element.</param>
    /// <returns>True when an active planet view was hit.</returns>
    internal bool TryCreateElement(
        PointerEventData eventData,
        out PlanetSystemWindowElement element
    )
    {
        PlanetSystemPlanetView view = GetRaycastTarget(eventData)
            ?.GetComponentInParent<PlanetSystemPlanetView>();
        if (
            view != null
            && planetViews.Contains(view)
            && view.gameObject.activeInHierarchy
            && view.TryCreateElement(eventData, out element)
        )
            return true;

        element = null;
        return false;
    }

    /// <summary>
    /// Tries to create a fleet drag preview from a rendered planet icon.
    /// </summary>
    /// <param name="element">The selected fleet presentation element.</param>
    /// <param name="windowX">The source-space horizontal window position.</param>
    /// <param name="windowY">The source-space vertical window position.</param>
    /// <param name="sourceX">The source-space horizontal pointer position.</param>
    /// <param name="sourceY">The source-space vertical pointer position.</param>
    /// <param name="preview">Receives the drag preview.</param>
    /// <returns>True when a visible fleet icon produced a preview.</returns>
    internal bool TryGetFleetDragPreview(
        PlanetSystemWindowElement element,
        int windowX,
        int windowY,
        int sourceX,
        int sourceY,
        out DragPreview preview
    )
    {
        preview = null;
        if (element?.Icon != PlanetIcon.Fleet)
            return false;

        PlanetSystemPlanetView view = GetActivePlanetView(element.PlanetIndex);
        if (view == null || !view.TryGetFleetDragImage(out Texture texture, out RectTransform rect))
            return false;

        RectInt planetRect = UILayout.GetSourceRect(view.transform as RectTransform);
        RectInt iconRect = UILayout.GetSourceRect(rect);
        RectInt sourceRect = new RectInt(
            windowX + planetRect.x + iconRect.x,
            windowY + planetRect.y + iconRect.y,
            iconRect.width,
            iconRect.height
        );
        preview = UILayout.CreateDragPreview(texture, sourceRect, sourceX, sourceY);
        return preview != null;
    }

    /// <summary>
    /// Verifies authored references when the prefab instance loads.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
    }

    /// <summary>
    /// Releases child subscriptions and notifies the owning controller.
    /// </summary>
    private void OnDestroy()
    {
        for (int index = 0; index < planetViews.Count; index++)
            UnbindPlanetView(planetViews[index]);
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Gets or creates a reusable authored planet view.
    /// </summary>
    /// <param name="index">The requested planet index.</param>
    /// <returns>The reusable planet view.</returns>
    private PlanetSystemPlanetView GetPlanetView(int index)
    {
        while (planetViews.Count <= index)
        {
            PlanetSystemPlanetView view = Instantiate(planetPrefab, planetsRoot);
            view.name = $"Planet{planetViews.Count}";
            BindPlanetView(view);
            planetViews.Add(view);
        }
        return planetViews[index];
    }

    /// <summary>
    /// Gets an active rendered planet view by its presentation index.
    /// </summary>
    /// <param name="index">The requested presentation index.</param>
    /// <returns>The matching active planet view, or null.</returns>
    private PlanetSystemPlanetView GetActivePlanetView(int index)
    {
        return
            index >= 0
            && index < planetViews.Count
            && planetViews[index].gameObject.activeInHierarchy
            ? planetViews[index]
            : null;
    }

    /// <summary>
    /// Subscribes to one dynamic planet view.
    /// </summary>
    /// <param name="view">The planet view to bind.</param>
    private void BindPlanetView(PlanetSystemPlanetView view)
    {
        view.Clicked += HandlePlanetClicked;
        view.HoverCleared += HandlePlanetHoverCleared;
        view.Hovered += HandlePlanetHovered;
        view.Pressed += HandlePlanetPressed;
        view.Released += HandlePlanetReleased;
    }

    /// <summary>
    /// Releases subscriptions from one dynamic planet view.
    /// </summary>
    /// <param name="view">The planet view to unbind.</param>
    private void UnbindPlanetView(PlanetSystemPlanetView view)
    {
        if (view == null)
            return;

        view.Clicked -= HandlePlanetClicked;
        view.HoverCleared -= HandlePlanetHoverCleared;
        view.Hovered -= HandlePlanetHovered;
        view.Pressed -= HandlePlanetPressed;
        view.Released -= HandlePlanetReleased;
    }

    /// <summary>
    /// Forwards a semantic planet double click.
    /// </summary>
    /// <param name="view">The clicked planet view.</param>
    /// <param name="element">The semantic presentation element.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePlanetClicked(
        PlanetSystemPlanetView view,
        PlanetSystemWindowElement element,
        PointerEventData eventData
    )
    {
        Clicked?.Invoke(this, element, eventData);
    }

    /// <summary>
    /// Forwards a semantic planet hover clear.
    /// </summary>
    /// <param name="view">The planet view that lost hover.</param>
    private void HandlePlanetHoverCleared(PlanetSystemPlanetView view)
    {
        HoverCleared?.Invoke(this);
    }

    /// <summary>
    /// Forwards a semantic planet hover.
    /// </summary>
    /// <param name="view">The hovered planet view.</param>
    /// <param name="element">The semantic presentation element.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePlanetHovered(
        PlanetSystemPlanetView view,
        PlanetSystemWindowElement element,
        PointerEventData eventData
    )
    {
        Hovered?.Invoke(this, element, eventData);
    }

    /// <summary>
    /// Forwards a semantic planet press.
    /// </summary>
    /// <param name="view">The pressed planet view.</param>
    /// <param name="element">The semantic presentation element.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePlanetPressed(
        PlanetSystemPlanetView view,
        PlanetSystemWindowElement element,
        PointerEventData eventData
    )
    {
        Pressed?.Invoke(this, element, eventData);
    }

    /// <summary>
    /// Forwards a semantic planet release or drop.
    /// </summary>
    /// <param name="view">The released planet view.</param>
    /// <param name="element">The semantic presentation element.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandlePlanetReleased(
        PlanetSystemPlanetView view,
        PlanetSystemWindowElement element,
        PointerEventData eventData
    )
    {
        Released?.Invoke(this, element, eventData);
    }

    /// <summary>
    /// Gets the current pointer raycast target.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>The current or pressed raycast target.</returns>
    private static GameObject GetRaycastTarget(PointerEventData eventData)
    {
        return eventData?.pointerCurrentRaycast.gameObject
            ?? eventData?.pointerPressRaycast.gameObject;
    }

    /// <summary>
    /// Verifies all authored references and projection values.
    /// </summary>
    private void VerifyReferences()
    {
        if (systemNameTextField == null)
            throw new MissingReferenceException($"{name}/SystemNameTextField is missing.");
        if (planetsRoot == null)
            throw new MissingReferenceException($"{name}/Planets is missing.");
        if (planetPrefab == null)
            throw new MissingReferenceException($"{name}/PlanetSystemPlanet prefab is missing.");
        if (sectorCoordinateRange == 0f)
            throw new MissingReferenceException($"{name}/SectorCoordinateRange is invalid.");
    }
}
