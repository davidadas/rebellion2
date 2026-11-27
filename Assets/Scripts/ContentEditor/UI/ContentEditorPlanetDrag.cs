using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Edits a planet's coordinates by dragging its gameplay presentation in the sector preview.
/// </summary>
public sealed class ContentEditorPlanetDrag
    : MonoBehaviour,
        IPointerDownHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
{
    private ContentPackEditorRecord record;
    private ContentPackEditorRecord sector;
    private PlanetSectorWindowView window;
    private Action<ContentPackEditorRecord> selected;
    private Action<ContentPackEditorRecord> started;
    private Action<ContentPackEditorRecord, Vector2Int> moved;
    private Action ended;
    private ContentEditorArtworkEditTarget selectionTarget;
    private bool dragging;
    private Vector2 grabOffset;

    /// <summary>
    /// Binds a rendered planet to its current authored definition and edit callbacks.
    /// </summary>
    /// <param name="planet">The authored planet.</param>
    /// <param name="parent">The authored sector.</param>
    /// <param name="view">The gameplay projection geometry.</param>
    /// <param name="onSelected">The selection action.</param>
    /// <param name="onStarted">The grouped-edit action.</param>
    /// <param name="onMoved">The coordinate edit action.</param>
    /// <param name="onEnded">The gesture completion action.</param>
    public void Bind(
        ContentPackEditorRecord planet,
        ContentPackEditorRecord parent,
        PlanetSectorWindowView view,
        Action<ContentPackEditorRecord> onSelected,
        Action<ContentPackEditorRecord> onStarted,
        Action<ContentPackEditorRecord, Vector2Int> onMoved,
        Action onEnded
    )
    {
        if (record?.Element != planet.Element)
        {
            EndDrag();
            ClearFocus();
        }
        record = planet;
        sector = parent;
        window = view;
        selected = onSelected;
        started = onStarted;
        moved = onMoved;
        ended = onEnded;
    }

    /// <summary>
    /// Selects the planet without creating a simulated game or changing its data.
    /// </summary>
    /// <param name="eventData">The pointer press.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || record == null)
            return;
        selected?.Invoke(record);
        if (isActiveAndEnabled)
            SelectPlanetArtwork();
    }

    /// <summary>
    /// Focuses a non-interactive outline that follows the rendered planet artwork.
    /// </summary>
    private void SelectPlanetArtwork()
    {
        if (selectionTarget == null)
        {
            RectTransform outline = new GameObject(
                "PlanetSelection",
                typeof(RectTransform)
            ).GetComponent<RectTransform>();
            outline.SetParent(GetComponent<PlanetSectorPlanetView>().PlanetArtworkTransform, false);
            outline.anchorMin = Vector2.zero;
            outline.anchorMax = Vector2.one;
            outline.offsetMin = outline.offsetMax = Vector2.zero;
            selectionTarget = outline.gameObject.AddComponent<ContentEditorArtworkEditTarget>();
        }
        selectionTarget.BindSelection();
        ContentEditorArtworkEditTarget.Select(selectionTarget);
    }

    /// <summary>
    /// Nudges the focused planet through the same grouped edit as a pointer drag.
    /// </summary>
    /// <param name="planet">The currently selected inspector record.</param>
    /// <param name="delta">The authored map-coordinate displacement.</param>
    /// <returns>Whether the focused planet handled the movement.</returns>
    internal bool Nudge(ContentPackEditorRecord planet, Vector2Int delta)
    {
        if (dragging || record == null || record.Element != planet?.Element)
            return false;
        started?.Invoke(record);
        try
        {
            moved?.Invoke(record, new Vector2Int(record.X, record.Y) + delta);
        }
        finally
        {
            ended?.Invoke();
        }
        return true;
    }

    /// <summary>
    /// Keeps the point grabbed on the planet under the pointer throughout the drag.
    /// </summary>
    /// <param name="eventData">The initial drag position.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (
            eventData.button != PointerEventData.InputButton.Left
            || record == null
            || !TryPoint(eventData, out Vector2 point)
        )
            return;
        RectInt image = GetComponent<PlanetSectorPlanetView>().GetRenderedPlanetImageSourceRect();
        grabOffset = new Vector2(image.x, image.y) - point;
        dragging = true;
        ContentEditorArtworkEditTarget.SetPreviewDragging(this, true);
        started?.Invoke(record);
    }

    /// <summary>
    /// Updates the authored coordinates using the gameplay window's inverse projection.
    /// </summary>
    /// <param name="eventData">The current drag position.</param>
    public void OnDrag(PointerEventData eventData) => Move(eventData);

    /// <summary>
    /// Completes the grouped coordinate edit.
    /// </summary>
    /// <param name="eventData">The release position.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        Move(eventData);
        EndDrag();
    }

    /// <summary>
    /// Completes interrupted gestures before their preview disappears.
    /// </summary>
    private void OnDisable()
    {
        EndDrag();
        ClearFocus();
    }

    /// <summary>
    /// Releases a pooled planet's project and callback references.
    /// </summary>
    public void Clear()
    {
        EndDrag();
        ClearFocus();
        record = sector = null;
        window = null;
        selected = started = null;
        moved = null;
        ended = null;
    }

    /// <summary>
    /// Clears focus when the rendered slot stops representing the selected planet.
    /// </summary>
    private void ClearFocus()
    {
        if (
            selectionTarget != null
            && EventSystem.current?.currentSelectedGameObject == selectionTarget.gameObject
        )
            ContentEditorArtworkEditTarget.Select(null);
    }

    /// <summary>
    /// Completes each started gesture once, including cancelled drags.
    /// </summary>
    private void EndDrag()
    {
        if (!dragging)
            return;
        dragging = false;
        ContentEditorArtworkEditTarget.SetPreviewDragging(this, false);
        ended?.Invoke();
    }

    /// <summary>
    /// Resolves a pointer into source-space coordinates with a top-left origin.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <param name="position">Receives its source-space position.</param>
    /// <returns>Whether a source-space position was resolved.</returns>
    private bool TryPoint(PointerEventData eventData, out Vector2 position)
    {
        RectTransform rect = (RectTransform)window.transform;
        bool found = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 point
        );
        position = new Vector2(point.x - rect.rect.xMin, rect.rect.yMax - point.y);
        return found;
    }

    /// <summary>
    /// Publishes an absolute coordinate update without modifying the preview's layout rules.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void Move(PointerEventData eventData)
    {
        if (
            dragging
            && eventData.button == PointerEventData.InputButton.Left
            && TryPoint(eventData, out Vector2 point)
        )
        {
            Vector2Int offset = window.UnprojectPlanetPosition(point + grabOffset);
            moved?.Invoke(record, new Vector2Int(sector.X + offset.x, sector.Y + offset.y));
        }
    }
}
