using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders an authored fleet window and emits semantic fleet-window gestures.
/// </summary>
public sealed class FleetWindowView : MonoBehaviour, IPointerClickHandler
{
    private readonly List<StrategyUnitCardView> detailItemViews = new List<StrategyUnitCardView>();
    private readonly List<FleetListRowView> fleetRowViews = new List<FleetListRowView>();
    private readonly List<string> renderedDetailItemNames = new List<string>();
    private readonly List<string> renderedFleetRowNames = new List<string>();
    private readonly List<UnityAction> tabListeners = new List<UnityAction>();

    [Header("Header")]
    [SerializeField]
    private RawImage titleImage;

    [SerializeField]
    private TextMeshProUGUI captionTextField;

    [Header("Fleet List")]
    [SerializeField]
    private ScrollAreaView fleetListScrollArea;

    [SerializeField]
    private FleetListRowView fleetListRowTemplate;

    [Header("Fleet Detail")]
    [SerializeField]
    private RawImage detailBackgroundImage;

    [SerializeField]
    private RawImage bannerImage;

    [SerializeField]
    private RawImage bannerEnrouteOverlayImage;

    [SerializeField]
    private RawImage bannerDamagedOverlayImage;

    [SerializeField]
    private TextMeshProUGUI fleetNameTextField;

    [SerializeField]
    private TextMeshProUGUI capacityLeftTextField;

    [SerializeField]
    private TextMeshProUGUI capacityRightTextField;

    [Header("Tabs")]
    [SerializeField]
    private RectTransform tabsRoot;

    [SerializeField]
    private RawImage[] tabImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] tabPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] tabButtons = Array.Empty<Button>();

    [Header("Detail Items")]
    [SerializeField]
    private ScrollAreaView detailItemsScrollArea;

    [SerializeField]
    private RectTransform detailItemsScrollPaddingTemplate;

    [SerializeField]
    private StrategyUnitCardView detailItemTemplate;

    [Header("Rename")]
    [SerializeField]
    private TMP_InputField renameInputField;

    private FleetWindowTab currentActiveTab = FleetWindowTab.CapitalShips;
    private int currentSelectedFleetIndex = -1;
    private int currentWindowX;
    private int currentWindowY;
    private int dragDetailItemIndex = -1;
    private int dragFleetIndex = -1;
    private bool renderedAnyDetailItems;
    private bool renderedAnyFleetRows;
    private FleetWindowTab renderedDetailActiveTab = FleetWindowTab.CapitalShips;
    private int renderedDetailSelectedFleetIndex = -1;
    private bool renameCommitInProgress;
    private bool renameEnding;
    private int renameDetailItemIndex = -1;
    private int renameFleetRowIndex = -1;
    private Transform renameInputHomeParent;
    private Vector2 renameInputHomeAnchorMax;
    private Vector2 renameInputHomeAnchorMin;
    private Vector2 renameInputHomePivot;
    private Vector2 renameInputHomePosition;
    private Vector2 renameInputHomeSize;
    private TextMeshProUGUI renameSourceTextField;
    private bool scrollEventsBound;

    /// <summary>
    /// Occurs when the view is destroyed.
    /// </summary>
    internal event Action<FleetWindowView> Destroyed;

    /// <summary>
    /// Occurs when the detail item is double-clicked.
    /// </summary>
    internal event Action<FleetWindowView, int, PointerEventData> DetailItemDoubleClicked;

    /// <summary>
    /// Occurs when a pointer drop is received by the detail item.
    /// </summary>
    internal event Action<FleetWindowView, int, PointerEventData> DetailItemDropped;

    /// <summary>
    /// Occurs when the detail item is pressed.
    /// </summary>
    internal event Action<FleetWindowView, int, PointerEventData> DetailItemPressed;

    /// <summary>
    /// Occurs when the detail item is released.
    /// </summary>
    internal event Action<FleetWindowView, int, PointerEventData> DetailItemReleased;

    internal event Action<FleetWindowView, PointerEventData> DetailItemsDropped;

    /// <summary>
    /// Occurs when a pointer drop is received by the fleet list.
    /// </summary>
    internal event Action<FleetWindowView, PointerEventData> FleetListDropped;

    /// <summary>
    /// Occurs when the fleet row is double-clicked.
    /// </summary>
    internal event Action<FleetWindowView, int, PointerEventData> FleetRowDoubleClicked;

    /// <summary>
    /// Occurs when a pointer drop is received by the fleet row.
    /// </summary>
    internal event Action<FleetWindowView, int, PointerEventData> FleetRowDropped;

    /// <summary>
    /// Occurs when the fleet row is pressed.
    /// </summary>
    internal event Action<FleetWindowView, int, PointerEventData> FleetRowPressed;

    /// <summary>
    /// Occurs when the fleet row is released.
    /// </summary>
    internal event Action<FleetWindowView, int, PointerEventData> FleetRowReleased;

    /// <summary>
    /// Occurs when rename is cancelled.
    /// </summary>
    internal event Action<FleetWindowView> RenameCancelled;

    /// <summary>
    /// Occurs when rename is submitted.
    /// </summary>
    internal event Action<FleetWindowView, string> RenameSubmitted;

    /// <summary>
    /// Occurs while the scroll area is dragged.
    /// </summary>
    internal event Action<FleetWindowView, PointerEventData> ScrollDragged;

    /// <summary>
    /// Occurs when scrolling ends.
    /// </summary>
    internal event Action<FleetWindowView, PointerEventData> ScrollDragEnded;

    /// <summary>
    /// Occurs when the surface is clicked.
    /// </summary>
    internal event Action<FleetWindowView, PointerEventData> SurfaceClicked;

    /// <summary>
    /// Occurs when a tab request is raised.
    /// </summary>
    internal event Action<FleetWindowView, FleetWindowTab> TabRequested;

    /// <summary>
    /// Verifies the authored hierarchy and binds local pointer controls.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        BindControls();
        BindScrollEvents();
    }

    /// <summary>
    /// Handles keyboard completion or cancellation while the authored rename field is active.
    /// </summary>
    private void Update()
    {
        if (!IsRenameActive())
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            CancelRename();
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            SubmitRename();
    }

    /// <summary>
    /// Releases local subscriptions and notifies the feature controller.
    /// </summary>
    private void OnDestroy()
    {
        EndRenamePresentation();
        UnbindControls();
        UnbindScrollEvents();
        UnbindItemViews();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies one complete fleet-window presentation snapshot.
    /// </summary>
    /// <param name="data">The immutable fleet-window snapshot.</param>
    public void Render(FleetWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        currentWindowX = data.X;
        currentWindowY = data.Y;
        currentActiveTab = data.ActiveTab;
        currentSelectedFleetIndex = data.SelectedFleetIndex;

        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetInteractiveImageTexture(titleImage, data.TitleTexture);
        UILayout.SetTextContent(captionTextField, data.Caption);
        RenderFleetRows(data.FleetRows);
        UILayout.SetImageTexture(detailBackgroundImage, data.DetailBackgroundTexture);
        RenderSelectedFleet(data);
        RenderRename(data);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Emits a semantic surface click for selection or targeting resolution.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData?.button == PointerEventData.InputButton.Left)
            SurfaceClicked?.Invoke(this, eventData);
    }

    /// <summary>
    /// Creates the current drag preview from the controller-selected visual source.
    /// </summary>
    /// <param name="sourceX">The pointer's source-space horizontal coordinate.</param>
    /// <param name="sourceY">The pointer's source-space vertical coordinate.</param>
    /// <param name="preview">Receives the drag preview.</param>
    /// <returns>True when a drawable preview source is available.</returns>
    public bool TryGetDragPreview(int sourceX, int sourceY, out DragPreview preview)
    {
        preview = null;
        if (
            dragFleetIndex >= 0
            && dragFleetIndex < fleetRowViews.Count
            && fleetRowViews[dragFleetIndex]
                .TryGetDragImage(out Texture fleetTexture, out RectTransform fleetRect)
        )
        {
            RectInt rowRect = UILayout.GetSourceRect(
                fleetRowViews[dragFleetIndex].transform as RectTransform
            );
            RectInt sourceRect = GetScrolledContentSourceRect(
                fleetListScrollArea,
                rowRect,
                UILayout.GetSourceRect(fleetRect)
            );
            preview = UILayout.CreateDragPreview(fleetTexture, sourceRect, sourceX, sourceY);
            return true;
        }

        if (
            dragDetailItemIndex >= 0
            && dragDetailItemIndex < detailItemViews.Count
            && detailItemViews[dragDetailItemIndex]
                .TryGetDragImage(out Texture detailTexture, out RectTransform detailRect)
        )
        {
            RectInt itemRect = UILayout.GetSourceRect(
                detailItemViews[dragDetailItemIndex].transform as RectTransform
            );
            RectInt sourceRect = GetScrolledContentSourceRect(
                detailItemsScrollArea,
                itemRect,
                UILayout.GetSourceRect(detailRect)
            );
            preview = UILayout.CreateDragPreview(detailTexture, sourceRect, sourceX, sourceY);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Selects a fleet row as the current drag-preview source.
    /// </summary>
    /// <param name="index">The fleet-row index.</param>
    internal void SetFleetRowDragSource(int index)
    {
        dragFleetIndex = index;
        dragDetailItemIndex = -1;
    }

    /// <summary>
    /// Selects a detail card as the current drag-preview source.
    /// </summary>
    /// <param name="index">The detail-card index.</param>
    internal void SetDetailItemDragSource(int index)
    {
        dragFleetIndex = -1;
        dragDetailItemIndex = index;
    }

    /// <summary>
    /// Clears the current drag-preview source.
    /// </summary>
    internal void ClearDragSource()
    {
        dragFleetIndex = -1;
        dragDetailItemIndex = -1;
    }

    /// <summary>
    /// Reports whether a pointer event originated from a fleet-list row.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <param name="index">Receives the fleet-row index.</param>
    /// <returns>True when the event target belongs to a visible fleet row.</returns>
    internal bool TryGetFleetRowIndex(PointerEventData eventData, out int index)
    {
        GameObject target = GetRaycastTarget(eventData);
        for (int i = 0; i < fleetRowViews.Count; i++)
        {
            FleetListRowView row = fleetRowViews[i];
            if (row.gameObject.activeInHierarchy && row.ContainsRaycastTarget(target))
            {
                index = row.Index;
                return true;
            }
        }

        index = -1;
        return false;
    }

    /// <summary>
    /// Reports whether a pointer event originated from a detail card.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <param name="index">Receives the detail-card index.</param>
    /// <returns>True when the event target belongs to a visible detail card.</returns>
    internal bool TryGetDetailItemIndex(PointerEventData eventData, out int index)
    {
        GameObject target = GetRaycastTarget(eventData);
        for (int i = 0; i < detailItemViews.Count; i++)
        {
            StrategyUnitCardView item = detailItemViews[i];
            if (
                item.gameObject.activeInHierarchy
                && target
                && target.transform.IsChildOf(item.transform)
            )
            {
                index = item.Index;
                return true;
            }
        }

        index = -1;
        return false;
    }

    /// <summary>
    /// Reports whether a pointer event belongs to either selectable visual collection.
    /// </summary>
    /// <param name="eventData">The pointer event to inspect.</param>
    /// <returns>True when a fleet row or detail card owns the event target.</returns>
    internal bool IsSelectionItemClick(PointerEventData eventData)
    {
        return TryGetFleetRowIndex(eventData, out _) || TryGetDetailItemIndex(eventData, out _);
    }

    /// <summary>
    /// Reports whether a row press began on its fleet drag image.
    /// </summary>
    /// <param name="index">The fleet-row index.</param>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>True when the event began on the fleet icon.</returns>
    internal bool FleetRowContainsDragSource(int index, PointerEventData eventData)
    {
        return index >= 0
            && index < fleetRowViews.Count
            && fleetRowViews[index].ContainsDragSource(eventData);
    }

    /// <summary>
    /// Reports whether a detail press began on its entity drag image.
    /// </summary>
    /// <param name="index">The detail-card index.</param>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>True when the event began on the entity image.</returns>
    internal bool DetailItemContainsDragSource(int index, PointerEventData eventData)
    {
        return index >= 0
            && index < detailItemViews.Count
            && detailItemViews[index].ContainsDragSource(eventData);
    }

    /// <summary>
    /// Applies selected-fleet visibility and detail presentation.
    /// </summary>
    /// <param name="data">The fleet-window snapshot.</param>
    private void RenderSelectedFleet(FleetWindowRenderData data)
    {
        SetActive(bannerImage, data.HasSelectedFleet);
        SetActive(bannerEnrouteOverlayImage, data.HasSelectedFleet);
        SetActive(bannerDamagedOverlayImage, data.HasSelectedFleet);
        fleetNameTextField.gameObject.SetActive(data.HasSelectedFleet);
        capacityLeftTextField.gameObject.SetActive(data.HasSelectedFleet && data.ShowCapacity);
        capacityRightTextField.gameObject.SetActive(data.HasSelectedFleet && data.ShowCapacity);
        tabsRoot.gameObject.SetActive(data.HasSelectedFleet);
        detailItemsScrollArea.gameObject.SetActive(data.HasSelectedFleet);

        if (!data.HasSelectedFleet)
        {
            HideTabs();
            HideDetailItems();
            return;
        }

        UILayout.SetImageTexture(bannerImage, data.BannerTexture);
        UILayout.SetImageTexture(bannerEnrouteOverlayImage, data.BannerEnrouteOverlayTexture);
        UILayout.SetImageTexture(bannerDamagedOverlayImage, data.BannerDamagedOverlayTexture);
        UILayout.SetTextContent(fleetNameTextField, data.FleetName, data.FleetNameColor);
        if (data.ShowCapacity)
        {
            UILayout.SetTextContent(capacityLeftTextField, data.CapacityLeft);
            UILayout.SetTextContent(capacityRightTextField, data.CapacityRight);
        }

        RenderTabs(data.Tabs);
        RenderDetailItems(data.DetailItems);
    }

    /// <summary>
    /// Applies and reconciles the fleet-list row collection.
    /// </summary>
    /// <param name="rows">The ordered fleet rows.</param>
    private void RenderFleetRows(IReadOnlyList<FleetListRowRenderData> rows)
    {
        IReadOnlyList<FleetListRowRenderData> safeRows =
            rows ?? Array.Empty<FleetListRowRenderData>();
        int rowHeight = GetFleetListRowHeight();
        fleetListScrollArea.SetContentHeight(
            safeRows.Count * rowHeight,
            rowHeight,
            FleetRowsChanged(safeRows)
        );
        for (int i = 0; i < safeRows.Count; i++)
        {
            FleetListRowView row = GetFleetRowView(i);
            row.SetIndex(i);
            row.Render(safeRows[i]);
        }

        for (int i = safeRows.Count; i < fleetRowViews.Count; i++)
            fleetRowViews[i].gameObject.SetActive(false);

        renderedAnyFleetRows = true;
        renderedFleetRowNames.Clear();
        for (int i = 0; i < safeRows.Count; i++)
            renderedFleetRowNames.Add(safeRows[i].Name);
    }

    /// <summary>
    /// Applies the ordered fleet-detail tabs.
    /// </summary>
    /// <param name="tabs">The ordered tab presentations.</param>
    private void RenderTabs(IReadOnlyList<FleetWindowTabRenderData> tabs)
    {
        if (
            tabs == null
            || tabs.Count != FleetWindowRenderData.TabCount
            || tabImages.Length != FleetWindowRenderData.TabCount
        )
            throw new ArgumentException("Fleet tab presentation count does not match the prefab.");

        for (int i = 0; i < tabImages.Length; i++)
        {
            FleetWindowTabRenderData tab = tabs[i];
            if (tab.Tab != FleetWindowRenderData.OrderedTabs[i])
                throw new ArgumentException(
                    "Fleet tab presentation order does not match the prefab."
                );

            Texture upTexture = tab.Texture;
            UILayout.SetInteractiveImageTexture(tabImages[i], upTexture);
            tabPressVisuals[i].SetTextures(upTexture, tab.PressedTexture);
        }
    }

    /// <summary>
    /// Hides every authored tab image.
    /// </summary>
    private void HideTabs()
    {
        for (int i = 0; i < tabImages.Length; i++)
            tabImages[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Applies and reconciles the selected fleet's detail cards.
    /// </summary>
    /// <param name="items">The ordered detail-card snapshots.</param>
    private void RenderDetailItems(IReadOnlyList<StrategyUnitCardRenderData> items)
    {
        IReadOnlyList<StrategyUnitCardRenderData> safeItems =
            items ?? Array.Empty<StrategyUnitCardRenderData>();
        int itemHeight = GetDetailItemHeight();
        int paddingHeight = UILayout.GetSourceRect(detailItemsScrollPaddingTemplate).height;
        detailItemsScrollArea.SetContentHeight(
            paddingHeight + safeItems.Count * itemHeight,
            itemHeight,
            DetailItemsChanged(safeItems)
        );
        for (int i = 0; i < safeItems.Count; i++)
        {
            StrategyUnitCardView item = GetDetailItemView(i);
            item.SetIndex(i);
            item.Render(safeItems[i]);
        }

        for (int i = safeItems.Count; i < detailItemViews.Count; i++)
            detailItemViews[i].gameObject.SetActive(false);

        renderedAnyDetailItems = true;
        renderedDetailActiveTab = currentActiveTab;
        renderedDetailSelectedFleetIndex = currentSelectedFleetIndex;
        renderedDetailItemNames.Clear();
        for (int i = 0; i < safeItems.Count; i++)
            renderedDetailItemNames.Add(safeItems[i].Name ?? string.Empty);
    }

    /// <summary>
    /// Hides every cached detail card.
    /// </summary>
    private void HideDetailItems()
    {
        for (int i = 0; i < detailItemViews.Count; i++)
            detailItemViews[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Gets or creates one reusable fleet-list row instance.
    /// </summary>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable fleet-list row.</returns>
    private FleetListRowView GetFleetRowView(int index)
    {
        while (fleetRowViews.Count <= index)
        {
            FleetListRowView row = Instantiate(
                fleetListRowTemplate,
                fleetListScrollArea.ContentRoot
            );
            row.name = $"FleetListRow{fleetRowViews.Count}";
            row.DoubleClicked += HandleFleetRowDoubleClicked;
            row.Dropped += HandleFleetRowDropped;
            row.Pressed += HandleFleetRowPressed;
            row.Released += HandleFleetRowReleased;
            fleetRowViews.Add(row);
        }

        return fleetRowViews[index];
    }

    /// <summary>
    /// Gets or creates one reusable fleet-detail card instance.
    /// </summary>
    /// <param name="index">The required detail-card index.</param>
    /// <returns>The reusable detail card.</returns>
    private StrategyUnitCardView GetDetailItemView(int index)
    {
        while (detailItemViews.Count <= index)
        {
            StrategyUnitCardView item = Instantiate(
                detailItemTemplate,
                detailItemsScrollArea.ContentRoot
            );
            item.name = $"FleetDetailItem{detailItemViews.Count}";
            item.gameObject.SetActive(false);
            item.DoubleClicked += HandleDetailItemDoubleClicked;
            item.Dropped += HandleDetailItemDropped;
            item.Pressed += HandleDetailItemPressed;
            item.Released += HandleDetailItemReleased;
            detailItemViews.Add(item);
        }

        return detailItemViews[index];
    }

    /// <summary>
    /// Applies the current controller-owned rename target to the authored input field.
    /// </summary>
    /// <param name="data">The fleet-window snapshot.</param>
    private void RenderRename(FleetWindowRenderData data)
    {
        TextMeshProUGUI source = GetRenameTextField(
            data.RenameFleetRowIndex,
            data.RenameDetailItemIndex
        );
        if (!source || !source.gameObject.activeInHierarchy)
        {
            if (IsRenameActive())
                EndRenamePresentation();
            return;
        }

        bool targetChanged =
            renameFleetRowIndex != data.RenameFleetRowIndex
            || renameDetailItemIndex != data.RenameDetailItemIndex;
        if (targetChanged)
        {
            EndRenamePresentation();
            renameFleetRowIndex = data.RenameFleetRowIndex;
            renameDetailItemIndex = data.RenameDetailItemIndex;
            renameSourceTextField = source;
            PlaceRenameInput(source, data.RenameText, true);
            return;
        }

        renameSourceTextField = source;
        PlaceRenameInput(source, data.RenameText, false);
    }

    /// <summary>
    /// Resolves the visual label represented by one rename snapshot.
    /// </summary>
    /// <param name="fleetRowIndex">The fleet-row index, or -1.</param>
    /// <param name="detailItemIndex">The detail-card index, or -1.</param>
    /// <returns>The matching authored text field, or null.</returns>
    private TextMeshProUGUI GetRenameTextField(int fleetRowIndex, int detailItemIndex)
    {
        if (fleetRowIndex >= 0 && fleetRowIndex < fleetRowViews.Count)
            return fleetRowViews[fleetRowIndex].NameTextField;
        if (detailItemIndex >= 0 && detailItemIndex < detailItemViews.Count)
            return detailItemViews[detailItemIndex].NameTextField;
        return null;
    }

    /// <summary>
    /// Places and optionally activates the authored rename field over one visual label.
    /// </summary>
    /// <param name="source">The label being edited.</param>
    /// <param name="value">The current rename value.</param>
    /// <param name="activate">Whether to activate and select the input.</param>
    private void PlaceRenameInput(TextMeshProUGUI source, string value, bool activate)
    {
        CaptureRenameInputHome();
        RectTransform sourceRect = source.rectTransform;
        RectTransform inputRect = renameInputField.transform as RectTransform;
        renameInputField.transform.SetParent(source.transform.parent, false);
        inputRect.anchorMin = sourceRect.anchorMin;
        inputRect.anchorMax = sourceRect.anchorMax;
        inputRect.pivot = sourceRect.pivot;
        inputRect.anchoredPosition = sourceRect.anchoredPosition;
        inputRect.sizeDelta = sourceRect.sizeDelta;
        ApplyRenameTextStyle(source);
        if (activate)
            renameInputField.SetTextWithoutNotify(value ?? string.Empty);

        renameInputField.gameObject.SetActive(true);
        source.enabled = false;
        if (!activate)
            return;

        renameInputField.Select();
        renameInputField.ActivateInputField();
    }

    /// <summary>
    /// Copies the edited label's authored typography to the shared rename field.
    /// </summary>
    /// <param name="source">The edited label.</param>
    private void ApplyRenameTextStyle(TextMeshProUGUI source)
    {
        TMP_Text text = renameInputField.textComponent;
        text.font = source.font;
        text.fontSize = source.fontSize;
        text.color = source.color;
        text.alignment = source.alignment;
        text.textWrappingMode = source.textWrappingMode;
        text.overflowMode = source.overflowMode;
        if (renameInputField.placeholder is TextMeshProUGUI placeholder)
            placeholder.text = string.Empty;
    }

    /// <summary>
    /// Captures the rename field's authored home layout once.
    /// </summary>
    private void CaptureRenameInputHome()
    {
        if (renameInputHomeParent != null)
            return;

        RectTransform rect = renameInputField.transform as RectTransform;
        renameInputHomeParent = renameInputField.transform.parent;
        renameInputHomeAnchorMin = rect.anchorMin;
        renameInputHomeAnchorMax = rect.anchorMax;
        renameInputHomePivot = rect.pivot;
        renameInputHomePosition = rect.anchoredPosition;
        renameInputHomeSize = rect.sizeDelta;
    }

    /// <summary>
    /// Restores the rename field to its authored home layout.
    /// </summary>
    private void RestoreRenameInputHome()
    {
        if (renameInputHomeParent == null)
            return;

        RectTransform rect = renameInputField.transform as RectTransform;
        renameInputField.transform.SetParent(renameInputHomeParent, false);
        rect.anchorMin = renameInputHomeAnchorMin;
        rect.anchorMax = renameInputHomeAnchorMax;
        rect.pivot = renameInputHomePivot;
        rect.anchoredPosition = renameInputHomePosition;
        rect.sizeDelta = renameInputHomeSize;
    }

    /// <summary>
    /// Completes the active rename gesture and emits its trimmed value.
    /// </summary>
    private void SubmitRename()
    {
        if (!IsRenameActive())
            return;

        renameCommitInProgress = true;
        string value = renameInputField.text?.Trim() ?? string.Empty;
        EndRenamePresentation();
        RenameSubmitted?.Invoke(this, value);
        renameCommitInProgress = false;
    }

    /// <summary>
    /// Cancels the active rename gesture and emits a semantic cancellation.
    /// </summary>
    private void CancelRename()
    {
        if (!IsRenameActive())
            return;

        EndRenamePresentation();
        RenameCancelled?.Invoke(this);
    }

    /// <summary>
    /// Returns whether the authored rename input currently represents a target.
    /// </summary>
    /// <returns>True when rename presentation is active.</returns>
    private bool IsRenameActive()
    {
        return (renameFleetRowIndex >= 0 || renameDetailItemIndex >= 0)
            && renameInputField
            && renameInputField.gameObject.activeSelf;
    }

    /// <summary>
    /// Ends rename presentation without emitting a semantic command.
    /// </summary>
    private void EndRenamePresentation()
    {
        renameEnding = true;
        if (renameSourceTextField != null)
            renameSourceTextField.enabled = true;

        renameFleetRowIndex = -1;
        renameDetailItemIndex = -1;
        renameSourceTextField = null;
        if (renameInputField != null)
        {
            renameInputField.DeactivateInputField();
            renameInputField.SetTextWithoutNotify(string.Empty);
            renameInputField.gameObject.SetActive(false);
            RestoreRenameInputHome();
        }

        renameEnding = false;
    }

    /// <summary>
    /// Binds authored tab and rename-field listeners exactly once.
    /// </summary>
    private void BindControls()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            FleetWindowTab tab = FleetWindowRenderData.OrderedTabs[i];
            UnityAction listener = () => TabRequested?.Invoke(this, tab);
            tabButtons[i].onClick.AddListener(listener);
            tabImages[i].GetComponent<UIPointerGestureRelay>().Dropped += HandleDetailItemsDropped;
            tabListeners.Add(listener);
        }

        renameInputField.onSubmit.AddListener(HandleRenameSubmitted);
        renameInputField.onEndEdit.AddListener(HandleRenameEndEdit);
    }

    /// <summary>
    /// Removes authored tab and rename-field listeners.
    /// </summary>
    private void UnbindControls()
    {
        int count = Mathf.Min(tabButtons.Length, tabListeners.Count);
        for (int i = 0; i < count; i++)
        {
            tabButtons[i]?.onClick.RemoveListener(tabListeners[i]);
            UIPointerGestureRelay dropRelay = tabImages[i]?.GetComponent<UIPointerGestureRelay>();
            if (dropRelay != null)
                dropRelay.Dropped -= HandleDetailItemsDropped;
        }
        tabListeners.Clear();

        if (renameInputField == null)
            return;
        renameInputField.onSubmit.RemoveListener(HandleRenameSubmitted);
        renameInputField.onEndEdit.RemoveListener(HandleRenameEndEdit);
    }

    /// <summary>
    /// Routes TMP submission through the local rename flow.
    /// </summary>
    /// <param name="value">The submitted value.</param>
    private void HandleRenameSubmitted(string value)
    {
        SubmitRename();
    }

    /// <summary>
    /// Treats focus loss as cancellation when no submit is in progress.
    /// </summary>
    /// <param name="value">The final input value.</param>
    private void HandleRenameEndEdit(string value)
    {
        if (IsRenameActive() && !renameCommitInProgress && !renameEnding)
            CancelRename();
    }

    /// <summary>
    /// Binds both authored scroll areas to semantic drag events.
    /// </summary>
    private void BindScrollEvents()
    {
        if (scrollEventsBound)
            return;

        fleetListScrollArea.Dragged += HandleScrollDragged;
        fleetListScrollArea.DragEnded += HandleScrollDragEnded;
        fleetListScrollArea.Dropped += HandleFleetListDropped;
        detailItemsScrollArea.Dragged += HandleScrollDragged;
        detailItemsScrollArea.DragEnded += HandleScrollDragEnded;
        detailItemsScrollArea.Dropped += HandleDetailItemsDropped;
        scrollEventsBound = true;
    }

    /// <summary>
    /// Removes both authored scroll areas' semantic drag subscriptions.
    /// </summary>
    private void UnbindScrollEvents()
    {
        if (!scrollEventsBound)
            return;

        fleetListScrollArea.Dragged -= HandleScrollDragged;
        fleetListScrollArea.DragEnded -= HandleScrollDragEnded;
        fleetListScrollArea.Dropped -= HandleFleetListDropped;
        detailItemsScrollArea.Dragged -= HandleScrollDragged;
        detailItemsScrollArea.DragEnded -= HandleScrollDragEnded;
        detailItemsScrollArea.Dropped -= HandleDetailItemsDropped;
        scrollEventsBound = false;
    }

    /// <summary>
    /// Detaches listeners from repeated fleet rows and detail cards owned by this view.
    /// </summary>
    private void UnbindItemViews()
    {
        for (int i = 0; i < fleetRowViews.Count; i++)
        {
            FleetListRowView row = fleetRowViews[i];
            if (row == null)
                continue;

            row.DoubleClicked -= HandleFleetRowDoubleClicked;
            row.Dropped -= HandleFleetRowDropped;
            row.Pressed -= HandleFleetRowPressed;
            row.Released -= HandleFleetRowReleased;
        }

        for (int i = 0; i < detailItemViews.Count; i++)
        {
            StrategyUnitCardView item = detailItemViews[i];
            if (item == null)
                continue;

            item.DoubleClicked -= HandleDetailItemDoubleClicked;
            item.Dropped -= HandleDetailItemDropped;
            item.Pressed -= HandleDetailItemPressed;
            item.Released -= HandleDetailItemReleased;
        }
    }

    /// <summary>
    /// Forwards a fleet-row press with its stable visual index.
    /// </summary>
    /// <param name="row">The pressed row.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetRowPressed(FleetListRowView row, PointerEventData eventData)
    {
        FleetRowPressed?.Invoke(this, row.Index, eventData);
    }

    /// <summary>
    /// Forwards a fleet-row release with its stable visual index.
    /// </summary>
    /// <param name="row">The released row.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetRowReleased(FleetListRowView row, PointerEventData eventData)
    {
        FleetRowReleased?.Invoke(this, row.Index, eventData);
    }

    /// <summary>
    /// Forwards a fleet-row drop with its stable visual index.
    /// </summary>
    /// <param name="row">The drop row.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetRowDropped(FleetListRowView row, PointerEventData eventData)
    {
        FleetRowDropped?.Invoke(this, row.Index, eventData);
    }

    /// <summary>
    /// Forwards a fleet-row double-click with its stable visual index.
    /// </summary>
    /// <param name="row">The double-clicked row.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetRowDoubleClicked(FleetListRowView row, PointerEventData eventData)
    {
        FleetRowDoubleClicked?.Invoke(this, row.Index, eventData);
    }

    /// <summary>
    /// Forwards a detail-card press with its stable visual index.
    /// </summary>
    /// <param name="item">The pressed detail card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDetailItemPressed(StrategyUnitCardView item, PointerEventData eventData)
    {
        DetailItemPressed?.Invoke(this, item.Index, eventData);
    }

    /// <summary>
    /// Forwards a detail-card release with its stable visual index.
    /// </summary>
    /// <param name="item">The released detail card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDetailItemReleased(StrategyUnitCardView item, PointerEventData eventData)
    {
        DetailItemReleased?.Invoke(this, item.Index, eventData);
    }

    /// <summary>
    /// Forwards a detail-card drop with its stable visual index.
    /// </summary>
    /// <param name="item">The drop detail card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDetailItemDropped(StrategyUnitCardView item, PointerEventData eventData)
    {
        DetailItemDropped?.Invoke(this, item.Index, eventData);
    }

    /// <summary>
    /// Forwards a detail-card double-click with its stable visual index.
    /// </summary>
    /// <param name="item">The double-clicked detail card.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleDetailItemDoubleClicked(
        StrategyUnitCardView item,
        PointerEventData eventData
    )
    {
        DetailItemDoubleClicked?.Invoke(this, item.Index, eventData);
    }

    /// <summary>
    /// Forwards a drop on the fleet-list surface.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleFleetListDropped(PointerEventData eventData)
    {
        FleetListDropped?.Invoke(this, eventData);
    }

    private void HandleDetailItemsDropped(PointerEventData eventData)
    {
        DetailItemsDropped?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards a scroll-surface drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleScrollDragged(PointerEventData eventData)
    {
        ScrollDragged?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards the end of a scroll-surface drag.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    private void HandleScrollDragEnded(PointerEventData eventData)
    {
        ScrollDragEnded?.Invoke(this, eventData);
    }

    /// <summary>
    /// Returns the raycast target carried by one pointer event.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    /// <returns>The current or pressed raycast target.</returns>
    private static GameObject GetRaycastTarget(PointerEventData eventData)
    {
        return eventData?.pointerCurrentRaycast.gameObject
            ?? eventData?.pointerPressRaycast.gameObject;
    }

    /// <summary>
    /// Calculates a scrolled drag-image rectangle in strategy source space.
    /// </summary>
    /// <param name="scrollArea">The owning scroll area.</param>
    /// <param name="itemRect">The repeated item's authored rectangle.</param>
    /// <param name="innerRect">The drag image's authored rectangle.</param>
    /// <returns>The drag image rectangle in strategy source space.</returns>
    private RectInt GetScrolledContentSourceRect(
        ScrollAreaView scrollArea,
        RectInt itemRect,
        RectInt innerRect
    )
    {
        RectInt scrollAreaRect = UILayout.GetSourceRect(scrollArea.transform as RectTransform);
        RectInt viewportRect = UILayout.GetSourceRect(scrollArea.ViewportRoot);
        int scrollY = Mathf.RoundToInt(scrollArea.ContentRoot.anchoredPosition.y);
        return new RectInt(
            currentWindowX + scrollAreaRect.x + viewportRect.x + itemRect.x + innerRect.x,
            currentWindowY + scrollAreaRect.y + viewportRect.y + itemRect.y - scrollY + innerRect.y,
            innerRect.width,
            innerRect.height
        );
    }

    /// <summary>
    /// Determines whether fleet-list content changed enough to reset scroll state.
    /// </summary>
    /// <param name="rows">The next fleet-list snapshot.</param>
    /// <returns>True when count or displayed names changed.</returns>
    private bool FleetRowsChanged(IReadOnlyList<FleetListRowRenderData> rows)
    {
        if (!renderedAnyFleetRows || renderedFleetRowNames.Count != rows.Count)
            return true;

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedFleetRowNames[i] != rows[i].Name)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines whether detail content changed enough to reset scroll state.
    /// </summary>
    /// <param name="items">The next detail-card snapshot.</param>
    /// <returns>True when tab, fleet, count, or displayed names changed.</returns>
    private bool DetailItemsChanged(IReadOnlyList<StrategyUnitCardRenderData> items)
    {
        if (
            !renderedAnyDetailItems
            || renderedDetailItemNames.Count != items.Count
            || renderedDetailActiveTab != currentActiveTab
            || renderedDetailSelectedFleetIndex != currentSelectedFleetIndex
        )
            return true;

        for (int i = 0; i < items.Count; i++)
        {
            if (renderedDetailItemNames[i] != (items[i].Name ?? string.Empty))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the authored fleet-list row height.
    /// </summary>
    /// <returns>The row height in source units.</returns>
    private int GetFleetListRowHeight()
    {
        return UILayout.GetSourceRect(fleetListRowTemplate.transform as RectTransform).height;
    }

    /// <summary>
    /// Gets the authored detail-card height.
    /// </summary>
    /// <returns>The card height in source units.</returns>
    private int GetDetailItemHeight()
    {
        return UILayout.GetSourceRect(detailItemTemplate.transform as RectTransform).height;
    }

    /// <summary>
    /// Changes a component's active state without affecting its authored layout.
    /// </summary>
    /// <param name="component">The component to update.</param>
    /// <param name="active">The requested active state.</param>
    private static void SetActive(Component component, bool active)
    {
        component.gameObject.SetActive(active);
    }

    /// <summary>
    /// Verifies every serialized reference required by the fleet presentation.
    /// </summary>
    private void VerifyReferences()
    {
        if (titleImage == null)
            throw new MissingReferenceException($"{name}/TitleImage is missing.");
        if (captionTextField == null)
            throw new MissingReferenceException($"{name}/CaptionTextField is missing.");
        if (fleetListScrollArea == null)
            throw new MissingReferenceException($"{name}/FleetListScrollArea is missing.");
        if (fleetListRowTemplate == null)
            throw new MissingReferenceException($"{name}/FleetListRowTemplate is missing.");
        if (detailBackgroundImage == null)
            throw new MissingReferenceException($"{name}/DetailBackgroundImage is missing.");
        if (bannerImage == null)
            throw new MissingReferenceException($"{name}/BannerImage is missing.");
        if (bannerEnrouteOverlayImage == null)
            throw new MissingReferenceException($"{name}/BannerEnrouteOverlayImage is missing.");
        if (bannerDamagedOverlayImage == null)
            throw new MissingReferenceException($"{name}/BannerDamagedOverlayImage is missing.");
        if (fleetNameTextField == null)
            throw new MissingReferenceException($"{name}/FleetNameTextField is missing.");
        if (capacityLeftTextField == null)
            throw new MissingReferenceException($"{name}/CapacityLeftTextField is missing.");
        if (capacityRightTextField == null)
            throw new MissingReferenceException($"{name}/CapacityRightTextField is missing.");
        if (tabsRoot == null)
            throw new MissingReferenceException($"{name}/Tabs is missing.");
        if (tabImages == null || tabImages.Length != FleetWindowRenderData.TabCount)
            throw new MissingReferenceException($"{name}/Tab images are missing.");
        if (tabButtons == null || tabButtons.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/Tab buttons are missing.");
        if (tabPressVisuals == null || tabPressVisuals.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/Tab press visuals are missing.");
        for (int i = 0; i < tabImages.Length; i++)
        {
            if (tabImages[i] == null)
                throw new MissingReferenceException(
                    $"{name}/{FleetWindowRenderData.OrderedTabs[i]}TabButtonImage is missing."
                );
            if (tabButtons[i] == null)
                throw new MissingReferenceException($"{name}/TabButton{i} is missing.");
            if (tabPressVisuals[i] == null)
                throw new MissingReferenceException($"{name}/TabPressVisual{i} is missing.");
            if (tabImages[i].GetComponent<UIPointerGestureRelay>() == null)
                throw new MissingReferenceException($"{name}/TabDropRelay{i} is missing.");
        }
        if (detailItemsScrollArea == null)
            throw new MissingReferenceException($"{name}/DetailItemsScrollArea is missing.");
        if (detailItemsScrollPaddingTemplate == null)
            throw new MissingReferenceException(
                $"{name}/DetailItemsScrollPaddingTemplate is missing."
            );
        if (detailItemTemplate == null)
            throw new MissingReferenceException($"{name}/DetailItemTemplate is missing.");
        if (renameInputField == null)
            throw new MissingReferenceException($"{name}/RenameInputField is missing.");
        fleetListRowTemplate.gameObject.SetActive(false);
        detailItemsScrollPaddingTemplate.gameObject.SetActive(false);
        detailItemTemplate.gameObject.SetActive(false);
    }
}
