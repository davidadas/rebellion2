using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Selects and drags authored sectors while reusing gameplay star-cluster rendering.
/// </summary>
public sealed class ContentEditorMapMarker
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
{
    [SerializeField]
    private PlanetSectorClusterView cluster;
    public ContentPackEditorRecord Record { get; private set; }
    public event Action<ContentEditorMapMarker> Selected;
    public event Action<ContentEditorMapMarker> Opened;
    public event Action<ContentEditorMapMarker, Vector2> Moved;
    public event Action<ContentEditorMapMarker> DragStarted;
    public event Action DragEnded;

    private bool dragging;
    private Vector2 dragOffset;
    private Vector2 position;
    private GalaxyMapClusterRenderData presentation;

    /// <summary>
    /// Captures the grab offset and starts one reversible drag operation.
    /// </summary>
    /// <param name="eventData">The starting pointer event.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || Record == null)
            return;
        dragOffset = Vector2.zero;
        RectTransform parent = (RectTransform)transform.parent;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 point
            )
        )
            return;
        dragOffset =
            ((RectTransform)transform).anchorMin
            - new Vector2(
                (point.x - parent.rect.xMin) / parent.rect.width,
                (point.y - parent.rect.yMin) / parent.rect.height
            );
        dragging = true;
        DragStarted?.Invoke(this);
    }

    /// <summary>
    /// Displays one map record at its authored position.
    /// </summary>
    /// <param name="record">The represented item.</param>
    /// <param name="position">Its normalized map position.</param>
    /// <param name="data">The actual star artwork and offsets in the sector.</param>
    public void Render(
        ContentPackEditorRecord record,
        Vector2 position,
        GalaxyMapClusterRenderData data
    )
    {
        if (Record?.Element != record.Element)
            EndDrag();
        Record = record;
        presentation = data;
        cluster.Render(data);
        SetPosition(position);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Moves the cluster within its source map without changing the planet offsets.
    /// </summary>
    /// <param name="value">The normalized map position.</param>
    public void SetPosition(Vector2 value)
    {
        position = value;
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = position;
        rect.pivot = Vector2.up;
        rect.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// Captures the individual occupied stars while preserving this sector as a rigid group.
    /// </summary>
    /// <returns>The source-space star rectangles relative to the sector anchor.</returns>
    internal IReadOnlyList<Rect> GetLayoutFootprints()
    {
        return presentation.Stars.Count == 0
            ? new[] { new Rect(0, 0, 1, 1) }
            : Enumerable
                .Range(0, presentation.Stars.Count)
                .Select(index =>
                {
                    RectInt rect = cluster.GetRenderedStarSourceRect(index);
                    return new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2);
                })
                .ToArray();
    }

    /// <summary>
    /// Reveals the sector name with the gameplay label layout.
    /// </summary>
    /// <param name="eventData">The pointer entering the cluster.</param>
    public void OnPointerEnter(PointerEventData eventData) => ShowLabel(true);

    /// <summary>
    /// Hides the sector name without changing its stars or authored coordinates.
    /// </summary>
    /// <param name="eventData">The pointer leaving the cluster.</param>
    public void OnPointerExit(PointerEventData eventData) => ShowLabel(false);

    /// <summary>
    /// Changes only the gameplay sector label's visibility.
    /// </summary>
    /// <param name="visible">Whether the name is visible.</param>
    private void ShowLabel(bool visible)
    {
        if (presentation == null)
            return;
        Vector3 scale = transform.localScale;
        cluster.Render(
            new GalaxyMapClusterRenderData(
                presentation.SectorInstanceId,
                0,
                0,
                presentation.Label,
                visible,
                presentation.Stars
            )
        );
        SetPosition(position);
        transform.localScale = scale;
    }

    /// <summary>
    /// Selects on a single click and opens on a double click without interrupting a drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || eventData.dragging)
            return;
        if (eventData.clickCount >= 2)
            Opened?.Invoke(this);
        else
            Selected?.Invoke(this);
    }

    /// <summary>
    /// Moves the item while preserving its pointer-space position.
    /// </summary>
    /// <param name="eventData">The drag event.</param>
    public void OnDrag(PointerEventData eventData)
    {
        Move(eventData);
    }

    /// <summary>
    /// Completes the drag and refreshes the coordinate inspector.
    /// </summary>
    /// <param name="eventData">The drag event.</param>
    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        Move(eventData);
        EndDrag();
    }

    /// <summary>
    /// Completes an interrupted gesture when the marker is hidden.
    /// </summary>
    private void OnDisable() => EndDrag();

    /// <summary>
    /// Releases a pooled marker's project and presentation references.
    /// </summary>
    public void Clear()
    {
        EndDrag();
        Record = null;
        presentation = null;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Publishes exactly one completion for a started gesture.
    /// </summary>
    private void EndDrag()
    {
        if (!dragging)
            return;
        dragging = false;
        DragEnded?.Invoke();
    }

    /// <summary>
    /// Converts a pointer position into normalized coordinates within the map.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void Move(PointerEventData eventData)
    {
        if (!dragging || eventData.button != PointerEventData.InputButton.Left)
            return;
        RectTransform parent = (RectTransform)transform.parent;
        if (
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 point
            )
        )
            Moved?.Invoke(
                this,
                new Vector2(
                    Mathf.Clamp01((point.x - parent.rect.xMin) / parent.rect.width + dragOffset.x),
                    Mathf.Clamp01((point.y - parent.rect.yMin) / parent.rect.height + dragOffset.y)
                )
            );
    }
}
