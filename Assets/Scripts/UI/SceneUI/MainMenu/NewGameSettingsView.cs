using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.GameEvents;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GenerationEntry = CustomGameSettingsSession.GenerationEntry;

/// <summary>
/// Identifies a page of the custom-game configuration window.
/// </summary>
public enum NewGameSettingsCategory
{
    GameSetup,
    DifficultyModifiers,
    WorldGeneration,
    FactionModifiers,
    SaveLoad,
}

/// <summary>
/// Edits the complete configuration used to start a custom game.
/// </summary>
public sealed class NewGameSettingsView : MonoBehaviour, IContentInitializable
{
    private enum ModifierRecipient
    {
        Player,
        AI,
    }

    private const string _rowIdleAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_row_background";
    private const string _rowActiveAddress =
        "Application/OptionsMenu/UI/ui_settingsmenu_row_selected_background";
    private const int _controlTop = 1;
    private const float _labelFontSize = 11f;
    private const float _descriptionFontSize = 9f;
    private const float _minimumRowHeight = 30f;
    private const float _rowSpacing = 4f;
    private const float _descriptionTop = 17f;
    private const float _defaultLabelWidth = 150f;
    private const float _settingsScrollHeight = 310f;
    private const float _settingsScrollTop = 16f;
    private const float _sectionStride = 16f;
    private const float _sectionSpacing = 8f;
    private const float _tableHeaderHeight = 20f;
    private const float _tableSettingsScrollHeight = 290f;
    private const float _tableSettingsScrollTop = 36f;
    private static readonly string[] _tableCellPrefixes = { "Player", "AI", "World" };
    private static readonly Color _textColor = new Color(0.875f, 0.910f, 0.941f);
    private static readonly Color _modifiedTextColor = new Color(0.373f, 0.659f, 0.925f);

    [SerializeField]
    private Button[] tabs;

    [SerializeField]
    private TMP_Text title;

    [SerializeField]
    private TMP_Text playerHeading;

    [SerializeField]
    private TMP_Text aiHeading;

    [SerializeField]
    private RectTransform content;

    [SerializeField]
    private GameObject rowTemplate;

    [SerializeField]
    private ScrollAreaView scrollArea;

    [SerializeField]
    private Sprite rowIdleSprite;

    [SerializeField]
    private Sprite rowActiveSprite;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private Button defaultsButton;

    [SerializeField]
    private Button applyButton;

    private readonly List<GameObject> rows = new();
    private readonly List<GameObject> generationSectionRows = new();
    private readonly List<GameObject> generationTableHeaderRows = new();
    private readonly List<GameObject> generationTableRows = new();
    private readonly Dictionary<string, OptionsChoiceFieldView> setupChoiceFields = new();
    private readonly Dictionary<GameEventCategory, OptionsToggleRowView> eventToggleRows = new();
    private readonly List<OptionsSaveSlot> presetSlots = new();
    private readonly List<CustomGamePresetEntry> presetEntries = new();
    private IReadOnlyList<ContentPackDefinition> availablePacks =
        Array.Empty<ContentPackDefinition>();
    private CustomGameSettingsSession settingsSession;
    private OptionsMenuView optionsMenuPrefab;
    private CustomGamePresetManager presetManager;
    private OptionsSaveListView presetSaveList;
    private ConfirmationDialogView presetConfirmDialog;
    private GameObject presetPage;
    private IContentAssetSource contentAssets;
    private int selectedPresetSlot = -1;
    private Action pendingPresetConfirmAction;
    private NewGameSettingsCategory selectedCategory;
    private bool controlsBound;

    public event Action<ContentPack, GameSummary> StartRequested;
    public event Action<string> ContentPackRequested;
    public event Action CloseRequested;

    /// <summary>
    /// Wires the settings window controls.
    /// </summary>
    private void Awake() => BindControls();

    /// <summary>
    /// Wires the settings window controls once, regardless of prefab activation timing.
    /// </summary>
    private void BindControls()
    {
        if (controlsBound)
            return;

        EnsureCategoryTabs();
        ConfigureWindowHeader();
        ConfigurePersistentNavigation();
        ConfigureValueInputTemplates();
        for (int index = 0; index < tabs.Length; index++)
        {
            NewGameSettingsCategory category = (NewGameSettingsCategory)index;
            tabs[index].onClick.AddListener(() => SelectCategory(category));
        }

        closeButton.onClick.AddListener(RequestClose);
        defaultsButton.onClick.AddListener(RestoreDefaults);
        applyButton.onClick.AddListener(Apply);
        controlsBound = true;
    }

    /// <summary>
    /// Applies the shared numeric-input presentation once to the reusable row template.
    /// </summary>
    private void ConfigureValueInputTemplates()
    {
        foreach (string prefix in _tableCellPrefixes)
        {
            Transform inputTransform = rowTemplate.transform.Find(prefix + "Value");
            if (inputTransform == null)
                continue;

            AlignControlTop(rowTemplate.transform.Find(prefix + "ValueBadge"));
            AlignControlTop(inputTransform);
            AlignControlTop(rowTemplate.transform.Find(prefix + "Reset"));
            UILayout.ConfigureCenteredNumericInput(
                inputTransform.GetComponent<TMP_InputField>(),
                _textColor,
                10f,
                4,
                1
            );
        }
    }

    /// <summary>
    /// Adds category tabs introduced after the currently authored prefab was generated.
    /// </summary>
    private void EnsureCategoryTabs()
    {
        string[] captions =
        {
            "GAME SETUP",
            "DIFFICULTY",
            "WORLD SETTINGS",
            "FACTION MODIFIERS",
            "SAVE / LOAD",
        };
        while (tabs.Length < captions.Length)
        {
            Button tab = Instantiate(tabs[tabs.Length - 1], tabs[tabs.Length - 1].transform.parent);
            int index = tabs.Length;
            tab.name = "Tab" + index;
            RectTransform rect = (RectTransform)tab.transform;
            rect.anchoredPosition += new Vector2(0f, -32f);
            tabs = tabs.Concat(new[] { tab }).ToArray();
        }

        for (int index = 0; index < tabs.Length; index++)
        {
            tabs[index].name = "Tab" + index;
            TMP_Text label = tabs[index].GetComponentInChildren<TMP_Text>();
            label.text = captions[index];
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontStyle = FontStyles.Normal;
            RectTransform tabRect = (RectTransform)tabs[index].transform;
            RectInt sourceRect = UILayout.GetSourceRect(tabRect);
            int y = index == (int)NewGameSettingsCategory.SaveLoad ? 360 : 82 + index * 32;
            UILayout.SetSourceRect(tabRect, sourceRect.x, y, sourceRect.width, sourceRect.height);
        }
    }

    /// <summary>
    /// Applies the shared menu-window typography to the custom-game caption.
    /// </summary>
    private void ConfigureWindowHeader()
    {
        TMP_Text header = transform.Find("ContentRoot/HeaderTextField")?.GetComponent<TMP_Text>();
        if (header == null)
            return;

        header.text = "Customize";
    }

    /// <summary>
    /// Matches persistent navigation to the shared escape-menu footer treatment.
    /// </summary>
    private void ConfigurePersistentNavigation()
    {
        playerHeading.enableAutoSizing = false;
        playerHeading.fontSize = 9f;
        aiHeading.enableAutoSizing = false;
        aiHeading.fontSize = 9f;

        TMP_Text closeLabel = closeButton.GetComponentInChildren<TMP_Text>();
        closeLabel.color = new Color(0.573f, 0.635f, 0.706f);
        closeLabel.alignment = TextAlignmentOptions.MidlineLeft;
        closeLabel.fontStyle = FontStyles.Normal;

        TMP_Text applyLabel = applyButton.GetComponentInChildren<TMP_Text>();
        applyLabel.color = new Color(0.373f, 0.659f, 0.925f);
        applyLabel.fontStyle = FontStyles.Bold;
    }

    /// <summary>
    /// Resolves navigation artwork after development asset references have been stripped.
    /// </summary>
    /// <param name="contentAssets">The active installation's menu artwork.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        this.contentAssets = contentAssets;
        Vector4 border = new Vector4(7f, 7f, 7f, 7f);
        rowIdleSprite = ContentBindings.RequireSprite(contentAssets, _rowIdleAddress, border);
        rowActiveSprite = ContentBindings.RequireSprite(contentAssets, _rowActiveAddress, border);
        foreach (OptionsToggleRowView row in eventToggleRows.Values)
            row.InitializeContent(contentAssets);
        if (presetSaveList != null)
            ContentBindings.Apply(presetSaveList.gameObject, contentAssets);
        presetConfirmDialog?.InitializeContent(contentAssets);
    }

    /// <summary>
    /// Initializes editable modifiers from authored defaults and existing launch overrides.
    /// </summary>
    /// <param name="pack">The active content and scenario.</param>
    /// <param name="summary">The new-game launch state receiving applied overrides.</param>
    public void Initialize(ContentPack pack, GameSummary summary)
    {
        Initialize(pack, summary, null, null);
    }

    /// <summary>
    /// Initializes complete custom-game setup, preset persistence, and reusable Save/Load UI.
    /// </summary>
    /// <param name="pack">The active content and scenario.</param>
    /// <param name="summary">The current launch configuration.</param>
    /// <param name="optionsPrefab">The Options prefab supplying the shared Save/Load page.</param>
    /// <param name="presetManager">The custom-game preset persistence service.</param>
    public void Initialize(
        ContentPack pack,
        GameSummary summary,
        OptionsMenuView optionsPrefab,
        CustomGamePresetManager presetManager
    )
    {
        Initialize(pack, summary, optionsPrefab, presetManager, null);
    }

    /// <summary>
    /// Initializes complete custom-game setup with the installed content-pack choices.
    /// </summary>
    /// <param name="pack">The active content and scenario.</param>
    /// <param name="summary">The current launch configuration.</param>
    /// <param name="optionsPrefab">The Options prefab supplying the shared Save/Load page.</param>
    /// <param name="presetManager">The custom-game preset persistence service.</param>
    /// <param name="installedPacks">The content packs available for custom games.</param>
    public void Initialize(
        ContentPack pack,
        GameSummary summary,
        OptionsMenuView optionsPrefab,
        CustomGamePresetManager presetManager,
        IReadOnlyList<ContentPackDefinition> installedPacks
    )
    {
        if (pack == null)
            throw new ArgumentNullException(nameof(pack));
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));

        BindControls();

        settingsSession = new CustomGameSettingsSession(pack, summary);
        pendingPresetConfirmAction = null;
        presetConfirmDialog?.Hide();
        availablePacks =
            installedPacks?.Where(candidate => candidate != null).ToArray()
            ?? new[] { pack.Definition };
        if (availablePacks.Count == 0)
            availablePacks = new[] { pack.Definition };
        this.optionsMenuPrefab = optionsPrefab ?? this.optionsMenuPrefab;
        this.presetManager = presetManager ?? this.presetManager;
        EnsurePresetPage();
        applyButton.GetComponentInChildren<TMP_Text>().text = "START GAME";
        Transform header = transform.Find("ContentRoot/HeaderTextField");
        if (header != null)
            header.GetComponent<TMP_Text>().text = "Customize";
        RefreshPresetSlots(true);
        SelectCategory(
            this.optionsMenuPrefab == null
                ? NewGameSettingsCategory.DifficultyModifiers
                : NewGameSettingsCategory.GameSetup
        );
    }

    /// <summary>
    /// Selects and renders one new-game settings page.
    /// </summary>
    /// <param name="selected">The page to display.</param>
    public void SelectCategory(NewGameSettingsCategory selected)
    {
        selectedCategory = selected;
        title.text = GetCategoryTitle(selected);
        bool saveLoadSelected = selected == NewGameSettingsCategory.SaveLoad;
        GameObject settingsPage = scrollArea.transform.parent.gameObject;
        settingsPage.SetActive(!saveLoadSelected);
        presetPage?.SetActive(saveLoadSelected);
        defaultsButton.gameObject.SetActive(!saveLoadSelected);
        applyButton.gameObject.SetActive(true);
        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == (int)selected;
            tabs[i].GetComponent<Image>().sprite = active ? rowActiveSprite : rowIdleSprite;
            tabs[i].transform.Find("SurfaceMask/Surface").gameObject.SetActive(!active);
            tabs[i].GetComponentInChildren<TMP_Text>().color = active
                ? new Color(0.875f, 0.910f, 0.941f)
                : new Color(0.573f, 0.635f, 0.706f);
        }

        bool difficultySelected = selected == NewGameSettingsCategory.DifficultyModifiers;
        playerHeading.gameObject.SetActive(difficultySelected);
        aiHeading.gameObject.SetActive(difficultySelected);
        if (!saveLoadSelected)
            ConfigureSettingsScrollLayout(difficultySelected);
        HideSetupChoiceButtons();
        HideEventToggleRows();

        if (saveLoadSelected)
        {
            RefreshPresetSlots(false);
            return;
        }

        if (selected == NewGameSettingsCategory.GameSetup)
        {
            RenderGameSetup();
            return;
        }

        if (difficultySelected)
        {
            HideGenerationTableHeaderRowsAfter(0);
            HideGenerationTableRowsAfter(0);
            float contentOffset = 0f;
            int sectionIndex = 0;
            int rowIndex = 0;
            foreach (
                var sectionGroup in settingsSession.DifficultyEntries.GroupBy(entry =>
                    entry.Section
                )
            )
            {
                if (sectionIndex > 0)
                    contentOffset += _sectionSpacing - _rowSpacing;
                GameObject sectionRow = GetGenerationSectionRow(sectionIndex++);
                ConfigureGenerationSectionRow(sectionRow, sectionGroup.Key, contentOffset);
                contentOffset += _sectionStride;

                foreach (var entry in sectionGroup)
                {
                    GameObject row = GetRow(rowIndex++);
                    row.name = "Setting-" + entry.Key;
                    row.SetActive(true);
                    contentOffset += ConfigureTextRow(
                        row,
                        contentOffset,
                        entry.Label,
                        entry.Description,
                        _defaultLabelWidth
                    );
                    SetDifficultyControlsVisible(row.transform, true);
                    ConfigureValueControl(row.transform, entry, ModifierRecipient.Player);
                    ConfigureValueControl(row.transform, entry, ModifierRecipient.AI);
                }
            }

            HideRowsAfter(rowIndex);
            HideGenerationSectionRowsAfter(sectionIndex);
            scrollArea.SetContentHeight(
                Mathf.Max(0f, contentOffset - _rowSpacing),
                _minimumRowHeight,
                true
            );
        }
        else
        {
            GenerationEntry[] categoryEntries = settingsSession
                .GenerationEntries.Where(entry => entry.Category == selected)
                .ToArray();
            float contentOffset = 0f;
            int sectionIndex = 0;
            int scalarRowIndex = 0;
            int tableHeaderIndex = 0;
            int tableRowIndex = 0;
            foreach (
                IGrouping<string, GenerationEntry> sectionGroup in categoryEntries.GroupBy(entry =>
                    entry.Section
                )
            )
            {
                GenerationEntry[] sectionEntries = sectionGroup.ToArray();
                if (sectionIndex > 0)
                    contentOffset += _sectionSpacing - _rowSpacing;
                GameObject sectionRow = GetGenerationSectionRow(sectionIndex++);
                ConfigureGenerationSectionRow(sectionRow, sectionGroup.Key, contentOffset);
                contentOffset += _sectionStride;

                if (sectionEntries.Any(entry => entry.Column != null))
                {
                    string[] columns = sectionEntries
                        .Select(entry => entry.Column)
                        .Distinct()
                        .ToArray();
                    GameObject headerRow = GetGenerationTableHeaderRow(tableHeaderIndex++);
                    ConfigureGenerationTableHeaderRow(headerRow, columns, contentOffset);
                    contentOffset += _tableHeaderHeight;

                    foreach (
                        IGrouping<string, GenerationEntry> rowGroup in sectionEntries.GroupBy(
                            entry => entry.RowKey
                        )
                    )
                    {
                        GameObject tableRow = GetGenerationTableRow(tableRowIndex++);
                        contentOffset += ConfigureGenerationTableRow(
                            tableRow,
                            rowGroup.ToArray(),
                            columns,
                            contentOffset
                        );
                    }

                    continue;
                }

                foreach (GenerationEntry entry in sectionEntries)
                {
                    GameObject row = GetRow(scalarRowIndex++);
                    row.name = "Setting-" + entry.Key;
                    row.SetActive(true);
                    contentOffset += ConfigureTextRow(
                        row,
                        contentOffset,
                        entry.Label,
                        entry.Description,
                        _defaultLabelWidth
                    );
                    SetDifficultyControlsVisible(row.transform, false);
                    ConfigureGenerationValueControl(row.transform, entry);
                }
            }

            HideRowsAfter(scalarRowIndex);
            HideGenerationSectionRowsAfter(sectionIndex);
            HideGenerationTableHeaderRowsAfter(tableHeaderIndex);
            HideGenerationTableRowsAfter(tableRowIndex);
            scrollArea.SetContentHeight(
                Mathf.Max(0f, contentOffset - _rowSpacing),
                _minimumRowHeight,
                true
            );
        }

        scrollArea.VerticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// Uses the shared content inset while preserving space for difficulty column headings.
    /// </summary>
    /// <param name="includeColumnHeadings">Whether player and AI headings occupy the first row.</param>
    private void ConfigureSettingsScrollLayout(bool includeColumnHeadings)
    {
        float top = includeColumnHeadings ? _tableSettingsScrollTop : _settingsScrollTop;
        float height = includeColumnHeadings ? _tableSettingsScrollHeight : _settingsScrollHeight;
        UILayout.SetSourceRect(
            (RectTransform)scrollArea.transform,
            20,
            Mathf.RoundToInt(top),
            342,
            Mathf.RoundToInt(height)
        );
        scrollArea.SetLayout(
            Vector2.zero,
            new Vector2(326f, height),
            new Vector2(330f, 0f),
            new Vector2(12f, height)
        );
    }

    /// <summary>
    /// Restores all settings to the selected scenario's authored values.
    /// </summary>
    public void RestoreDefaults()
    {
        settingsSession.RestoreDefaults(selectedCategory);
        SelectCategory(selectedCategory);
    }

    /// <summary>
    /// Returns the heading displayed for one settings category.
    /// </summary>
    /// <param name="category">The selected category.</param>
    /// <returns>The uppercase category heading.</returns>
    private static string GetCategoryTitle(NewGameSettingsCategory category)
    {
        return category switch
        {
            NewGameSettingsCategory.GameSetup => "GAME SETUP",
            NewGameSettingsCategory.DifficultyModifiers => "DIFFICULTY MODIFIERS",
            NewGameSettingsCategory.WorldGeneration => "WORLD GENERATION",
            NewGameSettingsCategory.FactionModifiers => "FACTION MODIFIERS",
            NewGameSettingsCategory.SaveLoad => "SAVE / LOAD CUSTOM GAMES",
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null),
        };
    }

    /// <summary>
    /// Renders every launch-defining selection on one custom-game setup page.
    /// </summary>
    private void RenderGameSetup()
    {
        HideGenerationTableHeaderRowsAfter(0);
        HideGenerationTableRowsAfter(0);
        float contentOffset = 0f;
        int sectionIndex = 0;
        int rowIndex = 0;

        GameObject campaignSection = GetGenerationSectionRow(sectionIndex++);
        ConfigureGenerationSectionRow(campaignSection, "CAMPAIGN", contentOffset);
        contentOffset += _sectionStride;
        contentOffset += ConfigureSetupRow(
            rowIndex++,
            contentOffset,
            "content-pack",
            "Content Pack",
            "Selects the content used to configure and start the game.",
            string.IsNullOrWhiteSpace(settingsSession.Pack.Definition.DisplayName)
                ? settingsSession.Pack.Definition.ID
                : settingsSession.Pack.Definition.DisplayName,
            CycleContentPack
        );
        contentOffset += ConfigureSetupRow(
            rowIndex++,
            contentOffset,
            "faction",
            "Player Faction",
            "Selects the faction controlled by the player.",
            GetFactionDisplayName(settingsSession.Draft.PlayerFactionID),
            CycleFaction
        );
        contentOffset += ConfigureSetupRow(
            rowIndex++,
            contentOffset,
            "difficulty",
            "Base Difficulty",
            "Sets the starting values. You can change them afterward.",
            settingsSession.Draft.Difficulty.ToString(),
            CycleDifficulty
        );
        contentOffset += ConfigureSetupRow(
            rowIndex++,
            contentOffset,
            "galaxy-size",
            "Galaxy Size",
            "Selects the generated galaxy extent.",
            settingsSession.Draft.GalaxySize.ToString(),
            CycleGalaxySize
        );
        contentOffset += ConfigureSetupRow(
            rowIndex++,
            contentOffset,
            "victory-condition",
            "Victory Condition",
            "Selects the condition required to win the campaign.",
            settingsSession.Draft.VictoryCondition.ToString(),
            CycleVictoryCondition
        );
        contentOffset += ConfigureSeedRow(rowIndex++, contentOffset);

        if (settingsSession.EventCategories.Count > 0)
        {
            contentOffset += _sectionSpacing - _rowSpacing;
            GameObject eventsSection = GetGenerationSectionRow(sectionIndex++);
            ConfigureGenerationSectionRow(eventsSection, "EVENTS", contentOffset);
            contentOffset += _sectionStride;

            foreach (GameEventCategory category in settingsSession.EventCategories)
            {
                GameObject row = GetRow(rowIndex++);
                row.name = "Setup-event-" + category;
                row.SetActive(true);
                contentOffset += ConfigureTextRow(
                    row,
                    contentOffset,
                    GetEventCategoryLabel(category),
                    GetEventCategoryDescription(category),
                    _defaultLabelWidth
                );
                SetAllValueControlsVisible(row.transform, false);

                OptionsToggleRowView toggle = GetEventToggleRow(category, row.transform);
                toggle.gameObject.SetActive(true);
                bool enabled = settingsSession.IsEventCategoryEnabled(category);
                toggle.Render(enabled, string.Empty);
            }
        }

        HideRowsAfter(rowIndex);
        HideGenerationSectionRowsAfter(sectionIndex);
        scrollArea.SetContentHeight(
            Mathf.Max(0f, contentOffset - _rowSpacing),
            _minimumRowHeight,
            true
        );
        scrollArea.VerticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// Configures the editable deterministic seed and its new-seed button.
    /// </summary>
    /// <param name="index">The row index.</param>
    /// <param name="contentOffset">The vertical position within the scroll content.</param>
    /// <returns>The vertical advance through the row and following gap.</returns>
    private float ConfigureSeedRow(int index, float contentOffset)
    {
        GameObject row = GetRow(index);
        row.name = "Setup-seed";
        row.SetActive(true);
        float rowAdvance = ConfigureTextRow(
            row,
            contentOffset,
            "Seed",
            "Controls deterministic world generation. Enter an integer or generate a new one.",
            _defaultLabelWidth
        );
        SetDifficultyControlsVisible(row.transform, false);
        UILayout.SetSourceRect(
            (RectTransform)row.transform.Find("WorldValueBadge"),
            202,
            _controlTop,
            86,
            16
        );
        UILayout.SetSourceRect(
            (RectTransform)row.transform.Find("WorldValue"),
            202,
            _controlTop,
            86,
            16
        );
        UILayout.SetSourceRect(
            (RectTransform)row.transform.Find("WorldReset"),
            290,
            _controlTop,
            30,
            16
        );

        TMP_InputField input = row.transform.Find("WorldValue").GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.onEndEdit.RemoveAllListeners();
        input.SetTextWithoutNotify(
            settingsSession.Draft.Seed.ToString(CultureInfo.InvariantCulture)
        );
        input.textComponent.color = _textColor;
        input.onEndEdit.AddListener(EditSeed);

        Button randomize = row.transform.Find("WorldReset").GetComponent<Button>();
        randomize.onClick.RemoveAllListeners();
        randomize.onClick.AddListener(RandomizeSeed);
        return rowAdvance;
    }

    /// <summary>
    /// Configures one setup row and its themed cycling control.
    /// </summary>
    /// <param name="index">The row index.</param>
    /// <param name="contentOffset">The vertical position within the scroll content.</param>
    /// <param name="key">The stable choice identity.</param>
    /// <param name="label">The visible choice label.</param>
    /// <param name="description">The visible choice description.</param>
    /// <param name="value">The selected value.</param>
    /// <param name="step">Moves to the previous or next available value.</param>
    /// <returns>The vertical advance through the row and following gap.</returns>
    private float ConfigureSetupRow(
        int index,
        float contentOffset,
        string key,
        string label,
        string description,
        string value,
        Action<int> step
    )
    {
        GameObject row = GetRow(index);
        row.name = "Setup-" + key;
        row.SetActive(true);
        float rowAdvance = ConfigureTextRow(
            row,
            contentOffset,
            label,
            description,
            _defaultLabelWidth
        );
        SetAllValueControlsVisible(row.transform, false);

        if (!setupChoiceFields.TryGetValue(key, out OptionsChoiceFieldView choice))
        {
            if (optionsMenuPrefab == null)
                throw new MissingReferenceException(
                    "The shared Options selector is required for Game Setup."
                );

            const int controlLeft = 171;
            choice = optionsMenuPrefab.CloneContentPackChoice(
                contentAssets,
                row.transform,
                "Choice-" + key,
                controlLeft,
                _controlTop,
                Mathf.RoundToInt(scrollArea.ViewportWidth) - controlLeft
            );
            choice.StepRequested += delta => step(delta);
            setupChoiceFields.Add(key, choice);
        }

        choice.gameObject.SetActive(true);
        choice.Render(value.ToUpperInvariant());
        return rowAdvance;
    }

    /// <summary>
    /// Hides setup controls retained on reusable rows.
    /// </summary>
    private void HideSetupChoiceButtons()
    {
        foreach (OptionsChoiceFieldView choice in setupChoiceFields.Values)
            choice.gameObject.SetActive(false);
    }

    /// <summary>
    /// Returns or creates the themed toggle for one classified event category.
    /// </summary>
    /// <param name="category">The event category represented by the toggle.</param>
    /// <param name="parent">The reusable settings row hosting the toggle.</param>
    /// <returns>The configured toggle row.</returns>
    private OptionsToggleRowView GetEventToggleRow(GameEventCategory category, Transform parent)
    {
        const int controlLeft = 171;
        int width = Mathf.RoundToInt(scrollArea.ViewportWidth) - controlLeft;
        if (!eventToggleRows.TryGetValue(category, out OptionsToggleRowView toggle))
        {
            if (optionsMenuPrefab == null)
                throw new MissingReferenceException(
                    "The shared Options toggle is required for event settings."
                );

            toggle = optionsMenuPrefab.CloneToggleRow(
                parent,
                "EventToggle-" + category,
                (int)category,
                controlLeft,
                _controlTop,
                width
            );
            toggle.ToggleRequested += ToggleEventCategory;
            if (contentAssets != null)
                toggle.InitializeContent(contentAssets);
            eventToggleRows.Add(category, toggle);
        }
        else
        {
            toggle.transform.SetParent(parent, false);
            UILayout.SetSourceRect(
                (RectTransform)toggle.transform,
                controlLeft,
                _controlTop,
                width,
                14
            );
        }

        return toggle;
    }

    /// <summary>
    /// Hides event toggles retained on reusable rows while another page is visible.
    /// </summary>
    private void HideEventToggleRows()
    {
        foreach (OptionsToggleRowView toggle in eventToggleRows.Values)
            toggle.gameObject.SetActive(false);
    }

    /// <summary>
    /// Toggles one event category and preserves the gameplay page's scroll position.
    /// </summary>
    /// <param name="option">The event-category value emitted by the selected row.</param>
    private void ToggleEventCategory(int option)
    {
        GameEventCategory category = (GameEventCategory)option;
        settingsSession.SetEventCategoryEnabled(
            category,
            !settingsSession.IsEventCategoryEnabled(category)
        );
        float position = scrollArea.VerticalNormalizedPosition;
        SelectCategory(NewGameSettingsCategory.GameSetup);
        scrollArea.VerticalNormalizedPosition = position;
    }

    /// <summary>
    /// Returns the player-facing name of one event category.
    /// </summary>
    /// <param name="category">The event category.</param>
    /// <returns>The category label.</returns>
    private static string GetEventCategoryLabel(GameEventCategory category)
    {
        return category switch
        {
            GameEventCategory.Story => "Story Events",
            GameEventCategory.Disaster => "Disaster Events",
            _ => category + " Events",
        };
    }

    /// <summary>
    /// Returns the player-facing explanation of one event category.
    /// </summary>
    /// <param name="category">The event category.</param>
    /// <returns>The category description.</returns>
    private static string GetEventCategoryDescription(GameEventCategory category)
    {
        return category switch
        {
            GameEventCategory.Story => "Disables all story events in the game.",
            GameEventCategory.Disaster => "Disables all disaster events in the game.",
            _ => "Disables all " + category.ToString().ToLowerInvariant() + " events in the game.",
        };
    }

    /// <summary>
    /// Requests the next installed content pack so the owner can load its dependent data.
    /// </summary>
    /// <param name="delta">The requested step direction.</param>
    private void CycleContentPack(int delta)
    {
        if (availablePacks.Count < 2 || delta == 0)
            return;

        int current = -1;
        for (int index = 0; index < availablePacks.Count; index++)
        {
            if (
                string.Equals(
                    availablePacks[index].ID,
                    settingsSession.Pack.Definition.ID,
                    StringComparison.Ordinal
                )
            )
            {
                current = index;
                break;
            }
        }

        int selected = (current + Math.Sign(delta) + availablePacks.Count) % availablePacks.Count;
        ContentPackRequested?.Invoke(availablePacks[selected].ID);
    }

    /// <summary>
    /// Advances the selected player faction through the scenario's playable factions.
    /// </summary>
    /// <param name="delta">The requested step direction.</param>
    private void CycleFaction(int delta)
    {
        IReadOnlyList<string> factions = settingsSession.Pack.Scenario.PlayableFactionIDs;
        int current = factions.ToList().IndexOf(settingsSession.Draft.PlayerFactionID);
        int selected = (current + Math.Sign(delta) + factions.Count) % factions.Count;
        settingsSession.SetPlayerFaction(factions[selected]);
        SelectCategory(selectedCategory);
    }

    /// <summary>
    /// Advances the selected difficulty and updates unchanged AI modifier defaults.
    /// </summary>
    /// <param name="delta">The requested step direction.</param>
    private void CycleDifficulty(int delta)
    {
        settingsSession.SetDifficulty(CycleEnum(settingsSession.Draft.Difficulty, delta));
        SelectCategory(selectedCategory);
    }

    /// <summary>
    /// Advances the selected galaxy size and rebuilds applicable generation rows.
    /// </summary>
    /// <param name="delta">The requested step direction.</param>
    private void CycleGalaxySize(int delta)
    {
        settingsSession.SetGalaxySize(CycleEnum(settingsSession.Draft.GalaxySize, delta));
        SelectCategory(selectedCategory);
    }

    /// <summary>
    /// Advances the selected victory condition.
    /// </summary>
    /// <param name="delta">The requested step direction.</param>
    private void CycleVictoryCondition(int delta)
    {
        settingsSession.SetVictoryCondition(
            CycleEnum(settingsSession.Draft.VictoryCondition, delta)
        );
        SelectCategory(selectedCategory);
    }

    /// <summary>
    /// Returns the next declared value of an enum, wrapping at the end.
    /// </summary>
    /// <typeparam name="T">The enum type.</typeparam>
    /// <param name="current">The current enum value.</param>
    /// <param name="delta">The requested step direction.</param>
    /// <returns>The adjacent declared enum value.</returns>
    private static T CycleEnum<T>(T current, int delta)
        where T : struct, Enum
    {
        T[] values = (T[])Enum.GetValues(typeof(T));
        int index = Array.IndexOf(values, current);
        return values[(index + Math.Sign(delta) + values.Length) % values.Length];
    }

    /// <summary>
    /// Resolves one active faction's player-facing name.
    /// </summary>
    /// <param name="factionID">The faction instance ID.</param>
    /// <returns>The faction display name.</returns>
    private string GetFactionDisplayName(string factionID)
    {
        Faction faction = settingsSession.Pack.GameData.Factions.Single(candidate =>
            candidate.InstanceID == factionID
        );
        return faction.GetDisplayName();
    }

    /// <summary>
    /// Clones the established Options Save/Load page for custom-game presets.
    /// </summary>
    private void EnsurePresetPage()
    {
        if (presetPage != null || optionsMenuPrefab == null)
            return;

        OptionsSaveListView template =
            optionsMenuPrefab.GetComponentInChildren<OptionsSaveListView>(true);
        if (template == null)
            throw new MissingReferenceException("Options Save/Load page is missing.");

        Transform settingsPage = scrollArea.transform.parent;
        presetPage = Instantiate(template.gameObject, settingsPage.parent, false);
        presetPage.name = "CustomGamePresetPage";
        RectTransform settingsRect = (RectTransform)settingsPage;
        RectTransform presetRect = (RectTransform)presetPage.transform;
        presetRect.anchorMin = settingsRect.anchorMin;
        presetRect.anchorMax = settingsRect.anchorMax;
        presetRect.pivot = settingsRect.pivot;
        presetRect.anchoredPosition = settingsRect.anchoredPosition;
        presetRect.sizeDelta = settingsRect.sizeDelta;
        presetRect.localRotation = Quaternion.identity;
        presetRect.localScale = Vector3.one;
        presetSaveList = presetPage.GetComponent<OptionsSaveListView>();
        TMP_Text heading = presetPage
            .GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(text => text.name == "SavedGamesLabel");
        if (heading != null)
            heading.text = "CUSTOM GAME PRESETS";
        if (contentAssets != null)
            ContentBindings.Apply(presetPage, contentAssets);

        ConfirmationDialogView confirmTemplate =
            optionsMenuPrefab.GetComponentInChildren<ConfirmationDialogView>(true);
        if (confirmTemplate == null)
            throw new MissingReferenceException("Options confirmation dialog is missing.");
        presetConfirmDialog = Instantiate(confirmTemplate, settingsPage.parent, false);
        presetConfirmDialog.name = "CustomGamePresetConfirmDialog";
        if (contentAssets != null)
            presetConfirmDialog.InitializeContent(contentAssets);
        presetConfirmDialog.Confirmed += ConfirmPresetAction;
        presetConfirmDialog.Canceled += CancelPresetAction;
        presetConfirmDialog.gameObject.SetActive(false);

        presetSaveList.SaveRequested += HandlePresetSaveRequested;
        presetSaveList.LoadRequested += HandlePresetLoadRequested;
        presetSaveList.SlotSelected += HandlePresetSlotSelected;
        presetSaveList.SlotRenamed += HandlePresetSlotRenamed;
        presetSaveList.SlotDeleteRequested += HandlePresetDeleteRequested;
        presetPage.SetActive(false);
    }

    /// <summary>
    /// Rebuilds the visible preset list from configurations compatible with active content.
    /// </summary>
    /// <param name="resetScroll">Whether to restore the list to its top.</param>
    /// <param name="selectedFileName">The preset identity to select after refreshing, when supplied.</param>
    private void RefreshPresetSlots(bool resetScroll, string selectedFileName = null)
    {
        if (presetSaveList == null || presetManager == null || settingsSession == null)
            return;

        presetEntries.Clear();
        presetEntries.AddRange(
            presetManager
                .GetPresets()
                .Where(entry => settingsSession.Pack.MatchesContentIdentity(entry.Summary))
        );
        presetSlots.Clear();
        presetSlots.Add(new OptionsSaveSlot("CREATE NEW PRESET", string.Empty, null, true, null));
        foreach (CustomGamePresetEntry entry in presetEntries)
        {
            presetSlots.Add(
                new OptionsSaveSlot(
                    entry.DisplayName,
                    entry.LastSavedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
                    ResolveFactionIcon(entry.Summary.PlayerFactionID),
                    false,
                    entry.FileName,
                    entry.Summary.Difficulty.ToString()
                )
            );
        }

        if (!string.IsNullOrEmpty(selectedFileName))
            selectedPresetSlot = presetSlots.FindIndex(slot => slot.FileName == selectedFileName);
        else if (selectedPresetSlot >= presetSlots.Count)
            selectedPresetSlot = -1;
        presetSaveList.Render(presetSlots, selectedPresetSlot, true, resetScroll);
    }

    /// <summary>
    /// Resolves the saved-game icon used for a preset's selected faction.
    /// </summary>
    /// <param name="factionID">The selected faction instance ID.</param>
    /// <returns>The configured faction icon, or null when unavailable.</returns>
    private Texture2D ResolveFactionIcon(string factionID)
    {
        if (contentAssets == null || string.IsNullOrWhiteSpace(factionID))
            return null;

        string path = new FactionThemeLibrary(settingsSession.Pack.GameData.FactionThemes)
            .GetTheme(factionID)
            ?.SavedGameSlotIconImagePath;
        return string.IsNullOrWhiteSpace(path) ? null : contentAssets.GetTexture(path);
    }

    /// <summary>
    /// Selects an existing preset row.
    /// </summary>
    /// <param name="slot">The visible preset-row index.</param>
    private void HandlePresetSlotSelected(int slot)
    {
        selectedPresetSlot = slot > 0 && slot < presetSlots.Count ? slot : -1;
        RefreshPresetSlots(false);
    }

    /// <summary>
    /// Creates, renames, or overwrites a named custom-game preset.
    /// </summary>
    /// <param name="slot">The visible preset-row index.</param>
    /// <param name="newName">The supplied preset name.</param>
    /// <param name="submitted">Whether Return explicitly submitted the edit.</param>
    private void HandlePresetSlotRenamed(int slot, string newName, bool submitted)
    {
        if (presetManager == null || string.IsNullOrWhiteSpace(newName))
            return;

        if (slot == 0)
        {
            presetManager.CreatePreset(newName, settingsSession.CreateSummarySnapshot());
            selectedPresetSlot = -1;
        }
        else if (slot > 0 && slot <= presetEntries.Count)
        {
            CustomGamePresetEntry entry = presetEntries[slot - 1];
            if (submitted)
            {
                RequestPresetOverwrite(entry, newName);
                return;
            }
            else
                presetManager.RenamePreset(entry.FileName, newName);
        }

        RefreshPresetSlots(false);
    }

    /// <summary>
    /// Requests confirmation before replacing the selected preset with the current draft.
    /// </summary>
    private void HandlePresetSaveRequested()
    {
        if (
            presetManager == null
            || selectedPresetSlot <= 0
            || selectedPresetSlot > presetEntries.Count
        )
            return;

        CustomGamePresetEntry entry = presetEntries[selectedPresetSlot - 1];
        RequestPresetOverwrite(entry, entry.DisplayName);
    }

    /// <summary>
    /// Confirms replacement of one preset with the draft captured for that request.
    /// </summary>
    /// <param name="entry">The existing preset to replace.</param>
    /// <param name="displayName">The display name to persist after confirmation.</param>
    private void RequestPresetOverwrite(CustomGamePresetEntry entry, string displayName)
    {
        GameSummary snapshot = settingsSession.CreateSummarySnapshot();
        RequestPresetConfirm(
            $"Overwrite \"{entry.DisplayName}\"?",
            () =>
            {
                presetManager.SavePreset(entry.FileName, displayName, snapshot);
                RefreshPresetSlots(false, entry.FileName);
            }
        );
    }

    /// <summary>
    /// Replaces the custom-game draft with the selected compatible preset.
    /// </summary>
    private void HandlePresetLoadRequested()
    {
        if (
            presetManager == null
            || selectedPresetSlot <= 0
            || selectedPresetSlot > presetEntries.Count
        )
            return;

        GameSummary loaded = presetManager.LoadPreset(
            presetEntries[selectedPresetSlot - 1].FileName
        );
        if (!settingsSession.Pack.MatchesContentIdentity(loaded))
            throw new InvalidOperationException(
                "The selected custom-game preset requires different active content."
            );

        Initialize(settingsSession.Pack, loaded, optionsMenuPrefab, presetManager, availablePacks);
    }

    /// <summary>
    /// Deletes the selected preset row.
    /// </summary>
    /// <param name="slot">The visible preset-row index.</param>
    private void HandlePresetDeleteRequested(int slot)
    {
        if (presetManager == null || slot <= 0 || slot > presetEntries.Count)
            return;

        CustomGamePresetEntry entry = presetEntries[slot - 1];
        RequestPresetConfirm(
            $"Delete \"{entry.DisplayName}\"?",
            () =>
            {
                presetManager.DeletePreset(entry.FileName);
                selectedPresetSlot = -1;
                RefreshPresetSlots(false);
            }
        );
    }

    /// <summary>
    /// Shows the shared confirmation dialog for one captured preset operation.
    /// </summary>
    /// <param name="message">The confirmation prompt.</param>
    /// <param name="onConfirmed">The operation to run after acceptance.</param>
    private void RequestPresetConfirm(string message, Action onConfirmed)
    {
        pendingPresetConfirmAction = onConfirmed;
        presetConfirmDialog.Show(message);
    }

    /// <summary>
    /// Runs and clears the preset operation accepted by the confirmation dialog.
    /// </summary>
    private void ConfirmPresetAction()
    {
        Action confirmed = pendingPresetConfirmAction;
        pendingPresetConfirmAction = null;
        confirmed?.Invoke();
    }

    /// <summary>
    /// Clears a canceled preset operation without changing stored presets.
    /// </summary>
    private void CancelPresetAction()
    {
        pendingPresetConfirmAction = null;
    }

    /// <summary>
    /// Returns an existing row or creates another row from the shared template.
    /// </summary>
    /// <param name="index">The requested row index.</param>
    /// <returns>The reusable settings row.</returns>
    private GameObject GetRow(int index)
    {
        if (rows.Count <= index)
            rows.Add(Instantiate(rowTemplate, content));
        return rows[index];
    }

    /// <summary>
    /// Returns an existing world-generation section row or creates one from the shared template.
    /// </summary>
    /// <param name="index">The requested section-row index.</param>
    /// <returns>The reusable section row.</returns>
    private GameObject GetGenerationSectionRow(int index)
    {
        if (generationSectionRows.Count <= index)
            generationSectionRows.Add(
                Instantiate(rowTemplate.transform.Find("Label").gameObject, content)
            );
        return generationSectionRows[index];
    }

    /// <summary>
    /// Returns an existing generation-table header or creates one from the shared row template.
    /// </summary>
    /// <param name="index">The requested header index.</param>
    /// <returns>The reusable table header.</returns>
    private GameObject GetGenerationTableHeaderRow(int index)
    {
        if (generationTableHeaderRows.Count <= index)
            generationTableHeaderRows.Add(Instantiate(rowTemplate, content));
        return generationTableHeaderRows[index];
    }

    /// <summary>
    /// Returns an existing generation-table value row or creates one from the shared row template.
    /// </summary>
    /// <param name="index">The requested row index.</param>
    /// <returns>The reusable table value row.</returns>
    private GameObject GetGenerationTableRow(int index)
    {
        if (generationTableRows.Count <= index)
            generationTableRows.Add(Instantiate(rowTemplate, content));
        return generationTableRows[index];
    }

    /// <summary>
    /// Configures a compact heading within the world-generation settings list.
    /// </summary>
    /// <param name="row">The reusable row used as a heading.</param>
    /// <param name="heading">The heading text.</param>
    /// <param name="contentOffset">The vertical position within the scroll content.</param>
    private static void ConfigureGenerationSectionRow(
        GameObject row,
        string heading,
        float contentOffset
    )
    {
        row.name = "Section-" + heading.Replace(' ', '-');
        row.SetActive(true);
        TMP_Text label = row.GetComponent<TMP_Text>();
        UILayout.ConfigureMenuSectionHeading(label, heading, new Color(0.373f, 0.659f, 0.925f));
        UILayout.SetSourceRect(label.rectTransform, 0, Mathf.RoundToInt(contentOffset), 200, 16);
    }

    /// <summary>
    /// Configures the column headings for one world-generation table.
    /// </summary>
    /// <param name="row">The reusable header row.</param>
    /// <param name="columns">The ordered column labels.</param>
    /// <param name="contentOffset">The vertical position within the scroll content.</param>
    private static void ConfigureGenerationTableHeaderRow(
        GameObject row,
        IReadOnlyList<string> columns,
        float contentOffset
    )
    {
        row.name = "GenerationTableHeader";
        row.SetActive(true);
        RectTransform rowRect = (RectTransform)row.transform;
        rowRect.anchoredPosition = new Vector2(0f, -contentOffset);
        rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, _tableHeaderHeight);
        row.transform.Find("Label").gameObject.SetActive(false);
        row.transform.Find("Description").gameObject.SetActive(false);
        SetAllValueControlsVisible(row.transform, false);

        for (int index = 0; index < _tableCellPrefixes.Length; index++)
        {
            TMP_Text heading = GetOrCreateTableHeading(row.transform, index);
            bool visible = index < columns.Count;
            heading.gameObject.SetActive(visible);
            if (!visible)
                continue;

            heading.text = columns[index].ToUpperInvariant();
            heading.color = new Color(0.373f, 0.659f, 0.925f);
            heading.fontStyle = FontStyles.Bold;
            heading.enableAutoSizing = false;
            heading.fontSize = 9f;
            heading.alignment = TextAlignmentOptions.Midline;
            GetTableColumnGeometry(columns.Count, index, out float x, out float valueWidth, out _);
            UILayout.SetSourceRect(
                heading.rectTransform,
                Mathf.RoundToInt(x),
                0,
                Mathf.RoundToInt(valueWidth),
                Mathf.RoundToInt(_tableHeaderHeight)
            );
        }
    }

    /// <summary>
    /// Configures one row of a world-generation table and binds each cell to its setting.
    /// </summary>
    /// <param name="row">The reusable table row.</param>
    /// <param name="entries">The row's generation settings.</param>
    /// <param name="columns">The ordered table columns.</param>
    /// <param name="contentOffset">The vertical position within the scroll content.</param>
    /// <returns>The vertical advance through the row and following gap.</returns>
    private float ConfigureGenerationTableRow(
        GameObject row,
        IReadOnlyList<GenerationEntry> entries,
        IReadOnlyList<string> columns,
        float contentOffset
    )
    {
        GenerationEntry first = entries[0];
        row.name = "Setting-" + first.RowKey;
        row.SetActive(true);
        TMP_Text label = row.transform.Find("Label").GetComponent<TMP_Text>();
        TMP_Text description = row.transform.Find("Description").GetComponent<TMP_Text>();
        label.gameObject.SetActive(true);
        description.gameObject.SetActive(true);
        float labelWidth = columns.Count == 3 ? 116f : 150f;
        float rowAdvance = ConfigureTextRow(
            row,
            contentOffset,
            first.Label,
            first.Description,
            labelWidth
        );
        SetAllValueControlsVisible(row.transform, false);

        for (int columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            GenerationEntry entry = entries.FirstOrDefault(candidate =>
                string.Equals(candidate.Column, columns[columnIndex], StringComparison.Ordinal)
            );
            if (entry == null)
                continue;

            string prefix = _tableCellPrefixes[columnIndex];
            ConfigureGenerationTableCellGeometry(row.transform, prefix, columns.Count, columnIndex);
            row.transform.Find(prefix + "ValueBadge").gameObject.SetActive(true);
            row.transform.Find(prefix + "Value").gameObject.SetActive(true);
            row.transform.Find(prefix + "Reset").gameObject.SetActive(true);
            ConfigureGenerationValueControl(row.transform, entry, prefix);
        }

        return rowAdvance;
    }

    /// <summary>
    /// Returns or creates one dynamic column heading on a table-header row.
    /// </summary>
    /// <param name="row">The owning header row.</param>
    /// <param name="index">The zero-based column index.</param>
    /// <returns>The heading text component.</returns>
    private static TMP_Text GetOrCreateTableHeading(Transform row, int index)
    {
        string name = "Column" + index + "Heading";
        Transform existing = row.Find(name);
        if (existing != null)
            return existing.GetComponent<TMP_Text>();

        GameObject heading = UnityEngine.Object.Instantiate(row.Find("Label").gameObject, row);
        heading.name = name;
        return heading.GetComponent<TMP_Text>();
    }

    /// <summary>
    /// Places one numeric field and reset button within a generation-table column.
    /// </summary>
    /// <param name="row">The owning table row.</param>
    /// <param name="prefix">The authored control prefix.</param>
    /// <param name="columnCount">The number of visible table columns.</param>
    /// <param name="columnIndex">The zero-based column index.</param>
    private static void ConfigureGenerationTableCellGeometry(
        Transform row,
        string prefix,
        int columnCount,
        int columnIndex
    )
    {
        GetTableColumnGeometry(
            columnCount,
            columnIndex,
            out float x,
            out float valueWidth,
            out float resetWidth
        );
        UILayout.SetSourceRect(
            (RectTransform)row.Find(prefix + "ValueBadge"),
            Mathf.RoundToInt(x),
            _controlTop,
            Mathf.RoundToInt(valueWidth),
            16
        );
        UILayout.SetSourceRect(
            (RectTransform)row.Find(prefix + "Value"),
            Mathf.RoundToInt(x),
            _controlTop,
            Mathf.RoundToInt(valueWidth),
            16
        );
        UILayout.SetSourceRect(
            (RectTransform)row.Find(prefix + "Reset"),
            Mathf.RoundToInt(x + valueWidth + 2f),
            _controlTop,
            Mathf.RoundToInt(resetWidth),
            16
        );
    }

    /// <summary>
    /// Resolves the horizontal geometry for one table column.
    /// </summary>
    /// <param name="columnCount">The number of visible columns.</param>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <param name="x">Receives the left coordinate.</param>
    /// <param name="valueWidth">Receives the input width.</param>
    /// <param name="resetWidth">Receives the reset-button width.</param>
    private static void GetTableColumnGeometry(
        int columnCount,
        int columnIndex,
        out float x,
        out float valueWidth,
        out float resetWidth
    )
    {
        if (columnCount == 3)
        {
            x = 120f + columnIndex * 69f;
            valueWidth = 39f;
            resetWidth = 26f;
            return;
        }

        x = columnCount == 1 ? 241f : 154f + columnIndex * 87f;
        valueWidth = 47f;
        resetWidth = 30f;
    }

    /// <summary>
    /// Sizes and positions one text-bearing settings row around its rendered description.
    /// </summary>
    /// <param name="row">The row to position.</param>
    /// <param name="contentOffset">The vertical position within the scroll content.</param>
    /// <param name="labelText">The row label.</param>
    /// <param name="descriptionText">The wrapped description.</param>
    /// <param name="labelWidth">The width available to label and description text.</param>
    /// <returns>The vertical advance through the row and following gap.</returns>
    private static float ConfigureTextRow(
        GameObject row,
        float contentOffset,
        string labelText,
        string descriptionText,
        float labelWidth
    )
    {
        RectTransform rowRect = (RectTransform)row.transform;
        TMP_Text label = row.transform.Find("Label").GetComponent<TMP_Text>();
        TMP_Text description = row.transform.Find("Description").GetComponent<TMP_Text>();
        label.text = labelText;
        label.enableAutoSizing = false;
        label.fontSize = _labelFontSize;
        description.text = descriptionText;
        description.fontSize = _descriptionFontSize;
        description.gameObject.SetActive(true);
        UILayout.SetSourceRect(
            label.rectTransform,
            0,
            _controlTop,
            Mathf.RoundToInt(labelWidth),
            16
        );
        float descriptionHeight = Mathf.Ceil(
            description.GetPreferredValues(descriptionText, labelWidth, 0f).y
        );
        UILayout.SetSourceRect(
            description.rectTransform,
            0,
            Mathf.RoundToInt(_descriptionTop),
            Mathf.RoundToInt(labelWidth),
            Mathf.RoundToInt(descriptionHeight)
        );
        float rowHeight = Mathf.Max(_minimumRowHeight, _descriptionTop + descriptionHeight);
        rowRect.anchoredPosition = new Vector2(0f, -contentOffset);
        rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, rowHeight);
        return rowHeight + _rowSpacing;
    }

    /// <summary>
    /// Aligns one authored row control with the top setting label.
    /// </summary>
    /// <param name="control">The control to align.</param>
    private static void AlignControlTop(Transform control)
    {
        RectTransform rect = (RectTransform)control;
        RectInt sourceRect = UILayout.GetSourceRect(rect);
        UILayout.SetSourceRect(
            rect,
            sourceRect.x,
            _controlTop,
            sourceRect.width,
            sourceRect.height
        );
    }

    /// <summary>
    /// Hides reusable rows beyond the active category's entry count.
    /// </summary>
    /// <param name="activeCount">The number of active rows.</param>
    private void HideRowsAfter(int activeCount)
    {
        for (int index = activeCount; index < rows.Count; index++)
            rows[index].SetActive(false);
    }

    /// <summary>
    /// Hides reusable world-generation section rows beyond the active section count.
    /// </summary>
    /// <param name="activeCount">The number of active section rows.</param>
    private void HideGenerationSectionRowsAfter(int activeCount)
    {
        for (int index = activeCount; index < generationSectionRows.Count; index++)
            generationSectionRows[index].SetActive(false);
    }

    /// <summary>
    /// Hides reusable table headers beyond the active header count.
    /// </summary>
    /// <param name="activeCount">The number of active table headers.</param>
    private void HideGenerationTableHeaderRowsAfter(int activeCount)
    {
        for (int index = activeCount; index < generationTableHeaderRows.Count; index++)
            generationTableHeaderRows[index].SetActive(false);
    }

    /// <summary>
    /// Hides reusable table rows beyond the active row count.
    /// </summary>
    /// <param name="activeCount">The number of active table rows.</param>
    private void HideGenerationTableRowsAfter(int activeCount)
    {
        for (int index = activeCount; index < generationTableRows.Count; index++)
            generationTableRows[index].SetActive(false);
    }

    /// <summary>
    /// Shows or hides every numeric control in a reusable settings row.
    /// </summary>
    /// <param name="row">The row containing the controls.</param>
    /// <param name="visible">Whether the controls should be visible.</param>
    private static void SetAllValueControlsVisible(Transform row, bool visible)
    {
        foreach (string prefix in new[] { "Player", "AI", "World" })
        {
            row.Find(prefix + "ValueBadge").gameObject.SetActive(visible);
            row.Find(prefix + "Value").gameObject.SetActive(visible);
            row.Find(prefix + "Reset").gameObject.SetActive(visible);
        }
    }

    /// <summary>
    /// Switches one row between the two-column difficulty controls and one world value.
    /// </summary>
    /// <param name="row">The row containing both control layouts.</param>
    /// <param name="visible">Whether the difficulty controls should be visible.</param>
    private static void SetDifficultyControlsVisible(Transform row, bool visible)
    {
        row.Find("PlayerValueBadge").gameObject.SetActive(visible);
        row.Find("PlayerValue").gameObject.SetActive(visible);
        row.Find("PlayerReset").gameObject.SetActive(visible);
        row.Find("AIValueBadge").gameObject.SetActive(visible);
        row.Find("AIValue").gameObject.SetActive(visible);
        row.Find("AIReset").gameObject.SetActive(visible);
        row.Find("WorldValueBadge").gameObject.SetActive(!visible);
        row.Find("WorldValue").gameObject.SetActive(!visible);
        row.Find("WorldReset").gameObject.SetActive(!visible);
    }

    /// <summary>
    /// Wires one recipient's value and reset controls for a modifier row.
    /// </summary>
    /// <param name="row">The modifier row.</param>
    /// <param name="entry">The modifier metadata.</param>
    /// <param name="recipient">The column being wired.</param>
    private void ConfigureValueControl(
        Transform row,
        (
            string Key,
            string Section,
            string Label,
            string Description,
            bool AllowsFraction,
            double Minimum,
            bool MinimumExclusive,
            double? Maximum,
            double PlayerDefault,
            double AIDefault
        ) entry,
        ModifierRecipient recipient
    )
    {
        bool isPlayer = recipient == ModifierRecipient.Player;
        string prefix = isPlayer ? "Player" : "AI";
        TMP_InputField input = row.Find(prefix + "Value").GetComponent<TMP_InputField>();
        input.contentType = entry.AllowsFraction
            ? TMP_InputField.ContentType.DecimalNumber
            : TMP_InputField.ContentType.IntegerNumber;
        input.onEndEdit.RemoveAllListeners();
        input.SetTextWithoutNotify(
            Format(settingsSession.GetDifficultyValue(entry.Key, !isPlayer))
        );
        input.textComponent.color = settingsSession.IsDifficultyValueDefault(entry.Key, !isPlayer)
            ? _textColor
            : _modifiedTextColor;
        input.onEndEdit.AddListener(value => EditValue(recipient, entry.Key, value));

        Button reset = row.Find(prefix + "Reset").GetComponent<Button>();
        reset.onClick.RemoveAllListeners();
        reset.onClick.AddListener(() => ResetValue(recipient, entry.Key));
    }

    /// <summary>
    /// Wires the value and reset controls for one world-generation setting.
    /// </summary>
    /// <param name="row">The setting row.</param>
    /// <param name="entry">The generation-setting metadata.</param>
    private void ConfigureGenerationValueControl(Transform row, GenerationEntry entry)
    {
        ConfigureGenerationValueControl(row, entry, "World");
    }

    /// <summary>
    /// Wires one prefixed value and reset control for a world-generation setting.
    /// </summary>
    /// <param name="row">The setting row.</param>
    /// <param name="entry">The generation-setting metadata.</param>
    /// <param name="prefix">The authored control-name prefix.</param>
    private void ConfigureGenerationValueControl(
        Transform row,
        GenerationEntry entry,
        string prefix
    )
    {
        int value = settingsSession.GetGenerationValue(entry);
        TMP_InputField input = row.Find(prefix + "Value").GetComponent<TMP_InputField>();
        input.contentType = TMP_InputField.ContentType.IntegerNumber;
        input.onEndEdit.RemoveAllListeners();
        input.SetTextWithoutNotify(value.ToString(CultureInfo.InvariantCulture));
        input.textComponent.color = settingsSession.IsGenerationValueDefault(entry)
            ? _textColor
            : _modifiedTextColor;
        input.onEndEdit.AddListener(text => EditGenerationValue(entry.Key, text));

        Button reset = row.Find(prefix + "Reset").GetComponent<Button>();
        reset.onClick.RemoveAllListeners();
        reset.onClick.AddListener(() => ResetGenerationValue(entry.Key));
    }

    /// <summary>
    /// Updates one local draft value when the input satisfies the modifier's domain.
    /// </summary>
    /// <param name="recipient">The column being edited.</param>
    /// <param name="key">The modifier identity.</param>
    /// <param name="text">The edited text.</param>
    private void EditValue(ModifierRecipient recipient, string key, string text)
    {
        settingsSession.TrySetDifficultyValue(key, recipient == ModifierRecipient.AI, text);

        float position = scrollArea.VerticalNormalizedPosition;
        SelectCategory(NewGameSettingsCategory.DifficultyModifiers);
        scrollArea.VerticalNormalizedPosition = position;
    }

    /// <summary>
    /// Updates one generation value when it satisfies its authored domain and relationships.
    /// </summary>
    /// <param name="key">The setting identity.</param>
    /// <param name="text">The edited integer text.</param>
    private void EditGenerationValue(string key, string text)
    {
        GenerationEntry entry = settingsSession.GenerationEntries.Single(candidate =>
            candidate.Key == key
        );
        settingsSession.TrySetGenerationValue(entry, text);

        float position = scrollArea.VerticalNormalizedPosition;
        SelectCategory(selectedCategory);
        scrollArea.VerticalNormalizedPosition = position;
    }

    /// <summary>
    /// Updates the game seed when the entered text is a signed integer.
    /// </summary>
    /// <param name="text">The entered seed.</param>
    private void EditSeed(string text)
    {
        settingsSession.TrySetSeed(text);
        SelectCategory(NewGameSettingsCategory.GameSetup);
    }

    /// <summary>
    /// Selects a newly generated game seed and refreshes the setup page.
    /// </summary>
    private void RandomizeSeed()
    {
        settingsSession.RandomizeSeed();
        SelectCategory(NewGameSettingsCategory.GameSetup);
    }

    /// <summary>
    /// Restores one recipient's modifier to its authored default.
    /// </summary>
    /// <param name="recipient">The column being restored.</param>
    /// <param name="key">The modifier identity.</param>
    private void ResetValue(ModifierRecipient recipient, string key)
    {
        settingsSession.ResetDifficultyValue(key, recipient == ModifierRecipient.AI);
        float position = scrollArea.VerticalNormalizedPosition;
        SelectCategory(NewGameSettingsCategory.DifficultyModifiers);
        scrollArea.VerticalNormalizedPosition = position;
    }

    /// <summary>
    /// Restores one generation value from the selected scenario configuration.
    /// </summary>
    /// <param name="key">The setting identity.</param>
    private void ResetGenerationValue(string key)
    {
        GenerationEntry entry = settingsSession.GenerationEntries.Single(candidate =>
            candidate.Key == key
        );
        settingsSession.ResetGenerationValue(entry);
        float position = scrollArea.VerticalNormalizedPosition;
        SelectCategory(selectedCategory);
        scrollArea.VerticalNormalizedPosition = position;
    }

    /// <summary>
    /// Publishes both modifier columns and treats the applied values as the current state.
    /// </summary>
    private void Apply()
    {
        StartRequested?.Invoke(settingsSession.Pack, settingsSession.CreateSummarySnapshot());

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem?.currentSelectedGameObject == applyButton.gameObject)
            eventSystem.SetSelectedGameObject(null);
    }

    /// <summary>
    /// Formats a modifier without adding a unit suffix.
    /// </summary>
    /// <param name="value">The value to display.</param>
    /// <returns>The invariant numeric text.</returns>
    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Requests closure through the scene's existing modal owner.
    /// </summary>
    private void RequestClose()
    {
        CancelPresetAction();
        presetConfirmDialog?.Hide();
        CloseRequested?.Invoke();
    }
}
