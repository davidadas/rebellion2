using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents a project-local listing draft, release notes, and the package export review.
/// </summary>
public sealed class ContentEditorPublishingView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private TMP_InputField _packageName;

    [SerializeField]
    private TMP_InputField _description;

    [SerializeField]
    private TMP_InputField _website;

    [SerializeField]
    private TMP_InputField _readme;

    [SerializeField]
    private TMP_InputField _dependencies;

    [SerializeField]
    private TMP_InputField _changelog;

    [SerializeField]
    private TMP_InputField _version;

    [SerializeField]
    private GameObject[] _stages;

    [SerializeField]
    private Button[] _stepButtons;

    [SerializeField]
    private Button _nextButton;

    [SerializeField]
    private Button _saveButton;

    [SerializeField]
    private Button _closeButton;

    [SerializeField]
    private Button _browseButton;

    [SerializeField]
    private Button _communityButton;

    [SerializeField]
    private TextMeshProUGUI _identity;

    [SerializeField]
    private TextMeshProUGUI _preview;

    [SerializeField]
    private TextMeshProUGUI _review;

    [SerializeField]
    private TextMeshProUGUI _status;

    [SerializeField]
    private TextMeshProUGUI _iconLabel;

    [SerializeField]
    private RawImage _icon;
    private Texture2D _iconTexture;
    private string _iconPath;
    private ContentPackDefinition _definition;
    private bool _bound;
    private int _step;

    internal event Action SaveRequested;
    internal event Action CloseRequested;
    internal event Action BrowseIconRequested;
    internal event Action ExportRequested;
    internal event Action CommunityRequested;
    internal bool IsOpen => gameObject.activeSelf;
    private IEnumerable<TMP_InputField> Fields =>
        new[] { _packageName, _description, _website, _readme, _dependencies, _changelog };

    /// <summary>
    /// Binds the authored publishing controls when their dialog becomes active.
    /// </summary>
    private void Awake() => BindControls();

    /// <summary>
    /// Connects the dialog's controls once, retaining ownership of the matching cleanup.
    /// </summary>
    private void BindControls()
    {
        if (_bound)
            return;
        if (
            _packageName == null
            || _description == null
            || _website == null
            || _readme == null
            || _dependencies == null
            || _changelog == null
            || _version == null
            || _stages == null
            || _stages.Length != 3
            || _stepButtons == null
            || _stepButtons.Length != 3
            || _nextButton == null
            || _saveButton == null
            || _closeButton == null
            || _browseButton == null
            || _communityButton == null
            || _identity == null
            || _preview == null
            || _review == null
            || _status == null
            || _iconLabel == null
            || _icon == null
        )
            throw new MissingReferenceException(name + " has incomplete publishing controls.");
        foreach (TMP_InputField field in Fields)
            field.onValueChanged.AddListener(HandleFieldChanged);
        _stepButtons[0].onClick.AddListener(ShowListing);
        _stepButtons[1].onClick.AddListener(ShowRelease);
        _stepButtons[2].onClick.AddListener(ShowReview);
        _nextButton.onClick.AddListener(Continue);
        _saveButton.onClick.AddListener(RequestSave);
        _closeButton.onClick.AddListener(RequestClose);
        _browseButton.onClick.AddListener(RequestIcon);
        _communityButton.onClick.AddListener(RequestCommunity);
        _bound = true;
    }

    /// <summary>
    /// Releases control listeners and the dialog-owned icon preview.
    /// </summary>
    private void OnDestroy()
    {
        if (_bound)
        {
            foreach (TMP_InputField field in Fields)
                field.onValueChanged.RemoveListener(HandleFieldChanged);
            _stepButtons[0].onClick.RemoveListener(ShowListing);
            _stepButtons[1].onClick.RemoveListener(ShowRelease);
            _stepButtons[2].onClick.RemoveListener(ShowReview);
            _nextButton.onClick.RemoveListener(Continue);
            _saveButton.onClick.RemoveListener(RequestSave);
            _closeButton.onClick.RemoveListener(RequestClose);
            _browseButton.onClick.RemoveListener(RequestIcon);
            _communityButton.onClick.RemoveListener(RequestCommunity);
        }
        ReleaseIcon();
    }

    /// <summary>
    /// Opens the publishing workflow without changing the pack's canonical identity or version.
    /// </summary>
    /// <param name="definition">The pack whose current snapshot will be released.</param>
    /// <param name="settings">The saved publishing draft.</param>
    internal void Show(ContentPackDefinition definition, ContentEditorPublishingSettings settings)
    {
        BindControls();
        _definition = definition;
        _packageName.SetTextWithoutNotify(settings.PackageName ?? string.Empty);
        _description.SetTextWithoutNotify(settings.Description ?? string.Empty);
        _website.SetTextWithoutNotify(settings.WebsiteUrl ?? string.Empty);
        _readme.SetTextWithoutNotify(settings.Readme ?? string.Empty);
        _dependencies.SetTextWithoutNotify(settings.Dependencies ?? string.Empty);
        _changelog.SetTextWithoutNotify(settings.Changelog ?? string.Empty);
        _version.SetTextWithoutNotify(definition.Version);
        _identity.text = definition.DisplayName + " · Content pack";
        SetIcon(settings.IconPath, null);
        ShowStatus(string.Empty);
        SetStep(0);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// Collects the current listing draft without applying it to runtime content.
    /// </summary>
    /// <returns>The independently editable publishing fields.</returns>
    internal ContentEditorPublishingSettings ReadSettings() =>
        new ContentEditorPublishingSettings
        {
            PackageName = _packageName.text.Trim(),
            Description = _description.text.Trim(),
            WebsiteUrl = _website.text.Trim(),
            Readme = _readme.text,
            Dependencies = _dependencies.text,
            Changelog = _changelog.text,
            IconPath = _iconPath,
        };

    /// <summary>
    /// Replaces the dialog-owned icon preview after the application validates its source bytes.
    /// </summary>
    /// <param name="path">The selected icon or an empty path for a new draft.</param>
    /// <param name="bytes">The icon bytes already read and validated by the application.</param>
    internal void SetIcon(string path, byte[] bytes)
    {
        ReleaseIcon();
        _iconPath = path;
        if (bytes != null)
        {
            _iconTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(_iconTexture, bytes, true))
                throw new InvalidDataException("The package icon could not be loaded.");
        }
        _icon.texture = _iconTexture;
        _icon.enabled = _iconTexture != null;
        _iconLabel.text = bytes == null ? "256 × 256 PNG required" : "256 × 256 PNG selected";
        RenderPreview();
    }

    /// <summary>
    /// Displays validation, save, or export feedback within its owning workflow.
    /// </summary>
    /// <param name="message">The user-facing operation result.</param>
    /// <param name="isError">Whether the operation failed.</param>
    internal void ShowStatus(string message, bool isError = false)
    {
        _status.text = message;
        _status
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(isError ? ContentEditorColorRole.Error : ContentEditorColorRole.Text);
    }

    /// <summary>
    /// Hides the publishing workflow after the controller has retained its draft.
    /// </summary>
    internal void Hide() => gameObject.SetActive(false);

    /// <summary>
    /// Routes Escape through the same draft-retaining close action as the visible button.
    /// </summary>
    /// <returns>Whether this dialog consumed the close request.</returns>
    public bool TryCancel()
    {
        if (!IsOpen)
            return false;
        RequestClose();
        return true;
    }

    /// <summary>
    /// Updates the listing preview after one native text control changes.
    /// </summary>
    /// <param name="value">The edited value.</param>
    private void HandleFieldChanged(string value) => RenderPreview();

    /// <summary>
    /// Shows the listing fields.
    /// </summary>
    private void ShowListing() => SetStep(0);

    /// <summary>
    /// Shows release notes and declared package dependencies.
    /// </summary>
    private void ShowRelease() => SetStep(1);

    /// <summary>
    /// Shows the exact release identity and export contents for review.
    /// </summary>
    private void ShowReview() => SetStep(2);

    /// <summary>
    /// Selects a publishing stage without discarding fields from the other stages.
    /// </summary>
    /// <param name="step">The zero-based listing, release, or review stage.</param>
    private void SetStep(int step)
    {
        _step = step;
        for (int i = 0; i < _stages.Length; i++)
        {
            _stages[i].SetActive(i == step);
            _stepButtons[i].interactable = i != step;
        }
        _nextButton.GetComponentInChildren<TMP_Text>().text =
            step == 2 ? "Export package..."
            : step == 1 ? "Review release"
            : "Continue to release";
        RenderPreview();
    }

    /// <summary>
    /// Advances the draft or requests a validated package export from the final review.
    /// </summary>
    private void Continue()
    {
        if (_step < 2)
            SetStep(_step + 1);
        else
            ExportRequested?.Invoke();
    }

    /// <summary>
    /// Requests persistence of the listing draft.
    /// </summary>
    private void RequestSave() => SaveRequested?.Invoke();

    /// <summary>
    /// Requests a close after retaining the current publishing draft.
    /// </summary>
    private void RequestClose() => CloseRequested?.Invoke();

    /// <summary>
    /// Requests a native file picker for a package icon.
    /// </summary>
    private void RequestIcon() => BrowseIconRequested?.Invoke();

    /// <summary>
    /// Requests the community page where the author can upload the exported package.
    /// </summary>
    private void RequestCommunity() => CommunityRequested?.Invoke();

    /// <summary>
    /// Refreshes listing text with the canonical pack version and inheritance requirement.
    /// </summary>
    private void RenderPreview()
    {
        if (_definition == null)
            return;
        string basePack = string.IsNullOrEmpty(_definition.BasePackID)
            ? "Standalone content pack"
            : "Requires " + _definition.BasePackID;
        _preview.text =
            _packageName.text
            + "\n\nVersion "
            + _definition.Version
            + "\n\n"
            + _description.text
            + "\n\n"
            + basePack;
        string dependencies = string.IsNullOrWhiteSpace(_dependencies.text)
            ? "None"
            : _dependencies.text.Trim();
        _review.text =
            "Package\n"
            + _packageName.text
            + "\n\nRelease version\n"
            + _definition.Version
            + "\n\nRequired base pack\n"
            + (string.IsNullOrEmpty(_definition.BasePackID) ? "None" : _definition.BasePackID)
            + "\n\nRequired Thunderstore packages\n"
            + dependencies
            + "\n\nIncludes your pack files, icon, readme, and release notes."
            + "\nContent validation runs when you export.";
    }

    /// <summary>
    /// Releases the preview texture without retaining it between publishing sessions.
    /// </summary>
    private void ReleaseIcon()
    {
        if (_iconTexture == null)
            return;
        if (Application.isPlaying)
            Destroy(_iconTexture);
        else
            DestroyImmediate(_iconTexture);
        _iconTexture = null;
    }
}
