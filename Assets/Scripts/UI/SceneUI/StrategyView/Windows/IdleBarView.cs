using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders the idle bar in the strategy desktop's upper-right corner.
/// </summary>
public sealed class IdleBarView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IScrollHandler
{
    private const int _columnGap = 1;
    private const int _horizontalPadding = 4;
    private const int _maximumRows = 2;
    private const int _outerPadding = 5;
    private const int _rowGap = 1;
    private const int _slotSize = 28;
    private const int _topPadding = 4;
    private const int _verticalPadding = 3;

    [SerializeField]
    private Image shelfHitArea;

    [SerializeField]
    private TextMeshProUGUI pageTextField;

    [SerializeField]
    private IdleBarSlotView slotTemplate;

    private readonly List<IdleBarSlotView> slots = new List<IdleBarSlotView>();

    private IdleBarRenderData currentData;
    private int columns;
    private int firstVisibleIndex;
    private bool hoverExitPending;
    private bool initialized;
    private bool pointerOverShelf;

    internal event Action<string> EntrySelected;

    internal event Action<string> EntryUntrackRequested;

    /// <summary>
    /// Applies current availability within a two-row shelf capped at half the desktop width.
    /// </summary>
    internal void Render(IdleBarRenderData data)
    {
        Initialize();
        currentData = data;
        if (data == null || data.Entries.Count == 0)
        {
            firstVisibleIndex = 0;
            HideShelf();
            return;
        }

        int maximumShelfWidth = Mathf.Max(
            _slotSize + 2 * _horizontalPadding,
            data.DesktopBounds.width / 2
        );
        columns = Mathf.Max(
            1,
            (maximumShelfWidth - 2 * _horizontalPadding + _columnGap) / (_slotSize + _columnGap)
        );
        int capacity = GetVisibleCapacity();
        firstVisibleIndex = Mathf.Clamp(
            firstVisibleIndex,
            0,
            Mathf.Max(0, data.Entries.Count - capacity)
        );

        RenderVisiblePage();
    }

    /// <summary>
    /// Reveals the unobtrusive page position while the shelf is being inspected.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetPointerOverShelf(true);
    }

    /// <summary>
    /// Conceals paging chrome when the pointer leaves the shelf.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        RequestPointerExit();
    }

    /// <summary>
    /// Pages one row at a time when the pointer wheel is used over the shelf.
    /// </summary>
    public void OnScroll(PointerEventData eventData)
    {
        if (currentData == null || columns <= 0 || Mathf.Approximately(eventData.scrollDelta.y, 0f))
            return;

        int capacity = GetVisibleCapacity();
        int maximumStart = Mathf.Max(0, currentData.Entries.Count - capacity);
        if (maximumStart == 0)
            return;

        eventData.Use();
        int direction = eventData.scrollDelta.y < 0f ? 1 : -1;
        int nextIndex = Mathf.Clamp(firstVisibleIndex + direction * columns, 0, maximumStart);
        if (nextIndex == firstVisibleIndex)
            return;

        firstVisibleIndex = nextIndex;
        RenderVisiblePage();
    }

    /// <summary>
    /// Validates and conceals the authored shelf elements.
    /// </summary>
    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// Validates authored references exactly once.
    /// </summary>
    private void Initialize()
    {
        if (initialized)
            return;

        if (shelfHitArea == null || pageTextField == null || slotTemplate == null)
            throw new MissingReferenceException("IdleBarView has incomplete authored references.");

        slotTemplate.gameObject.SetActive(false);
        HideShelf();
        initialized = true;
    }

    /// <summary>
    /// Releases selection subscriptions from instantiated slots.
    /// </summary>
    private void OnDestroy()
    {
        foreach (IdleBarSlotView slot in slots)
        {
            if (slot != null)
            {
                slot.Selected -= HandleSlotSelected;
                slot.UntrackRequested -= HandleSlotUntrackRequested;
            }
        }
    }

    /// <summary>
    /// Applies a deferred hover exit after child-to-child pointer transitions settle.
    /// </summary>
    private void LateUpdate()
    {
        if (!hoverExitPending)
            return;

        hoverExitPending = false;
        pointerOverShelf = false;
        if (currentData == null || currentData.Entries.Count == 0)
            return;
        RenderVisiblePage();
    }

    /// <summary>
    /// Renders the current page right-aligned with source ordering preserved across both rows.
    /// </summary>
    private void RenderVisiblePage()
    {
        int capacity = GetVisibleCapacity();
        firstVisibleIndex = Mathf.Clamp(
            firstVisibleIndex,
            0,
            Mathf.Max(0, currentData.Entries.Count - capacity)
        );
        int remaining = currentData.Entries.Count - firstVisibleIndex;
        int visibleCount = Mathf.Min(remaining, capacity);
        int rowCount = Mathf.CeilToInt((float)visibleCount / columns);
        int occupiedColumns = Mathf.Min(columns, visibleCount);
        int shelfWidth =
            occupiedColumns * _slotSize
            + Mathf.Max(0, occupiedColumns - 1) * _columnGap
            + 2 * _horizontalPadding;
        int shelfHeight =
            rowCount * _slotSize + Mathf.Max(0, rowCount - 1) * _rowGap + 2 * _verticalPadding;
        int shelfX = currentData.DesktopBounds.xMax - _outerPadding - shelfWidth;
        int shelfY = currentData.DesktopBounds.yMin + _topPadding;

        SetSourceRect(shelfHitArea.rectTransform, shelfX, shelfY, shelfWidth, shelfHeight);
        shelfHitArea.gameObject.SetActive(true);
        EnsureSlotCount(visibleCount);

        for (int index = 0; index < slots.Count; index++)
        {
            bool visible = index < visibleCount;
            slots[index].gameObject.SetActive(visible);
            if (!visible)
                continue;

            int row = index / columns;
            int column = index % columns;
            int entriesInRow = Mathf.Min(columns, visibleCount - row * columns);
            int rowWidth = entriesInRow * _slotSize + Mathf.Max(0, entriesInRow - 1) * _columnGap;
            int rowX =
                row == 0
                    ? currentData.DesktopBounds.xMax - _outerPadding - _horizontalPadding - rowWidth
                    : shelfX + _horizontalPadding;
            int visualColumn = column;
            slots[index]
                .Render(
                    currentData.Entries[firstVisibleIndex + index],
                    rowX + visualColumn * (_slotSize + _columnGap),
                    shelfY + _verticalPadding + row * (_slotSize + _rowGap),
                    _slotSize
                );
        }

        UpdateShelfPresentation();
    }

    /// <summary>
    /// Instantiates enough reusable slots for the visible page.
    /// </summary>
    private void EnsureSlotCount(int count)
    {
        while (slots.Count < count)
        {
            IdleBarSlotView slot = Instantiate(slotTemplate, transform);
            slot.gameObject.name = $"AvailabilitySlot{slots.Count + 1}";
            slot.Selected += HandleSlotSelected;
            slot.UntrackRequested += HandleSlotUntrackRequested;
            slots.Add(slot);
        }
    }

    /// <summary>
    /// Returns the current one- or two-row page capacity.
    /// </summary>
    private int GetVisibleCapacity()
    {
        return columns * (pointerOverShelf ? _maximumRows : 1);
    }

    /// <summary>
    /// Reveals the complete shelf while any of its interactive regions are hovered.
    /// </summary>
    private void SetPointerOverShelf(bool pointerOver)
    {
        bool hoverChanged = pointerOverShelf != pointerOver;
        hoverExitPending = false;
        pointerOverShelf = pointerOver;
        if (hoverChanged)
            RenderVisiblePage();
        else
            UpdateShelfPresentation();
    }

    /// <summary>
    /// Defers the second-row concealment so moving between portraits does not flicker.
    /// </summary>
    private void RequestPointerExit()
    {
        hoverExitPending = true;
    }

    /// <summary>
    /// Updates hover-only paging feedback.
    /// </summary>
    private void UpdateShelfPresentation()
    {
        if (currentData == null || currentData.Entries.Count == 0)
            return;

        int capacity = Mathf.Max(1, GetVisibleCapacity());
        bool paged = currentData.Entries.Count > capacity;
        pageTextField.gameObject.SetActive(pointerOverShelf && paged);
        if (!paged)
            return;

        pageTextField.transform.SetAsLastSibling();
        int maximumStart = currentData.Entries.Count - capacity;
        int pageCount = Mathf.CeilToInt((float)maximumStart / columns) + 1;
        int page = Mathf.CeilToInt((float)firstVisibleIndex / columns) + 1;
        pageTextField.text = $"{page}/{pageCount}";
        SetSourceRect(
            pageTextField.rectTransform,
            Mathf.RoundToInt(shelfHitArea.rectTransform.anchoredPosition.x) + 2,
            -Mathf.RoundToInt(shelfHitArea.rectTransform.anchoredPosition.y) + 1,
            20,
            8
        );
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
        if (pageTextField != null)
            pageTextField.gameObject.SetActive(false);
        foreach (IdleBarSlotView slot in slots)
            slot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Forwards one slot selection through the desktop view boundary.
    /// </summary>
    private void HandleSlotSelected(string instanceId)
    {
        EntrySelected?.Invoke(instanceId);
    }

    /// <summary>
    /// Forwards one slot's untracking request through the desktop view boundary.
    /// </summary>
    private void HandleSlotUntrackRequested(string instanceId)
    {
        EntryUntrackRequested?.Invoke(instanceId);
    }

    /// <summary>
    /// Applies a source-space rectangle using the strategy screen's top-left coordinates.
    /// </summary>
    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
