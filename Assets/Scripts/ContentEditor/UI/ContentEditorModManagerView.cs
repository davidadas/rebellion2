using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents community browsing and installed content without opening an authoring project.
/// </summary>
public sealed class ContentEditorModManagerView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private Button browseButton;

    [SerializeField]
    private RectTransform panel;

    [SerializeField]
    private Button installedButton;

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
    private TextMeshProUGUI heading;

    [SerializeField]
    private TextMeshProUGUI status;

    [SerializeField]
    private RawImage icon;

    [SerializeField]
    private TMP_Dropdown category;

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
    private Image installInset;

    [SerializeField]
    private GameObject detailContent;

    [SerializeField]
    private TextMeshProUGUI emptyDetails;

    [SerializeField]
    private TMP_Dropdown installation;

    [SerializeField]
    private TMP_Dropdown sorting;

    [SerializeField]
    private Button clearSearchButton;

    [SerializeField]
    private Button resetButton;

    [SerializeField]
    private TextMeshProUGUI resultCount;

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
    private Image browseMarker;

    [SerializeField]
    private Image installedMarker;
    private readonly List<ContentEditorModCardView> rows = new();
    private readonly Dictionary<string, Texture2D> icons = new(StringComparer.Ordinal);
    private ScrollRect packageScroll;
    private string selectedIconUrl;
    private string requestedIcons;
    private bool browseLocked;
    private const int _cardHeight = 160;
    private const int _columns = 1;
    private bool bound;
    private bool busy;

    internal event Action<bool> TabRequested;
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
    internal string Search => search.text;
    internal string Category =>
        category.value == 0 ? null
        : category.value == 1 ? string.Empty
        : category.options[category.value].text;
    internal ContentEditorModFilter Filter => (ContentEditorModFilter)installation.value;
    internal ContentEditorModSort Sort => (ContentEditorModSort)sorting.value;

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
            || browseButton == null
            || installedButton == null
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
            || heading == null
            || status == null
            || icon == null
            || detailContent == null
            || emptyDetails == null
            || pageTitle == null
            || filterPanel == null
            || filterButton == null
            || detailOverlay == null
            || detailPanel == null
            || detailCloseButton == null
            || detailStatus == null
            || requirement == null
            || installInset == null
            || category == null
            || installation == null
            || sorting == null
            || clearSearchButton == null
            || resetButton == null
            || resultCount == null
            || emptyResults == null
            || creator == null
            || popularity == null
            || updated == null
            || categories == null
            || packageState == null
            || iconInitials == null
            || browseMarker == null
            || installedMarker == null
        )
            throw new MissingReferenceException(name + " has incomplete mod-manager controls.");
        browseButton.onClick.AddListener(Browse);
        installedButton.onClick.AddListener(Installed);
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
        category.onValueChanged.AddListener(ChangeCriteria);
        installation.onValueChanged.AddListener(ChangeCriteria);
        sorting.onValueChanged.AddListener(ChangeCriteria);
        clearSearchButton.onClick.AddListener(ClearSearch);
        resetButton.onClick.AddListener(ResetFilters);
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
    /// Fits the complete manager within the workspace when its aspect ratio changes.
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        if (panel == null || !bound || !gameObject.activeInHierarchy)
            return;
        Rect available = ((RectTransform)transform).rect;
        panel.sizeDelta = new Vector2(1120, Mathf.Max(816, available.height - 64));
        foreach (RectTransform target in new[] { panel, detailPanel })
            if (target != null)
                target.localScale =
                    Vector3.one
                    * Mathf.Min(
                        1,
                        Mathf.Max(1, available.width - 40) / target.rect.width,
                        Mathf.Max(1, available.height - 40) / target.rect.height
                    );
        LayoutResults(rows.Count(row => row.gameObject.activeSelf));
    }

    /// <summary>
    /// Hides the manager after its controller has canceled pending network work.
    /// </summary>
    internal void Hide() => gameObject.SetActive(false);

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
        LayoutResults(items.Count, resetScroll);
        for (int i = 0; i < items.Count; i++)
        {
            if (i == rows.Count)
                rows.Add(Instantiate(rowTemplate, packages.ScrollArea.ContentRoot));
            ContentEditorModCardView row = rows[i];
            RectTransform rect = (RectTransform)row.transform;
            float width = (packages.ScrollArea.ViewportWidth - 16 * (_columns - 1)) / _columns;
            rect.anchorMin = rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.sizeDelta = new Vector2(width, _cardHeight - 16);
            rect.anchoredPosition = new Vector2(
                i % _columns * (width + 16),
                -(i / _columns) * _cardHeight
            );
            row.Render(
                items[i],
                !browseLocked,
                !busy,
                SelectPackage,
                InstallPackage,
                FindIcon(items[i].IconUrl)
            );
        }
        for (int i = items.Count; i < rows.Count; i++)
            rows[i].gameObject.SetActive(false);
        pageTitle.text = "Mods";
        browseMarker.gameObject.SetActive(!installed);
        installedMarker.gameObject.SetActive(installed);
        browseButton
            .GetComponentInChildren<TMP_Text>()
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(installed ? ContentEditorColorRole.Text : ContentEditorColorRole.Accent);
        installedButton
            .GetComponentInChildren<TMP_Text>()
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(installed ? ContentEditorColorRole.Accent : ContentEditorColorRole.Text);
        string noun = total == 1 ? " mod" : " mods";
        resultCount.text =
            items.Count == total ? total + noun : items.Count + " of " + total + noun;
        emptyResults.gameObject.SetActive(items.Count == 0);
        emptyResults.text =
            total == 0 && installed
                ? "No mods installed yet.\n\nBrowse community mods or install a mod ZIP from your computer."
                : "No mods found.\n\nTry a shorter search or clear your filters.";
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
        int contentTop = filterPanel.activeSelf ? 432 : 320;
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
        RectTransform viewport = (RectTransform)packages.transform;
        viewport.offsetMin = new Vector2(viewport.offsetMin.x, hasStatus ? statusHeight + 16 : 0);
        viewport.offsetMax = new Vector2(viewport.offsetMax.x, -contentTop);
        resultCount.rectTransform.anchoredPosition = new Vector2(0, -(contentTop - 40));
        emptyResults.rectTransform.anchoredPosition = new Vector2(0, -(contentTop + 48));
        packages.SetContentHeight(Mathf.Max(0, count * _cardHeight - 16), resetScroll);
    }

    /// <summary>
    /// Refreshes real community categories while retaining a still-available selection.
    /// </summary>
    /// <param name="values">The categories present in the public catalog.</param>
    internal void SetCategories(IEnumerable<string> values)
    {
        string previous = category.options.ElementAtOrDefault(category.value)?.text;
        List<string> options = new List<string> { "All categories", "Uncategorized" };
        options.AddRange(
            values
                .Where(value => !string.IsNullOrWhiteSpace(value) && value != "Uncategorized")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
        );
        category.ClearOptions();
        category.AddOptions(options);
        category.SetValueWithoutNotify(Mathf.Max(0, options.IndexOf(previous)));
        category.RefreshShownValue();
    }

    /// <summary>
    /// Renders one package with independently selected distribution versions.
    /// </summary>
    /// <param name="entry">The selected package and its public metadata, or null for an empty result.</param>
    /// <param name="stateText">The current availability, activation, or compatibility state.</param>
    /// <param name="compatibilityIssue">The installation requirement preventing use, or an empty string.</param>
    /// <param name="text">The description, native identities, and dependencies.</param>
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
        creator.text = entry == null ? string.Empty : "by " + entry.Author;
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
                : entry?.Installation != null ? ContentEditorColorRole.Success
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
        activateButton.gameObject.SetActive(entry?.Installation != null);
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
        float primaryTop = contentBottom + (installed ? 24 : 40);
        float releaseTop = installed ? primaryTop + 76 : primaryTop;
        ((RectTransform)activateButton.transform).anchoredPosition = new Vector2(32, -primaryTop);
        ((RectTransform)versions.transform).anchoredPosition = new Vector2(32, -releaseTop);
        ((RectTransform)installButton.transform).anchoredPosition = new Vector2(232, -releaseTop);
        installInset.gameObject.SetActive(installed);
        installButton
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(installed ? ContentEditorColorRole.Divider : ContentEditorColorRole.Accent);
        installButton
            .GetComponentInChildren<TMP_Text>()
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(installed ? ContentEditorColorRole.Text : ContentEditorColorRole.Background);
        float bottom =
            canInstall ? releaseTop + 44
            : installed ? primaryTop + 44
            : contentBottom;
        float linksTop = bottom + 16;
        ((RectTransform)listingButton.transform).anchoredPosition = new Vector2(32, -linksTop);
        ((RectTransform)folderButton.transform).anchoredPosition = new Vector2(
            listingButton.gameObject.activeSelf ? 256 : 32,
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
                browseButton,
                installedButton,
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
        foreach (ContentEditorModCardView row in rows)
            row.SetInteractable(!browseLocked, !value);
        foreach (
            Button button in new[]
            {
                browseButton,
                installedButton,
                clearSearchButton,
                resetButton,
                filterButton,
            }
        )
            button.interactable = !browseLocked;
        clearSearchButton.interactable = !browseLocked && search.text.Length > 0;
        search.interactable = !browseLocked;
        category.interactable = !browseLocked;
        installation.interactable = !browseLocked;
        sorting.interactable = !browseLocked;
        versions.interactable = !browseLocked;
        closeButton.GetComponentInChildren<TMP_Text>(true).text = browseLocked
            ? "Cancel operation"
            : "‹ Back to editor";
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
            ContentEditorModCardView row in rows.Where(row =>
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
                ContentEditorModCardView row in rows.Where(row => row.Entry?.IconUrl == oldest)
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
        return rows.Where(
                (row, index) =>
                    row.gameObject.activeSelf
                    && index / _columns * _cardHeight < bottom
                    && (index / _columns + 1) * _cardHeight > top
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
        HideFilters();
        detailOverlay.SetActive(true);
        detailOverlay.transform.SetAsLastSibling();
        SelectionChanged?.Invoke(id);
        RequestIcons();
    }

    /// <summary>
    /// Opens installation feedback and requests the latest release for the chosen package.
    /// </summary>
    /// <param name="id">The package to install or update.</param>
    private void InstallPackage(string id)
    {
        HideFilters();
        detailOverlay.SetActive(true);
        detailOverlay.transform.SetAsLastSibling();
        QuickInstallRequested?.Invoke(id);
        RequestIcons();
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
        LayoutResults(rows.Count(row => row.gameObject.activeSelf));
    }

    /// <summary>
    /// Collapses filter choices without changing their selected values.
    /// </summary>
    private void HideFilters()
    {
        filterPanel.SetActive(false);
        LayoutResults(rows.Count(row => row.gameObject.activeSelf));
    }

    /// <summary>
    /// Shows clearing actions only while a query or filter is active.
    /// </summary>
    private void UpdateClearControls()
    {
        int activeFilters =
            (Category != null ? 1 : 0) + (Filter != ContentEditorModFilter.All ? 1 : 0);
        filterButton.GetComponentInChildren<TMP_Text>().text =
            activeFilters == 0 ? "Filters" : "Filters (" + activeFilters + ")";
        clearSearchButton.gameObject.SetActive(search.text.Length > 0);
        resetButton.gameObject.SetActive(
            search.text.Length > 0 || Category != null || Filter != ContentEditorModFilter.All
        );
    }

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
        category.SetValueWithoutNotify(0);
        installation.SetValueWithoutNotify(0);
        UpdateClearControls();
        CriteriaChanged?.Invoke();
    }

    /// <summary>
    /// Focuses package search from the toolkit's standard find shortcut.
    /// </summary>
    /// <param name="key">The incoming native key event.</param>
    /// <returns>Whether the manager handled the shortcut.</returns>
    internal bool HandleKeyboardShortcut(Event key)
    {
        if (
            key?.type != EventType.KeyDown
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
        foreach (TMP_Dropdown dropdown in new[] { category, installation, sorting, versions })
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
            browseButton.onClick.RemoveListener(Browse);
            installedButton.onClick.RemoveListener(Installed);
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
            category.onValueChanged.RemoveListener(ChangeCriteria);
            installation.onValueChanged.RemoveListener(ChangeCriteria);
            sorting.onValueChanged.RemoveListener(ChangeCriteria);
            clearSearchButton.onClick.RemoveListener(ClearSearch);
            resetButton.onClick.RemoveListener(ResetFilters);
            packageScroll.onValueChanged.RemoveListener(HandleScroll);
        }
        foreach (Texture2D texture in icons.Values)
            ReleaseObject(texture);
        icons.Clear();
    }

    /// <summary>
    /// Requests community browsing.
    /// </summary>
    private void Browse() => TabRequested?.Invoke(false);

    /// <summary>
    /// Requests installed content.
    /// </summary>
    private void Installed() => TabRequested?.Invoke(true);

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
