using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Rebellion.Game;
using Rebellion.Util.Logging;
using Rebellion.Util.Serialization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns responsive authored forms, field bindings, artwork and table presentation.
/// </summary>
public sealed class ContentEditorFormView : MonoBehaviour
{
    private const string _defaultFactionArtworkCategory = "Planet sector";

    [SerializeField]
    private GameObject previewPane;

    [SerializeField]
    private RawImage previewImage;

    [SerializeField]
    private Button addMessageButton;

    [SerializeField]
    private TMP_Dropdown collectionDropdown;

    [SerializeField]
    private TMP_Dropdown shipDesignDropdown;

    [SerializeField]
    private Button[] artworkStateSizeButtons;

    private int artworkStateSize;
    private readonly List<int> artworkStateChoices = new();

    private ContentPackEditorRecord[] shipNameDesigns = Array.Empty<ContentPackEditorRecord>();
    private string selectedShipNameDesignID;

    [SerializeField]
    private TMP_Dropdown missionPresentationDropdown;

    [SerializeField]
    private Button missionReportsToggle;

    private string missionPresentationKey;
    private bool showMissionReports;

    internal ContentEditorTreeNode ActiveMissionPage =>
        selectedPage?.IsMissionPresentation == true ? selectedPage
        : section == "Mission presentation"
            ? selectedPage?.MissionPages.FirstOrDefault(page => page.Key == missionPresentationKey)
                ?? selectedPage?.MissionPages.FirstOrDefault()
        : null;

    private bool IsMissionReportSection =>
        section == ContentEditorMissionReport.Section
        || section == "Mission presentation" && showMissionReports;

    [SerializeField]
    private Button removeMessageButton;

    [SerializeField]
    private LineChartGraphic curveGraphic;

    [SerializeField]
    private TMP_Dropdown curveDropdown;

    [SerializeField]
    private TMP_InputField scoreInput;

    [SerializeField]
    private Button addScoreButton;

    [SerializeField]
    private Button removeRowTemplate;

    private readonly List<Button> removeRowButtons = new();
    private int selectedCurve;

    private XElement selectedCollection;
    private string selectedMissionReportKey;
    private XElement[] collectionItems = Array.Empty<XElement>();

    [SerializeField]
    private TextMeshProUGUI groupTemplate;

    [SerializeField]
    private RawImage tableRowTemplate;

    [SerializeField]
    private RawImage articleImageTemplate;

    [SerializeField]
    private ScrollAreaLayout fieldScroll;

    [SerializeField]
    private ContentEditorFieldView fieldTemplate;

    [SerializeField]
    private ContentEditorColorPickerView colorPickerTemplate;

    [SerializeField]
    private RawImage artworkPanelTemplate;

    [SerializeField]
    private UICheckboxView outputBackgroundToggle;

    [SerializeField]
    private UICheckboxView stateBackgroundToggle;

    private readonly List<TextMeshProUGUI> groupLabels = new List<TextMeshProUGUI>();

    private readonly List<ContentEditorFieldView> fieldViews = new List<ContentEditorFieldView>();
    private readonly Dictionary<XElement, int> factionArtworkFieldTops = new();

    private readonly List<ContentEditorColorPickerView> colorPickers = new();

    private readonly List<ContentEditorCardView> artworkPanels = new();

    private ContentEditorCardView activeArtworkPanel;
    private int activeArtworkPanelTop;
    private const int _artworkPanelLeft = 4;

    private bool reflowingFields;
    private bool showOutputArtworkBackground = true;
    private bool showStateArtworkBackground = true;

    private ContentEditorNumericGrid probabilityGrid;

    private readonly List<RawImage> articleImages = new List<RawImage>();

    private readonly List<RawImage> tableRows = new List<RawImage>();

    private ContentPackEditorProject project;
    private ContentAssets assets;
    private ContentPackEditorRecord selectedRecord;
    private ContentEditorTreeNode selectedPage;
    private IReadOnlyList<ContentPackEditorCatalog> catalogs;
    private string section;
    private bool bound;

    [SerializeField]
    private TMP_Dropdown generationTask;

    [SerializeField]
    private CollectionEditorView setupCollectionTemplate;

    [SerializeField]
    private Button setupUnitsTemplate;

    [SerializeField]
    private TMP_Dropdown artworkViewSelector;

    internal IReadOnlyList<string> ArtworkViewOptions { get; private set; } = Array.Empty<string>();

    internal string ArtworkViewSelection => selectedFactionArtworkCategory;

    [SerializeField]
    private ContentEditorSectorPreview artworkSectorPreview;

    [SerializeField]
    private ContentEditorArtworkWindowPreview artworkWindowPreview;

    [SerializeField]
    private ContentEditorArtworkWindowPreview artworkDisabledWindowPreview;

    private readonly List<CollectionEditorView> setupCollections = new();
    private readonly List<Button> setupUnitButtons = new();
    private Button artworkCutsButton;
    private readonly Dictionary<string, int> setupTasks = new();
    private readonly Dictionary<string, int> setupEntries = new();
    private string selectedFactionArtworkCategory = _defaultFactionArtworkCategory;
    public event Action<ContentPackEditorField> SelectionRequested;
    public event Action<ContentPackEditorCollection> UnitSelectionRequested;
    public event Action<ContentPackEditorCollection> CollectionAddRequested;
    public event Action<ContentPackEditorCollection, int> CollectionRemoveRequested;

    public event Action Changed;
    public event Action PresentationChanged;
    public event Action<ContentPackEditorField, IReadOnlyList<string>> FieldSubmitted;
    public event Action<ContentPackEditorField> FieldValidationRequested;
    public event Action WidthChanged;
    public event Action<Button> AddMessageRequested;
    internal event Action<
        ContentPackEditorRecord,
        ContentEditorMissionReport,
        bool
    > MissionReportOverrideRequested;
    public event Action AddNamePoolRequested;
    public event Action<ContentPackEditorField> RemoveMessageRequested;
    public event Action<IReadOnlyList<ContentPackEditorField>> ClearFieldsRequested;
    public event Action<ContentPackEditorField> ImportRequested;
    public event Action<
        ContentPackEditorField,
        IReadOnlyList<ContentPackEditorField>
    > ArtworkCutsRequested;
    internal event Action<
        ContentPackEditorField,
        ContentEditorArtworkEffect,
        bool
    > ArtworkEffectRequested;
    public event Action<ContentPackEditorField, string> PreviewRequested;
    public event Action<ContentPackEditorField> AssetEditRequested;
    public event Action<string> SectionRequested;
    public event Action<
        IReadOnlyList<ContentEditorArtworkPlacementChange>
    > ArtworkPlacementSubmitted;
    public event Action<string> ValidationFailed;
    public bool HasOpenChoice =>
        generationTask.IsExpanded
        || artworkViewSelector.IsExpanded
        || setupCollections.Any(view => view.gameObject.activeSelf && view.HasOpenChoice)
        || collectionDropdown.IsExpanded
        || shipDesignDropdown.IsExpanded
        || missionPresentationDropdown.IsExpanded
        || curveDropdown.IsExpanded
        || fieldViews.Any(view => view.gameObject.activeSelf && view.HasOpenChoice);
    public TextInputHistory InvalidTextHistory =>
        fieldViews
            .FirstOrDefault(view => view.gameObject.activeSelf && view.HasRejectedDraft)
            ?.GetComponentInChildren<TMP_InputField>()
            ?.GetComponent<TextInputHistory>();

    /// <summary>
    /// Connects the authored form controls before displaying any fields.
    /// </summary>
    private void Awake() => BindControls();

    /// <summary>
    /// Verifies templates and binds reusable form-level commands once.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;
        if (
            generationTask == null
            || setupCollectionTemplate == null
            || setupUnitsTemplate == null
            || artworkViewSelector == null
            || artworkSectorPreview == null
            || artworkWindowPreview == null
            || artworkDisabledWindowPreview == null
            || previewPane == null
            || previewImage == null
            || addMessageButton == null
            || collectionDropdown == null
            || shipDesignDropdown == null
            || artworkStateSizeButtons == null
            || artworkStateSizeButtons.Length != 3
            || missionPresentationDropdown == null
            || missionReportsToggle == null
            || removeMessageButton == null
            || curveGraphic == null
            || curveDropdown == null
            || scoreInput == null
            || addScoreButton == null
            || removeRowTemplate == null
            || groupTemplate == null
            || tableRowTemplate == null
            || articleImageTemplate == null
            || fieldScroll == null
            || fieldTemplate == null
            || colorPickerTemplate == null
            || artworkPanelTemplate == null
            || outputBackgroundToggle == null
            || stateBackgroundToggle == null
        )
            throw new MissingReferenceException(name + " has incomplete form references.");
        bound = true;
        generationTask.onValueChanged.AddListener(SelectGenerationTask);
        fieldScroll.WidthChanged += HandleWidthChanged;
        addMessageButton.onClick.AddListener(RequestAddMessage);
        collectionDropdown.onValueChanged.AddListener(SelectCollectionItem);
        shipDesignDropdown.onValueChanged.AddListener(SelectShipNameDesign);
        for (int i = 0; i < artworkStateSizeButtons.Length; i++)
        {
            int size = i;
            artworkStateSizeButtons[i].onClick.AddListener(() => SelectArtworkStateSize(size));
        }
        missionPresentationDropdown.onValueChanged.AddListener(SelectMissionPresentation);
        missionReportsToggle.onClick.AddListener(ToggleMissionReports);
        removeMessageButton.onClick.AddListener(RequestRemoveMessage);
        curveDropdown.onValueChanged.AddListener(SelectCurve);
        addScoreButton.onClick.AddListener(AddScore);
        artworkViewSelector.onValueChanged.AddListener(SelectFactionArtworkCategory);
        artworkSectorPreview.ArtworkOpened += OpenWindowArtwork;
        artworkWindowPreview.ArtworkOpened += OpenWindowArtwork;
        artworkWindowPreview.ArtworkMoved += MoveWindowArtwork;
        artworkDisabledWindowPreview.ArtworkOpened += OpenWindowArtwork;
        artworkDisabledWindowPreview.ArtworkMoved += MoveWindowArtwork;
        outputBackgroundToggle
            .GetComponent<Toggle>()
            .onValueChanged.AddListener(SetOutputArtworkBackgroundVisible);
        stateBackgroundToggle
            .GetComponent<Toggle>()
            .onValueChanged.AddListener(SetStateArtworkBackgroundVisible);
    }

    /// <summary>
    /// Releases authored-control and pooled-field subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;
        generationTask.onValueChanged.RemoveListener(SelectGenerationTask);
        fieldScroll.WidthChanged -= HandleWidthChanged;
        addMessageButton.onClick.RemoveListener(RequestAddMessage);
        collectionDropdown.onValueChanged.RemoveListener(SelectCollectionItem);
        shipDesignDropdown.onValueChanged.RemoveListener(SelectShipNameDesign);
        foreach (Button button in artworkStateSizeButtons)
            button.onClick.RemoveAllListeners();
        missionPresentationDropdown.onValueChanged.RemoveListener(SelectMissionPresentation);
        missionReportsToggle.onClick.RemoveListener(ToggleMissionReports);
        removeMessageButton.onClick.RemoveListener(RequestRemoveMessage);
        curveDropdown.onValueChanged.RemoveListener(SelectCurve);
        addScoreButton.onClick.RemoveListener(AddScore);
        artworkViewSelector.onValueChanged.RemoveListener(SelectFactionArtworkCategory);
        artworkSectorPreview.ArtworkOpened -= OpenWindowArtwork;
        artworkWindowPreview.ArtworkOpened -= OpenWindowArtwork;
        artworkWindowPreview.ArtworkMoved -= MoveWindowArtwork;
        artworkDisabledWindowPreview.ArtworkOpened -= OpenWindowArtwork;
        artworkDisabledWindowPreview.ArtworkMoved -= MoveWindowArtwork;
        outputBackgroundToggle
            .GetComponent<Toggle>()
            .onValueChanged.RemoveListener(SetOutputArtworkBackgroundVisible);
        stateBackgroundToggle
            .GetComponent<Toggle>()
            .onValueChanged.RemoveListener(SetStateArtworkBackgroundVisible);
        foreach (ContentEditorFieldView field in fieldViews)
        {
            field.ValidationFailed -= ReportValidationError;
            field.Submitted -= SubmitField;
            field.ValidationRequested -= ValidateField;
            field.SelectionRequested -= RequestSelection;
        }
    }

    /// <summary>
    /// Displays one logical page using the same pooled controls for every record.
    /// </summary>
    /// <param name="value">The project owning the fields.</param>
    /// <param name="contentAssets">Its resolved media assets.</param>
    /// <param name="record">The selected definition.</param>
    /// <param name="page">The combined logical page, when applicable.</param>
    /// <param name="sectionName">The selected inspector tab.</param>
    /// <param name="rebind">Whether to replace field bindings or retain uncommitted drafts.</param>
    public void Render(
        ContentPackEditorProject value,
        ContentAssets contentAssets,
        ContentPackEditorRecord record,
        ContentEditorTreeNode page,
        string sectionName,
        bool rebind
    )
    {
        BindControls();
        if (record != selectedRecord)
        {
            showOutputArtworkBackground = true;
            showStateArtworkBackground = true;
            artworkStateSize = 0;
        }
        project = value;
        assets = contentAssets;
        selectedRecord = record;
        selectedPage = page;
        catalogs = value.GetCatalogs();
        section = sectionName;
        RenderFields(rebind);
    }

    /// <summary>
    /// Commits visible drafts and reports whether the form can be left safely.
    /// </summary>
    /// <returns>True when every pending field edit has been accepted.</returns>
    public bool Commit()
    {
        foreach (
            ContentEditorFieldView field in fieldViews.Where(field => field.gameObject.activeSelf)
        )
            field.Commit();
        return fieldViews.All(field => !field.gameObject.activeSelf || !field.HasRejectedDraft);
    }

    /// <summary>
    /// Collects the authored overrides currently displayed by this inspector section.
    /// </summary>
    /// <returns>The distinct inheritable fields with local values in the active section.</returns>
    public IReadOnlyList<ContentPackEditorField> GetOverriddenFields()
    {
        IEnumerable<ContentPackEditorField> fields = fieldViews
            .Where(view => view.gameObject.activeSelf && view.Field != null)
            .Select(view => view.Field);
        if (probabilityGrid != null)
            fields = fields
                .Where(field => !probabilityGrid.ContainsTableField(field))
                .Concat(probabilityGrid.ResetFields);
        return fields
            .Concat(
                colorPickers
                    .Where(picker => picker.gameObject.activeSelf)
                    .SelectMany(picker => new[] { picker.Field, picker.LinkField })
            )
            .Where(field => field?.CanResetToDefault == true)
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Checks accepted values before saving without blocking navigation through unfinished content.
    /// </summary>
    /// <returns>Whether every visible authored value is valid.</returns>
    public bool Validate()
    {
        if (!Commit())
            return false;
        foreach (
            ContentEditorFieldView field in fieldViews.Where(field => field.gameObject.activeSelf)
        )
            field.ValidateCurrent();
        return fieldViews.All(field => !field.gameObject.activeSelf || !field.HasError);
    }

    /// <summary>
    /// Releases project bindings before hiding or replacing a project.
    /// </summary>
    public void Clear()
    {
        ClearSetupControls();
        ClearArtworkViewControls();
        foreach (ContentEditorFieldView field in fieldViews)
            field.Clear();
        foreach (TextMeshProUGUI label in groupLabels)
            label.gameObject.SetActive(false);
        foreach (RawImage image in articleImages)
        {
            image.texture = null;
            image.gameObject.SetActive(false);
        }
        foreach (RawImage row in tableRows)
            row.gameObject.SetActive(false);
        foreach (ContentEditorColorPickerView picker in colorPickers)
            picker.Clear();
        foreach (ContentEditorCardView panel in artworkPanels)
            panel.gameObject.SetActive(false);
        outputBackgroundToggle.gameObject.SetActive(false);
        stateBackgroundToggle.gameObject.SetActive(false);
        activeArtworkPanel = null;
        HidePreview();
        addMessageButton.gameObject.SetActive(false);
        collectionDropdown.gameObject.SetActive(false);
        shipDesignDropdown.gameObject.SetActive(false);
        foreach (Button button in artworkStateSizeButtons)
            button.gameObject.SetActive(false);
        missionPresentationDropdown.gameObject.SetActive(false);
        missionReportsToggle.gameObject.SetActive(false);
        removeMessageButton.gameObject.SetActive(false);
        collectionItems = Array.Empty<XElement>();
        selectedCollection = null;
        curveGraphic.gameObject.SetActive(false);
        curveDropdown.gameObject.SetActive(false);
        scoreInput.gameObject.SetActive(false);
        addScoreButton.gameObject.SetActive(false);
        foreach (Button button in removeRowButtons)
        {
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(false);
        }
        project = null;
        assets = null;
        selectedRecord = null;
        selectedPage = null;
        catalogs = Array.Empty<ContentPackEditorCatalog>();
        probabilityGrid = null;
    }

    /// <summary>
    /// Hides the main portrait without retaining its content texture.
    /// </summary>
    public void HidePreview()
    {
        previewImage.texture = null;
        previewPane.SetActive(false);
    }

    /// <summary>
    /// Reveals and focuses a field after a structural creation operation.
    /// </summary>
    /// <param name="field">The field to edit.</param>
    /// <param name="validationError">The current-value failure to display beside the field.</param>
    /// <returns>Whether the field has a visible control in the current form.</returns>
    public bool FocusField(ContentPackEditorField field, string validationError = null)
    {
        if (IsMissionReportSection)
        {
            ContentEditorMissionReport report = ActiveMissionPage.MissionReports.FirstOrDefault(
                report => report.Fields.Any(candidate => SameField(candidate, field))
            );
            if (report != null && report.Key != selectedMissionReportKey)
            {
                selectedMissionReportKey = report.Key;
                RenderFields(true);
            }
        }
        XElement item = field
            .XmlElement.AncestorsAndSelf()
            .FirstOrDefault(element => element.Name.LocalName is "Message" or "NamePool");
        if (item != null && selectedCollection != item)
        {
            selectedCollection = item;
            RenderFields(true);
        }
        ContentEditorFieldView row = fieldViews.SingleOrDefault(item =>
            item.gameObject.activeSelf
            && item.Field.XmlElement == field.XmlElement
            && item.Field.XmlAttributeName == field.XmlAttributeName
        );
        if (row == null)
            return false;
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            row.ShowValidationError(validationError);
            RenderFields(false);
        }
        RectTransform rect = (RectTransform)row.transform;
        float top = factionArtworkFieldTops.TryGetValue(field.XmlElement, out int artworkTop)
            ? artworkTop
            : -rect.anchoredPosition.y;
        fieldScroll.ScrollArea.AlignContentTop(top);
        row.Focus();
        return true;
    }

    /// <summary>
    /// Reveals the dedicated faction-color card representing an authored mask color.
    /// </summary>
    /// <param name="field">The color field to reveal.</param>
    private void FocusColorPicker(ContentPackEditorField field)
    {
        ContentEditorColorPickerView picker = colorPickers.SingleOrDefault(item =>
            item.gameObject.activeSelf && SameFactionColor(item.Field, field)
        );
        if (picker == null)
            return;
        RectTransform rect = (RectTransform)picker.transform;
        fieldScroll.ScrollArea.AlignContentTop(-rect.anchoredPosition.y);
        TMP_InputField input = picker.GetComponentInChildren<TMP_InputField>();
        EventSystem.current?.SetSelectedGameObject(input.gameObject);
        input.ActivateInputField();
    }

    /// <summary>
    /// Requests responsive layout after the form's viewport changes.
    /// </summary>
    private void HandleWidthChanged() => WidthChanged?.Invoke();

    /// <summary>
    /// Requests a starting-message category menu at its authored control.
    /// </summary>
    private void RequestAddMessage()
    {
        if (IsMissionReportSection)
        {
            ContentEditorMissionReport report = ActiveMissionPage.MissionReports.First(report =>
                report.Key == selectedMissionReportKey
            );
            if (report.Shared != null && Commit())
                MissionReportOverrideRequested?.Invoke(
                    ActiveMissionPage.RelatedRecords.Single(),
                    report,
                    report.Custom == null
                );
        }
        else if (section == "Ship names")
            AddNamePoolRequested?.Invoke();
        else
            AddMessageRequested?.Invoke(addMessageButton);
    }

    /// <summary>
    /// Opens a selected nested entry after accepting the current form's edits.
    /// </summary>
    /// <param name="index">The entry selected in the standard dropdown.</param>
    private void SelectCollectionItem(int index)
    {
        if (IsMissionReportSection)
        {
            IReadOnlyList<ContentEditorMissionReport> reports = ActiveMissionPage.MissionReports;
            if (!Commit())
            {
                collectionDropdown.SetValueWithoutNotify(
                    Array.FindIndex(
                        reports.ToArray(),
                        report => report.Key == selectedMissionReportKey
                    )
                );
                return;
            }
            selectedMissionReportKey = reports[index].Key;
            RenderFields(true);
            return;
        }
        if (!Commit())
        {
            collectionDropdown.SetValueWithoutNotify(
                Array.IndexOf(collectionItems, selectedCollection)
            );
            return;
        }
        selectedCollection = collectionItems[index];
        RenderFields(true);
    }

    /// <summary>
    /// Requests deletion of the currently displayed starting message.
    /// </summary>
    private void RequestRemoveMessage()
    {
        ContentPackEditorField title = selectedPage.Fields.First(field =>
            field.MemberName == "Title" && field.XmlElement.Parent == selectedCollection
        );
        RemoveMessageRequested?.Invoke(title);
    }

    /// <summary>
    /// Selects a probability column for the plot without discarding current field drafts.
    /// </summary>
    /// <param name="index">The selected curve.</param>
    private void SelectCurve(int index)
    {
        selectedCurve = index;
        RenderFields(false);
    }

    /// <summary>
    /// Adds one pending threshold through the grid's ordinary detached field bindings.
    /// </summary>
    private void AddScore()
    {
        if (
            !int.TryParse(
                scoreInput.text,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int score
            )
        )
        {
            ValidationFailed?.Invoke("Enter a whole-number score.");
            return;
        }
        if (!Commit())
            return;
        ContentEditorNumericGrid grid = probabilityGrid;
        if (!grid.AddScore(score))
        {
            ValidationFailed?.Invoke("That score already exists.");
            return;
        }
        foreach (ContentEditorFieldView field in fieldViews)
            field.Clear();
        reflowingFields = false;
        foreach (TextMeshProUGUI label in groupLabels)
            label.gameObject.SetActive(false);
        RenderRules(selectedPage.Fields.ToArray());
        FocusField(grid.GetCell(score, 0));
    }

    /// <summary>
    /// Draws one exact stepwise probability curve beside or above its editable table.
    /// </summary>
    /// <param name="grid">The current authored probability tables.</param>
    /// <param name="x">The chart's left edge.</param>
    /// <param name="y">The chart's top edge.</param>
    /// <param name="width">The chart's available width.</param>
    /// <param name="labelIndex">The next reusable caption.</param>
    /// <returns>The occupied height.</returns>
    private int RenderCurve(
        ContentEditorNumericGrid grid,
        float x,
        float y,
        float width,
        ref int labelIndex
    )
    {
        selectedCurve = Mathf.Clamp(selectedCurve, 0, grid.Columns.Count - 1);
        if (grid.Columns.Count > 1)
        {
            curveDropdown.gameObject.SetActive(true);
            curveDropdown.ClearOptions();
            curveDropdown.AddOptions(grid.Columns.ToList());
            curveDropdown.SetValueWithoutNotify(selectedCurve);
            UILayout.SetSourceRect(
                (RectTransform)curveDropdown.transform,
                Mathf.RoundToInt(x + 44),
                Mathf.RoundToInt(y),
                Mathf.RoundToInt(width - 48),
                32
            );
        }
        else
            PlaceLabel(grid.Columns[0] + " (%)", labelIndex++, x + 44, y, width - 48, false);
        IReadOnlyList<KeyValuePair<int, int>> entries = grid.GetEntries(selectedCurve);
        if (entries.Count == 0)
            return 40
                + PlaceLabel(
                    "Fill a cell to preview this curve.",
                    labelIndex++,
                    x + 44,
                    y + 40,
                    width - 48,
                    false
                );
        int minimum = grid.Scores.First();
        int maximum = grid.Scores.Last();
        float low = Mathf.Min(0, entries.Min(entry => entry.Value));
        float high = Mathf.Max(100, entries.Max(entry => entry.Value));
        List<Vector2> points = new() { new Vector2(0, (entries[0].Value - low) / (high - low)) };
        float previous = points[0].y;
        foreach (var entry in entries)
        {
            float pointX =
                maximum == minimum
                    ? 0
                    : (float)(((double)entry.Key - minimum) / ((double)maximum - minimum));
            float pointY = (entry.Value - low) / (high - low);
            points.Add(new Vector2(pointX, previous));
            points.Add(new Vector2(pointX, pointY));
            previous = pointY;
        }
        points.Add(new Vector2(1, previous));
        UILayout.SetSourceRect(
            (RectTransform)curveGraphic.transform,
            Mathf.RoundToInt(x + 44),
            Mathf.RoundToInt(y + 44),
            Mathf.RoundToInt(width - 52),
            180
        );
        curveGraphic.gameObject.SetActive(true);
        curveGraphic.Render(points);
        PlaceLabel(
            high.ToString("0", CultureInfo.InvariantCulture),
            labelIndex++,
            x,
            y + 38,
            40,
            false
        );
        PlaceLabel(
            low.ToString("0", CultureInfo.InvariantCulture),
            labelIndex++,
            x,
            y + 208,
            40,
            false
        );
        PlaceLabel(
            minimum.ToString(CultureInfo.InvariantCulture),
            labelIndex++,
            x + 44,
            y + 232,
            70,
            false
        );
        PlaceLabel(
            maximum.ToString(CultureInfo.InvariantCulture),
            labelIndex++,
            x + width - 70,
            y + 232,
            70,
            false
        );
        return 272;
    }

    /// <summary>
    /// Publishes a committed field without owning workspace selection.
    /// </summary>
    private void HandleFieldChanged() => Changed?.Invoke();

    /// <summary>
    /// Requests application-owned file selection for a media field.
    /// </summary>
    /// <param name="field">The target field.</param>
    private void RequestImport(ContentPackEditorField field) => ImportRequested?.Invoke(field);

    /// <summary>
    /// Requests application-owned asynchronous media loading.
    /// </summary>
    /// <param name="field">The media field.</param>
    /// <param name="address">The selected asset.</param>
    private void RequestPreview(ContentPackEditorField field, string address) =>
        PreviewRequested?.Invoke(field, address);

    /// <summary>
    /// Publishes a field error without owning workspace notifications.
    /// </summary>
    /// <param name="message">The validation message.</param>
    private void ReportValidationError(string message) => ValidationFailed?.Invoke(message);

    /// <summary>
    /// Reuses typed form rows for the current section.
    /// </summary>
    /// <param name="rebind">Whether to bind new values instead of only reflowing existing controls.</param>
    private void RenderFields(bool rebind)
    {
        ClearSetupControls();
        ClearArtworkViewControls();
        addMessageButton.gameObject.SetActive(false);
        collectionDropdown.gameObject.SetActive(false);
        shipDesignDropdown.gameObject.SetActive(false);
        foreach (Button button in artworkStateSizeButtons)
            button.gameObject.SetActive(false);
        missionPresentationDropdown.gameObject.SetActive(false);
        missionReportsToggle.gameObject.SetActive(false);
        removeMessageButton.gameObject.SetActive(false);
        curveGraphic.gameObject.SetActive(false);
        curveDropdown.gameObject.SetActive(false);
        scoreInput.gameObject.SetActive(false);
        addScoreButton.gameObject.SetActive(false);
        foreach (Button button in removeRowButtons)
            button.gameObject.SetActive(false);
        foreach (TextMeshProUGUI group in groupLabels)
            group.gameObject.SetActive(false);
        foreach (ContentEditorColorPickerView picker in colorPickers)
            picker.Clear();
        foreach (RawImage image in articleImages)
            image.gameObject.SetActive(false);
        foreach (RawImage row in tableRows)
            row.gameObject.SetActive(false);
        foreach (ContentEditorCardView panel in artworkPanels)
            panel.gameObject.SetActive(false);
        outputBackgroundToggle.gameObject.SetActive(false);
        stateBackgroundToggle.gameObject.SetActive(false);
        activeArtworkPanel = null;
        if (selectedRecord?.Kind == "GameEvent")
            return;
        if (rebind)
            fieldScroll.SetContentHeight(0);
        reflowingFields = !rebind;
        if (rebind)
        {
            probabilityGrid = null;
            foreach (ContentEditorFieldView field in fieldViews)
                field.Clear();
        }
        if (selectedRecord == null || section == "Advanced")
            return;
        if (selectedRecord.Kind == "PlanetSectors")
        {
            int height = PlaceLabel(
                "Select a sector to edit its properties.",
                0,
                4,
                0,
                fieldScroll.ScrollArea.ViewportWidth - 8,
                false
            );
            fieldScroll.SetContentHeight(height, rebind);
            return;
        }
        if (
            selectedPage?.IsMissionPresentation == true
            && section == ContentEditorMissionReport.Section
        )
        {
            RenderMissionReportText();
            return;
        }
        if (section == "Mission presentation" && ActiveMissionPage != null)
        {
            RenderMissionPresentationTab();
            return;
        }
        if (selectedPage?.IsMissionPresentation == true && section == "General")
        {
            RenderFactionMissionPresentation();
            return;
        }
        if (selectedPage?.Key.StartsWith("factions/", StringComparison.Ordinal) == true)
        {
            if (section == "Interfaces")
                RenderFactionArtwork();
            else if (section == "Colors")
                RenderFactionColors();
            else if (section == "Default backgrounds")
                RenderFactionDefaultBackgrounds();
            else
                RenderCombinedPage();
            return;
        }
        if (selectedRecord.Kind == "GameGenerationConfig")
        {
            RenderGenerationSettings();
            return;
        }
        IReadOnlyList<ContentPackEditorField> pageFields =
            section == "Encyclopedia"
                ? ContentEditorContentSections.EncyclopediaFields(selectedRecord, catalogs)
                : selectedPage?.Fields ?? ContentEditorInspectorLayout.GetFields(selectedRecord);
        bool unit = ContentEditorUnitFactory.CanCreate(selectedRecord.Kind);
        ContentPackEditorField[] fields = pageFields.Where(IsInCurrentSection).ToArray();
        if (selectedPage?.MissionID != null && section == "Rules")
        {
            RenderMissionRules();
            return;
        }
        if (selectedRecord.Kind == "GameConfig" && section == "General")
        {
            RenderRules(fields);
            return;
        }
        if (
            (selectedPage?.RelatedRecords.Count > 0 || selectedPage?.MissionID != null)
            && section == "General"
        )
        {
            RenderCombinedPage();
            return;
        }
        previewPane.SetActive(false);
        if (unit && section == "General")
        {
            RenderUnitFields(fields.OrderBy(ContentEditorInspectorLayout.GetOrder).ToArray());
            return;
        }
        if (unit && section == "Artwork")
        {
            RenderArtworkFields(
                pageFields
                    .Where(field =>
                        field.MediaType == ContentPackEditorMediaType.Image
                        && !field.IsMediaDirectory
                        && ContentEditorUnitArtworkLayout.SupportsMember(
                            selectedRecord,
                            field.MemberName
                        )
                    )
                    .OrderBy(field =>
                        ContentEditorUnitArtworkLayout.GetOrder(
                            selectedRecord.Kind,
                            field.MemberName
                        )
                    )
                    .ToArray()
            );
            return;
        }
        string imageAddress =
            section == "Encyclopedia"
                ? fields
                    .FirstOrDefault(field => field.MediaType == ContentPackEditorMediaType.Image)
                    ?.Value
                : selectedRecord.ImageAddress;
        int y =
            section != "Audio" && (!selectedRecord.Catalog.IsMap || section == "Encyclopedia")
                ? RenderPreview(
                    imageAddress,
                    Mathf.Min(440, fieldScroll.ScrollArea.ViewportWidth - 8)
                )
                : 0;
        int fieldIndex = 0,
            groupIndex = 0,
            imageIndex = 0;
        IReadOnlyList<string> previousSections = Array.Empty<string>();
        foreach (ContentPackEditorField field in fields)
        {
            float viewportWidth = fieldScroll.ScrollArea.ViewportWidth;
            y = Mathf.CeilToInt(
                PlaceSections(
                    field.SectionPath,
                    previousSections,
                    ref groupIndex,
                    4,
                    y,
                    viewportWidth - 8
                )
            );
            previousSections = field.SectionPath;
            float inset = Mathf.Min(field.SectionPath.Count, 4) * 16;
            float width = viewportWidth - inset;
            bool narrowMedia = field.IsMedia && width < 600;
            bool stacked =
                narrowMedia
                || width < 340 && !field.IsNumber && !field.IsBoolean && field.Choices.Count == 0;
            if (stacked)
            {
                y += PlaceLabel(
                    field.Caption,
                    groupIndex++,
                    inset + 4,
                    y,
                    width - 8,
                    false,
                    field.HelpText
                );
            }
            if (section is "Artwork" or "Default backgrounds")
                y = PlaceArtworkImagePreview(field, y, width, ref imageIndex, ref groupIndex);
            y += PlaceField(
                field,
                fieldIndex++,
                inset,
                y,
                width,
                stacked ? 0
                    : selectedRecord.Catalog.IsMap ? 128
                    : Mathf.Min(244, width * 0.45f),
                narrowMedia ? 120 : 0
            );
        }
        fieldScroll.SetContentHeight(y, rebind);
    }

    /// <summary>
    /// Fits a preview into an explicit slot and reserves the same height in the scrolling form.
    /// </summary>
    /// <param name="address">The image used by the current tab.</param>
    /// <param name="width">The available column width.</param>
    /// <param name="maximumHeight">The largest useful image height for this presentation.</param>
    /// <returns>The vertical space occupied by the preview and its trailing margin.</returns>
    private int RenderPreview(string address, float width, float maximumHeight = 280)
    {
        Texture2D texture = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(address))
                texture = assets.GetTexture(address);
        }
        catch (ArgumentException)
        {
            ValidationFailed?.Invoke("Image path is invalid. Use Import to choose an image.");
        }
        previewImage.texture = texture;
        previewPane.SetActive(texture != null);
        if (texture == null)
            return 0;
        int height = Mathf.CeilToInt(
            Mathf.Min(maximumHeight, width * texture.height / texture.width)
        );
        UILayout.SetSourceRect(
            (RectTransform)previewPane.transform,
            4,
            0,
            Mathf.FloorToInt(width - 8),
            height
        );
        UILayout.SetSourceRect(
            (RectTransform)previewImage.transform.parent,
            0,
            0,
            Mathf.FloorToInt(width - 8),
            height
        );
        previewImage.enabled = true;
        previewImage.GetComponent<AspectRatioFitter>().aspectRatio =
            (float)texture.width / texture.height;
        return height + 20;
    }

    /// <summary>
    /// Selects the current page's form fields without exposing the same property in several tabs.
    /// </summary>
    /// <param name="field">The authored field to classify.</param>
    /// <returns>Whether the field belongs in the active inspector tab.</returns>
    private bool IsInCurrentSection(ContentPackEditorField field)
    {
        if (field.Record.Kind == "ContentPack" && field.Group == "Preloads")
            return false;
        if (selectedRecord.Kind == "PlanetSector" && field.Group == "Planets")
            return false;
        if (section == "Encyclopedia")
            return true;
        if (
            ContentEditorContentSections.HasEncyclopedia(selectedRecord)
            && ContentEditorContentSections.IsEncyclopediaField(field)
        )
            return false;
        if (
            selectedRecord.Kind == "GameConfig"
            && selectedPage == null
            && ContentPackEditorFieldMetadata.IsMissionField(field)
        )
            return false;
        return section switch
        {
            "Audio" => field.MediaType == ContentPackEditorMediaType.Audio,
            "Artwork" => field.IsMedia && field.MediaType != ContentPackEditorMediaType.Audio,
            _ => !field.IsMedia,
        };
    }

    /// <summary>
    /// Presents a unit's current output images, effective backgrounds, and state-specific overrides.
    /// </summary>
    /// <param name="fields">The unit's editable artwork fields.</param>
    private void RenderArtworkFields(ContentPackEditorField[] fields)
    {
        float width = fieldScroll.ScrollArea.ViewportWidth;
        int y = 6;
        int fieldIndex = 0,
            labelIndex = 0,
            imageIndex = 0,
            panelIndex = 0;
        ContentPackEditorField backgroundField = fields.FirstOrDefault(field =>
            field.MemberName == ContentEditorUnitArtworkLayout.BackgroundMember
        );
        Texture2D background = ResolveEffectiveUnitBackground(backgroundField);
        Texture2D compactBackground = ResolveUnitCompactBackground();
        Texture2D travelBackground = selectedRecord.Kind
            is "Officer"
                or "SpecialForce"
                or "Regiment"
            ? ResolveUnitCompactBackground(inTransit: true)
            : null;
        ContentPackEditorField[] normal = fields
            .Where(field =>
                field != backgroundField
                && !ContentEditorUnitArtworkLayout.IsStateSpecific(field.MemberName)
            )
            .ToArray();
        ContentPackEditorField[] states = fields
            .Where(field => ContentEditorUnitArtworkLayout.IsStateSpecific(field.MemberName))
            .ToArray();

        if (normal.Length > 0)
        {
            int panelStart = y;
            y = BeginArtworkPanel(ref panelIndex, "Images", y, width);
            y = PlaceArtworkSectionHeader(
                "Images",
                background != null || compactBackground != null ? outputBackgroundToggle : null,
                showOutputArtworkBackground,
                y,
                width,
                ref labelIndex
            );
            y = PlaceArtworkCutsAction(normal, y, width);
            y = PlaceArtworkOutputGallery(
                normal,
                showOutputArtworkBackground ? background : null,
                showOutputArtworkBackground ? compactBackground : null,
                y,
                width,
                ref fieldIndex,
                ref imageIndex,
                ref labelIndex
            );
            y = CompleteArtworkPanel(panelStart, y);
        }

        if (states.Length > 0)
        {
            int panelStart = y;
            y = BeginArtworkPanel(ref panelIndex, "GameplayStates", y, width);
            y = PlaceArtworkSectionHeader(
                "Gameplay states",
                compactBackground != null
                || travelBackground != null
                || background != null && selectedRecord.Kind is "CapitalShip" or "Starfighter"
                    ? stateBackgroundToggle
                    : null,
                showStateArtworkBackground,
                y,
                width,
                ref labelIndex
            );
            y = PlaceArtworkStateSize(states, y, width);
            y = PlaceArtworkStateGallery(
                normal,
                states,
                showStateArtworkBackground ? background : null,
                showStateArtworkBackground ? compactBackground : null,
                showStateArtworkBackground ? travelBackground : null,
                y,
                width,
                ref fieldIndex,
                ref imageIndex,
                ref labelIndex
            );
            y = CompleteArtworkPanel(panelStart, y);
        }

        if (backgroundField != null)
        {
            int panelStart = y;
            y = BeginArtworkPanel(ref panelIndex, "Background", y, width);
            GetArtworkContentBounds(width, out float left, out float availableWidth);
            float backgroundWidth = Mathf.Min(232, availableWidth);
            y += PlaceLabel("Background", labelIndex++, left, y, availableWidth, true) + 6;
            if (selectedRecord.Kind == "Officer")
                y +=
                    PlaceLabel(
                        "Visible through transparent message and encyclopedia artwork.",
                        labelIndex++,
                        left,
                        y,
                        availableWidth,
                        false
                    ) + 6;
            if (background != null)
                y = PlaceEffectiveBackgroundPreview(
                    backgroundField,
                    background,
                    left,
                    y,
                    backgroundWidth,
                    ref imageIndex,
                    ref labelIndex
                );
            y += PlaceField(backgroundField, fieldIndex++, left, y, backgroundWidth, 0, 0, true);
            y = CompleteArtworkPanel(panelStart, y);
        }
        fieldScroll.SetContentHeight(y + 24, !reflowingFields);
    }

    /// <summary>
    /// Places a compact artwork section title with its preview-only background control.
    /// </summary>
    /// <param name="caption">The section caption.</param>
    /// <param name="toggle">The preview control owned by the section, if a background can be shown.</param>
    /// <param name="isChecked">Whether this section currently composites its background.</param>
    /// <param name="y">The section's top edge.</param>
    /// <param name="width">The available form width.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <returns>The first position below the title and control.</returns>
    private int PlaceArtworkSectionHeader(
        string caption,
        UICheckboxView toggle,
        bool isChecked,
        int y,
        float width,
        ref int labelIndex
    )
    {
        GetArtworkContentBounds(width, out float contentLeft, out float contentWidth);
        if (toggle == null)
            return y + PlaceLabel(caption, labelIndex++, contentLeft, y, contentWidth, true) + 6;
        const int toggleWidth = 174;
        const int toggleHeight = 26;
        int headingHeight;
        if (contentWidth >= 400)
        {
            headingHeight = PlaceLabel(
                caption,
                labelIndex++,
                contentLeft,
                y,
                contentWidth - toggleWidth - 12,
                true
            );
            SetFormRect(
                (RectTransform)toggle.transform,
                contentLeft + contentWidth - toggleWidth,
                y,
                toggleWidth,
                toggleHeight
            );
            toggle.SetIsCheckedWithoutNotify(isChecked);
            toggle.gameObject.SetActive(true);
            return y + Mathf.Max(headingHeight, toggleHeight) + 6;
        }

        headingHeight = PlaceLabel(caption, labelIndex++, contentLeft, y, contentWidth, true);
        y += headingHeight + 4;
        SetFormRect((RectTransform)toggle.transform, contentLeft, y, toggleWidth, toggleHeight);
        toggle.SetIsCheckedWithoutNotify(isChecked);
        toggle.gameObject.SetActive(true);
        return y + toggleHeight + 6;
    }

    /// <summary>
    /// Offers standard output generation after a usable large unit image has been selected.
    /// </summary>
    /// <param name="fields">The unit's ordinary output image fields.</param>
    /// <param name="y">The first available position in the images card.</param>
    /// <param name="width">The complete form width.</param>
    /// <returns>The first position below the optional generation action.</returns>
    private int PlaceArtworkCutsAction(
        IReadOnlyList<ContentPackEditorField> fields,
        int y,
        float width
    )
    {
        if (!ContentEditorUnitArtworkLayout.SupportsStandardCuts(selectedRecord))
            return y;
        ContentPackEditorField source = ContentEditorUnitArtworkLayout
            .GetGenerationSourceMembers(selectedRecord.Kind)
            .Select(member =>
                fields.FirstOrDefault(field =>
                    field.MemberName == member && ResolveArtworkTexture(field) != null
                )
            )
            .FirstOrDefault(field => field != null);
        if (source == null)
            return y;
        ContentPackEditorField[] targets = fields
            .Where(field =>
                field != source
                && ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(field, out _)
            )
            .OrderBy(GetArtworkOutputOrder)
            .ToArray();
        if (targets.Length == 0)
            return y;

        artworkCutsButton ??= Instantiate(setupUnitsTemplate, fieldScroll.ScrollArea.ContentRoot);
        artworkCutsButton.name = "GenerateArtworkCuts";
        artworkCutsButton.onClick.RemoveAllListeners();
        ContentPackEditorField capturedSource = source;
        IReadOnlyList<ContentPackEditorField> capturedTargets = targets;
        artworkCutsButton.onClick.AddListener(() =>
        {
            if (Commit())
                ArtworkCutsRequested?.Invoke(capturedSource, capturedTargets);
        });
        string sourceName = source.MemberName switch
        {
            "MessageImagePath" => "message",
            "DisplayImagePath" => "display",
            "BattleResultImagePath" => "battle result",
            "SmallDisplayImagePath" => "small display",
            _ => "encyclopedia",
        };
        artworkCutsButton.GetComponentInChildren<TMP_Text>().text =
            "Generate cuts from " + sourceName + " image";
        GetArtworkContentBounds(width, out float contentLeft, out float contentWidth);
        SetFormRect(
            (RectTransform)artworkCutsButton.transform,
            contentLeft,
            y,
            Mathf.Min(380, contentWidth),
            34
        );
        artworkCutsButton.gameObject.SetActive(true);
        return y + 50;
    }

    /// <summary>
    /// Changes only the ordinary output preview composition without editing content data.
    /// </summary>
    /// <param name="visible">Whether output previews should show their effective background.</param>
    private void SetOutputArtworkBackgroundVisible(bool visible)
    {
        showOutputArtworkBackground = visible;
        RenderFields(false);
    }

    /// <summary>
    /// Changes only the gameplay-state preview composition without editing content data.
    /// </summary>
    /// <param name="visible">Whether state previews should show their effective background.</param>
    private void SetStateArtworkBackgroundVisible(bool visible)
    {
        showStateArtworkBackground = visible;
        RenderFields(false);
    }

    /// <summary>
    /// Opens one content-owning unit-artwork card with a fixed inset and clipping boundary.
    /// </summary>
    /// <param name="panelIndex">The next reusable panel index.</param>
    /// <param name="name">The stable panel name.</param>
    /// <param name="start">The card's form-space top position.</param>
    /// <param name="width">The available form width.</param>
    /// <returns>The first content position inside the card.</returns>
    private int BeginArtworkPanel(ref int panelIndex, string name, int start, float width)
    {
        while (artworkPanels.Count <= panelIndex)
        {
            RawImage panel = Instantiate(artworkPanelTemplate, fieldScroll.ScrollArea.ContentRoot);
            ContentEditorCardView card =
                panel.GetComponent<ContentEditorCardView>()
                ?? panel.gameObject.AddComponent<ContentEditorCardView>();
            card.transform.SetAsFirstSibling();
            artworkPanels.Add(card);
        }
        activeArtworkPanel = artworkPanels[panelIndex++];
        activeArtworkPanel.name = artworkPanelTemplate.name + "-" + name;
        activeArtworkPanelTop = start;
        activeArtworkPanel.Begin(
            _artworkPanelLeft,
            start,
            Mathf.Max(1, Mathf.FloorToInt(width - _artworkPanelLeft * 2))
        );
        return start + ContentEditorCardView.ContentInset;
    }

    /// <summary>
    /// Finalizes the active unit-artwork card and releases it as the placement parent.
    /// </summary>
    /// <param name="start">The card's form-space top position.</param>
    /// <param name="contentBottom">The first position below its final child.</param>
    /// <returns>The first position below the card and its inter-card gap.</returns>
    private int CompleteArtworkPanel(int start, int contentBottom)
    {
        if (activeArtworkPanel == null)
            throw new InvalidOperationException("No artwork card is active.");
        int bottom = contentBottom + ContentEditorCardView.ContentInset;
        activeArtworkPanel.Complete(bottom - start);
        activeArtworkPanel = null;
        activeArtworkPanelTop = 0;
        return bottom + 18;
    }

    /// <summary>
    /// Converts a form-space rectangle into the active card's padded local coordinates.
    /// </summary>
    /// <param name="child">The child being placed.</param>
    /// <param name="x">The requested form-space left edge.</param>
    /// <param name="y">The requested form-space top edge.</param>
    /// <param name="width">The requested width.</param>
    /// <param name="height">The requested height, which may be zero for auto-sized fields.</param>
    /// <returns>The rectangle to pass to the child's layout implementation.</returns>
    private Rect PrepareFormRect(RectTransform child, float x, float y, float width, float height)
    {
        if (activeArtworkPanel == null)
        {
            if (child.parent != fieldScroll.ScrollArea.ContentRoot)
                child.SetParent(fieldScroll.ScrollArea.ContentRoot, false);
            return new Rect(x, y, width, height);
        }

        RectInt constrained = activeArtworkPanel.SetChildRect(
            child,
            new Rect(x - _artworkPanelLeft, y - activeArtworkPanelTop, width, Mathf.Max(1, height))
        );
        return new Rect(constrained.x, constrained.y, constrained.width, height);
    }

    /// <summary>
    /// Places a graphic or label through the active card's clipping and padding contract.
    /// </summary>
    /// <param name="child">The rectangle to place.</param>
    /// <param name="x">The requested form-space left edge.</param>
    /// <param name="y">The requested form-space top edge.</param>
    /// <param name="width">The requested width.</param>
    /// <param name="height">The requested height.</param>
    /// <returns>The assigned rectangle in its current parent coordinates.</returns>
    private RectInt SetFormRect(RectTransform child, float x, float y, float width, float height)
    {
        Rect rect = PrepareFormRect(child, x, y, width, height);
        RectInt result = new(
            Mathf.RoundToInt(rect.x),
            Mathf.RoundToInt(rect.y),
            Mathf.Max(1, Mathf.RoundToInt(rect.width)),
            Mathf.Max(1, Mathf.RoundToInt(rect.height))
        );
        UILayout.SetSourceRect(child, result.x, result.y, result.width, result.height);
        return result;
    }

    /// <summary>
    /// Gets the horizontal bounds available to a gallery inside the current card or plain form.
    /// </summary>
    /// <param name="formWidth">The complete form width.</param>
    /// <param name="left">The form-space left edge of the available content.</param>
    /// <param name="availableWidth">The available content width.</param>
    private void GetArtworkContentBounds(float formWidth, out float left, out float availableWidth)
    {
        if (activeArtworkPanel == null)
        {
            left = 4;
            availableWidth = Mathf.Max(1, formWidth - 8);
            return;
        }
        left = _artworkPanelLeft + ContentEditorCardView.ContentInset;
        availableWidth = Mathf.Max(
            1,
            activeArtworkPanel.RectTransform.rect.width - ContentEditorCardView.ContentInset * 2
        );
    }

    /// <summary>
    /// Places a linear group of artwork inputs using the standard media and scalar controls.
    /// </summary>
    /// <param name="fields">The fields to place.</param>
    /// <param name="y">The initial vertical position.</param>
    /// <param name="width">The available width.</param>
    /// <param name="fieldIndex">The next reusable field view.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <param name="imageIndex">The next reusable inline artwork preview.</param>
    /// <param name="skipPreview">Whether a separate effective preview already represents the field.</param>
    /// <returns>The next vertical position.</returns>
    private int PlaceArtworkFieldGroup(
        IReadOnlyList<ContentPackEditorField> fields,
        int y,
        float width,
        ref int fieldIndex,
        ref int labelIndex,
        ref int imageIndex,
        bool skipPreview = false
    )
    {
        foreach (ContentPackEditorField field in fields)
        {
            bool stacked = field.IsMedia || width < 480;
            if (stacked)
                y += PlaceLabel(
                    GetArtworkFieldCaption(field),
                    labelIndex++,
                    4,
                    y,
                    width - 8,
                    false,
                    field.HelpText
                );
            if (!skipPreview)
                y = PlaceArtworkImagePreview(field, y, width, ref imageIndex, ref labelIndex);
            y += PlaceField(
                field,
                fieldIndex++,
                0,
                y,
                width,
                stacked ? 0 : Mathf.Min(280, width * 0.45f),
                0,
                field.MediaType == ContentPackEditorMediaType.Image
            );
        }
        return y;
    }

    /// <summary>
    /// Removes dimension text already represented by the artwork gallery heading.
    /// </summary>
    /// <param name="field">The artwork field.</param>
    /// <returns>The concise field caption.</returns>
    private static string GetArtworkFieldCaption(ContentPackEditorField field)
    {
        string expected = ContentPackEditorFieldMetadata.GetExpectedImageDimensions(field);
        string suffix = expected == null ? null : " (" + expected + ")";
        return suffix != null && field.Caption.EndsWith(suffix, StringComparison.Ordinal)
            ? field.Caption[..^suffix.Length]
            : field.Caption;
    }

    /// <summary>
    /// Places the distinct large, medium, and small output cuts in one responsive gallery.
    /// </summary>
    /// <param name="fields">The normal output fields.</param>
    /// <param name="background">The optional effective faction background.</param>
    /// <param name="compactBackground">The gameplay card background for compact personnel artwork.</param>
    /// <param name="y">The initial vertical position.</param>
    /// <param name="width">The available form width.</param>
    /// <param name="fieldIndex">The next reusable field view.</param>
    /// <param name="imageIndex">The next reusable preview image.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <returns>The first position below the complete output gallery.</returns>
    private int PlaceArtworkOutputGallery(
        IReadOnlyList<ContentPackEditorField> fields,
        Texture2D background,
        Texture2D compactBackground,
        int y,
        float width,
        ref int fieldIndex,
        ref int imageIndex,
        ref int labelIndex
    )
    {
        var outputs = fields
            .Select(field =>
            {
                bool found = TryGetArtworkDimensions(field, out Vector2Int dimensions);
                return new
                {
                    Field = field,
                    Found = found,
                    Dimensions = dimensions,
                    Texture = ResolveArtworkTexture(field),
                };
            })
            .Where(output => output.Found)
            .OrderBy(output => GetArtworkOutputOrder(output.Field))
            .ToArray();
        if (outputs.Length == 0)
            return y;

        const int gap = 16;
        bool ship = selectedRecord.Kind is "CapitalShip" or "Starfighter";
        int minimumColumnWidth = ship ? 218 : 240;
        GetArtworkContentBounds(width, out float contentLeft, out float contentWidth);
        int columns = Mathf.Clamp(
            Mathf.FloorToInt((contentWidth + gap) / (minimumColumnWidth + gap)),
            1,
            Mathf.Max(1, Mathf.Min(ship ? 5 : 4, outputs.Length))
        );
        int columnWidth = Mathf.Max(
            1,
            Mathf.FloorToInt((contentWidth - gap * (columns - 1)) / columns)
        );
        Vector2Int[] sizes = outputs
            .Select(output => GetArtworkGallerySize(columnWidth, output.Dimensions))
            .ToArray();
        int[] cardWidths = sizes.Select(size => Mathf.Max(120, size.x)).ToArray();
        int outputIndex = 0;
        while (outputIndex < outputs.Length)
        {
            int rowStart = outputIndex;
            float rowProbeX = contentLeft;
            while (
                outputIndex < outputs.Length
                && (
                    outputIndex == rowStart
                    || rowProbeX + cardWidths[outputIndex] <= contentLeft + contentWidth + 0.5f
                )
            )
            {
                rowProbeX += cardWidths[outputIndex] + gap;
                outputIndex++;
            }

            float x = contentLeft;
            int imageBottom = y;
            for (int index = rowStart; index < outputIndex; index++)
            {
                var output = outputs[index];
                Vector2Int size = sizes[index];
                string caption = GetArtworkOutputCaption(output.Field, output.Dimensions);
                int labelHeight = PlaceLabel(caption, labelIndex++, x, y, cardWidths[index], false);
                int imageY = y + labelHeight + 4;
                Texture2D entity = output.Texture;
                PlaceArtworkComposition(
                    "Output-" + output.Field.MemberName,
                    Mathf.RoundToInt(x),
                    imageY,
                    size.x,
                    size.y,
                    output.Dimensions,
                    entity == null
                        ? null
                        : GetArtworkBackground(
                            new[] { output.Field },
                            background,
                            compactBackground
                        ),
                    entity,
                    Array.Empty<Texture2D>(),
                    ref imageIndex,
                    fitEntity: true
                );
                if (entity == null)
                    PlaceArtworkPlaceholder(
                        string.IsNullOrWhiteSpace(output.Field.Value)
                            ? "Empty image slot"
                            : "Image unavailable",
                        labelIndex++,
                        x,
                        imageY,
                        size.x
                    );
                imageBottom = Mathf.Max(imageBottom, imageY + size.y);
                x += cardWidths[index] + gap;
            }

            int controlsY = imageBottom + 8;
            int rowBottom = controlsY;
            x = contentLeft;
            for (int index = rowStart; index < outputIndex; index++)
            {
                rowBottom = Mathf.Max(
                    rowBottom,
                    controlsY
                        + PlaceField(
                            outputs[index].Field,
                            fieldIndex++,
                            x,
                            controlsY,
                            cardWidths[index],
                            0,
                            0,
                            true
                        )
                );
                x += cardWidths[index] + gap;
            }
            y = rowBottom + (outputIndex < outputs.Length ? gap : 8);
        }
        return y;
    }

    /// <summary>
    /// Names one artwork card by its actual use instead of reducing it to a size class.
    /// </summary>
    /// <param name="field">The independently editable image.</param>
    /// <param name="dimensions">Its expected pixel dimensions.</param>
    /// <returns>The semantic card title and dimensions.</returns>
    internal static string GetArtworkOutputCaption(
        ContentPackEditorField field,
        Vector2Int dimensions
    )
    {
        string caption = GetArtworkFieldCaption(field);
        if (caption.EndsWith(" image", StringComparison.OrdinalIgnoreCase))
            caption = caption[..^6];
        return caption + " (" + dimensions.x + " × " + dimensions.y + ")";
    }

    /// <summary>
    /// Places the main ship art and its gameplay cuts before the report images.
    /// </summary>
    /// <param name="field">The image field being placed.</param>
    /// <returns>The semantic card order.</returns>
    private static int GetArtworkOutputOrder(ContentPackEditorField field) =>
        (field.Record.Kind, field.MemberName) switch
        {
            ("CapitalShip" or "Starfighter", "DisplayImagePath") => -3,
            ("CapitalShip" or "Starfighter", "SmallDisplayImagePath") => -2,
            ("CapitalShip" or "Starfighter", "BattleResultImagePath") => -1,
            (_, "MessageImagePath") => 0,
            (_, "EncyclopediaImagePath") => 1,
            (_, "DisplayImagePath") => 2,
            (_, "SmallDisplayImagePath") => 3,
            _ => 10,
        };

    /// <summary>
    /// Switches the gameplay gallery between the supported card sizes.
    /// </summary>
    /// <param name="states">The authored state outputs.</param>
    /// <param name="y">The next vertical position.</param>
    /// <param name="width">The available form width.</param>
    /// <returns>The first position below the optional size selector.</returns>
    private int PlaceArtworkStateSize(
        IReadOnlyList<ContentPackEditorField> states,
        int y,
        float width
    )
    {
        artworkStateChoices.Clear();
        artworkStateChoices.Add(0);
        if (states.Any(field => field.MemberName is "InTransitImagePath" or "DamagedImagePath"))
            artworkStateChoices.Add(1);
        if (
            states.Any(field =>
                field.MemberName
                    is "BattleResultInTransitImagePath"
                        or "BattleResultDamagedImagePath"
            )
        )
            artworkStateChoices.Add(2);
        if (!artworkStateChoices.Contains(artworkStateSize))
            artworkStateSize = 0;
        if (artworkStateChoices.Count == 1)
            return y;

        GetArtworkContentBounds(width, out float left, out float available);
        float tabWidth = Mathf.Min(
            140,
            (available - (artworkStateChoices.Count - 1) * 6) / artworkStateChoices.Count
        );
        for (int i = 0; i < artworkStateChoices.Count; i++)
        {
            int choice = artworkStateChoices[i];
            Button tab = artworkStateSizeButtons[choice];
            SetFormRect((RectTransform)tab.transform, left + i * (tabWidth + 6), y, tabWidth, 34);
            tab.gameObject.SetActive(true);
            ContentEditorArtworkWorkspace.SetSelected(tab, choice == artworkStateSize);
        }
        return y + 46;
    }

    /// <summary>
    /// Shows the exact artwork fields that supply the selected runtime card.
    /// </summary>
    /// <param name="index">The size option selected by the author.</param>
    private void SelectArtworkStateSize(int index)
    {
        if (index == artworkStateSize || !Commit())
            return;
        artworkStateSize = index;
        RenderFields(true);
    }

    /// <summary>
    /// Places the normal and combined status layers for the selected gameplay card.
    /// </summary>
    /// <param name="normalFields">The normal output fields.</param>
    /// <param name="stateFields">The separately authored status fields.</param>
    /// <param name="background">The optional effective faction background.</param>
    /// <param name="compactBackground">The gameplay card background for compact personnel artwork.</param>
    /// <param name="travelBackground">The faction's shared background for personnel in transit.</param>
    /// <param name="y">The initial vertical position.</param>
    /// <param name="width">The available form width.</param>
    /// <param name="fieldIndex">The next reusable field view.</param>
    /// <param name="imageIndex">The next reusable preview image.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <returns>The first position below the complete state gallery.</returns>
    private int PlaceArtworkStateGallery(
        IReadOnlyList<ContentPackEditorField> normalFields,
        IReadOnlyList<ContentPackEditorField> stateFields,
        Texture2D background,
        Texture2D compactBackground,
        Texture2D travelBackground,
        int y,
        float width,
        ref int fieldIndex,
        ref int imageIndex,
        ref int labelIndex
    )
    {
        var members = artworkStateSize switch
        {
            1 => (
                Normal: "DisplayImagePath",
                Travel: "InTransitImagePath",
                Damage: "DamagedImagePath"
            ),
            2 => (
                Normal: "BattleResultImagePath",
                Travel: "BattleResultInTransitImagePath",
                Damage: "BattleResultDamagedImagePath"
            ),
            _ => (
                Normal: "SmallDisplayImagePath",
                Travel: "InTransitSmallImagePath",
                Damage: "DamagedSmallImagePath"
            ),
        };
        ContentPackEditorField preferredBase = normalFields.FirstOrDefault(field =>
            field.MemberName == members.Normal
        );
        ContentPackEditorField baseField =
            preferredBase != null && ResolveArtworkTexture(preferredBase) != null
                ? preferredBase
                : normalFields.FirstOrDefault(field => field.MemberName == "DisplayImagePath")
                    ?? normalFields.FirstOrDefault();
        if (
            baseField == null
            || !TryGetArtworkDimensions(preferredBase ?? baseField, out Vector2Int dimensions)
        )
            return y;

        ContentPackEditorField transit = stateFields.FirstOrDefault(field =>
            field.MemberName == members.Travel
        );
        bool officer = selectedRecord.Kind == "Officer";
        bool largeOfficerCard = officer && artworkStateSize == 1;
        ContentPackEditorField damage = stateFields.FirstOrDefault(field =>
            field.MemberName == members.Damage
        );
        if (!largeOfficerCard)
            damage ??= stateFields.FirstOrDefault(field => field.MemberName == "InjuredImagePath");
        ContentPackEditorField captured = largeOfficerCard
            ? null
            : stateFields.FirstOrDefault(field => field.MemberName == "CapturedOverlayImagePath");
        Texture2D officerTravelBackground = travelBackground;
        if (officer)
        {
            ContentPackEditorField travelSource = transit;
            if (string.IsNullOrEmpty(travelSource?.Value))
            {
                string fallbackMember = largeOfficerCard
                    ? "InTransitSmallImagePath"
                    : "InTransitImagePath";
                travelSource = stateFields.FirstOrDefault(field =>
                    field.MemberName == fallbackMember
                );
            }
            officerTravelBackground = ResolveArtworkTexture(travelSource) ?? travelBackground;
        }

        bool regiment = selectedRecord.Kind == "Regiment";
        Texture2D defenseTravel =
            regiment && showStateArtworkBackground
                ? ResolveUnitCompactBackground(inTransit: true, defense: true)
                : null;
        bool splitTransit =
            regiment
            && (defenseTravel != travelBackground || ResolveArtworkTexture(transit) != null);
        List<(
            string Caption,
            ContentPackEditorField[] Layers,
            ContentPackEditorField Action
        )> states = new() { ("Normal", Array.Empty<ContentPackEditorField>(), null) };
        if (officer && transit == null)
            states.Add(("In transit", Array.Empty<ContentPackEditorField>(), null));
        if (splitTransit)
            states.Add(("In transit (Fleet)", Array.Empty<ContentPackEditorField>(), null));
        if (transit != null)
        {
            string caption =
                splitTransit ? "In transit (Defense)"
                : regiment || officer ? "In transit"
                : "Travel";
            states.Add((caption, new[] { transit }, transit));
        }
        if (damage != null)
            states.Add(
                (
                    damage.MemberName == "InjuredImagePath" ? "Injured" : "Damaged",
                    new[] { damage },
                    damage
                )
            );
        if (transit != null && damage != null)
            states.Add(
                (
                    damage.MemberName == "InjuredImagePath"
                        ? "Travel + injured"
                        : "Travel + damaged",
                    new[] { transit, damage },
                    null
                )
            );
        if (captured != null)
            states.Add(("Captured", new[] { captured }, captured));

        Texture2D baseTexture = ResolveArtworkTexture(baseField);
        const int gap = 16;
        GetArtworkContentBounds(width, out float contentLeft, out float contentWidth);
        int columnCount = Mathf.Clamp(
            Mathf.FloorToInt((contentWidth + gap) / (220 + gap)),
            1,
            states.Count
        );
        float columnWidth = (contentWidth - (columnCount - 1) * gap) / columnCount;
        Vector2Int size = GetArtworkGallerySize(columnWidth, dimensions);
        float x = contentLeft;
        int rowHeight = 0;
        foreach (
            (
                string caption,
                ContentPackEditorField[] layers,
                ContentPackEditorField action
            ) in states
        )
        {
            if (x > contentLeft && x + size.x > contentLeft + contentWidth + 0.5f)
            {
                y += rowHeight + gap;
                x = contentLeft;
                rowHeight = 0;
            }

            int labelHeight = PlaceLabel(caption, labelIndex++, x, y, size.x, false);
            int imageY = y + labelHeight + 4;
            bool officerTransit = officer && transit != null && layers.Contains(transit);
            Texture2D[] overlays = layers
                .Where(field => !officerTransit || field != transit)
                .Select(field => ResolveArtworkTexture(field))
                .Where(texture => texture != null)
                .ToArray();
            bool hasStateArtwork = overlays.Length > 0;
            Texture2D stateBackground = GetArtworkBackground(
                new[] { baseField },
                background,
                compactBackground
            );
            Texture2D stateEntity = baseTexture;
            if (caption is "In transit" or "In transit (Fleet)")
                stateBackground = travelBackground;
            if (officerTransit || (officer && caption == "In transit"))
                stateBackground = officerTravelBackground;
            if (action == transit && transit != null)
            {
                if (selectedRecord.Kind == "Building" && overlays.Length > 0)
                {
                    stateEntity = overlays[0];
                    overlays = Array.Empty<Texture2D>();
                }
                else if (selectedRecord.Kind == "SpecialForce")
                {
                    stateBackground = overlays.FirstOrDefault() ?? travelBackground;
                    overlays = Array.Empty<Texture2D>();
                }
                else if (regiment)
                    stateBackground = defenseTravel;
            }
            PlaceArtworkComposition(
                "State-" + caption.Replace(' ', '-'),
                Mathf.RoundToInt(x),
                imageY,
                size.x,
                size.y,
                dimensions,
                stateBackground,
                stateEntity,
                overlays,
                ref imageIndex,
                fitEntity: selectedRecord.Kind == "Officer"
            );
            if (stateEntity == null && stateBackground == null && overlays.Length == 0)
                PlaceArtworkPlaceholder("Empty image slot", labelIndex++, x, imageY, size.x);
            int cardHeight = labelHeight + 4 + size.y;
            if (action != null)
            {
                bool canEditEffect = ContentEditorArtworkEffectGenerator.TryGetStyle(
                    action.Record,
                    action == transit
                        ? ContentEditorArtworkEffect.Travel
                        : ContentEditorArtworkEffect.Damage,
                    out _
                );
                Action edit = null;
                if (canEditEffect)
                    edit = () =>
                    {
                        if (Commit())
                            ArtworkEffectRequested?.Invoke(
                                action,
                                action == transit
                                    ? ContentEditorArtworkEffect.Travel
                                    : ContentEditorArtworkEffect.Damage,
                                action == transit && !hasStateArtwork
                            );
                    };
                cardHeight +=
                    4
                    + PlaceField(
                        action,
                        fieldIndex++,
                        x,
                        imageY + size.y + 4,
                        size.x,
                        0,
                        0,
                        true,
                        editArtwork: edit,
                        hasEffectArtwork: hasStateArtwork
                    );
            }
            rowHeight = Mathf.Max(rowHeight, cardHeight);
            x += size.x + gap;
        }
        return y + rowHeight + 8;
    }

    /// <summary>
    /// Draws the image status over its frame, regardless of the pooled controls' creation order.
    /// </summary>
    /// <param name="text">The empty or unavailable image status.</param>
    /// <param name="index">The pooled label slot.</param>
    /// <param name="x">The left edge of the preview frame.</param>
    /// <param name="y">The top edge of the preview frame.</param>
    /// <param name="width">The preview frame width.</param>
    private void PlaceArtworkPlaceholder(string text, int index, float x, int y, int width)
    {
        PlaceLabel(text, index, x + 8, y + 8, Mathf.Max(1, width - 16), false);
        groupLabels[index].transform.SetAsLastSibling();
    }

    /// <summary>
    /// Places one layered artwork composition in the gameplay rendering order.
    /// </summary>
    /// <param name="name">The stable preview name.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The displayed width.</param>
    /// <param name="height">The displayed height.</param>
    /// <param name="target">The output dimensions represented by the composition.</param>
    /// <param name="background">The optional background layer.</param>
    /// <param name="entity">The normal entity layer.</param>
    /// <param name="overlays">The travel, damage, injury, or captured layers in render order.</param>
    /// <param name="imageIndex">The next reusable preview image.</param>
    /// <param name="fitEntity">Whether the normal image must fit entirely within the preview.</param>
    private void PlaceArtworkComposition(
        string name,
        int x,
        int y,
        int width,
        int height,
        Vector2Int target,
        Texture2D background,
        Texture2D entity,
        IReadOnlyList<Texture2D> overlays,
        ref int imageIndex,
        bool fitEntity = false
    )
    {
        RawImage backing = GetArtworkPreviewImage(ref imageIndex, name + "-Backing");
        backing.texture = null;
        backing.color =
            entity == null && background == null && overlays.Count == 0
                ? ContentEditorAppearance.ColorFor(
                    GetComponentInParent<ContentEditorAppearance>()?.Mode
                        ?? ContentEditorColorMode.Dark,
                    ContentEditorColorRole.ScrollTrack
                )
                : new Color(0.035f, 0.04f, 0.05f, 1f);
        backing.uvRect = new Rect(0, 0, 1, 1);
        SetFormRect(backing.rectTransform, x, y, width, height);
        PlaceArtworkCompositionLayer(
            name + "-Background",
            background,
            target,
            false,
            x,
            y,
            width,
            height,
            ref imageIndex
        );
        PlaceArtworkCompositionLayer(
            name + "-Entity",
            entity,
            target,
            fitEntity || selectedRecord?.Kind == "Building",
            x,
            y,
            width,
            height,
            ref imageIndex
        );
        for (int index = 0; index < overlays.Count; index++)
        {
            PlaceArtworkCompositionLayer(
                name + "-Overlay-" + index,
                overlays[index],
                target,
                ContentEditorUnitArtworkLayout.IsHeadquartersIcon(selectedRecord),
                x,
                y,
                width,
                height,
                ref imageIndex
            );
        }
    }

    /// <summary>
    /// Places one optional texture within a composition frame using its established fit policy.
    /// </summary>
    /// <param name="name">The stable layer name.</param>
    /// <param name="texture">The optional layer texture.</param>
    /// <param name="target">The output dimensions represented by the composition.</param>
    /// <param name="fitEntireTexture">Whether the complete source must remain visible.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The displayed width.</param>
    /// <param name="height">The displayed height.</param>
    /// <param name="imageIndex">The next reusable preview image.</param>
    private void PlaceArtworkCompositionLayer(
        string name,
        Texture2D texture,
        Vector2Int target,
        bool fitEntireTexture,
        int x,
        int y,
        int width,
        int height,
        ref int imageIndex
    )
    {
        if (texture == null)
            return;
        RawImage image = GetArtworkPreviewImage(ref imageIndex, name);
        image.texture = texture;
        image.color = Color.white;
        Vector2Int source = new(texture.width, texture.height);
        if (fitEntireTexture)
        {
            image.uvRect = new Rect(0, 0, 1, 1);
            RectInt fitted = ContentEditorArtworkCrop.GetCenteredFitRect(
                source,
                new RectInt(x, y, width, height)
            );
            SetFormRect(image.rectTransform, fitted.x, fitted.y, fitted.width, fitted.height);
            return;
        }
        image.uvRect = ContentEditorArtworkCrop.GetCenteredUvRect(source, target);
        SetFormRect(image.rectTransform, x, y, width, height);
    }

    /// <summary>
    /// Fits one gallery image into compact shared limits while preserving its aspect ratio.
    /// </summary>
    /// <param name="availableWidth">The available gallery width.</param>
    /// <param name="dimensions">The represented output dimensions.</param>
    /// <returns>The displayed image size.</returns>
    private static Vector2Int GetArtworkGallerySize(float availableWidth, Vector2Int dimensions)
    {
        int width = Mathf.Max(1, Mathf.Min(420, Mathf.FloorToInt(availableWidth)));
        int height = Mathf.Max(1, Mathf.RoundToInt((float)width * dimensions.y / dimensions.x));
        if (height > 190)
        {
            height = 190;
            width = Mathf.Max(1, Mathf.RoundToInt((float)height * dimensions.x / dimensions.y));
        }
        return new Vector2Int(width, height);
    }

    /// <summary>
    /// Selects the optional composition background that belongs behind one output group.
    /// </summary>
    /// <param name="fields">The output fields sharing one preview.</param>
    /// <param name="background">The effective authored unit background.</param>
    /// <param name="compactBackground">The gameplay card background for compact personnel artwork.</param>
    /// <returns>The background for this output, or null for transparent officer cuts.</returns>
    private Texture2D GetArtworkBackground(
        IReadOnlyList<ContentPackEditorField> fields,
        Texture2D background,
        Texture2D compactBackground
    )
    {
        if (selectedRecord?.Kind is "CapitalShip" or "Starfighter")
            return background;
        if (
            selectedRecord?.Kind == "Officer"
            && fields.Any(field =>
                field.MemberName is "MessageImagePath" or "EncyclopediaImagePath"
            )
        )
            return background;
        return fields.Any(field => field.MemberName == "SmallDisplayImagePath")
            ? compactBackground
            : null;
    }

    /// <summary>
    /// Resolves the background and crop used by one generated output in the artwork gallery.
    /// </summary>
    /// <param name="field">The output whose preview is being prepared.</param>
    /// <param name="dimensions">The exact generated output dimensions.</param>
    /// <returns>The optional background texture and its normalized sample rectangle.</returns>
    internal (Texture2D Texture, Rect Uv) GetArtworkCutBackground(
        ContentPackEditorField field,
        Vector2Int dimensions
    )
    {
        if (!showOutputArtworkBackground)
            return (null, default);
        ContentPackEditorField overrideField = selectedRecord.Kind
            is "Officer"
                or "CapitalShip"
                or "Starfighter"
            ? selectedRecord.GetField(ContentEditorUnitArtworkLayout.BackgroundMember)
            : null;
        Texture2D texture = GetArtworkBackground(
            new[] { field },
            ResolveEffectiveUnitBackground(overrideField),
            ResolveUnitCompactBackground()
        );
        return texture == null
            ? (null, default)
            : (
                texture,
                ContentEditorArtworkCrop.GetCenteredUvRect(
                    new Vector2Int(texture.width, texture.height),
                    dimensions
                )
            );
    }

    /// <summary>
    /// Shows the theme background used while the unit's optional override remains empty.
    /// </summary>
    /// <param name="field">The empty background-override field.</param>
    /// <param name="texture">The effective faction background.</param>
    /// <param name="x">The left edge shared with the override action.</param>
    /// <param name="y">The next vertical form position.</param>
    /// <param name="width">The available form width.</param>
    /// <param name="imageIndex">The next reusable inline artwork preview.</param>
    /// <param name="labelIndex">The next reusable status label.</param>
    /// <returns>The next vertical position.</returns>
    private int PlaceEffectiveBackgroundPreview(
        ContentPackEditorField field,
        Texture2D texture,
        float x,
        int y,
        float width,
        ref int imageIndex,
        ref int labelIndex
    )
    {
        if (!TryGetArtworkDimensions(field, out Vector2Int target))
            target = new Vector2Int(texture.width, texture.height);
        int imageWidth = Mathf.Max(1, Mathf.RoundToInt(width));
        int imageHeight = Mathf.Max(1, Mathf.RoundToInt((float)imageWidth * target.y / target.x));
        if (imageHeight > 220)
        {
            imageHeight = 220;
            imageWidth = Mathf.Max(1, Mathf.RoundToInt((float)imageHeight * target.x / target.y));
        }
        RawImage preview = GetArtworkPreviewImage(
            ref imageIndex,
            "EffectiveBackground-" + field.MemberName
        );
        preview.texture = texture;
        preview.uvRect = ContentEditorArtworkCrop.GetCenteredUvRect(
            new Vector2Int(texture.width, texture.height),
            target
        );
        SetFormRect(preview.rectTransform, x, y, imageWidth, imageHeight);
        y += imageHeight + 8;
        if (string.IsNullOrWhiteSpace(field.Value))
            y += PlaceLabel("Using faction default.", labelIndex++, x, y, imageWidth, false) + 4;
        return y;
    }

    /// <summary>
    /// Activates one pooled artwork image with a stable testable name.
    /// </summary>
    /// <param name="imageIndex">The next reusable inline artwork preview.</param>
    /// <param name="imageName">The semantic preview name.</param>
    /// <returns>The active image.</returns>
    private RawImage GetArtworkPreviewImage(ref int imageIndex, string imageName)
    {
        while (articleImages.Count <= imageIndex)
        {
            RawImage image = Instantiate(articleImageTemplate, fieldScroll.ScrollArea.ContentRoot);
            image.raycastTarget = false;
            articleImages.Add(image);
        }
        RawImage preview = articleImages[imageIndex++];
        preview.name = articleImageTemplate.name + "-" + imageName;
        if (activeArtworkPanel != null)
        {
            if (preview.transform.parent != activeArtworkPanel.transform)
                preview.transform.SetParent(activeArtworkPanel.transform, false);
            preview.transform.SetAsLastSibling();
        }
        else if (preview.transform.parent != fieldScroll.ScrollArea.ContentRoot)
            preview.transform.SetParent(fieldScroll.ScrollArea.ContentRoot, false);
        preview.color = Color.white;
        preview.enabled = true;
        preview.gameObject.SetActive(true);
        return preview;
    }

    /// <summary>
    /// Reads one fixed output size from the shared field metadata.
    /// </summary>
    /// <param name="field">The image field being previewed.</param>
    /// <param name="dimensions">The parsed output dimensions.</param>
    /// <returns>True when the field has one established fixed size.</returns>
    private static bool TryGetArtworkDimensions(
        ContentPackEditorField field,
        out Vector2Int dimensions
    ) => ContentPackEditorFieldMetadata.TryGetExpectedImageDimensions(field, out dimensions);

    /// <summary>
    /// Resolves one authored artwork field without allowing a malformed path to break the form.
    /// </summary>
    /// <param name="field">The optional image field.</param>
    /// <returns>The resolved texture, or null when the field is empty or invalid.</returns>
    /// <param name="readable">Whether a CPU-readable copy is required for software compositing.</param>
    private Texture2D ResolveArtworkTexture(ContentPackEditorField field, bool readable = false)
    {
        if (field == null || string.IsNullOrWhiteSpace(field.Value))
            return null;
        try
        {
            string address = field.GetMediaAddress(field.Value);
            return readable ? assets.GetReadableTexture(address) : assets.GetTexture(address);
        }
        catch (ArgumentException exception)
        {
            GameLogger.Warning("Invalid artwork path: " + exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Resolves a unit override first and then the matching faction theme background.
    /// </summary>
    /// <param name="overrideField">The unit's optional background field.</param>
    /// <returns>The effective background texture, or null when no background applies.</returns>
    /// <param name="readable">Whether a CPU-readable copy is required for software compositing.</param>
    private Texture2D ResolveEffectiveUnitBackground(
        ContentPackEditorField overrideField,
        bool readable = false
    )
    {
        if (!string.IsNullOrWhiteSpace(overrideField?.Value))
            return ResolveArtworkTexture(overrideField, readable);
        FactionTheme theme = ResolveUnitFactionTheme();
        string address = theme?.ArtworkDefaults?.GetBackgroundImagePath(selectedRecord.Kind);
        if (string.IsNullOrWhiteSpace(address))
            return null;
        try
        {
            return readable ? assets.GetReadableTexture(address) : assets.GetTexture(address);
        }
        catch (ArgumentException exception)
        {
            GameLogger.Warning("Invalid faction artwork path: " + exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Resolves the optional gameplay backdrop for a generated ship state preview.
    /// </summary>
    /// <returns>A readable ship backdrop, or null when the author has hidden it.</returns>
    internal Texture2D GetShipEffectBackground() =>
        showStateArtworkBackground
            ? ResolveEffectiveUnitBackground(
                selectedRecord.GetField(ContentEditorUnitArtworkLayout.BackgroundMember),
                readable: true
            )
            : null;

    /// <summary>
    /// Resolves the card background used behind compact personnel and regiment artwork in gameplay.
    /// </summary>
    /// <param name="inTransit">Whether to use the shared travel background for personnel.</param>
    /// <returns>The configured card background, or null when the selected unit is presented without one.</returns>
    /// <param name="readable">Whether a CPU-readable copy is required for software compositing.</param>
    /// <param name="defense">Whether to use the Defense window's troop transit backdrop.</param>
    internal Texture2D ResolveUnitCompactBackground(
        bool inTransit = false,
        bool readable = false,
        bool defense = false
    )
    {
        string address = GetUnitCompactBackgroundAddress(inTransit, defense);
        if (string.IsNullOrWhiteSpace(address))
            return null;
        try
        {
            return readable ? assets.GetReadableTexture(address) : assets.GetTexture(address);
        }
        catch (ArgumentException exception)
        {
            GameLogger.Warning("Invalid compact unit background path: " + exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Identifies the pack artwork used for a unit's matching gameplay backdrop.
    /// </summary>
    /// <param name="inTransit">Whether to select the travel state.</param>
    /// <param name="defense">Whether the override targets the defense card.</param>
    /// <returns>The effective media address, if configured.</returns>
    internal string GetUnitCompactBackgroundAddress(bool inTransit, bool defense)
    {
        FactionTheme theme = ResolveUnitFactionTheme();
        if (
            theme == null
            && selectedRecord.Kind == "Building"
            && inTransit
            && !string.IsNullOrEmpty(project.Definition.NeutralThemePath)
        )
            theme = ContentEditorPresentation.GetNeutralTheme(project);
        return selectedRecord.Kind switch
        {
            "Officer" when inTransit => theme
                ?.StrategyWindows
                ?.Fleet
                ?.PersonnelEnrouteBackgroundImagePath,
            "SpecialForce" when inTransit => theme
                ?.StrategyWindows
                ?.Defense
                ?.EnrouteBackgroundImagePath
                ?? theme?.StrategyWindows?.Fleet?.PersonnelEnrouteBackgroundImagePath,
            "Officer" or "SpecialForce" => theme
                ?.StrategyWindows
                ?.Defense
                ?.PersonnelBackgroundImagePath
                ?? theme?.StrategyWindows?.Fleet?.PersonnelBackgroundImagePath,
            "Regiment" when inTransit && defense => theme
                ?.StrategyWindows
                ?.Defense
                ?.EnrouteBackgroundImagePath,
            "Regiment" when inTransit => theme
                ?.StrategyWindows
                ?.Fleet
                ?.PersonnelEnrouteBackgroundImagePath,
            "Regiment" => theme?.StrategyWindows?.Fleet?.PersonnelBackgroundImagePath,
            "Building" when inTransit => theme
                ?.StrategyWindows
                ?.Defense
                ?.EnrouteBackgroundImagePath,
            _ => null,
        };
    }

    /// <summary>
    /// Finds the faction theme assigned to the selected faction-owned or faction-restricted unit.
    /// </summary>
    /// <returns>The matching theme, or null for shared unrestricted units.</returns>
    private FactionTheme ResolveUnitFactionTheme()
    {
        string factionID = null;
        if (!string.Equals(selectedRecord.Catalog.Faction, "Shared", StringComparison.Ordinal))
        {
            factionID = catalogs
                .Where(catalog =>
                    string.Equals(
                        catalog.Faction,
                        selectedRecord.Catalog.Faction,
                        StringComparison.Ordinal
                    )
                )
                .SelectMany(catalog => catalog.Records)
                .Where(record => record.Kind == "Faction")
                .Select(record =>
                    record.Element.Element("ID")?.Value
                    ?? record.Element.Element("InstanceID")?.Value
                )
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }
        factionID ??= new[] { "RecruitingFactionInstanceIDs", "ManufacturingFactionInstanceIDs" }
            .Select(member => selectedRecord.Element.Element(member))
            .Where(element => element != null)
            .SelectMany(element =>
                element.HasElements
                    ? element.Elements().Select(item => item.Value)
                    : new[] { element.Value }
            )
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return ContentEditorPresentation
            .GetFactionThemes(project)
            .Select(item => item.Theme)
            .FirstOrDefault(theme =>
                string.Equals(theme.FactionInstanceID, factionID, StringComparison.Ordinal)
            );
    }

    /// <summary>
    /// Shows the selected image and its current and expected dimensions directly beside its path.
    /// </summary>
    /// <param name="field">The artwork field being presented.</param>
    /// <param name="y">The next vertical form position.</param>
    /// <param name="width">The available form width.</param>
    /// <param name="imageIndex">The next reusable inline artwork preview.</param>
    /// <param name="labelIndex">The next reusable status label.</param>
    /// <returns>The next vertical position after the preview and dimension status.</returns>
    private int PlaceArtworkImagePreview(
        ContentPackEditorField field,
        int y,
        float width,
        ref int imageIndex,
        ref int labelIndex
    ) => PlaceArtworkImagePreview(field, 4, y, width, 440, 180, ref imageIndex, ref labelIndex);

    /// <summary>
    /// Shows an image and dimension status within an explicit artwork-browser column.
    /// </summary>
    /// <param name="field">The artwork field being presented.</param>
    /// <param name="x">The left edge of the preview column.</param>
    /// <param name="y">The next vertical form position.</param>
    /// <param name="width">The available preview-column width.</param>
    /// <param name="maximumWidth">The largest displayed image width.</param>
    /// <param name="maximumHeight">The largest displayed image height.</param>
    /// <param name="imageIndex">The next reusable inline artwork preview.</param>
    /// <param name="labelIndex">The next reusable status label.</param>
    /// <returns>The next vertical position after the preview and dimension status.</returns>
    private int PlaceArtworkImagePreview(
        ContentPackEditorField field,
        float x,
        int y,
        float width,
        int maximumWidth,
        int maximumHeight,
        ref int imageIndex,
        ref int labelIndex
    )
    {
        if (field.MediaType != ContentPackEditorMediaType.Image || field.IsMediaDirectory)
            return y;
        string address = field.Values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        Texture2D texture = null;
        bool invalid = false;
        if (address != null)
            try
            {
                texture = assets.GetTexture(field.GetMediaAddress(address));
            }
            catch (ArgumentException)
            {
                invalid = true;
            }
        Vector2Int current = Vector2Int.zero;
        RectInt contentBounds = default;
        if (texture != null)
        {
            RawImage preview = GetArtworkPreviewImage(
                ref imageIndex,
                "Selected-" + field.MemberName
            );
            preview.texture = texture;
            preview.color = Color.white;
            current = new Vector2Int(texture.width, texture.height);
            contentBounds = assets.GetTextureContentBounds(texture);
            preview.uvRect = new Rect(
                (float)contentBounds.x / current.x,
                (float)contentBounds.y / current.y,
                (float)contentBounds.width / current.x,
                (float)contentBounds.height / current.y
            );
            int availableWidth = Mathf.Max(1, Mathf.Min(Mathf.RoundToInt(width - 8), maximumWidth));
            int imageWidth = Mathf.Max(1, Mathf.Min(contentBounds.width, availableWidth));
            int imageHeight = Mathf.Max(
                1,
                Mathf.RoundToInt((float)imageWidth * contentBounds.height / contentBounds.width)
            );
            if (imageHeight > maximumHeight)
            {
                imageHeight = maximumHeight;
                imageWidth = Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        (float)imageHeight * contentBounds.width / contentBounds.height
                    )
                );
            }
            float imageX = x;
            SetFormRect(preview.rectTransform, imageX, y, imageWidth, imageHeight);
            preview.enabled = true;
            preview.gameObject.SetActive(true);
            y += imageHeight + 8;
        }
        if (texture == null)
        {
            Vector2Int dimensions = TryGetArtworkDimensions(field, out Vector2Int expected)
                ? expected
                : new Vector2Int(2, 1);
            Vector2Int size = GetArtworkGallerySize(
                Mathf.Max(1, Mathf.Min(width - 8, maximumWidth)),
                dimensions
            );
            int height = Mathf.Min(size.y, maximumHeight);
            PlaceArtworkComposition(
                "Empty-" + field.MemberName,
                Mathf.RoundToInt(x),
                y,
                size.x,
                height,
                dimensions,
                null,
                null,
                Array.Empty<Texture2D>(),
                ref imageIndex
            );
            PlaceArtworkPlaceholder(
                invalid || address != null ? "Image unavailable" : "Empty image slot",
                labelIndex++,
                x,
                y,
                size.x
            );
            return y + height + 8;
        }
        string status = GetArtworkDimensionWarning(field, current);
        if (!string.IsNullOrEmpty(status))
            y += PlaceLabel(status, labelIndex++, x, y, width - 8, false) + 8;
        return y;
    }

    /// <summary>
    /// Reports only source-size problems that would make an output crop invalid or blurry.
    /// </summary>
    /// <param name="field">The image field with an established output size.</param>
    /// <param name="current">The selected image size.</param>
    /// <returns>A concise warning, or null when the image is suitable.</returns>
    private static string GetArtworkDimensionWarning(
        ContentPackEditorField field,
        Vector2Int current
    )
    {
        if (!TryGetArtworkDimensions(field, out Vector2Int expected))
            return null;
        bool tooSmall = current.x < expected.x || current.y < expected.y;
        float currentRatio = (float)current.x / current.y;
        float expectedRatio = (float)expected.x / expected.y;
        bool wrongRatio = Mathf.Abs(currentRatio / expectedRatio - 1f) > 0.01f;
        if (!tooSmall && !wrongRatio)
            return null;
        string expectedText = expected.x + " × " + expected.y;
        if (tooSmall && wrongRatio)
            return "Warning: Image is smaller than "
                + expectedText
                + " and has the wrong aspect ratio.";
        return tooSmall
            ? "Warning: Image is smaller than " + expectedText + "."
            : "Warning: Image does not match the " + expectedText + " aspect ratio.";
    }

    /// <summary>
    /// Presents faction mask colors as live, editable samples of the actual interface shapes.
    /// </summary>
    private void RenderFactionColors()
    {
        previewPane.SetActive(false);
        float width = fieldScroll.ScrollArea.ViewportWidth;
        int labelIndex = 0;
        int y = 4;
        string heading =
            selectedPage.Key == ContentEditorContentSections.NeutralThemePageKey
                ? "Neutral colors"
                : "Faction colors";
        y += PlaceLabel(heading, labelIndex++, 4, y, width - 8, true);
        y +=
            PlaceLabel(
                "Set the faction color, menu text highlight, and individual interface tints.",
                labelIndex++,
                4,
                y,
                width - 8,
                false
            ) + 12;

        ContentPackEditorRecord themeRecord = selectedPage.RelatedRecords.FirstOrDefault(record =>
            record.Kind == "FactionTheme"
        );
        if (themeRecord == null)
        {
            y += PlaceLabel(
                "This faction does not have a theme record.",
                labelIndex++,
                4,
                y,
                width - 8,
                false
            );
            fieldScroll.SetContentHeight(y + 20, !reflowingFields);
            return;
        }

        FactionTheme theme = ContentEditorPresentation.GetFactionTheme(themeRecord);
        var scheme = ContentEditorContentSections
            .FactionColorFields(themeRecord)
            .ToDictionary(field => field.MemberName, StringComparer.Ordinal);
        ContentPackEditorField[] fields = scheme
            .Values.Where(field => !field.IsBoolean)
            .OrderBy(FactionColorOrder)
            .ThenBy(field => field.Caption, StringComparer.Ordinal)
            .ToArray();
        int columnCount = width >= 940 ? 2 : 1;
        const int gap = 16;
        const int cardHeight = 296;
        float cardWidth = (width - 8 - gap * (columnCount - 1)) / columnCount;
        for (int index = 0; index < fields.Length; index++)
        {
            int column = index % columnCount;
            int row = index / columnCount;
            ContentEditorColorPickerView picker = GetColorPicker(index);
            ContentPackEditorField field = fields[index];
            string linkName = ContentEditorContentSections.FactionColorLinkMember(field.MemberName);
            ContentPackEditorField link = linkName == null ? null : scheme[linkName];
            picker.Render(
                field,
                ResolveFactionColorSample(field, theme),
                ContentEditorContentSections.FactionColorValue(theme, field.MemberName)
                    ?? Color.white,
                (
                    link,
                    link != null
                        && ContentEditorContentSections.FactionColorFollowsPrimary(
                            theme,
                            field.MemberName,
                            link.Value
                        ),
                    theme.GetPrimaryColor()
                ),
                (submittedField, value) => SubmitField(submittedField, new[] { value }),
                HandleFieldChanged,
                ReportValidationError
            );
            picker.SetLayout(
                new Rect(
                    4 + column * (cardWidth + gap),
                    y + row * (cardHeight + gap),
                    cardWidth,
                    cardHeight
                )
            );
        }
        int rowCount = Mathf.CeilToInt((float)fields.Length / columnCount);
        fieldScroll.SetContentHeight(y + rowCount * (cardHeight + gap) + 8, !reflowingFields);
    }

    /// <summary>
    /// Gets or creates one pooled faction-color card.
    /// </summary>
    /// <param name="index">The reusable card index.</param>
    /// <returns>The active card.</returns>
    private ContentEditorColorPickerView GetColorPicker(int index)
    {
        while (colorPickers.Count <= index)
            colorPickers.Add(Instantiate(colorPickerTemplate, fieldScroll.ScrollArea.ContentRoot));
        return colorPickers[index];
    }

    /// <summary>
    /// Resolves the representative shared mask used by one faction color.
    /// </summary>
    /// <param name="field">The authored color field.</param>
    /// <param name="theme">The effective faction theme.</param>
    /// <returns>The representative mask texture, or null for a plain swatch.</returns>
    private Texture2D ResolveFactionColorSample(ContentPackEditorField field, FactionTheme theme)
    {
        string address = field.MemberName switch
        {
            nameof(FactionColorScheme.SelectionColorHex) => theme
                .StrategyWindows
                ?.Defense
                ?.SelectionImagePath,
            nameof(FactionColorScheme.ActiveWindowTitleColorHex) => theme
                .WindowTitleTheme
                ?.ActiveImagePath,
            nameof(FactionColorScheme.InactiveWindowTitleColorHex) => theme
                .WindowTitleTheme
                ?.InactiveImagePath,
            nameof(FactionColorScheme.ActiveBuildingPaneColorHex) => theme
                .PlanetWindowTheme
                ?.BuildingsPane
                ?.ManufacturingLaneState
                ?.ActiveImagePath,
            nameof(FactionColorScheme.InactiveBuildingPaneColorHex) => theme
                .PlanetWindowTheme
                ?.BuildingsPane
                ?.ManufacturingLaneState
                ?.InactiveImagePath,
            nameof(FactionColorScheme.PlanetMarkerColorHex) => theme
                .GalaxyBackground
                ?.PlanetIcons
                ?.Small,
            _ => null,
        };
        return string.IsNullOrWhiteSpace(address) ? null : assets.GetTexture(address);
    }

    /// <summary>
    /// Orders faction colors by their visual hierarchy and common editing flow.
    /// </summary>
    /// <param name="field">The color field.</param>
    /// <returns>The stable color-card position.</returns>
    private static int FactionColorOrder(ContentPackEditorField field) =>
        field.MemberName switch
        {
            nameof(FactionColorScheme.PrimaryColorHex) => 0,
            nameof(FactionColorScheme.HighlightTextColorHex) => 1,
            nameof(FactionColorScheme.SelectionColorHex) => 2,
            nameof(FactionColorScheme.ActiveWindowTitleColorHex) => 3,
            nameof(FactionColorScheme.InactiveWindowTitleColorHex) => 4,
            nameof(FactionColorScheme.ActiveBuildingPaneColorHex) => 5,
            nameof(FactionColorScheme.InactiveBuildingPaneColorHex) => 6,
            nameof(FactionColorScheme.PlanetMarkerColorHex) => 7,
            _ => 100,
        };

    /// <summary>
    /// Presents the three faction-owned unit backgrounds as compact preview-and-action cards.
    /// </summary>
    private void RenderFactionDefaultBackgrounds()
    {
        previewPane.SetActive(false);
        ContentPackEditorField[] fields = selectedPage
            .Fields.Where(ContentEditorContentSections.IsFactionBackgroundField)
            .ToArray();
        float width = fieldScroll.ScrollArea.ViewportWidth;
        int fieldIndex = 0,
            imageIndex = 0,
            labelIndex = 0,
            panelIndex = 0;
        int panelStart = 4;
        int y = BeginArtworkPanel(ref panelIndex, "DefaultBackgrounds", panelStart, width);
        y += PlaceLabel("Default backgrounds", labelIndex++, 0, y, width, true) + 8;
        GetArtworkContentBounds(width, out float contentLeft, out float contentWidth);
        const int gap = 14;
        int columns = Mathf.Clamp(
            Mathf.FloorToInt((contentWidth + gap) / (220 + gap)),
            1,
            Mathf.Max(1, fields.Length)
        );
        int columnWidth = Mathf.Max(
            1,
            Mathf.FloorToInt((contentWidth - gap * (columns - 1)) / columns)
        );
        int imageWidth = Mathf.Min(columnWidth, 360);
        int imageHeight = Mathf.Min(170, Mathf.Max(80, Mathf.RoundToInt(imageWidth * 0.5f)));
        for (int rowStart = 0; rowStart < fields.Length; rowStart += columns)
        {
            int rowCount = Mathf.Min(columns, fields.Length - rowStart);
            int headingHeight = 0;
            for (int column = 0; column < rowCount; column++)
            {
                float x = contentLeft + column * (columnWidth + gap);
                headingHeight = Mathf.Max(
                    headingHeight,
                    PlaceLabel(
                        GetDefaultBackgroundCaption(fields[rowStart + column]),
                        labelIndex++,
                        x,
                        y,
                        columnWidth,
                        true
                    )
                );
            }

            int imageY = y + headingHeight + 5;
            int controlY = imageY + imageHeight + 8;
            int rowBottom = controlY;
            for (int column = 0; column < rowCount; column++)
            {
                ContentPackEditorField field = fields[rowStart + column];
                float columnX = contentLeft + column * (columnWidth + gap);
                PlaceArtworkComposition(
                    "DefaultBackground-" + field.MemberName,
                    Mathf.RoundToInt(columnX),
                    imageY,
                    imageWidth,
                    imageHeight,
                    new Vector2Int(1800, 900),
                    null,
                    ResolveArtworkTexture(field),
                    Array.Empty<Texture2D>(),
                    ref imageIndex
                );
                rowBottom = Mathf.Max(
                    rowBottom,
                    controlY
                        + PlaceField(field, fieldIndex++, columnX, controlY, imageWidth, 0, 0, true)
                );
            }
            y = rowBottom + gap;
        }
        y = CompleteArtworkPanel(panelStart, y);
        fieldScroll.SetContentHeight(y + 20, !reflowingFields);
    }

    /// <summary>
    /// Names one faction default by the unit family it frames.
    /// </summary>
    /// <param name="field">The faction artwork default.</param>
    /// <returns>The compact card caption.</returns>
    private static string GetDefaultBackgroundCaption(ContentPackEditorField field) =>
        field.MemberName switch
        {
            nameof(FactionArtworkDefaults.OfficerBackgroundImagePath) => "Officers",
            nameof(FactionArtworkDefaults.CapitalShipBackgroundImagePath) => "Capital ships",
            nameof(FactionArtworkDefaults.StarfighterBackgroundImagePath) => "Starfighters",
            _ => field.Caption,
        };

    /// <summary>
    /// Chooses one mission within the current faction and offers its available report editor.
    /// </summary>
    private void RenderMissionPresentationTab()
    {
        IReadOnlyList<ContentEditorTreeNode> pages = selectedPage.MissionPages;
        ContentEditorTreeNode page = ActiveMissionPage;
        if (missionPresentationKey != page.Key)
            showMissionReports = false;
        missionPresentationKey = page.Key;
        missionPresentationDropdown.ClearOptions();
        missionPresentationDropdown.AddOptions(pages.Select(item => item.Caption).ToList());
        missionPresentationDropdown.SetValueWithoutNotify(pages.ToList().IndexOf(page));
        missionPresentationDropdown.RefreshShownValue();
        int width = Mathf.FloorToInt(fieldScroll.ScrollArea.ViewportWidth);
        bool reports = page.MissionReports.Count > 0;
        int pickerWidth = Mathf.Min(380, width - 8);
        bool split = reports && width >= pickerWidth + 194;
        UILayout.SetSourceRect(
            (RectTransform)missionPresentationDropdown.transform,
            4,
            4,
            pickerWidth,
            34
        );
        missionPresentationDropdown.gameObject.SetActive(true);
        int top = 50;
        if (reports)
        {
            missionReportsToggle.GetComponentInChildren<TMP_Text>().text = showMissionReports
                ? "Show presentation"
                : "Show reports";
            UILayout.SetSourceRect(
                (RectTransform)missionReportsToggle.transform,
                split ? pickerWidth + 12 : 4,
                split ? 4 : top,
                170,
                34
            );
            missionReportsToggle.gameObject.SetActive(true);
            if (!split)
                top += 44;
        }
        if (reports && showMissionReports)
            RenderMissionReportText(top);
        else
            RenderFactionMissionPresentation(top);
    }

    /// <summary>
    /// Opens the selected mission while keeping the same faction and inspector tab.
    /// </summary>
    /// <param name="index">The selected mission option.</param>
    private void SelectMissionPresentation(int index)
    {
        var pages = selectedPage.MissionPages;
        if (!Commit())
        {
            missionPresentationDropdown.SetValueWithoutNotify(
                pages.ToList().IndexOf(ActiveMissionPage)
            );
            return;
        }
        missionPresentationKey = pages[index].Key;
        showMissionReports = false;
        RenderFields(true);
    }

    /// <summary>
    /// Switches between the selected mission's presentation and report text.
    /// </summary>
    private void ToggleMissionReports()
    {
        if (!Commit())
            return;
        showMissionReports = !showMissionReports;
        RenderFields(true);
    }

    /// <summary>
    /// Shows a faction's two gameplay mission icons and its authored encyclopedia presentation.
    /// </summary>
    /// <param name="top">The starting offset below the mission picker.</param>
    private void RenderFactionMissionPresentation(int top = 0)
    {
        previewPane.SetActive(false);
        float width = fieldScroll.ScrollArea.ViewportWidth;
        int y = top,
            fieldIndex = 0,
            imageIndex = 0,
            labelIndex = 0,
            panelIndex = 0;
        ContentPackEditorField[] icons = ActiveMissionPage
            .Fields.Where(ContentEditorContentSections.IsFactionMissionIcon)
            .ToArray();
        if (icons.Length > 0)
        {
            y = BeginArtworkPanel(ref panelIndex, "MissionIcons", y, width);
            GetArtworkContentBounds(width, out float left, out float available);
            const int gap = 16;
            bool sideBySide = available >= 480;
            int columnWidth = Mathf.Min(
                320,
                Mathf.FloorToInt(sideBySide ? (available - gap) / 2 : available)
            );
            int iconBottom = y;
            for (int index = 0; index < icons.Length; index++)
            {
                ContentPackEditorField icon = icons[index];
                float x = left + (sideBySide ? index * (columnWidth + gap) : 0);
                int rowTop = sideBySide ? y : iconBottom;
                rowTop += PlaceLabel(icon.Caption, labelIndex++, x, rowTop, columnWidth, true) + 6;
                if (!string.IsNullOrWhiteSpace(icon.Value))
                    rowTop = PlaceArtworkImagePreview(
                        icon,
                        x,
                        rowTop,
                        columnWidth,
                        columnWidth - 8,
                        115,
                        ref imageIndex,
                        ref labelIndex
                    );
                iconBottom = Mathf.Max(
                    iconBottom,
                    rowTop + PlaceField(icon, fieldIndex++, x, rowTop, columnWidth, 0, 0, true) + 8
                );
            }
            y = CompleteArtworkPanel(top, iconBottom);
        }
        ContentPackEditorField[] article = ActiveMissionPage
            .Fields.Where(field => field.Record.Kind == "EncyclopediaEntry")
            .ToArray();
        if (article.Length > 0)
        {
            int start = y;
            y = BeginArtworkPanel(ref panelIndex, "MissionEncyclopedia", y, width);
            y += PlaceLabel("Encyclopedia", labelIndex++, 0, y, width, true) + 8;
            y = PlaceArtworkFieldGroup(
                article,
                y,
                width,
                ref fieldIndex,
                ref labelIndex,
                ref imageIndex
            );
            y = CompleteArtworkPanel(start, y);
        }
        else if (ActiveMissionPage.MissionArticleCatalog != null)
        {
            addMessageButton.gameObject.SetActive(true);
            addMessageButton.name = "AddMissionArticle";
            addMessageButton.GetComponentInChildren<TMP_Text>().text = "Add encyclopedia entry";
            UILayout.SetSourceRect(
                (RectTransform)addMessageButton.transform,
                4,
                y,
                Mathf.RoundToInt(Mathf.Min(220, width - 8)),
                34
            );
            y += 42;
        }
        fieldScroll.SetContentHeight(y, !reflowingFields);
    }

    /// <summary>
    /// Displays the shared report and the editor for an optional faction-specific copy.
    /// </summary>
    /// <param name="top">The starting offset below the mission picker.</param>
    private void RenderMissionReportText(int top = 4)
    {
        previewPane.SetActive(false);
        float width = Mathf.Min(1024, fieldScroll.ScrollArea.ViewportWidth);
        ContentEditorMissionReport[] reports = ActiveMissionPage.MissionReports.ToArray();
        int index = Math.Max(
            0,
            Array.FindIndex(reports, report => report.Key == selectedMissionReportKey)
        );
        ContentEditorMissionReport selected = reports[index];
        selectedMissionReportKey = selected.Key;
        int heading = 0,
            fieldIndex = 0;
        int y = top;
        y += PlaceLabel("Outcome", heading++, 4, y, width - 8, true);
        int dropdownWidth = Mathf.FloorToInt(Mathf.Min(350, width - 8));
        bool actionAlongside = width >= dropdownWidth + 256;
        collectionDropdown.gameObject.SetActive(true);
        collectionDropdown.ClearOptions();
        collectionDropdown.AddOptions(reports.Select(report => report.Caption).ToList());
        collectionDropdown.SetValueWithoutNotify(index);
        collectionDropdown.RefreshShownValue();
        UILayout.SetSourceRect(
            (RectTransform)collectionDropdown.transform,
            4,
            y,
            dropdownWidth,
            34
        );

        if (selected.Shared != null)
        {
            bool customize = selected.Custom == null;
            addMessageButton.gameObject.SetActive(true);
            addMessageButton.name = customize ? "CustomizeEspionageReport" : "ResetEspionageReport";
            addMessageButton.GetComponentInChildren<TMP_Text>().text = customize
                ? "Customize for this faction"
                : "Use shared report";
            UILayout.SetSourceRect(
                (RectTransform)addMessageButton.transform,
                actionAlongside ? dropdownWidth + 16 : 4,
                actionAlongside ? y : y + 42,
                Mathf.FloorToInt(Mathf.Min(240, width - 8)),
                34
            );
            if (!actionAlongside)
                y += 42;
        }
        y += 48;

        if (selected.Custom == null)
        {
            y += PlaceLabel("Shared report template", heading++, 4, y, width - 8, true);
            string subject = (string)selected.Shared.Element.Element("Subject");
            if (!string.IsNullOrEmpty(subject))
                y += PlaceLabel(subject, heading++, 4, y, width - 8, true);
            string body = (string)selected.Shared.Element.Element("Body");
            if (!string.IsNullOrEmpty(body))
                y += PlaceLabel(body, heading++, 4, y, width - 8, false);
        }
        else
        {
            foreach (ContentPackEditorField field in selected.Fields)
            {
                bool stacked = field.IsMultiline || width < 480;
                if (stacked)
                    y += PlaceLabel(field.Caption, heading++, 4, y, width - 8, false);
                int height =
                    field.MemberName is "Body" or "UnattributedBody" ? 112
                    : field.IsMultiline ? 80
                    : 46;
                y += PlaceField(
                    field,
                    fieldIndex++,
                    4,
                    y,
                    width - 8,
                    stacked ? 0 : Mathf.Min(280, width * 0.4f),
                    height
                );
            }
        }
        fieldScroll.SetContentHeight(y + 12, !reflowingFields);
    }

    /// <summary>
    /// Presents one lazy faction-artwork surface at a time beneath a reusable surface selector.
    /// </summary>
    private void RenderFactionArtwork()
    {
        previewPane.SetActive(false);
        float width = fieldScroll.ScrollArea.ViewportWidth;
        bool neutral = selectedPage.Key == ContentEditorContentSections.NeutralThemePageKey;
        IEnumerable<string> categoryNames = ContentEditorContentSections.FactionArtworkCategories;
        if (neutral)
            categoryNames = categoryNames
                .Concat(
                    new[]
                    {
                        "Galaxy map icons",
                        "Other artwork",
                        ContentEditorContentSections.FacilityResearchIconsArtworkCategory,
                    }
                )
                .OrderBy(name => name, StringComparer.Ordinal);
        var categories = categoryNames
            .Select(name => new
            {
                Name = name,
                Fields = selectedPage
                    .Fields.Where(field =>
                        ContentEditorContentSections.IsFactionInterfaceField(field, neutral)
                        && ContentEditorContentSections.FactionArtworkCategory(field, neutral)
                            == name
                    )
                    .ToArray(),
            })
            .Where(category => category.Fields.Length > 0)
            .ToArray();
        int y = 4,
            fieldIndex = 0,
            labelIndex = 0,
            imageIndex = 0;
        if (categories.Length == 0)
        {
            y += PlaceLabel(
                "No faction artwork is configured in this theme.",
                labelIndex++,
                4,
                y,
                width - 8,
                false
            );
            fieldScroll.SetContentHeight(y + 20, !reflowingFields);
            return;
        }

        if (!categories.Any(category => category.Name == selectedFactionArtworkCategory))
            selectedFactionArtworkCategory =
                neutral
                && categories.Any(category => category.Name == _defaultFactionArtworkCategory)
                    ? _defaultFactionArtworkCategory
                    : categories[0].Name;
        y = PlaceFactionArtworkViewSelector(
            categories.Select(category => category.Name).ToArray(),
            y,
            width
        );
        var category = categories.Single(category =>
            category.Name == selectedFactionArtworkCategory
        );
        bool sector = category.Name == _defaultFactionArtworkCategory;
        bool window = artworkWindowPreview.Supports(category.Name);
        y +=
            PlaceLabel(
                neutral && sector
                    ? "Facility and defense icons for neutral planets, plus bookmark artwork."
                    : ContentEditorContentSections.FactionArtworkCategoryDescription(category.Name),
                labelIndex++,
                4,
                y,
                width - 8,
                false
            ) + 12;
        if (sector || window)
            y +=
                PlaceLabel(
                    category.Name == "Strategy view"
                            ? "Scroll to zoom. Drag to pan or move artwork. Alt/Option + drag moves a custom galaxy background."
                        : neutral && sector
                            ? "Double-click an outlined icon to open its image settings below."
                        : "Double-click outlined artwork to edit it. Drag artwork with placement controls to move it. Press and hold a control to see its pressed artwork.",
                    labelIndex++,
                    4,
                    y,
                    width - 8,
                    false
                ) + 10;
        IReadOnlyList<ContentPackEditorField> renderedFields;
        if (sector)
        {
            y = PlaceStrategyArtworkPreview(category.Fields, y, width, ref labelIndex, neutral);
            renderedFields = artworkSectorPreview.RenderedFields;
        }
        else if (window)
        {
            y = PlaceArtworkWindowPreview(category.Name, y, width, ref labelIndex);
            renderedFields = artworkWindowPreview
                .RenderedFields.Concat(artworkDisabledWindowPreview.RenderedFields)
                .Distinct()
                .ToArray();
        }
        else
            renderedFields = Array.Empty<ContentPackEditorField>();

        ContentPackEditorField[] visibleFields = category
            .Fields.Where(field => renderedFields.Any(rendered => SameField(rendered, field)))
            .ToArray();
        if (visibleFields.Length == 0 && !neutral)
        {
            y += PlaceLabel(
                "This preview does not contain a faction-owned editable image in its current state.",
                labelIndex++,
                4,
                y,
                width - 8,
                false
            );
        }

        ContentPackEditorField[] fields = neutral
            ? visibleFields.Concat(category.Fields.Except(visibleFields)).ToArray()
            : visibleFields;
        IReadOnlyList<string> previousSections = Array.Empty<string>();
        for (int index = 0; index < fields.Length; index++)
        {
            if (
                neutral
                && index == visibleFields.Length
                && visibleFields.Length > 0
                && (sector || window)
            )
            {
                y +=
                    PlaceLabel("More artwork for this view", labelIndex++, 4, y, width - 8, true)
                    + 8;
                previousSections = Array.Empty<string>();
            }
            ContentPackEditorField field = fields[index];
            IReadOnlyList<string> sections = ContentEditorContentSections.FactionArtworkSections(
                field
            );
            y = Mathf.CeilToInt(
                PlaceSections(sections, previousSections, ref labelIndex, 4, y, width - 8)
            );
            previousSections = sections;
            y = PlaceFactionArtworkField(
                field,
                y,
                width,
                ref fieldIndex,
                ref labelIndex,
                ref imageIndex
            );
        }
        fieldScroll.SetContentHeight(y + 20, !reflowingFields);
    }

    /// <summary>
    /// Reports whether two projected field objects refer to the same XML value.
    /// </summary>
    /// <param name="left">The first projected field.</param>
    /// <param name="right">The second projected field.</param>
    /// <returns>True when both fields own the same element or attribute.</returns>
    private static bool SameField(ContentPackEditorField left, ContentPackEditorField right) =>
        left != null
        && right != null
        && left.XmlElement == right.XmlElement
        && left.XmlAttributeName == right.XmlAttributeName;

    /// <summary>
    /// Matches a faction color across form renders, including colors whose XML is still optional.
    /// </summary>
    /// <param name="left">The first projected setting.</param>
    /// <param name="right">The second projected setting.</param>
    /// <returns>True when both fields represent the same faction color.</returns>
    private static bool SameFactionColor(
        ContentPackEditorField left,
        ContentPackEditorField right
    ) =>
        left?.Group == nameof(FactionTheme.ColorScheme)
        && right?.Group == nameof(FactionTheme.ColorScheme)
        && left.Record.Key == right.Record.Key
        && left.MemberName == right.MemberName;

    /// <summary>
    /// Places the dropdown for the artwork surfaces available in the selected theme.
    /// </summary>
    /// <param name="categories">The available surface names in display order.</param>
    /// <param name="y">The selector position.</param>
    /// <param name="width">The available browser width.</param>
    /// <returns>The first position below the selector.</returns>
    private int PlaceFactionArtworkViewSelector(
        IReadOnlyList<string> categories,
        int y,
        float width
    )
    {
        const int height = 38;
        int selectorWidth = Mathf.Min(520, Mathf.Max(1, Mathf.FloorToInt(width - 8)));
        int selectorX = Mathf.Max(4, Mathf.RoundToInt((width - selectorWidth) * 0.5f));
        UILayout.SetSourceRect(
            (RectTransform)artworkViewSelector.transform,
            selectorX,
            y,
            selectorWidth,
            height
        );
        ArtworkViewOptions = categories;
        List<string> options = categories.ToList();
        artworkViewSelector.ClearOptions();
        artworkViewSelector.AddOptions(options);
        artworkViewSelector.SetValueWithoutNotify(options.IndexOf(selectedFactionArtworkCategory));
        artworkViewSelector.RefreshShownValue();
        artworkViewSelector.gameObject.SetActive(true);
        return y + height + 8;
    }

    /// <summary>
    /// Renders a representative authored sector through the exact gameplay window presentation.
    /// </summary>
    /// <param name="editableFields">The selected strategy-surface fields shown in the preview.</param>
    /// <param name="y">The next browser position.</param>
    /// <param name="width">The available browser width.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <param name="neutral">Whether the page already explains editing above the sector.</param>
    /// <returns>The first position below the composed preview and its instruction.</returns>
    private int PlaceStrategyArtworkPreview(
        IReadOnlyList<ContentPackEditorField> editableFields,
        int y,
        float width,
        ref int labelIndex,
        bool neutral
    )
    {
        ContentPackEditorRecord sector = catalogs
            .Where(catalog => catalog.IsMap)
            .SelectMany(catalog => catalog.Records)
            .Where(record => record.Kind == "PlanetSector" && record.Planets.Count > 0)
            .Where(record =>
                record.Planets.All(planet =>
                    planet
                        .Element.Element("PlanetIconPath")
                        ?.Value.Contains("asteroid", StringComparison.OrdinalIgnoreCase) != true
                )
            )
            .OrderBy(record => record.Planets.Count < 3)
            .ThenBy(record => record.Planets.Count)
            .ThenBy(record => record.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        ContentPackEditorRecord theme = selectedPage.RelatedRecords.FirstOrDefault(record =>
            record.Kind == "FactionTheme"
        );
        if (sector == null || theme == null)
        {
            return y
                + PlaceLabel(
                    "Add a planet sector to preview these images together.",
                    labelIndex++,
                    4,
                    y,
                    width - 8,
                    false
                )
                + 12;
        }

        int availableWidth = Mathf.Max(1, Mathf.FloorToInt(width - 8));
        int previewWidth = Mathf.Min(520, availableWidth);
        int previewHeight = Mathf.RoundToInt(previewWidth / artworkSectorPreview.AspectRatio);
        int x = Mathf.Max(4, Mathf.RoundToInt((width - previewWidth) * 0.5f));
        UILayout.SetSourceRect(
            (RectTransform)artworkSectorPreview.transform,
            x,
            y,
            previewWidth,
            previewHeight
        );
        artworkSectorPreview.gameObject.SetActive(true);
        try
        {
            FactionTheme factionTheme = ContentEditorPresentation.GetFactionTheme(theme);
            PlaceStrategyBackdrop(neutral ? null : factionTheme);
            artworkSectorPreview.Render(project, sector, assets, factionTheme, editableFields);
        }
        catch (ArgumentException)
        {
            artworkSectorPreview.Clear();
            artworkSectorPreview.gameObject.SetActive(false);
            return y
                + PlaceLabel(
                    "The composed sector preview contains an image path that could not be loaded.",
                    labelIndex++,
                    4,
                    y,
                    width - 8,
                    false
                )
                + 12;
        }
        y += previewHeight + 8;
        if (neutral)
            return y + 16;
        return y
            + PlaceLabel(
                "Hover an image to outline it, then double-click to open the exact field supplying that planet, overlay, or background.",
                labelIndex++,
                4,
                y,
                width - 8,
                false
            )
            + 16;
    }

    /// <summary>
    /// Renders the exact gameplay window or related window group for an artwork category.
    /// </summary>
    /// <param name="category">The selected faction-artwork category.</param>
    /// <param name="y">The next browser position.</param>
    /// <param name="width">The available browser width.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <returns>The first position below the enlarged preview and its instruction.</returns>
    private int PlaceArtworkWindowPreview(string category, int y, float width, ref int labelIndex)
    {
        ContentPackEditorRecord theme = selectedPage.RelatedRecords.FirstOrDefault(record =>
            record.Kind == "FactionTheme"
        );
        if (theme == null)
            return y;

        int zoomToolbarTop = y;
        if (category == "Strategy view")
            y += 42;

        ContentPackEditorField[] themeFields = theme
            .GetFields()
            .Concat(selectedPage.Fields.Where(field => field.Record.Kind == "FactionTheme"))
            .ToArray();
        bool neutral = selectedPage.Key == ContentEditorContentSections.NeutralThemePageKey;
        ContentPackEditorField[] surfaceFields = themeFields
            .Where(field =>
                ContentEditorContentSections.IsFactionInterfaceField(field, neutral)
                && ContentEditorContentSections.FactionArtworkCategory(field, neutral) == category
            )
            .ToArray();
        bool hasConfiguredDisabledArtwork = surfaceFields.Any(field =>
            field.MemberName == "DisabledImagePath"
        );
        int availableWidth = Mathf.Max(1, Mathf.FloorToInt(width - 8));
        const int gap = 16;
        int availableHeight = Mathf.Max(
            240,
            Mathf.FloorToInt(fieldScroll.ScrollArea.ViewportHeight - y - 96)
        );
        try
        {
            FactionTheme factionTheme = ContentEditorPresentation.GetFactionTheme(theme);
            if (hasConfiguredDisabledArtwork)
            {
                Vector2Int inspectionSize = artworkWindowPreview.GetPreferredSize(
                    category,
                    availableWidth,
                    availableHeight
                );
                if (inspectionSize == Vector2Int.zero)
                    return y;
                UILayout.SetSourceRect(
                    (RectTransform)artworkWindowPreview.transform,
                    4,
                    y,
                    inspectionSize.x,
                    inspectionSize.y
                );
                artworkWindowPreview.Render(
                    category,
                    project,
                    factionTheme,
                    assets,
                    themeFields,
                    surfaceFields
                );
            }

            bool hasDisabledArtwork =
                hasConfiguredDisabledArtwork
                && artworkWindowPreview.CanRenderState(
                    surfaceFields,
                    assets,
                    ContentEditorArtworkPreviewState.Disabled
                );
            int columnWidth = hasDisabledArtwork
                ? Mathf.Max(1, (availableWidth - gap) / 2)
                : availableWidth;
            Vector2Int previewSize = artworkWindowPreview.GetPreferredSize(
                category,
                columnWidth,
                availableHeight
            );
            if (previewSize == Vector2Int.zero)
                return y;
            Vector2Int disabledSize = hasDisabledArtwork
                ? artworkDisabledWindowPreview.GetPreferredSize(
                    category,
                    columnWidth,
                    availableHeight
                )
                : Vector2Int.zero;
            int previewY = y;
            int groupWidth = previewSize.x + (hasDisabledArtwork ? gap + disabledSize.x : 0);
            int firstColumnX = Mathf.Max(4, Mathf.RoundToInt((width - groupWidth) * 0.5f));
            int secondColumnX = firstColumnX + previewSize.x + gap;
            if (hasDisabledArtwork)
            {
                int labelHeight = PlaceLabel(
                    "Default",
                    labelIndex++,
                    firstColumnX,
                    y,
                    previewSize.x,
                    true
                );
                labelHeight = Mathf.Max(
                    labelHeight,
                    PlaceLabel("Disabled", labelIndex++, secondColumnX, y, disabledSize.x, true)
                );
                previewY += labelHeight + 4;
            }
            UILayout.SetSourceRect(
                (RectTransform)artworkWindowPreview.transform,
                firstColumnX,
                previewY,
                previewSize.x,
                previewSize.y
            );
            artworkWindowPreview.Render(
                category,
                project,
                factionTheme,
                assets,
                themeFields,
                surfaceFields
            );
            artworkWindowPreview.PlaceZoomControls(zoomToolbarTop, Mathf.FloorToInt(width));
            if (hasDisabledArtwork)
            {
                UILayout.SetSourceRect(
                    (RectTransform)artworkDisabledWindowPreview.transform,
                    secondColumnX,
                    previewY,
                    disabledSize.x,
                    disabledSize.y
                );
                artworkDisabledWindowPreview.Render(
                    category,
                    project,
                    factionTheme,
                    assets,
                    themeFields,
                    surfaceFields,
                    ContentEditorArtworkPreviewState.Disabled
                );
                previewSize.y = Mathf.Max(previewSize.y, disabledSize.y);
            }
            else
                artworkDisabledWindowPreview.Clear();

            y = previewY + previewSize.y + 8;
        }
        catch (Exception exception)
            when (exception is ArgumentException || exception is MissingMemberException)
        {
            artworkWindowPreview.Clear();
            artworkDisabledWindowPreview.Clear();
            return y
                + PlaceLabel(
                    "The gameplay-window preview contains an image path that could not be loaded.",
                    labelIndex++,
                    4,
                    y,
                    width - 8,
                    false
                )
                + 12;
        }

        return y
            + PlaceLabel(
                "Choose an image to copy it into this pack.",
                labelIndex++,
                4,
                y,
                width - 8,
                false
            )
            + 16;
    }

    /// <summary>
    /// Places the faction's galaxy artwork behind the translucent sector window without distortion.
    /// </summary>
    /// <param name="theme">The selected faction presentation, when a player backdrop applies.</param>
    private void PlaceStrategyBackdrop(FactionTheme theme)
    {
        RawImage backdrop = artworkSectorPreview.GetComponent<RawImage>();
        string address = theme?.GalaxyBackground?.ImagePath;
        Texture2D texture = string.IsNullOrWhiteSpace(address) ? null : assets.GetTexture(address);
        backdrop.texture = texture;
        backdrop.color = texture == null ? Color.black : Color.white;
        backdrop.uvRect = new Rect(0, 0, 1, 1);
        if (texture == null)
            return;
        Rect bounds = ((RectTransform)artworkSectorPreview.transform).rect;
        float targetAspect = bounds.width / bounds.height;
        float textureAspect = (float)texture.width / texture.height;
        if (textureAspect > targetAspect)
        {
            float visibleWidth = targetAspect / textureAspect;
            backdrop.uvRect = new Rect((1 - visibleWidth) * 0.5f, 0, visibleWidth, 1);
        }
        else if (textureAspect < targetAspect)
        {
            float visibleHeight = textureAspect / targetAspect;
            backdrop.uvRect = new Rect(0, (1 - visibleHeight) * 0.5f, 1, visibleHeight);
        }
    }

    /// <summary>
    /// Switches the lazy artwork surface after accepting edits in the current one.
    /// </summary>
    /// <param name="index">The interface selected in the dropdown.</param>
    private void SelectFactionArtworkCategory(int index)
    {
        if (index < 0 || index >= ArtworkViewOptions.Count)
            return;
        string category = ArtworkViewOptions[index];
        if (!Commit())
        {
            artworkViewSelector.SetValueWithoutNotify(
                artworkViewSelector.options.FindIndex(option =>
                    option.text == selectedFactionArtworkCategory
                )
            );
            return;
        }
        selectedFactionArtworkCategory = category;
        RenderFields(true);
    }

    /// <summary>
    /// Reveals the image field represented by an exact gameplay-window preview element.
    /// </summary>
    /// <param name="field">The exact field supplying the clicked rendered image.</param>
    private void OpenWindowArtwork(ContentPackEditorField field)
    {
        if (field == null)
            return;
        ContentPackEditorField current = selectedPage?.Fields.FirstOrDefault(candidate =>
            SameField(candidate, field) || SameFactionColor(candidate, field)
        );
        if (current == null)
        {
            AssetEditRequested?.Invoke(field);
            return;
        }
        if (current.Group == nameof(FactionTheme.ColorScheme))
        {
            SectionRequested?.Invoke("Colors");
            FocusColorPicker(current);
            return;
        }
        string category = ContentEditorContentSections.FactionArtworkCategory(
            current,
            selectedPage.Key == ContentEditorContentSections.NeutralThemePageKey
        );
        if (category != selectedFactionArtworkCategory)
        {
            selectedFactionArtworkCategory = category;
            RenderFields(true);
        }
        FocusField(current);
    }

    /// <summary>
    /// Forwards one composed-preview placement gesture to the project-owning workspace.
    /// </summary>
    /// <param name="changes">The authored source coordinates produced by the drag.</param>
    private void MoveWindowArtwork(IReadOnlyList<ContentEditorArtworkPlacementChange> changes) =>
        ArtworkPlacementSubmitted?.Invoke(changes);

    /// <summary>
    /// Hides the artwork selector and releases composed-preview project references.
    /// </summary>
    private void ClearArtworkViewControls()
    {
        factionArtworkFieldTops.Clear();
        artworkViewSelector.gameObject.SetActive(false);
        ArtworkViewOptions = Array.Empty<string>();
        artworkSectorPreview.Clear();
        RawImage backdrop = artworkSectorPreview.GetComponent<RawImage>();
        backdrop.texture = null;
        backdrop.uvRect = new Rect(0, 0, 1, 1);
        artworkSectorPreview.gameObject.SetActive(false);
        artworkWindowPreview.Clear();
        artworkDisabledWindowPreview.Clear();
    }

    /// <summary>
    /// Places one faction artwork entry as a compact thumbnail-and-path row when space permits.
    /// </summary>
    /// <param name="field">The faction artwork field.</param>
    /// <param name="y">The next vertical form position.</param>
    /// <param name="width">The available browser width.</param>
    /// <param name="fieldIndex">The next reusable field view.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <param name="imageIndex">The next reusable thumbnail.</param>
    /// <returns>The next vertical position.</returns>
    private int PlaceFactionArtworkField(
        ContentPackEditorField field,
        int y,
        float width,
        ref int fieldIndex,
        ref int labelIndex,
        ref int imageIndex
    )
    {
        factionArtworkFieldTops[field.XmlElement] = y;
        if (ContentEditorContentSections.IsAdvisorPlacementField(field))
        {
            string portrait =
                field.XmlElement.Parent.Name.LocalName
                == nameof(StrategyAdvisorTheme.ProtocolSourceLayout)
                    ? "Advisor"
                    : "Droid";
            string coordinate =
                field.MemberName == nameof(SourceRectLayout.X) ? "horizontal" : "vertical";
            y += PlaceLabel(
                $"{portrait} {coordinate} position",
                labelIndex++,
                4,
                y,
                width - 8,
                false
            );
            return y + PlaceField(field, fieldIndex++, 4, y, Mathf.Min(200, width - 8), 0) + 10;
        }
        y += PlaceLabel(field.Caption, labelIndex++, 4, y, width - 8, false, field.HelpText);
        if (width >= 900 && !field.IsMediaDirectory)
        {
            int rowTop = y;
            float previewWidth = Mathf.Min(300, Mathf.Max(220, width - 640));
            int previewBottom = PlaceArtworkImagePreview(
                field,
                4,
                rowTop,
                previewWidth,
                300,
                140,
                ref imageIndex,
                ref labelIndex
            );
            float fieldX = previewWidth + 16;
            int fieldHeight = PlaceField(field, fieldIndex++, fieldX, rowTop, width - fieldX, 0);
            return Mathf.Max(previewBottom, rowTop + fieldHeight) + 12;
        }
        y = PlaceArtworkImagePreview(field, 4, y, width, 360, 160, ref imageIndex, ref labelIndex);
        y += PlaceField(field, fieldIndex++, 0, y, width, 0);
        return y + 12;
    }

    /// <summary>
    /// Presents related definitions as one page with faction artwork alongside their editable fields.
    /// </summary>
    private void RenderCombinedPage()
    {
        previewPane.SetActive(false);
        float width = fieldScroll.ScrollArea.ViewportWidth;
        int y = 0,
            fieldIndex = 0,
            headingIndex = 0,
            imageIndex = 0;
        bool collection =
            selectedPage.Key.StartsWith("factions/", StringComparison.Ordinal)
            && section is "Messages" or "Ship names";
        string itemName = section == "Messages" ? "Message" : "NamePool";
        if (collection)
        {
            collectionItems = selectedPage
                .Fields.Select(field =>
                    field
                        .XmlElement.AncestorsAndSelf()
                        .FirstOrDefault(element => element.Name.LocalName == itemName)
                )
                .Where(item => item != null)
                .Distinct()
                .ToArray();
            if (!collectionItems.Contains(selectedCollection))
                selectedCollection = collectionItems.FirstOrDefault();
            addMessageButton.gameObject.SetActive(true);
            addMessageButton.GetComponentInChildren<TMP_Text>().text =
                section == "Messages" ? "Add starting message" : "Add ship name pool";
            addMessageButton.gameObject.name =
                section == "Messages" ? "AddFactionMessage" : "AddShipNamePool";
            UILayout.SetSourceRect((RectTransform)addMessageButton.transform, 4, 0, 190, 32);
            y =
                44
                + PlaceLabel(
                    section == "Messages"
                        ? "Messages shown when a new game starts."
                        : "Select a pool to edit its ship names.",
                    headingIndex++,
                    4,
                    44,
                    width - 8,
                    false
                );
            if (collectionItems.Length > 0)
            {
                collectionDropdown.gameObject.SetActive(true);
                collectionDropdown.ClearOptions();
                collectionDropdown.AddOptions(
                    collectionItems
                        .Select(item =>
                            itemName == "Message"
                                ? (
                                    item.Element("Title")?.Value is { Length: > 0 } title
                                        ? title
                                        : "Untitled message"
                                )
                                : "Pool " + item.Element("NamePoolID")?.Value
                        )
                        .ToList()
                );
                collectionDropdown.SetValueWithoutNotify(
                    Array.IndexOf(collectionItems, selectedCollection)
                );
                collectionDropdown.RefreshShownValue();
                UILayout.SetSourceRect(
                    (RectTransform)collectionDropdown.transform,
                    4,
                    y,
                    Mathf.RoundToInt(width - (section == "Messages" ? 152 : 8)),
                    34
                );
                removeMessageButton.gameObject.SetActive(section == "Messages");
                UILayout.SetSourceRect(
                    (RectTransform)removeMessageButton.transform,
                    Mathf.RoundToInt(width - 140),
                    y,
                    132,
                    34
                );
                y += 50;
            }
            else
                y += PlaceLabel(
                    section == "Messages"
                        ? "No starting messages. Add one to get started."
                        : "No ship name pools. Add one to get started.",
                    headingIndex++,
                    4,
                    y,
                    width - 8,
                    false
                );
        }
        foreach (
            IGrouping<ContentPackEditorRecord, ContentPackEditorField> group in selectedPage
                .Fields.Where(field =>
                    (selectedPage.MissionID == null || field.Record.Kind != "GameConfig")
                    && (
                        !collection
                        || field.XmlElement.AncestorsAndSelf().Contains(selectedCollection)
                    )
                    && (
                        !selectedPage.Key.StartsWith("factions/", StringComparison.Ordinal)
                        || ContentEditorContentSections.FactionSection(field) == section
                    )
                )
                .GroupBy(field => field.Record)
        )
        {
            ContentPackEditorRecord record = group.Key;
            bool article = record.Kind == "EncyclopediaEntry";
            string title =
                article
                    ? (selectedPage.MissionID == null ? record.Name : record.Catalog.Faction)
                        + " encyclopedia"
                : record.Kind == "GameConfig" ? "Mission rules"
                : record.Kind == "FactionTheme" ? "Theme settings"
                : record.Element.Element("FactionDataPath") != null ? "Content files"
                : section;
            if (article || record.Kind != "Faction")
                y += PlaceLabel(title, headingIndex++, 4, y, width - 8, true) + 8;
            if (article && !string.IsNullOrEmpty(record.ImageAddress))
            {
                Texture2D texture = assets.GetTexture(record.ImageAddress);
                if (texture != null)
                {
                    RawImage preview = GetArtworkPreviewImage(
                        ref imageIndex,
                        "Encyclopedia-" + record.ID
                    );
                    preview.texture = texture;
                    Vector2Int source = UILayout.GetTextureSourceSize(preview.texture);
                    int imageWidth = Mathf.Min(Mathf.RoundToInt(width - 8), 440);
                    int imageHeight = Mathf.RoundToInt((float)imageWidth * source.y / source.x);
                    UILayout.SetSourceRect(preview.rectTransform, 4, y, imageWidth, imageHeight);
                    preview.enabled = true;
                    preview.gameObject.SetActive(true);
                    y += imageHeight + 12;
                }
            }
            IReadOnlyDictionary<XElement, string> poolTitles = GetNamePoolTitles(record);
            IReadOnlyList<string> previous = Array.Empty<string>();
            foreach (ContentPackEditorField field in group)
            {
                if (
                    article
                    && field.MemberName is "TypeID" or "Category" or "VisibleFactionInstanceID"
                )
                    continue;
                IReadOnlyList<string> sections = collection
                    ? Array.Empty<string>()
                    : field.SectionPath;
                if (record.Kind == "FactionTheme")
                    sections = Array.Empty<string>();
                if (
                    selectedPage.Key.StartsWith("factions/", StringComparison.Ordinal)
                    && field.Group is "Settings" or "ShipNamePools" or "Messages"
                )
                    sections = sections.Skip(1).ToArray();
                XElement pool = field
                    .XmlElement.AncestorsAndSelf()
                    .FirstOrDefault(element => element.Name.LocalName == "NamePool");
                if (pool != null && poolTitles.TryGetValue(pool, out string poolTitle))
                    sections = sections
                        .Select(part =>
                            part == "Pool " + pool.Element("NamePoolID")?.Value ? poolTitle : part
                        )
                        .ToArray();
                if (
                    record.Kind == "GameConfig"
                    && ContentPackEditorFieldMetadata.MissionFor(field) == null
                )
                    sections = new[] { "Shared mission rules" }.Concat(sections).ToArray();
                y = Mathf.CeilToInt(
                    PlaceSections(sections, previous, ref headingIndex, 4, y, width - 8)
                );
                previous = sections;
                bool stacked = field.IsMultiline || field.IsList || field.IsMedia || width < 480;
                if (stacked)
                    y += PlaceLabel(
                        field.Caption,
                        headingIndex++,
                        4,
                        y,
                        width - 8,
                        false,
                        field.HelpText
                    );
                if (section is "Artwork" or "Default backgrounds")
                    y = PlaceArtworkImagePreview(field, y, width, ref imageIndex, ref headingIndex);
                y += PlaceField(
                    field,
                    fieldIndex++,
                    0,
                    y,
                    width,
                    stacked ? 0 : Mathf.Min(280, width * 0.45f)
                );
            }
            if (collection && itemName == "NamePool")
                y = PlaceShipNameAssignment(
                    record,
                    y + 12,
                    width,
                    ref fieldIndex,
                    ref headingIndex
                );
            y += 24;
        }
        fieldScroll.SetContentHeight(y, !reflowingFields);
    }

    /// <summary>
    /// Selects a ship design and edits its assigned naming pool in the faction inspector.
    /// </summary>
    /// <param name="faction">The faction that owns the visible pools.</param>
    /// <param name="y">The next vertical position.</param>
    /// <param name="width">The available form width.</param>
    /// <param name="fieldIndex">The next pooled editor field.</param>
    /// <param name="headingIndex">The next pooled label.</param>
    /// <returns>The next vertical position below the assignment controls.</returns>
    private int PlaceShipNameAssignment(
        ContentPackEditorRecord faction,
        int y,
        float width,
        ref int fieldIndex,
        ref int headingIndex
    )
    {
        shipNameDesigns = GetNamePoolDesigns(faction)
            .OrderBy(ship => ship.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (shipNameDesigns.Length == 0)
            return y;

        int index = Array.FindIndex(shipNameDesigns, ship => ship.ID == selectedShipNameDesignID);
        if (index < 0)
        {
            string poolID = (string)selectedCollection.Element("NamePoolID");
            index = Math.Max(
                0,
                Array.FindIndex(
                    shipNameDesigns,
                    ship => (string)ship.Element.Element("ShipNamePoolID") == poolID
                )
            );
        }
        ContentPackEditorRecord design = shipNameDesigns[index];
        selectedShipNameDesignID = design.ID;

        y += PlaceLabel("Ship assignments", headingIndex++, 4, y, width - 8, true) + 8;
        const int gap = 16;
        bool split = width >= 620;
        int column = Mathf.FloorToInt(split ? Mathf.Min(440, (width - gap - 8) * 0.5f) : width - 8);
        int caption = PlaceLabel("Ship design", headingIndex++, 4, y, column, false);
        UILayout.SetSourceRect(
            (RectTransform)shipDesignDropdown.transform,
            4,
            y + caption + 4,
            column,
            32
        );
        shipDesignDropdown.ClearOptions();
        shipDesignDropdown.AddOptions(shipNameDesigns.Select(ship => ship.Name).ToList());
        shipDesignDropdown.SetValueWithoutNotify(index);
        shipDesignDropdown.RefreshShownValue();
        shipDesignDropdown.gameObject.SetActive(true);

        int poolX = split ? 4 + column + gap : 4;
        int poolY = split ? y : y + caption + 46;
        ContentPackEditorField assignment = design.GetField("ShipNamePoolID");
        poolY += PlaceLabel(
            "Name pool",
            headingIndex++,
            poolX,
            poolY,
            column,
            false,
            assignment.HelpText
        );
        Dictionary<string, string> poolChoices = new(StringComparer.Ordinal)
        {
            [string.Empty] = "None",
        };
        foreach (XElement pool in collectionItems)
            if ((string)pool.Element("NamePoolID") is { Length: > 0 } id)
                poolChoices[id] = "Pool " + id;
        return poolY
            + PlaceField(
                assignment,
                fieldIndex++,
                poolX - 4,
                poolY,
                column + 8,
                0,
                referenceLabels: poolChoices
            );
    }

    /// <summary>
    /// Binds the inline assignment field to the selected ship design.
    /// </summary>
    /// <param name="index">The ship selected in the assignment controls.</param>
    private void SelectShipNameDesign(int index)
    {
        if (!Commit())
        {
            shipDesignDropdown.SetValueWithoutNotify(
                Array.FindIndex(shipNameDesigns, ship => ship.ID == selectedShipNameDesignID)
            );
            return;
        }
        selectedShipNameDesignID = shipNameDesigns[index].ID;
        RenderFields(true);
    }

    /// <summary>
    /// Lists the ship designs eligible to use naming pools from this faction.
    /// </summary>
    /// <param name="faction">The faction that supplies the names.</param>
    /// <returns>The designs available to this faction.</returns>
    private IEnumerable<ContentPackEditorRecord> GetNamePoolDesigns(ContentPackEditorRecord faction)
    {
        string owner = (string)faction.Element.Element("InstanceID");
        return catalogs
            .Where(catalog => catalog.Name == "Capital ships")
            .SelectMany(catalog => catalog.Records)
            .Where(ship =>
            {
                XElement manufacturing = ship.Element.Element("ManufacturingFactionInstanceIDs");
                return manufacturing?.HasElements != true
                    || manufacturing.Elements().Any(id => id.Value == owner);
            });
    }

    /// <summary>
    /// Explains each authored ship-name pool using its current ship designs and fallbacks.
    /// </summary>
    /// <param name="record">The faction whose pools are being edited.</param>
    /// <returns>The usage captions keyed by their exact collection entries.</returns>
    private IReadOnlyDictionary<XElement, string> GetNamePoolTitles(ContentPackEditorRecord record)
    {
        XElement[] pools =
            record.Element.Element("ShipNamePools")?.Elements().ToArray()
            ?? Array.Empty<XElement>();
        ContentPackEditorRecord[] ships =
            pools.Length == 0
                ? Array.Empty<ContentPackEditorRecord>()
                : GetNamePoolDesigns(record).ToArray();
        return pools.ToDictionary(
            pool => pool,
            pool =>
            {
                string id = pool.Element("NamePoolID")?.Value;
                string[] names = ships
                    .Where(ship => ship.Element.Element("ShipNamePoolID")?.Value == id)
                    .Select(ship => ship.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                string[] fallbacks = pools
                    .Where(other => other.Element("FallbackNamePoolID")?.Value == id)
                    .Select(other => other.Element("NamePoolID")?.Value)
                    .Where(value => !string.IsNullOrEmpty(value))
                    .ToArray();
                string usage =
                    names.Length > 0
                        ? $"{names.Length} {(names.Length == 1 ? "ship design" : "ship designs")}"
                    : fallbacks.Length > 0
                        ? "Fallback for "
                            + string.Join(", ", fallbacks.Select(value => "pool " + value))
                    : "No ships assigned";
                return "Pool " + id + ": " + usage;
            }
        );
    }

    /// <summary>
    /// Presents one generation task using the shared compact configuration rows.
    /// </summary>
    private void RenderGenerationSettings()
    {
        previewPane.SetActive(false);
        var groups = ContentEditorGenerationLayout.Groups(section);
        int task = Mathf.Clamp(
            setupTasks.TryGetValue(section, out int selected) ? selected : 0,
            0,
            groups.Count - 1
        );
        setupTasks[section] = task;
        float width = Mathf.Min(1100, fieldScroll.ScrollArea.ViewportWidth);
        int y = 0,
            fieldIndex = 0,
            labelIndex = 0,
            collectionIndex = 0,
            unitIndex = 0;
        if (groups.Count > 1)
        {
            generationTask.ClearOptions();
            generationTask.AddOptions(groups.Select(group => group.Caption).ToList());
            generationTask.SetValueWithoutNotify(task);
            generationTask.RefreshShownValue();
            UILayout.SetSourceRect(
                (RectTransform)generationTask.transform,
                4,
                4,
                Mathf.RoundToInt(width - 8),
                34
            );
            generationTask.gameObject.SetActive(true);
            y = 54;
        }
        string[] path = groups[task].Path;
        XElement owner = selectedRecord.Element;
        foreach (string member in path.Take(path.Length - 1))
            owner = owner?.Element(member);
        string memberName = path.Last();
        Type ownerType =
            owner == selectedRecord.Element ? ContentPackEditorFieldSchema.GetRecordType(owner)
            : owner == null ? null
            : ContentPackEditorFieldSchema.GetValueType(selectedRecord.Element, owner);
        Type memberType =
            ownerType == null ? null : SerializationMetadata.GetElementType(ownerType, memberName);
        if (
            memberType != null
            && ContentPackEditorFieldSchema.GetItemType(memberType) is Type item
            && !TypeHelper.IsScalar(item)
        )
            RenderSetupCollection(
                project.GetCollection(selectedRecord, selectedRecord.Element, path),
                width,
                ref y,
                ref fieldIndex,
                ref labelIndex,
                ref collectionIndex,
                ref unitIndex
            );
        else if (owner?.Element(memberName) is XElement scope)
            RenderSetupObject(
                scope,
                width,
                ref y,
                ref fieldIndex,
                ref labelIndex,
                ref collectionIndex,
                ref unitIndex
            );
        fieldScroll.SetContentHeight(y, !reflowingFields);
    }

    /// <summary>
    /// Displays one selected object while keeping each child collection independently editable.
    /// </summary>
    /// <param name="owner">The selected setup entry.</param>
    /// <param name="width">Available form width.</param>
    /// <param name="y">Next vertical position.</param>
    /// <param name="fieldIndex">Next field slot.</param>
    /// <param name="labelIndex">Next heading slot.</param>
    /// <param name="collectionIndex">Next collection control slot.</param>
    /// <param name="unitIndex">Next unit-picker slot.</param>
    private void RenderSetupObject(
        XElement owner,
        float width,
        ref int y,
        ref int fieldIndex,
        ref int labelIndex,
        ref int collectionIndex,
        ref int unitIndex
    )
    {
        var collections = ContentEditorGenerationLayout.Collections(project, selectedRecord, owner);
        var fields = selectedRecord
            .GetFields(owner)
            .Where(field =>
                !collections.Any(collection =>
                    field.XmlElement.AncestorsAndSelf().Contains(collection.Element)
                )
            )
            .ToList();
        HashSet<XElement> ranges = new();
        foreach (var field in fields)
        {
            XElement parent = field.XmlElement.Parent;
            Type type =
                parent == null
                    ? null
                    : ContentPackEditorFieldSchema.GetValueType(selectedRecord.Element, parent);
            if (
                type == typeof(DiceFormula)
                || type == typeof(SupportFormula)
                || type == typeof(PlanetSizeProfile)
            )
            {
                if (!ranges.Add(parent))
                    continue;
                y += RenderRange(
                    fields.Where(term => term.XmlElement.Parent == parent).ToArray(),
                    parent.Name.LocalName == "NumStartingOfficers"
                        ? "Starting officer count"
                        : ContentPackEditorField.FormatName(parent.Name.LocalName),
                    0,
                    y,
                    width,
                    ref fieldIndex,
                    ref labelIndex,
                    true
                );
            }
            else
                y += PlaceField(field, fieldIndex++, 0, y, width, Mathf.Min(280, width * 0.45f));
        }
        foreach (var collection in collections)
            RenderSetupCollection(
                collection,
                width,
                ref y,
                ref fieldIndex,
                ref labelIndex,
                ref collectionIndex,
                ref unitIndex
            );
    }

    /// <summary>
    /// Shows collection selection and explicit creation, including quantity selection for starting units.
    /// </summary>
    /// <param name="collection">The authored collection.</param>
    /// <param name="width">Available form width.</param>
    /// <param name="y">Next vertical position.</param>
    /// <param name="fieldIndex">Next field slot.</param>
    /// <param name="labelIndex">Next heading slot.</param>
    /// <param name="collectionIndex">Next collection slot.</param>
    /// <param name="unitIndex">Next unit-picker slot.</param>
    private void RenderSetupCollection(
        ContentPackEditorCollection collection,
        float width,
        ref int y,
        ref int fieldIndex,
        ref int labelIndex,
        ref int collectionIndex,
        ref int unitIndex
    )
    {
        if (collection.ItemType == typeof(UnitEntry))
        {
            string title = collection.Name == "Cargo" ? "Cargo" : "Units";
            y += PlaceLabel(title, labelIndex++, 4, y + 8, width - 8, true) + 8;
            while (setupUnitButtons.Count <= unitIndex)
                setupUnitButtons.Add(
                    Instantiate(setupUnitsTemplate, fieldScroll.ScrollArea.ContentRoot)
                );
            Button button = setupUnitButtons[unitIndex++];
            button.GetComponentInChildren<TMP_Text>().text = "Choose units and quantities...";
            UILayout.SetSourceRect(
                (RectTransform)button.transform,
                4,
                y,
                Mathf.RoundToInt(width - 8),
                34
            );
            button.onClick.AddListener(() =>
            {
                if (Commit())
                    UnitSelectionRequested?.Invoke(collection);
            });
            button.gameObject.SetActive(true);
            y += 44;
            var options = project.GetCollectionUnitOptions(collection);
            string summary = string.Join(
                "\n",
                collection.Entries.Select(entry =>
                    (entry.Element("Count")?.Value ?? "0")
                    + " x "
                    + (
                        options.TryGetValue(
                            entry.Element("TypeID")?.Value ?? string.Empty,
                            out string name
                        )
                            ? name
                            : "Missing: " + entry.Element("TypeID")?.Value
                    )
                )
            );
            if (summary.Length > 0)
                y += PlaceLabel(summary, labelIndex++, 4, y, width - 8, false) + 12;
            return;
        }
        while (setupCollections.Count <= collectionIndex)
            setupCollections.Add(
                Instantiate(setupCollectionTemplate, fieldScroll.ScrollArea.ContentRoot)
            );
        CollectionEditorView view = setupCollections[collectionIndex++];
        int index = Mathf.Clamp(
            setupEntries.TryGetValue(collection.Key, out int current) ? current : 0,
            0,
            Math.Max(0, collection.Entries.Count - 1)
        );
        setupEntries[collection.Key] = index;
        UILayout.SetSourceRect((RectTransform)view.transform, 0, y, Mathf.RoundToInt(width), 120);
        view.Render(
            ContentEditorGenerationLayout.Caption(collection.Name),
            collection
                .Entries.Select(
                    (entry, ordinal) =>
                        ContentEditorGenerationLayout.EntryCaption(project, entry, ordinal)
                )
                .ToArray(),
            index,
            ContentEditorGenerationLayout.AddCaption(collection.Name)
        );
        view.Selected += value =>
        {
            if (Commit())
            {
                setupEntries[collection.Key] = value;
                Refresh();
            }
            else
                Refresh();
        };
        view.AddRequested += () =>
        {
            if (Commit())
            {
                setupEntries[collection.Key] = collection.Entries.Count;
                CollectionAddRequested?.Invoke(collection);
            }
        };
        view.RemoveRequested += () =>
        {
            if (Commit())
                CollectionRemoveRequested?.Invoke(collection, index);
        };
        y += 132;
        if (collection.Entries.Count > 0)
            RenderSetupObject(
                collection.Entries[index],
                width,
                ref y,
                ref fieldIndex,
                ref labelIndex,
                ref collectionIndex,
                ref unitIndex
            );
    }

    /// <summary>
    /// Switches the current setup task only after accepting pending form edits.
    /// </summary>
    /// <param name="index">The selected task.</param>
    private void SelectGenerationTask(int index)
    {
        if (Commit())
            setupTasks[section] = index;
        Refresh();
        PresentationChanged?.Invoke();
    }

    /// <summary>
    /// Refreshes structural edits while retaining the current scroll position.
    /// </summary>
    public void Refresh()
    {
        float offset = fieldScroll.ScrollArea.ContentRoot.anchoredPosition.y;
        RenderFields(true);
        fieldScroll.ScrollArea.RevealContentRect(offset, fieldScroll.ScrollArea.ViewportHeight);
    }

    /// <summary>
    /// Resets setup navigation when a different project is opened.
    /// </summary>
    public void ResetGenerationSelection()
    {
        setupTasks.Clear();
        setupEntries.Clear();
    }

    /// <summary>
    /// Releases pooled setup bindings before presenting another page.
    /// </summary>
    private void ClearSetupControls()
    {
        generationTask.gameObject.SetActive(false);
        if (artworkCutsButton != null)
        {
            artworkCutsButton.onClick.RemoveAllListeners();
            artworkCutsButton.gameObject.SetActive(false);
        }
        foreach (var view in setupCollections)
            view.Clear();
        foreach (var button in setupUnitButtons)
        {
            button.onClick.RemoveAllListeners();
            button.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Forwards a named reference selection request to the workspace.
    /// </summary>
    /// <param name="field">The list being edited.</param>
    private void RequestSelection(ContentPackEditorField field)
    {
        if (Commit())
            SelectionRequested?.Invoke(field);
    }

    /// <summary>
    /// Places a generation formula's base and independent random terms together on one readable row.
    /// </summary>
    /// <param name="terms">The formula's authored terms.</param>
    /// <param name="caption">The resource or support value generated by the formula.</param>
    /// <param name="x">The row's left edge.</param>
    /// <param name="y">The row's top edge.</param>
    /// <param name="width">The available width.</param>
    /// <param name="fieldIndex">The next reusable input.</param>
    /// <param name="labelIndex">The next reusable label.</param>
    /// <param name="showHeaders">Whether this is the first formula in the group.</param>
    /// <returns>The occupied height.</returns>
    private int RenderRange(
        ContentPackEditorField[] terms,
        string caption,
        float x,
        float y,
        float width,
        ref int fieldIndex,
        ref int labelIndex,
        bool showHeaders
    )
    {
        float captionWidth = Mathf.Min(280, width * 0.45f);
        float cellWidth = (width - captionWidth) / terms.Length;
        int headerHeight = showHeaders ? 28 : 0;
        int captionHeight = PlaceLabel(
            caption,
            labelIndex++,
            x + 4,
            y + headerHeight + 4,
            captionWidth - 12,
            false,
            ContentEditorGenerationLayout.GroupDescription(
                terms.FirstOrDefault()?.XmlElement.Parent?.Name.LocalName ?? string.Empty
            )
        );
        int fieldHeight = 44;
        for (int i = 0; i < terms.Length; i++)
        {
            ContentPackEditorField term = terms[i];
            float left = x + captionWidth + i * cellWidth;
            string name =
                term.MemberName is "Small" or "Medium" or "Large" ? term.MemberName
                : term.MemberName == "Base" ? "Base"
                : terms.Length == 2 ? "+ random"
                : "+ random " + i;
            if (showHeaders)
                PlaceLabel(name, labelIndex++, left + 4, y, cellWidth - 8, false, term.HelpText);
            fieldHeight = Mathf.Max(
                fieldHeight,
                PlaceField(term, fieldIndex++, left, y + headerHeight, cellWidth, 0, 44)
            );
        }
        return Mathf.Max(headerHeight + fieldHeight + 4, captionHeight + headerHeight + 8);
    }

    /// <summary>
    /// Presents only the selected mission's settings and success curve.
    /// </summary>
    private void RenderMissionRules()
    {
        previewPane.SetActive(false);
        ContentPackEditorField[] fields = selectedPage
            .Fields.Where(field => field.Record.Kind == "GameConfig")
            .ToArray();
        RenderRules(fields);
    }

    /// <summary>
    /// Presents a focused rules page with compact properties and numeric lookup tables.
    /// </summary>
    /// <param name="fields">The selected page's rules.</param>
    private void RenderRules(ContentPackEditorField[] fields)
    {
        previewPane.SetActive(false);
        ContentEditorNumericGrid grid = probabilityGrid ??= new(fields, selectedPage?.MissionID);
        float width = Mathf.Min(1100, fieldScroll.ScrollArea.ViewportWidth);
        int y = 0,
            fieldIndex = 0,
            groupIndex = 0;
        foreach (
            var group in grid.Settings.GroupBy(field =>
                field.XmlElement.Parent?.Parent?.Name == "TickRanges"
                    ? "Timing"
                    : string.Join(
                        " / ",
                        field
                            .SectionPath.Skip(1)
                            .Where(part =>
                                part is not "Value" and not "Game modifier" and not "Mission"
                            )
                    )
            )
        )
            RenderSettings(
                string.IsNullOrEmpty(group.Key) ? "Settings" : group.Key,
                group,
                width,
                ref y,
                ref fieldIndex,
                ref groupIndex
            );
        if (grid.Columns.Count > 0)
        {
            string keyCaption = grid.IsProbability ? "Score" : "Threshold";
            float keyWidth = Mathf.Ceil(groupTemplate.GetPreferredValues(keyCaption).x) + 16;
            float availableWidth = width;
            float tableWidth = Mathf.Min(width, keyWidth + 180 * grid.Columns.Count + 42);
            bool sideCurve = grid.IsProbability && availableWidth - tableWidth >= 320;
            width = tableWidth;
            y +=
                PlaceLabel(
                    grid.IsProbability ? "Probability tables" : "Lookup tables",
                    groupIndex++,
                    4,
                    y,
                    width - 8,
                    true
                ) + 8;
            y +=
                PlaceLabel(
                    grid.IsProbability
                        ? "Each score uses the preceding threshold. Blank cells have no entry."
                        : "Values by threshold. Blank cells have no entry.",
                    groupIndex++,
                    4,
                    y,
                    width - 8,
                    false
                ) + 10;
            if (grid.IsProbability && !sideCurve)
                y += RenderCurve(grid, 0, y, availableWidth, ref groupIndex) + 12;
            scoreInput.gameObject.SetActive(true);
            addScoreButton.gameObject.SetActive(true);
            UILayout.SetSourceRect((RectTransform)scoreInput.transform, 4, y, 110, 32);
            UILayout.SetSourceRect((RectTransform)addScoreButton.transform, 124, y, 116, 32);
            y += 44;
            int chartBottom = sideCurve
                ? y + RenderCurve(grid, width + 20, y, availableWidth - width - 20, ref groupIndex)
                : 0;
            float scoreWidth = Mathf.Min(keyWidth, width * 0.5f);
            float cellWidth = (width - scoreWidth - 42) / grid.Columns.Count;
            int headerStart = groupIndex;
            int headerHeight = PlaceLabel(keyCaption, groupIndex++, 4, y, scoreWidth - 8, true);
            for (int column = 0; column < grid.Columns.Count; column++)
                headerHeight = Mathf.Max(
                    headerHeight,
                    PlaceLabel(
                        grid.Columns[column] + (grid.IsProbability ? " (%)" : string.Empty),
                        groupIndex++,
                        scoreWidth + column * cellWidth + 4,
                        y,
                        cellWidth - 8,
                        true
                    )
                );
            for (int index = headerStart; index < groupIndex; index++)
            {
                TextMeshProUGUI heading = groupLabels[index];
                heading.alignment = TextAlignmentOptions.Center;
                heading.rectTransform.sizeDelta = new Vector2(
                    heading.rectTransform.sizeDelta.x,
                    headerHeight
                );
            }
            y += headerHeight + 8;
            int rowIndex = 0;
            foreach (int score in grid.Scores)
            {
                while (tableRows.Count <= rowIndex)
                    tableRows.Add(
                        Instantiate(tableRowTemplate, fieldScroll.ScrollArea.ContentRoot)
                    );
                RawImage row = tableRows[rowIndex];
                ContentEditorColorRole rowRole =
                    rowIndex % 2 == 0
                        ? ContentEditorColorRole.Panel
                        : ContentEditorColorRole.Background;
                row.GetComponent<ContentEditorColorBinding>().SetRole(rowRole);
                UILayout.SetSourceRect(row.rectTransform, 0, y, Mathf.FloorToInt(width), 44);
                row.transform.SetAsFirstSibling();
                row.gameObject.SetActive(true);
                PlaceLabel(
                    score.ToString(CultureInfo.InvariantCulture),
                    groupIndex++,
                    4,
                    y + 6,
                    scoreWidth - 8,
                    false
                );
                groupLabels[groupIndex - 1].alignment = TextAlignmentOptions.Center;
                int rowHeight = 44;
                for (int column = 0; column < grid.Columns.Count; column++)
                {
                    ContentPackEditorField field = grid.GetCell(score, column);
                    rowHeight = Mathf.Max(
                        rowHeight,
                        PlaceField(
                            field,
                            fieldIndex++,
                            scoreWidth + column * cellWidth,
                            y,
                            cellWidth,
                            0,
                            44
                        )
                    );
                    ContentEditorFieldView cell = fieldViews[fieldIndex - 1];
                    cell.name = "Probability" + column + "Score" + score + "Field";
                    cell.SetBackgroundRole(rowRole);
                    cell.SetCellLayout();
                }
                while (removeRowButtons.Count <= rowIndex)
                    removeRowButtons.Add(
                        Instantiate(removeRowTemplate, fieldScroll.ScrollArea.ContentRoot)
                    );
                Button remove = removeRowButtons[rowIndex];
                remove.gameObject.SetActive(true);
                remove.onClick.RemoveAllListeners();
                int removedScore = score;
                remove.onClick.AddListener(() =>
                {
                    if (Commit())
                        ClearFieldsRequested?.Invoke(
                            Enumerable
                                .Range(0, grid.Columns.Count)
                                .Select(column => grid.GetCell(removedScore, column))
                                .ToArray()
                        );
                });
                UILayout.SetSourceRect(
                    (RectTransform)remove.transform,
                    Mathf.RoundToInt(width - 38),
                    y + 6,
                    34,
                    32
                );
                UILayout.SetSourceRect(row.rectTransform, 0, y, Mathf.FloorToInt(width), rowHeight);
                y += rowHeight;
                rowIndex++;
            }
            y = Mathf.Max(y, chartBottom);
        }
        fieldScroll.SetContentHeight(y, !reflowingFields);
    }

    /// <summary>
    /// Arranges scalar rules beside compact inputs and gives text and lists full-width rows.
    /// </summary>
    /// <param name="title">The contextual section heading.</param>
    /// <param name="settings">The existing authored properties.</param>
    /// <param name="width">The available inspector width.</param>
    /// <param name="y">The next available row position.</param>
    /// <param name="fieldIndex">The next reusable input slot.</param>
    /// <param name="labelIndex">The next reusable label slot.</param>
    private void RenderSettings(
        string title,
        IEnumerable<ContentPackEditorField> settings,
        float width,
        ref int y,
        ref int fieldIndex,
        ref int labelIndex
    )
    {
        ContentPackEditorField[] fields = settings.ToArray();
        if (fields.Length == 0)
            return;
        y += PlaceLabel(title, labelIndex++, 4, y, width - 8, true) + 10;
        int columns = Mathf.Clamp(Mathf.FloorToInt(width / 400), 1, 3);
        float columnWidth = width / columns;
        for (int start = 0; start < fields.Length; )
        {
            if (!fields[start].IsNumber && !fields[start].IsBoolean)
            {
                ContentPackEditorField text = fields[start++];
                y += PlaceLabel(text.Caption, labelIndex++, 4, y, width - 8, false);
                y += PlaceField(text, fieldIndex++, 0, y, width, 0);
                continue;
            }
            int rowHeight = 44;
            for (int column = 0; column < columns && start < fields.Length; column++)
            {
                ContentPackEditorField field = fields[start];
                if (!field.IsNumber && !field.IsBoolean)
                    break;
                start++;
                string caption = field.Caption;
                if (field.XmlElement.Parent?.Parent?.Name == "TickRanges")
                    caption = field.MemberName == "Base" ? "Base ticks" : "Random additional ticks";
                if (field.Group == "DifficultyModifiers")
                {
                    caption = field.MemberName switch
                    {
                        "MissionSuccessChancePoints" => "Mission success bonus (points)",
                        "MissionFoilChancePoints" => "Mission foil bonus (points)",
                        "MissionExecutionSpeedIncreasePercent" => "Mission speed bonus (%)",
                        _ => field.Caption,
                    };
                }
                float x = column * columnWidth;
                rowHeight = Mathf.Max(
                    rowHeight,
                    PlaceLabel(
                        caption,
                        labelIndex++,
                        x + 4,
                        y + 4,
                        columnWidth - 124,
                        false,
                        field.HelpText
                    ) + 8
                );
                rowHeight = Mathf.Max(
                    rowHeight,
                    PlaceField(field, fieldIndex++, x + columnWidth - 116, y, 100, 0, 40)
                );
            }
            y += rowHeight;
        }
        y += 20;
    }

    /// <summary>
    /// Arranges units into compact identity, skill, production, and eligibility columns.
    /// </summary>
    /// <param name="fields">The typed fields for the unit.</param>
    private void RenderUnitFields(ContentPackEditorField[] fields)
    {
        float viewportWidth = fieldScroll.ScrollArea.ViewportWidth;
        int columns = Mathf.Clamp(Mathf.FloorToInt((viewportWidth + 24) / 324), 1, 3);
        float width = (viewportWidth - (columns - 1) * 24) / columns;
        float[] top = new float[columns];
        if (ContentEditorUnitArtworkLayout.IsHeadquartersIcon(selectedRecord))
        {
            previewPane.SetActive(false);
            fields = fields.Where(field => field.MemberName is "TypeID" or "DisplayName").ToArray();
            ContentPackEditorField display = selectedRecord.GetField("DisplayImagePath");
            Texture2D icon = ResolveArtworkTexture(display);
            if (icon != null && TryGetArtworkDimensions(display, out Vector2Int size))
            {
                int imageIndex = 0;
                PlaceArtworkComposition(
                    "Headquarters",
                    4,
                    4,
                    size.x,
                    size.y,
                    size,
                    null,
                    icon,
                    Array.Empty<Texture2D>(),
                    ref imageIndex
                );
                top[0] = size.y + 12;
            }
        }
        else
            top[0] = RenderPreview(selectedRecord.ImageAddress, width, columns == 1 ? 160 : 280);
        int fieldIndex = 0,
            groupIndex = 0;
        foreach (
            IGrouping<string, ContentPackEditorField> group in fields.GroupBy(
                ContentPackEditorFieldMetadata.GetInspectorGroup
            )
        )
        {
            int column = group.Key switch
            {
                "Skills" or "Durability" or "Weapons" => 1,
                "Command and missions" or "Loyalty" or "Force" or "Production" => 2,
                _ => 0,
            };
            column = Mathf.Min(column, columns - 1);
            float x = column * (width + 24);
            top[column] +=
                PlaceLabel(group.Key, groupIndex++, x + 4, top[column] + 10, width - 8, true) + 18;
            if (group.Key == "Skills" && selectedRecord.Kind is "Officer" or "SpecialForce")
            {
                ContentPackEditorField[] skillFields = group.ToArray();
                bool hasVariance = skillFields.Any(field =>
                    field.MemberName.EndsWith("Variance", StringComparison.Ordinal)
                );
                PlaceLabel(
                    "Base",
                    groupIndex++,
                    x + width - (hasVariance ? 164 : 80),
                    top[column],
                    72,
                    false
                );
                if (hasVariance)
                    PlaceLabel("Variance", groupIndex++, x + width - 80, top[column], 80, false);
                top[column] += 30;
                foreach (
                    ContentPackEditorField field in skillFields.Where(field =>
                        !field.MemberName.EndsWith("Variance", StringComparison.Ordinal)
                    )
                )
                {
                    int captionHeight = PlaceLabel(
                        field.Caption,
                        groupIndex++,
                        x + 4,
                        top[column] + 4,
                        width - 180,
                        false,
                        field.HelpText
                    );
                    int rowHeight = PlaceField(
                        field,
                        fieldIndex++,
                        x + width - (hasVariance ? 164 : 80),
                        top[column],
                        80,
                        0,
                        48
                    );
                    string key =
                        field.Group == "Ratings"
                            ? field.Name.Replace(" ", string.Empty)
                            : field.MemberName;
                    ContentPackEditorField variance = skillFields.FirstOrDefault(candidate =>
                        candidate.MemberName == key + "Variance"
                    );
                    if (variance != null)
                        rowHeight = Mathf.Max(
                            rowHeight,
                            PlaceField(
                                variance,
                                fieldIndex++,
                                x + width - 80,
                                top[column],
                                80,
                                0,
                                48
                            )
                        );
                    top[column] += Mathf.Max(rowHeight, captionHeight + 12);
                }
            }
            else
            {
                IReadOnlyList<string> previousSections = Array.Empty<string>();
                foreach (ContentPackEditorField field in group)
                {
                    top[column] = PlaceSections(
                        field.SectionPath,
                        previousSections,
                        ref groupIndex,
                        x + 4,
                        top[column],
                        width - 8
                    );
                    previousSections = field.SectionPath;
                    bool stacked =
                        field.IsMultiline || field.IsList || (!field.IsBoolean && width < 340);
                    if (stacked)
                    {
                        top[column] += PlaceLabel(
                            field.Caption,
                            groupIndex++,
                            x + 4,
                            top[column],
                            width - 8,
                            false,
                            field.HelpText
                        );
                    }
                    top[column] += PlaceField(
                        field,
                        fieldIndex++,
                        x,
                        top[column],
                        width,
                        stacked ? 0 : width * 0.6f
                    );
                }
            }
            top[column] += 22;
        }
        fieldScroll.SetContentHeight(Mathf.Max(top), !reflowingFields);
    }

    /// <summary>
    /// Places a pooled shared field at an explicit content-space position.
    /// </summary>
    /// <param name="field">The authored value.</param>
    /// <param name="index">The reusable field slot.</param>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    /// <param name="width">The available width.</param>
    /// <param name="labelWidth">The space reserved for the caption.</param>
    /// <param name="height">An optional compact height.</param>
    /// <param name="artworkAction">Whether an existing card preview replaces the normal media controls.</param>
    /// <returns>The occupied vertical space.</returns>
    /// <param name="referenceLabels">Optional reference choices scoped to the surrounding editor.</param>
    /// <param name="editArtwork">Opens the gameplay artwork editor when one is available.</param>
    /// <param name="hasEffectArtwork">Whether the associated effect image could be loaded.</param>
    private int PlaceField(
        ContentPackEditorField field,
        int index,
        float x,
        float y,
        float width,
        float labelWidth,
        int height = 0,
        bool artworkAction = false,
        IReadOnlyDictionary<string, string> referenceLabels = null,
        Action editArtwork = null,
        bool hasEffectArtwork = false
    )
    {
        while (fieldViews.Count <= index)
        {
            ContentEditorFieldView created = Instantiate(
                fieldTemplate,
                fieldScroll.ScrollArea.ContentRoot
            );
            created.ValidationFailed += ReportValidationError;
            created.Submitted += SubmitField;
            created.ValidationRequested += ValidateField;
            created.SelectionRequested += RequestSelection;
            fieldViews.Add(created);
        }
        ContentEditorFieldView view = fieldViews[index];
        if (reflowingFields && !IsBoundTo(view, field))
        {
            int existing = fieldViews.FindIndex(
                index + 1,
                candidate => IsBoundTo(candidate, field)
            );
            if (existing >= 0)
            {
                fieldViews[index] = fieldViews[existing];
                fieldViews[existing] = view;
                view = fieldViews[index];
            }
        }
        view.name = field.Name.Replace(" / ", " ").Replace(" ", string.Empty) + "Field";
        if (
            !reflowingFields
            || !view.gameObject.activeSelf
            || !IsBoundTo(view, field)
            || view.Field.XmlElement != field.XmlElement && view.Field.Value != field.Value
        )
            view.Render(
                ContentPackEditorProjector.BuildField(project, field, referenceLabels),
                HandleFieldChanged,
                RequestImport,
                RequestPreview
            );
        Rect rect = PrepareFormRect((RectTransform)view.transform, x, y, width, height);
        return artworkAction
            ? view.SetArtworkActionLayout(rect, editArtwork, hasEffectArtwork)
            : view.SetLayout(rect, labelWidth);
    }

    /// <summary>
    /// Retains each draft on its own control when attaching optional members changes field order.
    /// </summary>
    /// <param name="view">The existing pooled control.</param>
    /// <param name="field">The property being laid out.</param>
    /// <returns>Whether the control represents the same authored or pending property.</returns>
    private static bool IsBoundTo(ContentEditorFieldView view, ContentPackEditorField field) =>
        view.gameObject.activeSelf
        && view.Field != null
        && (
            view.Field.XmlElement == field.XmlElement
            || view.Field.Record.Element == field.Record.Element
                && view.Field.Name == field.Name
                && view.Field.MemberName == field.MemberName
        );

    /// <summary>
    /// Places a pooled heading or skill caption without introducing another boxed panel.
    /// </summary>
    /// <param name="text">The label text.</param>
    /// <param name="index">The reusable label slot.</param>
    /// <param name="x">The horizontal position.</param>
    /// <param name="y">The vertical position.</param>
    /// <param name="width">The label width.</param>
    /// <param name="heading">Whether to use section typography.</param>
    /// <param name="helpText">The optional explanation revealed on hover.</param>
    /// <returns>The complete height required by the wrapped text.</returns>
    private int PlaceLabel(
        string text,
        int index,
        float x,
        float y,
        float width,
        bool heading,
        string helpText = null
    )
    {
        while (groupLabels.Count <= index)
            groupLabels.Add(Instantiate(groupTemplate, fieldScroll.ScrollArea.ContentRoot));
        TextMeshProUGUI label = groupLabels[index];
        label.text = text;
        HelpTooltipView help = label.GetComponentInChildren<HelpTooltipView>(true);
        help.Configure(helpText, captionFont: label.font);
        bool hasHelp = !string.IsNullOrWhiteSpace(helpText);
        help.gameObject.SetActive(hasHelp);
        RectTransform helpRect = (RectTransform)help.transform;
        int helpSize = Mathf.RoundToInt(helpRect.rect.width);
        float captionWidth = Mathf.Max(1, width - (hasHelp ? helpSize + 4 : 0));
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.fontSize = heading ? 17 : 14;
        label
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(heading ? ContentEditorColorRole.Accent : ContentEditorColorRole.Text);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Overflow;
        int height = Mathf.Max(
            32,
            Mathf.CeilToInt(label.GetPreferredValues(text, captionWidth, float.PositiveInfinity).y)
                + 2
        );
        SetFormRect(label.rectTransform, x, y, captionWidth, height);
        label.gameObject.SetActive(true);
        if (hasHelp)
            help.PositionBeside(label);
        return height;
    }

    /// <summary>
    /// Shows the changed portion of a field's hierarchy as separate, wrapping section headings.
    /// </summary>
    /// <param name="sections">The current field's containing sections.</param>
    /// <param name="previous">The preceding field's sections.</param>
    /// <param name="labelIndex">The next reusable heading slot.</param>
    /// <param name="x">The left edge of the form.</param>
    /// <param name="y">The next available vertical position.</param>
    /// <param name="width">The available form width.</param>
    /// <returns>The next available position below the new headings.</returns>
    private float PlaceSections(
        IReadOnlyList<string> sections,
        IReadOnlyList<string> previous,
        ref int labelIndex,
        float x,
        float y,
        float width
    )
    {
        int common = 0;
        while (
            common < sections.Count
            && common < previous.Count
            && sections[common] == previous[common]
        )
            common++;
        for (int i = common; i < sections.Count; i++)
        {
            float inset = Mathf.Min(i, 4) * 16;
            y += 12;
            y += PlaceLabel(sections[i], labelIndex++, x + inset, y, width - inset, true) + 8;
        }
        return y;
    }

    /// <summary>
    /// Forwards a field submission without applying it inside the form.
    /// </summary>
    /// <param name="field">The edited member.</param>
    /// <param name="values">The complete proposed values.</param>
    private void SubmitField(ContentPackEditorField field, IReadOnlyList<string> values) =>
        FieldSubmitted?.Invoke(field, values);

    /// <summary>
    /// Forwards explicit validation of an accepted authored value.
    /// </summary>
    /// <param name="field">The member to check.</param>
    private void ValidateField(ContentPackEditorField field) =>
        FieldValidationRequested?.Invoke(field);
}
