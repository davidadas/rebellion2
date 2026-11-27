using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Presents community browsing and installed content without opening an authoring project.
/// </summary>
public sealed class ContentEditorModManagerView : MonoBehaviour, ICancelable
{
    private const int _sectionHeadingHeight = 36;

    [SerializeField]
    private RectTransform panel;

    [SerializeField]
    private Button refreshButton;

    [SerializeField]
    private Button importButton;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private Button installButton;

    [SerializeField]
    private Button activateButton;

    [SerializeField]
    private Button uninstallButton;

    [SerializeField]
    private Button listingButton;

    [SerializeField]
    private Button folderButton;

    [SerializeField]
    private TMP_InputField search;

    [SerializeField]
    private TMP_Dropdown versions;

    [SerializeField]
    private ScrollAreaLayout packages;

    [SerializeField]
    private ContentEditorModCardView rowTemplate;

    [SerializeField]
    private ScrollAreaLayout details;

    [SerializeField]
    private TextMeshProUGUI detailText;

    [SerializeField]
    private TextMeshProUGUI releaseMetadata;

    [SerializeField]
    private TextMeshProUGUI heading;

    [SerializeField]
    private TextMeshProUGUI status;

    [SerializeField]
    private RawImage icon;

    [SerializeField]
    private ContentEditorModFiltersView filters;

    [SerializeField]
    private TextMeshProUGUI pageTitle;

    [SerializeField]
    private GameObject detailOverlay;

    [SerializeField]
    private GameObject filterPanel;

    [SerializeField]
    private Button filterButton;

    [SerializeField]
    private RectTransform detailPanel;

    [SerializeField]
    private Button detailCloseButton;

    [SerializeField]
    private TextMeshProUGUI detailStatus;

    [SerializeField]
    private TextMeshProUGUI requirement;

    [SerializeField]
    private GameObject detailContent;

    [SerializeField]
    private TextMeshProUGUI emptyDetails;

    [SerializeField]
    private TMP_Dropdown sorting;

    [SerializeField]
    private Button clearSearchButton;

    [SerializeField]
    private Button resetButton;

    [SerializeField]
    private TextMeshProUGUI resultCount;

    [SerializeField]
    private TextMeshProUGUI modsHeading;

    [SerializeField]
    [FormerlySerializedAs("erasHeading")]
    private TextMeshProUGUI gamePacksHeading;

    [SerializeField]
    private TextMeshProUGUI emptyResults;

    [SerializeField]
    private TextMeshProUGUI creator;

    [SerializeField]
    private TextMeshProUGUI popularity;

    [SerializeField]
    private TextMeshProUGUI updated;

    [SerializeField]
    private TextMeshProUGUI categories;

    [SerializeField]
    private TextMeshProUGUI packageState;

    [SerializeField]
    private TextMeshProUGUI iconInitials;

    [SerializeField]
    private TMP_Dropdown activePack;

    [SerializeField]
    private GameObject defaultPackControls;

    [SerializeField]
    private RectTransform defaultPackDescription;

    [SerializeField]
    private Button loadOrderButton;

    [SerializeField]
    private ContentEditorModOrderView modOrder;

    private float contentZoom = 1;
    private int inlineFilterHeight;
    private string[] activePackIDs = Array.Empty<string>();
    private bool library;
    private int libraryModCount;
    private SelectableListView<ContentEditorModCardView, ContentEditorModEntry> packageList;
    private IReadOnlyList<ContentEditorModCardView> Rows =>
        packageList?.Rows ?? Array.Empty<ContentEditorModCardView>();
    private string selectedPackageID;
    private readonly Dictionary<string, Texture2D> icons = new(StringComparer.Ordinal);
    private ScrollRect packageScroll;
    private string selectedIconUrl;
    private string requestedIcons;
    private bool browseLocked;
    private const int _cardHeight = 160;
    private const int _columns = 1;
    private bool bound;
    private bool busy;

    internal event Action<string> ActivePackRequested;
    internal event Action LoadOrderRequested;
    internal event Action RefreshRequested;
    internal event Action ImportRequested;
    internal event Action CloseRequested;
    internal event Action CancelRequested;
    internal event Action InstallRequested;
    internal event Action<string> QuickInstallRequested;
    internal event Action ActivateRequested;
    internal event Action UninstallRequested;
    internal event Action ListingRequested;
    internal event Action FolderRequested;
    internal event Action<string> SearchChanged;
    internal event Action<string> SelectionChanged;
    internal event Action<int> VersionChanged;
    internal event Action CriteriaChanged;
    internal event Action<string[]> IconsRequested;
    internal bool IsOpen => gameObject.activeSelf;
    internal bool IsChangingFiles => browseLocked;
    internal string Search => search.text;
    internal ContentEditorModFiltersView Filters => filters;
    internal ContentEditorModSort Sort =>
        ((DropdownOption<ContentEditorModSort>)sorting.options[sorting.value]).Value;

    /// <summary>
    /// Connects the authored controls once.
    /// </summary>
    private void Awake() => Bind();

    /// <summary>
    /// Registers matching listeners for manager operations.
    /// </summary>
    private void Bind()
    {
        if (bound)
            return;
        if (
            panel == null
            || refreshButton == null
            || importButton == null
            || closeButton == null
            || installButton == null
            || activateButton == null
            || uninstallButton == null
            || listingButton == null
            || folderButton == null
            || search == null
            || versions == null
            || packages == null
            || rowTemplate == null
            || details == null
            || detailText == null
            || releaseMetadata == null
            || heading == null
            || status == null
            || icon == null
            || detailContent == null
            || emptyDetails == null
            || activePack == null
            || defaultPackControls == null
            || pageTitle == null
            || filterPanel == null
            || filterButton == null
            || detailOverlay == null
            || detailPanel == null
            || detailCloseButton == null
            || detailStatus == null
            || requirement == null
            || filters == null
            || sorting == null
            || clearSearchButton == null
            || resetButton == null
            || resultCount == null
            || modsHeading == null
            || gamePacksHeading == null
            || emptyResults == null
            || creator == null
            || popularity == null
            || updated == null
            || categories == null
            || packageState == null
            || iconInitials == null
            || loadOrderButton == null
            || modOrder == null
        )
            throw new MissingReferenceException(name + " has incomplete mod-manager controls.");
        activePack.onValueChanged.AddListener(SelectActivePack);
        loadOrderButton.onClick.AddListener(RequestLoadOrder);
        refreshButton.onClick.AddListener(Refresh);
        importButton.onClick.AddListener(Import);
        closeButton.onClick.AddListener(Close);
        detailCloseButton.onClick.AddListener(DismissDetails);
        filterButton.onClick.AddListener(ToggleFilters);
        installButton.onClick.AddListener(Install);
        activateButton.onClick.AddListener(Activate);
        uninstallButton.onClick.AddListener(Uninstall);
        listingButton.onClick.AddListener(Listing);
        folderButton.onClick.AddListener(Folder);
        search.onValueChanged.AddListener(Find);
        versions.onValueChanged.AddListener(SelectVersion);
        filters.Changed += ChangeFilters;
        sorting.options = new List<TMP_Dropdown.OptionData>
        {
            new DropdownOption<ContentEditorModSort>(ContentEditorModSort.Relevance, "Best match"),
            new DropdownOption<ContentEditorModSort>(
                ContentEditorModSort.Downloads,
                "Most downloaded"
            ),
            new DropdownOption<ContentEditorModSort>(
                ContentEditorModSort.Updated,
                "Recently updated"
            ),
            new DropdownOption<ContentEditorModSort>(ContentEditorModSort.Newest, "Newest"),
            new DropdownOption<ContentEditorModSort>(ContentEditorModSort.Name, "Name (A–Z)"),
            new DropdownOption<ContentEditorModSort>(ContentEditorModSort.Likes, "Most liked"),
        };
        sorting.RefreshShownValue();
        sorting.onValueChanged.AddListener(ChangeCriteria);
        clearSearchButton.onClick.AddListener(ClearSearch);
        resetButton.onClick.AddListener(ResetFilters);
        packageList = new SelectableListView<ContentEditorModCardView, ContentEditorModEntry>(
            packages.ScrollArea,
            rowTemplate,
            "Package",
            (row, pointer) =>
            {
                selectedPackageID = row.Entry.ID;
                if (pointer != null)
                    SelectPackage(row.Entry.ID);
            },
            (row, _) => SelectPackage(row.Entry.ID),
            canNavigate: () => !browseLocked && !detailOverlay.activeSelf,
            navigationScope: packages.transform
        );
        packageScroll = packages.ScrollArea.ScrollRoot.GetComponent<ScrollRect>();
        packageScroll.onValueChanged.AddListener(HandleScroll);
        bound = true;
    }

    /// <summary>
    /// Opens the manager above the current workspace.
    /// </summary>
    internal void Show()
    {
        Bind();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        requestedIcons = null;
        detailOverlay.SetActive(false);
        filterPanel.SetActive(false);
        OnRectTransformDimensionsChange();
    }

    /// <summary>
    /// Applies the shared content scale without changing the active page or filter choices.
    /// </summary>
    /// <param name="zoom">The application content scale.</param>
    internal void SetContentZoom(float zoom)
    {
        contentZoom = zoom;
        OnRectTransformDimensionsChange();
    }

    /// <summary>
    /// Reflows browsing controls and cards within the window at the selected content scale.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        if (panel == null || !bound || !gameObject.activeInHierarchy)
            return;
        Rect available = ((RectTransform)transform).rect;
        Vector2 size = new(
            Mathf.Min(1120, Mathf.Max(1, available.width - 40) / contentZoom),
            Mathf.Max(1, available.height - 64) / contentZoom
        );
        if (panel.sizeDelta != size || !Mathf.Approximately(panel.localScale.x, contentZoom))
        {
            filters.TryCancel();
            foreach (TMP_Dropdown dropdown in new[] { sorting, activePack, versions })
                if (dropdown.IsExpanded)
                    dropdown.Hide();
        }
        panel.localScale = Vector3.one * contentZoom;
        panel.sizeDelta = size;
        int width = Mathf.FloorToInt(size.x);
        int sortWidth = Mathf.Min(304, Mathf.FloorToInt(width * 0.3f));
        int searchWidth = width - sortWidth - 144;
        UILayout.SetSourceRect((RectTransform)search.transform, 0, 112, searchWidth, 44);
        UILayout.SetSourceRect(
            (RectTransform)clearSearchButton.transform,
            searchWidth - 48,
            112,
            44,
            44
        );
        UILayout.SetSourceRect(
            (RectTransform)filterButton.transform,
            searchWidth + 16,
            112,
            112,
            44
        );
        UILayout.SetSourceRect(
            (RectTransform)sorting.transform,
            width - sortWidth,
            112,
            sortWidth,
            44
        );
        filters.SetWidth(width);
        UILayout.SetSourceRect((RectTransform)filterPanel.transform, 0, 176, width, filters.Height);
        int packWidth = Mathf.Min(440, Mathf.FloorToInt(width * 0.45f));
        const int orderWidth = 128;
        UILayout.SetSourceRect((RectTransform)defaultPackControls.transform, 0, 112, width, 72);
        UILayout.SetSourceRect(
            defaultPackDescription,
            0,
            0,
            Mathf.Max(1, width - packWidth - orderWidth - 36),
            72
        );
        UILayout.SetSourceRect(
            (RectTransform)activePack.transform,
            width - packWidth - orderWidth - 12,
            0,
            packWidth,
            44
        );
        UILayout.SetSourceRect(
            (RectTransform)loadOrderButton.transform,
            width - orderWidth,
            0,
            orderWidth,
            44
        );
        resultCount.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        emptyResults.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        detailPanel.localScale =
            Vector3.one
            * Mathf.Min(
                contentZoom,
                Mathf.Max(1, available.width - 40) / detailPanel.rect.width,
                Mathf.Max(1, available.height - 40) / detailPanel.rect.height
            );
        LayoutResults(Rows.Count(row => row.gameObject.activeSelf));
    }

    /// <summary>
    /// Hides the manager after its controller has canceled pending network work.
    /// </summary>
    internal void Hide()
    {
        modOrder.Close();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns to the editor unless an installation must first finish or be canceled.
    /// </summary>
    /// <returns>Whether the host may continue navigation.</returns>
    internal bool TryLeave()
    {
        if (browseLocked)
        {
            SetStatus("Finish or cancel the current operation before leaving Mods.");
            return false;
        }
        CloseRequested?.Invoke();
        return true;
    }

    /// <summary>
    /// Reuses package cards while retaining selection and scroll position across ordinary updates.
    /// </summary>
    /// <param name="items">The matching packages in display order.</param>
    /// <param name="installed">Whether the installed tab is selected.</param>
    /// <param name="total">The unfiltered package count.</param>
    /// <param name="resetScroll">Whether changed search criteria require returning to the top.</param>
    internal void RenderRows(
        IReadOnlyList<ContentEditorModEntry> items,
        bool installed,
        int total,
        bool resetScroll
    )
    {
        library = installed;
        libraryModCount = installed ? items.Count(entry => !entry.IsGamePack) : 0;
        LayoutResults(items.Count, resetScroll);
        packageList.Render(
            items,
            Mathf.CeilToInt(packages.ScrollArea.ContentRoot.rect.height),
            38,
            resetScroll,
            _cardHeight,
            (row, entry, index) =>
                row.Render(
                    index,
                    entry,
                    !browseLocked,
                    !busy,
                    SelectPackage,
                    InstallPackage,
                    FindIcon(entry.IconUrl)
                ),
            installed ? null : (entry, _) => entry.ID == selectedPackageID
        );
        pageTitle.text = installed ? "Library" : "Browse";
        defaultPackControls.SetActive(installed);
        search.gameObject.SetActive(!installed);
        sorting.gameObject.SetActive(!installed);
        filterButton.gameObject.SetActive(!installed);
        clearSearchButton.gameObject.SetActive(!installed && search.text.Length > 0);
        if (installed)
            filterPanel.SetActive(false);
        importButton.gameObject.SetActive(installed && !browseLocked);
        LayoutResults(items.Count, resetScroll);
        if (installed && !browseLocked && !detailOverlay.activeSelf)
        {
            ContentEditorModCardView selected = Rows.FirstOrDefault(row =>
                row.gameObject.activeSelf && row.Entry.ID == selectedPackageID
            );
            if (selected != null)
            {
                RectTransform rect = (RectTransform)selected.transform;
                packages.ScrollArea.RevealContentRect(-rect.anchoredPosition.y, rect.rect.height);
                SelectableListRowView.FocusRowForNavigation(packages.transform, true, selected);
            }
        }
        resultCount.gameObject.SetActive(!installed);
        string noun = total == 1 ? " package" : " packages";
        resultCount.text =
            items.Count == total ? total + noun : items.Count + " of " + total + noun;
        emptyResults.gameObject.SetActive(items.Count == 0);
        emptyResults.text =
            total == 0 && installed
                ? "No content installed yet.\n\nBrowse community content or install a ZIP from your computer."
                : "No packages found.\n\nTry a shorter search or clear your filters.";
        UpdateClearControls();
        RequestIcons();
    }

    /// <summary>
    /// Keeps the workspace stable while filters change the available result area.
    /// </summary>
    /// <param name="count">The number of matching cards.</param>
    /// <param name="resetScroll">Whether to return the results to the top.</param>
    private void LayoutResults(int count, bool resetScroll = false)
    {
        bool hasFilters = !library && filterPanel.activeSelf;
        int expandedTop =
            176 + Mathf.CeilToInt(((RectTransform)filterPanel.transform).rect.height) + 56;
        int contentTop =
            library ? 212
            : hasFilters ? expandedTop
            : 216;
        bool hasStatus = !string.IsNullOrEmpty(status.text);
        status.gameObject.SetActive(hasStatus);
        float statusHeight = hasStatus
            ? Mathf.Clamp(
                status.GetPreferredValues(status.text, panel.rect.width, float.PositiveInfinity).y,
                24,
                64
            )
            : 0;
        status.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, statusHeight);
        // Keep filters in the scrolling area when zoom leaves too little space for a card.
        inlineFilterHeight =
            hasFilters && panel.rect.height - contentTop - statusHeight < _cardHeight
                ? Mathf.CeilToInt(((RectTransform)filterPanel.transform).rect.height) + 16
                : 0;
        if (inlineFilterHeight > 0)
            contentTop = 216;
        Transform filterParent = inlineFilterHeight > 0 ? packages.ScrollArea.ContentRoot : panel;
        if (filterPanel.transform.parent != filterParent)
            filterPanel.transform.SetParent(filterParent, false);
        UILayout.SetSourceRect(
            (RectTransform)filterPanel.transform,
            0,
            inlineFilterHeight > 0 ? 0 : 176,
            Mathf.FloorToInt(panel.rect.width),
            filters.Height
        );
        RectTransform viewport = (RectTransform)packages.transform;
        viewport.offsetMin = new Vector2(viewport.offsetMin.x, hasStatus ? statusHeight + 16 : 0);
        viewport.offsetMax = new Vector2(viewport.offsetMax.x, -contentTop);
        resultCount.rectTransform.anchoredPosition = new Vector2(0, -(contentTop - 40));
        if (emptyResults.transform.parent != filterParent)
            emptyResults.transform.SetParent(filterParent, false);
        UILayout.SetSourceRect(
            emptyResults.rectTransform,
            0,
            (inlineFilterHeight > 0 ? inlineFilterHeight : contentTop) + 48,
            Mathf.FloorToInt(panel.rect.width),
            160
        );
        modsHeading.gameObject.SetActive(library && libraryModCount > 0);
        gamePacksHeading.gameObject.SetActive(library && count > libraryModCount);
        int modsHeight = modsHeading.gameObject.activeSelf ? _sectionHeadingHeight : 0;
        int gamePacksHeight = gamePacksHeading.gameObject.activeSelf ? _sectionHeadingHeight : 0;
        int widthAvailable = Mathf.FloorToInt(packages.ScrollArea.ViewportWidth);
        UILayout.SetSourceRect(modsHeading.rectTransform, 0, 0, widthAvailable, 28);
        UILayout.SetSourceRect(
            gamePacksHeading.rectTransform,
            0,
            modsHeight + libraryModCount * _cardHeight,
            widthAvailable,
            28
        );
        packages.SetContentHeight(
            inlineFilterHeight
                + modsHeight
                + gamePacksHeight
                + (
                    inlineFilterHeight > 0 && count == 0
                        ? 208
                        : Mathf.Max(0, count * _cardHeight - 16)
                ),
            resetScroll
        );
        for (int index = 0; index < Rows.Count; index++)
        {
            RectTransform rect = (RectTransform)Rows[index].transform;
            float width = (packages.ScrollArea.ViewportWidth - 16 * (_columns - 1)) / _columns;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.sizeDelta = new Vector2(width, _cardHeight - 16);
            rect.anchoredPosition = new Vector2(
                index % _columns * (width + 16),
                -GetCardTop(index)
            );
        }
    }

    /// <summary>
    /// Locates a card in the same coordinate space as the scroll viewport and section headings.
    /// </summary>
    /// <param name="index">The ordered card slot.</param>
    /// <returns>The distance from the content root's top edge.</returns>
    private int GetCardTop(int index) =>
        inlineFilterHeight
        + (index / _columns) * _cardHeight
        + (modsHeading.gameObject.activeSelf ? _sectionHeadingHeight : 0)
        + (
            gamePacksHeading.gameObject.activeSelf && index >= libraryModCount
                ? _sectionHeadingHeight
                : 0
        );

    /// <summary>
    /// Lists installed packs without changing the saved default during rendering.
    /// </summary>
    /// <param name="packs">The installed content pack definitions.</param>
    /// <param name="selectedID">The saved default pack identifier.</param>
    internal void RenderActivePacks(IReadOnlyList<ContentPackDefinition> packs, string selectedID)
    {
        activePackIDs = packs.Select(pack => pack.ID).ToArray();
        activePack.ClearOptions();
        activePack.AddOptions(
            packs.Count == 0
                ? new List<string> { "No game packs installed" }
                : packs.Select(pack => pack.DisplayName).ToList()
        );
        activePack.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(activePackIDs, selectedID)));
        activePack.RefreshShownValue();
        activePack.interactable = !busy && packs.Count > 0;
        loadOrderButton.interactable = activePack.interactable;
    }

    /// <summary>
    /// Opens the focused editor for the enabled order of the current game pack.
    /// </summary>
    /// <param name="gamePack">The default game pack name shown in the library.</param>
    /// <param name="mods">The currently applied stack.</param>
    /// <param name="apply">The action that saves the proposed order.</param>
    internal void ShowLoadOrder(
        string gamePack,
        IReadOnlyList<ContentModDefinition> mods,
        Action<IReadOnlyList<string>> apply
    ) => modOrder.Show(gamePack, mods, apply);

    /// <summary>
    /// Requests the load order for the library's current game pack.
    /// </summary>
    private void RequestLoadOrder() => LoadOrderRequested?.Invoke();

    /// <summary>
    /// Requests the default pack explicitly chosen by the player.
    /// </summary>
    /// <param name="index">The selected installed pack index.</param>
    private void SelectActivePack(int index)
    {
        if (index >= 0 && index < activePackIDs.Length)
            ActivePackRequested?.Invoke(activePackIDs[index]);
    }

    /// <summary>
    /// Renders one package with independently selected distribution versions.
    /// </summary>
    /// <param name="entry">The selected package and its public metadata, or null for an empty result.</param>
    /// <param name="stateText">The current availability, activation, or compatibility state.</param>
    /// <param name="compatibilityIssue">The installation requirement preventing use, or an empty string.</param>
    /// <param name="text">The description, native identities, and dependencies.</param>
    /// <param name="metadata">The installed release and download summary.</param>
    /// <param name="releases">Available distribution versions.</param>
    /// <param name="selectedVersion">The selected release index.</param>
    /// <param name="installLabel">The install, update, or reinstall action label.</param>
    /// <param name="canInstall">Whether a release is available.</param>
    /// <param name="activateLabel">The native activation action.</param>
    /// <param name="canActivate">Whether the package can be activated.</param>
    /// <param name="canRemove">Whether an installed package can be removed.</param>
    /// <param name="hasListing">Whether a public listing exists.</param>
    internal void RenderDetails(
        ContentEditorModEntry entry,
        string stateText,
        string compatibilityIssue,
        string text,
        string metadata,
        List<string> releases,
        int selectedVersion,
        string installLabel,
        bool canInstall,
        string activateLabel,
        bool canActivate,
        bool canRemove,
        bool hasListing
    )
    {
        detailContent.SetActive(entry != null);
        emptyDetails.gameObject.SetActive(entry == null);
        heading.text = entry?.Name ?? "Select a package";
        creator.text = string.IsNullOrEmpty(entry?.Author) ? string.Empty : "by " + entry.Author;
        popularity.text = entry?.Popularity ?? string.Empty;
        updated.text =
            entry?.Listing == null
                ? "Local content"
                : "Updated "
                    + entry.Updated.ToString(
                        "d MMM yyyy",
                        System.Globalization.CultureInfo.InvariantCulture
                    );
        categories.text = entry == null ? string.Empty : string.Join(" · ", entry.Categories);
        packageState.text = entry == null ? string.Empty : stateText;
        requirement.text = compatibilityIssue;
        requirement.gameObject.SetActive(!string.IsNullOrEmpty(compatibilityIssue));
        packageState
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(
                !string.IsNullOrEmpty(compatibilityIssue) ? ContentEditorColorRole.Error
                : entry?.IsInstalled == true ? ContentEditorColorRole.Success
                : ContentEditorColorRole.Muted
            );
        iconInitials.text =
            entry == null
                ? "?"
                : entry.Name.Substring(0, Math.Min(2, entry.Name.Length)).ToUpperInvariant();
        selectedIconUrl = entry?.IconUrl;
        icon.texture = FindIcon(selectedIconUrl);
        icon.enabled = icon.texture != null;
        iconInitials.gameObject.SetActive(!icon.enabled);
        detailText.text = text;
        releaseMetadata.text = metadata;
        details.SetContentHeight(detailText.rectTransform.rect.height);
        float width = Mathf.Max(1, details.ScrollArea.ViewportWidth);
        float height = Mathf.Ceil(
            detailText.GetPreferredValues(text, width, float.PositiveInfinity).y
        );
        detailText.rectTransform.sizeDelta = new Vector2(width, height);
        details.SetContentHeight(height, true);
        versions.ClearOptions();
        versions.AddOptions(
            releases.Count == 0 ? new List<string> { "No online releases" } : releases
        );
        versions.SetValueWithoutNotify(Mathf.Max(0, selectedVersion));
        versions.RefreshShownValue();
        versions.gameObject.SetActive(canInstall);
        versions.interactable = !browseLocked && releases.Count > 0;
        installButton.GetComponentInChildren<TMP_Text>(true).text = installLabel;
        installButton.gameObject.SetActive(canInstall);
        activateButton.GetComponentInChildren<TMP_Text>(true).text = activateLabel;
        activateButton.gameObject.SetActive(entry?.IsInstalled == true);
        uninstallButton.gameObject.SetActive(canRemove);
        listingButton.gameObject.SetActive(hasListing);
        folderButton.gameObject.SetActive(canRemove);
        LayoutDetails();
        foreach (
            Button button in new[]
            {
                installButton,
                activateButton,
                uninstallButton,
                listingButton,
                folderButton,
            }
        )
            button.interactable = !busy;
        activateButton.interactable = !busy && canActivate;
        RequestIcons();
    }

    /// <summary>
    /// Fits package actions and feedback without leaving empty rows for unavailable commands.
    /// </summary>
    private void LayoutDetails()
    {
        if (!detailContent.activeSelf)
        {
            detailPanel.sizeDelta = new Vector2(detailPanel.sizeDelta.x, 320);
            OnRectTransformDimensionsChange();
            return;
        }
        bool hasCategories = !string.IsNullOrEmpty(categories.text);
        categories.gameObject.SetActive(hasCategories);
        float descriptionTop = hasCategories ? 208 : 176;
        ((RectTransform)details.transform).anchoredPosition = new Vector2(32, -descriptionTop);
        float textWidth = Mathf.Max(1, details.ScrollArea.ViewportWidth);
        float descriptionHeight = Mathf.Clamp(
            Mathf.Ceil(
                detailText.GetPreferredValues(detailText.text, textWidth, float.PositiveInfinity).y
            ),
            24,
            200
        );
        ((RectTransform)details.transform).SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            descriptionHeight
        );
        float contentBottom = descriptionTop + descriptionHeight;
        bool hasMetadata = !string.IsNullOrEmpty(releaseMetadata.text);
        releaseMetadata.gameObject.SetActive(hasMetadata);
        if (hasMetadata)
        {
            float metadataHeight = Mathf.Max(
                24,
                Mathf.Ceil(
                    releaseMetadata
                        .GetPreferredValues(
                            releaseMetadata.text,
                            releaseMetadata.rectTransform.rect.width,
                            float.PositiveInfinity
                        )
                        .y
                )
            );
            releaseMetadata.rectTransform.anchoredPosition = new Vector2(32, -(contentBottom + 16));
            releaseMetadata.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                metadataHeight
            );
            contentBottom += metadataHeight + 16;
        }
        if (requirement.gameObject.activeSelf)
        {
            float requirementHeight = Mathf.Max(
                24,
                requirement
                    .GetPreferredValues(
                        requirement.text,
                        requirement.rectTransform.rect.width,
                        float.PositiveInfinity
                    )
                    .y
            );
            requirement.rectTransform.anchoredPosition = new Vector2(32, -(contentBottom + 16));
            requirement.rectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                requirementHeight
            );
            contentBottom += requirementHeight + 16;
        }
        bool installed = activateButton.gameObject.activeSelf;
        bool canInstall = installButton.gameObject.activeSelf;
        float releaseTop = contentBottom + (canInstall ? 52 : 24);
        ((RectTransform)activateButton.transform).anchoredPosition = new Vector2(448, -releaseTop);
        ((RectTransform)versions.transform).anchoredPosition = new Vector2(32, -releaseTop);
        ((RectTransform)installButton.transform).anchoredPosition = new Vector2(232, -releaseTop);
        ((RectTransform)installButton.transform).SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            installed ? 200 : 496
        );
        ContentEditorAppearance.SetActionStyle(installButton, !installed);
        float bottom = canInstall || installed ? releaseTop + 44 : contentBottom;
        float linksTop = bottom + 24;
        ((RectTransform)listingButton.transform).anchoredPosition = new Vector2(32, -linksTop);
        ((RectTransform)folderButton.transform).anchoredPosition = new Vector2(
            listingButton.gameObject.activeSelf ? 232 : 32,
            -linksTop
        );
        ((RectTransform)uninstallButton.transform).anchoredPosition = new Vector2(628, -linksTop);
        if (
            listingButton.gameObject.activeSelf
            || folderButton.gameObject.activeSelf
            || uninstallButton.gameObject.activeSelf
        )
            bottom = linksTop + 44;
        bool hasStatus = !string.IsNullOrEmpty(detailStatus.text);
        detailStatus.gameObject.SetActive(hasStatus);
        float statusHeight = hasStatus
            ? Mathf.Max(
                24,
                detailStatus
                    .GetPreferredValues(
                        detailStatus.text,
                        detailStatus.rectTransform.rect.width,
                        float.PositiveInfinity
                    )
                    .y
            )
            : 0;
        detailStatus.rectTransform.anchoredPosition = new Vector2(32, -(bottom + 16));
        detailStatus.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            statusHeight
        );
        detailPanel.sizeDelta = new Vector2(
            detailPanel.sizeDelta.x,
            bottom + 32 + (hasStatus ? statusHeight + 16 : 0)
        );
        OnRectTransformDimensionsChange();
    }

    /// <summary>
    /// Displays operation feedback within the manager.
    /// </summary>
    /// <param name="text">The current operation or actionable failure.</param>
    internal void SetStatus(string text)
    {
        status.text = text;
        detailStatus.text = text;
        LayoutDetails();
    }

    /// <summary>
    /// Keeps cancellation available while preventing concurrent filesystem operations.
    /// </summary>
    /// <param name="value">Whether an operation is running.</param>
    /// <param name="lockBrowsing">Whether the operation must also pause search and navigation.</param>
    internal void SetBusy(bool value, bool lockBrowsing = true)
    {
        busy = value;
        browseLocked = value && lockBrowsing;
        foreach (
            Button button in new[]
            {
                refreshButton,
                importButton,
                installButton,
                activateButton,
                uninstallButton,
                listingButton,
                folderButton,
            }
        )
            button.interactable = !value;
        foreach (ContentEditorModCardView row in Rows)
            row.SetInteractable(!browseLocked, !value);
        foreach (Button button in new[] { clearSearchButton, resetButton, filterButton })
            button.interactable = !browseLocked;
        clearSearchButton.interactable = !browseLocked && search.text.Length > 0;
        search.interactable = !browseLocked;
        filters.SetInteractable(!browseLocked);
        sorting.interactable = !browseLocked;
        versions.interactable = !browseLocked;
        activePack.interactable = !value && activePackIDs.Length > 0;
        loadOrderButton.interactable = activePack.interactable;
        closeButton.gameObject.SetActive(browseLocked);
        importButton.gameObject.SetActive(library && !browseLocked);
        refreshButton.GetComponentInChildren<TMP_Text>(true).text =
            value && !browseLocked ? "Refreshing…" : "Refresh";
        detailCloseButton.GetComponentInChildren<TMP_Text>(true).text = browseLocked
            ? "Cancel"
            : "Close";
    }

    /// <summary>
    /// Returns whether an icon is already decoded in this manager's bounded cache.
    /// </summary>
    /// <param name="url">The exact public icon URL.</param>
    /// <returns>Whether the icon can be reused without another request.</returns>
    internal bool HasIcon(string url) => !string.IsNullOrEmpty(url) && icons.ContainsKey(url);

    /// <summary>
    /// Decodes one icon and updates only the cards still displaying that URL.
    /// </summary>
    /// <param name="url">The public image identity.</param>
    /// <param name="bytes">The encoded package artwork.</param>
    internal void SetIcon(string url, byte[] bytes)
    {
        if (HasIcon(url))
            return;
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!ImageConversion.LoadImage(texture, bytes, true))
        {
            ReleaseObject(texture);
            return;
        }
        icons.Add(url, texture);
        foreach (
            ContentEditorModCardView row in Rows.Where(row =>
                row.gameObject.activeSelf && row.Entry?.IconUrl == url
            )
        )
            row.SetArtwork(texture);
        if (selectedIconUrl == url)
        {
            icon.texture = texture;
            icon.enabled = true;
            iconInitials.gameObject.SetActive(false);
        }
        if (icons.Count > 64)
        {
            HashSet<string> visible = new HashSet<string>(VisibleIcons(), StringComparer.Ordinal);
            string oldest = icons.Keys.First(key => !visible.Contains(key));
            foreach (
                ContentEditorModCardView row in Rows.Where(row => row.Entry?.IconUrl == oldest)
            )
                row.SetArtwork(null);
            ReleaseObject(icons[oldest]);
            icons.Remove(oldest);
        }
    }

    /// <summary>
    /// Returns a cache-owned icon without creating a placeholder texture.
    /// </summary>
    /// <param name="url">The public image identity.</param>
    /// <returns>The decoded image, or null while unavailable.</returns>
    private Texture2D FindIcon(string url) =>
        url != null && icons.TryGetValue(url, out Texture2D value) ? value : null;

    /// <summary>
    /// Enumerates artwork needed by visible cards and the selected detail panel.
    /// </summary>
    /// <returns>The distinct visible icon URLs.</returns>
    private IEnumerable<string> VisibleIcons()
    {
        float top = packages.ScrollArea.ContentRoot.anchoredPosition.y;
        float bottom = top + packages.ScrollArea.ViewportHeight;
        return Rows.Where(
                (row, index) =>
                    row.gameObject.activeSelf
                    && GetCardTop(index) < bottom
                    && GetCardTop(index) + _cardHeight > top
            )
            .Select(row => row.Entry?.IconUrl)
            .Append(detailOverlay.activeSelf ? selectedIconUrl : null)
            .Where(url => !string.IsNullOrEmpty(url))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(url => url, StringComparer.Ordinal);
    }

    /// <summary>
    /// Requests new visible artwork only when the visible URL set changes.
    /// </summary>
    private void RequestIcons()
    {
        if (!gameObject.activeInHierarchy)
            return;
        string[] urls = VisibleIcons().ToArray();
        string key = string.Join("\n", urls);
        if (key == requestedIcons)
            return;
        requestedIcons = key;
        IconsRequested?.Invoke(urls);
    }

    /// <summary>
    /// Updates artwork requests when the user scrolls to different cards.
    /// </summary>
    /// <param name="position">The shared scroll control's current position.</param>
    private void HandleScroll(Vector2 position) => RequestIcons();

    /// <summary>
    /// Forwards the current card's stable identity without recreating its button listener.
    /// </summary>
    /// <param name="id">The selected package identity.</param>
    private void SelectPackage(string id)
    {
        selectedPackageID = id;
        HideFilters();
        detailOverlay.SetActive(true);
        detailOverlay.transform.SetAsLastSibling();
        SelectionChanged?.Invoke(id);
        RequestIcons();
    }

    /// <summary>
    /// Starts installation from the card while keeping the current results visible.
    /// </summary>
    /// <param name="id">The package to install or update.</param>
    private void InstallPackage(string id)
    {
        selectedPackageID = id;
        QuickInstallRequested?.Invoke(id);
    }

    /// <summary>
    /// Dismisses package details while routing an active operation through cancellation.
    /// </summary>
    private void DismissDetails()
    {
        if (browseLocked)
            Close();
        else
        {
            detailOverlay.SetActive(false);
            RequestIcons();
        }
    }

    /// <summary>
    /// Opens filter choices only when the user asks to refine the catalog.
    /// </summary>
    private void ToggleFilters()
    {
        filterPanel.SetActive(!filterPanel.activeSelf);
        LayoutResults(Rows.Count(row => row.gameObject.activeSelf), true);
    }

    /// <summary>
    /// Collapses filter choices without changing their selected values.
    /// </summary>
    private void HideFilters()
    {
        filterPanel.SetActive(false);
        LayoutResults(Rows.Count(row => row.gameObject.activeSelf));
    }

    /// <summary>
    /// Shows clearing actions only while a query or filter is active.
    /// </summary>
    private void UpdateClearControls()
    {
        int activeFilters = filters.ActiveCount;
        filterButton.GetComponentInChildren<TMP_Text>().text =
            activeFilters == 0 ? "Filters" : "Filters (" + activeFilters + ")";
        clearSearchButton.gameObject.SetActive(!library && search.text.Length > 0);
        resetButton.gameObject.SetActive(search.text.Length > 0 || filters.ActiveCount > 0);
    }

    /// <summary>
    /// Applies combined community filter changes to the current catalogue.
    /// </summary>
    private void ChangeFilters() => ChangeCriteria(0);

    /// <summary>
    /// Applies a filter or sort selection immediately.
    /// </summary>
    /// <param name="value">The changed selector index.</param>
    private void ChangeCriteria(int value)
    {
        UpdateClearControls();
        CriteriaChanged?.Invoke();
    }

    /// <summary>
    /// Clears the query while retaining the user's current filters and ordering.
    /// </summary>
    private void ClearSearch()
    {
        search.text = string.Empty;
        search.Select();
        search.ActivateInputField();
    }

    /// <summary>
    /// Resets search and filters while retaining the chosen ordering.
    /// </summary>
    private void ResetFilters()
    {
        search.SetTextWithoutNotify(string.Empty);
        filters.Clear();
    }

    /// <summary>
    /// Focuses package search from the toolkit's standard find shortcut.
    /// </summary>
    /// <param name="key">The incoming native key event.</param>
    /// <returns>Whether the manager handled the shortcut.</returns>
    internal bool HandleKeyboardShortcut(Event key)
    {
        if (
            library
            || key?.type != EventType.KeyDown
            || key.keyCode != KeyCode.F
            || (!key.control && !key.command)
        )
            return false;
        if (browseLocked)
        {
            key.Use();
            return true;
        }
        HideFilters();
        if (detailOverlay.activeSelf)
            DismissDetails();
        search.Select();
        search.ActivateInputField();
        key.Use();
        return true;
    }

    /// <summary>
    /// Routes Escape through cancellation or the manager close action.
    /// </summary>
    /// <returns>Whether the manager consumed the request.</returns>
    public bool TryCancel()
    {
        if (!IsOpen)
            return false;
        if (modOrder.TryCancel())
            return true;
        if (filters.TryCancel())
            return true;
        foreach (TMP_Dropdown dropdown in new[] { sorting, versions })
            if (dropdown.IsExpanded)
            {
                dropdown.Hide();
                return true;
            }
        if (detailOverlay.activeSelf)
        {
            DismissDetails();
            return true;
        }
        if (filterPanel.activeSelf)
        {
            HideFilters();
            return true;
        }
        if (search.isFocused && search.text.Length > 0)
        {
            ClearSearch();
            return true;
        }
        Close();
        return true;
    }

    /// <summary>
    /// Releases owned UI objects in both play mode and editor previews.
    /// </summary>
    /// <param name="value">The owned object to release.</param>
    private static void ReleaseObject(UnityEngine.Object value)
    {
        if (Application.isPlaying)
            Destroy(value);
        else
            DestroyImmediate(value);
    }

    /// <summary>
    /// Releases control subscriptions and owned preview media.
    /// </summary>
    private void OnDestroy()
    {
        if (bound)
        {
            activePack.onValueChanged.RemoveListener(SelectActivePack);
            loadOrderButton.onClick.RemoveListener(RequestLoadOrder);
            refreshButton.onClick.RemoveListener(Refresh);
            importButton.onClick.RemoveListener(Import);
            closeButton.onClick.RemoveListener(Close);
            detailCloseButton.onClick.RemoveListener(DismissDetails);
            filterButton.onClick.RemoveListener(ToggleFilters);
            installButton.onClick.RemoveListener(Install);
            activateButton.onClick.RemoveListener(Activate);
            uninstallButton.onClick.RemoveListener(Uninstall);
            listingButton.onClick.RemoveListener(Listing);
            folderButton.onClick.RemoveListener(Folder);
            search.onValueChanged.RemoveListener(Find);
            versions.onValueChanged.RemoveListener(SelectVersion);
            filters.Changed -= ChangeFilters;
            sorting.onValueChanged.RemoveListener(ChangeCriteria);
            clearSearchButton.onClick.RemoveListener(ClearSearch);
            resetButton.onClick.RemoveListener(ResetFilters);
            packageScroll.onValueChanged.RemoveListener(HandleScroll);
        }
        packageList?.Clear();
        foreach (Texture2D texture in icons.Values)
            ReleaseObject(texture);
        icons.Clear();
    }

    /// <summary>
    /// Requests a fresh catalog.
    /// </summary>
    private void Refresh() => RefreshRequested?.Invoke();

    /// <summary>
    /// Requests local archive import.
    /// </summary>
    private void Import() => ImportRequested?.Invoke();

    /// <summary>
    /// Dispatches the displayed cancel or return action without hiding an active installation.
    /// </summary>
    private void Close()
    {
        if (browseLocked)
            CancelRequested?.Invoke();
        else
            CloseRequested?.Invoke();
    }

    /// <summary>
    /// Requests the selected release.
    /// </summary>
    private void Install() => InstallRequested?.Invoke();

    /// <summary>
    /// Requests content activation.
    /// </summary>
    private void Activate() => ActivateRequested?.Invoke();

    /// <summary>
    /// Requests removal confirmation.
    /// </summary>
    private void Uninstall() => UninstallRequested?.Invoke();

    /// <summary>
    /// Requests the public listing.
    /// </summary>
    private void Listing() => ListingRequested?.Invoke();

    /// <summary>
    /// Requests the installed folder.
    /// </summary>
    private void Folder() => FolderRequested?.Invoke();

    /// <summary>
    /// Filters the visible catalog.
    /// </summary>
    /// <param name="value">The entered query.</param>
    private void Find(string value)
    {
        clearSearchButton.interactable = !browseLocked && value.Length > 0;
        UpdateClearControls();
        SearchChanged?.Invoke(value);
    }

    /// <summary>
    /// Selects a distribution release.
    /// </summary>
    /// <param name="value">The selected release index.</param>
    private void SelectVersion(int value) => VersionChanged?.Invoke(value);
}
