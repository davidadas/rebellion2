using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders immutable Finder snapshots and emits semantic user gestures.
/// </summary>
public sealed class FinderWindowView : MonoBehaviour
{
    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private RawImage overlayFrameImage;

    [SerializeField]
    private RawImage buttonStripImage;

    [SerializeField]
    private RawImage[] upperButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] upperButtonPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] upperButtons = Array.Empty<Button>();

    [SerializeField]
    private RawImage[] twoButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] twoButtonPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] twoButtons = Array.Empty<Button>();

    [SerializeField]
    private RawImage[] fourButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] fourButtonPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] fourButtons = Array.Empty<Button>();

    [SerializeField]
    private TextMeshProUGUI titleTextField;

    [SerializeField]
    private TextMeshProUGUI labelTextField;

    [SerializeField]
    private TMP_InputField labelInputField;

    [SerializeField]
    private RawImage[] tabImageSlots = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] tabPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] tabButtons = Array.Empty<Button>();

    [SerializeField]
    private RectTransform[] defaultTabSlotTemplates = Array.Empty<RectTransform>();

    [SerializeField]
    private RectTransform[] compactTabSlotTemplates = Array.Empty<RectTransform>();

    [SerializeField]
    private TextMeshProUGUI tabTitleTextField;

    [SerializeField]
    private TextMeshProUGUI defaultTabTitleTextTemplate;

    [SerializeField]
    private TextMeshProUGUI compactTabTitleTextTemplate;

    [SerializeField]
    private ScrollAreaView rowsScrollArea;

    [SerializeField]
    private RectTransform defaultRowsClipTemplate;

    [SerializeField]
    private RectTransform troopRowsClipTemplate;

    [SerializeField]
    private RectTransform personnelRowsClipTemplate;

    [SerializeField]
    private RectTransform personnelPanelRowsClipTemplate;

    [SerializeField]
    private RectTransform rowsScrollPaddingTemplate;

    [SerializeField]
    private FinderWindowRowView rowTemplate;

    [SerializeField]
    private FinderWindowRowView personnelRowTemplate;

    [SerializeField]
    private FinderWindowRowView personnelPanelRowTemplate;

    [SerializeField]
    private int troopRowPitch;

    [SerializeField]
    private RectTransform defaultScrollbarTemplate;

    [SerializeField]
    private RectTransform compactScrollbarTemplate;

    [SerializeField]
    private RectTransform personnelScrollbarTemplate;

    [SerializeField]
    private RectTransform personnelPanelScrollbarTemplate;

    private readonly List<Button> boundButtons = new List<Button>();
    private readonly List<UnityAction> boundButtonListeners = new List<UnityAction>();
    private readonly List<string> renderedRowIds = new List<string>();

    private FinderWindowRenderData lastData;
    private bool renderedAnyRows;
    private FinderMode renderedMode;
    private bool renderedPanel;
    private int renderedActiveTab = -1;
    private FinderWindowRowView activeRowTemplate;
    private SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> rowsList;
    private Texture defaultBackgroundTexture;
    private Texture defaultOverlayFrameTexture;
    private Texture defaultButtonStripTexture;
    private Texture[] defaultUpperButtonTextures = Array.Empty<Texture>();
    private Texture[] defaultTwoButtonTextures = Array.Empty<Texture>();
    private Texture[] defaultFourButtonTextures = Array.Empty<Texture>();
    private Texture[] defaultTabTextures = Array.Empty<Texture>();

    /// <summary>
    /// Raised when an authored command button is pressed.
    /// </summary>
    public event Action<FinderWindowView, FinderWindowCommand> CommandRequested;

    /// <summary>
    /// Raised when a result row requests the strategy context menu.
    /// </summary>
    public event Action<FinderWindowView, string, PointerEventData> ContextRequested;

    /// <summary>
    /// Raised when the view is destroyed so its controller can release subscriptions.
    /// </summary>
    public event Action<FinderWindowView> Destroyed;

    /// <summary>
    /// Raised when the owning strategy window should receive focus.
    /// </summary>
    public event Action<FinderWindowView> FocusRequested;

    /// <summary>
    /// Raised when a visible result row is activated.
    /// </summary>
    public event Action<FinderWindowView, string> RowActivated;

    /// <summary>
    /// Raised when a visible result row is selected.
    /// </summary>
    public event Action<FinderWindowView, string> RowSelected;

    /// <summary>
    /// Raised when the result-name filter changes.
    /// </summary>
    public event Action<FinderWindowView, string> SearchTextChanged;

    /// <summary>
    /// Raised when one authored tab is selected.
    /// </summary>
    public event Action<FinderWindowView, int> TabSelected;

    /// <summary>
    /// Validates authored references, captures fallback artwork, and binds controls.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        HideAuthoredTemplates();
        CaptureDefaultTextures();
        BindControls();
    }

    /// <summary>
    /// Releases local control bindings and notifies the feature controller.
    /// </summary>
    private void OnDestroy()
    {
        UnbindControls();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies one complete immutable Finder presentation snapshot.
    /// </summary>
    /// <param name="data">The presentation snapshot to render.</param>
    public void Render(FinderWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        lastData = data;
        UILayout.SetSourcePosition(transform as RectTransform, data.Frame.X, data.Frame.Y);
        RenderFrame(data.Frame);
        UILayout.SetTextContent(titleTextField, data.Title);
        RenderSearchInput(data.Label, data.SearchText);
        RenderTabs(data);
        RenderTabTitle(data);
        ApplyRowsScrollAreaLayout(data.Mode, data.Panel);
        RenderRows(data);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Binds every authored control to a local semantic handler.
    /// </summary>
    private void BindControls()
    {
        BindTabButtons();
        BindDialogButtons(upperButtons);
        BindDialogButtons(twoButtons);
        BindDialogButtons(fourButtons);
        labelInputField.onValueChanged.AddListener(HandleSearchTextChanged);
    }

    /// <summary>
    /// Removes every listener owned by this view.
    /// </summary>
    private void UnbindControls()
    {
        labelInputField?.onValueChanged.RemoveListener(HandleSearchTextChanged);
        for (int i = 0; i < boundButtons.Count; i++)
        {
            if (boundButtons[i] != null)
                boundButtons[i].onClick.RemoveListener(boundButtonListeners[i]);
        }

        boundButtons.Clear();
        boundButtonListeners.Clear();
    }

    /// <summary>
    /// Binds each authored tab button to its stable slot index.
    /// </summary>
    private void BindTabButtons()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int tab = i;
            BindButton(tabButtons[i], () => HandleTabSelected(tab));
        }
    }

    /// <summary>
    /// Binds authored dialog buttons to semantic command lookup.
    /// </summary>
    /// <param name="buttons">The authored buttons in slot order.</param>
    private void BindDialogButtons(IReadOnlyList<Button> buttons)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Count; i++)
        {
            Button button = buttons[i];
            BindButton(button, () => HandleDialogButtonClicked(button));
        }
    }

    /// <summary>
    /// Adds one tracked button listener for deterministic teardown.
    /// </summary>
    /// <param name="button">The authored button.</param>
    /// <param name="listener">The listener owned by this view.</param>
    private void BindButton(Button button, UnityAction listener)
    {
        if (button == null || listener == null)
            return;

        button.onClick.AddListener(listener);
        boundButtons.Add(button);
        boundButtonListeners.Add(listener);
    }

    /// <summary>
    /// Emits a normalized result-name filter gesture.
    /// </summary>
    /// <param name="value">The new input-field value.</param>
    private void HandleSearchTextChanged(string value)
    {
        SearchTextChanged?.Invoke(this, value ?? string.Empty);
    }

    /// <summary>
    /// Emits one authored tab-selection gesture.
    /// </summary>
    /// <param name="tab">The selected authored tab index.</param>
    private void HandleTabSelected(int tab)
    {
        FocusRequested?.Invoke(this);
        TabSelected?.Invoke(this, tab);
    }

    /// <summary>
    /// Emits the semantic command represented by one active dialog-button slot.
    /// </summary>
    /// <param name="button">The clicked authored button.</param>
    private void HandleDialogButtonClicked(Button button)
    {
        FocusRequested?.Invoke(this);
        FinderWindowCommand command = GetButtonCommand(button);
        if (command == FinderWindowCommand.None)
            return;

        CommandRequested?.Invoke(this, command);
    }

    /// <summary>
    /// Resolves the semantic command assigned to an active dialog button.
    /// </summary>
    /// <param name="button">The clicked authored button.</param>
    /// <returns>The assigned semantic command.</returns>
    private FinderWindowCommand GetButtonCommand(Button button)
    {
        if (lastData?.Frame == null)
            return FinderWindowCommand.None;

        Button[] buttons = GetDialogButtonComponents(lastData.Frame);
        int count = Mathf.Min(buttons.Length, lastData.Frame.DialogButtons.Count);
        for (int i = 0; i < count; i++)
        {
            if (buttons[i] == button)
                return lastData.Frame.DialogButtons[i].Command;
        }

        return FinderWindowCommand.None;
    }

    /// <summary>
    /// Emits one visible-row selection gesture.
    /// </summary>
    /// <param name="row">The selected row view.</param>
    /// <param name="eventData">The pointer or navigation event.</param>
    private void HandleRowSelected(FinderWindowRowView row, PointerEventData eventData)
    {
        if (row == null)
            return;

        FocusRequested?.Invoke(this);
        RowSelected?.Invoke(this, row.RowId);
    }

    /// <summary>
    /// Emits one visible-row activation gesture.
    /// </summary>
    /// <param name="row">The activated row view.</param>
    /// <param name="eventData">The pointer or navigation event.</param>
    private void HandleRowActivated(FinderWindowRowView row, PointerEventData eventData)
    {
        if (row == null)
            return;

        FocusRequested?.Invoke(this);
        RowActivated?.Invoke(this, row.RowId);
    }

    /// <summary>
    /// Forwards a row context request without changing selection.
    /// </summary>
    /// <param name="row">The requesting row view.</param>
    /// <param name="eventData">The pointer event.</param>
    private void HandleRowContextRequested(FinderWindowRowView row, PointerEventData eventData)
    {
        if (row != null)
            ContextRequested?.Invoke(this, row.RowId, eventData);
    }

    /// <summary>
    /// Reports whether row keyboard navigation is available for this rendered window.
    /// </summary>
    /// <returns>True when the Finder window owns focus.</returns>
    private bool CanNavigateRows()
    {
        return lastData?.Frame?.ActiveWindow == true;
    }

    /// <summary>
    /// Renders frame artwork and the active command-button layout.
    /// </summary>
    /// <param name="frame">The resolved frame presentation.</param>
    private void RenderFrame(FinderWindowFrameRenderData frame)
    {
        UILayout.SetImageTexture(
            backgroundImage,
            frame.BackgroundTexture ?? defaultBackgroundTexture
        );
        UILayout.SetImageTexture(
            overlayFrameImage,
            frame.OverlayFrameTexture ?? defaultOverlayFrameTexture
        );
        RenderButtonStrip(frame);

        HideDialogButtonSlots(upperButtonImages, upperButtons);
        HideDialogButtonSlots(twoButtonImages, twoButtons);
        HideDialogButtonSlots(fourButtonImages, fourButtons);

        RawImage[] images = GetDialogButtonImages(frame);
        RawImagePressVisual[] pressVisuals = GetDialogButtonPressVisuals(frame);
        Button[] buttons = GetDialogButtonComponents(frame);
        Texture[] defaults = GetDialogButtonDefaultTextures(frame);
        if (
            frame.DialogButtons.Count > images.Length
            || frame.DialogButtons.Count > pressVisuals.Length
            || frame.DialogButtons.Count > buttons.Length
        )
        {
            throw new MissingReferenceException(
                $"{name} cannot render {frame.DialogButtons.Count} Finder command buttons."
            );
        }

        for (int i = 0; i < frame.DialogButtons.Count; i++)
            RenderDialogButton(
                images[i],
                pressVisuals[i],
                buttons[i],
                defaults[i],
                frame.DialogButtons[i]
            );
    }

    /// <summary>
    /// Renders or hides the faction command-button strip.
    /// </summary>
    /// <param name="frame">The resolved frame presentation.</param>
    private void RenderButtonStrip(FinderWindowFrameRenderData frame)
    {
        Texture texture = frame.UseUpperButtonLayout
            ? null
            : frame.ButtonStripTexture ?? defaultButtonStripTexture;
        if (texture == null)
        {
            UILayout.SetImageTexture(buttonStripImage, null);
            return;
        }

        RectInt authoredRect = UILayout.GetSourceRect(buttonStripImage.rectTransform);
        int textureWidth = UILayout.GetTextureSourceWidth(texture);
        int width = textureWidth > 0 ? textureWidth : authoredRect.width;
        UILayout.SetImage(buttonStripImage, texture, frame.Width - width, authoredRect.y);
    }

    /// <summary>
    /// Applies one resolved command button to one authored slot.
    /// </summary>
    /// <param name="image">The authored button image.</param>
    /// <param name="pressVisual">The authored pressed-state visual.</param>
    /// <param name="button">The authored button control.</param>
    /// <param name="defaultTexture">The authored fallback texture.</param>
    /// <param name="data">The resolved command presentation.</param>
    private static void RenderDialogButton(
        RawImage image,
        RawImagePressVisual pressVisual,
        Button button,
        Texture defaultTexture,
        FinderWindowDialogButtonRenderData data
    )
    {
        Texture texture = data.Texture ?? defaultTexture;
        if (texture == null || data.Command == FinderWindowCommand.None)
        {
            image.gameObject.SetActive(false);
            button.interactable = false;
            return;
        }

        ApplyButtonLayout(image, data.SourceRect, texture);
        pressVisual.SetInteractiveTextures(texture, data.PressedTexture ?? texture);
        button.interactable = true;
    }

    /// <summary>
    /// Applies configured source bounds or preserves authored right alignment.
    /// </summary>
    /// <param name="image">The authored button image.</param>
    /// <param name="sourceRect">The optional configured source bounds.</param>
    /// <param name="texture">The resolved button texture.</param>
    private static void ApplyButtonLayout(RawImage image, RectInt? sourceRect, Texture texture)
    {
        if (!sourceRect.HasValue)
        {
            UILayout.SetRightAlignedImageSize(image, texture);
            return;
        }

        RectInt rect = sourceRect.Value;
        UILayout.SetSourceRect(image.rectTransform, rect.x, rect.y, rect.width, rect.height);
    }

    /// <summary>
    /// Hides one inactive authored command-button layout.
    /// </summary>
    /// <param name="images">The authored button images.</param>
    /// <param name="buttons">The authored button controls.</param>
    private static void HideDialogButtonSlots(
        IReadOnlyList<RawImage> images,
        IReadOnlyList<Button> buttons
    )
    {
        if (images != null)
        {
            for (int i = 0; i < images.Count; i++)
            {
                if (images[i] != null)
                    images[i].gameObject.SetActive(false);
            }
        }

        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Count; i++)
        {
            if (buttons[i] != null)
                buttons[i].interactable = false;
        }
    }

    /// <summary>
    /// Applies the current search label and local input value.
    /// </summary>
    /// <param name="label">The displayed search label.</param>
    /// <param name="value">The local search value.</param>
    private void RenderSearchInput(string label, string value)
    {
        UILayout.SetTextContent(labelTextField, label);
        labelInputField.gameObject.SetActive(true);
        labelInputField.SetTextWithoutNotify(value ?? string.Empty);
    }

    /// <summary>
    /// Renders projected tab artwork into the authored tab slots.
    /// </summary>
    /// <param name="data">The current Finder presentation.</param>
    private void RenderTabs(FinderWindowRenderData data)
    {
        if (data.Tabs.Count > tabImageSlots.Length || data.Tabs.Count > tabButtons.Length)
        {
            throw new MissingReferenceException(
                $"{name} cannot render {data.Tabs.Count} Finder tabs."
            );
        }

        for (int i = 0; i < data.Tabs.Count; i++)
        {
            RawImage image = tabImageSlots[i];
            ApplyTabLayout(image, i, data.Mode);
            Texture texture = data.Tabs[i].Texture ?? defaultTabTextures[i];
            tabPressVisuals[i]
                .SetInteractiveTextures(texture, data.Tabs[i].PressedTexture ?? texture);
            tabButtons[i].interactable = texture != null;
        }

        for (int i = data.Tabs.Count; i < tabImageSlots.Length; i++)
        {
            tabImageSlots[i].gameObject.SetActive(false);
            tabButtons[i].interactable = false;
        }
    }

    /// <summary>
    /// Renders the active tab title using the authored mode-specific typography template.
    /// </summary>
    /// <param name="data">The current Finder presentation.</param>
    private void RenderTabTitle(FinderWindowRenderData data)
    {
        TextMeshProUGUI template = GetTabTitleTemplate(data.Mode, data.Panel);
        UILayout.SetTemplateText(
            tabTitleTextField,
            template,
            data.ActiveTabText,
            template.color,
            UILayout.GetSourceRect(template.rectTransform)
        );
    }

    /// <summary>
    /// Renders projected rows through the shared reusable list component.
    /// </summary>
    /// <param name="data">The current Finder presentation.</param>
    private void RenderRows(FinderWindowRenderData data)
    {
        bool resetScroll = RowsChanged(data);
        int rowPitch = GetRowPitch(data.Mode, data.Panel);
        FinderWindowRowView template = GetRowTemplate(data.Mode, data.Panel);
        EnsureRowsList(template)
            .Render(
                data.Rows,
                GetScrollContentHeight(data.Rows.Count, rowPitch),
                rowPitch,
                resetScroll,
                rowPitch,
                (rowView, row, index) => rowView.Render(index, row, rowPitch),
                (row, _) => row.Selected
            );

        renderedAnyRows = true;
        renderedMode = data.Mode;
        renderedPanel = data.Panel;
        renderedActiveTab = data.ActiveTab;
        renderedRowIds.Clear();
        for (int i = 0; i < data.Rows.Count; i++)
            renderedRowIds.Add(data.Rows[i].RowId);
    }

    /// <summary>
    /// Gets or creates the reusable list bound to the current authored row template.
    /// </summary>
    /// <param name="template">The active authored row template.</param>
    /// <returns>The reusable Finder row list.</returns>
    private SelectableListView<FinderWindowRowView, FinderWindowRowRenderData> EnsureRowsList(
        FinderWindowRowView template
    )
    {
        if (activeRowTemplate == template && rowsList != null)
            return rowsList;

        rowsList?.Clear();
        activeRowTemplate = template;
        rowsList = new SelectableListView<FinderWindowRowView, FinderWindowRowRenderData>(
            rowsScrollArea,
            template,
            "FinderRow",
            HandleRowSelected,
            HandleRowActivated,
            HandleRowContextRequested,
            CanNavigateRows,
            transform
        );
        return rowsList;
    }

    /// <summary>
    /// Reports whether row identity or result category changed since the prior render.
    /// </summary>
    /// <param name="data">The current Finder presentation.</param>
    /// <returns>True when scrolling should reset to the first row.</returns>
    private bool RowsChanged(FinderWindowRenderData data)
    {
        if (
            !renderedAnyRows
            || renderedMode != data.Mode
            || renderedPanel != data.Panel
            || renderedActiveTab != data.ActiveTab
            || renderedRowIds.Count != data.Rows.Count
        )
        {
            return true;
        }

        for (int i = 0; i < data.Rows.Count; i++)
        {
            if (!string.Equals(renderedRowIds[i], data.Rows[i].RowId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Applies mode-specific viewport and scrollbar bounds from authored templates.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    private void ApplyRowsScrollAreaLayout(FinderMode mode, bool panel)
    {
        RectInt rowsClip = UILayout.GetSourceRect(GetRowsClipTemplate(mode, panel));
        RectInt scrollbar = UILayout.GetSourceRect(GetScrollbarTemplate(mode, panel));
        RectInt bounds = Union(rowsClip, scrollbar);
        UILayout.SetSourceRect(
            rowsScrollArea.transform as RectTransform,
            bounds.x,
            bounds.y,
            bounds.width,
            bounds.height
        );
        rowsScrollArea.SetLayout(
            new Vector2(rowsClip.x - bounds.x, rowsClip.y - bounds.y),
            new Vector2(rowsClip.width, rowsClip.height),
            new Vector2(scrollbar.x - bounds.x, scrollbar.y - bounds.y),
            new Vector2(scrollbar.width, scrollbar.height)
        );
    }

    /// <summary>
    /// Calculates the complete scroll content height for a rendered result count.
    /// </summary>
    /// <param name="rowCount">The rendered result count.</param>
    /// <param name="rowPitch">The active authored row pitch.</param>
    /// <returns>The required source-space content height.</returns>
    private int GetScrollContentHeight(int rowCount, int rowPitch)
    {
        return UILayout.GetSourceRect(rowsScrollPaddingTemplate).height + rowCount * rowPitch;
    }

    /// <summary>
    /// Gets the authored result-row pitch for one Finder category and panel.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>The source-space row pitch.</returns>
    private int GetRowPitch(FinderMode mode, bool panel)
    {
        if (mode == FinderMode.Troops)
            return troopRowPitch;

        return UILayout
            .GetSourceRect(GetRowTemplate(mode, panel).transform as RectTransform)
            .height;
    }

    /// <summary>
    /// Gets the authored tab-title template for one Finder category and panel.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>The authored tab-title template.</returns>
    private TextMeshProUGUI GetTabTitleTemplate(FinderMode mode, bool panel)
    {
        return mode == FinderMode.Personnel && !panel
            ? compactTabTitleTextTemplate
            : defaultTabTitleTextTemplate;
    }

    /// <summary>
    /// Gets the authored rows-viewport template for one Finder category and panel.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>The authored viewport template.</returns>
    private RectTransform GetRowsClipTemplate(FinderMode mode, bool panel)
    {
        if (mode == FinderMode.Troops)
            return troopRowsClipTemplate;
        if (mode == FinderMode.Personnel)
            return panel ? personnelPanelRowsClipTemplate : personnelRowsClipTemplate;

        return defaultRowsClipTemplate;
    }

    /// <summary>
    /// Gets the authored row template for one Finder category and panel.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>The authored row template.</returns>
    private FinderWindowRowView GetRowTemplate(FinderMode mode, bool panel)
    {
        if (mode != FinderMode.Personnel)
            return rowTemplate;

        return panel ? personnelPanelRowTemplate : personnelRowTemplate;
    }

    /// <summary>
    /// Gets the authored scrollbar template for one Finder category and panel.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>The authored scrollbar template.</returns>
    private RectTransform GetScrollbarTemplate(FinderMode mode, bool panel)
    {
        if (mode == FinderMode.Troops)
            return compactScrollbarTemplate;
        if (mode == FinderMode.Personnel)
            return panel ? personnelPanelScrollbarTemplate : personnelScrollbarTemplate;

        return defaultScrollbarTemplate;
    }

    /// <summary>
    /// Gets the authored tab-layout templates for one Finder category.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <returns>The authored tab-layout templates.</returns>
    private RectTransform[] GetTabLayoutTemplates(FinderMode mode)
    {
        return mode is FinderMode.Troops or FinderMode.Personnel
            ? compactTabSlotTemplates
            : defaultTabSlotTemplates;
    }

    /// <summary>
    /// Applies an authored repeated tab position to one tab image.
    /// </summary>
    /// <param name="image">The tab image to position.</param>
    /// <param name="index">The authored tab slot index.</param>
    /// <param name="mode">The active Finder category.</param>
    private void ApplyTabLayout(RawImage image, int index, FinderMode mode)
    {
        RectTransform[] templates = GetTabLayoutTemplates(mode);
        RectInt first = UILayout.GetSourceRect(templates[0]);
        RectInt rect = first;
        if (index > 0)
        {
            RectInt second = UILayout.GetSourceRect(templates[1]);
            int tabPitch = second.x - first.x;
            rect = new RectInt(first.x + index * tabPitch, first.y, first.width, first.height);
        }

        UILayout.SetSourceRect(image.rectTransform, rect.x, rect.y, rect.width, rect.height);
    }

    /// <summary>
    /// Gets the active authored dialog-button images.
    /// </summary>
    /// <param name="frame">The current frame presentation.</param>
    /// <returns>The active image slots.</returns>
    private RawImage[] GetDialogButtonImages(FinderWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return upperButtonImages;

        return frame.DialogButtons.Count > twoButtonImages.Length
            ? fourButtonImages
            : twoButtonImages;
    }

    /// <summary>
    /// Gets the active authored dialog-button pressed-state visuals.
    /// </summary>
    /// <param name="frame">The current frame presentation.</param>
    /// <returns>The active pressed-state visual slots.</returns>
    private RawImagePressVisual[] GetDialogButtonPressVisuals(FinderWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return upperButtonPressVisuals;

        return frame.DialogButtons.Count > twoButtonPressVisuals.Length
            ? fourButtonPressVisuals
            : twoButtonPressVisuals;
    }

    /// <summary>
    /// Gets the active authored dialog-button controls.
    /// </summary>
    /// <param name="frame">The current frame presentation.</param>
    /// <returns>The active button controls.</returns>
    private Button[] GetDialogButtonComponents(FinderWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return upperButtons;

        return frame.DialogButtons.Count > twoButtons.Length ? fourButtons : twoButtons;
    }

    /// <summary>
    /// Gets authored fallback textures for the active dialog-button layout.
    /// </summary>
    /// <param name="frame">The current frame presentation.</param>
    /// <returns>The authored fallback textures.</returns>
    private Texture[] GetDialogButtonDefaultTextures(FinderWindowFrameRenderData frame)
    {
        if (frame.UseUpperButtonLayout)
            return defaultUpperButtonTextures;

        return frame.DialogButtons.Count > defaultTwoButtonTextures.Length
            ? defaultFourButtonTextures
            : defaultTwoButtonTextures;
    }

    /// <summary>
    /// Captures authored textures used when optional theme assets are unavailable.
    /// </summary>
    private void CaptureDefaultTextures()
    {
        defaultBackgroundTexture = backgroundImage.texture;
        defaultOverlayFrameTexture = overlayFrameImage.texture;
        defaultButtonStripTexture = buttonStripImage.texture;
        defaultUpperButtonTextures = CaptureTextures(upperButtonImages);
        defaultTwoButtonTextures = CaptureTextures(twoButtonImages);
        defaultFourButtonTextures = CaptureTextures(fourButtonImages);
        defaultTabTextures = CaptureTextures(tabImageSlots);
    }

    /// <summary>
    /// Copies textures from an authored image array.
    /// </summary>
    /// <param name="images">The authored images.</param>
    /// <returns>The captured textures in slot order.</returns>
    private static Texture[] CaptureTextures(IReadOnlyList<RawImage> images)
    {
        if (images == null)
            return Array.Empty<Texture>();

        Texture[] textures = new Texture[images.Count];
        for (int i = 0; i < images.Count; i++)
            textures[i] = images[i]?.texture;

        return textures;
    }

    /// <summary>
    /// Keeps layout and row templates inactive during runtime presentation.
    /// </summary>
    private void HideAuthoredTemplates()
    {
        defaultTabTitleTextTemplate.gameObject.SetActive(false);
        compactTabTitleTextTemplate.gameObject.SetActive(false);
        defaultRowsClipTemplate.gameObject.SetActive(false);
        troopRowsClipTemplate.gameObject.SetActive(false);
        personnelRowsClipTemplate.gameObject.SetActive(false);
        personnelPanelRowsClipTemplate.gameObject.SetActive(false);
        rowsScrollPaddingTemplate.gameObject.SetActive(false);
        rowTemplate.gameObject.SetActive(false);
        personnelRowTemplate.gameObject.SetActive(false);
        personnelPanelRowTemplate.gameObject.SetActive(false);
        defaultScrollbarTemplate.gameObject.SetActive(false);
        compactScrollbarTemplate.gameObject.SetActive(false);
        personnelScrollbarTemplate.gameObject.SetActive(false);
        personnelPanelScrollbarTemplate.gameObject.SetActive(false);
    }

    /// <summary>
    /// Verifies every authored reference required by Finder presentation and input.
    /// </summary>
    private void VerifyReferences()
    {
        VerifyImage(backgroundImage, "BackgroundImage");
        VerifyImage(overlayFrameImage, "OverlayFrameImage");
        VerifyImage(buttonStripImage, "ButtonStripImage");
        VerifyButtonLayout("Upper", upperButtonImages, upperButtonPressVisuals, upperButtons);
        VerifyButtonLayout("Two", twoButtonImages, twoButtonPressVisuals, twoButtons);
        VerifyButtonLayout("Four", fourButtonImages, fourButtonPressVisuals, fourButtons);
        VerifyText(titleTextField, "TitleTextField");
        VerifyText(labelTextField, "LabelTextField");
        if (labelInputField == null || labelInputField.textComponent == null)
            throw new MissingReferenceException($"{name}/LabelInputField is incomplete.");
        VerifyButtonLayout("Tab", tabImageSlots, tabPressVisuals, tabButtons);
        VerifyTabLayoutTemplates("Default", defaultTabSlotTemplates);
        VerifyTabLayoutTemplates("Compact", compactTabSlotTemplates);
        VerifyText(tabTitleTextField, "TabTitleTextField");
        VerifyText(defaultTabTitleTextTemplate, "DefaultTabTitleTextTemplate");
        VerifyText(compactTabTitleTextTemplate, "CompactTabTitleTextTemplate");
        if (rowsScrollArea == null)
            throw new MissingReferenceException($"{name}/RowsScrollArea is missing.");
        VerifyRect(defaultRowsClipTemplate, "DefaultRowsClipTemplate");
        VerifyRect(troopRowsClipTemplate, "TroopRowsClipTemplate");
        VerifyRect(personnelRowsClipTemplate, "PersonnelRowsClipTemplate");
        VerifyRect(personnelPanelRowsClipTemplate, "PersonnelPanelRowsClipTemplate");
        VerifyRect(rowsScrollPaddingTemplate, "RowsScrollPaddingTemplate");
        VerifyRowTemplate(rowTemplate, "RowTemplate");
        VerifyRowTemplate(personnelRowTemplate, "PersonnelRowTemplate");
        VerifyRowTemplate(personnelPanelRowTemplate, "PersonnelPanelRowTemplate");
        if (troopRowPitch <= 0)
            throw new MissingReferenceException($"{name}/TroopRowPitch is missing.");
        VerifyRect(defaultScrollbarTemplate, "DefaultScrollbarTemplate");
        VerifyRect(compactScrollbarTemplate, "CompactScrollbarTemplate");
        VerifyRect(personnelScrollbarTemplate, "PersonnelScrollbarTemplate");
        VerifyRect(personnelPanelScrollbarTemplate, "PersonnelPanelScrollbarTemplate");
    }

    /// <summary>
    /// Verifies one authored image reference.
    /// </summary>
    /// <param name="image">The image to verify.</param>
    /// <param name="label">The authored field label.</param>
    private void VerifyImage(RawImage image, string label)
    {
        if (image == null)
            throw new MissingReferenceException($"{name}/{label} is missing.");
    }

    /// <summary>
    /// Verifies one authored text reference.
    /// </summary>
    /// <param name="text">The text to verify.</param>
    /// <param name="label">The authored field label.</param>
    private void VerifyText(TextMeshProUGUI text, string label)
    {
        if (text == null)
            throw new MissingReferenceException($"{name}/{label} is missing.");
    }

    /// <summary>
    /// Verifies one authored rectangle reference.
    /// </summary>
    /// <param name="rect">The rectangle to verify.</param>
    /// <param name="label">The authored field label.</param>
    private void VerifyRect(RectTransform rect, string label)
    {
        if (rect == null)
            throw new MissingReferenceException($"{name}/{label} is missing.");
    }

    /// <summary>
    /// Verifies one authored Finder row template.
    /// </summary>
    /// <param name="row">The row template to verify.</param>
    /// <param name="label">The authored field label.</param>
    private void VerifyRowTemplate(FinderWindowRowView row, string label)
    {
        if (row == null)
            throw new MissingReferenceException($"{name}/{label} is missing.");
    }

    /// <summary>
    /// Verifies one complete authored button layout.
    /// </summary>
    /// <param name="label">The authored layout label.</param>
    /// <param name="images">The authored button images.</param>
    /// <param name="pressVisuals">The authored pressed-state visuals.</param>
    /// <param name="buttons">The authored button controls.</param>
    private void VerifyButtonLayout(
        string label,
        IReadOnlyList<RawImage> images,
        IReadOnlyList<RawImagePressVisual> pressVisuals,
        IReadOnlyList<Button> buttons
    )
    {
        if (
            images == null
            || pressVisuals == null
            || buttons == null
            || images.Count == 0
            || images.Count != pressVisuals.Count
            || images.Count != buttons.Count
        )
            throw new MissingReferenceException($"{name}/{label} button layout is incomplete.");

        for (int i = 0; i < images.Count; i++)
        {
            if (images[i] == null || pressVisuals[i] == null || buttons[i] == null)
            {
                throw new MissingReferenceException(
                    $"{name}/{label} button slot {i} is incomplete."
                );
            }
        }
    }

    /// <summary>
    /// Verifies authored repeated-tab geometry templates.
    /// </summary>
    /// <param name="label">The authored layout label.</param>
    /// <param name="templates">The authored tab templates.</param>
    private void VerifyTabLayoutTemplates(string label, IReadOnlyList<RectTransform> templates)
    {
        if (templates == null || templates.Count < 2)
            throw new MissingReferenceException($"{name}/{label} tab templates are missing.");

        for (int i = 0; i < templates.Count; i++)
        {
            if (templates[i] == null)
                throw new MissingReferenceException($"{name}/{label} tab template {i} is missing.");
        }
    }

    /// <summary>
    /// Returns the bounding rectangle containing two source-space rectangles.
    /// </summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns>The union of both rectangles.</returns>
    private static RectInt Union(RectInt first, RectInt second)
    {
        int minX = Mathf.Min(first.x, second.x);
        int minY = Mathf.Min(first.y, second.y);
        int maxX = Mathf.Max(first.x + first.width, second.x + second.width);
        int maxY = Mathf.Max(first.y + first.height, second.y + second.height);
        return new RectInt(minX, minY, maxX - minX, maxY - minY);
    }
}
