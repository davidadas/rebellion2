using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders the idle bar in the strategy desktop's upper-right corner.
/// </summary>
public sealed class IdleBarView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private const int _collapsedEntryLimit = 5;
    private const int _collapsedColumnCount = 5;
    private const int _columnGap = 1;
    private const int _expandedColumnCount = 7;
    private const int _expandedRowLimit = 3;
    private const int _horizontalPadding = 4;
    private const int _outerPadding = 5;
    private const int _rowGap = 1;
    private const int _scrollbarGap = 1;
    private const int _scrollbarWidth = 13;
    private const int _slotSize = 28;
    private const int _topPadding = 4;
    private const int _verticalPadding = 3;

    [SerializeField]
    private Image shelfHitArea;

    [SerializeField]
    private ScrollAreaView entriesScrollArea;

    [SerializeField]
    private IdleBarSlotView slotTemplate;

    private readonly List<IdleBarSlotView> slots = new List<IdleBarSlotView>();

    private IdleBarRenderData currentData;
    private bool contextMenuOpen;
    private bool hoverExitPending;
    private bool initialized;
    private Func<bool> isContextMenuOpen;
    private IdleBarSlotView overflowSlot;
    private bool pointerOverShelf;

    /// <summary>Raised when the player selects an idle-bar entry.</summary>
    internal event Action<string> EntrySelected;

    /// <summary>Raised when the player requests an entry's normal context menu.</summary>
    internal event Action<string, PointerEventData> EntryContextRequested;

    /// <summary>Raised when an entity portrait begins receiving pointer hover.</summary>
    internal event Action<string> EntryHovered;

    /// <summary>Raised when an entity portrait stops receiving pointer hover.</summary>
    internal event Action<string> EntryHoverCleared;

    /// <summary>Raised when an entity portrait may begin a direct item drag.</summary>
    internal event Action<string, DragPreview, PointerEventData> EntryDragCandidateRequested;

    /// <summary>Raised while a direct item drag advances.</summary>
    internal event Action<PointerEventData> EntryDragMoved;

    /// <summary>Raised when a direct item drag or pending candidate ends.</summary>
    internal event Action<PointerEventData> EntryDragEnded;

    /// <summary>Raised when this authored view is destroyed.</summary>
    internal event Action<IdleBarView> Destroyed;

    /// <summary>
    /// Applies current availability within the compact idle-bar shelf.
    /// </summary>
    /// <param name="data">The complete idle-bar presentation.</param>
    internal void Render(IdleBarRenderData data)
    {
        Initialize();
        bool resetScroll = !HasSameEntries(currentData, data);
        currentData = data;
        gameObject.SetActive(data?.Visible == true);
        if (data?.Visible != true || data.Entries.Count == 0)
        {
            HideShelf();
            return;
        }

        RenderShelf(resetScroll);
    }

    /// <summary>
    /// Sets the provider used to keep the shelf expanded for its active context menu.
    /// </summary>
    /// <param name="provider">Reports whether this shelf's context menu is open.</param>
    internal void SetContextMenuOpenProvider(Func<bool> provider)
    {
        isContextMenuOpen = provider;
        RefreshContextMenuState();
    }

    /// <summary>Reports whether a screen-space point lies within the visible idle-bar shelf.</summary>
    /// <param name="screenPosition">The screen-space point to test.</param>
    /// <returns>True when the point lies within the active shelf.</returns>
    internal bool ContainsScreenPoint(Vector2 screenPosition)
    {
        if (shelfHitArea?.gameObject.activeInHierarchy != true)
            return false;

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera camera =
            canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        return RectTransformUtility.RectangleContainsScreenPoint(
            shelfHitArea.rectTransform,
            screenPosition,
            camera
        );
    }

    /// <summary>
    /// Reveals the scrollable rows while the shelf is being inspected.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetPointerOverShelf(true);
    }

    /// <summary>
    /// Returns the shelf to its compact summary when the pointer leaves.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        hoverExitPending = true;
    }

    /// <summary>
    /// Validates authored references exactly once.
    /// </summary>
    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// Applies a deferred hover exit after child-to-child pointer transitions settle.
    /// </summary>
    private void LateUpdate()
    {
        RefreshContextMenuState();
        if (!hoverExitPending)
            return;

        hoverExitPending = false;
        SetPointerOverShelf(false);
    }

    /// <summary>
    /// Releases selection subscriptions from instantiated slots.
    /// </summary>
    private void OnDestroy()
    {
        if (entriesScrollArea != null)
        {
            entriesScrollArea.Dragged -= HandleEntryDragMoved;
            entriesScrollArea.DragEnded -= HandleEntryDragEnded;
        }

        foreach (IdleBarSlotView slot in slots)
        {
            if (slot != null)
            {
                slot.Selected -= HandleSlotSelected;
                slot.ContextRequested -= HandleSlotContextRequested;
                slot.Hovered -= HandleSlotHovered;
                slot.HoverCleared -= HandleSlotHoverCleared;
                slot.DragCandidateRequested -= HandleSlotDragCandidateRequested;
                slot.DragCandidateReleased -= HandleEntryDragEnded;
            }
        }

        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Validates authored references and creates the non-entity overflow slot.
    /// </summary>
    private void Initialize()
    {
        if (initialized)
            return;

        if (shelfHitArea == null || entriesScrollArea == null || slotTemplate == null)
            throw new MissingReferenceException("IdleBarView has incomplete authored references.");

        slotTemplate.gameObject.SetActive(false);
        overflowSlot = Instantiate(slotTemplate, entriesScrollArea.ContentRoot);
        overflowSlot.gameObject.name = "OverflowSlot";
        overflowSlot.gameObject.SetActive(false);
        entriesScrollArea.Dragged += HandleEntryDragMoved;
        entriesScrollArea.DragEnded += HandleEntryDragEnded;
        HideShelf();
        initialized = true;
    }

    /// <summary>
    /// Renders either the compact summary or the expanded scrollable grid.
    /// </summary>
    /// <param name="resetScroll">Whether the scroll area should return to its first row.</param>
    private void RenderShelf(bool resetScroll)
    {
        bool expanded =
            (pointerOverShelf || contextMenuOpen)
            && currentData.Entries.Count > _collapsedEntryLimit;
        int visibleEntryCount = expanded
            ? currentData.Entries.Count
            : Math.Min(
                currentData.Entries.Count,
                currentData.Entries.Count > _collapsedEntryLimit
                    ? _collapsedEntryLimit - 1
                    : _collapsedEntryLimit
            );
        int rowCount = expanded
            ? Mathf.CeilToInt((float)currentData.Entries.Count / _expandedColumnCount)
            : 1;
        int viewportRowCount = expanded ? Math.Min(rowCount, _expandedRowLimit) : 1;
        bool scrollable = expanded && rowCount > _expandedRowLimit;
        int columnCount = expanded ? _expandedColumnCount : _collapsedColumnCount;
        int gridWidth = columnCount * _slotSize + (columnCount - 1) * _columnGap;
        int viewportHeight = viewportRowCount * _slotSize + (viewportRowCount - 1) * _rowGap;
        int shelfWidth =
            2 * _horizontalPadding + gridWidth + (scrollable ? _scrollbarWidth + _scrollbarGap : 0);
        int shelfHeight = viewportHeight + 2 * _verticalPadding;
        int shelfX = currentData.DesktopBounds.xMax - _outerPadding - shelfWidth;
        int shelfY = currentData.DesktopBounds.yMin + _topPadding;
        int gridX = _horizontalPadding;
        int scrollbarX = _horizontalPadding + gridWidth + _scrollbarGap;

        SetSourceRect(shelfHitArea.rectTransform, shelfX, shelfY, shelfWidth, shelfHeight);
        shelfHitArea.gameObject.SetActive(true);
        SetSourceRect(
            entriesScrollArea.transform as RectTransform,
            shelfX,
            shelfY,
            shelfWidth,
            shelfHeight
        );
        entriesScrollArea.gameObject.SetActive(true);
        entriesScrollArea.SetLayout(
            new Vector2(gridX, _verticalPadding),
            new Vector2(gridWidth, viewportHeight),
            new Vector2(scrollbarX, _verticalPadding),
            new Vector2(_scrollbarWidth, viewportHeight)
        );

        EnsureSlotCount(visibleEntryCount);
        for (int index = 0; index < slots.Count; index++)
        {
            bool visible = index < visibleEntryCount;
            slots[index].gameObject.SetActive(visible);
            if (!visible)
                continue;

            int row = expanded ? index / _expandedColumnCount : 0;
            int entriesInRow = expanded
                ? Math.Min(
                    _expandedColumnCount,
                    currentData.Entries.Count - row * _expandedColumnCount
                )
                : visibleEntryCount;
            int firstColumn = expanded && row > 0 ? 0 : columnCount - entriesInRow;
            int column = expanded ? index % _expandedColumnCount : index;
            slots[index]
                .Render(
                    currentData.Entries[index],
                    (firstColumn + column) * (_slotSize + _columnGap),
                    row * (_slotSize + _rowGap),
                    _slotSize
                );
        }

        bool showOverflow = !expanded && currentData.Entries.Count > _collapsedEntryLimit;
        overflowSlot.gameObject.SetActive(showOverflow);
        if (showOverflow)
        {
            overflowSlot.RenderOverflow(
                currentData.Entries.Count - (_collapsedEntryLimit - 1),
                (_collapsedEntryLimit - 1) * (_slotSize + _columnGap),
                0,
                _slotSize
            );
            overflowSlot.transform.SetAsLastSibling();
        }

        int contentHeight = rowCount * _slotSize + Math.Max(0, rowCount - 1) * _rowGap;
        entriesScrollArea.SetContentHeight(contentHeight, _slotSize + _rowGap, resetScroll);
    }

    /// <summary>
    /// Instantiates enough reusable slots for the current presentation.
    /// </summary>
    /// <param name="count">The required number of entity slots.</param>
    private void EnsureSlotCount(int count)
    {
        while (slots.Count < count)
        {
            IdleBarSlotView slot = Instantiate(slotTemplate, entriesScrollArea.ContentRoot);
            slot.gameObject.name = $"AvailabilitySlot{slots.Count + 1}";
            slot.Selected += HandleSlotSelected;
            slot.ContextRequested += HandleSlotContextRequested;
            slot.Hovered += HandleSlotHovered;
            slot.HoverCleared += HandleSlotHoverCleared;
            slot.DragCandidateRequested += HandleSlotDragCandidateRequested;
            slot.DragCandidateReleased += HandleEntryDragEnded;
            slots.Add(slot);
        }
    }

    /// <summary>
    /// Applies the expanded state while preserving child-to-child pointer transitions.
    /// </summary>
    /// <param name="pointerOver">Whether the pointer is over the shelf.</param>
    private void SetPointerOverShelf(bool pointerOver)
    {
        hoverExitPending = false;
        if (pointerOverShelf == pointerOver)
            return;

        pointerOverShelf = pointerOver;
        if (currentData?.Entries.Count > 0)
            RenderShelf(resetScroll: pointerOver);
    }

    /// <summary>
    /// Refreshes the expansion pin from the active context-menu request.
    /// </summary>
    private void RefreshContextMenuState()
    {
        bool open = isContextMenuOpen?.Invoke() == true;
        if (contextMenuOpen == open)
            return;

        contextMenuOpen = open;
        if (currentData?.Entries.Count > 0)
            RenderShelf(resetScroll: false);
    }

    /// <summary>
    /// Conceals the shelf when nothing is available.
    /// </summary>
    private void HideShelf()
    {
        hoverExitPending = false;
        pointerOverShelf = false;
        if (shelfHitArea != null)
            shelfHitArea.gameObject.SetActive(false);
        if (entriesScrollArea != null)
            entriesScrollArea.gameObject.SetActive(false);
        if (overflowSlot != null)
            overflowSlot.gameObject.SetActive(false);
        foreach (IdleBarSlotView slot in slots)
            slot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns whether two presentations contain the same ordered entities.
    /// </summary>
    /// <param name="previous">The previous presentation.</param>
    /// <param name="next">The next presentation.</param>
    /// <returns>True when the ordered entity identities match.</returns>
    private static bool HasSameEntries(IdleBarRenderData previous, IdleBarRenderData next)
    {
        if (previous == null || next == null || previous.Entries.Count != next.Entries.Count)
            return false;

        for (int index = 0; index < previous.Entries.Count; index++)
        {
            if (
                previous.Entries[index].Entity?.InstanceID != next.Entries[index].Entity?.InstanceID
            )
                return false;
        }

        return true;
    }

    /// <summary>
    /// Forwards one slot selection through the desktop view boundary.
    /// </summary>
    /// <param name="instanceId">The selected entity identifier.</param>
    private void HandleSlotSelected(string instanceId)
    {
        EntrySelected?.Invoke(instanceId);
    }

    /// <summary>
    /// Forwards one slot's context-menu request through the desktop view boundary.
    /// </summary>
    /// <param name="instanceId">The context-clicked entity identifier.</param>
    /// <param name="eventData">The source pointer event.</param>
    private void HandleSlotContextRequested(string instanceId, PointerEventData eventData)
    {
        EntryContextRequested?.Invoke(instanceId, eventData);
    }

    /// <summary>
    /// Forwards one slot's hover through the desktop view boundary.
    /// </summary>
    /// <param name="instanceId">The hovered entity identifier.</param>
    private void HandleSlotHovered(string instanceId)
    {
        EntryHovered?.Invoke(instanceId);
    }

    /// <summary>
    /// Forwards one slot's hover exit through the desktop view boundary.
    /// </summary>
    /// <param name="instanceId">The entity identifier whose hover ended.</param>
    private void HandleSlotHoverCleared(string instanceId)
    {
        EntryHoverCleared?.Invoke(instanceId);
    }

    /// <summary>
    /// Forwards one slot's direct drag candidate through the desktop view boundary.
    /// </summary>
    /// <param name="instanceId">The pressed entity identifier.</param>
    /// <param name="preview">The compact entity drag preview.</param>
    /// <param name="eventData">The source pointer event.</param>
    private void HandleSlotDragCandidateRequested(
        string instanceId,
        DragPreview preview,
        PointerEventData eventData
    )
    {
        EntryDragCandidateRequested?.Invoke(instanceId, preview, eventData);
    }

    /// <summary>
    /// Forwards direct item-drag movement through the desktop view boundary.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    private void HandleEntryDragMoved(PointerEventData eventData)
    {
        EntryDragMoved?.Invoke(eventData);
    }

    /// <summary>
    /// Forwards a completed direct item drag or released candidate.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    private void HandleEntryDragEnded(PointerEventData eventData)
    {
        EntryDragEnded?.Invoke(eventData);
    }

    /// <summary>
    /// Applies a source-space rectangle using the strategy screen's top-left coordinates.
    /// </summary>
    /// <param name="rect">The rectangle to position.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
