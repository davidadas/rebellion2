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

    private readonly List<TextMeshProUGUI> bindingHeaderFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> bindingActionFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> bindingKeyFields = new List<TextMeshProUGUI>();
    private readonly List<Image> bindingRowImages = new List<Image>();
    private readonly List<Image> bindingBadgeImages = new List<Image>();
    private readonly List<Image> slotRowImages = new List<Image>();
    private readonly List<TextMeshProUGUI> slotNameFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> slotMetaFields = new List<TextMeshProUGUI>();

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
    private ScrollAreaView saveSlotScrollArea;

    [SerializeField]
    private Image slotRowTemplate;

    [SerializeField]
    private TextMeshProUGUI slotNameTemplate;

    [SerializeField]
    private TextMeshProUGUI slotMetaTemplate;

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

    /// <summary>Occurs when the player selects a page.</summary>
    public event Action<OptionsMenuTab> TabSelected;

    /// <summary>Occurs when the player asks to resume the game.</summary>
    public event Action ResumeRequested;

    /// <summary>Occurs when the player asks to save.</summary>
    public event Action SaveRequested;

    /// <summary>Occurs when the player asks to load.</summary>
    public event Action LoadRequested;

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
        BindControls();
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
                tabImage.color = Color.white;
            }
        }

        SetPageActive(graphicsPage, activeTab == OptionsMenuTab.Graphics);
        SetPageActive(audioPage, activeTab == OptionsMenuTab.Audio);
        SetPageActive(saveLoadPage, activeTab == OptionsMenuTab.SaveLoad);
        SetPageActive(controlsPage, activeTab == OptionsMenuTab.Controls);
    }

    /// <summary>
    /// Applies footer action availability.
    /// </summary>
    /// <param name="data">The presentation snapshot.</param>
    private void RenderFooter(OptionsMenuRenderData data)
    {
        backToGameButton.interactable = data.CanReturnToGame;
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
        saveButton.interactable = data.CanSave;
        loadButton.interactable = data.CanLoad;

        RectInt rowTemplate = UILayout.GetSourceRect(slotRowTemplate.rectTransform);
        RectInt nameTemplate = UILayout.GetSourceRect(slotNameTemplate.rectTransform);
        RectInt metaTemplate = UILayout.GetSourceRect(slotMetaTemplate.rectTransform);
        int gap = 6;
        int contentHeight = 0;
        for (int i = 0; i < data.SaveSlots.Count; i++)
        {
            OptionsSaveSlot slot = data.SaveSlots[i];
            int top = contentHeight;
            Image rowImage = GetSlotRow(i);
            UILayout.SetSourceRect(
                rowImage.rectTransform,
                rowTemplate.x,
                top,
                rowTemplate.width,
                rowTemplate.height
            );
            rowImage.sprite = i == data.SelectedSlot ? rowActiveSprite : rowIdleSprite;
            rowImage.color = Color.white;

            UILayout.SetTemplateText(
                GetSlotField(slotNameFields, slotNameTemplate, "SlotName", i),
                slotNameTemplate,
                slot.Name,
                slot.Filled ? TextColor : MetaColor,
                new RectInt(nameTemplate.x, top + nameTemplate.y, nameTemplate.width, nameTemplate.height)
            );
            UILayout.SetTemplateText(
                GetSlotField(slotMetaFields, slotMetaTemplate, "SlotMeta", i),
                slotMetaTemplate,
                slot.Detail,
                MetaColor,
                new RectInt(metaTemplate.x, top + metaTemplate.y, metaTemplate.width, metaTemplate.height)
            );
            contentHeight += rowTemplate.height + gap;
        }

        saveSlotScrollArea.SetContentHeight(contentHeight, rowTemplate.height + gap, true);
        HideImagesFrom(slotRowImages, data.SaveSlots.Count);
        HideFieldsFrom(slotNameFields, data.SaveSlots.Count);
        HideFieldsFrom(slotMetaFields, data.SaveSlots.Count);
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
            if (string.IsNullOrEmpty(binding.Keys))
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
                    actionTemplate.width,
                    actionTemplate.height
                )
            );
            Image badgeImage = GetBindingImage(
                bindingBadgeImages,
                bindingKeyBadgeTemplate,
                "BindingBadge",
                rowCount
            );
            UILayout.SetSourceRect(
                badgeImage.rectTransform,
                badgeTemplate.x,
                contentHeight + badgeTemplate.y,
                badgeTemplate.width,
                badgeTemplate.height
            );
            UILayout.SetTemplateText(
                GetBindingField(bindingKeyFields, bindingKeyTemplate, "BindingKey", rowCount),
                bindingKeyTemplate,
                binding.Keys,
                TextColor,
                new RectInt(
                    keyTemplate.x,
                    contentHeight + keyTemplate.y,
                    keyTemplate.width,
                    keyTemplate.height
                )
            );
            rowCount++;
            contentHeight += rowTemplate.height + 5;
        }

        controlsScrollArea.SetContentHeight(contentHeight, rowTemplate.height + 5, true);
        HideFieldsFrom(bindingHeaderFields, headerCount);
        HideFieldsFrom(bindingActionFields, rowCount);
        HideFieldsFrom(bindingKeyFields, rowCount);
        HideImagesFrom(bindingRowImages, rowCount);
        HideImagesFrom(bindingBadgeImages, rowCount);
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
    /// Gets or creates a reusable save-slot background image cloned from the template.
    /// </summary>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable row image.</returns>
    private Image GetSlotRow(int index)
    {
        while (slotRowImages.Count <= index)
        {
            Image image = Instantiate(slotRowTemplate, saveSlotScrollArea.ContentRoot);
            image.name = $"SlotRow{slotRowImages.Count}";
            image.gameObject.SetActive(true);
            slotRowImages.Add(image);
        }

        return slotRowImages[index];
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
                volumeSliders[i].ValueChanged += value => VolumeChanged?.Invoke(channel, value);
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
        if (resolutionValueField == null || fullScreenValueField == null)
            throw new MissingReferenceException($"{name} is missing a Graphics value field.");
        if (resolutionPrevButton == null || resolutionNextButton == null
            || fullScreenPrevButton == null || fullScreenNextButton == null)
            throw new MissingReferenceException($"{name} is missing a Graphics step button.");
        if (saveButton == null || loadButton == null)
            throw new MissingReferenceException($"{name} is missing a Save/Load button.");
        if (saveSlotScrollArea == null || slotRowTemplate == null || slotNameTemplate == null
            || slotMetaTemplate == null)
            throw new MissingReferenceException($"{name} is missing a save-slot template.");
        if (controlsScrollArea == null || bindingRowTemplate == null
            || bindingHeaderTemplate == null || bindingActionTemplate == null
            || bindingKeyBadgeTemplate == null || bindingKeyTemplate == null)
            throw new MissingReferenceException($"{name} is missing a binding template.");

        slotRowTemplate.gameObject.SetActive(false);
        slotNameTemplate.gameObject.SetActive(false);
        slotMetaTemplate.gameObject.SetActive(false);
        bindingRowTemplate.gameObject.SetActive(false);
        bindingHeaderTemplate.gameObject.SetActive(false);
        bindingActionTemplate.gameObject.SetActive(false);
        bindingKeyBadgeTemplate.gameObject.SetActive(false);
        bindingKeyTemplate.gameObject.SetActive(false);
    }
}
