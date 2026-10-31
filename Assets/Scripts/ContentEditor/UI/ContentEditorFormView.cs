using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Owns responsive authored forms, field bindings, artwork and table presentation.
/// </summary>
public sealed class ContentEditorFormView : MonoBehaviour
{
    [SerializeField]
    private GameObject previewPane;

    [SerializeField]
    private RawImage previewImage;

    [SerializeField]
    private Button addMessageButton;

    [SerializeField]
    private TMP_Dropdown collectionDropdown;

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

    private readonly List<TextMeshProUGUI> groupLabels = new List<TextMeshProUGUI>();

    private readonly List<ContentEditorFieldView> fieldViews = new List<ContentEditorFieldView>();

    private bool reflowingFields;

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
    private readonly List<CollectionEditorView> setupCollections = new();
    private readonly List<Button> setupUnitButtons = new();
    private readonly Dictionary<string, int> setupTasks = new();
    private readonly Dictionary<string, int> setupEntries = new();
    public event Action<ContentPackEditorField> SelectionRequested;
    public event Action<ContentPackEditorCollection> UnitSelectionRequested;
    public event Action<ContentPackEditorCollection> CollectionAddRequested;
    public event Action<ContentPackEditorCollection, int> CollectionRemoveRequested;

    public event Action Changed;
    public event Action<ContentPackEditorField, IReadOnlyList<string>> FieldSubmitted;
    public event Action<ContentPackEditorField> FieldValidationRequested;
    public event Action WidthChanged;
    public event Action<Button> AddMessageRequested;
    public event Action AddNamePoolRequested;
    public event Action<ContentPackEditorField> RemoveMessageRequested;
    public event Action<IReadOnlyList<ContentPackEditorField>> ClearFieldsRequested;
    public event Action<ContentPackEditorField> ImportRequested;
    public event Action<ContentPackEditorField, string> PreviewRequested;
    public event Action<string> ValidationFailed;
    public bool HasOpenChoice =>
        generationTask.IsExpanded
        || setupCollections.Any(view => view.gameObject.activeSelf && view.HasOpenChoice)
        || collectionDropdown.IsExpanded
        || curveDropdown.IsExpanded
        || fieldViews.Any(field => field.gameObject.activeSelf && field.HasOpenChoice);
    public TextInputHistory InvalidTextHistory =>
        fieldViews
            .FirstOrDefault(field => field.gameObject.activeSelf && field.HasError)
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
            || previewPane == null
            || previewImage == null
            || addMessageButton == null
            || collectionDropdown == null
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
        )
            throw new MissingReferenceException(name + " has incomplete form references.");
        bound = true;
        generationTask.onValueChanged.AddListener(SelectGenerationTask);
        fieldScroll.WidthChanged += HandleWidthChanged;
        addMessageButton.onClick.AddListener(RequestAddMessage);
        collectionDropdown.onValueChanged.AddListener(SelectCollectionItem);
        removeMessageButton.onClick.AddListener(RequestRemoveMessage);
        curveDropdown.onValueChanged.AddListener(SelectCurve);
        addScoreButton.onClick.AddListener(AddScore);
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
        removeMessageButton.onClick.RemoveListener(RequestRemoveMessage);
        curveDropdown.onValueChanged.RemoveListener(SelectCurve);
        addScoreButton.onClick.RemoveListener(AddScore);
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
        return fieldViews.All(field => !field.gameObject.activeSelf || !field.HasError);
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
        HidePreview();
        addMessageButton.gameObject.SetActive(false);
        collectionDropdown.gameObject.SetActive(false);
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
    public void FocusField(ContentPackEditorField field)
    {
        XElement item = field
            .XmlElement.AncestorsAndSelf()
            .FirstOrDefault(element => element.Name.LocalName is "Message" or "NamePool");
        if (item != null && selectedCollection != item)
        {
            selectedCollection = item;
            RenderFields(true);
        }
        ContentEditorFieldView row = fieldViews.Single(item =>
            item.gameObject.activeSelf && item.Field.XmlElement == field.XmlElement
        );
        RectTransform rect = (RectTransform)row.transform;
        fieldScroll.ScrollArea.RevealContentRect(
            -rect.anchoredPosition.y,
            fieldScroll.ScrollArea.ViewportHeight
        );
        TMP_InputField input = row.GetComponentInChildren<TMP_InputField>();
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
        if (section == "Ship names")
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
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
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
            high.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            labelIndex++,
            x,
            y + 38,
            40,
            false
        );
        PlaceLabel(
            low.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            labelIndex++,
            x,
            y + 208,
            40,
            false
        );
        PlaceLabel(
            minimum.ToString(System.Globalization.CultureInfo.InvariantCulture),
            labelIndex++,
            x + 44,
            y + 232,
            70,
            false
        );
        PlaceLabel(
            maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
        addMessageButton.gameObject.SetActive(false);
        collectionDropdown.gameObject.SetActive(false);
        removeMessageButton.gameObject.SetActive(false);
        curveGraphic.gameObject.SetActive(false);
        curveDropdown.gameObject.SetActive(false);
        scoreInput.gameObject.SetActive(false);
        addScoreButton.gameObject.SetActive(false);
        foreach (Button button in removeRowButtons)
            button.gameObject.SetActive(false);
        foreach (TextMeshProUGUI group in groupLabels)
            group.gameObject.SetActive(false);
        foreach (RawImage image in articleImages)
            image.gameObject.SetActive(false);
        foreach (RawImage row in tableRows)
            row.gameObject.SetActive(false);
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
        if (selectedPage?.Key.StartsWith("factions/", StringComparison.Ordinal) == true)
        {
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
        bool unit =
            selectedRecord.Kind
            is "Officer"
                or "SpecialForce"
                or "Regiment"
                or "CapitalShip"
                or "Starfighter"
                or "Building";
        previewPane.SetActive(false);
        if (unit && section == "General")
        {
            RenderUnitFields(fields.OrderBy(ContentEditorInspectorLayout.GetOrder).ToArray());
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
            groupIndex = 0;
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
        catch (ArgumentException exception)
        {
            Debug.LogWarning("Invalid artwork path: " + exception.Message);
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
                : record.Kind == "FactionTheme" ? "Unit icons"
                : record.Element.Element("FactionDataPath") != null ? "Content files"
                : section;
            if (article || record.Kind != "Faction")
                y += PlaceLabel(title, headingIndex++, 4, y, width - 8, true) + 8;
            if (
                collection
                && itemName == "NamePool"
                && GetNamePoolTitles(record).TryGetValue(selectedCollection, out string usage)
            )
                y += PlaceLabel(usage, headingIndex++, 4, y, width - 8, false) + 8;
            if (article && !string.IsNullOrEmpty(record.ImageAddress))
            {
                while (articleImages.Count <= imageIndex)
                {
                    RawImage image = Instantiate(
                        articleImageTemplate,
                        fieldScroll.ScrollArea.ContentRoot
                    );
                    image.raycastTarget = false;
                    articleImages.Add(image);
                }
                RawImage preview = articleImages[imageIndex++];
                preview.texture = assets.GetTexture(record.ImageAddress);
                if (preview.texture != null)
                {
                    Vector2Int source = UILayout.GetTextureSourceSize(preview.texture);
                    int imageWidth = Mathf.Min(Mathf.RoundToInt(width - 8), 440);
                    int imageHeight = Mathf.RoundToInt((float)imageWidth * source.y / source.x);
                    UILayout.SetSourceRect(preview.rectTransform, 4, y, imageWidth, imageHeight);
                    preview.enabled = true;
                    preview.gameObject.SetActive(true);
                    y += imageHeight + 12;
                }
            }
            IReadOnlyDictionary<System.Xml.Linq.XElement, string> poolTitles = GetNamePoolTitles(
                record
            );
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
                System.Xml.Linq.XElement pool = field
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
                y += PlaceField(
                    field,
                    fieldIndex++,
                    0,
                    y,
                    width,
                    stacked ? 0 : Mathf.Min(280, width * 0.45f)
                );
            }
            y += 24;
        }
        fieldScroll.SetContentHeight(y, !reflowingFields);
    }

    /// <summary>
    /// Explains each authored ship-name pool using its current ship designs and fallbacks.
    /// </summary>
    /// <param name="record">The faction whose pools are being edited.</param>
    /// <returns>The usage captions keyed by their exact collection entries.</returns>
    private IReadOnlyDictionary<System.Xml.Linq.XElement, string> GetNamePoolTitles(
        ContentPackEditorRecord record
    )
    {
        System.Xml.Linq.XElement[] pools =
            record.Element.Element("ShipNamePools")?.Elements().ToArray()
            ?? Array.Empty<System.Xml.Linq.XElement>();
        string owner = record.Element.Element("InstanceID")?.Value;
        ContentPackEditorRecord[] ships =
            pools.Length == 0
                ? Array.Empty<ContentPackEditorRecord>()
                : catalogs
                    .Where(catalog => catalog.Name == "Capital ships")
                    .SelectMany(catalog => catalog.Records)
                    .Where(ship =>
                        ship.Element.Element("ManufacturingFactionInstanceIDs")
                            ?.Elements()
                            .Any(id => id.Value == owner) == true
                    )
                    .ToArray();
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
                    names.Length > 0 ? string.Join(", ", names)
                    : fallbacks.Length > 0
                        ? "Fallback for "
                            + string.Join(", ", fallbacks.Select(value => "pool " + value))
                    : "No ships assigned";
                return "Pool " + id + " — " + usage;
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
            ownerType == null
                ? null
                : Rebellion.Util.Serialization.SerializationMetadata.GetElementType(
                    ownerType,
                    memberName
                );
        if (
            memberType != null
            && ContentPackEditorFieldSchema.GetItemType(memberType) is Type item
            && !Rebellion.Util.Serialization.TypeHelper.IsScalar(item)
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
                type == typeof(Rebellion.Game.DiceFormula)
                || type == typeof(Rebellion.Game.SupportFormula)
                || type == typeof(Rebellion.Game.PlanetSizeProfile)
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
        if (collection.ItemType == typeof(Rebellion.Game.UnitEntry))
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
            false
        );
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
            PlaceField(term, fieldIndex++, left, y + headerHeight, cellWidth, 0, 44);
        }
        return Mathf.Max(headerHeight + 48, captionHeight + headerHeight + 8);
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
                field.XmlElement.Parent?.Parent?.Name == "TickRanges" ? "Timing"
                : selectedPage?.MissionID != null ? "Settings"
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
                    score.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    groupIndex++,
                    4,
                    y + 6,
                    scoreWidth - 8,
                    false
                );
                groupLabels[groupIndex - 1].alignment = TextAlignmentOptions.Center;
                for (int column = 0; column < grid.Columns.Count; column++)
                {
                    ContentPackEditorField field = grid.GetCell(score, column);
                    PlaceField(
                        field,
                        fieldIndex++,
                        scoreWidth + column * cellWidth,
                        y,
                        cellWidth,
                        0,
                        44
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
                y += 44;
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
                    PlaceField(
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
                        PlaceField(variance, fieldIndex++, x + width - 80, top[column], 80, 0, 48);
                    top[column] += Mathf.Max(44, captionHeight + 12);
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
    /// <returns>The occupied vertical space.</returns>
    private int PlaceField(
        ContentPackEditorField field,
        int index,
        float x,
        float y,
        float width,
        float labelWidth,
        int height = 0
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
                ContentPackEditorProjector.BuildField(project, field),
                HandleFieldChanged,
                RequestImport,
                RequestPreview
            );
        int occupied = height;
        return view.SetLayout(new Rect(x, y, width, occupied), labelWidth);
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
        help.Configure(helpText);
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
        UILayout.SetSourceRect(
            label.rectTransform,
            Mathf.RoundToInt(x),
            Mathf.RoundToInt(y),
            Mathf.RoundToInt(captionWidth),
            height
        );
        if (hasHelp)
        {
            UILayout.SetSourceRect(
                helpRect,
                Mathf.RoundToInt(Mathf.Min(label.preferredWidth, captionWidth) + 4),
                -Mathf.RoundToInt((helpSize - label.fontSize) / 2),
                helpSize,
                helpSize
            );
        }
        label.gameObject.SetActive(true);
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
