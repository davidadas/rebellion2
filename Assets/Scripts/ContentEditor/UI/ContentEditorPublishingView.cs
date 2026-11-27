using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    private RectTransform _panel;

    [SerializeField]
    private TMP_InputField _packageName;

    [SerializeField]
    private TMP_InputField _description;

    [SerializeField]
    private TMP_InputField _website;

    [SerializeField]
    private TMP_InputField _readme;

    [SerializeField]
    private ContentEditorDependenciesView _dependencies;

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

    [SerializeField]
    private Button _connectionButton;

    [SerializeField]
    private Button _exportButton;

    [SerializeField]
    private TMP_Dropdown _team;

    [SerializeField]
    private GameObject _progressTrack;

    [SerializeField]
    private Image _progress;

    [SerializeField]
    private ContentEditorImageCropView _imageCrop;

    private string[] _teams = Array.Empty<string>();
    private string _preferredTeam;
    private bool _busy;
    private Texture2D _iconTexture;
    private string _iconPath;
    private (string ID, string Version, string BasePackID) _release;
    private bool _bound;
    private int _step;

    internal event Action SaveRequested;
    internal event Action CloseRequested;
    internal event Action BrowseIconRequested;
    internal event Action DependenciesRequested;
    internal event Action ExportRequested;
    internal event Action PublishRequested;
    internal event Action ConnectionRequested;
    internal event Action CancelRequested;
    internal event Action CommunityRequested;
    internal bool IsOpen => gameObject.activeSelf;
    internal ContentEditorDependenciesView Dependencies => _dependencies;
    internal ContentEditorImageCropView ImageCrop => _imageCrop;
    private IEnumerable<TMP_InputField> Fields =>
        new[] { _packageName, _description, _website, _readme, _changelog };

    /// <summary>
    /// Binds the authored publishing controls when their dialog becomes active.
    /// </summary>
    private void Awake() => BindControls();

    /// <summary>
    /// Keeps publishing fields and actions inside a resized window.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnRectTransformDimensionsChange()
    {
        if (_panel != null)
            UILayout.FitPanel(_panel);
    }

    /// <summary>
    /// Connects the dialog's controls once, retaining ownership of the matching cleanup.
    /// </summary>
    private void BindControls()
    {
        if (_bound)
            return;
        if (
            _panel == null
            || _packageName == null
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
            || _connectionButton == null
            || _exportButton == null
            || _team == null
            || _progressTrack == null
            || _progress == null
            || _imageCrop == null
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
        _dependencies.ChooseRequested += RequestDependencies;
        _dependencies.Changed += RenderPreview;
        _communityButton.onClick.AddListener(RequestCommunity);
        _connectionButton.onClick.AddListener(ShowConnection);
        _exportButton.onClick.AddListener(RequestExport);
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
            _dependencies.ChooseRequested -= RequestDependencies;
            _dependencies.Changed -= RenderPreview;
            _communityButton.onClick.RemoveListener(RequestCommunity);
            _connectionButton.onClick.RemoveListener(ShowConnection);
            _exportButton.onClick.RemoveListener(RequestExport);
        }
        ReleaseIcon();
    }

    /// <summary>
    /// Opens the publishing workflow without changing the pack's canonical identity or version.
    /// </summary>
    /// <param name="project">The project whose current snapshot will be released.</param>
    /// <param name="settings">The saved publishing draft.</param>
    internal void Show(ContentPackEditorProject project, ContentEditorPublishingSettings settings)
    {
        BindControls();
        _release = (project.ID, project.Version, project.BasePackID);
        _preferredTeam = settings.TeamName;
        RenderConnection(_teams);
        _packageName.SetTextWithoutNotify(settings.PackageName ?? string.Empty);
        _description.SetTextWithoutNotify(settings.Description ?? string.Empty);
        _website.SetTextWithoutNotify(settings.WebsiteUrl ?? string.Empty);
        _readme.SetTextWithoutNotify(settings.Readme ?? string.Empty);
        _dependencies.Show(settings.GetDependencies());
        _changelog.SetTextWithoutNotify(settings.Changelog ?? string.Empty);
        _version.SetTextWithoutNotify(_release.Version);
        _identity.text = project.DisplayName + (project.Mod == null ? " · Game pack" : " · Mod");
        SetIcon(settings.IconPath, null);
        ShowStatus(string.Empty);
        SetStep(0);
        gameObject.SetActive(true);
        UILayout.FitPanel(_panel);
        transform.SetAsLastSibling();
    }

    /// <summary>
    /// Collects the current listing draft without applying it to runtime content.
    /// </summary>
    /// <returns>The independently editable publishing fields.</returns>
    internal ContentEditorPublishingSettings ReadSettings() =>
        new ContentEditorPublishingSettings
        {
            TeamName = _teams.Length > 0 ? _teams[_team.value] : _preferredTeam ?? string.Empty,
            PackageName = _packageName.text.Trim(),
            Description = _description.text.Trim(),
            WebsiteUrl = _website.text.Trim(),
            Readme = _readme.text,
            Dependencies = string.Join("\n", _dependencies.Read()),
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
        _iconLabel.text =
            bytes == null ? "Choose an image to crop to 256 × 256" : "256 × 256 PNG selected";
        RenderPreview();
    }

    /// <summary>
    /// Frames an imported package icon before its confirmed PNG is passed to project storage.
    /// </summary>
    /// <param name="path">The chosen source image.</param>
    /// <param name="onSubmitted">The confirmed image receiver.</param>
    internal void ShowIconCrop(string path, Action<byte[]> onSubmitted) =>
        _imageCrop.Show(path, new Vector2Int(256, 256), onSubmitted);

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
    internal void Hide()
    {
        _imageCrop.Cancel();
        gameObject.SetActive(false);
    }

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
    private void ShowListing() => RequestStep(0);

    /// <summary>
    /// Shows release notes and declared package dependencies.
    /// </summary>
    private void ShowRelease() => RequestStep(1);

    /// <summary>
    /// Shows the exact release identity and export contents for review.
    /// </summary>
    private void ShowReview() => RequestStep(2);

    /// <summary>
    /// Admits forward navigation only after all earlier required fields are valid.
    /// </summary>
    /// <param name="step">The requested publishing stage.</param>
    private void RequestStep(int step)
    {
        if (_busy || (step > _step && !ValidateBeforeStep(step)))
            return;
        SetStep(step);
        ShowStatus(string.Empty);
    }

    /// <summary>
    /// Keeps invalid input on its owning page and explains the requirement without discarding drafts.
    /// </summary>
    /// <param name="step">The stage or action whose prerequisites must be met.</param>
    /// <returns>Whether the requested forward action can proceed.</returns>
    private bool ValidateBeforeStep(int step)
    {
        ContentEditorPublishingSettings settings = ReadSettings();
        int invalidStep = 0;
        try
        {
            if (step > 0)
            {
                settings.ValidateListing();
                if (_iconTexture == null || _iconTexture.width != 256 || _iconTexture.height != 256)
                    throw new ArgumentException("Choose a package icon before continuing.");
            }
            if (step > 1)
            {
                invalidStep = 1;
                settings.ValidateRelease(_release.ID, _release.Version);
            }
            return true;
        }
        catch (ArgumentException exception)
        {
            SetStep(invalidStep);
            ShowStatus(exception.Message, true);
            return false;
        }
    }

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
            step == 2 ? "Publish release"
            : step == 1 ? "Review release"
            : "Continue to release";
        RenderPreview();
    }

    /// <summary>
    /// Advances the draft or requests a validated package export from the final review.
    /// </summary>
    private void Continue()
    {
        if (_busy)
            return;
        if (_step < 2)
            RequestStep(_step + 1);
        else if (!ValidateBeforeStep(2))
            return;
        else if (_teams.Length == 0)
            ShowConnection();
        else
            PublishRequested?.Invoke();
    }

    /// <summary>
    /// Requests persistence of the listing draft.
    /// </summary>
    private void RequestSave() => SaveRequested?.Invoke();

    /// <summary>
    /// Requests a close after retaining the current publishing draft.
    /// </summary>
    private void RequestClose()
    {
        if (_imageCrop.IsOpen)
        {
            _imageCrop.Cancel();
            return;
        }
        if (_busy)
            CancelRequested?.Invoke();
        else
            CloseRequested?.Invoke();
    }

    /// <summary>
    /// Requests a native file picker for a package icon.
    /// </summary>
    private void RequestIcon() => BrowseIconRequested?.Invoke();

    /// <summary>
    /// Requests package choices without exposing manifest-format text editing.
    /// </summary>
    private void RequestDependencies() => DependenciesRequested?.Invoke();

    /// <summary>
    /// Requests the community page where the author can upload the exported package.
    /// </summary>
    private void RequestCommunity() => CommunityRequested?.Invoke();

    /// <summary>
    /// Refreshes listing text with the canonical pack version and inheritance requirement.
    /// </summary>
    private void RenderPreview()
    {
        if (_release.ID == null)
            return;
        string basePack = string.IsNullOrEmpty(_release.BasePackID)
            ? "Standalone game pack"
            : "Requires " + _release.BasePackID;
        _preview.text =
            _packageName.text
            + "\n\nVersion "
            + _release.Version
            + "\n\n"
            + _description.text
            + "\n\n"
            + basePack;
        string dependencies =
            _dependencies.Read().Length == 0 ? "None" : string.Join("\n", _dependencies.Read());
        _review.text =
            "Package\n"
            + _packageName.text
            + " · "
            + _release.Version
            + "\n\nRequired game pack\n"
            + (string.IsNullOrEmpty(_release.BasePackID) ? "None" : _release.BasePackID)
            + "\n\nRequired Thunderstore packages\n"
            + dependencies
            + "\n\nIncludes your project files, icon, readme, and release notes."
            + "\nContent validation runs before publishing or export.";
    }

    /// <summary>
    /// Presents connected publishing teams without retaining the entered credential in a field.
    /// </summary>
    /// <param name="teams">The active teams authorized by the session.</param>
    internal void RenderConnection(string[] teams)
    {
        BindControls();
        _teams = teams;
        _team.ClearOptions();
        _team.AddOptions(
            teams.Length == 0
                ? new List<string> { "Connect Thunderstore to choose a team" }
                : new List<string>(teams)
        );
        _team.SetValueWithoutNotify(Math.Max(0, Array.IndexOf(teams, _preferredTeam)));
        _team.RefreshShownValue();
        _team.interactable = !_busy && teams.Length > 0;
        _connectionButton.GetComponentInChildren<TMP_Text>().text =
            teams.Length == 0 ? "Connect Thunderstore…" : "Thunderstore connected";
    }

    /// <summary>
    /// Points the release review toward the package being published.
    /// </summary>
    /// <param name="available">Whether the controller has a reviewed package URL.</param>
    internal void SetPublished(bool available) =>
        _communityButton.GetComponentInChildren<TMP_Text>().text = available
            ? "View package page"
            : "Open Thunderstore";

    /// <summary>
    /// Keeps cancellation available while protecting the release fields during a network operation.
    /// </summary>
    /// <param name="busy">Whether publishing owns the draft.</param>
    internal void SetBusy(bool busy)
    {
        _busy = busy;
        foreach (TMP_InputField field in Fields)
            field.interactable = !busy;
        for (int i = 0; i < _stepButtons.Length; i++)
            _stepButtons[i].interactable = !busy && i != _step;
        foreach (
            Button button in new[]
            {
                _nextButton,
                _saveButton,
                _browseButton,
                _connectionButton,
                _exportButton,
            }
        )
            button.interactable = !busy;
        _dependencies.SetBusy(busy);
        _team.interactable = !busy && _teams.Length > 0;
        _closeButton.GetComponentInChildren<TMP_Text>().text = busy ? "Cancel" : "Close";
        _progressTrack.SetActive(busy);
        if (busy)
            _progress.rectTransform.anchorMax = new Vector2(0, 1);
    }

    /// <summary>
    /// Displays streamed upload progress on the owning publishing page.
    /// </summary>
    /// <param name="message">The current publishing phase.</param>
    /// <param name="progress">The completed upload fraction.</param>
    internal void ShowProgress(string message, float progress)
    {
        if (!_busy)
            return;
        _progress.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1);
        ShowStatus(message);
    }

    /// <summary>
    /// Opens the application's shared Thunderstore connection controls.
    /// </summary>
    private void ShowConnection() => ConnectionRequested?.Invoke();

    /// <summary>
    /// Requests a portable archive without publishing it.
    /// </summary>
    private void RequestExport()
    {
        if (!_busy && ValidateBeforeStep(2))
            ExportRequested?.Invoke();
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
