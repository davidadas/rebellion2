using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders the searchable Encyclopedia index and emits local index interactions.
/// </summary>
public sealed class EncyclopediaIndexPanelView : MonoBehaviour
{
    private readonly List<UnityAction> tabListeners = new List<UnityAction>();

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private TMP_InputField entryNameInputField;

    [SerializeField]
    private RawImage[] tabImageSlots = Array.Empty<RawImage>();

    [SerializeField]
    private Button[] tabButtons = Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI tabTitleTextField;

    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private EncyclopediaWindowRowView rowTemplate;

    [SerializeField]
    private TextMeshProUGUI rowTextTemplate;

    [SerializeField]
    private RectTransform navigationScope;

    [SerializeField]
    private Texture2D allSystemsButtonUpTexture;

    [SerializeField]
    private Texture2D allSystemsButtonDownTexture;

    [SerializeField]
    private Texture2D systemButtonUpTexture;

    [SerializeField]
    private Texture2D systemButtonDownTexture;

    [SerializeField]
    private int contentBottomPadding;

    private readonly List<string> renderedEntryTypeIds = new List<string>();

    private bool activeWindow;
    private Texture[] defaultTabTextures = Array.Empty<Texture>();
    private EncyclopediaWindowTab renderedActiveTab;
    private bool renderedAnyRows;
    private SelectableListView<EncyclopediaWindowRowView, EncyclopediaWindowRowRenderData> rowsList;

    /// <summary>
    /// Raised when an index row requests the strategy context menu.
    /// </summary>
    public event Action<string, PointerEventData> ContextRequested;

    /// <summary>
    /// Raised when an index row is activated.
    /// </summary>
    public event Action<string> RowActivated;

    /// <summary>
    /// Raised when an index row is selected.
    /// </summary>
    public event Action<string> RowSelected;

    /// <summary>
    /// Raised when the entry-name filter changes.
    /// </summary>
    public event Action<string> SearchTextChanged;

    /// <summary>
    /// Raised when a database tab is selected.
    /// </summary>
    public event Action<EncyclopediaWindowTab> TabSelected;

    /// <summary>
    /// Validates authored references and binds controls.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        defaultTabTextures = CaptureTextures(tabImageSlots);
        BindControls();
    }

    /// <summary>
    /// Detaches every authored index control listener owned by this view.
    /// </summary>
    private void OnDestroy()
    {
        if (entryNameInputField != null)
            entryNameInputField.onValueChanged.RemoveListener(HandleSearchTextChanged);

        int count = Math.Min(tabButtons.Length, tabListeners.Count);
        for (int i = 0; i < count; i++)
        {
            if (tabButtons[i] != null && tabListeners[i] != null)
                tabButtons[i].onClick.RemoveListener(tabListeners[i]);
        }
    }

    /// <summary>
    /// Applies an immutable index-panel presentation snapshot.
    /// </summary>
    /// <param name="data">The index-panel presentation to render.</param>
    /// <param name="windowActive">Whether the owning window accepts keyboard navigation.</param>
    public void Render(EncyclopediaWindowIndexRenderData data, bool windowActive)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        activeWindow = windowActive;
        gameObject.SetActive(true);
        backgroundImage.gameObject.SetActive(true);
        entryNameInputField.gameObject.SetActive(true);
        entryNameInputField.SetTextWithoutNotify(data.SearchText);
        RenderTabs(data);
        UILayout.SetTextContent(tabTitleTextField, data.TabTitle);
        RenderRows(data);
    }

    /// <summary>
    /// Hides every index-only visual while retaining reusable row instances.
    /// </summary>
    public void Hide()
    {
        RowsList.Hide();
        backgroundImage.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Binds authored index controls to semantic handlers.
    /// </summary>
    private void BindControls()
    {
        entryNameInputField.onValueChanged.AddListener(HandleSearchTextChanged);
        for (int i = 0; i < tabButtons.Length; i++)
        {
            Button button = tabButtons[i];
            if (button == null)
            {
                tabListeners.Add(null);
                continue;
            }

            int tabIndex = i;
            UnityAction listener = () => HandleTabSelected(tabIndex);
            tabListeners.Add(listener);
            button.onClick.AddListener(listener);
        }
    }

    /// <summary>
    /// Emits the semantic database tab assigned to one authored slot.
    /// </summary>
    /// <param name="tabIndex">The authored tab slot.</param>
    private void HandleTabSelected(int tabIndex)
    {
        TabSelected?.Invoke(EncyclopediaWindowTabCatalog.GetTab(tabIndex));
    }

    /// <summary>
    /// Forwards a normalized entry-name filter.
    /// </summary>
    /// <param name="value">The current input-field value.</param>
    private void HandleSearchTextChanged(string value)
    {
        SearchTextChanged?.Invoke(value ?? string.Empty);
    }

    /// <summary>
    /// Forwards selection from one reusable index row.
    /// </summary>
    /// <param name="row">The selected row.</param>
    /// <param name="eventData">The pointer or navigation event.</param>
    private void HandleRowSelected(EncyclopediaWindowRowView row, PointerEventData eventData)
    {
        RowSelected?.Invoke(row.EntryTypeId);
    }

    /// <summary>
    /// Forwards activation from one reusable index row.
    /// </summary>
    /// <param name="row">The activated row.</param>
    /// <param name="eventData">The pointer or navigation event.</param>
    private void HandleRowActivated(EncyclopediaWindowRowView row, PointerEventData eventData)
    {
        RowActivated?.Invoke(row.EntryTypeId);
    }

    /// <summary>
    /// Forwards a context request without changing row selection.
    /// </summary>
    /// <param name="row">The row that received the context input.</param>
    /// <param name="eventData">The pointer event that requested the context menu.</param>
    private void HandleRowContextRequested(
        EncyclopediaWindowRowView row,
        PointerEventData eventData
    )
    {
        if (row != null)
            ContextRequested?.Invoke(row.EntryTypeId, eventData);
    }

    /// <summary>
    /// Renders faction-resolved artwork for each database tab.
    /// </summary>
    /// <param name="data">The index-panel presentation to render.</param>
    private void RenderTabs(EncyclopediaWindowIndexRenderData data)
    {
        if (data.Tabs.Count > tabImageSlots.Length || data.Tabs.Count > tabButtons.Length)
        {
            throw new MissingReferenceException(
                $"{name} cannot render {data.Tabs.Count} Encyclopedia tabs."
            );
        }

        for (int i = 0; i < data.Tabs.Count; i++)
        {
            RawImage image = tabImageSlots[i];
            EncyclopediaTabRenderData tab = data.Tabs[i];
            Texture texture =
                tab.Texture
                ?? GetAuthoredTabTexture(tab.Tab, tab.Tab == data.ActiveTab)
                ?? defaultTabTextures[i];
            UILayout.SetImageTexture(image, texture);
            image.raycastTarget = texture != null;
            tabButtons[i].interactable = texture != null;
        }

        for (int i = data.Tabs.Count; i < tabImageSlots.Length; i++)
            tabImageSlots[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Renders projected database rows through the authored row template.
    /// </summary>
    /// <param name="data">The index-panel presentation to render.</param>
    private void RenderRows(EncyclopediaWindowIndexRenderData data)
    {
        IReadOnlyList<EncyclopediaWindowRowRenderData> rows =
            data.Rows ?? Array.Empty<EncyclopediaWindowRowRenderData>();
        bool resetScroll = RowsChanged(data.ActiveTab, rows);
        int rowPitch = GetRowPitch();
        RowsList.Render(
            rows,
            GetScrollContentHeight(rows.Count, rowPitch),
            rowPitch,
            resetScroll,
            rowPitch,
            (rowView, row, index) => rowView.Render(index, row, rowTextTemplate),
            (row, _) => row.Selected
        );

        renderedAnyRows = true;
        renderedActiveTab = data.ActiveTab;
        renderedEntryTypeIds.Clear();
        for (int i = 0; i < rows.Count; i++)
            renderedEntryTypeIds.Add(rows[i].EntryTypeId);
    }

    /// <summary>
    /// Returns whether index content changed enough to reset scroll position.
    /// </summary>
    /// <param name="activeTab">The currently selected semantic database tab.</param>
    /// <param name="rows">The currently projected index rows.</param>
    /// <returns>True when tab or row identity changed.</returns>
    private bool RowsChanged(
        EncyclopediaWindowTab activeTab,
        IReadOnlyList<EncyclopediaWindowRowRenderData> rows
    )
    {
        if (
            !renderedAnyRows
            || renderedActiveTab != activeTab
            || renderedEntryTypeIds.Count != rows.Count
        )
        {
            return true;
        }

        for (int i = 0; i < rows.Count; i++)
        {
            if (renderedEntryTypeIds[i] != rows[i].EntryTypeId)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates the source-space content height for the index list.
    /// </summary>
    /// <param name="rowCount">The number of projected index rows.</param>
    /// <param name="rowPitch">The authored vertical row pitch.</param>
    /// <returns>The required source-space content height.</returns>
    private int GetScrollContentHeight(int rowCount, int rowPitch)
    {
        return UILayout.GetSourceRect(rowTextTemplate.rectTransform).y
            + contentBottomPadding
            + rowCount * rowPitch;
    }

    /// <summary>
    /// Gets the authored vertical pitch of an index row.
    /// </summary>
    /// <returns>The index row pitch in source-space units.</returns>
    private int GetRowPitch()
    {
        RectInt rowText = UILayout.GetSourceRect(rowTextTemplate.rectTransform);
        return rowText.y + rowText.height;
    }

    /// <summary>
    /// Returns whether row keyboard navigation is available.
    /// </summary>
    /// <returns>True when the owning window is active.</returns>
    private bool CanNavigateRows()
    {
        return activeWindow;
    }

    /// <summary>
    /// Captures immutable prefab textures for authored tab slots.
    /// </summary>
    /// <param name="images">The authored tab images.</param>
    /// <returns>The captured textures in slot order.</returns>
    private static Texture[] CaptureTextures(IReadOnlyList<RawImage> images)
    {
        if (images == null)
            return Array.Empty<Texture>();

        Texture[] textures = new Texture[images.Count];
        for (int index = 0; index < images.Count; index++)
            textures[index] = images[index]?.texture;
        return textures;
    }

    /// <summary>
    /// Returns the authored fallback texture for faction-neutral database tabs.
    /// </summary>
    /// <param name="tab">The semantic database tab.</param>
    /// <param name="selected">Whether the tab is selected.</param>
    /// <returns>The authored fallback texture, or null when none is assigned.</returns>
    private Texture GetAuthoredTabTexture(EncyclopediaWindowTab tab, bool selected)
    {
        return tab switch
        {
            EncyclopediaWindowTab.AllDatabases => selected
                ? allSystemsButtonDownTexture
                : allSystemsButtonUpTexture,
            EncyclopediaWindowTab.Systems => selected
                ? systemButtonDownTexture
                : systemButtonUpTexture,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the reusable index-list renderer backed by the authored row template.
    /// </summary>
    private SelectableListView<EncyclopediaWindowRowView, EncyclopediaWindowRowRenderData> RowsList
    {
        get
        {
            rowsList ??= new SelectableListView<
                EncyclopediaWindowRowView,
                EncyclopediaWindowRowRenderData
            >(
                rowsScrollArea,
                rowTemplate,
                "EncyclopediaRow",
                HandleRowSelected,
                HandleRowActivated,
                HandleRowContextRequested,
                CanNavigateRows,
                navigationScope
            );
            return rowsList;
        }
    }

    /// <summary>
    /// Verifies every authored child reference required to render the index panel.
    /// </summary>
    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (backgroundImage.texture == null)
            throw new MissingReferenceException($"{name}/BackgroundImage texture is missing.");
        if (entryNameInputField == null || entryNameInputField.textComponent == null)
            throw new MissingReferenceException($"{name}/EntryNameInputField is missing.");
        if (tabImageSlots == null || tabImageSlots.Length < EncyclopediaWindowTabCatalog.Count)
            throw new MissingReferenceException($"{name}/TabImageSlots are missing.");
        if (tabButtons == null || tabButtons.Length < EncyclopediaWindowTabCatalog.Count)
            throw new MissingReferenceException($"{name}/TabButtons are missing.");
        if (tabTitleTextField == null)
            throw new MissingReferenceException($"{name}/TabTitleTextField is missing.");
        if (rowsScrollArea == null)
            throw new MissingReferenceException($"{name}/RowsScrollArea is missing.");
        if (rowTemplate == null)
            throw new MissingReferenceException($"{name}/RowTemplate is missing.");
        if (rowTextTemplate == null)
            throw new MissingReferenceException($"{name}/RowTextTemplate is missing.");
        if (navigationScope == null)
            throw new MissingReferenceException($"{name}/NavigationScope is missing.");
        if (contentBottomPadding < 0)
            throw new MissingReferenceException($"{name}/ContentBottomPadding is invalid.");

        for (int i = 0; i < EncyclopediaWindowTabCatalog.Count; i++)
        {
            if (tabImageSlots[i] == null)
                throw new MissingReferenceException($"{name}/TabImage{i} is missing.");
            if (tabButtons[i] == null)
                throw new MissingReferenceException($"{name}/TabButton{i} is missing.");
        }

        rowTemplate.gameObject.SetActive(false);
        rowTextTemplate.gameObject.SetActive(false);
    }
}
