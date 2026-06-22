using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class SelectableListView<TRowView, TRowData>
    where TRowView : SelectableListRowView
{
    private readonly List<TRowView> rowViews = new List<TRowView>();
    private readonly ScrollAreaView scrollArea;
    private readonly TRowView rowTemplate;
    private readonly string rowNamePrefix;
    private readonly Action<TRowView, PointerEventData> rowSelected;
    private readonly Action<TRowView, PointerEventData> rowActivated;
    private readonly Action<TRowView, PointerEventData> rowContextRequested;
    private readonly Func<bool> canNavigate;
    private readonly Transform navigationScope;

    public SelectableListView(
        ScrollAreaView scrollArea,
        TRowView rowTemplate,
        string rowNamePrefix,
        Action<TRowView, PointerEventData> rowSelected,
        Action<TRowView, PointerEventData> rowActivated,
        Action<TRowView, PointerEventData> rowContextRequested = null,
        Func<bool> canNavigate = null,
        Transform navigationScope = null
    )
    {
        this.scrollArea = scrollArea;
        this.rowTemplate = rowTemplate;
        this.rowNamePrefix = rowNamePrefix;
        this.rowSelected = rowSelected;
        this.rowActivated = rowActivated;
        this.rowContextRequested = rowContextRequested;
        this.canNavigate = canNavigate;
        this.navigationScope = navigationScope;
    }

    public void Render(
        IReadOnlyList<TRowData> rows,
        int contentHeight,
        int scrollStep,
        bool resetScroll,
        int rowPitch,
        Action<TRowView, TRowData, int> renderRow,
        Func<TRowData, int, bool> isSelected = null
    )
    {
        IReadOnlyList<TRowData> safeRows = rows ?? Array.Empty<TRowData>();
        scrollArea.gameObject.SetActive(true);
        scrollArea.SetContentHeight(contentHeight, scrollStep, resetScroll);

        for (int i = 0; i < safeRows.Count; i++)
        {
            TRowView rowView = GetRowView(i);
            SetTopLeftRect(
                rowView.transform as RectTransform,
                0,
                i * rowPitch,
                Mathf.RoundToInt(scrollArea.ViewportWidth),
                rowPitch
            );
            renderRow(rowView, safeRows[i], i);
        }

        HideRowsFrom(safeRows.Count);
        RevealAndFocusSelectedRow(safeRows, rowPitch, isSelected);
    }

    public void Hide()
    {
        HideRowsFrom(0);
    }

    public void Clear()
    {
        for (int i = 0; i < rowViews.Count; i++)
        {
            TRowView row = rowViews[i];
            if (row == null)
                continue;

            row.Selected -= HandleRowSelected;
            row.Activated -= HandleRowActivated;
            row.ContextRequested -= HandleRowContextRequested;
            UnityEngine.Object.Destroy(row.gameObject);
        }

        rowViews.Clear();
    }

    private TRowView GetRowView(int index)
    {
        while (rowViews.Count <= index)
        {
            TRowView row = UnityEngine.Object.Instantiate(rowTemplate, scrollArea.ContentRoot);
            row.name = $"{rowNamePrefix}{rowViews.Count}";
            row.Selected += HandleRowSelected;
            row.Activated += HandleRowActivated;
            row.ContextRequested += HandleRowContextRequested;
            row.SetNavigationGate(CanNavigate);
            rowViews.Add(row);
        }

        return rowViews[index];
    }

    private void HideRowsFrom(int index)
    {
        for (int i = index; i < rowViews.Count; i++)
            rowViews[i].gameObject.SetActive(false);
    }

    private void HandleRowSelected(SelectableListRowView row, PointerEventData eventData)
    {
        if (row is TRowView typedRow)
            rowSelected?.Invoke(typedRow, eventData);
    }

    private void HandleRowActivated(SelectableListRowView row, PointerEventData eventData)
    {
        if (row is TRowView typedRow)
            rowActivated?.Invoke(typedRow, eventData);
    }

    private void HandleRowContextRequested(SelectableListRowView row, PointerEventData eventData)
    {
        if (row is TRowView typedRow)
            rowContextRequested?.Invoke(typedRow, eventData);
    }

    private void RevealAndFocusSelectedRow(
        IReadOnlyList<TRowData> rows,
        int rowPitch,
        Func<TRowData, int, bool> isSelected
    )
    {
        if (isSelected == null)
            return;

        if (!CanNavigate())
            return;

        int count = Math.Min(rows.Count, rowViews.Count);
        for (int i = 0; i < count; i++)
        {
            if (!isSelected(rows[i], i))
                continue;

            TRowView row = rowViews[i];
            if (row.gameObject.activeInHierarchy)
            {
                scrollArea.RevealContentRect(i * rowPitch, rowPitch);
                SelectableListRowView.FocusRowForNavigation(navigationScope, true, row);
            }
            return;
        }
    }

    private bool CanNavigate()
    {
        return canNavigate?.Invoke() != false;
    }

    private static void SetTopLeftRect(
        RectTransform rectTransform,
        int x,
        int y,
        int width,
        int height
    )
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = new Vector2(0f, 1f);
        rectTransform.anchorMax = new Vector2(0f, 1f);
        rectTransform.pivot = new Vector2(0f, 1f);
        rectTransform.anchoredPosition = new Vector2(x, -y);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}
