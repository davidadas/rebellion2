using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the in-game Options overlay and emits semantic requests. Audio channels are
/// indexed 0..4 as Master, Music, SFX, Ambience, Video.
/// </summary>
public sealed class OptionsMenuView : MonoBehaviour
{
    private static readonly Color ActiveTabColor = new Color(0.875f, 0.910f, 0.941f);
    private static readonly Color InactiveTabColor = new Color(0.573f, 0.635f, 0.706f);
    private static readonly Color AccentColor = new Color(0.373f, 0.659f, 0.925f);
    private static readonly Color MetaColor = new Color(0.573f, 0.635f, 0.706f);
    private static readonly Color TextColor = new Color(0.875f, 0.910f, 0.941f);
    private static readonly Color BadgeColor = new Color(0.204f, 0.243f, 0.302f);
    private static readonly Color BadgeListeningColor = new Color(0.373f, 0.659f, 0.925f);

    private readonly List<TextMeshProUGUI> bindingHeaderFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> bindingActionFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> bindingKeyFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> bindingSecondaryKeyFields = new List<TextMeshProUGUI>();
    private readonly List<Image> bindingRowImages = new List<Image>();
    private readonly List<Image> bindingBadgeImages = new List<Image>();
    private readonly List<Image> bindingSecondaryBadgeImages = new List<Image>();
    private readonly HashSet<Button> wiredBadges = new HashSet<Button>();
    private readonly List<Image> slotRowImages = new List<Image>();
    private readonly List<RawImage> slotIconImages = new List<RawImage>();
    private readonly List<TextMeshProUGUI> slotNameFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> slotMetaFields = new List<TextMeshProUGUI>();
    private readonly List<Button> slotDeleteButtons = new List<Button>();
    private readonly Dictionary<Button, float> lastSlotClickTime = new Dictionary<Button, float>();
    private readonly List<int> slotRowTops = new List<int>();
    private readonly List<OptionsSaveSlot> slotData = new List<OptionsSaveSlot>();
    private int slotRowWidth;
    private int slotRowHeight;
    private int renameRow = -1;
    private bool suppressRenameCommit;
    private bool pendingRenameFocus;

    [Header("Frame")]
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private TextMeshProUGUI headerTextField;

    [SerializeField]
    private TextMeshProUGUI pageTitleTextField;

    [Header("Navigation")]
    [SerializeField]
    private Sprite rowIdleSprite;

    [SerializeField]
    private Sprite rowActiveSprite;

    [SerializeField]
    private Button[] tabButtons = Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI[] tabLabelFields = Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private GameObject graphicsPage;

    [SerializeField]
    private GameObject audioPage;

    [SerializeField]
    private GameObject saveLoadPage;

    [SerializeField]
    private GameObject controlsPage;

    [Header("Footer")]
    [SerializeField]
    private Button backToGameButton;

    [SerializeField]
    private Button mainMenuButton;

    [SerializeField]
    private Button quitButton;

    [Header("Settings actions")]
    [SerializeField]
    private GameObject settingsActions;

    [SerializeField]
    private Button applyButton;

    [SerializeField]
    private Button defaultsButton;

    [SerializeField]
    private SaveMenuConfirmDialogView confirmDialog;

    [Header("Graphics page")]
    [SerializeField]
    private OptionsToggleRowView[] tacticalRows = Array.Empty<OptionsToggleRowView>();

    [SerializeField]
    private TextMeshProUGUI resolutionValueField;

    [SerializeField]
    private Button resolutionPrevButton;

    [SerializeField]
    private Button resolutionNextButton;

    [SerializeField]
    private TextMeshProUGUI fullScreenValueField;

    [SerializeField]
    private Button fullScreenPrevButton;

    [SerializeField]
    private Button fullScreenNextButton;

    [Header("Audio page")]
    [SerializeField]
    private SaveMenuSliderView[] volumeSliders = Array.Empty<SaveMenuSliderView>();

    [SerializeField]
    private TextMeshProUGUI[] volumeValueFields = Array.Empty<TextMeshProUGUI>();

    [Header("Save/Load page")]
    [SerializeField]
    private Button saveButton;

    [SerializeField]
    private Button loadButton;

    [SerializeField]
    private RawImage saveDisabledImage;

    [SerializeField]
    private RawImage loadDisabledImage;

    [SerializeField]
    private ScrollAreaView saveSlotScrollArea;

    [SerializeField]
    private Image slotRowTemplate;

    [SerializeField]
    private RawImage slotIconTemplate;

    [SerializeField]
    private TextMeshProUGUI slotNameTemplate;

    [SerializeField]
    private TextMeshProUGUI slotMetaTemplate;

    [SerializeField]
    private Button slotDeleteTemplate;

    [SerializeField]
    private TMP_InputField slotRenameField;

    [Header("Controls page")]
    [SerializeField]
    private ScrollAreaView controlsScrollArea;

    [SerializeField]
    private Image bindingRowTemplate;

    [SerializeField]
    private TextMeshProUGUI bindingHeaderTemplate;

    [SerializeField]
    private TextMeshProUGUI bindingActionTemplate;

    [SerializeField]
    private Image bindingKeyBadgeTemplate;

    [SerializeField]
    private TextMeshProUGUI bindingKeyTemplate;

    private bool bound;
    private string activeConfirmMessage;

    /// <summary>Occurs when the player selects a page.</summary>
    public event Action<OptionsMenuTab> TabSelected;

    /// <summary>Occurs when the player asks to resume the game.</summary>
    public event Action ResumeRequested;

    /// <summary>Occurs when the player asks to save.</summary>
    public event Action SaveRequested;

    /// <summary>Occurs when the player asks to load.</summary>
    public event Action LoadRequested;

    /// <summary>Occurs when the player selects a save slot by index (single click).</summary>
    public event Action<int> SlotSelected;

    /// <summary>
    /// Occurs when the player commits a save-slot name (row, new name, submitted with Return).
    /// </summary>
    public event Action<int, string, bool> SlotRenamed;

    /// <summary>Occurs when the player asks to delete a save slot by index.</summary>
    public event Action<int> SlotDeleteRequested;

    /// <summary>Occurs when inline text editing begins (true) or ends (false).</summary>
    public event Action<bool> RenameEditingChanged;

    /// <summary>Occurs when the player commits pending settings.</summary>
    public event Action ApplyRequested;

    /// <summary>Occurs when the player resets settings to defaults.</summary>
    public event Action DefaultsRequested;

    /// <summary>Occurs when the player accepts the active confirmation prompt.</summary>
    public event Action ConfirmAccepted;

    /// <summary>Occurs when the player declines the active confirmation prompt.</summary>
    public event Action ConfirmDeclined;

    /// <summary>Occurs when the player double-clicks a binding to rebind it (row, isSecondary).</summary>
    public event Action<int, bool> RebindRequested;

    /// <summary>Occurs when the player asks to return to the main menu.</summary>
    public event Action MainMenuRequested;

    /// <summary>Occurs when the player asks to quit the application.</summary>
    public event Action QuitRequested;

    /// <summary>Occurs when the player toggles a detail option.</summary>
    public event Action<UserTacticalOption> TacticalToggleRequested;

    /// <summary>Occurs when the player steps the resolution by the given delta.</summary>
    public event Action<int> ResolutionStepRequested;

    /// <summary>Occurs when the player steps the display mode by the given delta.</summary>
    public event Action<int> FullScreenStepRequested;

    /// <summary>Occurs when the player changes a volume channel (index, normalized value).</summary>
    public event Action<int, float> VolumeChanged;

    /// <summary>Occurs when the view is destroyed.</summary>
    public event Action<OptionsMenuView> Destroyed;

    /// <summary>
    /// Verifies the authored hierarchy and binds control listeners.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        ExpandDisplayStepperHitAreas();
        BindControls();
    }

    /// <summary>
    /// Makes each display value's full left and right halves clickable. The authored chevrons stay
    /// in place, but players no longer have to hit a nearly invisible 26-pixel hotspot precisely.
    /// Applied at runtime as well as by the prefab builder so older generated prefabs are repaired.
    /// </summary>
    private void ExpandDisplayStepperHitAreas()
    {
        ExpandDisplayStepperPair(resolutionPrevButton, resolutionNextButton);
        ExpandDisplayStepperPair(fullScreenPrevButton, fullScreenNextButton);
    }

    /// <summary>Expands one pair of step buttons across its complete 155-pixel value badge.</summary>
    /// <param name="previous">The previous-value button.</param>
    /// <param name="next">The next-value button.</param>
    private static void ExpandDisplayStepperPair(Button previous, Button next)
    {
        RectTransform previousRect = previous.transform as RectTransform;
        RectTransform nextRect = next.transform as RectTransform;
        int y = UILayout.GetSourceRect(previousRect).y;
        UILayout.SetSourceRect(previousRect, 194, y, 78, 18);
        UILayout.SetSourceRect(nextRect, 272, y, 77, 18);

        if (
            previous.targetGraphic != null
            && previous.targetGraphic.transform != previous.transform
        )
            UILayout.SetSourceRect(previous.targetGraphic.rectTransform, 0, 0, 26, 18);
        if (next.targetGraphic != null && next.targetGraphic.transform != next.transform)
            UILayout.SetSourceRect(next.targetGraphic.rectTransform, 51, 0, 26, 18);
    }

    /// <summary>
    /// Removes control listeners and notifies the owning controller of destruction.
    /// </summary>
    private void OnDestroy()
    {
        UnbindControls();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies a complete Options presentation to the authored hierarchy.
    /// </summary>
    /// <param name="data">The immutable presentation snapshot.</param>
    public void Render(OptionsMenuRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetTextContent(headerTextField, "OPTIONS");
        UILayout.SetTextContent(pageTitleTextField, GetTabTitle(data.ActiveTab));
        if (data.ActiveTab != OptionsMenuTab.SaveLoad)
            CancelRename();
        RenderTabs(data.ActiveTab);
        RenderFooter(data);
        RenderGraphicsPage(data);
        RenderAudioPage(data);
        RenderSaveLoadPage(data);
        RenderControlsPage(data);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Highlights the active tab and shows only its page.
    /// </summary>
    /// <param name="activeTab">The currently visible page.</param>
    private void RenderTabs(OptionsMenuTab activeTab)
    {
        for (int i = 0; i < tabLabelFields.Length; i++)
        {
            bool active = i == (int)activeTab;
            if (tabLabelFields[i] != null)
                tabLabelFields[i].color = active ? ActiveTabColor : InactiveTabColor;
            if (
                i < tabButtons.Length
                && tabButtons[i] != null
                && tabButtons[i].targetGraphic is Image tabImage
            )
            {
                tabImage.sprite = active ? rowActiveSprite : rowIdleSprite;
            }
        }

        SetPageActive(graphicsPage, activeTab == OptionsMenuTab.Graphics);
        SetPageActive(audioPage, activeTab == OptionsMenuTab.Audio);
        SetPageActive(saveLoadPage, activeTab == OptionsMenuTab.SaveLoad);
        SetPageActive(controlsPage, activeTab == OptionsMenuTab.Controls);
        if (settingsActions != null)
            settingsActions.SetActive(activeTab != OptionsMenuTab.SaveLoad);
    }

    /// <summary>
    /// Applies footer action availability.
    /// </summary>
    /// <param name="data">The presentation snapshot.</param>
    private void RenderFooter(OptionsMenuRenderData data)
    {
        // With no running game (e.g. the Main Menu) the rows stay in place to preserve the grid, but
        // are disabled: there is nothing to return to, and the Main Menu is already the destination.
        backToGameButton.interactable = data.CanReturnToGame;
        mainMenuButton.interactable = data.CanReturnToMainMenu;
    }

    /// <summary>
    /// Applies detail-toggle and display values to the Graphics page.
    /// </summary>
    /// <param name="data">The presentation snapshot.</param>
    private void RenderGraphicsPage(OptionsMenuRenderData data)
    {
        foreach (OptionsToggleRowView row in tacticalRows)
        {
            if (row == null)
                continue;

            bool enabled = data.TacticalStates.TryGetValue(row.Option, out bool value) && value;
            row.Render(enabled);
        }

        UILayout.SetTextContent(resolutionValueField, data.ResolutionLabel);
        UILayout.SetTextContent(fullScreenValueField, data.FullScreenLabel);
    }

    /// <summary>
    /// Applies normalized volumes to the Audio page sliders and labels.
    /// </summary>
    /// <param name="data">The presentation snapshot.</param>
    private void RenderAudioPage(OptionsMenuRenderData data)
    {
        for (int i = 0; i < volumeSliders.Length; i++)
        {
            float value = i < data.Volumes.Count ? data.Volumes[i] : 0f;
            if (volumeSliders[i] != null)
                volumeSliders[i].Render(value);
            if (i < volumeValueFields.Length && volumeValueFields[i] != null)
                UILayout.SetTextContent(
                    volumeValueFields[i],
                    Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString()
                );
        }
    }

    /// <summary>
    /// Rebuilds the save-slot list and applies action availability.
    /// </summary>
    /// <param name="data">The presentation snapshot.</param>
    private void RenderSaveLoadPage(OptionsMenuRenderData data)
    {
        int selected = data.SelectedSlot;
        bool existingSelected =
            selected >= 0 && selected < data.SaveSlots.Count && !data.SaveSlots[selected].IsCreateNew;
        bool canSaveNow = data.CanSave && existingSelected;
        bool canLoadNow = data.CanLoad && existingSelected;
        saveButton.interactable = canSaveNow;
        loadButton.interactable = canLoadNow;
        SetButtonDisabledVisual(saveButton, saveDisabledImage, canSaveNow);
        SetButtonDisabledVisual(loadButton, loadDisabledImage, canLoadNow);

        RectInt rowTemplate = UILayout.GetSourceRect(slotRowTemplate.rectTransform);
        RectInt iconTemplate = UILayout.GetSourceRect(slotIconTemplate.rectTransform);
        RectInt nameTemplate = UILayout.GetSourceRect(slotNameTemplate.rectTransform);
        RectInt dateTemplate = UILayout.GetSourceRect(slotMetaTemplate.rectTransform);
        slotRowWidth = rowTemplate.width;
        slotRowHeight = rowTemplate.height;
        slotData.Clear();
        slotRowTops.Clear();
        int gap = 6;
        int contentHeight = 0;
        for (int i = 0; i < data.SaveSlots.Count; i++)
        {
            OptionsSaveSlot slot = data.SaveSlots[i];
            int top = contentHeight;
            slotData.Add(slot);
            slotRowTops.Add(top);

            Image rowImage = GetSlotRow(i);
            UILayout.SetSourceRect(
                rowImage.rectTransform,
                rowTemplate.x,
                top,
                rowTemplate.width,
                rowTemplate.height
            );
            rowImage.sprite = i == selected ? rowActiveSprite : rowIdleSprite;
            rowImage.color = Color.white;

            RawImage icon = GetSlotIcon(i);
            icon.texture = slot.FactionIcon;
            icon.enabled = !slot.IsCreateNew && slot.FactionIcon != null;
            RectInt iconRect = FitPreservingAspect(slot.FactionIcon, iconTemplate);
            UILayout.SetSourceRect(
                icon.rectTransform,
                iconRect.x,
                top + iconRect.y,
                iconRect.width,
                iconRect.height
            );

            TextMeshProUGUI nameField = GetSlotField(slotNameFields, slotNameTemplate, "SlotName", i);
            if (slot.IsCreateNew)
            {
                UILayout.SetTemplateText(
                    nameField,
                    slotNameTemplate,
                    slot.Name,
                    AccentColor,
                    new RectInt(0, top, rowTemplate.width, rowTemplate.height)
                );
                nameField.alignment = TextAlignmentOptions.Midline;
            }
            else
            {
                UILayout.SetTemplateText(
                    nameField,
                    slotNameTemplate,
                    slot.Name,
                    TextColor,
                    new RectInt(nameTemplate.x, top + nameTemplate.y, nameTemplate.width, nameTemplate.height)
                );
            }

            UILayout.SetTemplateText(
                GetSlotField(slotMetaFields, slotMetaTemplate, "SlotDate", i),
                slotMetaTemplate,
                slot.IsCreateNew ? string.Empty : slot.Date,
                MetaColor,
                new RectInt(dateTemplate.x, top + dateTemplate.y, dateTemplate.width, dateTemplate.height)
            );

            Button deleteButton = GetSlotDelete(i);
            deleteButton.gameObject.SetActive(!slot.IsCreateNew);
            UILayout.SetSourceRect(
                (RectTransform)deleteButton.transform,
                rowTemplate.width - 24,
                top + (rowTemplate.height - 16) / 2,
                16,
                16
            );

            contentHeight += rowTemplate.height + gap;
        }

        saveSlotScrollArea.SetContentHeight(contentHeight, rowTemplate.height + gap, false);
        HideImagesFrom(slotRowImages, data.SaveSlots.Count);
        HideRawImagesFrom(slotIconImages, data.SaveSlots.Count);
        HideFieldsFrom(slotNameFields, data.SaveSlots.Count);
        HideFieldsFrom(slotMetaFields, data.SaveSlots.Count);
        HideButtonsFrom(slotDeleteButtons, data.SaveSlots.Count);
    }

    /// <summary>
    /// Rebuilds the read-only key-binding list: accent group headers followed by
    /// backed rows with badged key captions.
    /// </summary>
    /// <param name="data">The presentation snapshot.</param>
    private void RenderControlsPage(OptionsMenuRenderData data)
    {
        RectInt rowTemplate = UILayout.GetSourceRect(bindingRowTemplate.rectTransform);
        RectInt headerTemplate = UILayout.GetSourceRect(bindingHeaderTemplate.rectTransform);
        RectInt actionTemplate = UILayout.GetSourceRect(bindingActionTemplate.rectTransform);
        RectInt badgeTemplate = UILayout.GetSourceRect(bindingKeyBadgeTemplate.rectTransform);
        RectInt keyTemplate = UILayout.GetSourceRect(bindingKeyTemplate.rectTransform);
        int contentHeight = 0;
        int headerCount = 0;
        int rowCount = 0;
        for (int i = 0; i < data.Bindings.Count; i++)
        {
            OptionsBindingRow binding = data.Bindings[i];
            if (binding.IsHeader)
            {
                if (i > 0)
                    contentHeight += 6;
                UILayout.SetTemplateText(
                    GetBindingField(
                        bindingHeaderFields,
                        bindingHeaderTemplate,
                        "BindingHeader",
                        headerCount++
                    ),
                    bindingHeaderTemplate,
                    binding.Action,
                    AccentColor,
                    new RectInt(
                        headerTemplate.x,
                        contentHeight,
                        headerTemplate.width,
                        headerTemplate.height
                    )
                );
                contentHeight += headerTemplate.height + 4;
                continue;
            }

            Image rowImage = GetBindingImage(
                bindingRowImages,
                bindingRowTemplate,
                "BindingRow",
                rowCount
            );
            UILayout.SetSourceRect(
                rowImage.rectTransform,
                rowTemplate.x,
                contentHeight,
                rowTemplate.width,
                rowTemplate.height
            );
            UILayout.SetTemplateText(
                GetBindingField(
                    bindingActionFields,
                    bindingActionTemplate,
                    "BindingAction",
                    rowCount
                ),
                bindingActionTemplate,
                binding.Action,
                MetaColor,
                new RectInt(
                    actionTemplate.x,
                    contentHeight + actionTemplate.y,
                    180,
                    actionTemplate.height
                )
            );
            RenderBindingKey(
                bindingBadgeImages,
                bindingKeyFields,
                "BindingPrimary",
                rowCount,
                badgeTemplate.x - 67,
                contentHeight,
                binding.Primary,
                badgeTemplate,
                keyTemplate,
                false,
                rowCount == data.ListeningRow && !data.ListeningSecondary
            );
            RenderBindingKey(
                bindingSecondaryBadgeImages,
                bindingSecondaryKeyFields,
                "BindingSecondary",
                rowCount,
                badgeTemplate.x,
                contentHeight,
                binding.Secondary,
                badgeTemplate,
                keyTemplate,
                true,
                rowCount == data.ListeningRow && data.ListeningSecondary
            );
            rowCount++;
            contentHeight += rowTemplate.height + 5;
        }

        controlsScrollArea.SetContentHeight(contentHeight, rowTemplate.height + 5, false);
        HideFieldsFrom(bindingHeaderFields, headerCount);
        HideFieldsFrom(bindingActionFields, rowCount);
        HideFieldsFrom(bindingKeyFields, rowCount);
        HideFieldsFrom(bindingSecondaryKeyFields, rowCount);
        HideImagesFrom(bindingRowImages, rowCount);
        HideImagesFrom(bindingBadgeImages, rowCount);
        HideImagesFrom(bindingSecondaryBadgeImages, rowCount);
    }

    /// <summary>
    /// Renders one key badge + caption for a binding column, cloning the shared key templates.
    /// </summary>
    /// <param name="badges">The badge cache for this column.</param>
    /// <param name="keys">The key-field cache for this column.</param>
    /// <param name="prefix">The instance-name prefix.</param>
    /// <param name="index">The row index.</param>
    /// <param name="x">The source-space column left.</param>
    /// <param name="top">The row's source-space top.</param>
    /// <param name="text">The key caption.</param>
    /// <param name="badgeTemplate">The badge template rect.</param>
    /// <param name="keyTemplate">The key template rect.</param>
    /// <param name="isSecondary">Whether this is the secondary column.</param>
    /// <param name="listening">Whether this cell is awaiting a key press.</param>
    private void RenderBindingKey(
        List<Image> badges,
        List<TextMeshProUGUI> keys,
        string prefix,
        int index,
        int x,
        int top,
        string text,
        RectInt badgeTemplate,
        RectInt keyTemplate,
        bool isSecondary,
        bool listening
    )
    {
        Image badge = GetBindingImage(badges, bindingKeyBadgeTemplate, prefix + "Badge", index);
        if (badge.TryGetComponent(out Button badgeButton) && wiredBadges.Add(badgeButton))
        {
            int row = index;
            badgeButton.onClick.AddListener(() => HandleBadgeClick(badgeButton, row, isSecondary));
        }

        // Both columns always show an identical gray bubble; a bound cell simply carries text.
        // The bubble turns accent while it listens for a new key.
        badge.gameObject.SetActive(true);
        badge.color = listening ? BadgeListeningColor : BadgeColor;
        UILayout.SetSourceRect(
            badge.rectTransform,
            x,
            top + badgeTemplate.y,
            badgeTemplate.width,
            badgeTemplate.height
        );
        UILayout.SetTemplateText(
            GetBindingField(keys, bindingKeyTemplate, prefix + "Key", index),
            bindingKeyTemplate,
            listening ? "..." : text,
            TextColor,
            new RectInt(x, top + keyTemplate.y, keyTemplate.width, keyTemplate.height)
        );
    }

    /// <summary>
    /// Raises a rebind request when a binding badge is clicked.
    /// </summary>
    /// <param name="button">The clicked badge button.</param>
    /// <param name="row">The binding row index.</param>
    /// <param name="secondary">Whether the secondary column was clicked.</param>
    private void HandleBadgeClick(Button button, int row, bool secondary)
    {
        RebindRequested?.Invoke(row, secondary);
    }

    /// <summary>
    /// Gets or creates a reusable controls-page image cloned from a template.
    /// </summary>
    /// <param name="images">The image cache.</param>
    /// <param name="template">The authored template image.</param>
    /// <param name="namePrefix">The instance-name prefix.</param>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable image.</returns>
    private Image GetBindingImage(List<Image> images, Image template, string namePrefix, int index)
    {
        while (images.Count <= index)
        {
            Image image = Instantiate(template, controlsScrollArea.ContentRoot);
            image.name = $"{namePrefix}{images.Count}";
            image.gameObject.SetActive(true);
            images.Add(image);
        }

        return images[index];
    }

    /// <summary>
    /// Shows the disabled-gray overlay and hides the live icon when a button is unavailable.
    /// </summary>
    /// <param name="button">The action button.</param>
    /// <param name="disabledImage">The disabled-gray overlay icon, if authored.</param>
    /// <param name="enabled">Whether the action is currently available.</param>
    private static void SetButtonDisabledVisual(Button button, RawImage disabledImage, bool enabled)
    {
        if (disabledImage != null)
            disabledImage.gameObject.SetActive(!enabled);
        if (button != null && button.targetGraphic != null)
            button.targetGraphic.enabled = enabled;
    }

    /// <summary>
    /// Gets or creates a reusable save-slot background image cloned from the template.
    /// </summary>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable row image.</returns>
    private Image GetSlotRow(int index)
    {
        while (slotRowImages.Count <= index)
        {
            int slotIndex = slotRowImages.Count;
            Image image = Instantiate(slotRowTemplate, saveSlotScrollArea.ContentRoot);
            image.name = $"SlotRow{slotIndex}";
            image.gameObject.SetActive(true);
            if (image.TryGetComponent(out Button rowButton))
                rowButton.onClick.AddListener(() => HandleSlotClick(rowButton, slotIndex));
            slotRowImages.Add(image);
        }

        return slotRowImages[index];
    }

    /// <summary>
    /// Gets or creates a reusable per-row delete button cloned from the template.
    /// </summary>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable delete button.</returns>
    private Button GetSlotDelete(int index)
    {
        while (slotDeleteButtons.Count <= index)
        {
            int slotIndex = slotDeleteButtons.Count;
            Button button = Instantiate(slotDeleteTemplate, saveSlotScrollArea.ContentRoot);
            button.name = $"SlotDelete{slotIndex}";
            button.gameObject.SetActive(true);
            button.transform.SetAsLastSibling();
            button.onClick.AddListener(() => SlotDeleteRequested?.Invoke(slotIndex));
            slotDeleteButtons.Add(button);
        }

        return slotDeleteButtons[index];
    }

    /// <summary>
    /// Routes a save-row click: one click selects an existing save, while a double click opens its
    /// inline rename editor. The create-new row opens its editor immediately.
    /// </summary>
    /// <param name="button">The clicked row button.</param>
    /// <param name="index">The clicked row index.</param>
    private void HandleSlotClick(Button button, int index)
    {
        if (index < 0 || index >= slotData.Count)
            return;

        bool existing = !slotData[index].IsCreateNew;
        float now = Time.unscaledTime;
        if (
            existing
            && lastSlotClickTime.TryGetValue(button, out float last)
            && now - last < 0.35f
        )
        {
            lastSlotClickTime[button] = 0f;
            CancelRename();
            SlotSelected?.Invoke(index);
            BeginRename(index);
            return;
        }

        lastSlotClickTime[button] = now;
        CancelRename();
        SlotSelected?.Invoke(index);
        if (!existing)
            BeginRename(index);
    }

    /// <summary>
    /// Opens the inline text editor over the given save row. For the create-new row this starts
    /// empty so the player types the new save's name; for an existing row it seeds the current name.
    /// </summary>
    /// <param name="index">The row to edit.</param>
    private void BeginRename(int index)
    {
        if (slotRenameField == null || index < 0 || index >= slotData.Count)
            return;

        renameRow = index;
        suppressRenameCommit = false;
        int width = slotData[index].IsCreateNew ? slotRowWidth - 44 : slotRowWidth - 62;
        // A tight box centered on the row keeps the caret on the text line (a tall box lets TMP park
        // the caret at the top edge, well above the centered text).
        int fieldHeight = 18;
        int fieldTop = slotRowTops[index] + (slotRowHeight - fieldHeight) / 2;
        // TMP creates its caret as a separate sibling in OnEnable and copies the text component's
        // RectTransform at that moment. Finalize both the field and label geometry while inactive;
        // changing it after activation leaves the cached caret at the prefab's old top edge.
        UILayout.SetSourceRect(
            (RectTransform)slotRenameField.transform,
            32,
            fieldTop,
            width,
            fieldHeight
        );
        AlignRenameInput();
        slotRenameField.transform.SetAsLastSibling();
        slotRenameField.gameObject.SetActive(true);
        slotRenameField.SetTextWithoutNotify(
            slotData[index].IsCreateNew ? string.Empty : slotData[index].Name
        );

        // Focus is deferred one frame: activating the field inside the row's click handler would be
        // undone by the EventSystem finishing the same click (it would deselect the field, firing an
        // immediate end-edit that hides the box and clears the selection).
        pendingRenameFocus = true;
        RenameEditingChanged?.Invoke(true);
    }

    /// <summary>Keeps the inline editor's label, placeholder, and generated caret aligned.</summary>
    private void AlignRenameInput()
    {
        if (slotRenameField.textComponent is TextMeshProUGUI text)
        {
            text.alignment = TextAlignmentOptions.MidlineLeft;
            StretchRenameText(text.rectTransform);
        }

        if (slotRenameField.placeholder is TextMeshProUGUI placeholder)
        {
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            StretchRenameText(placeholder.rectTransform);
        }

        slotRenameField.textViewport = slotRenameField.transform as RectTransform;
        slotRenameField.customCaretColor = true;
        slotRenameField.caretColor = Color.white;
        slotRenameField.caretWidth = 2;
        slotRenameField.caretBlinkRate = 0.85f;
        slotRenameField.onFocusSelectAll = false;
        slotRenameField.ForceLabelUpdate();
    }

    /// <summary>Stretches an inline-editor label with its authored horizontal inset.</summary>
    /// <param name="rect">The label rectangle.</param>
    private static void StretchRenameText(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(6f, 0f);
        rect.offsetMax = new Vector2(-6f, 0f);
    }

    /// <summary>
    /// Grants keyboard focus to a freshly opened rename editor one frame after it was requested.
    /// </summary>
    private void Update()
    {
        if (!pendingRenameFocus)
            return;

        pendingRenameFocus = false;
        if (slotRenameField != null && slotRenameField.gameObject.activeInHierarchy)
        {
            slotRenameField.Select();
            slotRenameField.ActivateInputField();
            slotRenameField.caretPosition = slotRenameField.text?.Length ?? 0;
            slotRenameField.selectionAnchorPosition = slotRenameField.caretPosition;
            slotRenameField.selectionFocusPosition = slotRenameField.caretPosition;
            slotRenameField.ForceLabelUpdate();
        }
    }

    /// <summary>
    /// Closes the inline rename editor without committing a rename.
    /// </summary>
    private void CancelRename()
    {
        if (renameRow < 0)
            return;

        renameRow = -1;
        suppressRenameCommit = true;
        pendingRenameFocus = false;
        if (slotRenameField != null)
            slotRenameField.gameObject.SetActive(false);
        RenameEditingChanged?.Invoke(false);
    }

    /// <summary>
    /// Commits (or discards) the inline rename when the editor loses focus or the player submits.
    /// </summary>
    /// <param name="newName">The edited name.</param>
    private void HandleRenameEndEdit(string newName)
    {
        CompleteRename(newName, false);
    }

    /// <summary>Commits the inline name with Return so gameplay can save the edited slot.</summary>
    /// <param name="newName">The submitted name.</param>
    private void HandleRenameSubmitted(string newName)
    {
        CompleteRename(newName, true);
    }

    /// <summary>Completes one inline name edit exactly once.</summary>
    /// <param name="newName">The edited name.</param>
    /// <param name="submitted">Whether Return submitted the edit.</param>
    private void CompleteRename(string newName, bool submitted)
    {
        if (renameRow < 0)
        {
            suppressRenameCommit = false;
            return;
        }

        int row = renameRow;
        renameRow = -1;
        pendingRenameFocus = false;
        RenameEditingChanged?.Invoke(false);
        if (slotRenameField != null)
            slotRenameField.gameObject.SetActive(false);
        if (suppressRenameCommit)
        {
            suppressRenameCommit = false;
            return;
        }

        SlotRenamed?.Invoke(row, newName, submitted);
    }

    /// <summary>
    /// Hides cached buttons beginning at the supplied index.
    /// </summary>
    /// <param name="buttons">The button cache.</param>
    /// <param name="firstHiddenIndex">The first button to hide.</param>
    private static void HideButtonsFrom(List<Button> buttons, int firstHiddenIndex)
    {
        for (int i = firstHiddenIndex; i < buttons.Count; i++)
            buttons[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Fits a texture inside a box, preserving its aspect ratio and centering it.
    /// </summary>
    /// <param name="texture">The texture to fit, or null.</param>
    /// <param name="box">The available source-space box.</param>
    /// <returns>The centered, aspect-correct rect.</returns>
    private static RectInt FitPreservingAspect(Texture texture, RectInt box)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0)
            return box;

        float aspect = (float)texture.width / texture.height;
        int width;
        int height;
        if (aspect >= (float)box.width / box.height)
        {
            width = box.width;
            height = Mathf.RoundToInt(box.width / aspect);
        }
        else
        {
            height = box.height;
            width = Mathf.RoundToInt(box.height * aspect);
        }

        return new RectInt(
            box.x + (box.width - width) / 2,
            box.y + (box.height - height) / 2,
            width,
            height
        );
    }

    /// <summary>
    /// Gets or creates a reusable save-row faction icon cloned from the template.
    /// </summary>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable icon image.</returns>
    private RawImage GetSlotIcon(int index)
    {
        while (slotIconImages.Count <= index)
        {
            RawImage icon = Instantiate(slotIconTemplate, saveSlotScrollArea.ContentRoot);
            icon.name = $"SlotIcon{slotIconImages.Count}";
            icon.gameObject.SetActive(true);
            slotIconImages.Add(icon);
        }

        return slotIconImages[index];
    }

    /// <summary>
    /// Gets or creates a reusable slot text field cloned from a template.
    /// </summary>
    /// <param name="fields">The field cache.</param>
    /// <param name="template">The authored template field.</param>
    /// <param name="namePrefix">The instance-name prefix.</param>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable field.</returns>
    private TextMeshProUGUI GetSlotField(
        List<TextMeshProUGUI> fields,
        TextMeshProUGUI template,
        string namePrefix,
        int index
    )
    {
        while (fields.Count <= index)
        {
            TextMeshProUGUI field = Instantiate(template, saveSlotScrollArea.ContentRoot);
            field.name = $"{namePrefix}{fields.Count}";
            field.gameObject.SetActive(true);
            fields.Add(field);
        }

        return fields[index];
    }

    /// <summary>
    /// Gets or creates a reusable binding field cloned from a template.
    /// </summary>
    /// <param name="fields">The field cache.</param>
    /// <param name="template">The authored template field.</param>
    /// <param name="namePrefix">The instance-name prefix.</param>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable binding field.</returns>
    private TextMeshProUGUI GetBindingField(
        List<TextMeshProUGUI> fields,
        TextMeshProUGUI template,
        string namePrefix,
        int index
    )
    {
        while (fields.Count <= index)
        {
            TextMeshProUGUI field = Instantiate(template, controlsScrollArea.ContentRoot);
            field.name = $"{namePrefix}{fields.Count}";
            field.gameObject.SetActive(true);
            fields.Add(field);
        }

        return fields[index];
    }

    /// <summary>
    /// Hides cached images beginning at the supplied index.
    /// </summary>
    /// <param name="images">The image cache.</param>
    /// <param name="firstHiddenIndex">The first image to hide.</param>
    private static void HideImagesFrom(List<Image> images, int firstHiddenIndex)
    {
        for (int i = firstHiddenIndex; i < images.Count; i++)
            images[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides cached raw images beginning at the supplied index.
    /// </summary>
    /// <param name="images">The raw-image cache.</param>
    /// <param name="firstHiddenIndex">The first image to hide.</param>
    private static void HideRawImagesFrom(List<RawImage> images, int firstHiddenIndex)
    {
        for (int i = firstHiddenIndex; i < images.Count; i++)
            images[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Hides cached text fields beginning at the supplied index.
    /// </summary>
    /// <param name="fields">The field cache to update.</param>
    /// <param name="firstHiddenIndex">The first field to hide.</param>
    private static void HideFieldsFrom(List<TextMeshProUGUI> fields, int firstHiddenIndex)
    {
        for (int i = firstHiddenIndex; i < fields.Count; i++)
            fields[i].gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows or hides one authored page container.
    /// </summary>
    /// <param name="page">The authored page container.</param>
    /// <param name="active">Whether the page should be visible.</param>
    private static void SetPageActive(GameObject page, bool active)
    {
        if (page != null && page.activeSelf != active)
            page.SetActive(active);
    }

    /// <summary>
    /// Resolves the display title for a page.
    /// </summary>
    /// <param name="tab">The page whose title is requested.</param>
    /// <returns>The page title.</returns>
    private static string GetTabTitle(OptionsMenuTab tab)
    {
        return tab switch
        {
            OptionsMenuTab.Graphics => "GRAPHICS",
            OptionsMenuTab.Audio => "AUDIO",
            OptionsMenuTab.SaveLoad => "SAVE / LOAD",
            OptionsMenuTab.Controls => "CONTROLS",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Attaches every control listener exactly once.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            OptionsMenuTab tab = (OptionsMenuTab)i;
            if (tabButtons[i] != null)
                tabButtons[i].onClick.AddListener(() => TabSelected?.Invoke(tab));
        }

        backToGameButton.onClick.AddListener(() => ResumeRequested?.Invoke());
        mainMenuButton.onClick.AddListener(() => MainMenuRequested?.Invoke());
        quitButton.onClick.AddListener(() => QuitRequested?.Invoke());
        saveButton.onClick.AddListener(() => SaveRequested?.Invoke());
        loadButton.onClick.AddListener(() => LoadRequested?.Invoke());
        applyButton.onClick.AddListener(() => ApplyRequested?.Invoke());
        defaultsButton.onClick.AddListener(() => DefaultsRequested?.Invoke());
        confirmDialog.Confirmed += HandleConfirmAccepted;
        confirmDialog.Canceled += HandleConfirmDeclined;

        foreach (OptionsToggleRowView row in tacticalRows)
        {
            if (row != null)
                row.ToggleRequested += HandleTacticalToggle;
        }

        resolutionPrevButton.onClick.AddListener(() => ResolutionStepRequested?.Invoke(-1));
        resolutionNextButton.onClick.AddListener(() => ResolutionStepRequested?.Invoke(1));
        fullScreenPrevButton.onClick.AddListener(() => FullScreenStepRequested?.Invoke(-1));
        fullScreenNextButton.onClick.AddListener(() => FullScreenStepRequested?.Invoke(1));

        for (int i = 0; i < volumeSliders.Length; i++)
        {
            int channel = i;
            if (volumeSliders[i] != null)
                volumeSliders[i].ValueChanged += value =>
                {
                    if (channel < volumeValueFields.Length && volumeValueFields[channel] != null)
                        UILayout.SetTextContent(
                            volumeValueFields[channel],
                            Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString()
                        );
                    VolumeChanged?.Invoke(channel, value);
                };
        }

        if (slotRenameField != null)
        {
            slotRenameField.onEndEdit.AddListener(HandleRenameEndEdit);
            slotRenameField.onSubmit.AddListener(HandleRenameSubmitted);
        }

        bound = true;
    }

    /// <summary>
    /// Removes tactical-row listeners that outlive the Unity button lifetime.
    /// </summary>
    private void UnbindControls()
    {
        foreach (OptionsToggleRowView row in tacticalRows)
        {
            if (row != null)
                row.ToggleRequested -= HandleTacticalToggle;
        }
    }

    /// <summary>
    /// Forwards a detail-toggle request to subscribers.
    /// </summary>
    /// <param name="option">The toggled option.</param>
    private void HandleTacticalToggle(UserTacticalOption option)
    {
        TacticalToggleRequested?.Invoke(option);
    }

    /// <summary>
    /// Shows the confirmation prompt with a message.
    /// </summary>
    /// <param name="message">The prompt text.</param>
    public void ShowConfirm(string message)
    {
        activeConfirmMessage = message ?? string.Empty;
        confirmDialog.Show(message);
    }

    /// <summary>
    /// Restores the confirmation and locks its controls while the host changes scenes.
    /// </summary>
    public void HoldConfirmForTransition()
    {
        if (confirmDialog == null)
            return;

        // The shared confirmation view hides itself before raising Confirmed. Re-show the same
        // prompt so its full-screen blocker remains in place until the scene replaces this view.
        confirmDialog.Show(activeConfirmMessage);
        confirmDialog.SetInteractionEnabled(false);
    }

    /// <summary>
    /// Hides the confirmation prompt.
    /// </summary>
    public void HideConfirm()
    {
        if (confirmDialog != null)
            confirmDialog.Hide();
    }

    /// <summary>
    /// Forwards the dialog's confirm response to subscribers.
    /// </summary>
    private void HandleConfirmAccepted()
    {
        ConfirmAccepted?.Invoke();
    }

    /// <summary>
    /// Forwards the dialog's decline response to subscribers.
    /// </summary>
    private void HandleConfirmDeclined()
    {
        ConfirmDeclined?.Invoke();
    }

    /// <summary>
    /// Verifies every authored reference required by the overlay.
    /// </summary>
    private void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (headerTextField == null || pageTitleTextField == null)
            throw new MissingReferenceException($"{name} is missing a title field.");
        if (rowIdleSprite == null || rowActiveSprite == null)
            throw new MissingReferenceException($"{name} is missing a row sprite.");
        if (tabButtons.Length != 4 || tabLabelFields.Length != 4)
            throw new MissingReferenceException($"{name} expects four tabs.");
        if (graphicsPage == null || audioPage == null || saveLoadPage == null
            || controlsPage == null)
            throw new MissingReferenceException($"{name} is missing a page container.");
        if (backToGameButton == null || mainMenuButton == null || quitButton == null)
            throw new MissingReferenceException($"{name} is missing a footer button.");
        if (settingsActions == null || applyButton == null || defaultsButton == null)
            throw new MissingReferenceException($"{name} is missing a settings-action button.");
        if (confirmDialog == null)
            throw new MissingReferenceException($"{name} is missing the confirm dialog.");
        if (resolutionValueField == null || fullScreenValueField == null)
            throw new MissingReferenceException($"{name} is missing a Graphics value field.");
        if (resolutionPrevButton == null || resolutionNextButton == null
            || fullScreenPrevButton == null || fullScreenNextButton == null)
            throw new MissingReferenceException($"{name} is missing a Graphics step button.");
        if (saveButton == null || loadButton == null)
            throw new MissingReferenceException($"{name} is missing a Save/Load button.");
        if (saveSlotScrollArea == null || slotRowTemplate == null || slotIconTemplate == null
            || slotNameTemplate == null || slotMetaTemplate == null || slotDeleteTemplate == null
            || slotRenameField == null)
            throw new MissingReferenceException($"{name} is missing a save-slot template.");
        if (controlsScrollArea == null || bindingRowTemplate == null
            || bindingHeaderTemplate == null || bindingActionTemplate == null
            || bindingKeyBadgeTemplate == null || bindingKeyTemplate == null)
            throw new MissingReferenceException($"{name} is missing a binding template.");

        slotRowTemplate.gameObject.SetActive(false);
        slotIconTemplate.gameObject.SetActive(false);
        slotNameTemplate.gameObject.SetActive(false);
        slotMetaTemplate.gameObject.SetActive(false);
        slotDeleteTemplate.gameObject.SetActive(false);
        // slotRenameField is NOT a cloned template: it is the live inline editor whose visibility is
        // owned by BeginRename/CancelRename. It must not be force-hidden here (Render runs every
        // frame, which would kill the editor the instant it opens).
        bindingRowTemplate.gameObject.SetActive(false);
        bindingHeaderTemplate.gameObject.SetActive(false);
        bindingActionTemplate.gameObject.SetActive(false);
        bindingKeyBadgeTemplate.gameObject.SetActive(false);
        bindingKeyTemplate.gameObject.SetActive(false);
    }
}
