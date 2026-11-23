using System;
using System.Collections.Generic;
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
    private Button rowTemplate;

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
    private readonly List<Button> rows = new();
    private Texture2D iconTexture;
    private bool bound;
    private bool busy;

    internal event Action<bool> TabRequested;
    internal event Action RefreshRequested;
    internal event Action ImportRequested;
    internal event Action CloseRequested;
    internal event Action InstallRequested;
    internal event Action ActivateRequested;
    internal event Action UninstallRequested;
    internal event Action ListingRequested;
    internal event Action FolderRequested;
    internal event Action<string> SearchChanged;
    internal event Action<string> SelectionChanged;
    internal event Action<int> VersionChanged;
    internal bool IsOpen => gameObject.activeSelf;
    internal string Search => search.text;

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
            browseButton == null
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
        )
            throw new MissingReferenceException(name + " has incomplete mod-manager controls.");
        browseButton.onClick.AddListener(Browse);
        installedButton.onClick.AddListener(Installed);
        refreshButton.onClick.AddListener(Refresh);
        importButton.onClick.AddListener(Import);
        closeButton.onClick.AddListener(Close);
        installButton.onClick.AddListener(Install);
        activateButton.onClick.AddListener(Activate);
        uninstallButton.onClick.AddListener(Uninstall);
        listingButton.onClick.AddListener(Listing);
        folderButton.onClick.AddListener(Folder);
        search.onValueChanged.AddListener(Find);
        versions.onValueChanged.AddListener(SelectVersion);
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
    }

    /// <summary>
    /// Hides the manager after its controller has canceled pending network work.
    /// </summary>
    internal void Hide() => gameObject.SetActive(false);

    /// <summary>
    /// Replaces visible listing rows without retaining old selection callbacks.
    /// </summary>
    /// <param name="items">The stable identity and display text for each visible row.</param>
    /// <param name="selected">The selected package identity.</param>
    /// <param name="installed">Whether the installed tab is selected.</param>
    internal void RenderRows(
        IReadOnlyList<(string ID, string Text)> items,
        string selected,
        bool installed
    )
    {
        foreach (Button row in rows)
        {
            row.gameObject.SetActive(false);
            ReleaseObject(row.gameObject);
        }
        rows.Clear();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            Button row = Instantiate(rowTemplate, packages.ScrollArea.ContentRoot);
            row.gameObject.SetActive(true);
            RectTransform rect = (RectTransform)row.transform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0, 1);
            rect.offsetMin = new Vector2(0, -i * 80 - 74);
            rect.offsetMax = new Vector2(0, -i * 80);
            row.GetComponentInChildren<TMP_Text>(true).text = item.Text;
            row.interactable = !busy && item.ID != selected;
            row.onClick.AddListener(() => SelectionChanged?.Invoke(item.ID));
            rows.Add(row);
        }
        packages.SetContentHeight(items.Count * 80, true);
        browseButton.interactable = !busy && installed;
        installedButton.interactable = !busy && !installed;
    }

    /// <summary>
    /// Renders one package with independently selected distribution versions.
    /// </summary>
    /// <param name="title">The package display name.</param>
    /// <param name="text">The description, native identities, dependencies, and compatibility.</param>
    /// <param name="releases">Available distribution versions.</param>
    /// <param name="selectedVersion">The selected release index.</param>
    /// <param name="installLabel">The install, update, or reinstall action label.</param>
    /// <param name="canInstall">Whether a release is available.</param>
    /// <param name="activateLabel">The native activation action.</param>
    /// <param name="canActivate">Whether the package can be activated.</param>
    /// <param name="canRemove">Whether an installed package can be removed.</param>
    /// <param name="hasListing">Whether a public listing exists.</param>
    internal void RenderDetails(
        string title,
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
        heading.text = title;
        detailText.text = text;
        float height = detailText.GetPreferredValues(text, 496, float.PositiveInfinity).y + 16;
        detailText.rectTransform.sizeDelta = new Vector2(496, height);
        details.SetContentHeight(height, true);
        versions.ClearOptions();
        versions.AddOptions(
            releases.Count == 0 ? new List<string> { "No online releases" } : releases
        );
        versions.SetValueWithoutNotify(Mathf.Max(0, selectedVersion));
        versions.RefreshShownValue();
        versions.interactable = !busy && releases.Count > 0;
        installButton.GetComponentInChildren<TMP_Text>(true).text = installLabel;
        installButton.gameObject.SetActive(canInstall);
        activateButton.GetComponentInChildren<TMP_Text>(true).text = activateLabel;
        activateButton.gameObject.SetActive(canActivate);
        uninstallButton.gameObject.SetActive(canRemove);
        listingButton.gameObject.SetActive(hasListing);
        folderButton.gameObject.SetActive(canRemove);
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
    }

    /// <summary>
    /// Displays operation feedback within the manager.
    /// </summary>
    /// <param name="text">The current operation or actionable failure.</param>
    internal void SetStatus(string text) => status.text = text;

    /// <summary>
    /// Keeps cancellation available while preventing concurrent filesystem operations.
    /// </summary>
    /// <param name="value">Whether an operation is running.</param>
    internal void SetBusy(bool value)
    {
        busy = value;
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
        foreach (Button row in rows)
            row.interactable = !value;
        search.interactable = !value;
        versions.interactable = !value;
        closeButton.GetComponentInChildren<TMP_Text>(true).text = value ? "Cancel" : "Close";
    }

    /// <summary>
    /// Replaces the selected package's owned icon.
    /// </summary>
    /// <param name="bytes">Encoded icon bytes, or null to clear the preview.</param>
    internal void SetIcon(byte[] bytes)
    {
        ReleaseIcon();
        if (bytes != null)
        {
            iconTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(iconTexture, bytes, true))
                ReleaseIcon();
        }
        icon.texture = iconTexture;
        icon.enabled = iconTexture != null;
    }

    /// <summary>
    /// Routes Escape through cancellation or the manager close action.
    /// </summary>
    /// <returns>Whether the manager consumed the request.</returns>
    public bool TryCancel()
    {
        if (!IsOpen)
            return false;
        Close();
        return true;
    }

    /// <summary>
    /// Releases the current preview image.
    /// </summary>
    private void ReleaseIcon()
    {
        if (iconTexture != null)
            ReleaseObject(iconTexture);
        iconTexture = null;
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
            installButton.onClick.RemoveListener(Install);
            activateButton.onClick.RemoveListener(Activate);
            uninstallButton.onClick.RemoveListener(Uninstall);
            listingButton.onClick.RemoveListener(Listing);
            folderButton.onClick.RemoveListener(Folder);
            search.onValueChanged.RemoveListener(Find);
            versions.onValueChanged.RemoveListener(SelectVersion);
        }
        ReleaseIcon();
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
    /// Requests cancellation or dismissal.
    /// </summary>
    private void Close() => CloseRequested?.Invoke();

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
    private void Find(string value) => SearchChanged?.Invoke(value);

    /// <summary>
    /// Selects a distribution release.
    /// </summary>
    /// <param name="value">The selected release index.</param>
    private void SelectVersion(int value) => VersionChanged?.Invoke(value);
}
