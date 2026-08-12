using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders an authored modal confirmation dialog and emits the selected response.
/// </summary>
public sealed class SaveMenuConfirmDialogView : MonoBehaviour, IContentInitializable
{
    private const string _backgroundAddress = "Application/Common/UI/ui_common_confirmation_dialog";
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
    public void InitializeContent(IContentAssetSource contentAssets)
    {
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));

        backgroundImage.texture = ContentBindings.RequireTexture(contentAssets, _backgroundAddress);
        confirmButtonUpTexture = ContentBindings.RequireTexture(contentAssets, _confirmUpAddress);
        confirmButtonDownTexture = ContentBindings.RequireTexture(
            contentAssets,
            _confirmDownAddress
        );
        cancelButtonUpTexture = ContentBindings.RequireTexture(contentAssets, _cancelUpAddress);
        cancelButtonDownTexture = ContentBindings.RequireTexture(contentAssets, _cancelDownAddress);
    }

    /// <summary>
    /// Shows the dialog with the supplied message.
    /// </summary>
    /// <param name="message">The confirmation prompt.</param>
    public void Show(string message)
    {
        VerifyReferences();
        BindControls();
        Image inputBlocker = transform.Find("InputBlocker")?.GetComponent<Image>();
        if (inputBlocker != null)
        {
            StretchToDialog(inputBlocker.rectTransform);
            inputBlocker.color = new Color(0f, 0f, 0f, 0.8f);
        }
        backgroundImage.enabled = backgroundImage.texture != null;
        backgroundImage.raycastTarget = false;
        confirmButtonPressVisual.SetTextures(confirmButtonUpTexture, confirmButtonDownTexture);
        cancelButtonPressVisual.SetTextures(cancelButtonUpTexture, cancelButtonDownTexture);
        UILayout.SetTextContent(messageTextField, message ?? string.Empty, messageTextColor);
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Stretches the input blocker across the dialog.
    /// </summary>
    private static void StretchToDialog(RectTransform blocker)
    {
        blocker.anchorMin = Vector2.zero;
        blocker.anchorMax = Vector2.one;
        blocker.pivot = new Vector2(0.5f, 0.5f);
        blocker.anchoredPosition = Vector2.zero;
        blocker.sizeDelta = Vector2.zero;
    }

    /// <summary>
    /// Hides the dialog without emitting a response.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Verifies every authored reference and texture required by the dialog.
    /// </summary>
    public void VerifyReferences()
    {
        if (backgroundImage == null)
            throw new MissingReferenceException("BackgroundImage is missing.");
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
    /// Hides the dialog and emits confirmation acceptance.
    /// </summary>
    private void Confirm()
    {
        Hide();
        Confirmed?.Invoke();
    }

    /// <summary>
    /// Hides the dialog and emits confirmation cancellation.
    /// </summary>
    private void Cancel()
    {
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
