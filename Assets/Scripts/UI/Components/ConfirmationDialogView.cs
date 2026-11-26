using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders an authored modal confirmation dialog and emits the selected response.
/// </summary>
public sealed class ConfirmationDialogView : MonoBehaviour, IContentInitializable, ICancelable
{
    private enum DialogMode
    {
        Confirmation,
        Acknowledgment,
        Progress,
    }

    private const string _backgroundAddress = "Application/Common/UI/ui_common_confirmation_dialog";
    private const string _acknowledgmentBackgroundAddress =
        "Application/Common/UI/ui_common_acknowledgment_dialog";
    private const string _confirmUpAddress =
        "Application/Common/UI/ui_common_confirmation_yes_button";
    private const string _confirmDownAddress =
        "Application/Common/UI/ui_common_confirmation_yes_button_pressed";
    private const string _cancelUpAddress =
        "Application/Common/UI/ui_common_confirmation_no_button";
    private const string _cancelDownAddress =
        "Application/Common/UI/ui_common_confirmation_no_button_pressed";

    [SerializeField]
    private RawImage backgroundImage;

    [SerializeField]
    private Texture2D confirmationBackgroundTexture;

    [SerializeField]
    private Texture2D acknowledgmentBackgroundTexture;

    [SerializeField]
    private Vector2 confirmationButtonPosition;

    [SerializeField]
    private Vector2 acknowledgmentButtonPosition;

    [SerializeField]
    private Color messageTextColor;

    [SerializeField]
    private RawImage confirmButtonImage;

    [SerializeField]
    private Button confirmButton;

    [SerializeField]
    private RawImagePressVisual confirmButtonPressVisual;

    [SerializeField]
    private Texture2D confirmButtonUpTexture;

    [SerializeField]
    private Texture2D confirmButtonDownTexture;

    [SerializeField]
    private RawImage cancelButtonImage;

    [SerializeField]
    private Button cancelButton;

    [SerializeField]
    private RawImagePressVisual cancelButtonPressVisual;

    [SerializeField]
    private Texture2D cancelButtonUpTexture;

    [SerializeField]
    private Texture2D cancelButtonDownTexture;

    [SerializeField]
    private TextMeshProUGUI messageTextField;

    private bool bound;
    private DialogMode _mode;
    private RectInt? authoredMessageArea;
    private SourceRectLayout confirmationMessageArea;
    private Texture2D backgroundOverride;
    private Texture2D yesOverride;
    private Texture2D yesPressedOverride;
    private Texture2D noOverride;
    private Texture2D noPressedOverride;
    private ConfirmationWindowArtwork confirmationArtwork;
    private Vector2? authoredNoPosition;

    /// <summary>
    /// Gets the artwork defining the dialog's visible bounds.
    /// </summary>
    public RawImage BackgroundArtwork => backgroundImage;

    /// <summary>
    /// Gets the Yes button's two artwork states.
    /// </summary>
    public RawImagePressVisual YesArtwork => confirmButtonPressVisual;

    /// <summary>
    /// Gets the No button's two artwork states.
    /// </summary>
    public RawImagePressVisual NoArtwork => cancelButtonPressVisual;

    /// <summary>
    /// Gets the rectangle used to lay out the shared dialog's message.
    /// </summary>
    public RectTransform MessageAreaTransform => messageTextField.rectTransform;

    /// <summary>
    /// Gets the prefab's message area in top-left source coordinates.
    /// </summary>
    public RectInt AuthoredMessageArea
    {
        get { return authoredMessageArea ??= UILayout.GetSourceRect(MessageAreaTransform); }
    }

    /// <summary>
    /// Occurs when the dialog is confirmed.
    /// </summary>
    public event Action Confirmed;

    /// <summary>
    /// Occurs when the operation is canceled.
    /// </summary>
    public event Action Canceled;

    /// <summary>
    /// Replaces editor previews with confirmation artwork loaded from installation content.
    /// </summary>
    /// <param name="contentAssets">The content assets.</param>
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        ContentPackDefinition pack = (contentAssets as ContentAssets)?.PackDefinition;
        InitializeContent(contentAssets, pack?.ConfirmationWindowArtwork);
        SetConfirmationMessageArea(pack?.ConfirmationMessageArea);
    }

    /// <summary>
    /// Loads the confirmation artwork with the supplied working-pack overrides.
    /// </summary>
    /// <param name="contentAssets">The content assets.</param>
    /// <param name="artwork">The optional overrides for the two-choice dialog.</param>
    public void InitializeContent(
        IContentAssetSource contentAssets,
        ConfirmationWindowArtwork artwork
    )
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));

        confirmationBackgroundTexture = ContentBindings.RequireTexture(
            contentAssets,
            _backgroundAddress
        );
        acknowledgmentBackgroundTexture = ContentBindings.RequireTexture(
            contentAssets,
            _acknowledgmentBackgroundAddress
        );
        confirmButtonUpTexture = ContentBindings.RequireTexture(contentAssets, _confirmUpAddress);
        confirmButtonDownTexture = ContentBindings.RequireTexture(
            contentAssets,
            _confirmDownAddress
        );
        cancelButtonUpTexture = ContentBindings.RequireTexture(contentAssets, _cancelUpAddress);
        cancelButtonDownTexture = ContentBindings.RequireTexture(contentAssets, _cancelDownAddress);
        backgroundOverride = LoadOverride(contentAssets, artwork?.BackgroundImagePath);
        yesOverride = LoadOverride(contentAssets, artwork?.YesImagePath);
        yesPressedOverride = LoadOverride(contentAssets, artwork?.YesPressedImagePath);
        noOverride = LoadOverride(contentAssets, artwork?.NoImagePath);
        noPressedOverride = LoadOverride(contentAssets, artwork?.NoPressedImagePath);
        confirmationArtwork = artwork;
        ApplyDialogArtwork();
    }

    /// <summary>
    /// Loads an authored texture when its address is present.
    /// </summary>
    /// <param name="assets">The content assets.</param>
    /// <param name="address">The optional artwork address.</param>
    /// <returns>The loaded override, or null to use the built-in artwork.</returns>
    private static Texture2D LoadOverride(IContentAssetSource assets, string address) =>
        string.IsNullOrWhiteSpace(address) ? null : assets.GetTexture(address);

    /// <summary>
    /// Updates the prompt area used by the two-choice dialog.
    /// </summary>
    /// <param name="area">The optional pack override in source coordinates.</param>
    public void SetConfirmationMessageArea(SourceRectLayout area)
    {
        confirmationMessageArea = area;
        ApplyMessageArea();
    }

    /// <summary>
    /// Uses the authored prompt area when a dialog mode has no usable override.
    /// </summary>
    private void ApplyMessageArea()
    {
        RectInt area = AuthoredMessageArea;
        if (
            _mode == DialogMode.Confirmation
            && confirmationMessageArea is { Width: > 0, Height: > 0 } layout
        )
            area = new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
        RectTransform rect = messageTextField.rectTransform;
        rect.anchoredPosition = new Vector2(area.x, -area.y);
        rect.sizeDelta = new Vector2(area.width, area.height);
    }

    /// <summary>
    /// Shows the dialog with the supplied message.
    /// </summary>
    /// <param name="message">The confirmation prompt.</param>
    public void Show(string message) => Show(message, takeFocus: true);

    /// <summary>
    /// Shows the confirmation while letting the host retain its current keyboard focus.
    /// </summary>
    /// <param name="message">The confirmation prompt.</param>
    /// <param name="takeFocus">Whether to select the initial confirmation button.</param>
    public void Show(string message, bool takeFocus)
    {
        ShowDialog(message, DialogMode.Confirmation, takeFocus);
    }

    /// <summary>
    /// Shows one acknowledgment action and retains the notice until its owner completes the transition.
    /// </summary>
    /// <param name="message">The notice to acknowledge.</param>
    public void ShowAcknowledgment(string message)
    {
        ShowDialog(message, DialogMode.Acknowledgment);
    }

    /// <summary>
    /// Shows a pending operation with one cancel action using the shared single-action artwork.
    /// </summary>
    /// <param name="message">The operation's current progress.</param>
    public void ShowProgress(string message)
    {
        ShowDialog(message, DialogMode.Progress);
    }

    /// <summary>
    /// Applies the authored dialog presentation and the available response controls.
    /// </summary>
    /// <param name="message">The prompt or notice text.</param>
    /// <param name="mode">The response controls available for this dialog.</param>
    /// <param name="takeFocus">Whether this dialog may select its primary response.</param>
    private void ShowDialog(string message, DialogMode mode, bool takeFocus = true)
    {
        VerifyReferences();
        BindControls();
        bool selectConfirm =
            mode == DialogMode.Confirmation || _mode != mode || !gameObject.activeSelf;
        _mode = mode;
        ApplyDialogArtwork();
        ApplyMessageArea();
        bool confirmation = mode == DialogMode.Confirmation;
        confirmButtonPressVisual.SetTextures(
            mode == DialogMode.Progress ? cancelButtonUpTexture
                : confirmation ? yesOverride ?? confirmButtonUpTexture
                : confirmButtonUpTexture,
            mode == DialogMode.Progress ? cancelButtonDownTexture
                : confirmation ? yesPressedOverride ?? confirmButtonDownTexture
                : confirmButtonDownTexture
        );
        cancelButtonPressVisual.SetTextures(
            confirmation ? noOverride ?? cancelButtonUpTexture : cancelButtonUpTexture,
            confirmation ? noPressedOverride ?? cancelButtonDownTexture : cancelButtonDownTexture
        );
        cancelButton.gameObject.SetActive(confirmation);
        UILayout.SetTextContent(messageTextField, message ?? string.Empty, messageTextColor);
        gameObject.SetActive(true);
        if (takeFocus && selectConfirm)
            confirmButton.Select();
    }

    /// <summary>
    /// Applies the configured background and confirm-button position for the active dialog mode.
    /// </summary>
    private void ApplyDialogArtwork()
    {
        backgroundImage.texture =
            _mode == DialogMode.Confirmation
                ? backgroundOverride ?? confirmationBackgroundTexture
                : acknowledgmentBackgroundTexture;
        backgroundImage.enabled = backgroundImage.texture != null;
        backgroundImage.raycastTarget = false;
        authoredNoPosition ??= cancelButtonImage.rectTransform.anchoredPosition;
        bool confirmation = _mode == DialogMode.Confirmation;
        confirmButtonImage.rectTransform.anchoredPosition = confirmation
            ? GetButtonPosition(confirmationArtwork?.YesPosition, confirmationButtonPosition)
            : acknowledgmentButtonPosition;
        cancelButtonImage.rectTransform.anchoredPosition = confirmation
            ? GetButtonPosition(confirmationArtwork?.NoPosition, authoredNoPosition.Value)
            : authoredNoPosition.Value;
    }

    /// <summary>
    /// Converts an optional source-space point to an authored button anchor.
    /// </summary>
    /// <param name="source">The optional position measured from the dialog's top-left corner.</param>
    /// <param name="fallback">The prefab's default anchored position.</param>
    /// <returns>The position used by the view.</returns>
    private static Vector2 GetButtonPosition(SourcePointLayout source, Vector2 fallback) =>
        source == null ? fallback : new Vector2(source.X, -source.Y);

    /// <summary>
    /// Hides the dialog without emitting a response.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Consumes cancellation while an acknowledgment is visible or declines a normal confirmation.
    /// </summary>
    /// <returns>True when this visible dialog owns the cancellation request.</returns>
    public bool TryCancel()
    {
        if (!gameObject.activeSelf)
            return false;
        Cancel();
        return true;
    }

    /// <summary>
    /// Verifies every authored reference and texture required by the dialog.
    /// </summary>
    public void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException("BackgroundImage is missing.");
        if (confirmationBackgroundTexture == null || acknowledgmentBackgroundTexture == null)
            throw new MissingReferenceException("Dialog background textures are incomplete.");
        if (confirmButtonImage == null || confirmButton == null || confirmButtonPressVisual == null)
            throw new MissingReferenceException("Confirm button references are incomplete.");
        if (cancelButtonImage == null || cancelButton == null || cancelButtonPressVisual == null)
            throw new MissingReferenceException("Cancel button references are incomplete.");
        if (messageTextField == null)
            throw new MissingReferenceException("MessageTextField is missing.");
        if (confirmButtonUpTexture == null || confirmButtonDownTexture == null)
            throw new MissingReferenceException("Confirm button textures are incomplete.");
        if (cancelButtonUpTexture == null || cancelButtonDownTexture == null)
            throw new MissingReferenceException("Cancel button textures are incomplete.");
    }

    /// <summary>
    /// Binds the authored buttons when the prefab instance is loaded.
    /// </summary>
    private void Awake()
    {
        if (ReferencesAssigned())
            BindControls();
    }

    /// <summary>
    /// Removes button listeners when the prefab instance is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (!bound)
            return;

        confirmButton.onClick.RemoveListener(Confirm);
        cancelButton.onClick.RemoveListener(Cancel);
    }

    /// <summary>
    /// Attaches semantic response callbacks exactly once.
    /// </summary>
    private void BindControls()
    {
        if (bound)
            return;

        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
        bound = true;
    }

    /// <summary>
    /// Emits acceptance while retaining acknowledgment notices for their transition owner.
    /// </summary>
    private void Confirm()
    {
        if (_mode == DialogMode.Progress)
        {
            Cancel();
            return;
        }
        if (_mode == DialogMode.Confirmation)
            Hide();
        Confirmed?.Invoke();
    }

    /// <summary>
    /// Hides a normal confirmation and emits cancellation while leaving acknowledgment notices intact.
    /// </summary>
    private void Cancel()
    {
        if (_mode == DialogMode.Acknowledgment)
            return;
        Hide();
        Canceled?.Invoke();
    }

    /// <summary>
    /// Checks whether the prefab has all references needed for early binding.
    /// </summary>
    /// <returns>True when every required authored reference is assigned.</returns>
    private bool ReferencesAssigned()
    {
        return backgroundImage != null
            && confirmationBackgroundTexture != null
            && acknowledgmentBackgroundTexture != null
            && confirmButtonImage != null
            && confirmButton != null
            && confirmButtonPressVisual != null
            && confirmButtonUpTexture != null
            && confirmButtonDownTexture != null
            && cancelButtonImage != null
            && cancelButton != null
            && cancelButtonPressVisual != null
            && cancelButtonUpTexture != null
            && cancelButtonDownTexture != null
            && messageTextField != null;
    }
}
