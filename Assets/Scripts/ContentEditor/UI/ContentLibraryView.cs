using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents installed, community and update collections with reusable list and detail controls.
/// </summary>
public sealed class ContentLibraryView : MonoBehaviour
{
    [SerializeField]
    private Button installedButton;

    [SerializeField]
    private Button browseButton;

    [SerializeField]
    private Button updatesButton;

    [SerializeField]
    private Button refreshButton;

    [SerializeField]
    private Button searchButton;

    [SerializeField]
    private TMP_InputField search;

    [SerializeField]
    private ScrollAreaLayout scroll;

    [SerializeField]
    private ContentLibraryRowView rowTemplate;

    [SerializeField]
    private TextMeshProUGUI empty;

    [SerializeField]
    private TextMeshProUGUI status;

    [SerializeField]
    private GameObject detail;

    [SerializeField]
    private TextMeshProUGUI detailTitle;

    [SerializeField]
    private TextMeshProUGUI detailAuthor;

    [SerializeField]
    private TextMeshProUGUI detailDescription;

    [SerializeField]
    private TextMeshProUGUI detailMetadata;

    [SerializeField]
    private ScrollAreaLayout detailScroll;

    [SerializeField]
    private ContentEditorWorkbenchLayout layout;

    [SerializeField]
    private RemoteImageView image;

    [SerializeField]
    private Button installButton;

    [SerializeField]
    private Button removeButton;

    [SerializeField]
    private Button folderButton;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private Button previousButton;

    [SerializeField]
    private Button nextButton;

    [SerializeField]
    private Button resumeButton;
    private SelectableListView<ContentLibraryRowView, ContentLibraryRow> list;
    private ContentLibraryPresentation current;
    private string previousFilter;
    private bool layoutDirty;

    public event Action<ContentLibraryPage> PageRequested;
    public event Action<string> SearchChanged;
    public event Action SearchRequested;
    public event Action<string> SelectionRequested;
    public event Action InstallRequested;
    public event Action<string> RemoveRequested;
    public event Action<string> FolderRequested;
    public event Action RefreshRequested;
    public event Action CancelRequested;
    public event Action<int> StepRequested;
    public event Action ResumeRequested;

    /// <summary>
    /// Connects authored controls once, including inactive prefab previews.
    /// </summary>
    private void Initialize()
    {
        if (list != null)
            return;
        installedButton.onClick.AddListener(() =>
            PageRequested?.Invoke(ContentLibraryPage.Installed)
        );
        browseButton.onClick.AddListener(() => PageRequested?.Invoke(ContentLibraryPage.Browse));
        updatesButton.onClick.AddListener(() => PageRequested?.Invoke(ContentLibraryPage.Updates));
        search.onValueChanged.AddListener(value => SearchChanged?.Invoke(value));
        search.onSubmit.AddListener(_ => SearchRequested?.Invoke());
        searchButton.onClick.AddListener(() => SearchRequested?.Invoke());
        installButton.onClick.AddListener(() => InstallRequested?.Invoke());
        removeButton.onClick.AddListener(() => RemoveRequested?.Invoke(current?.Selected?.Key));
        folderButton.onClick.AddListener(() =>
            FolderRequested?.Invoke(current?.Selected?.Directory)
        );
        refreshButton.onClick.AddListener(() => RefreshRequested?.Invoke());
        cancelButton.onClick.AddListener(() => CancelRequested?.Invoke());
        previousButton.onClick.AddListener(() => StepRequested?.Invoke(-1));
        nextButton.onClick.AddListener(() => StepRequested?.Invoke(1));
        resumeButton.onClick.AddListener(() => ResumeRequested?.Invoke());
        scroll.WidthChanged += HandleWidthChanged;
        detailScroll.WidthChanged += HandleWidthChanged;
        list = new SelectableListView<ContentLibraryRowView, ContentLibraryRow>(
            scroll.ScrollArea,
            rowTemplate,
            "ContentLibraryRow",
            (row, _) => SelectionRequested?.Invoke(row.Key),
            null,
            null,
            canNavigate: () => current?.Busy != true && !search.isFocused,
            navigationScope: scroll.transform
        );
    }

    /// <summary>
    /// Applies the current projection without accessing content files or starting catalog requests.
    /// </summary>
    /// <param name="data">The cached library state.</param>
    public void Render(ContentLibraryPresentation data)
    {
        Initialize();
        layoutDirty = false;
        current = data ?? throw new ArgumentNullException(nameof(data));
        string filter = data.Page + ":" + data.Search;
        bool reset = previousFilter != filter;
        previousFilter = filter;
        if (!search.isFocused)
            search.SetTextWithoutNotify(data.Search);
        installedButton.interactable = !data.Busy;
        browseButton.interactable = !data.Busy;
        updatesButton.interactable = !data.Busy;
        refreshButton.interactable = !data.Busy;
        search.interactable = !data.Busy;
        searchButton.interactable = !data.Busy;
        cancelButton.gameObject.SetActive(data.Busy);
        resumeButton.interactable = !data.Busy;
        previousButton.gameObject.SetActive(data.Page == ContentLibraryPage.Browse);
        nextButton.gameObject.SetActive(data.Page == ContentLibraryPage.Browse);
        previousButton.interactable = !data.Busy && data.CanPrevious;
        nextButton.interactable = !data.Busy && data.CanNext;
        SetTab(installedButton, data.Page == ContentLibraryPage.Installed);
        SetTab(browseButton, data.Page == ContentLibraryPage.Browse);
        SetTab(updatesButton, data.Page == ContentLibraryPage.Updates);
        status.SetText(data.Status ?? string.Empty);
        empty.SetText(data.EmptyMessage ?? string.Empty);
        empty.gameObject.SetActive(data.Rows.Length == 0);
        scroll.SetContentHeight(data.Rows.Length * 88);
        list.Render(
            data.Rows,
            data.Rows.Length * 88,
            88,
            reset,
            88,
            (view, row, index) => view.Render(row, index, row.Key == data.Selected?.Key)
        );
        ContentLibraryRow selected = data.Selected;
        detail.SetActive(selected != null);
        if (selected == null)
            return;
        detailTitle.SetText(selected.Name ?? string.Empty);
        detailAuthor.SetText(
            string.IsNullOrWhiteSpace(selected.Author) ? selected.Summary : "By " + selected.Author
        );
        detailDescription.SetText(selected.Description ?? string.Empty);
        detailMetadata.SetText(
            "Version: "
                + selected.Version
                + "\nStatus: "
                + selected.Status
                + (selected.Size > 0 ? "\nDownload: " + FormatSize(selected.Size) : string.Empty)
        );
        float width = Mathf.Max(1, detailScroll.ScrollArea.ViewportWidth);
        float descriptionHeight = Mathf.Max(
            40,
            detailDescription.GetPreferredValues(detailDescription.text, width, 0).y + 8
        );
        detailDescription.rectTransform.sizeDelta = new Vector2(width, descriptionHeight);
        detailMetadata.rectTransform.anchoredPosition = new Vector2(0, -descriptionHeight - 20);
        float metadataHeight = Mathf.Max(
            100,
            detailMetadata.GetPreferredValues(detailMetadata.text, width, 0).y + 8
        );
        detailMetadata.rectTransform.sizeDelta = new Vector2(width, metadataHeight);
        detailScroll.SetContentHeight(
            Mathf.CeilToInt(descriptionHeight + metadataHeight + 20),
            reset
        );
        image.Show(selected.ImageURL);
        installButton.gameObject.SetActive(selected.ModID > 0);
        installButton.GetComponentInChildren<TMP_Text>(true).SetText(selected.Action ?? "Install");
        installButton.interactable = !data.Busy && selected.CanInstall;
        removeButton.gameObject.SetActive(selected.CanRemove);
        removeButton.interactable = !data.Busy;
        folderButton.gameObject.SetActive(!string.IsNullOrEmpty(selected.Directory));
        folderButton.interactable = !data.Busy;
    }

    /// <summary>
    /// Shows a return action only while an editing project remains open.
    /// </summary>
    /// <param name="available">Whether the editor owns a project.</param>
    public void SetResumeAvailable(bool available)
    {
        Initialize();
        resumeButton.gameObject.SetActive(available);
    }

    /// <summary>
    /// Applies the editor's shared content zoom to the library panes.
    /// </summary>
    /// <param name="value">The application content scale.</param>
    public void SetZoom(float value) => layout.SetZoom(value);

    /// <summary>
    /// Marks text wrapping and pooled row widths for reflow after a pane is resized.
    /// </summary>
    private void HandleWidthChanged() => layoutDirty = true;

    /// <summary>
    /// Reflows after Unity finishes resizing the shared scroll viewports.
    /// </summary>
    private void LateUpdate()
    {
        if (layoutDirty && current != null)
            Render(current);
    }

    /// <summary>
    /// Disconnects scroll-layout notifications before the view is released.
    /// </summary>
    private void OnDestroy()
    {
        if (scroll != null)
            scroll.WidthChanged -= HandleWidthChanged;
        if (detailScroll != null)
            detailScroll.WidthChanged -= HandleWidthChanged;
    }

    /// <summary>
    /// Applies selected tab text through the existing editor color bindings.
    /// </summary>
    /// <param name="button">The authored tab.</param>
    /// <param name="selected">Whether its collection is active.</param>
    private static void SetTab(Button button, bool selected) =>
        button
            .GetComponentInChildren<ContentEditorColorBinding>(true)
            .SetRole(selected ? ContentEditorColorRole.Accent : ContentEditorColorRole.TextButton);

    /// <summary>
    /// Formats download sizes using the same units throughout the library.
    /// </summary>
    /// <param name="size">The archive size in bytes.</param>
    /// <returns>The readable download size.</returns>
    private static string FormatSize(long size) =>
        size >= 1024 * 1024
            ? (size / (1024d * 1024)).ToString("0.#", CultureInfo.CurrentCulture) + " MB"
            : (size / 1024d).ToString("0.#", CultureInfo.CurrentCulture) + " KB";
}
