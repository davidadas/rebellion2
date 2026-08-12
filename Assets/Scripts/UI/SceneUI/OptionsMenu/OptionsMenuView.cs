using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the Options menu.
/// </summary>
public sealed class OptionsMenuView : MonoBehaviour
{
    // Colors.
    private static readonly Color _activeTabColor = new Color(0.875f, 0.910f, 0.941f);
    private static readonly Color _inactiveTabColor = new Color(0.573f, 0.635f, 0.706f);
    private static readonly Color _accentColor = new Color(0.373f, 0.659f, 0.925f);
    private static readonly Color _metaColor = new Color(0.573f, 0.635f, 0.706f);
    private static readonly Color _textColor = new Color(0.875f, 0.910f, 0.941f);
    private static readonly Color _badgeColor = new Color(0.204f, 0.243f, 0.302f);
    private static readonly Color _badgeListeningColor = new Color(0.373f, 0.659f, 0.925f);

    // Binding Rows.
    private readonly List<TextMeshProUGUI> _bindingHeaderFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> _bindingActionFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> _bindingKeyFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> _bindingSecondaryKeyFields = new List<TextMeshProUGUI>();
    private readonly List<Image> _bindingRowImages = new List<Image>();
    private readonly List<Image> _bindingBadgeImages = new List<Image>();
    private readonly List<Image> _bindingSecondaryBadgeImages = new List<Image>();
    private readonly HashSet<Button> _wiredBadges = new HashSet<Button>();

    [Header("Frame")]
    [SerializeField]
    private RawImage _backgroundImage;

    [SerializeField]
    private TextMeshProUGUI _headerTextField;

    [SerializeField]
    private TextMeshProUGUI _pageTitleTextField;

    [Header("Navigation")]
    [SerializeField]
    private Sprite _rowIdleSprite;

    [SerializeField]
    private Sprite _rowActiveSprite;

    [SerializeField]
    private Button[] _tabButtons = Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI[] _tabLabelFields = Array.Empty<TextMeshProUGUI>();

    [SerializeField]
    private GameObject _graphicsPage;

    [SerializeField]
    private GameObject _audioPage;

    [SerializeField]
    private GameObject _saveLoadPage;

    [SerializeField]
    private GameObject _controlsPage;

    [Header("Footer")]
    [SerializeField]
    private Button _backToGameButton;

    [SerializeField]
    private Button _mainMenuButton;

    [SerializeField]
    private Button _quitButton;

    [Header("Settings actions")]
    [SerializeField]
    private GameObject _settingsActions;

    [SerializeField]
    private Button _applyButton;

    [SerializeField]
    private Button _defaultsButton;

    [SerializeField]
    private ConfirmationDialogView _confirmDialog;

    [Header("Graphics page")]
    [SerializeField]
    private OptionsToggleRowView[] _tacticalRows = Array.Empty<OptionsToggleRowView>();

    [SerializeField]
    private TextMeshProUGUI _resolutionValueField;

    [SerializeField]
    private Button _resolutionPrevButton;

    [SerializeField]
    private Button _resolutionNextButton;

    [SerializeField]
    private TextMeshProUGUI _fullScreenValueField;

    [SerializeField]
    private Button _fullScreenPrevButton;

    [SerializeField]
    private Button _fullScreenNextButton;

    [Header("Audio page")]
    [SerializeField]
    private NormalizedSliderView[] _volumeSliders = Array.Empty<NormalizedSliderView>();

    [SerializeField]
    private TextMeshProUGUI[] _volumeValueFields = Array.Empty<TextMeshProUGUI>();

    [Header("Save/Load page")]
    [SerializeField]
    private Button _saveButton;

    [SerializeField]
    private Button _loadButton;

    [SerializeField]
    private RawImage _saveDisabledImage;

    [SerializeField]
    private RawImage _loadDisabledImage;

    [SerializeField]
    private ScrollAreaView _saveSlotScrollArea;

    [SerializeField]
    private Image _slotRowTemplate;

    [SerializeField]
    private RawImage _slotIconTemplate;

    [SerializeField]
    private TextMeshProUGUI _slotNameTemplate;

    [SerializeField]
    private TextMeshProUGUI _slotMetaTemplate;

    [SerializeField]
    private Button _slotDeleteTemplate;

    [SerializeField]
    private TMP_InputField _slotRenameField;

    [Header("Controls page")]
    [SerializeField]
    private ScrollAreaView _controlsScrollArea;

    [SerializeField]
    private Image _bindingRowTemplate;

    [SerializeField]
    private TextMeshProUGUI _bindingHeaderTemplate;

    [SerializeField]
    private TextMeshProUGUI _bindingActionTemplate;

    [SerializeField]
    private Image _bindingKeyBadgeTemplate;

    [SerializeField]
    private TextMeshProUGUI _bindingKeyTemplate;

    // Menu State.
    private OptionsSaveListPresenter _saveListPresenter;
    private bool _bound;

    // Events.
    public event Action<OptionsMenuTab> TabSelected;
    public event Action ResumeRequested;
    public event Action SaveRequested;
    public event Action LoadRequested;
    public event Action<int> SlotSelected;
    public event Action<int, string, bool> SlotRenamed;
    public event Action<int> SlotDeleteRequested;
    public event Action<bool> RenameEditingChanged;
    public event Action ApplyRequested;
    public event Action DefaultsRequested;
    public event Action ConfirmAccepted;
    public event Action ConfirmDeclined;
    public event Action<int, bool> RebindRequested;
    public event Action MainMenuRequested;
    public event Action QuitRequested;
    public event Action<UserTacticalOption> TacticalToggleRequested;
    public event Action<int> ResolutionStepRequested;
    public event Action<int> FullScreenStepRequested;
    public event Action<int, float> VolumeChanged;
    public event Action<OptionsMenuView> Destroyed;

    /// <summary>
    /// Checks the menu references and adds its listeners.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        _saveListPresenter = new OptionsSaveListPresenter(
            _saveButton,
            _loadButton,
            _saveDisabledImage,
            _loadDisabledImage,
            _saveSlotScrollArea,
            _slotRowTemplate,
            _slotIconTemplate,
            _slotNameTemplate,
            _slotMetaTemplate,
            _slotDeleteTemplate,
            _slotRenameField,
            _rowIdleSprite,
            _rowActiveSprite,
            index => SlotSelected?.Invoke(index),
            (index, value, submitted) => SlotRenamed?.Invoke(index, value, submitted),
            index => SlotDeleteRequested?.Invoke(index),
            editing => RenameEditingChanged?.Invoke(editing)
        );
        BindControls();
    }

    /// <summary>
    /// Removes the menu listeners.
    /// </summary>
    private void OnDestroy()
    {
        UnbindControls();
        _saveListPresenter?.Dispose();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Displays the Options menu data.
    /// </summary>
    /// <param name="data">The Options menu data.</param>
    public void Render(OptionsMenuRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        UILayout.SetSourcePosition(transform as RectTransform, data.X, data.Y);
        UILayout.SetTextContent(_headerTextField, "OPTIONS");
        UILayout.SetTextContent(_pageTitleTextField, GetTabTitle(data.ActiveTab));
        if (data.ActiveTab != OptionsMenuTab.SaveLoad)
            _saveListPresenter.CancelRename();
        RenderTabs(data.ActiveTab);
        RenderFooter(data);
        switch (data.ActiveTab)
        {
            case OptionsMenuTab.Graphics:
                RenderGraphicsPage(data);
                break;
            case OptionsMenuTab.Audio:
                RenderAudioPage(data);
                break;
            case OptionsMenuTab.SaveLoad:
                RenderSaveLoadPage(data);
                break;
            case OptionsMenuTab.Controls:
                RenderControlsPage(data);
                break;
        }
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Highlights the active tab and shows only its page.
    /// </summary>
    /// <param name="activeTab">The currently visible page.</param>
    private void RenderTabs(OptionsMenuTab activeTab)
    {
        for (int i = 0; i < _tabLabelFields.Length; i++)
        {
            bool active = i == (int)activeTab;
            if (_tabLabelFields[i] != null)
                _tabLabelFields[i].color = active ? _activeTabColor : _inactiveTabColor;
            if (i < _tabButtons.Length && _tabButtons[i]?.targetGraphic is Image tabImage)
            {
                tabImage.sprite = active ? _rowActiveSprite : _rowIdleSprite;
            }
        }

        SetPageActive(_graphicsPage, activeTab == OptionsMenuTab.Graphics);
        SetPageActive(_audioPage, activeTab == OptionsMenuTab.Audio);
        SetPageActive(_saveLoadPage, activeTab == OptionsMenuTab.SaveLoad);
        SetPageActive(_controlsPage, activeTab == OptionsMenuTab.Controls);
        _settingsActions?.SetActive(activeTab != OptionsMenuTab.SaveLoad);
    }

    /// <summary>
    /// Applies footer action availability.
    /// </summary>
    /// <param name="data">The Options menu data.</param>
    private void RenderFooter(OptionsMenuRenderData data)
    {
        bool layoutChanged =
            _backToGameButton.gameObject.activeSelf != data.CanReturnToGame
            || _mainMenuButton.gameObject.activeSelf != data.CanReturnToMainMenu;
        _backToGameButton.gameObject.SetActive(data.CanReturnToGame);
        _mainMenuButton.gameObject.SetActive(data.CanReturnToMainMenu);
        if (layoutChanged && _backToGameButton.transform.parent is RectTransform navigationRoot)
            LayoutRebuilder.ForceRebuildLayoutImmediate(navigationRoot);
    }

    /// <summary>
    /// Applies detail-toggle and display values to the Graphics page.
    /// </summary>
    /// <param name="data">The Options menu data.</param>
    private void RenderGraphicsPage(OptionsMenuRenderData data)
    {
        foreach (OptionsToggleRowView row in _tacticalRows)
        {
            if (row == null)
                continue;

            bool enabled = data.TacticalStates.TryGetValue(row.Option, out bool value) && value;
            row.Render(enabled);
        }

        UILayout.SetTextContent(_resolutionValueField, data.ResolutionLabel);
        UILayout.SetTextContent(_fullScreenValueField, data.FullScreenLabel);
    }

    /// <summary>
    /// Applies normalized volumes to the Audio page sliders and labels.
    /// </summary>
    /// <param name="data">The Options menu data.</param>
    private void RenderAudioPage(OptionsMenuRenderData data)
    {
        for (int i = 0; i < _volumeSliders.Length; i++)
        {
            float value = i < data.Volumes.Count ? data.Volumes[i] : 0f;
            _volumeSliders[i]?.Render(value);
            if (i < _volumeValueFields.Length && _volumeValueFields[i] != null)
                UILayout.SetTextContent(
                    _volumeValueFields[i],
                    Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString()
                );
        }
    }

    /// <summary>
    /// Rebuilds the save-slot list and applies action availability.
    /// </summary>
    /// <param name="data">The Options menu data.</param>
    private void RenderSaveLoadPage(OptionsMenuRenderData data)
    {
        _saveListPresenter.Render(data);
    }

    /// <summary>
    /// Rebuilds the read-only key-binding list: accent group headers followed by
    /// backed rows with badged key captions.
    /// </summary>
    /// <param name="data">The Options menu data.</param>
    private void RenderControlsPage(OptionsMenuRenderData data)
    {
        RectInt rowTemplate = UILayout.GetSourceRect(_bindingRowTemplate.rectTransform);
        RectInt headerTemplate = UILayout.GetSourceRect(_bindingHeaderTemplate.rectTransform);
        RectInt actionTemplate = UILayout.GetSourceRect(_bindingActionTemplate.rectTransform);
        RectInt badgeTemplate = UILayout.GetSourceRect(_bindingKeyBadgeTemplate.rectTransform);
        RectInt keyTemplate = UILayout.GetSourceRect(_bindingKeyTemplate.rectTransform);
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
                        _bindingHeaderFields,
                        _bindingHeaderTemplate,
                        "BindingHeader",
                        headerCount++
                    ),
                    _bindingHeaderTemplate,
                    binding.Action,
                    _accentColor,
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
                _bindingRowImages,
                _bindingRowTemplate,
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
                    _bindingActionFields,
                    _bindingActionTemplate,
                    "BindingAction",
                    rowCount
                ),
                _bindingActionTemplate,
                binding.Action,
                _metaColor,
                new RectInt(
                    actionTemplate.x,
                    contentHeight + actionTemplate.y,
                    180,
                    actionTemplate.height
                )
            );
            RenderBindingKey(
                _bindingBadgeImages,
                _bindingKeyFields,
                "BindingPrimary",
                rowCount,
                i,
                badgeTemplate.x - 67,
                contentHeight,
                binding.Primary,
                badgeTemplate,
                keyTemplate,
                false,
                i == data.ListeningRow && !data.ListeningSecondary
            );
            RenderBindingKey(
                _bindingSecondaryBadgeImages,
                _bindingSecondaryKeyFields,
                "BindingSecondary",
                rowCount,
                i,
                badgeTemplate.x,
                contentHeight,
                binding.Secondary,
                badgeTemplate,
                keyTemplate,
                true,
                i == data.ListeningRow && data.ListeningSecondary
            );
            rowCount++;
            contentHeight += rowTemplate.height + 5;
        }

        _controlsScrollArea.SetContentHeight(contentHeight, rowTemplate.height + 5, false);
        HideFieldsFrom(_bindingHeaderFields, headerCount);
        HideFieldsFrom(_bindingActionFields, rowCount);
        HideFieldsFrom(_bindingKeyFields, rowCount);
        HideFieldsFrom(_bindingSecondaryKeyFields, rowCount);
        HideImagesFrom(_bindingRowImages, rowCount);
        HideImagesFrom(_bindingBadgeImages, rowCount);
        HideImagesFrom(_bindingSecondaryBadgeImages, rowCount);
    }

    /// <summary>
    /// Displays a key binding.
    /// </summary>
    /// <param name="badges">The badge cache for this column.</param>
    /// <param name="keys">The key-field cache for this column.</param>
    /// <param name="prefix">The instance-name prefix.</param>
    /// <param name="index">The row index.</param>
    /// <param name="bindingIndex">The source binding index, including group headers.</param>
    /// <param name="x">The left position.</param>
    /// <param name="top">The top position.</param>
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
        int bindingIndex,
        int x,
        int top,
        string text,
        RectInt badgeTemplate,
        RectInt keyTemplate,
        bool isSecondary,
        bool listening
    )
    {
        Image badge = GetBindingImage(badges, _bindingKeyBadgeTemplate, prefix + "Badge", index);
        if (badge.TryGetComponent(out Button badgeButton) && _wiredBadges.Add(badgeButton))
        {
            badgeButton.onClick.AddListener(() => HandleBadgeClick(bindingIndex, isSecondary));
        }

        // Binding Key.
        badge.gameObject.SetActive(true);
        badge.color = listening ? _badgeListeningColor : _badgeColor;
        UILayout.SetSourceRect(
            badge.rectTransform,
            x,
            top + badgeTemplate.y,
            badgeTemplate.width,
            badgeTemplate.height
        );
        UILayout.SetTemplateText(
            GetBindingField(keys, _bindingKeyTemplate, prefix + "Key", index),
            _bindingKeyTemplate,
            listening ? "..." : text,
            _textColor,
            new RectInt(x, top + keyTemplate.y, keyTemplate.width, keyTemplate.height)
        );
    }

    /// <summary>
    /// Raises a rebind request when a binding badge is clicked.
    /// </summary>
    /// <param name="row">The binding row index.</param>
    /// <param name="secondary">Whether the secondary column was clicked.</param>
    private void HandleBadgeClick(int row, bool secondary)
    {
        RebindRequested?.Invoke(row, secondary);
    }

    /// <summary>
    /// Gets or creates a reusable controls-page image cloned from a template.
    /// </summary>
    /// <param name="images">The image cache.</param>
    /// <param name="template">The template image.</param>
    /// <param name="namePrefix">The instance-name prefix.</param>
    /// <param name="index">The required row index.</param>
    /// <returns>The reusable image.</returns>
    private Image GetBindingImage(List<Image> images, Image template, string namePrefix, int index)
    {
        while (images.Count <= index)
        {
            Image image = Instantiate(template, _controlsScrollArea.ContentRoot);
            image.name = $"{namePrefix}{images.Count}";
            image.gameObject.SetActive(true);
            images.Add(image);
        }

        return images[index];
    }

    /// <summary>
    /// Aligns the save name field and caret.
    /// </summary>
    internal void AlignRenameInput()
    {
        _saveListPresenter.AlignRenameInput();
    }

    /// <summary>
    /// Focuses the save name field after a row click.
    /// </summary>
    private void Update()
    {
        _saveListPresenter?.UpdateFocus();
    }

    /// <summary>
    /// Gets or creates a reusable binding field cloned from a template.
    /// </summary>
    /// <param name="fields">The field cache.</param>
    /// <param name="template">The template field.</param>
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
            TextMeshProUGUI field = Instantiate(template, _controlsScrollArea.ContentRoot);
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
    /// Shows or hides a menu page.
    /// </summary>
    /// <param name="page">The menu page.</param>
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
    /// Adds the Options menu listeners.
    /// </summary>
    private void BindControls()
    {
        if (_bound)
            return;

        for (int i = 0; i < _tabButtons.Length; i++)
        {
            OptionsMenuTab tab = (OptionsMenuTab)i;
            if (_tabButtons[i] != null)
                _tabButtons[i].onClick.AddListener(() => TabSelected?.Invoke(tab));
        }

        _backToGameButton.onClick.AddListener(() => ResumeRequested?.Invoke());
        _mainMenuButton.onClick.AddListener(() => MainMenuRequested?.Invoke());
        _quitButton.onClick.AddListener(() => QuitRequested?.Invoke());
        _saveButton.onClick.AddListener(() => SaveRequested?.Invoke());
        _loadButton.onClick.AddListener(() => LoadRequested?.Invoke());
        _applyButton.onClick.AddListener(() => ApplyRequested?.Invoke());
        _defaultsButton.onClick.AddListener(() => DefaultsRequested?.Invoke());
        _confirmDialog.Confirmed += HandleConfirmAccepted;
        _confirmDialog.Canceled += HandleConfirmDeclined;

        foreach (OptionsToggleRowView row in _tacticalRows)
        {
            if (row != null)
                row.ToggleRequested += HandleTacticalToggle;
        }

        _resolutionPrevButton.onClick.AddListener(() => ResolutionStepRequested?.Invoke(-1));
        _resolutionNextButton.onClick.AddListener(() => ResolutionStepRequested?.Invoke(1));
        _fullScreenPrevButton.onClick.AddListener(() => FullScreenStepRequested?.Invoke(-1));
        _fullScreenNextButton.onClick.AddListener(() => FullScreenStepRequested?.Invoke(1));

        for (int i = 0; i < _volumeSliders.Length; i++)
        {
            int channel = i;
            if (_volumeSliders[i] != null)
                _volumeSliders[i].ValueChanged += value =>
                {
                    if (channel < _volumeValueFields.Length && _volumeValueFields[channel] != null)
                        UILayout.SetTextContent(
                            _volumeValueFields[channel],
                            Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString()
                        );
                    VolumeChanged?.Invoke(channel, value);
                };
        }

        _bound = true;
    }

    /// <summary>
    /// Removes tactical-row listeners that outlive the Unity button lifetime.
    /// </summary>
    private void UnbindControls()
    {
        foreach (OptionsToggleRowView row in _tacticalRows)
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
        _confirmDialog.Show(message);
    }

    /// <summary>
    /// Hides the confirmation prompt.
    /// </summary>
    public void HideConfirm()
    {
        _confirmDialog?.Hide();
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
    /// Checks the Options menu references.
    /// </summary>
    private void VerifyReferences()
    {
        if (_backgroundImage == null)
            throw new MissingReferenceException($"{name}/BackgroundImage is missing.");
        if (_headerTextField == null || _pageTitleTextField == null)
            throw new MissingReferenceException($"{name} is missing a title field.");
        if (_rowIdleSprite == null || _rowActiveSprite == null)
            throw new MissingReferenceException($"{name} is missing a row sprite.");
        if (_tabButtons.Length != 4 || _tabLabelFields.Length != 4)
            throw new MissingReferenceException($"{name} expects four tabs.");
        if (
            _graphicsPage == null
            || _audioPage == null
            || _saveLoadPage == null
            || _controlsPage == null
        )
            throw new MissingReferenceException($"{name} is missing a page container.");
        if (_backToGameButton == null || _mainMenuButton == null || _quitButton == null)
            throw new MissingReferenceException($"{name} is missing a footer button.");
        if (_settingsActions == null || _applyButton == null || _defaultsButton == null)
            throw new MissingReferenceException($"{name} is missing a settings-action button.");
        if (_confirmDialog == null)
            throw new MissingReferenceException($"{name} is missing the confirm dialog.");
        if (_resolutionValueField == null || _fullScreenValueField == null)
            throw new MissingReferenceException($"{name} is missing a Graphics value field.");
        if (
            _resolutionPrevButton == null
            || _resolutionNextButton == null
            || _fullScreenPrevButton == null
            || _fullScreenNextButton == null
        )
            throw new MissingReferenceException($"{name} is missing a Graphics step button.");
        if (_saveButton == null || _loadButton == null)
            throw new MissingReferenceException($"{name} is missing a Save/Load button.");
        if (
            _saveSlotScrollArea == null
            || _slotRowTemplate == null
            || _slotIconTemplate == null
            || _slotNameTemplate == null
            || _slotMetaTemplate == null
            || _slotDeleteTemplate == null
            || _slotRenameField == null
        )
            throw new MissingReferenceException($"{name} is missing a save-slot template.");
        if (
            _controlsScrollArea == null
            || _bindingRowTemplate == null
            || _bindingHeaderTemplate == null
            || _bindingActionTemplate == null
            || _bindingKeyBadgeTemplate == null
            || _bindingKeyTemplate == null
        )
            throw new MissingReferenceException($"{name} is missing a binding template.");

        _slotRowTemplate.gameObject.SetActive(false);
        _slotIconTemplate.gameObject.SetActive(false);
        _slotNameTemplate.gameObject.SetActive(false);
        _slotMetaTemplate.gameObject.SetActive(false);
        _slotDeleteTemplate.gameObject.SetActive(false);
        // The rename field is controlled by the save list.
        _bindingRowTemplate.gameObject.SetActive(false);
        _bindingHeaderTemplate.gameObject.SetActive(false);
        _bindingActionTemplate.gameObject.SetActive(false);
        _bindingKeyBadgeTemplate.gameObject.SetActive(false);
        _bindingKeyTemplate.gameObject.SetActive(false);
    }
}
