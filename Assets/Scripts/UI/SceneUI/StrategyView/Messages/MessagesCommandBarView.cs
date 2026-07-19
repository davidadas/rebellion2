using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Owns the authored messages command bar and emits semantic command requests.
/// </summary>
public sealed class MessagesCommandBarView : MonoBehaviour
{
    [SerializeField]
    private RawImage buttonStripImage;

    [SerializeField]
    private RawImage closeButtonImage;

    [SerializeField]
    private RawImagePressVisual closeButtonPressVisual;

    [SerializeField]
    private Button closeButton;

    [SerializeField]
    private RawImage displayButtonImage;

    [SerializeField]
    private RawImagePressVisual displayButtonPressVisual;

    [SerializeField]
    private Button displayButton;

    [SerializeField]
    private RawImage indexButtonImage;

    [SerializeField]
    private RawImagePressVisual indexButtonPressVisual;

    [SerializeField]
    private Button indexButton;

    [SerializeField]
    private RawImage signalButtonImage;

    [SerializeField]
    private RawImagePressVisual signalButtonPressVisual;

    [SerializeField]
    private Button signalButton;

    [SerializeField]
    private RawImage signalTargetButtonImage;

    [SerializeField]
    private RawImagePressVisual signalTargetButtonPressVisual;

    [SerializeField]
    private Button signalTargetButton;

    [SerializeField]
    private RawImage chatButtonImage;

    [SerializeField]
    private RawImagePressVisual chatButtonPressVisual;

    [SerializeField]
    private Button chatButton;

    private Texture defaultButtonStripTexture;
    private Texture defaultChatButtonTexture;
    private Texture defaultCloseButtonTexture;
    private Texture defaultDisplayButtonTexture;
    private Texture defaultIndexButtonTexture;
    private Texture defaultSignalButtonTexture;
    private Texture defaultSignalTargetButtonTexture;
    private UnityAction chatButtonListener;
    private UnityAction closeButtonListener;
    private UnityAction displayButtonListener;
    private bool initialized;
    private UnityAction indexButtonListener;
    private UnityAction signalButtonListener;
    private UnityAction signalTargetButtonListener;

    /// <summary>
    /// Raised when any command control is pressed.
    /// </summary>
    public event Action ControlPressed;

    /// <summary>
    /// Raised when the chat command is requested.
    /// </summary>
    public event Action ChatRequested;

    /// <summary>
    /// Raised when the close command is requested.
    /// </summary>
    public event Action CloseRequested;

    /// <summary>
    /// Raised when the display command is requested.
    /// </summary>
    public event Action DisplayRequested;

    /// <summary>
    /// Raised when the index command is requested.
    /// </summary>
    public event Action IndexRequested;

    /// <summary>
    /// Raised when the notification command is requested.
    /// </summary>
    public event Action SignalRequested;

    /// <summary>
    /// Raised when the selected message target command is requested.
    /// </summary>
    public event Action TargetRequested;

    /// <summary>
    /// Applies a complete command-bar presentation snapshot.
    /// </summary>
    /// <param name="data">The projected command-bar presentation.</param>
    public void Render(MessagesCommandBarRenderData data)
    {
        EnsureInitialized();
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        UILayout.SetImageTexture(
            buttonStripImage,
            data.ButtonStripTexture ?? defaultButtonStripTexture
        );
        RenderButton(
            closeButtonImage,
            closeButtonPressVisual,
            closeButton,
            data.CloseButton,
            defaultCloseButtonTexture
        );
        RenderButton(
            displayButtonImage,
            displayButtonPressVisual,
            displayButton,
            data.DisplayButton,
            defaultDisplayButtonTexture
        );
        RenderButton(
            indexButtonImage,
            indexButtonPressVisual,
            indexButton,
            data.IndexButton,
            defaultIndexButtonTexture
        );
        RenderButton(
            signalButtonImage,
            signalButtonPressVisual,
            signalButton,
            data.SignalButton,
            defaultSignalButtonTexture
        );
        RenderButton(
            signalTargetButtonImage,
            signalTargetButtonPressVisual,
            signalTargetButton,
            data.SignalTargetButton,
            defaultSignalTargetButtonTexture
        );
        RenderButton(
            chatButtonImage,
            chatButtonPressVisual,
            chatButton,
            data.ChatButton,
            defaultChatButtonTexture
        );
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies authored references and binds command controls.
    /// </summary>
    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Detaches every authored command listener owned by this view.
    /// </summary>
    private void OnDestroy()
    {
        UnbindButton(closeButton, closeButtonListener);
        UnbindButton(displayButton, displayButtonListener);
        UnbindButton(indexButton, indexButtonListener);
        UnbindButton(signalButton, signalButtonListener);
        UnbindButton(signalTargetButton, signalTargetButtonListener);
        UnbindButton(chatButton, chatButtonListener);
    }

    /// <summary>
    /// Captures authored fallback textures and binds each command exactly once.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized)
            return;

        VerifyReferences();
        defaultButtonStripTexture = buttonStripImage.texture;
        defaultCloseButtonTexture = closeButtonImage.texture;
        defaultDisplayButtonTexture = displayButtonImage.texture;
        defaultIndexButtonTexture = indexButtonImage.texture;
        defaultSignalButtonTexture = signalButtonImage.texture;
        defaultSignalTargetButtonTexture = signalTargetButtonImage.texture;
        defaultChatButtonTexture = chatButtonImage.texture;

        closeButtonListener = BindButton(closeButton, () => CloseRequested?.Invoke());
        displayButtonListener = BindButton(displayButton, () => DisplayRequested?.Invoke());
        indexButtonListener = BindButton(indexButton, () => IndexRequested?.Invoke());
        signalButtonListener = BindButton(signalButton, () => SignalRequested?.Invoke());
        signalTargetButtonListener = BindButton(
            signalTargetButton,
            () => TargetRequested?.Invoke()
        );
        chatButtonListener = BindButton(chatButton, () => ChatRequested?.Invoke());
        initialized = true;
    }

    /// <summary>
    /// Binds one authored button to its semantic request and the shared press event.
    /// </summary>
    /// <param name="button">The authored button.</param>
    /// <param name="request">The semantic request delegate captured at initialization.</param>
    /// <returns>The retained Unity listener used to detach the binding.</returns>
    private UnityAction BindButton(Button button, Action request)
    {
        UnityAction listener = () =>
        {
            ControlPressed?.Invoke();
            request?.Invoke();
        };
        button.onClick.AddListener(listener);
        return listener;
    }

    /// <summary>
    /// Detaches one retained command listener when both authored references remain available.
    /// </summary>
    /// <param name="button">The authored button.</param>
    /// <param name="listener">The retained Unity listener.</param>
    private static void UnbindButton(Button button, UnityAction listener)
    {
        if (button != null && listener != null)
            button.onClick.RemoveListener(listener);
    }

    /// <summary>
    /// Applies one command button's textures and interaction state.
    /// </summary>
    /// <param name="image">The authored button image.</param>
    /// <param name="pressVisual">The authored pressed-state visual.</param>
    /// <param name="button">The authored button control.</param>
    /// <param name="data">The projected button presentation.</param>
    /// <param name="fallbackTexture">The authored fallback texture.</param>
    private static void RenderButton(
        RawImage image,
        RawImagePressVisual pressVisual,
        Button button,
        MessagesCommandButtonRenderData data,
        Texture fallbackTexture
    )
    {
        if (!data.Visible)
        {
            pressVisual.SetInteractiveTextures(null, null);
            button.interactable = false;
            image.raycastTarget = false;
            return;
        }

        Texture texture = data.Texture ?? fallbackTexture;
        Texture pressedTexture = data.PressedTexture ?? texture;
        pressVisual.SetInteractiveTextures(texture, data.Enabled ? pressedTexture : null);
        button.interactable = data.Enabled && texture != null;
        image.raycastTarget = button.interactable;
    }

    /// <summary>
    /// Verifies every authored command-bar reference before use.
    /// </summary>
    private void VerifyReferences()
    {
        if (buttonStripImage == null)
            throw new MissingReferenceException($"{name}/ButtonStripImage is missing.");
        if (closeButtonImage == null || closeButtonPressVisual == null || closeButton == null)
            throw new MissingReferenceException($"{name}/CloseButton is missing.");
        if (displayButtonImage == null || displayButtonPressVisual == null || displayButton == null)
            throw new MissingReferenceException($"{name}/DisplayButton is missing.");
        if (indexButtonImage == null || indexButtonPressVisual == null || indexButton == null)
            throw new MissingReferenceException($"{name}/IndexButton is missing.");
        if (signalButtonImage == null || signalButtonPressVisual == null || signalButton == null)
            throw new MissingReferenceException($"{name}/SignalButton is missing.");
        if (
            signalTargetButtonImage == null
            || signalTargetButtonPressVisual == null
            || signalTargetButton == null
        )
            throw new MissingReferenceException($"{name}/SignalTargetButton is missing.");
        if (chatButtonImage == null || chatButtonPressVisual == null || chatButton == null)
            throw new MissingReferenceException($"{name}/ChatButton is missing.");
    }
}
