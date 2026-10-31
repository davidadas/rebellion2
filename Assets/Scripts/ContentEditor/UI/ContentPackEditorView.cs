using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Messages;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents category editors, named records, artwork previews, and map authoring.
/// </summary>
public sealed class ContentPackEditorView : MonoBehaviour, ICancelable
{
    [SerializeField]
    private CanvasGroup workbench;

    [SerializeField]
    private ContentEditorWorkbenchLayout workbenchLayout;

    [SerializeField]
    private Button fileButton;

    [SerializeField]
    private TextMeshProUGUI projectLabel;

    [SerializeField]
    private Button editButton;

    [SerializeField]
    private GameObject projectWorkspace;

    [SerializeField]
    private GameObject welcomePane;

    [SerializeField]
    private Button newPackButton;

    [SerializeField]
    private Button openPackButton;

    [SerializeField]
    private Button viewButton;

    [SerializeField]
    private Button testButton;

    [SerializeField]
    private MenuBarHeadingView[] menuHeadings;

    [SerializeField]
    private ContentEditorAppearance appearance;

    [SerializeField]
    private TMP_InputField searchInput;

    [SerializeField]
    private Button clearSearchButton;
    private float inspectorTop = 136;

    [SerializeField]
    private ScrollAreaLayout recordScroll;

    [SerializeField]
    private ContentEditorRecordRowView recordTemplate;

    [SerializeField]
    private Button duplicateButton;

    [SerializeField]
    private Button addUnitButton;

    [SerializeField]
    private Button expandAllButton;

    [SerializeField]
    private Button collapseAllButton;

    [SerializeField]
    private Button deleteButton;

    [SerializeField]
    private TextMeshProUGUI recordTitle;

    [SerializeField]
    private TextMeshProUGUI recordSubtitle;

    [SerializeField]
    private Button propertiesTab;

    [SerializeField]
    private Button artworkTab;

    [SerializeField]
    private Button audioTab;

    [SerializeField]
    private Button advancedTab;

    [SerializeField]
    private Button encyclopediaTab;

    [SerializeField]
    private Button rulesTab;

    [SerializeField]
    private Button shipNamesTab;

    [SerializeField]
    private Button factionMessagesTab;

    [SerializeField]
    private Button referenceTab;

    [SerializeField]
    private Button[] generationTabs;

    [SerializeField]
    private Texture2D checkmark;

    [SerializeField]
    private RectTransform tabRoot;

    [SerializeField]
    private ContentEditorMapView map;

    [SerializeField]
    private ContentEditorFormView form;

    [SerializeField]
    private ContentEditorXmlView xml;

    [SerializeField]
    private ContextMenuView menu;

    [SerializeField]
    private ContentEditorPackDialogView packDialog;

    [SerializeField]
    private GameObject notice;

    [SerializeField]
    private TextMeshProUGUI noticeText;

    [SerializeField]
    private Button dismissNoticeButton;

    [SerializeField]
    private ActionDialogView confirmation;

    [SerializeField]
    private SelectionDialogView selectionDialog;
    internal event Action<ContentPackEditorCollection> CollectionAddRequested;
    internal event Action<ContentPackEditorCollection, int> CollectionRemoveRequested;
    internal event Action<
        ContentPackEditorCollection,
        IReadOnlyDictionary<string, int>
    > CollectionQuantitiesSubmitted;

    [SerializeField]
    private ImagePreviewView imagePreview;

    [SerializeField]
    private AudioSource previewAudio;

    private readonly ContentEditorNavigation navigation = new ContentEditorNavigation();
    private SelectableListView<ContentEditorRecordRowView, ContentEditorTreeNode> recordList;
    private IReadOnlyList<ContentPackEditorCatalog> catalogs =
        Array.Empty<ContentPackEditorCatalog>();
    private ContentPackEditorProject project;
    private ContentAssets assets;
    private ContentPackEditorRecord selectedRecord;
    private ContentEditorTreeNode selectedPage;
    private ContentPackEditorRecord mapSector;
    private string section = "General";
    private bool initialized;
    private bool fieldLayoutDirty;
    private bool mapDragging;
    private Button menuAnchor;
    private TextInputHistory textRedoTarget;
    private GameSize previewSize = GameSize.Large;
    private GameDifficulty previewDifficulty = GameDifficulty.Medium;
    private string previewScenario;

    public event Action<string, string, string, string, string> CreateRequested;
    public event Action OpenRequested;
    public event Action<string> BrowseFolderRequested;
    public event Action<ContentPackEditorField> ImportRequested;
    public event Action SaveRequested;
    public event Action ValidateRequested;
    public event Action<ContentEditorMapLayout> MapLayoutRequested;
    public event Action UndoRequested;
    public event Action RedoRequested;
    public event Action ExportRequested;
    public event Action CloseRequested;
    public event Action ExitRequested;
    public event Action<ContentEditorColorMode> ColorModeChanged;
    public event Action<float, float> LayoutChanged;
    public event Action<ContentEditorPlayOptions> PlayRequested;
    public event Action<ContentPackEditorField, string> PreviewRequested;
    public event Action PreviewCancelled;
    internal event Action<ContentPackEditorRecord> DuplicateRequested;
    internal event Action<ContentPackEditorCatalog, string> AddUnitRequested;
    internal event Action<ContentPackEditorCatalog> AddTraitRequested;
    internal event Action<ContentPackEditorCatalog> AddEventRequested;
    internal event Action AddFactionRequested;
    internal event Action<ContentPackEditorCatalog, Vector2Int> AddSectorRequested;
    internal event Action<ContentPackEditorRecord, Vector2Int> AddPlanetRequested;
    internal event Action<ContentPackEditorRecord, MessageType> AddMessageRequested;
    internal event Action<ContentPackEditorRecord> AddNamePoolRequested;
    internal event Action<ContentPackEditorField> RemoveMessageRequested;
    internal event Action<IReadOnlyList<ContentPackEditorField>> ClearFieldsRequested;
    internal event Action<ContentPackEditorRecord> DeleteRequested;
    internal event Action<ContentEditorXmlScope, string, int> XmlSubmitted;
    internal event Action<ContentPackEditorField, IReadOnlyList<string>> FieldSubmitted;
    internal event Action<ContentPackEditorField> FieldValidationRequested;
    internal event Action<ContentPackEditorRecord> MapEditStarted;
    internal event Action<ContentPackEditorRecord, Vector2Int> MapMoved;
    internal event Action MapEditEnded;
    internal event Action<
        ContentPackEditorRecord,
        ContentEditorTreeNode,
        ContentPackEditorRecord
    > SelectionRequested;
    internal event Action<string> SectionRequested;

    public ContentEditorColorMode ColorMode => appearance.Mode;
    public bool IsPreviewAudioPlaying => previewAudio && previewAudio.isPlaying;
    public static IReadOnlyList<string> GenerationSections =>
        ContentEditorContentSections.GenerationSections;

    public bool HasTextFocus => FocusedTextInput() != null;
    public bool CanUndoText => TextHistory()?.CanUndo == true;
    public bool CanRedoText => (TextHistory() ?? textRedoTarget)?.CanRedo == true;
    private static string ShortcutModifier =>
        Application.platform is RuntimePlatform.OSXPlayer or RuntimePlatform.OSXEditor
            ? "Cmd"
            : "Ctrl";

    /// <summary>
    /// Finds the input that currently owns text editing.
    /// </summary>
    /// <returns>The focused input, or null outside text editing.</returns>
    private static TMP_InputField FocusedTextInput()
    {
        TMP_InputField input =
            EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>();
        return input?.isFocused == true ? input : null;
    }

    /// <summary>
    /// Finds pending text that must be corrected before document history can be used.
    /// </summary>
    /// <returns>The input history responsible for the current draft.</returns>
    private TextInputHistory TextHistory()
    {
        TMP_InputField focused = FocusedTextInput();
        if (focused != null)
            return focused.GetComponent<TextInputHistory>();
        if (selectedRecord != null && xml.PendingTextHistory != null)
            return xml.PendingTextHistory;
        return form.InvalidTextHistory;
    }

    /// <summary>
    /// Undoes text still being edited before consuming project history.
    /// </summary>
    /// <returns>Whether a pending text edit was undone.</returns>
    public bool UndoText()
    {
        TextInputHistory history = TextHistory();
        if (history?.Undo() != true)
            return false;
        textRedoTarget = history;
        if (!HasTextFocus)
            history.GetComponentInParent<ContentEditorFieldView>()?.Commit();
        return true;
    }

    /// <summary>
    /// Reapplies a pending text edit before consuming project history.
    /// </summary>
    /// <returns>Whether a pending text edit was reapplied.</returns>
    public bool RedoText()
    {
        TextInputHistory history = TextHistory() ?? textRedoTarget;
        if (history?.Redo() != true)
            return false;
        if (!HasTextFocus)
            history.GetComponentInParent<ContentEditorFieldView>()?.Commit();
        return true;
    }

    /// <summary>
    /// Applies an explicit expand-all or collapse-all command independently of selection.
    /// </summary>
    /// <param name="expanded">Whether all content branches should be open.</param>
    private void SetTreeExpansion(bool expanded)
    {
        searchInput.SetTextWithoutNotify(string.Empty);
        navigation.SetExpanded(expanded);
        RenderRecords(revealSelection: false);
    }

    /// <summary>
    /// Connects authored controls to semantic workspace operations.
    /// </summary>
    private void Awake()
    {
        InitializeControls();
    }

    /// <summary>
    /// Binds authored controls once for runtime use and native preview rendering.
    /// </summary>
    private void InitializeControls()
    {
        if (initialized)
            return;
        if (
            workbench == null
            || workbenchLayout == null
            || fileButton == null
            || projectLabel == null
            || editButton == null
            || projectWorkspace == null
            || welcomePane == null
            || newPackButton == null
            || openPackButton == null
            || viewButton == null
            || testButton == null
            || menuHeadings == null
            || menuHeadings.Length != 4
            || menuHeadings.Any(heading => heading == null)
            || appearance == null
            || searchInput == null
            || clearSearchButton == null
            || recordScroll == null
            || recordTemplate == null
            || duplicateButton == null
            || addUnitButton == null
            || expandAllButton == null
            || collapseAllButton == null
            || deleteButton == null
            || recordTitle == null
            || recordSubtitle == null
            || propertiesTab == null
            || artworkTab == null
            || audioTab == null
            || advancedTab == null
            || encyclopediaTab == null
            || rulesTab == null
            || shipNamesTab == null
            || factionMessagesTab == null
            || referenceTab == null
            || xml == null
            || checkmark == null
            || tabRoot == null
            || map == null
            || form == null
            || menu == null
            || packDialog == null
            || notice == null
            || noticeText == null
            || dismissNoticeButton == null
            || selectionDialog == null
            || confirmation == null
            || imagePreview == null
            || previewAudio == null
            || generationTabs == null
            || generationTabs.Length != ContentEditorContentSections.GenerationSections.Length
            || generationTabs.Any(button => button == null)
        )
            throw new MissingReferenceException(name + " has incomplete workspace references.");
        initialized = true;
        form.WidthChanged += HandleFieldAreaResize;
        workbenchLayout.Changed += HandleWorkbenchChanged;
        fileButton.onClick.AddListener(ShowFileMenu);
        editButton.onClick.AddListener(ShowEditMenu);
        viewButton.onClick.AddListener(ShowViewMenu);
        testButton.onClick.AddListener(ShowTestMenu);
        foreach (MenuBarHeadingView heading in menuHeadings)
            heading.Hovered += HoverMenu;
        newPackButton.onClick.AddListener(ShowNewPackDialog);
        openPackButton.onClick.AddListener(RequestOpenPack);
        searchInput.onValueChanged.AddListener(HandleSearchChanged);
        clearSearchButton.onClick.AddListener(ClearSearch);
        propertiesTab.onClick.AddListener(() => ShowSection("General"));
        artworkTab.onClick.AddListener(() => ShowSection("Artwork"));
        audioTab.onClick.AddListener(() => ShowSection("Audio"));
        advancedTab.onClick.AddListener(() => ShowSection("Advanced"));
        encyclopediaTab.onClick.AddListener(() => ShowSection("Encyclopedia"));
        rulesTab.onClick.AddListener(() => ShowSection("Rules"));
        shipNamesTab.onClick.AddListener(() => ShowSection("Ship names"));
        factionMessagesTab.onClick.AddListener(() => ShowSection("Messages"));
        referenceTab.onClick.AddListener(() => ShowSection("Encyclopedia"));
        for (int index = 0; index < generationTabs.Length; index++)
        {
            string caption = ContentEditorContentSections.GenerationSections[index];
            generationTabs[index].onClick.AddListener(() => ShowSection(caption));
        }
        form.AddMessageRequested += ShowAddMessageMenu;
        form.AddNamePoolRequested += RequestNamePool;
        form.RemoveMessageRequested += ConfirmRemoveMessage;
        form.ClearFieldsRequested += ForwardClearFields;
        form.Changed += HandleFieldChanged;
        form.FieldSubmitted += SubmitField;
        form.SelectionRequested += ShowFieldSelection;
        form.UnitSelectionRequested += ShowUnitSelection;
        form.CollectionAddRequested += ForwardCollectionAdd;
        form.CollectionRemoveRequested += ConfirmCollectionRemove;
        form.FieldValidationRequested += ValidateField;
        form.ImportRequested += ImportMedia;
        form.PreviewRequested += PreviewMedia;
        form.ValidationFailed += ReportFieldError;
        expandAllButton.onClick.AddListener(() => SetTreeExpansion(true));
        collapseAllButton.onClick.AddListener(() => SetTreeExpansion(false));
        xml.ApplyRequested += ApplyXmlDraft;
        xml.DiscardRequested += RequestDiscardXml;
        xml.ScopeRequested += RequestXmlScope;
        duplicateButton.onClick.AddListener(DuplicateRecord);
        addUnitButton.onClick.AddListener(AddUnit);
        deleteButton.onClick.AddListener(ConfirmDeleteRecord);
        packDialog.CreateRequested += ForwardCreateRequest;
        packDialog.BrowseRequested += ForwardBrowseRequest;
        dismissNoticeButton.onClick.AddListener(() => notice.SetActive(false));
        map.BackRequested += ReturnToGalaxy;
        map.ArrangeRequested += ArrangeMap;
        map.Selected += SelectMapRecord;
        map.Opened += OpenMapRecord;
        map.DragStarted += BeginMapDrag;
        map.Moved += MoveMapRecord;
        map.DragEnded += FinishMapDrag;
        menu.CommandSelected += HandleMenuSelection;
        menu.DismissRequested += _ => menu.Reset();
        recordList = new SelectableListView<ContentEditorRecordRowView, ContentEditorTreeNode>(
            recordScroll.ScrollArea,
            recordTemplate,
            "Record",
            (row, pointer) =>
            {
                if (pointer != null || row.Node.CreationCatalog == null)
                    SelectNode(row.Node);
            },
            (row, pointer) =>
            {
                if (pointer == null)
                    SelectNode(row.Node);
            },
            navigationScope: recordScroll.transform
        );
        duplicateButton.interactable = false;
        deleteButton.interactable = false;
        form.HidePreview();
        tabRoot.gameObject.SetActive(false);
        form.gameObject.SetActive(false);
    }

    /// <summary>
    /// Releases reusable list subscriptions and popup commands.
    /// </summary>
    private void OnDestroy()
    {
        StopAudioPreview();
        FinishMapEdit();
        recordList?.Clear();
        if (initialized)
        {
            foreach (MenuBarHeadingView heading in menuHeadings)
                heading.Hovered -= HoverMenu;
            xml.ApplyRequested -= ApplyXmlDraft;
            xml.DiscardRequested -= RequestDiscardXml;
            xml.ScopeRequested -= RequestXmlScope;
            form.WidthChanged -= HandleFieldAreaResize;
            form.AddMessageRequested -= ShowAddMessageMenu;
            form.AddNamePoolRequested -= RequestNamePool;
            form.RemoveMessageRequested -= ConfirmRemoveMessage;
            form.ClearFieldsRequested -= ForwardClearFields;
            form.Changed -= HandleFieldChanged;
            form.FieldSubmitted -= SubmitField;
            form.SelectionRequested -= ShowFieldSelection;
            form.UnitSelectionRequested -= ShowUnitSelection;
            form.CollectionAddRequested -= ForwardCollectionAdd;
            form.CollectionRemoveRequested -= ConfirmCollectionRemove;
            form.FieldValidationRequested -= ValidateField;
            form.ImportRequested -= ImportMedia;
            form.PreviewRequested -= PreviewMedia;
            form.ValidationFailed -= ReportFieldError;
            workbenchLayout.Changed -= HandleWorkbenchChanged;
            menu.CommandSelected -= HandleMenuSelection;
            map.BackRequested -= ReturnToGalaxy;
            map.ArrangeRequested -= ArrangeMap;
            map.Selected -= SelectMapRecord;
            map.Opened -= OpenMapRecord;
            map.DragStarted -= BeginMapDrag;
            map.Moved -= MoveMapRecord;
            map.DragEnded -= FinishMapDrag;
            packDialog.CreateRequested -= ForwardCreateRequest;
            packDialog.BrowseRequested -= ForwardBrowseRequest;
        }
    }

    /// <summary>
    /// Defers responsive form layout until Unity has resolved the resized canvas.
    /// </summary>
    private void HandleFieldAreaResize()
    {
        fieldLayoutDirty = true;
    }

    /// <summary>
    /// Reflows forms once after a window resize without rebuilding them during a canvas pass.
    /// </summary>
    private void LateUpdate()
    {
        if (!fieldLayoutDirty || selectedRecord == null)
            return;
        fieldLayoutDirty = false;
        if (selectedRecord.Kind == "GameEvent" || section == "Advanced")
            return;
        RenderFields(false);
    }

    /// <summary>
    /// Switches headings while the File or Edit menu is already open.
    /// </summary>
    /// <param name="button">The entered heading.</param>
    public void HoverMenu(Button button)
    {
        if (
            !menu.Open
            || menuAnchor == button
            || menuAnchor != fileButton
                && menuAnchor != editButton
                && menuAnchor != viewButton
                && menuAnchor != testButton
        )
            return;
        if (button == fileButton)
            ShowFileMenu();
        else if (button == editButton)
            ShowEditMenu();
        else if (button == viewButton)
            ShowViewMenu();
        else if (button == testButton)
            ShowTestMenu();
    }

    /// <summary>
    /// Dismisses the foremost transient UI through the native key-event path.
    /// </summary>
    /// <returns>Whether an editor popup consumed the cancellation.</returns>
    public bool TryCancel()
    {
        if (selectionDialog.TryCancel())
            return true;
        if (
            packDialog.HasOpenChoice
            || form.HasOpenChoice
            || xml.HasOpenChoice
            || map.HasOpenChoice
        )
            return false;
        if (
            menu.TryCancel()
            || imagePreview.TryCancel()
            || confirmation.TryCancel()
            || packDialog.TryCancel()
        )
            return true;
        TMP_InputField focused = FocusedTextInput();
        ContentEditorFieldView draft = focused?.GetComponentInParent<ContentEditorFieldView>();
        if (draft?.HasError == true)
        {
            draft.RevertDraft();
            return true;
        }
        if (!string.IsNullOrEmpty(searchInput.text) && (focused == null || focused == searchInput))
        {
            ClearSearch();
            return true;
        }
        if (!notice.activeSelf)
            return false;
        notice.SetActive(false);
        return true;
    }

    /// <summary>
    /// Initializes pack choices without clearing the open project.
    /// </summary>
    /// <param name="definitions">The installed packs.</param>
    /// <param name="contentRootPath">The installed Content directory.</param>
    public void Initialize(IReadOnlyList<ContentPackDefinition> definitions, string contentRootPath)
    {
        InitializeControls();
        packDialog.Initialize(
            definitions,
            ContentFileResolver.ResolveModsRootPath(contentRootPath)
        );
        if (project == null)
            ClearProject();
    }

    /// <summary>
    /// Displays an opened project and its primary authoring categories.
    /// </summary>
    /// <param name="value">The opened project.</param>
    /// <param name="contentAssets">The owned asset store.</param>
    public void RenderProject(ContentPackEditorProject value, ContentAssets contentAssets)
    {
        InitializeControls();
        if (value.IsInstalled)
            throw new InvalidOperationException(
                "Create a new content pack instead of editing installed content."
            );
        projectWorkspace.SetActive(true);
        welcomePane.SetActive(false);
        editButton.interactable = true;
        selectionDialog.Hide();
        confirmation.TryCancel();
        imagePreview.TryCancel();
        menu.Reset();
        FinishMapEdit();
        string selectedKey = project == value ? selectedRecord?.Key : null;
        string pageKey = project == value ? selectedPage?.Key : null;
        string sectorKey = project == value ? mapSector?.Key : null;
        string restoredSection = project == value ? section : "General";
        if (project != value)
        {
            previewScenario = value.Definition.DefaultScenarioID;
            searchInput.SetTextWithoutNotify(string.Empty);
            navigation.Reset();
            form.ResetGenerationSelection();
        }
        StopAudioPreview();
        xml.Clear();
        form.Clear();
        selectedRecord = null;
        selectedPage = null;
        textRedoTarget = null;
        project = value;
        assets = contentAssets;
        ContentBindings.Apply(gameObject, contentAssets);
        catalogs = value.GetCatalogs();
        map.Bind(value, contentAssets);
        addUnitButton.gameObject.SetActive(
            catalogs.Any(catalog =>
                catalog.CanAddUnit
                || catalog.CanAddEvent
                || catalog.CanAddTrait
                || catalog.CanAddFaction
                || catalog.IsMap
            )
        );
        section = "General";
        mapSector = null;
        packDialog.Hide();
        navigation.Render(catalogs);
        RefreshProjectState();
        ContentEditorTreeNode restoredPage = navigation.Find(pageKey);
        if (restoredPage?.Fields != null)
        {
            SelectRecord(restoredPage.Record, restoredPage);
            if (restoredSection != "General")
                ShowSection(restoredSection);
            return;
        }
        if (restoredPage?.Catalog?.IsMap == true && restoredPage.Record == null)
        {
            SelectRecord(restoredPage.Catalog.DocumentRecord, restoredPage);
            return;
        }
        ContentPackEditorRecord selected =
            selectedKey == null
                ? null
                : catalogs
                    .SelectMany(catalog =>
                        catalog.Records.Concat(catalog.Records.SelectMany(record => record.Planets))
                    )
                    .FirstOrDefault(record => record.Key == selectedKey);
        selected ??=
            catalogs.FirstOrDefault(catalog => catalog.Name == "Officers")?.Records.FirstOrDefault()
            ?? catalogs.SelectMany(catalog => catalog.Records).FirstOrDefault();
        if (selected != null)
        {
            navigation.Reveal(selected);
            ContentPackEditorRecord sector =
                sectorKey == null
                    ? null
                    : catalogs
                        .SelectMany(catalog => catalog.Records)
                        .FirstOrDefault(record => record.Key == sectorKey);
            ShowRecord(selected, null, sector);
            if (selected.Key == selectedKey && restoredSection != "General")
                ShowSection(restoredSection);
        }
        else
        {
            form.HidePreview();
            map.gameObject.SetActive(false);
            form.gameObject.SetActive(false);
            tabRoot.gameObject.SetActive(false);
            xml.Hide();
            duplicateButton.interactable = false;
            deleteButton.interactable = false;
            recordTitle.text = value.Definition.DisplayName;
            recordSubtitle.text = "Choose an entry from the content tree to edit.";
            RenderRecords();
        }
    }

    /// <summary>
    /// Updates the pack's unsaved marker and current record caption.
    /// </summary>
    public void RefreshProjectState()
    {
        if (project != null)
            projectLabel.text =
                project.Definition.DisplayName + (project.IsDirty ? "  /  Unsaved changes" : "");
        if (selectedRecord == null)
            return;
        recordTitle.text =
            selectedRecord.Kind == "GameGenerationConfig"
                ? "Starting setup"
                : selectedPage?.Caption ?? selectedRecord.Name;
        string kind =
            selectedPage?.MissionID != null ? "Mission"
            : selectedRecord.Kind is "PlanetSector" or "PlanetSectors" ? "Galaxy"
            : selectedRecord.Kind == "GameConfig" ? "Game rules"
            : selectedRecord.Kind == "GameGenerationConfig" ? "New-game settings"
            : selectedRecord.Kind == "Faction" ? "Faction"
            : ContentPackEditorField.FormatName(selectedRecord.Kind);
        string faction = navigation.GetUnitGroup(selectedRecord);
        recordSubtitle.text =
            kind
            + (
                kind == "Faction"
                || selectedPage?.MissionID != null
                || faction == "Shared" && !ContentEditorUnitFactory.CanCreate(selectedRecord.Kind)
                    ? string.Empty
                    : " · " + faction
            )
            + (selectedRecord.Catalog.Document.IsDirty ? " · Unsaved changes" : string.Empty);
    }

    /// <summary>
    /// Commits pending edits and rejects operations while an invalid draft remains.
    /// </summary>
    /// <returns>True when the current form contains no rejected drafts.</returns>
    public bool CommitFields()
    {
        if (selectedRecord != null && xml.HasDraft)
        {
            if (!ApplyXml())
                return false;
        }
        return form.Commit();
    }

    /// <summary>
    /// Commits drafts and validates visible accepted fields for an explicit save operation.
    /// </summary>
    /// <returns>Whether the current form can be saved.</returns>
    public bool ValidateFields() => CommitFields() && form.Validate();

    /// <summary>
    /// Enables or disables authoring input while keeping operation status visible.
    /// </summary>
    /// <param name="busy">Whether a cancellable operation owns the workspace.</param>
    public void SetBusy(bool busy)
    {
        workbench.interactable = !busy;
        workbench.blocksRaycasts = !busy;
    }

    /// <summary>
    /// Presents an operation result near its controls or inside the active pack dialog.
    /// </summary>
    /// <param name="message">The operation result.</param>
    /// <param name="isError">Whether the operation failed.</param>
    public void RenderStatus(string message, bool isError)
    {
        if (isError && noticeText.text != message)
            Debug.LogWarning("Content editor: " + message);
        if (isError && packDialog.IsOpen)
        {
            packDialog.ShowError(message);
            return;
        }
        noticeText.text = message;
        float height = noticeText
            .GetPreferredValues(message, noticeText.rectTransform.rect.width, 0)
            .y;
        ((RectTransform)notice.transform).SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            Mathf.Max(80, height + 24)
        );
        noticeText
            .GetComponent<ContentEditorColorBinding>()
            .SetRole(isError ? ContentEditorColorRole.Error : ContentEditorColorRole.Success);
        notice.SetActive(true);
    }

    /// <summary>
    /// Offers project operations with no direct installed-content editing.
    /// </summary>
    private void ShowFileMenu()
    {
        ShowMenu(
            fileButton,
            new ContentEditorMenuCommand(
                "New content pack...",
                ShowNewPackDialog,
                shortcut: ShortcutModifier + "+N"
            ),
            new ContentEditorMenuCommand(
                "Open content pack...",
                RequestOpenPack,
                shortcut: ShortcutModifier + "+O"
            ),
            new ContentEditorMenuCommand(
                "Save pack",
                () => SaveRequested?.Invoke(),
                project != null,
                shortcut: ShortcutModifier + "+S"
            ),
            new ContentEditorMenuCommand(
                "Validate pack",
                () => ValidateRequested?.Invoke(),
                project != null
            ),
            new ContentEditorMenuCommand(
                "Export pack...",
                () => ExportRequested?.Invoke(),
                project != null
            ),
            new ContentEditorMenuCommand(
                "Close pack",
                () => CloseRequested?.Invoke(),
                project != null,
                shortcut: ShortcutModifier + "+W"
            ),
            new ContentEditorMenuCommand("Exit", () => ExitRequested?.Invoke())
        );
    }

    /// <summary>
    /// Requests the name, working folder, and inheritance source for a new pack.
    /// </summary>
    public void ShowNewPackDialog()
    {
        if (!CommitFields())
            return;
        packDialog.Show();
    }

    /// <summary>
    /// Opens the operating system's file dialog to choose a pack manifest.
    /// </summary>
    private void RequestOpenPack() => OpenRequested?.Invoke();

    /// <summary>
    /// Asks how to handle pending edits before replacing a project or closing the app.
    /// </summary>
    /// <param name="save">The save-and-continue operation.</param>
    /// <param name="discard">The discard-and-continue operation.</param>
    public void ConfirmUnsaved(Action save, Action discard)
    {
        confirmation.Show(
            "Save changes to " + project.Definition.DisplayName + "?",
            "Save",
            save,
            "Discard",
            discard
        );
    }

    /// <summary>
    /// Allows a rejected draft to be discarded without requiring it to become valid first.
    /// </summary>
    /// <param name="discard">The operation that explicitly discards pending edits.</param>
    public void ConfirmDiscard(Action discard)
    {
        confirmation.Show("Discard unsaved changes?", "Discard", discard);
    }

    /// <summary>
    /// Clears project-owned forms before the controller releases their assets.
    /// </summary>
    public void ClearProject()
    {
        selectionDialog.Hide();
        form.ResetGenerationSelection();
        xml.Clear();
        FinishMapEdit();
        map.Clear();
        StopAudioPreview();
        imagePreview.TryCancel();
        selectedRecord = null;
        selectedPage = null;
        textRedoTarget = null;
        project = null;
        projectWorkspace.SetActive(false);
        welcomePane.SetActive(true);
        editButton.interactable = false;
        catalogs = Array.Empty<ContentPackEditorCatalog>();
        recordList.Clear();
        navigation.Reset();
        form.Clear();
        form.HidePreview();
        map.gameObject.SetActive(false);
        form.gameObject.SetActive(false);
        tabRoot.gameObject.SetActive(false);
        xml.Hide();
        duplicateButton.interactable = deleteButton.interactable = false;
        recordTitle.text = "Create or open a content pack";
        recordSubtitle.text = "File / New / Content pack";
        projectLabel.text = "No content pack open";
        addUnitButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// Opens an entry or expands one branch of the content tree.
    /// </summary>
    /// <param name="node">The selected hierarchy row.</param>
    private void SelectNode(ContentEditorTreeNode node)
    {
        if (node.CreationCatalog != null)
        {
            if (node.CreationParent != null)
                AddPlanet(node.CreationParent);
            else
                AddRecord(node.CreationCatalog, node.CreationFactionInstanceID);
            return;
        }
        if (node.Record == null && node.Catalog == null)
        {
            navigation.Toggle(node);
            RenderRecords(revealSelection: false);
            return;
        }
        if (!ResolveXmlDraft(() => SelectNode(node)))
            return;
        if (!CommitFields())
            return;
        if (node.Children.Count > 0)
            navigation.Toggle(node);
        if (node.Record != null)
        {
            SelectRecord(node.Record, node);
        }
        else if (node.Catalog?.IsMap == true)
        {
            mapSector = null;
            SelectRecord(node.Catalog.DocumentRecord, node);
        }
        else
        {
            RenderRecords(revealSelection: false);
        }
    }

    /// <summary>
    /// Keeps the clear action synchronized with the current navigation filter.
    /// </summary>
    /// <param name="text">The entered filter.</param>
    private void HandleSearchChanged(string text) => RenderRecords(revealSelection: false);

    /// <summary>
    /// Removes the navigation filter and reveals the selected entry in its hierarchy.
    /// </summary>
    private void ClearSearch()
    {
        searchInput.SetTextWithoutNotify(string.Empty);
        RenderRecords();
    }

    /// <summary>
    /// Expands a disclosure control without changing the selected entry.
    /// </summary>
    /// <param name="node">The branch to expand or collapse.</param>
    private void ToggleBranch(ContentEditorTreeNode node)
    {
        navigation.Toggle(node);
        RenderRecords(revealSelection: false);
    }

    /// <summary>
    /// Renders the hierarchy through the shared selectable-list control.
    /// </summary>
    /// <param name="revealSelection">Whether to scroll to the selected record instead of retaining the viewport.</param>
    private void RenderRecords(bool revealSelection = true)
    {
        if (project == null)
            return;
        bool filtered = !string.IsNullOrEmpty(searchInput.text);
        clearSearchButton.gameObject.SetActive(filtered);
        searchInput.textViewport.offsetMax = new Vector2(
            filtered ? -62 : -8,
            searchInput.textViewport.offsetMax.y
        );
        float scrollOffset = recordScroll.ScrollArea.ContentRoot.anchoredPosition.y;
        bool selectionWasVisible = navigation.GetRows(searchInput.text).Any(IsSelected);
        navigation.Render(catalogs);
        IReadOnlyList<ContentEditorTreeNode> rows = navigation.GetRows(searchInput.text);
        if (selectionWasVisible && !rows.Any(IsSelected))
        {
            navigation.Reveal(selectedRecord, selectedPage?.Key);
            rows = navigation.GetRows(searchInput.text);
            revealSelection = true;
        }
        recordScroll.SetContentHeight(rows.Count * 36);
        recordList.Render(
            rows,
            rows.Count * 36,
            36,
            false,
            36,
            (row, node, index) =>
                row.Render(
                    node,
                    index,
                    IsSelected(node),
                    navigation.IsExpanded(node) || !string.IsNullOrEmpty(searchInput.text),
                    assets,
                    string.IsNullOrWhiteSpace(searchInput.text) ? ToggleBranch : null
                ),
            revealSelection ? (node, _) => IsSelected(node) : null
        );
        if (!revealSelection)
            recordScroll.ScrollArea.RevealContentRect(
                scrollOffset,
                recordScroll.ScrollArea.ViewportHeight
            );
    }

    /// <summary>
    /// Distinguishes logical rules pages that share the same authored XML record.
    /// </summary>
    /// <param name="node">The row being rendered.</param>
    /// <returns>Whether this row represents the selected page.</returns>
    private bool IsSelected(ContentEditorTreeNode node)
    {
        if (selectedPage?.Key == node.Key)
            return true;
        return node.Record != null
            && node.Record.Element == selectedRecord?.Element
            && (selectedPage == null || selectedPage.Key == node.Key);
    }

    /// <summary>
    /// Presents history and record operations.
    /// </summary>
    private void ShowEditMenu()
    {
        ShowMenu(
            editButton,
            new ContentEditorMenuCommand(
                "Undo",
                () => UndoRequested?.Invoke(),
                project?.CanUndo == true || CanUndoText,
                shortcut: ShortcutModifier + "+Z"
            ),
            new ContentEditorMenuCommand(
                "Redo",
                () => RedoRequested?.Invoke(),
                project?.CanRedo == true || CanRedoText,
                shortcut: ShortcutModifier == "Cmd" ? "Cmd+Shift+Z" : "Ctrl+Y"
            ),
            new ContentEditorMenuCommand(
                "Duplicate",
                DuplicateRecord,
                selectedRecord?.Catalog.CanDuplicate == true
            ),
            new ContentEditorMenuCommand(
                "Delete",
                ConfirmDeleteRecord,
                selectedRecord?.Kind == "Faction" || selectedRecord?.Catalog.CanDuplicate == true
            )
        );
    }

    /// <summary>
    /// Applies an editor presentation without committing or rebuilding any current input draft.
    /// </summary>
    /// <param name="mode">The light or dark palette.</param>
    public void SetColorMode(ContentEditorColorMode mode) => appearance.SetMode(mode);

    /// <summary>
    /// Changes the content size without scaling application menu text.
    /// </summary>
    /// <param name="amount">The signed scale adjustment.</param>
    public void ZoomContent(float amount) => workbenchLayout.SetZoom(workbenchLayout.Zoom + amount);

    /// <summary>
    /// Restores application presentation independently of the currently open content pack.
    /// </summary>
    /// <param name="settings">The saved application preferences.</param>
    public void ApplySettings(ContentEditorSettings settings)
    {
        SetColorMode(settings.ColorMode);
        workbenchLayout.SetZoom(settings.Zoom);
        workbenchLayout.SetSplit(settings.Split);
    }

    /// <summary>
    /// Publishes explicit layout edits without treating a window resize as a preference change.
    /// </summary>
    private void HandleWorkbenchChanged() =>
        LayoutChanged?.Invoke(workbenchLayout.Zoom, workbenchLayout.Split);

    /// <summary>
    /// Presents the one-time alpha compatibility notice with an explicit acknowledgement.
    /// </summary>
    /// <param name="confirmed">Persists the user's acknowledgement.</param>
    public void ShowAlphaNotice(Action confirmed) =>
        confirmation.Show(
            "This editor is in alpha.\n\nContent packs are not guaranteed to work between versions during the alpha.",
            "Confirm",
            confirmed,
            allowCancel: false,
            title: "WARNING"
        );

    /// <summary>
    /// Routes desktop key events through the same intents as menu actions, including rapid repeats.
    /// </summary>
    /// <param name="key">The incoming native key event.</param>
    /// <returns>Whether the editor consumed the shortcut.</returns>
    public bool HandleKeyboardShortcut(Event key)
    {
        if (
            key?.type == EventType.KeyDown
            && key.keyCode == KeyCode.Escape
            && workbench.interactable
        )
        {
            bool handled = TryCancel();
            if (handled)
                key.Use();
            return handled;
        }
        if (
            !workbench.interactable
            || packDialog.IsOpen
            || selectionDialog.IsOpen
            || confirmation.gameObject.activeSelf
            || imagePreview.gameObject.activeSelf
        )
            return false;
        if (
            key == null
            || key.type != EventType.KeyDown
            || (!key.control && !key.command)
            || key.alt
        )
            return false;
        Action action = key.keyCode switch
        {
            KeyCode.Z when key.shift => () => RedoRequested?.Invoke(),
            KeyCode.Z => () => UndoRequested?.Invoke(),
            KeyCode.Y => () => RedoRequested?.Invoke(),
            KeyCode.S => () => SaveRequested?.Invoke(),
            KeyCode.O => RequestOpenPack,
            KeyCode.N => ShowNewPackDialog,
            KeyCode.W => () => CloseRequested?.Invoke(),
            KeyCode.Equals or KeyCode.Plus or KeyCode.KeypadPlus => () => ZoomContent(0.1f),
            KeyCode.Minus or KeyCode.KeypadMinus => () => ZoomContent(-0.1f),
            KeyCode.Alpha0 or KeyCode.Keypad0 => () => ZoomContent(1 - workbenchLayout.Zoom),
            _ => null,
        };
        if (action == null)
            return false;
        action();
        key.Use();
        return true;
    }

    /// <summary>
    /// Presents content sizing and appearance commands with their keyboard shortcuts.
    /// </summary>
    private void ShowViewMenu()
    {
        ShowMenu(
            viewButton,
            new ContentEditorMenuCommand(
                "Zoom",
                children: new[]
                {
                    new ContentEditorMenuCommand(
                        "Zoom in",
                        () => ZoomContent(0.1f),
                        shortcut: ShortcutModifier + "+Plus"
                    ),
                    new ContentEditorMenuCommand(
                        "Zoom out",
                        () => ZoomContent(-0.1f),
                        shortcut: ShortcutModifier + "+Minus"
                    ),
                    new ContentEditorMenuCommand(
                        "Actual size (100%)",
                        () => ZoomContent(1 - workbenchLayout.Zoom),
                        shortcut: ShortcutModifier + "+0"
                    ),
                }
            ),
            new ContentEditorMenuCommand(
                "Theme",
                children: Enum.GetValues(typeof(ContentEditorColorMode))
                    .Cast<ContentEditorColorMode>()
                    .Select(mode => new ContentEditorMenuCommand(
                        mode.ToString(),
                        () =>
                        {
                            SetColorMode(mode);
                            ColorModeChanged?.Invoke(mode);
                        },
                        isChecked: mode == ColorMode
                    ))
                    .ToArray()
            )
        );
    }

    /// <summary>
    /// Presents playable factions and test settings from the current authored scenario.
    /// </summary>
    private void ShowTestMenu()
    {
        if (project == null)
        {
            ShowMenu(
                testButton,
                new ContentEditorMenuCommand("Open a content pack first", enabled: false)
            );
            return;
        }
        ContentPackEditorRecord[] scenarios = catalogs
            .SelectMany(catalog => catalog.Records)
            .Where(record => record.Kind == "Scenario")
            .ToArray();
        ContentPackEditorRecord selected =
            scenarios.FirstOrDefault(record => record.ID == previewScenario)
            ?? scenarios.FirstOrDefault();
        previewScenario = selected?.ID;
        IReadOnlyDictionary<string, string> factions = project.GetFactionOptions();
        string[] playable =
            selected
                ?.Element.Element("PlayableFactionIDs")
                ?.Elements()
                .Select(element => element.Value)
                .ToArray()
            ?? Array.Empty<string>();
        List<ContentEditorMenuCommand> commands = playable
            .Select(id => new ContentEditorMenuCommand(
                "Play as " + (factions.TryGetValue(id, out string caption) ? caption : id),
                () =>
                    PlayRequested?.Invoke(
                        new ContentEditorPlayOptions(
                            previewScenario,
                            id,
                            previewSize,
                            previewDifficulty
                        )
                    )
            ))
            .ToList();
        if (commands.Count == 0)
            commands.Add(new ContentEditorMenuCommand("No playable factions", enabled: false));
        commands.Add(
            new ContentEditorMenuCommand(
                "Galaxy size: " + previewSize,
                children: Enum.GetValues(typeof(GameSize))
                    .Cast<GameSize>()
                    .Select(size => new ContentEditorMenuCommand(
                        size.ToString(),
                        () => previewSize = size,
                        isChecked: size == previewSize
                    ))
                    .ToArray()
            )
        );
        commands.Add(
            new ContentEditorMenuCommand(
                "Difficulty: " + previewDifficulty,
                children: Enum.GetValues(typeof(GameDifficulty))
                    .Cast<GameDifficulty>()
                    .Select(difficulty => new ContentEditorMenuCommand(
                        difficulty.ToString(),
                        () => previewDifficulty = difficulty,
                        isChecked: difficulty == previewDifficulty
                    ))
                    .ToArray()
            )
        );
        ShowMenu(testButton, commands.ToArray());
    }

    /// <summary>
    /// Selects a record and configures its property, artwork, or map inspector.
    /// </summary>
    /// <param name="record">The selected record.</param>
    /// <param name="page">The optional logical inspector page.</param>
    private void SelectRecord(ContentPackEditorRecord record, ContentEditorTreeNode page = null)
    {
        ContentPackEditorRecord sector = record?.Kind switch
        {
            "PlanetSector" => record,
            "Planet" => ContentPackEditorProjector.GetParentSector(record),
            _ => null,
        };
        ShowRecord(record, page, sector);
    }

    /// <summary>
    /// Presents a selected definition in the requested galaxy or sector context.
    /// </summary>
    /// <param name="record">The selected definition.</param>
    /// <param name="page">The optional logical inspector page.</param>
    /// <param name="sector">The sector to open, or null to retain the galaxy overview.</param>
    private void ShowRecord(
        ContentPackEditorRecord record,
        ContentEditorTreeNode page,
        ContentPackEditorRecord sector
    )
    {
        FinishMapEdit();
        if (!ResolveXmlDraft(() => ShowRecord(record, page, sector)))
            return;
        StopAudioPreview();
        if (!CommitFields())
            return;
        SelectionRequested?.Invoke(record, page, sector);
    }

    /// <summary>
    /// Renders the controller-owned selection without deciding which object or tab is selected.
    /// </summary>
    /// <param name="data">The selected record and derived inspector context.</param>
    internal void RenderSelection(ContentPackEditorSelection data)
    {
        ContentPackEditorRecord record = data.Record;
        ContentEditorTreeNode page = data.Page;
        ContentPackEditorRecord sector = data.Sector;
        selectedRecord = record;
        selectedPage = data.Page;
        section = data.Section;
        xml.Bind(record, selectedPage?.Fields);
        textRedoTarget = null;
        navigation.Render(catalogs);
        navigation.Reveal(record, page?.Key);
        xml.Hide();
        bool isMap = record?.Catalog.IsMap == true;
        mapSector = sector;
        form.HidePreview();
        map.gameObject.SetActive(isMap);
        tabRoot.gameObject.SetActive(record != null);
        form.gameObject.SetActive(record != null);
        duplicateButton.interactable = record?.Catalog.CanDuplicate == true;
        deleteButton.interactable = record?.Kind == "Faction" || duplicateButton.interactable;
        if (record == null)
        {
            recordTitle.text = "No matching entries";
            recordSubtitle.text = "Choose another category or faction, or clear the search.";
            return;
        }
        RefreshProjectState();
        if (record.Kind == "GameEvent")
        {
            ShowEventConfiguration();
            RenderRecords();
            return;
        }
        RectTransform fieldRect = (RectTransform)form.transform;
        if (isMap)
        {
            fieldRect.anchorMin = new Vector2(1, 0);
            fieldRect.anchorMax = Vector2.one;
            fieldRect.offsetMin = new Vector2(-360, 0);
            fieldRect.offsetMax = new Vector2(0, -136);
            RenderMap();
        }
        else
        {
            fieldRect.anchorMin = Vector2.zero;
            fieldRect.anchorMax = Vector2.one;
            fieldRect.offsetMin = Vector2.zero;
            fieldRect.offsetMax = new Vector2(0, -136);
        }
        RenderFields();
        RenderRecords();
    }

    /// <summary>
    /// Switches between property, media, and description forms.
    /// </summary>
    /// <param name="name">The inspector section.</param>
    private void ShowSection(string name)
    {
        if (!ResolveXmlDraft(() => ShowSection(name)))
            return;
        if (!CommitFields())
            return;
        if (name != "Audio")
            StopAudioPreview();
        SectionRequested?.Invoke(name);
    }

    /// <summary>
    /// Displays the inspector tab selected by the workspace session.
    /// </summary>
    /// <param name="name">The inspector tab to present.</param>
    internal void RenderSection(string name)
    {
        section = name;
        RenderTabSelection();
        bool advanced = section == "Advanced";
        if (advanced)
            xml.Show();
        else
            xml.Hide();
        form.gameObject.SetActive(!advanced);
        map.gameObject.SetActive(
            !advanced && name != "Encyclopedia" && selectedRecord?.Catalog.IsMap == true
        );
        RenderFields();
    }

    /// <summary>
    /// Marks the active inspector tab and omits media tabs with no relevant fields.
    /// </summary>
    private void RenderTabSelection()
    {
        if (selectedRecord == null)
            return;
        advancedTab.gameObject.SetActive(selectedRecord.Kind != "PlanetSectors");
        IReadOnlyList<ContentPackEditorField> fields = ContentEditorInspectorLayout.GetFields(
            selectedRecord
        );
        encyclopediaTab.gameObject.SetActive(
            ContentEditorContentSections.HasEncyclopedia(selectedRecord)
        );
        artworkTab.gameObject.SetActive(
            selectedRecord.Kind != "PlanetSectors"
                && fields.Any(field =>
                    field.IsMedia
                    && field.MediaType != ContentPackEditorMediaType.Audio
                    && !ContentEditorContentSections.IsEncyclopediaField(field)
                )
        );
        audioTab.gameObject.SetActive(
            selectedRecord.Kind != "PlanetSectors"
                && fields.Any(field => field.MediaType == ContentPackEditorMediaType.Audio)
        );
        if (selectedPage?.Fields != null)
        {
            encyclopediaTab.gameObject.SetActive(false);
            artworkTab.gameObject.SetActive(false);
            audioTab.gameObject.SetActive(false);
        }
        bool faction = selectedPage?.Key.StartsWith("factions/", StringComparison.Ordinal) == true;
        bool generation = selectedRecord.Kind == "GameGenerationConfig";
        propertiesTab.gameObject.SetActive(!generation);
        if (faction)
            artworkTab.gameObject.SetActive(
                selectedPage.Fields.Any(ContentEditorContentSections.IsFactionArtworkField)
            );
        if (generation)
        {
            encyclopediaTab.gameObject.SetActive(false);
            artworkTab.gameObject.SetActive(false);
            audioTab.gameObject.SetActive(false);
        }
        rulesTab.gameObject.SetActive(
            faction
                || selectedPage?.MissionID != null
                    && selectedPage.Fields.Any(field => field.Record.Kind == "GameConfig")
        );
        shipNamesTab.gameObject.SetActive(
            faction
                && selectedPage.RelatedRecords.Any(record =>
                    record.Kind == "Faction" && record.Element.Element("InstanceID") != null
                )
        );
        factionMessagesTab.gameObject.SetActive(
            faction
                && selectedPage.RelatedRecords.Any(record =>
                    record.Kind == "Faction" && record.Element.Element("InstanceID") != null
                )
        );
        referenceTab.gameObject.SetActive(
            faction && selectedPage.RelatedRecords.Any(record => record.Kind == "EncyclopediaEntry")
        );
        foreach (Button tab in generationTabs)
            tab.gameObject.SetActive(generation);
        Button[] tabs =
            generation ? generationTabs.Concat(new[] { advancedTab }).ToArray()
            : faction
                ? new[]
                {
                    propertiesTab,
                    rulesTab,
                    shipNamesTab,
                    factionMessagesTab,
                    artworkTab,
                    referenceTab,
                    advancedTab,
                }
            : new[] { propertiesTab, encyclopediaTab, artworkTab, audioTab, rulesTab, advancedTab };
        string[] names =
            generation
                ? ContentEditorContentSections
                    .GenerationSections.Concat(new[] { "Advanced" })
                    .ToArray()
            : faction
                ? new[]
                {
                    "General",
                    "Rules",
                    "Ship names",
                    "Messages",
                    "Artwork",
                    "Encyclopedia",
                    "Advanced",
                }
            : new[] { "General", "Encyclopedia", "Artwork", "Audio", "Rules", "Advanced" };
        if (
            !Enumerable
                .Range(0, tabs.Length)
                .Any(index => tabs[index].gameObject.activeSelf && names[index] == section)
        )
            SectionRequested?.Invoke(names[0]);
        List<(RectTransform Rect, float Width)> layout = new();
        for (int i = 0; i < tabs.Length; i++)
        {
            if (!tabs[i].gameObject.activeSelf)
                continue;
            TMP_Text caption = tabs[i].GetComponentInChildren<TMP_Text>();
            layout.Add(
                (
                    (RectTransform)tabs[i].transform,
                    Mathf.Max(64, caption.GetPreferredValues(caption.text).x + 28)
                )
            );
            tabs[i].transform.Find("Selection").gameObject.SetActive(section == names[i]);
        }
        inspectorTop = 102 + UILayout.ArrangeWrappingRow(layout, tabRoot.rect.width, 34, 6);
        tabRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, inspectorTop - 102);
        xml.SetHeaderOffset(inspectorTop);
    }

    /// <summary>
    /// Refreshes inspector layout while keeping preview and form ownership separate.
    /// </summary>
    /// <param name="rebind">Whether to replace field bindings instead of preserving drafts.</param>
    private void RenderFields(bool rebind = true)
    {
        if (selectedRecord?.Kind == "GameEvent")
            return;
        if (selectedPage != null)
            selectedPage = navigation.Find(selectedPage.Key) ?? selectedPage;
        RenderTabSelection();
        if (selectedRecord?.Catalog.IsMap == true)
        {
            RectTransform rect = (RectTransform)form.transform;
            bool showMap = section != "Encyclopedia";
            float propertiesWidth = Mathf.Min(360, ((RectTransform)rect.parent).rect.width * 0.42f);
            rect.anchorMin = showMap ? Vector2.right : Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = showMap ? new Vector2(-propertiesWidth, 0) : Vector2.zero;
            rect.offsetMax = new Vector2(0, -inspectorTop);
            ((RectTransform)map.transform).offsetMax = new Vector2(
                -propertiesWidth - 30,
                -inspectorTop
            );
            form.gameObject.SetActive(section != "Advanced");
        }
        if (rebind)
            textRedoTarget = null;
        ((RectTransform)form.transform).offsetMax = new Vector2(0, -inspectorTop);
        form.Render(project, assets, selectedRecord, selectedPage, section, rebind);
    }

    /// <summary>
    /// Refreshes workspace summaries after a form commits a field.
    /// </summary>
    private void HandleFieldChanged()
    {
        RefreshProjectState();
        RenderRecords(revealSelection: false);
        fieldLayoutDirty = true;
        if (selectedRecord?.Catalog.IsMap == true)
            RenderMap();
    }

    /// <summary>
    /// Displays one field validation failure through the workspace status.
    /// </summary>
    /// <param name="message">The concise validation message.</param>
    private void ReportFieldError(string message) => RenderStatus(message, true);

    /// <summary>
    /// Applies valid XML as one reversible operation and rebuilds the editor.
    /// </summary>
    /// <returns>Whether the XML was accepted.</returns>
    private bool ApplyXml()
    {
        if (selectedRecord == null)
            return false;
        try
        {
            XmlSubmitted?.Invoke(xml.Scope, xml.Text, xml.ScopeIndex);
            return true;
        }
        catch (Exception exception)
        {
            int line =
                exception is System.Xml.XmlException xml ? xml.LineNumber
                : exception is System.Xml.Schema.XmlSchemaException schema ? schema.LineNumber
                : 0;
            if (
                selectedRecord.Kind == "GameEvent"
                && exception is System.Xml.Schema.XmlSchemaException
                && line > 1
            )
                line--;
            RenderStatus(
                (line > 0 ? "Line " + line + ": " : string.Empty) + exception.Message,
                true
            );
            return false;
        }
    }

    /// <summary>
    /// Offers to discard an unapplied XML draft before changing its editing context.
    /// </summary>
    /// <param name="next">The navigation to resume after an explicit discard.</param>
    /// <returns>Whether navigation can proceed immediately.</returns>
    private bool ResolveXmlDraft(Action next)
    {
        if (!xml.HasDraft)
            return true;
        confirmation.Show(
            "Discard XML changes?",
            "Discard",
            () =>
            {
                xml.ResetDraft();
                next();
            }
        );
        return false;
    }

    /// <summary>
    /// Requests applying the displayed XML draft.
    /// </summary>
    private void ApplyXmlDraft() => ApplyXml();

    /// <summary>
    /// Requests explicit confirmation before resetting an XML draft.
    /// </summary>
    private void RequestDiscardXml() => ResolveXmlDraft(() => { });

    /// <summary>
    /// Switches XML blocks only after their current draft has been handled.
    /// </summary>
    /// <param name="index">The proposed block index.</param>
    private void RequestXmlScope(int index)
    {
        if (ResolveXmlDraft(() => xml.SelectScope(index)))
            xml.SelectScope(index);
    }

    /// <summary>
    /// Shows a complete event configuration instead of an incomplete generated action form.
    /// </summary>
    private void ShowEventConfiguration()
    {
        form.Clear();

        form.HidePreview();
        map.gameObject.SetActive(false);
        form.gameObject.SetActive(false);
        tabRoot.gameObject.SetActive(false);
        xml.Show(eventConfiguration: true);
    }

    /// <summary>
    /// Previews a media reference without changing the authored path.
    /// </summary>
    /// <param name="field">The media field.</param>
    /// <param name="address">The selected media item.</param>
    private void PreviewMedia(ContentPackEditorField field, string address)
    {
        if (!string.IsNullOrWhiteSpace(address))
            PreviewRequested?.Invoke(field, address);
    }

    /// <summary>
    /// Displays a texture resolved by the application controller.
    /// </summary>
    /// <param name="texture">The resolved artwork.</param>
    /// <param name="address">The content address shown in the preview caption.</param>
    public void ShowImagePreview(Texture2D texture, string address) =>
        imagePreview.Show(texture, address);

    /// <summary>
    /// Plays a clip whose loading and lifetime are owned by the application controller.
    /// </summary>
    /// <param name="clip">The resolved audio clip.</param>
    public void PlayPreviewAudio(AudioClip clip)
    {
        previewAudio.clip = clip;
        previewAudio.Play();
    }

    /// <summary>
    /// Cancels pending playback before changing the selected record or project.
    /// </summary>
    public void StopAudioPreview()
    {
        if (previewAudio != null)
        {
            previewAudio.Stop();
            previewAudio.clip = null;
        }
        PreviewCancelled?.Invoke();
    }

    /// <summary>
    /// Selects and imports replacement media into the current pack.
    /// </summary>
    /// <param name="field">The media member being edited.</param>
    private void ImportMedia(ContentPackEditorField field)
    {
        if (CommitFields())
            ImportRequested?.Invoke(field);
    }

    /// <summary>
    /// Forwards a completed creation form to the application controller.
    /// </summary>
    /// <param name="basePackID">The selected inheritance source.</param>
    /// <param name="packID">The authored pack identifier.</param>
    /// <param name="displayName">The entered display name.</param>
    /// <param name="version">The entered version.</param>
    /// <param name="folder">The complete working-pack directory.</param>
    private void ForwardCreateRequest(
        string basePackID,
        string packID,
        string displayName,
        string version,
        string folder
    ) => CreateRequested?.Invoke(basePackID, packID, displayName, version, folder);

    /// <summary>
    /// Requests application-owned native folder selection for the creation form.
    /// </summary>
    /// <param name="folder">The creation form's current parent folder.</param>
    private void ForwardBrowseRequest(string folder) => BrowseFolderRequested?.Invoke(folder);

    /// <summary>
    /// Displays a parent directory returned by the application's native picker.
    /// </summary>
    /// <param name="folder">The selected parent directory.</param>
    public void SetPackLocation(string folder) => packDialog.SetLocation(folder);

    /// <summary>
    /// Creates an independently identified copy of the selected record.
    /// </summary>
    private void DuplicateRecord()
    {
        if (selectedRecord != null && CommitFields())
            DuplicateRequested?.Invoke(selectedRecord);
    }

    /// <summary>
    /// Offers unit categories without choosing a faction or depending on the selected record.
    /// </summary>
    private void AddUnit()
    {
        if (!ResolveXmlDraft(AddUnit))
            return;
        if (project == null || !CommitFields())
            return;
        ContentPackEditorRecord targetSector =
            selectedRecord?.Catalog.IsMap == true ? mapSector : null;
        navigation.Render(catalogs);
        List<ContentEditorMenuCommand> commands = new();
        foreach (ContentPackEditorCatalog catalog in navigation.GetCreationCatalogs())
        {
            commands.Add(
                new ContentEditorMenuCommand(
                    catalog.CanAddFaction ? "Faction" : catalog.ItemName,
                    () => AddRecord(catalog)
                )
            );
            if (catalog.IsMap && targetSector != null)
                commands.Add(new ContentEditorMenuCommand("Planet", () => AddPlanet(targetSector)));
        }
        ShowMenu(addUnitButton, commands.ToArray());
    }

    /// <summary>
    /// Creates a blank record from the action at the end of its own catalog list.
    /// </summary>
    /// <param name="catalog">The catalog receiving the new entry.</param>
    /// <param name="factionInstanceID">The optional faction selected by the sidebar action.</param>
    private void AddRecord(ContentPackEditorCatalog catalog, string factionInstanceID = null)
    {
        if (!ResolveXmlDraft(() => AddRecord(catalog, factionInstanceID)) || !CommitFields())
            return;
        if (catalog.CanAddUnit)
            AddUnit(catalog, factionInstanceID);
        else if (catalog.CanAddFaction)
            AddFaction();
        else if (catalog.CanAddEvent)
            AddEvent(catalog);
        else if (catalog.IsMap)
            AddSector(catalog);
        else if (catalog.CanAddTrait)
        {
            AddTraitRequested?.Invoke(catalog);
        }
    }

    /// <summary>
    /// Creates and opens a new event without needing an existing event as a template.
    /// </summary>
    /// <param name="catalog">The destination events catalog.</param>
    private void AddEvent(ContentPackEditorCatalog catalog)
    {
        AddEventRequested?.Invoke(catalog);
    }

    /// <summary>
    /// Creates a faction through the same project history and navigation as other definitions.
    /// </summary>
    private void AddFaction()
    {
        if (ResolveXmlDraft(AddFaction) && CommitFields())
            AddFactionRequested?.Invoke();
    }

    /// <summary>
    /// Presents the warning requested by the workspace owner before adding an extra faction.
    /// </summary>
    /// <param name="confirmed">The operation to invoke after acknowledgement.</param>
    internal void ConfirmAdditionalFaction(Action confirmed) =>
        confirmation.Show(
            "Content packs with three or more factions have not been tested and are not guaranteed to work.",
            "Add faction",
            confirmed,
            title: "Warning"
        );

    /// <summary>
    /// Requests an atomic clear through the workspace's project owner.
    /// </summary>
    /// <param name="fields">The fields belonging to the selected table row.</param>
    private void ForwardClearFields(IReadOnlyList<ContentPackEditorField> fields) =>
        ClearFieldsRequested?.Invoke(fields);

    /// <summary>
    /// Requests an empty name pool for the faction represented by the current combined page.
    /// </summary>
    private void RequestNamePool()
    {
        if (CommitFields())
            AddNamePoolRequested?.Invoke(
                selectedPage.RelatedRecords.First(record =>
                    record.Kind == "Faction" && record.Element.Element("InstanceID") != null
                )
            );
    }

    /// <summary>
    /// Confirms deletion of one starting message independently of the faction definition.
    /// </summary>
    /// <param name="title">The selected message's title field.</param>
    private void ConfirmRemoveMessage(ContentPackEditorField title)
    {
        if (CommitFields())
            confirmation.Show(
                "Delete starting message \"" + title.Value + "\"?",
                "Delete message",
                () => RemoveMessageRequested?.Invoke(title)
            );
    }

    /// <summary>
    /// Offers the game's message categories for a new starting faction message.
    /// </summary>
    /// <param name="anchor">The form command that owns the popup.</param>
    private void ShowAddMessageMenu(Button anchor)
    {
        ShowMenu(
            anchor,
            Enum.GetValues(typeof(MessageType))
                .Cast<MessageType>()
                .Select(type => new ContentEditorMenuCommand(
                    ContentPackEditorField.FormatName(type.ToString()),
                    () => AddFactionMessage(type)
                ))
                .ToArray()
        );
    }

    /// <summary>
    /// Adds a starting message and reveals its editable title without copying another message.
    /// </summary>
    /// <param name="type">The selected message category.</param>
    private void AddFactionMessage(MessageType type)
    {
        if (!CommitFields())
            return;
        ContentPackEditorRecord faction = selectedPage.RelatedRecords.First(record =>
            record.Kind == "Faction" && record.Element.Element("InstanceID") != null
        );
        AddMessageRequested?.Invoke(faction, type);
    }

    /// <summary>
    /// Creates and selects a blank sector through the normal project history.
    /// </summary>
    /// <param name="catalog">The destination galaxy catalog.</param>
    private void AddSector(ContentPackEditorCatalog catalog)
    {
        if (ResolveXmlDraft(() => AddSector(catalog)) && CommitFields())
            AddSectorRequested?.Invoke(catalog, map.GetGalaxyPlacementPosition());
    }

    /// <summary>
    /// Creates and selects a blank planet in the explicitly chosen sector.
    /// </summary>
    /// <param name="sector">The destination sector.</param>
    private void AddPlanet(ContentPackEditorRecord sector)
    {
        if (!ResolveXmlDraft(() => AddPlanet(sector)) || !CommitFields())
            return;
        Vector2Int offset = map.GetPlacementOffset();
        AddPlanetRequested?.Invoke(
            sector,
            new Vector2Int(sector.X + offset.x, sector.Y + offset.y)
        );
    }

    /// <summary>
    /// Creates a unit with the selected faction scope and reveals its general form.
    /// </summary>
    /// <param name="catalog">The destination document for the selected unit category.</param>
    /// <param name="factionInstanceID">The optional recruiting or manufacturing faction.</param>
    private void AddUnit(ContentPackEditorCatalog catalog, string factionInstanceID = null)
    {
        if (CommitFields())
            AddUnitRequested?.Invoke(catalog, factionInstanceID);
    }

    /// <summary>
    /// Requests confirmation before removing the selected record.
    /// </summary>
    private void ConfirmDeleteRecord()
    {
        if (selectedRecord == null)
            return;
        confirmation.Show(
            "Delete "
                + selectedRecord.Name
                + (
                    selectedRecord.Kind == "Faction" ? " and its faction-specific data"
                    : selectedRecord.Planets.Count > 0
                        ? " and its " + selectedRecord.Planets.Count + " planets"
                    : string.Empty
                )
                + "?",
            "Delete",
            () =>
            {
                try
                {
                    DeleteRequested?.Invoke(selectedRecord);
                }
                catch (InvalidOperationException exception)
                {
                    RenderStatus(exception.Message, true);
                }
            }
        );
    }

    /// <summary>
    /// Renders the selected map without owning its projection or preview controls.
    /// </summary>
    private void RenderMap()
    {
        if (selectedRecord?.Catalog.IsMap == true)
            map.Render(selectedRecord, mapSector);
    }

    /// <summary>
    /// Refreshes moved coordinates without resetting map zoom, selection, or faction presentation.
    /// </summary>
    internal void ShowArrangedMap()
    {
        RefreshProjectState();
        RenderMap();
        RenderFields();
    }

    /// <summary>
    /// Returns from a sector preview to the galaxy.
    /// </summary>
    private void ReturnToGalaxy()
    {
        mapSector = null;
        ContentEditorTreeNode galaxy = navigation.Find("galaxy");
        SelectRecord(galaxy.Catalog.DocumentRecord, galaxy);
    }

    /// <summary>
    /// Commits the visible form before capturing a cancellable map-layout request.
    /// </summary>
    private void ArrangeMap()
    {
        FinishMapEdit();
        if (!ResolveXmlDraft(ArrangeMap) || !CommitFields())
            return;
        RenderMap();
        MapLayoutRequested?.Invoke(map.CreateLayout());
    }

    /// <summary>
    /// Selects a sector without opening its interior.
    /// </summary>
    /// <param name="record">The selected map definition.</param>
    private void SelectMapRecord(ContentPackEditorRecord record) => ShowRecord(record, null, null);

    /// <summary>
    /// Opens a sector or selects a planet within its sector.
    /// </summary>
    /// <param name="record">The opened definition.</param>
    private void OpenMapRecord(ContentPackEditorRecord record) => SelectRecord(record);

    /// <summary>
    /// Groups a map gesture into one project edit.
    /// </summary>
    /// <param name="record">The dragged definition.</param>
    private void BeginMapDrag(ContentPackEditorRecord record)
    {
        FinishMapEdit();
        MapEditStarted?.Invoke(record);
        mapDragging = true;
    }

    /// <summary>
    /// Applies a map position through the project ownership boundary.
    /// </summary>
    /// <param name="record">The dragged definition.</param>
    /// <param name="position">Its absolute source coordinates.</param>
    private void MoveMapRecord(ContentPackEditorRecord record, Vector2Int position)
    {
        MapMoved?.Invoke(record, position);
    }

    /// <summary>
    /// Completes a map edit and refreshes the form after the gesture.
    /// </summary>
    private void FinishMapDrag()
    {
        if (!mapDragging)
            return;
        FinishMapEdit();
        if (isActiveAndEnabled && project != null && selectedRecord != null)
            RenderFields();
    }

    /// <summary>
    /// Closes an active map edit before replacing or releasing its project.
    /// </summary>
    private void FinishMapEdit()
    {
        if (!mapDragging)
            return;
        mapDragging = false;
        MapEditEnded?.Invoke();
    }

    /// <summary>
    /// Opens an anchored popup through the shared context-menu view.
    /// </summary>
    /// <param name="anchor">The control owning the popup.</param>
    /// <param name="commands">The available operations.</param>
    private void ShowMenu(Button anchor, params ContentEditorMenuCommand[] commands)
    {
        menuAnchor = anchor;
        menu.transform.SetAsLastSibling();
        Vector3[] corners = new Vector3[4];
        ((RectTransform)anchor.transform).GetWorldCorners(corners);
        Vector3 point = menu.transform.InverseTransformPoint(corners[0]);
        RectTransform surface = (RectTransform)menu.transform;
        menu.OpenAt(
            this,
            Mathf.RoundToInt(point.x - surface.rect.xMin),
            Mathf.RoundToInt(surface.rect.yMax - point.y),
            0,
            commands.Select(ToMenuItem).ToArray(),
            new ContextMenuView.ContextMenuVisuals(
                appearance.ColorFor(ContentEditorColorRole.Text),
                appearance.ColorFor(ContentEditorColorRole.Accent),
                appearance.ColorFor(ContentEditorColorRole.Muted)
            )
        );
        menu.RenderCurrent();
    }

    /// <summary>
    /// Projects a semantic command tree into the shared menu presentation.
    /// </summary>
    /// <param name="command">The semantic command.</param>
    /// <returns>The menu item.</returns>
    private ContextMenuCommandItem ToMenuItem(ContentEditorMenuCommand command)
    {
        return new ContextMenuCommandItem(
            command,
            iconTexture: command.IsChecked ? checkmark : null,
            submenuCommands: command.Children.Select(ToMenuItem).ToArray(),
            shortcut: command.Shortcut
        );
    }

    /// <summary>
    /// Closes the popup before invoking its selected operation.
    /// </summary>
    /// <param name="command">The selected command.</param>
    private void HandleMenuSelection(IContextMenuCommand command)
    {
        menu.Reset();
        if (command is ContentEditorMenuCommand action)
            action.Execute();
    }

    /// <summary>
    /// Displays a newly created definition without applying its creation in the view.
    /// </summary>
    /// <param name="record">The definition created by the workspace controller.</param>
    internal void ShowCreatedRecord(ContentPackEditorRecord record)
    {
        searchInput.SetTextWithoutNotify(string.Empty);
        if (record.Kind == "Faction")
        {
            RenderProject(project, assets);
            navigation.Reveal(record);
            ContentEditorTreeNode page = navigation
                .GetRows(string.Empty)
                .First(node => node.Record?.Element == record.Element);
            SelectRecord(record, page);
            return;
        }
        if (record.Kind == "PlanetSector")
            mapSector = null;
        SelectRecord(record);
        if (record.Kind == "PlanetSector")
        {
            ContentEditorTreeNode branch = navigation.Find(record.Key);
            if (!navigation.IsExpanded(branch))
                navigation.Toggle(branch);
            RenderRecords();
        }
    }

    /// <summary>
    /// Removes stale presentation bindings after the controller deletes a definition.
    /// </summary>
    /// <param name="record">The removed definition.</param>
    /// <param name="parent">The remaining parent sector, if any.</param>
    internal void ShowRemovedRecord(ContentPackEditorRecord record, ContentPackEditorRecord parent)
    {
        form.Clear();
        xml.Clear();
        if (record.Kind == "Faction")
        {
            selectedRecord = null;
            selectedPage = null;
            searchInput.SetTextWithoutNotify(string.Empty);
            RenderProject(project, assets);
            return;
        }
        mapSector = parent;
        SelectRecord(
            parent?.Planets.FirstOrDefault() ?? parent ?? record.Catalog.Records.FirstOrDefault()
        );
    }

    /// <summary>
    /// Restores the inspector after the controller applies a scoped XML draft.
    /// </summary>
    /// <param name="record">The updated definition.</param>
    /// <param name="index">The previously visible XML scope.</param>
    internal void ShowAppliedXml(ContentPackEditorRecord record, int index)
    {
        selectedRecord = record;
        RenderProject(project, assets);
        if (selectedRecord.Kind != "GameEvent")
        {
            ShowSection("Advanced");
            xml.SelectScope(Mathf.Min(index, xml.ScopeCount - 1));
        }
    }

    /// <summary>
    /// Reveals the field requested by a completed workspace operation.
    /// </summary>
    /// <param name="field">The field to focus.</param>
    internal void FocusField(ContentPackEditorField field) => form.FocusField(field);

    /// <summary>
    /// Opens named multi-selection, retaining repeated building quantities and destination weights.
    /// </summary>
    /// <param name="field">The authored reference list.</param>
    private void ShowFieldSelection(ContentPackEditorField field)
    {
        string quantity =
            field.MemberName == "TargetPlanets" ? "Weight"
            : field.MemberName == "FacilityTypeIDs" ? "Quantity"
            : null;
        selectionDialog.Show(
            field.Caption,
            project.GetReferenceOptions(field),
            ItemSelection.Count(field.Values),
            counts =>
            {
                SubmitField(field, new ItemSelection(counts).ApplyTo(field.Values));
                RefreshInspector();
            },
            quantity
        );
    }

    /// <summary>
    /// Opens named unit selection with explicit quantities for a placement or ship cargo.
    /// </summary>
    /// <param name="collection">The starting-unit collection.</param>
    private void ShowUnitSelection(ContentPackEditorCollection collection)
    {
        var counts = collection
            .Entries.GroupBy(entry => entry.Element("TypeID")?.Value ?? string.Empty)
            .ToDictionary(
                group => group.Key,
                group =>
                    checked((int)group.Sum(entry => (long)((int?)entry.Element("Count") ?? 0))),
                StringComparer.Ordinal
            );
        selectionDialog.Show(
            collection.Name == "Cargo" ? "Ship cargo" : "Starting units",
            project.GetCollectionUnitOptions(collection),
            counts,
            values =>
            {
                CollectionQuantitiesSubmitted?.Invoke(collection, values);
            },
            "Quantity"
        );
    }

    /// <summary>
    /// Forwards explicit entry creation to the workspace owner.
    /// </summary>
    /// <param name="collection">The destination collection.</param>
    private void ForwardCollectionAdd(ContentPackEditorCollection collection) =>
        CollectionAddRequested?.Invoke(collection);

    /// <summary>
    /// Confirms removal of exactly one selected setup entry.
    /// </summary>
    /// <param name="collection">The owning collection.</param>
    /// <param name="index">The selected entry.</param>
    private void ConfirmCollectionRemove(ContentPackEditorCollection collection, int index) =>
        confirmation.Show(
            "Delete this starting setup entry?",
            "Delete entry",
            () => CollectionRemoveRequested?.Invoke(collection, index)
        );

    /// <summary>
    /// Refreshes authored fields after a structural edit without replacing the workspace.
    /// </summary>
    internal void RefreshInspector()
    {
        form.Refresh();
        RefreshProjectState();
    }

    /// <summary>
    /// Forwards a semantic field edit to the workspace controller.
    /// </summary>
    /// <param name="field">The edited member.</param>
    /// <param name="values">The complete proposed values.</param>
    private void SubmitField(ContentPackEditorField field, IReadOnlyList<string> values) =>
        FieldSubmitted?.Invoke(field, values);

    /// <summary>
    /// Forwards unchanged-field validation without mutating project data in the view.
    /// </summary>
    /// <param name="field">The member to validate.</param>
    private void ValidateField(ContentPackEditorField field) =>
        FieldValidationRequested?.Invoke(field);
}

/// <summary>
/// Describes an editor action for the shared menu control.
/// </summary>
internal sealed class ContentEditorMenuCommand : IContextMenuCommand
{
    private readonly Action action;
    public string Text { get; }
    public bool Enabled { get; }
    public IReadOnlyList<ContentEditorMenuCommand> Children { get; }
    public string Shortcut { get; }
    public bool IsChecked { get; }

    /// <summary>
    /// Creates a leaf action or submenu.
    /// </summary>
    /// <param name="text">The caption.</param>
    /// <param name="action">The selected action.</param>
    /// <param name="enabled">Whether selection is allowed.</param>
    /// <param name="children">The submenu entries.</param>
    /// <param name="shortcut">The optional keyboard hint.</param>
    /// <param name="isChecked">Whether this choice is selected.</param>
    internal ContentEditorMenuCommand(
        string text,
        Action action = null,
        bool enabled = true,
        IReadOnlyList<ContentEditorMenuCommand> children = null,
        string shortcut = null,
        bool isChecked = false
    )
    {
        Text = text;
        this.action = action;
        Enabled = enabled;
        Children = children ?? Array.Empty<ContentEditorMenuCommand>();
        Shortcut = shortcut;
        IsChecked = isChecked;
    }

    /// <summary>
    /// Executes this menu action after selection.
    /// </summary>
    internal void Execute()
    {
        action?.Invoke();
    }
}
