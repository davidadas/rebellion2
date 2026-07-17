using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Renders immutable Encyclopedia snapshots and emits semantic user gestures.
/// </summary>
public sealed class EncyclopediaWindowView : MonoBehaviour
{
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
    private RawImage[] lowerButtonImages = Array.Empty<RawImage>();

    [SerializeField]
    private RawImagePressVisual[] lowerButtonPressVisuals = Array.Empty<RawImagePressVisual>();

    [SerializeField]
    private Button[] lowerButtons = Array.Empty<Button>();

    [SerializeField]
    private EncyclopediaIndexPanelView indexPanel;

    [SerializeField]
    private EncyclopediaDetailPanelView detailPanel;

    private readonly List<Button> boundDialogButtons = new List<Button>();
    private readonly List<UnityAction> dialogButtonListeners = new List<UnityAction>();
    private EncyclopediaWindowRenderData lastData;
    private Texture defaultOverlayFrameTexture;
    private Texture defaultButtonStripTexture;
    private Texture[] defaultUpperButtonTextures = Array.Empty<Texture>();
    private Texture[] defaultLowerButtonTextures = Array.Empty<Texture>();

    /// <summary>
    /// Raised when an authored command button is pressed.
    /// </summary>
    public event Action<EncyclopediaWindowView, EncyclopediaWindowCommand> CommandRequested;

    /// <summary>
    /// Raised when an index row requests the strategy context menu.
    /// </summary>
    public event Action<EncyclopediaWindowView, string, PointerEventData> ContextRequested;

    /// <summary>
    /// Raised when the view is destroyed so its controller can release subscriptions.
    /// </summary>
    public event Action<EncyclopediaWindowView> Destroyed;

    /// <summary>
    /// Raised when the owning strategy window should receive focus.
    /// </summary>
    public event Action<EncyclopediaWindowView> FocusRequested;

    /// <summary>
    /// Raised when the next projected entry is requested.
    /// </summary>
    public event Action<EncyclopediaWindowView> NextRequested;

    /// <summary>
    /// Raised when the previous projected entry is requested.
    /// </summary>
    public event Action<EncyclopediaWindowView> PreviousRequested;

    /// <summary>
    /// Raised when an index row is activated.
    /// </summary>
    public event Action<EncyclopediaWindowView, string> RowActivated;

    /// <summary>
    /// Raised when an index row is selected.
    /// </summary>
    public event Action<EncyclopediaWindowView, string> RowSelected;

    /// <summary>
    /// Raised when the entry-name filter changes.
    /// </summary>
    public event Action<EncyclopediaWindowView, string> SearchTextChanged;

    /// <summary>
    /// Raised when a semantic database tab is selected.
    /// </summary>
    public event Action<EncyclopediaWindowView, EncyclopediaWindowTab> TabSelected;

    /// <summary>
    /// Validates authored references, captures fallback artwork, and binds controls.
    /// </summary>
    private void Awake()
    {
        VerifyReferences();
        CaptureDefaultTextures();
        BindControls();
    }

    /// <summary>
    /// Processes keyboard entry navigation while this window owns focus.
    /// </summary>
    private void Update()
    {
        if (lastData?.Frame?.ActiveWindow != true || !lastData.Panel)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.leftArrowKey.wasPressedThisFrame)
            RequestPreviousEntry();
        else if (keyboard.rightArrowKey.wasPressedThisFrame)
            RequestNextEntry();
    }

    /// <summary>
    /// Notifies the feature controller that this view no longer owns subscriptions.
    /// </summary>
    private void OnDestroy()
    {
        UnbindControls();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Applies a complete Encyclopedia presentation snapshot.
    /// </summary>
    /// <param name="data">The immutable presentation snapshot to render.</param>
    public void Render(EncyclopediaWindowRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        VerifyReferences();
        lastData = data;
        UILayout.SetSourcePosition(transform as RectTransform, data.Frame.X, data.Frame.Y);
        RenderOverlay(data.Frame);
        RenderDialogButtons(data.Frame);

        if (data.Panel)
        {
            indexPanel.Hide();
            detailPanel.Render(data.Detail, data.Index.SelectedIndex);
        }
        else
        {
            detailPanel.Hide();
            indexPanel.Render(data.Index, data.Frame.ActiveWindow);
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Binds authored controls and focused child panels to semantic handlers.
    /// </summary>
    private void BindControls()
    {
        BindDialogButtons(upperButtons);
        BindDialogButtons(lowerButtons);

        indexPanel.ContextRequested += HandleContextRequested;
        indexPanel.RowActivated += HandleRowActivated;
        indexPanel.RowSelected += HandleRowSelected;
        indexPanel.SearchTextChanged += HandleSearchTextChanged;
        indexPanel.TabSelected += HandleTabSelected;
        detailPanel.NextRequested += RequestNextEntry;
        detailPanel.PreviousRequested += RequestPreviousEntry;
    }

    /// <summary>
    /// Binds one authored command-button layout.
    /// </summary>
    /// <param name="buttons">The command buttons to bind.</param>
    private void BindDialogButtons(Button[] buttons)
    {
        if (buttons == null)
            return;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;

            UnityAction listener = () => HandleDialogButtonClicked(button);
            boundDialogButtons.Add(button);
            dialogButtonListeners.Add(listener);
            button.onClick.AddListener(listener);
        }
    }

    /// <summary>
    /// Detaches every authored button and child-view event owned by this window.
    /// </summary>
    private void UnbindControls()
    {
        int count = Math.Min(boundDialogButtons.Count, dialogButtonListeners.Count);
        for (int i = 0; i < count; i++)
        {
            Button button = boundDialogButtons[i];
            UnityAction listener = dialogButtonListeners[i];
            if (button != null && listener != null)
                button.onClick.RemoveListener(listener);
        }

        if (indexPanel != null)
        {
            indexPanel.ContextRequested -= HandleContextRequested;
            indexPanel.RowActivated -= HandleRowActivated;
            indexPanel.RowSelected -= HandleRowSelected;
            indexPanel.SearchTextChanged -= HandleSearchTextChanged;
            indexPanel.TabSelected -= HandleTabSelected;
        }

        if (detailPanel != null)
        {
            detailPanel.NextRequested -= RequestNextEntry;
            detailPanel.PreviousRequested -= RequestPreviousEntry;
        }
    }

    /// <summary>
    /// Emits a normalized entry-name filter gesture.
    /// </summary>
    /// <param name="value">The new input-field value.</param>
    private void HandleSearchTextChanged(string value)
    {
        SearchTextChanged?.Invoke(this, value ?? string.Empty);
    }

    /// <summary>
    /// Emits one semantic database-tab selection gesture.
    /// </summary>
    /// <param name="tab">The selected semantic database tab.</param>
    private void HandleTabSelected(EncyclopediaWindowTab tab)
    {
        RequestFocus();
        TabSelected?.Invoke(this, tab);
    }

    /// <summary>
    /// Emits one clicked index-row selection gesture.
    /// </summary>
    /// <param name="entryTypeId">The clicked catalog entry identifier.</param>
    private void HandleRowSelected(string entryTypeId)
    {
        RequestFocus();
        RowSelected?.Invoke(this, entryTypeId);
    }

    /// <summary>
    /// Emits one index-row activation gesture.
    /// </summary>
    /// <param name="entryTypeId">The activated catalog entry identifier.</param>
    private void HandleRowActivated(string entryTypeId)
    {
        RequestFocus();
        RowActivated?.Invoke(this, entryTypeId);
    }

    /// <summary>
    /// Forwards a row context request without changing local selection.
    /// </summary>
    /// <param name="entryTypeId">The catalog entry that received the context gesture.</param>
    /// <param name="eventData">The pointer event that requested the context menu.</param>
    private void HandleContextRequested(string entryTypeId, PointerEventData eventData)
    {
        ContextRequested?.Invoke(this, entryTypeId, eventData);
    }

    /// <summary>
    /// Emits the semantic command represented by one authored button.
    /// </summary>
    /// <param name="button">The command button that was clicked.</param>
    private void HandleDialogButtonClicked(Button button)
    {
        RequestFocus();
        EncyclopediaWindowCommand command = GetButtonCommand(button);
        if (command == EncyclopediaWindowCommand.None)
            return;

        CommandRequested?.Invoke(this, command);
    }

    /// <summary>
    /// Resolves the semantic command assigned to an authored button slot.
    /// </summary>
    /// <param name="button">The command button to resolve.</param>
    /// <returns>The configured semantic command, or none when no slot matches.</returns>
    private EncyclopediaWindowCommand GetButtonCommand(Button button)
    {
        if (lastData == null)
            return EncyclopediaWindowCommand.None;

        Button[] buttonSlots = GetButtonComponents(lastData.Frame);
        int count = Mathf.Min(buttonSlots.Length, lastData.Frame.DialogButtons.Count);
        for (int i = 0; i < count; i++)
        {
            if (buttonSlots[i] == button)
                return lastData.Frame.DialogButtons[i].Command;
        }

        return EncyclopediaWindowCommand.None;
    }

    /// <summary>
    /// Emits a request for the previous projected entry.
    /// </summary>
    private void RequestPreviousEntry()
    {
        RequestFocus();
        PreviousRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a request for the next projected entry.
    /// </summary>
    private void RequestNextEntry()
    {
        RequestFocus();
        NextRequested?.Invoke(this);
    }

    /// <summary>
    /// Requests focus for the owning strategy window.
    /// </summary>
    private void RequestFocus()
    {
        FocusRequested?.Invoke(this);
    }

    /// <summary>
    /// Applies the current overlay frame artwork.
    /// </summary>
    /// <param name="frame">The immutable frame presentation.</param>
    private void RenderOverlay(EncyclopediaWindowFrameRenderData frame)
    {
        UILayout.SetImageTexture(
            overlayFrameImage,
            frame.OverlayFrameTexture ?? defaultOverlayFrameTexture
        );
    }

    /// <summary>
    /// Renders the active authored command-button layout.
    /// </summary>
    /// <param name="frame">The immutable frame presentation.</param>
    private void RenderDialogButtons(EncyclopediaWindowFrameRenderData frame)
    {
        RectInt buttonStripRect = UILayout.GetSourceRect(buttonStripImage.rectTransform);
        Texture buttonStripTexture = frame.ButtonStripTexture;
        if (buttonStripTexture == null && !frame.UseUpperButtonLayout)
            buttonStripTexture = defaultButtonStripTexture;

        int textureWidth = UILayout.GetTextureSourceWidth(buttonStripTexture);
        int buttonStripWidth = textureWidth > 0 ? textureWidth : buttonStripRect.width;
        UILayout.SetImage(
            buttonStripImage,
            buttonStripTexture,
            frame.Width - buttonStripWidth,
            buttonStripRect.y
        );

        HideButtonSlots(upperButtonImages);
        HideButtonSlots(lowerButtonImages);

        RawImage[] buttonSlots = GetButtonSlots(frame);
        RawImagePressVisual[] pressVisuals = GetButtonPressVisuals(frame);
        Button[] buttonComponents = GetButtonComponents(frame);
        if (
            frame.DialogButtons.Count > buttonSlots.Length
            || frame.DialogButtons.Count > pressVisuals.Length
            || frame.DialogButtons.Count > buttonComponents.Length
        )
        {
            throw new MissingReferenceException(
                $"{name} cannot render {frame.DialogButtons.Count} Encyclopedia command buttons."
            );
        }

        int count = frame.DialogButtons.Count;
        for (int i = 0; i < count; i++)
        {
            EncyclopediaDialogButtonRenderData button = frame.DialogButtons[i];
            Texture buttonUpTexture = button?.Texture ?? GetDefaultButtonTexture(buttonSlots, i);
            Texture buttonDownTexture = button?.PressedTexture ?? buttonUpTexture;
            SetDialogButton(
                buttonSlots[i],
                pressVisuals[i],
                buttonComponents,
                button?.SourceRect,
                buttonUpTexture,
                buttonDownTexture,
                i
            );
        }
    }

    /// <summary>
    /// Selects the authored command-button image layout for the current frame.
    /// </summary>
    /// <param name="frame">The immutable frame presentation.</param>
    /// <returns>The active command-button image slots.</returns>
    private RawImage[] GetButtonSlots(EncyclopediaWindowFrameRenderData frame)
    {
        return frame.UseUpperButtonLayout ? upperButtonImages : lowerButtonImages;
    }

    /// <summary>
    /// Selects the authored command-button pressed-state visuals for the current frame.
    /// </summary>
    /// <param name="frame">The immutable frame presentation.</param>
    /// <returns>The active command-button pressed-state visuals.</returns>
    private RawImagePressVisual[] GetButtonPressVisuals(EncyclopediaWindowFrameRenderData frame)
    {
        return frame.UseUpperButtonLayout ? upperButtonPressVisuals : lowerButtonPressVisuals;
    }

    /// <summary>
    /// Selects the authored command-button controls for the current frame.
    /// </summary>
    /// <param name="frame">The immutable frame presentation.</param>
    /// <returns>The active command-button controls.</returns>
    private Button[] GetButtonComponents(EncyclopediaWindowFrameRenderData frame)
    {
        return frame.UseUpperButtonLayout ? upperButtons : lowerButtons;
    }

    /// <summary>
    /// Returns the authored fallback texture for a command-button slot.
    /// </summary>
    /// <param name="buttonSlots">The active authored button-image layout.</param>
    /// <param name="index">The requested button slot.</param>
    /// <returns>The authored fallback texture, or null when unavailable.</returns>
    private Texture GetDefaultButtonTexture(RawImage[] buttonSlots, int index)
    {
        Texture[] textures =
            buttonSlots == upperButtonImages
                ? defaultUpperButtonTextures
                : defaultLowerButtonTextures;
        return index >= 0 && index < textures.Length ? textures[index] : null;
    }

    /// <summary>
    /// Captures authored fallback textures before runtime presentation changes.
    /// </summary>
    private void CaptureDefaultTextures()
    {
        defaultOverlayFrameTexture = overlayFrameImage.texture;
        defaultButtonStripTexture = buttonStripImage.texture;
        defaultUpperButtonTextures = CaptureDefaultTextures(upperButtonImages);
        defaultLowerButtonTextures = CaptureDefaultTextures(lowerButtonImages);
    }

    /// <summary>
    /// Copies current textures from an authored image layout.
    /// </summary>
    /// <param name="images">The images whose textures should be copied.</param>
    /// <returns>The copied texture sequence.</returns>
    private static Texture[] CaptureDefaultTextures(RawImage[] images)
    {
        if (images == null)
            return Array.Empty<Texture>();

        Texture[] textures = new Texture[images.Length];
        for (int i = 0; i < images.Length; i++)
            textures[i] = images[i]?.texture;

        return textures;
    }

    /// <summary>
    /// Hides every image in an authored command-button layout.
    /// </summary>
    /// <param name="images">The command-button images to hide.</param>
    private static void HideButtonSlots(RawImage[] images)
    {
        if (images == null)
            return;

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] != null)
                images[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Applies resolved artwork, layout, and availability to one command-button slot.
    /// </summary>
    /// <param name="image">The authored command-button image.</param>
    /// <param name="pressVisual">The authored pressed-state visual.</param>
    /// <param name="buttons">The authored command-button controls.</param>
    /// <param name="sourceRect">The optional configured source-space bounds.</param>
    /// <param name="texture">The current-state texture.</param>
    /// <param name="pressedTexture">The pressed-state texture.</param>
    /// <param name="index">The authored button slot.</param>
    private static void SetDialogButton(
        RawImage image,
        RawImagePressVisual pressVisual,
        Button[] buttons,
        RectInt? sourceRect,
        Texture texture,
        Texture pressedTexture,
        int index
    )
    {
        if (image == null)
            return;

        if (texture == null)
        {
            image.gameObject.SetActive(false);
            if (buttons != null && index >= 0 && index < buttons.Length && buttons[index] != null)
                buttons[index].interactable = false;
            return;
        }

        ApplyButtonLayout(image, sourceRect, texture);
        pressVisual.SetInteractiveTextures(texture, pressedTexture);
        if (buttons != null && index >= 0 && index < buttons.Length && buttons[index] != null)
            buttons[index].interactable = true;
    }

    /// <summary>
    /// Applies configured source-space bounds or preserves right alignment by texture size.
    /// </summary>
    /// <param name="image">The command-button image to lay out.</param>
    /// <param name="sourceRect">The optional configured source-space bounds.</param>
    /// <param name="texture">The resolved command-button texture.</param>
    private static void ApplyButtonLayout(RawImage image, RectInt? sourceRect, Texture texture)
    {
        if (!sourceRect.HasValue)
        {
            UILayout.SetRightAlignedImageSize(image, texture);
            return;
        }

        RectInt layout = sourceRect.Value;
        UILayout.SetSourceRect(
            image.rectTransform,
            layout.x,
            layout.y,
            layout.width,
            layout.height
        );
    }

    /// <summary>
    /// Verifies every authored reference required to coordinate the Encyclopedia panels.
    /// </summary>
    private void VerifyReferences()
    {
        if (overlayFrameImage == null)
            throw new MissingReferenceException($"{name}/OverlayFrameImage is missing.");
        if (buttonStripImage == null)
            throw new MissingReferenceException($"{name}/ButtonStripImage is missing.");
        VerifyButtonSlotReferences("Upper", upperButtonImages);
        VerifyPressVisualReferences("Upper", upperButtonPressVisuals, upperButtonImages.Length);
        VerifyButtonReferences("Upper", upperButtons);
        VerifyButtonSlotReferences("Lower", lowerButtonImages);
        VerifyPressVisualReferences("Lower", lowerButtonPressVisuals, lowerButtonImages.Length);
        VerifyButtonReferences("Lower", lowerButtons);
        if (indexPanel == null)
            throw new MissingReferenceException($"{name}/IndexPanel is missing.");
        if (detailPanel == null)
            throw new MissingReferenceException($"{name}/DetailPanel is missing.");
    }

    /// <summary>
    /// Verifies an authored command-button image layout.
    /// </summary>
    /// <param name="label">The layout label used in validation errors.</param>
    /// <param name="images">The authored command-button images.</param>
    private void VerifyButtonSlotReferences(string label, RawImage[] images)
    {
        if (images == null || images.Length == 0)
            throw new MissingReferenceException($"{name}/{label} button images are missing.");

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i] == null)
                throw new MissingReferenceException(
                    $"{name}/{label} command button image slot {i} is missing."
                );
        }
    }

    /// <summary>
    /// Verifies an authored command-button pressed-state visual layout.
    /// </summary>
    /// <param name="label">The layout label used in validation errors.</param>
    /// <param name="pressVisuals">The authored pressed-state visuals.</param>
    /// <param name="expectedCount">The matching command-button image count.</param>
    private void VerifyPressVisualReferences(
        string label,
        RawImagePressVisual[] pressVisuals,
        int expectedCount
    )
    {
        if (pressVisuals == null || pressVisuals.Length != expectedCount)
            throw new MissingReferenceException(
                $"{name}/{label} button press visuals are incomplete."
            );

        for (int i = 0; i < pressVisuals.Length; i++)
        {
            if (pressVisuals[i] == null)
                throw new MissingReferenceException(
                    $"{name}/{label} command button press visual slot {i} is missing."
                );
        }
    }

    /// <summary>
    /// Verifies an authored command-button control layout.
    /// </summary>
    /// <param name="label">The layout label used in validation errors.</param>
    /// <param name="buttons">The authored command-button controls.</param>
    private void VerifyButtonReferences(string label, Button[] buttons)
    {
        if (buttons == null || buttons.Length == 0)
            throw new MissingReferenceException($"{name}/{label} buttons are missing.");

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
                throw new MissingReferenceException(
                    $"{name}/{label} command button slot {i} is missing."
                );
        }
    }
}
