using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns the authored messages index, its repeated rows, and index-local input controls.
/// </summary>
public sealed class MessagesIndexPanelView : MonoBehaviour
{
    private readonly List<string> renderedMessageIds = new List<string>();
    private readonly List<UnityAction> tabListeners = new List<UnityAction>();

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private Texture2D backgroundTexture;

    [SerializeField]
    private RawImage[] tabImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] tabPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] tabButtons = Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private MessageWindowRowView rowTemplate;

    [SerializeField]
    private int rowsContentPadding;

    [SerializeField]
    private RawImage selectAllButtonImage;

    [SerializeField]
    private RawImagePressVisual selectAllButtonPressVisual;

    [SerializeField]
    private Button selectAllButton;

    [SerializeField]
    private Texture2D selectAllButtonUpTexture;

    [SerializeField]
    private Texture2D selectAllButtonDownTexture;

    [SerializeField]
    private RawImage removeSelectedButtonImage;

    [SerializeField]
    private RawImagePressVisual removeSelectedButtonPressVisual;

    [SerializeField]
    private Button removeSelectedButton;

    [SerializeField]
    private Texture2D removeSelectedButtonUpTexture;

    [SerializeField]
    private Texture2D removeSelectedButtonDownTexture;

    private Func<bool> canNavigateRows;
    private bool initialized;
    private Transform navigationScope;
    private bool renderedAnyRows;
    private MessagesTab? renderedActiveTab;
    private SelectableListView<MessageWindowRowView, MessageWindowRowRenderData> rowsList;

    /// <summary>
    /// Raised when a row requests its context menu.
    /// </summary>
    public event Action<PointerEventData> ContextRequested;

    /// <summary>
    /// Raised when a message row is clicked.
    /// </summary>
    public event Action<string> RowClicked;

    /// <summary>
    /// Raised when a message row is double-clicked.
    /// </summary>
    public event Action<string> RowDoubleClicked;

    /// <summary>
    /// Raised when every message should be selected.
    /// </summary>
    public event Action SelectAllRequested;

    /// <summary>
    /// Raised when selected messages should be removed.
    /// </summary>
    public event Action RemoveSelectedRequested;

    /// <summary>
    /// Raised when another semantic Messages tab is requested.
    /// </summary>
    internal event Action<MessagesTab> TabRequested;

    /// <summary>
    /// Supplies the owning window's navigation predicate.
    /// </summary>
    /// <param name="canNavigate">Returns whether keyboard row navigation is currently allowed.</param>
    /// <param name="selectionScope">The owning window scope used to preserve keyboard focus.</param>
    public void Initialize(Func<bool> canNavigate, Transform selectionScope)
    {
        canNavigateRows = canNavigate ?? throw new ArgumentNullException(nameof(canNavigate));
        navigationScope = selectionScope ?? throw new ArgumentNullException(nameof(selectionScope));
        EnsureInitialized();
    }

    /// <summary>
    /// Applies a complete messages-index presentation snapshot.
    /// </summary>
    /// <param name="data">The projected messages-index presentation.</param>
    public void Render(MessagesIndexPanelRenderData data)
    {
        EnsureInitialized();
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        gameObject.SetActive(true);
        SetImageAtAuthoredRect(backgroundImage, backgroundTexture);
        RenderTabs(data.Tabs);
        UILayout.SetTextContent(titleTextField, data.Title);
        RenderRows(data.ActiveTab, data.Rows);
        SetButtonVisual(
            selectAllButtonImage,
            selectAllButtonPressVisual,
            selectAllButton,
            selectAllButtonUpTexture,
            selectAllButtonDownTexture
        );
        SetButtonVisual(
            removeSelectedButtonImage,
            removeSelectedButtonPressVisual,
            removeSelectedButton,
            removeSelectedButtonUpTexture,
            removeSelectedButtonDownTexture
        );
    }

    /// <summary>
    /// Hides the complete index panel without changing its local scroll history.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns the authored row pitch in source-space units.
    /// </summary>
    /// <returns>The row pitch.</returns>
    private int GetRowPitch()
    {
        EnsureInitialized();
        return UILayout.GetSourceRect(rowTemplate.transform as RectTransform).height;
    }

    /// <summary>
    /// Returns the content height required for a row count.
    /// </summary>
    /// <param name="rowCount">The number of displayed rows.</param>
    /// <returns>The required content height in source-space units.</returns>
    private int GetContentHeight(int rowCount)
    {
        return rowsContentPadding + rowCount * GetRowPitch();
    }

    /// <summary>
    /// Verifies authored references and binds index controls.
    /// </summary>
    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Detaches every authored index control listener owned by this view.
    /// </summary>
    private void OnDestroy()
    {
        int count = Math.Min(tabButtons.Length, tabListeners.Count);
        for (int index = 0; index < count; index++)
        {
            if (tabButtons[index] != null && tabListeners[index] != null)
                tabButtons[index].onClick.RemoveListener(tabListeners[index]);
        }

        if (selectAllButton != null)
            selectAllButton.onClick.RemoveListener(RequestSelectAll);
        if (removeSelectedButton != null)
            removeSelectedButton.onClick.RemoveListener(RequestRemoveSelected);
    }

    /// <summary>
    /// Captures authored tab fallbacks and binds controls exactly once.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized)
            return;

        VerifyReferences();
        for (int index = 0; index < tabButtons.Length; index++)
        {
            MessagesTab tab = MessagesTabCatalog.GetAt(index);
            UnityAction listener = () => TabRequested?.Invoke(tab);
            tabListeners.Add(listener);
            tabButtons[index].onClick.AddListener(listener);
        }

        selectAllButton.onClick.AddListener(RequestSelectAll);
        removeSelectedButton.onClick.AddListener(RequestRemoveSelected);
        rowTemplate.gameObject.SetActive(false);
        initialized = true;
    }

    /// <summary>
    /// Raises the semantic select-all request.
    /// </summary>
    private void RequestSelectAll()
    {
        SelectAllRequested?.Invoke();
    }

    /// <summary>
    /// Raises the semantic remove-selected request.
    /// </summary>
    private void RequestRemoveSelected()
    {
        RemoveSelectedRequested?.Invoke();
    }

    /// <summary>
    /// Applies tab textures in authored slot order.
    /// </summary>
    /// <param name="tabs">The projected tab presentations.</param>
    private void RenderTabs(IReadOnlyList<MessagesTabRenderData> tabs)
    {
        if (tabs == null || tabs.Count != MessagesTabCatalog.Count)
            throw new ArgumentException("Messages tab presentation is incomplete.", nameof(tabs));

        for (int index = 0; index < tabImages.Length; index++)
        {
            MessagesTabRenderData tab = tabs[index];
            if (tab.Tab != MessagesTabCatalog.GetAt(index))
                throw new ArgumentException(
                    "Messages tab presentation does not match authored slot order.",
                    nameof(tabs)
                );

            RawImage image = tabImages[index];
            image.gameObject.SetActive(true);
            Texture texture = tab.Texture;
            tabPressVisuals[index].SetInteractiveTextures(texture, tab.PressedTexture ?? texture);
            tabButtons[index].interactable = texture != null;
            image.raycastTarget = texture != null;
        }
    }

    /// <summary>
    /// Reconciles repeated row views and their scroll state.
    /// </summary>
    /// <param name="activeTab">The active semantic Messages tab.</param>
    /// <param name="rows">The rows in display order.</param>
    private void RenderRows(MessagesTab activeTab, IReadOnlyList<MessageWindowRowRenderData> rows)
    {
        IReadOnlyList<MessageWindowRowRenderData> safeRows =
            rows ?? Array.Empty<MessageWindowRowRenderData>();
        bool resetScroll = RowsChanged(activeTab, safeRows);
        RowsList.Render(
            safeRows,
            GetContentHeight(safeRows.Count),
            GetRowPitch(),
            resetScroll,
            GetRowPitch(),
            (rowView, row, index) => rowView.Render(row, index),
            (row, _) => row.Selected
        );

        renderedAnyRows = true;
        renderedActiveTab = activeTab;
        renderedMessageIds.Clear();
        for (int index = 0; index < safeRows.Count; index++)
            renderedMessageIds.Add(safeRows[index].MessageId);
    }

    /// <summary>
    /// Returns whether row identity or active tab changed since the previous render.
    /// </summary>
    /// <param name="activeTab">The active semantic Messages tab.</param>
    /// <param name="rows">The current rows in display order.</param>
    /// <returns><see langword="true"/> when scroll state should reset.</returns>
    private bool RowsChanged(MessagesTab activeTab, IReadOnlyList<MessageWindowRowRenderData> rows)
    {
        if (
            !renderedAnyRows
            || renderedActiveTab != activeTab
            || renderedMessageIds.Count != rows.Count
        )
            return true;

        for (int index = 0; index < rows.Count; index++)
        {
            if (renderedMessageIds[index] != rows[index].MessageId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Raises the semantic row-click request.
    /// </summary>
    /// <param name="row">The clicked row view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleRowClicked(MessageWindowRowView row, PointerEventData eventData)
    {
        RowClicked?.Invoke(row.MessageId);
    }

    /// <summary>
    /// Raises the semantic row-activation request.
    /// </summary>
    /// <param name="row">The activated row view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleRowDoubleClicked(MessageWindowRowView row, PointerEventData eventData)
    {
        RowDoubleClicked?.Invoke(row.MessageId);
    }

    /// <summary>
    /// Raises the row context-menu request.
    /// </summary>
    /// <param name="row">The row requesting a context menu.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleRowContextRequested(MessageWindowRowView row, PointerEventData eventData)
    {
        ContextRequested?.Invoke(eventData);
    }

    /// <summary>
    /// Returns whether the owning window currently permits row navigation.
    /// </summary>
    /// <returns><see langword="true"/> when row navigation is allowed.</returns>
    private bool CanNavigateRows()
    {
        return canNavigateRows?.Invoke() == true;
    }

    /// <summary>
    /// Assigns a static texture without changing its authored bounds.
    /// </summary>
    /// <param name="image">The authored image.</param>
    /// <param name="texture">The displayed texture.</param>
    private static void SetImageAtAuthoredRect(RawImage image, Texture texture)
    {
        UILayout.SetImageTexture(image, texture);
    }

    /// <summary>
    /// Applies static normal and pressed textures to an authored button.
    /// </summary>
    /// <param name="image">The authored button image.</param>
    /// <param name="pressVisual">The authored pressed-state visual.</param>
    /// <param name="button">The authored button control.</param>
    /// <param name="texture">The normal texture.</param>
    /// <param name="pressedTexture">The pressed texture.</param>
    private static void SetButtonVisual(
        RawImage image,
        RawImagePressVisual pressVisual,
        Button button,
        Texture texture,
        Texture pressedTexture
    )
    {
        pressVisual.SetInteractiveTextures(texture, pressedTexture ?? texture);
        button.interactable = texture != null;
        image.raycastTarget = texture != null;
    }

    /// <summary>
    /// Verifies every authored index-panel reference before use.
    /// </summary>
    private void VerifyReferences()
    {
        if (backgroundImage == null || backgroundTexture == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (tabImages == null || tabImages.Length != MessagesTabCatalog.Count)
            throw new MissingReferenceException(
                $"{name}/TabImages must contain {MessagesTabCatalog.Count} authored slots."
            );
        if (tabPressVisuals == null || tabPressVisuals.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/TabPressVisuals are incomplete.");
        if (tabButtons == null || tabButtons.Length != tabImages.Length)
            throw new MissingReferenceException($"{name}/TabButtons are incomplete.");
        for (int index = 0; index < tabImages.Length; index++)
        {
            if (
                tabImages[index] == null
                || tabPressVisuals[index] == null
                || tabButtons[index] == null
            )
                throw new MissingReferenceException($"{name}/Tab{index} is missing.");
        }
        if (titleTextField == null)
            throw new MissingReferenceException($"{name}/TitleTextField is missing.");
        if (rowsScrollArea == null || rowTemplate == null)
            throw new MissingReferenceException($"{name}/Rows are missing.");
        if (
            selectAllButtonImage == null
            || selectAllButtonPressVisual == null
            || selectAllButton == null
        )
            throw new MissingReferenceException($"{name}/SelectAllButton is missing.");
        if (
            removeSelectedButtonImage == null
            || removeSelectedButtonPressVisual == null
            || removeSelectedButton == null
        )
            throw new MissingReferenceException($"{name}/RemoveSelectedButton is missing.");
    }

    /// <summary>
    /// Lazily creates the repeated-row presenter after authored references are available.
    /// </summary>
    private SelectableListView<MessageWindowRowView, MessageWindowRowRenderData> RowsList
    {
        get
        {
            rowsList ??= new SelectableListView<MessageWindowRowView, MessageWindowRowRenderData>(
                rowsScrollArea,
                rowTemplate,
                "MessageRow",
                HandleRowClicked,
                HandleRowDoubleClicked,
                HandleRowContextRequested,
                CanNavigateRows,
                navigationScope
            );
            return rowsList;
        }
    }
}
