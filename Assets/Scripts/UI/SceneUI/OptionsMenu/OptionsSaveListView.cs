using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays and edits the save list in the Options menu.
/// </summary>
public sealed class OptionsSaveListView : MonoBehaviour, IContentInitializable
{
    private static readonly Color _accentColor = new Color(0.373f, 0.659f, 0.925f);
    private static readonly Color _metaColor = new Color(0.573f, 0.635f, 0.706f);
    private static readonly Color _textColor = new Color(0.875f, 0.910f, 0.941f);

    [SerializeField]
    private Button _saveButton;

    [SerializeField]
    private Button _loadButton;

    [SerializeField]
    private RawImage _saveDisabledImage;

    [SerializeField]
    private RawImage _loadDisabledImage;

    [SerializeField]
    private ScrollAreaView _scrollArea;

    [SerializeField]
    private Image _rowTemplate;

    [SerializeField]
    private RawImage _iconTemplate;

    [SerializeField]
    private TextMeshProUGUI _nameTemplate;

    [SerializeField]
    private TextMeshProUGUI _metaTemplate;

    [SerializeField]
    private Button _deleteTemplate;

    [SerializeField]
    private TMP_InputField _renameField;

    [SerializeField]
    private Sprite _rowIdleSprite;

    [SerializeField]
    private string _rowIdleSpriteAddress;

    [SerializeField]
    private Sprite _rowActiveSprite;

    [SerializeField]
    private string _rowActiveSpriteAddress;

    private readonly List<Image> _rowImages = new List<Image>();
    private readonly List<RawImage> _iconImages = new List<RawImage>();
    private readonly List<TextMeshProUGUI> _nameFields = new List<TextMeshProUGUI>();
    private readonly List<TextMeshProUGUI> _metaFields = new List<TextMeshProUGUI>();
    private readonly List<Button> _deleteButtons = new List<Button>();
    private readonly Dictionary<Button, float> _lastClickTimes = new Dictionary<Button, float>();
    private readonly List<int> _rowTops = new List<int>();
    private readonly List<OptionsSaveSlot> _slots = new List<OptionsSaveSlot>();

    private int _rowWidth;
    private int _rowHeight;
    private int _renameRow = -1;
    private bool _suppressRenameCommit;
    private bool _pendingRenameFocus;

    /// <summary>
    /// Raised when the Save command is requested.
    /// </summary>
    public event Action SaveRequested;

    /// <summary>
    /// Raised when the Load command is requested.
    /// </summary>
    public event Action LoadRequested;

    /// <summary>
    /// Raised when a save row is selected.
    /// </summary>
    public event Action<int> SlotSelected;

    /// <summary>
    /// Raised when a save name edit completes.
    /// </summary>
    public event Action<int, string, bool> SlotRenamed;

    /// <summary>
    /// Raised when a save row requests deletion.
    /// </summary>
    public event Action<int> SlotDeleteRequested;

    /// <summary>
    /// Raised when save-name text editing starts or stops.
    /// </summary>
    public event Action<bool> RenameEditingChanged;

    /// <summary>
    /// Loads content-backed row sprites.
    /// </summary>
    /// <param name="contentAssets">The active content asset source.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        Vector4 border = new Vector4(7f, 7f, 7f, 7f);
        _rowIdleSprite = ContentBindings.RequireSprite(
            contentAssets,
            _rowIdleSpriteAddress,
            border
        );
        _rowActiveSprite = ContentBindings.RequireSprite(
            contentAssets,
            _rowActiveSpriteAddress,
            border
        );
    }

    /// <summary>
    /// Verifies authored references and binds semantic control events.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        AlignRenameInput();
        _saveButton.onClick.AddListener(HandleSaveRequested);
        _loadButton.onClick.AddListener(HandleLoadRequested);
        _renameField.onEndEdit.AddListener(HandleRenameEndEdit);
        _renameField.onSubmit.AddListener(HandleRenameSubmitted);
    }

    /// <summary>
    /// Applies deferred text-field focus after the layout pass.
    /// </summary>
    private void Update()
    {
        UpdateFocus();
    }

    /// <summary>
    /// Removes listeners installed by this view.
    /// </summary>
    private void OnDestroy()
    {
        _saveButton.onClick.RemoveListener(HandleSaveRequested);
        _loadButton.onClick.RemoveListener(HandleLoadRequested);
        _renameField.onEndEdit.RemoveListener(HandleRenameEndEdit);
        _renameField.onSubmit.RemoveListener(HandleRenameSubmitted);
    }

    /// <summary>
    /// Renders the current save slots, selection, and available save actions.
    /// </summary>
    /// <param name="data">The current Options menu render state.</param>
    public void Render(OptionsMenuRenderData data)
    {
        int selected = data.SelectedSlot;
        bool existingSelected =
            selected >= 0
            && selected < data.SaveSlots.Count
            && !data.SaveSlots[selected].IsCreateNew;
        bool canSave = data.CanSave && existingSelected;
        bool canLoad = data.CanLoad && existingSelected;
        _saveButton.interactable = canSave;
        _loadButton.interactable = canLoad;
        SetButtonDisabledVisual(_saveButton, _saveDisabledImage, canSave);
        SetButtonDisabledVisual(_loadButton, _loadDisabledImage, canLoad);

        RectInt rowRect = UILayout.GetSourceRect(_rowTemplate.rectTransform);
        RectInt iconRect = UILayout.GetSourceRect(_iconTemplate.rectTransform);
        RectInt nameRect = UILayout.GetSourceRect(_nameTemplate.rectTransform);
        RectInt metaRect = UILayout.GetSourceRect(_metaTemplate.rectTransform);
        _rowWidth = rowRect.width;
        _rowHeight = rowRect.height;
        _slots.Clear();
        _rowTops.Clear();
        const int gap = 6;
        int contentHeight = 0;
        for (int index = 0; index < data.SaveSlots.Count; index++)
        {
            OptionsSaveSlot slot = data.SaveSlots[index];
            int top = contentHeight;
            _slots.Add(slot);
            _rowTops.Add(top);

            Image row = GetRow(index);
            UILayout.SetSourceRect(
                row.rectTransform,
                rowRect.x,
                top,
                rowRect.width,
                rowRect.height
            );
            row.sprite = index == selected ? _rowActiveSprite : _rowIdleSprite;
            row.color = Color.white;

            RawImage icon = GetIcon(index);
            icon.texture = slot.FactionIcon;
            icon.enabled = !slot.IsCreateNew && slot.FactionIcon != null;
            RectInt fittedIcon = FitPreservingAspect(slot.FactionIcon, iconRect);
            UILayout.SetSourceRect(
                icon.rectTransform,
                fittedIcon.x,
                top + fittedIcon.y,
                fittedIcon.width,
                fittedIcon.height
            );

            TextMeshProUGUI name = GetField(_nameFields, _nameTemplate, "SlotName", index);
            RectInt renderedNameRect = slot.IsCreateNew
                ? new RectInt(0, top, rowRect.width, rowRect.height)
                : new RectInt(nameRect.x, top + nameRect.y, nameRect.width, nameRect.height);
            UILayout.SetTemplateText(
                name,
                _nameTemplate,
                slot.Name,
                slot.IsCreateNew ? _accentColor : _textColor,
                renderedNameRect
            );
            if (slot.IsCreateNew)
                name.alignment = TextAlignmentOptions.Midline;

            UILayout.SetTemplateText(
                GetField(_metaFields, _metaTemplate, "SlotDate", index),
                _metaTemplate,
                slot.IsCreateNew ? string.Empty : slot.Date,
                _metaColor,
                new RectInt(metaRect.x, top + metaRect.y, metaRect.width, metaRect.height)
            );

            Button delete = GetDelete(index);
            delete.gameObject.SetActive(!slot.IsCreateNew);
            UILayout.SetSourceRect(
                (RectTransform)delete.transform,
                rowRect.width - 24,
                top + (rowRect.height - 16) / 2,
                16,
                16
            );
            contentHeight += rowRect.height + gap;
        }

        _scrollArea.SetContentHeight(contentHeight, rowRect.height + gap, false);
        HideFrom(_rowImages, data.SaveSlots.Count);
        HideFrom(_iconImages, data.SaveSlots.Count);
        HideFrom(_nameFields, data.SaveSlots.Count);
        HideFrom(_metaFields, data.SaveSlots.Count);
        HideFrom(_deleteButtons, data.SaveSlots.Count);
    }

    /// <summary>
    /// Applies deferred focus to an active rename field after layout completes.
    /// </summary>
    private void UpdateFocus()
    {
        if (!_pendingRenameFocus)
            return;

        _pendingRenameFocus = false;
        if (!_renameField.gameObject.activeInHierarchy)
            return;
        _renameField.Select();
        _renameField.ActivateInputField();
        _renameField.caretPosition = _renameField.text?.Length ?? 0;
        _renameField.selectionAnchorPosition = _renameField.caretPosition;
        _renameField.selectionFocusPosition = _renameField.caretPosition;
        _renameField.ForceLabelUpdate();
    }

    /// <summary>
    /// Cancels the current rename without committing its text.
    /// </summary>
    public void CancelRename()
    {
        if (_renameRow < 0)
            return;

        _renameRow = -1;
        _suppressRenameCommit = true;
        _pendingRenameFocus = false;
        _renameField.gameObject.SetActive(false);
        RenameEditingChanged?.Invoke(false);
    }

    /// <summary>
    /// Aligns the rename text and caret within the authored input rectangle.
    /// </summary>
    internal void AlignRenameInput()
    {
        if (_renameField.textComponent is TextMeshProUGUI text)
        {
            text.alignment = TextAlignmentOptions.MidlineLeft;
            StretchRenameText(text.rectTransform);
        }
        if (_renameField.placeholder is TextMeshProUGUI placeholder)
        {
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            StretchRenameText(placeholder.rectTransform);
        }

        _renameField.textViewport = _renameField.transform as RectTransform;
        _renameField.customCaretColor = true;
        _renameField.caretColor = Color.white;
        _renameField.caretWidth = 2;
        _renameField.caretBlinkRate = 0.85f;
        _renameField.characterLimit = SaveGameManager.MaxDisplayNameLength;
        _renameField.onFocusSelectAll = false;
        _renameField.ForceLabelUpdate();
    }

    /// <summary>
    /// Gets or creates the visual row for a save-list index.
    /// </summary>
    private Image GetRow(int index)
    {
        while (_rowImages.Count <= index)
        {
            int slotIndex = _rowImages.Count;
            Image image = UnityEngine.Object.Instantiate(_rowTemplate, _scrollArea.ContentRoot);
            image.name = $"SlotRow{slotIndex}";
            image.gameObject.SetActive(true);
            if (image.TryGetComponent(out Button rowButton))
                rowButton.onClick.AddListener(() => HandleSlotClick(rowButton, slotIndex));
            _rowImages.Add(image);
        }

        Image row = _rowImages[index];
        row.gameObject.SetActive(true);
        return row;
    }

    /// <summary>
    /// Gets or creates the faction icon for a save-list index.
    /// </summary>
    private RawImage GetIcon(int index)
    {
        while (_iconImages.Count <= index)
        {
            RawImage icon = UnityEngine.Object.Instantiate(_iconTemplate, _scrollArea.ContentRoot);
            icon.name = $"SlotIcon{_iconImages.Count}";
            icon.gameObject.SetActive(true);
            _iconImages.Add(icon);
        }

        RawImage iconImage = _iconImages[index];
        iconImage.gameObject.SetActive(true);
        return iconImage;
    }

    /// <summary>
    /// Gets or creates a text field from an authored row template.
    /// </summary>
    private TextMeshProUGUI GetField(
        List<TextMeshProUGUI> fields,
        TextMeshProUGUI template,
        string prefix,
        int index
    )
    {
        while (fields.Count <= index)
        {
            TextMeshProUGUI field = UnityEngine.Object.Instantiate(
                template,
                _scrollArea.ContentRoot
            );
            field.name = $"{prefix}{fields.Count}";
            field.gameObject.SetActive(true);
            fields.Add(field);
        }

        TextMeshProUGUI textField = fields[index];
        textField.gameObject.SetActive(true);
        return textField;
    }

    /// <summary>
    /// Gets or creates the delete button for a save-list index.
    /// </summary>
    private Button GetDelete(int index)
    {
        while (_deleteButtons.Count <= index)
        {
            int slotIndex = _deleteButtons.Count;
            Button button = UnityEngine.Object.Instantiate(
                _deleteTemplate,
                _scrollArea.ContentRoot
            );
            button.name = $"SlotDelete{slotIndex}";
            button.gameObject.SetActive(true);
            button.transform.SetAsLastSibling();
            button.onClick.AddListener(() => SlotDeleteRequested?.Invoke(slotIndex));
            _deleteButtons.Add(button);
        }

        Button deleteButton = _deleteButtons[index];
        deleteButton.gameObject.SetActive(true);
        return deleteButton;
    }

    /// <summary>
    /// Selects a clicked slot and starts renaming on a qualifying double-click.
    /// </summary>
    private void HandleSlotClick(Button button, int index)
    {
        if (index < 0 || index >= _slots.Count)
            return;

        bool existing = !_slots[index].IsCreateNew;
        float now = Time.unscaledTime;
        if (existing && _lastClickTimes.TryGetValue(button, out float last) && now - last < 0.35f)
        {
            _lastClickTimes[button] = 0f;
            CancelRename();
            SlotSelected?.Invoke(index);
            BeginRename(index);
            return;
        }

        _lastClickTimes[button] = now;
        CancelRename();
        SlotSelected?.Invoke(index);
        if (!existing)
            BeginRename(index);
    }

    /// <summary>
    /// Positions and opens the rename field for a save-list index.
    /// </summary>
    private void BeginRename(int index)
    {
        if (index < 0 || index >= _slots.Count)
            return;

        _renameRow = index;
        _suppressRenameCommit = false;
        int width = _slots[index].IsCreateNew ? _rowWidth - 44 : _rowWidth - 62;
        const int fieldHeight = 18;
        int fieldTop = _rowTops[index] + (_rowHeight - fieldHeight) / 2;
        UILayout.SetSourceRect(
            (RectTransform)_renameField.transform,
            32,
            fieldTop,
            width,
            fieldHeight
        );
        AlignRenameInput();
        _renameField.transform.SetAsLastSibling();
        _renameField.gameObject.SetActive(true);
        _renameField.SetTextWithoutNotify(
            _slots[index].IsCreateNew ? string.Empty : _slots[index].Name
        );
        _pendingRenameFocus = true;
        RenameEditingChanged?.Invoke(true);
    }

    /// <summary>
    /// Completes editing without treating focus loss as an explicit submission.
    /// </summary>
    private void HandleRenameEndEdit(string value) => CompleteRename(value, false);

    /// <summary>
    /// Completes editing as an explicit keyboard submission.
    /// </summary>
    private void HandleRenameSubmitted(string value) => CompleteRename(value, true);

    /// <summary>
    /// Closes the rename field and forwards a committed name to the controller.
    /// </summary>
    private void CompleteRename(string value, bool submitted)
    {
        if (_renameRow < 0)
        {
            _suppressRenameCommit = false;
            return;
        }

        int row = _renameRow;
        _renameRow = -1;
        _pendingRenameFocus = false;
        RenameEditingChanged?.Invoke(false);
        _renameField.gameObject.SetActive(false);
        if (_suppressRenameCommit)
        {
            _suppressRenameCommit = false;
            return;
        }
        SlotRenamed?.Invoke(row, value, submitted);
    }

    /// <summary>
    /// Forwards the authored Save button request.
    /// </summary>
    private void HandleSaveRequested()
    {
        SaveRequested?.Invoke();
    }

    /// <summary>
    /// Forwards the authored Load button request.
    /// </summary>
    private void HandleLoadRequested()
    {
        LoadRequested?.Invoke();
    }

    /// <summary>
    /// Stretches rename text within the input field while retaining horizontal padding.
    /// </summary>
    private static void StretchRenameText(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(6f, 0f);
        rect.offsetMax = new Vector2(-6f, 0f);
    }

    /// <summary>
    /// Switches a button between its enabled control and disabled artwork.
    /// </summary>
    private static void SetButtonDisabledVisual(Button button, RawImage disabledImage, bool enabled)
    {
        if (disabledImage != null)
            disabledImage.gameObject.SetActive(!enabled);
        if (button?.targetGraphic != null)
            button.targetGraphic.enabled = enabled;
    }

    /// <summary>
    /// Fits a texture within a source rectangle without changing its aspect ratio.
    /// </summary>
    private static RectInt FitPreservingAspect(Texture texture, RectInt box)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0)
            return box;

        float aspect = (float)texture.width / texture.height;
        int width =
            aspect >= (float)box.width / box.height
                ? box.width
                : Mathf.RoundToInt(box.height * aspect);
        int height =
            aspect >= (float)box.width / box.height
                ? Mathf.RoundToInt(box.width / aspect)
                : box.height;
        return new RectInt(
            box.x + (box.width - width) / 2,
            box.y + (box.height - height) / 2,
            width,
            height
        );
    }

    /// <summary>
    /// Hides pooled row components after the last currently rendered item.
    /// </summary>
    private static void HideFrom<T>(List<T> items, int firstHiddenIndex)
        where T : Component
    {
        for (int index = firstHiddenIndex; index < items.Count; index++)
            items[index].gameObject.SetActive(false);
    }

    /// <summary>
    /// Verifies that the generated save-list subview contains every required reference.
    /// </summary>
    private void VerifyReferences()
    {
        if (_saveButton == null || _loadButton == null)
            throw new MissingReferenceException($"{name} is missing a save command button.");
        if (_saveDisabledImage == null || _loadDisabledImage == null)
            throw new MissingReferenceException($"{name} is missing a disabled command image.");
        if (
            _scrollArea == null
            || _rowTemplate == null
            || _iconTemplate == null
            || _nameTemplate == null
            || _metaTemplate == null
            || _deleteTemplate == null
            || _renameField == null
        )
            throw new MissingReferenceException($"{name} is missing a save-row reference.");
        if (_rowIdleSprite == null || _rowActiveSprite == null)
            throw new MissingReferenceException($"{name} is missing a row sprite.");
        if (
            string.IsNullOrWhiteSpace(_rowIdleSpriteAddress)
            || string.IsNullOrWhiteSpace(_rowActiveSpriteAddress)
        )
            throw new MissingReferenceException($"{name} is missing a row sprite address.");

        _rowTemplate.gameObject.SetActive(false);
        _iconTemplate.gameObject.SetActive(false);
        _nameTemplate.gameObject.SetActive(false);
        _metaTemplate.gameObject.SetActive(false);
        _deleteTemplate.gameObject.SetActive(false);
        _renameField.gameObject.SetActive(false);
    }
}
