using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Rebellion.Game;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Identifies a page of the new-campaign configuration window.
/// </summary>
public enum NewGameSettingsCategory
{
    DifficultyModifiers,
}

/// <summary>
/// Edits the player and AI difficulty modifiers for a new campaign.
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
    private const float _rowStride = 28f;

    [SerializeField]
    private Button[] tabs;

    [SerializeField]
    private TMP_Text title;

    [SerializeField]
    private TMP_Text subtitle;

    [SerializeField]
    private TMP_Text scope;

    [SerializeField]
    private TMP_Text changedCount;

    [SerializeField]
    private RectTransform content;

    [SerializeField]
    private GameObject rowTemplate;

    [SerializeField]
    private RectTransform tooltipRoot;

    [SerializeField]
    private TMP_Text tooltipText;

    [SerializeField]
    private ScrollRect scroll;

    [SerializeField]
    private ScrollAreaView scrollArea;

    [SerializeField]
    private Sprite rowIdleSprite;

    [SerializeField]
    private Sprite rowActiveSprite;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private Button defaultsButton;

    [SerializeField]
    private Button applyButton;

    private readonly List<(
        string Key,
        string Label,
        string Description,
        bool AllowsFraction,
        bool AllowsNegative,
        bool RequiresPositive,
        double PlayerDefault,
        double AIDefault
    )> entries = new();
    private readonly Dictionary<string, double> playerDraft = new();
    private readonly Dictionary<string, double> aiDraft = new();
    private readonly Dictionary<string, double> playerOriginal = new();
    private readonly Dictionary<string, double> aiOriginal = new();
    private readonly List<GameObject> rows = new();
    private Coroutine tooltipDelay;
    private bool controlsBound;

    public event Action<DifficultyModifiers, DifficultyModifiers> ApplyRequested;
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

        foreach (Button tab in tabs)
            tab.onClick.AddListener(() =>
                SelectCategory(NewGameSettingsCategory.DifficultyModifiers)
            );

        cancelButton.onClick.AddListener(RequestClose);
        closeButton.onClick.AddListener(RequestClose);
        defaultsButton.onClick.AddListener(RestoreDefaults);
        applyButton.onClick.AddListener(Apply);
        scroll.onValueChanged.AddListener(_ => HideTooltip());
        applyButton.interactable = false;
        controlsBound = true;
    }

    /// <summary>
    /// Resolves navigation artwork after development asset references have been stripped.
    /// </summary>
    /// <param name="contentAssets">The active installation's menu artwork.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        Vector4 border = new Vector4(7f, 7f, 7f, 7f);
        rowIdleSprite = ContentBindings.RequireSprite(contentAssets, _rowIdleAddress, border);
        rowActiveSprite = ContentBindings.RequireSprite(contentAssets, _rowActiveAddress, border);
    }

    /// <summary>
    /// Initializes editable modifiers from authored defaults and existing launch overrides.
    /// </summary>
    /// <param name="pack">The active content and scenario.</param>
    /// <param name="summary">The new-game launch state receiving applied overrides.</param>
    public void Initialize(ContentPack pack, GameSummary summary)
    {
        if (pack == null)
            throw new ArgumentNullException(nameof(pack));
        if (summary == null)
            throw new ArgumentNullException(nameof(summary));

        BindControls();

        DifficultyModifiers playerDefaults = new DifficultyModifiers();
        DifficultyModifiers aiDefaults = ResolveAIDefaults(pack, summary.Difficulty);
        DifficultyModifiers playerValues = summary.PlayerDifficultyModifiers ?? playerDefaults;
        DifficultyModifiers aiValues = summary.AIDifficultyModifiers ?? aiDefaults;

        entries.Clear();
        playerDraft.Clear();
        aiDraft.Clear();
        playerOriginal.Clear();
        aiOriginal.Clear();

        Add(
            "mine-output",
            "Mine Output Modifier",
            "Scales how quickly each mine produces raw material.",
            false,
            false,
            false,
            playerDefaults.MineOutputPercent,
            aiDefaults.MineOutputPercent,
            playerValues.MineOutputPercent,
            aiValues.MineOutputPercent
        );
        Add(
            "refinery-output",
            "Refinery Output Modifier",
            "Scales how quickly each refinery converts raw material into refined material.",
            false,
            false,
            false,
            playerDefaults.RefineryOutputPercent,
            aiDefaults.RefineryOutputPercent,
            playerValues.RefineryOutputPercent,
            aiValues.RefineryOutputPercent
        );
        Add(
            "maintenance",
            "Maintenance Modifier",
            "Scales the maintenance points supplied by each working mine and refinery pair.",
            false,
            false,
            false,
            playerDefaults.MaintenanceCapacityPercent,
            aiDefaults.MaintenanceCapacityPercent,
            playerValues.MaintenanceCapacityPercent,
            aiValues.MaintenanceCapacityPercent
        );
        Add(
            "manufacturing-speed",
            "Manufacturing Speed Modifier",
            "Scales progress made by shipyards, training facilities, and construction facilities.",
            false,
            false,
            false,
            playerDefaults.ManufacturingSpeedPercent,
            aiDefaults.ManufacturingSpeedPercent,
            playerValues.ManufacturingSpeedPercent,
            aiValues.ManufacturingSpeedPercent
        );
        Add(
            "mission-success",
            "Mission Success Bonus",
            "Adds percentage points to mission success chances.",
            false,
            true,
            false,
            playerDefaults.MissionSuccessChancePoints,
            aiDefaults.MissionSuccessChancePoints,
            playerValues.MissionSuccessChancePoints,
            aiValues.MissionSuccessChancePoints
        );
        Add(
            "mission-foil",
            "Mission Foil Bonus",
            "Adds percentage points to mission foil chances.",
            false,
            true,
            false,
            playerDefaults.MissionFoilChancePoints,
            aiDefaults.MissionFoilChancePoints,
            playerValues.MissionFoilChancePoints,
            aiValues.MissionFoilChancePoints
        );
        Add(
            "mission-speed",
            "Mission Speed Bonus",
            "Increases mission execution speed by the entered percentage.",
            false,
            false,
            false,
            playerDefaults.MissionExecutionSpeedIncreasePercent,
            aiDefaults.MissionExecutionSpeedIncreasePercent,
            playerValues.MissionExecutionSpeedIncreasePercent,
            aiValues.MissionExecutionSpeedIncreasePercent
        );
        Add(
            "detection-rating",
            "Detection Rating Bonus",
            "Multiplies detection ratings. A value of 1 is unchanged; 1.25 is twenty-five percent higher.",
            true,
            false,
            true,
            playerDefaults.DetectionRatingMultiplier,
            aiDefaults.DetectionRatingMultiplier,
            playerValues.DetectionRatingMultiplier,
            aiValues.DetectionRatingMultiplier
        );

        subtitle.text =
            $"{pack.Definition.DisplayName ?? pack.Definition.ID}  /  {summary.Difficulty}";
        SelectCategory(NewGameSettingsCategory.DifficultyModifiers);
    }

    /// <summary>
    /// Selects the difficulty-modifier page.
    /// </summary>
    /// <param name="selected">The page to display.</param>
    public void SelectCategory(NewGameSettingsCategory selected)
    {
        HideTooltip();
        title.text = "DIFFICULTY MODIFIERS";
        scope.text = "PLAYER AND AI FACTIONS";
        for (int i = 0; i < tabs.Length; i++)
        {
            bool active = i == (int)selected;
            tabs[i].GetComponent<Image>().sprite = active ? rowActiveSprite : rowIdleSprite;
            tabs[i].transform.Find("SurfaceMask/Surface").gameObject.SetActive(!active);
            tabs[i].GetComponentInChildren<TMP_Text>().color = active
                ? new Color(0.875f, 0.910f, 0.941f)
                : new Color(0.573f, 0.635f, 0.706f);
        }

        for (int index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (rows.Count <= index)
                rows.Add(Instantiate(rowTemplate, content));

            GameObject row = rows[index];
            row.name = "Setting-" + entry.Key;
            row.SetActive(true);
            ((RectTransform)row.transform).anchoredPosition = new Vector2(0f, -index * _rowStride);
            row.transform.Find("Label").GetComponent<TMP_Text>().text = entry.Label;
            ConfigureTooltip(row.transform.Find("Info") as RectTransform, entry.Description);
            ConfigureValueControl(row.transform, entry, ModifierRecipient.Player);
            ConfigureValueControl(row.transform, entry, ModifierRecipient.AI);
        }

        for (int i = entries.Count; i < rows.Count; i++)
            rows[i].SetActive(false);

        scrollArea.SetContentHeight(entries.Count * _rowStride, _rowStride, true);
        RefreshChangedState();
    }

    /// <summary>
    /// Restores both columns to the neutral player and selected-difficulty AI defaults.
    /// </summary>
    public void RestoreDefaults()
    {
        foreach (var entry in entries)
        {
            playerDraft[entry.Key] = entry.PlayerDefault;
            aiDraft[entry.Key] = entry.AIDefault;
        }

        SelectCategory(NewGameSettingsCategory.DifficultyModifiers);
    }

    /// <summary>
    /// Resolves the authored AI defaults for the selected difficulty.
    /// </summary>
    /// <param name="pack">The active content pack.</param>
    /// <param name="difficulty">The selected new-game difficulty.</param>
    /// <returns>The configured modifiers, or neutral modifiers when the difficulty is absent.</returns>
    private static DifficultyModifiers ResolveAIDefaults(
        ContentPack pack,
        GameDifficulty difficulty
    )
    {
        if (
            pack.GameData.GameConfig.DifficultyModifiers.TryGetValue(
                difficulty,
                out DifficultyModifiers modifiers
            )
        )
            return modifiers;

        return new DifficultyModifiers();
    }

    /// <summary>
    /// Adds one modifier and its defaults and initial values to the editable draft.
    /// </summary>
    /// <param name="key">The modifier identity.</param>
    /// <param name="label">The visible modifier name.</param>
    /// <param name="description">The help text.</param>
    /// <param name="allowsFraction">Whether fractional input is accepted.</param>
    /// <param name="allowsNegative">Whether negative input is accepted.</param>
    /// <param name="requiresPositive">Whether zero is rejected.</param>
    /// <param name="playerDefault">The authored player default.</param>
    /// <param name="aiDefault">The authored AI default.</param>
    /// <param name="playerValue">The initial player value.</param>
    /// <param name="aiValue">The initial AI value.</param>
    private void Add(
        string key,
        string label,
        string description,
        bool allowsFraction,
        bool allowsNegative,
        bool requiresPositive,
        double playerDefault,
        double aiDefault,
        double playerValue,
        double aiValue
    )
    {
        entries.Add(
            (
                key,
                label,
                description,
                allowsFraction,
                allowsNegative,
                requiresPositive,
                playerDefault,
                aiDefault
            )
        );
        playerDraft.Add(key, playerValue);
        aiDraft.Add(key, aiValue);
        playerOriginal.Add(key, playerValue);
        aiOriginal.Add(key, aiValue);
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
            string Label,
            string Description,
            bool AllowsFraction,
            bool AllowsNegative,
            bool RequiresPositive,
            double PlayerDefault,
            double AIDefault
        ) entry,
        ModifierRecipient recipient
    )
    {
        bool isPlayer = recipient == ModifierRecipient.Player;
        Dictionary<string, double> draft = isPlayer ? playerDraft : aiDraft;
        double defaultValue = isPlayer ? entry.PlayerDefault : entry.AIDefault;
        string prefix = isPlayer ? "Player" : "AI";
        TMP_InputField input = row.Find(prefix + "Value").GetComponent<TMP_InputField>();
        input.contentType = entry.AllowsFraction
            ? TMP_InputField.ContentType.DecimalNumber
            : TMP_InputField.ContentType.IntegerNumber;
        input.onEndEdit.RemoveAllListeners();
        input.SetTextWithoutNotify(Format(draft[entry.Key]));
        input.textComponent.color =
            draft[entry.Key] == defaultValue ? Color.white : new Color(1f, 0.79f, 0.38f);
        input.onEndEdit.AddListener(value => EditValue(recipient, entry.Key, value));

        Button reset = row.Find(prefix + "Reset").GetComponent<Button>();
        reset.onClick.RemoveAllListeners();
        reset.onClick.AddListener(() => ResetValue(recipient, entry.Key));
    }

    /// <summary>
    /// Updates one local draft value when the input satisfies the modifier's domain.
    /// </summary>
    /// <param name="recipient">The column being edited.</param>
    /// <param name="key">The modifier identity.</param>
    /// <param name="text">The edited text.</param>
    private void EditValue(ModifierRecipient recipient, string key, string text)
    {
        var entry = entries.Find(candidate => candidate.Key == key);
        if (
            double.TryParse(
                text,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value
            )
            && double.IsFinite(value)
            && (entry.AllowsFraction || value == Math.Truncate(value))
            && (entry.AllowsNegative || value >= 0)
            && (!entry.RequiresPositive || value > 0)
            && (entry.AllowsFraction || value is >= int.MinValue and <= int.MaxValue)
        )
        {
            Dictionary<string, double> draft =
                recipient == ModifierRecipient.Player ? playerDraft : aiDraft;
            draft[key] = value;
        }

        float position = scroll.verticalNormalizedPosition;
        SelectCategory(NewGameSettingsCategory.DifficultyModifiers);
        scroll.verticalNormalizedPosition = position;
    }

    /// <summary>
    /// Restores one recipient's modifier to its authored default.
    /// </summary>
    /// <param name="recipient">The column being restored.</param>
    /// <param name="key">The modifier identity.</param>
    private void ResetValue(ModifierRecipient recipient, string key)
    {
        var entry = entries.Find(candidate => candidate.Key == key);
        Dictionary<string, double> draft =
            recipient == ModifierRecipient.Player ? playerDraft : aiDraft;
        draft[key] = recipient == ModifierRecipient.Player ? entry.PlayerDefault : entry.AIDefault;
        float position = scroll.verticalNormalizedPosition;
        SelectCategory(NewGameSettingsCategory.DifficultyModifiers);
        scroll.verticalNormalizedPosition = position;
    }

    /// <summary>
    /// Publishes both modifier columns and treats the applied values as the current state.
    /// </summary>
    private void Apply()
    {
        DifficultyModifiers playerModifiers = CreateModifiers(playerDraft);
        DifficultyModifiers aiModifiers = CreateModifiers(aiDraft);
        ApplyRequested?.Invoke(playerModifiers, aiModifiers);

        CopyValues(playerDraft, playerOriginal);
        CopyValues(aiDraft, aiOriginal);
        RefreshChangedState();
    }

    /// <summary>
    /// Creates a modifier object from one column's draft values.
    /// </summary>
    /// <param name="values">The values keyed by modifier identity.</param>
    /// <returns>The populated difficulty modifiers.</returns>
    private static DifficultyModifiers CreateModifiers(IReadOnlyDictionary<string, double> values)
    {
        return new DifficultyModifiers
        {
            MineOutputPercent = (int)values["mine-output"],
            RefineryOutputPercent = (int)values["refinery-output"],
            MaintenanceCapacityPercent = (int)values["maintenance"],
            ManufacturingSpeedPercent = (int)values["manufacturing-speed"],
            MissionSuccessChancePoints = (int)values["mission-success"],
            MissionFoilChancePoints = (int)values["mission-foil"],
            MissionExecutionSpeedIncreasePercent = (int)values["mission-speed"],
            DetectionRatingMultiplier = values["detection-rating"],
        };
    }

    /// <summary>
    /// Copies all modifier values into another dictionary.
    /// </summary>
    /// <param name="source">The values to copy.</param>
    /// <param name="destination">The dictionary receiving the values.</param>
    private static void CopyValues(
        IReadOnlyDictionary<string, double> source,
        IDictionary<string, double> destination
    )
    {
        destination.Clear();
        foreach (KeyValuePair<string, double> pair in source)
            destination.Add(pair.Key, pair.Value);
    }

    /// <summary>
    /// Formats a modifier without adding a unit suffix.
    /// </summary>
    /// <param name="value">The value to display.</param>
    /// <returns>The invariant numeric text.</returns>
    private static string Format(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Updates the dirty count and Apply button from both modifier columns.
    /// </summary>
    private void RefreshChangedState()
    {
        int count = 0;
        foreach (var entry in entries)
        {
            if (playerDraft[entry.Key] != playerOriginal[entry.Key])
                count++;
            if (aiDraft[entry.Key] != aiOriginal[entry.Key])
                count++;
        }

        changedCount.SetText(count == 1 ? "1 CHANGE" : "{0} CHANGES", count);
        applyButton.interactable = count > 0;
    }

    /// <summary>
    /// Wires an info control's hover events to its modifier explanation.
    /// </summary>
    /// <param name="target">The info control's hover target.</param>
    /// <param name="description">The explanation to display.</param>
    private void ConfigureTooltip(RectTransform target, string description)
    {
        ScrollAreaDragRelay dragRelay =
            target.GetComponent<ScrollAreaDragRelay>()
            ?? target.gameObject.AddComponent<ScrollAreaDragRelay>();
        dragRelay.Initialize(scrollArea);
        EventTrigger trigger =
            target.GetComponent<EventTrigger>() ?? target.gameObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();
        EventTrigger.Entry enter = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter,
        };
        enter.callback.AddListener(_ =>
        {
            HideTooltip();
            tooltipDelay = StartCoroutine(ShowTooltipAfterDelay(target, description));
        });
        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideTooltip());
        trigger.triggers.Add(enter);
        trigger.triggers.Add(exit);
    }

    /// <summary>
    /// Shows one wrapped explanation after a short hover, clamped inside the menu frame.
    /// </summary>
    /// <param name="target">The hovered info control.</param>
    /// <param name="description">The explanation to display.</param>
    /// <returns>The hover-delay coroutine.</returns>
    private IEnumerator ShowTooltipAfterDelay(RectTransform target, string description)
    {
        yield return new WaitForSecondsRealtime(0.35f);
        tooltipDelay = null;
        if (!target.gameObject.activeInHierarchy)
            yield break;

        tooltipText.text = description;
        float height = tooltipText.GetPreferredValues(description, 312f, Mathf.Infinity).y + 12f;
        RectTransform parent = (RectTransform)tooltipRoot.parent;
        Vector3 point = parent.InverseTransformPoint(
            target.TransformPoint(new Vector3(0f, -target.rect.height - 4f, 0f))
        );
        float x = Mathf.Clamp(point.x - parent.rect.xMin, 8f, parent.rect.width - 334f);
        float y = Mathf.Clamp(parent.rect.yMax - point.y, 8f, parent.rect.height - height - 8f);
        tooltipRoot.anchoredPosition = new Vector2(x, -y);
        tooltipRoot.sizeDelta = new Vector2(326f, height);
        tooltipText.rectTransform.sizeDelta = new Vector2(312f, height - 12f);
        tooltipRoot.SetAsLastSibling();
        tooltipRoot.gameObject.SetActive(true);
    }

    /// <summary>
    /// Cancels pending help and hides visible help.
    /// </summary>
    private void HideTooltip()
    {
        if (tooltipDelay != null)
            StopCoroutine(tooltipDelay);
        tooltipDelay = null;
        if (tooltipRoot != null)
            tooltipRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// Clears hover state when the menu closes.
    /// </summary>
    private void OnDisable() => HideTooltip();

    /// <summary>
    /// Requests closure through the scene's existing modal owner.
    /// </summary>
    private void RequestClose() => CloseRequested?.Invoke();
}
