using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders an authored modal confirmation dialog and emits the selected response.
/// </summary>
public sealed class ConfirmationDialogView : MonoBehaviour, IContentInitializable, ICancelable
{
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
    private bool acknowledgmentMode;

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
        ApplyDialogArtwork();
    }

    /// <summary>
    /// Shows the dialog with the supplied message.
    /// </summary>
    /// <param name="message">The confirmation prompt.</param>
    public void Show(string message)
    {
        ShowDialog(message, false);
    }

    /// <summary>
    /// Shows one acknowledgment action and retains the notice until its owner completes the transition.
    /// </summary>
    /// <param name="message">The notice to acknowledge.</param>
    public void ShowAcknowledgment(string message)
    {
        ShowDialog(message, true);
    }

    /// <summary>
    /// Applies the authored dialog presentation and the available response controls.
    /// </summary>
    /// <param name="message">The prompt or notice text.</param>
    /// <param name="acknowledgment">Whether cancellation is unavailable and acceptance keeps the notice visible.</param>
    private void ShowDialog(string message, bool acknowledgment)
    {
        VerifyReferences();
        BindControls();
        bool selectConfirm = !acknowledgment || !acknowledgmentMode || !gameObject.activeSelf;
        acknowledgmentMode = acknowledgment;
        ApplyDialogArtwork();
        confirmButtonPressVisual.SetTextures(confirmButtonUpTexture, confirmButtonDownTexture);
        cancelButtonPressVisual.SetTextures(cancelButtonUpTexture, cancelButtonDownTexture);
        cancelButton.gameObject.SetActive(!acknowledgment);
        UILayout.SetTextContent(messageTextField, message ?? string.Empty, messageTextColor);
        gameObject.SetActive(true);
        if (selectConfirm)
            confirmButton.Select();
    }

    /// <summary>
    /// Applies the configured background and confirm-button position for the active dialog mode.
    /// </summary>
    private void ApplyDialogArtwork()
    {
        backgroundImage.texture = acknowledgmentMode
            ? acknowledgmentBackgroundTexture
            : confirmationBackgroundTexture;
        backgroundImage.enabled = backgroundImage.texture != null;
        backgroundImage.raycastTarget = false;
        confirmButtonImage.rectTransform.anchoredPosition = acknowledgmentMode
            ? acknowledgmentButtonPosition
            : confirmationButtonPosition;
    }

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
        if (!acknowledgmentMode)
            Hide();
        Confirmed?.Invoke();
    }

    /// <summary>
    /// Hides a normal confirmation and emits cancellation while leaving acknowledgment notices intact.
    /// </summary>
    private void Cancel()
    {
        if (acknowledgmentMode)
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
