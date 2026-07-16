using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

/// <summary>
/// Renders immutable Messages snapshots and emits semantic interaction gestures.
/// </summary>
public sealed class MessagesWindowView : MonoBehaviour
{
    [SerializeField]
    private UIWindow windowShell;

    [SerializeField]
    private RawImage overlayFrameImage;

    [SerializeField]
    private MessagesCommandBarView commandBar;

    [SerializeField]
    private MessagesIndexPanelView indexPanel;

    [SerializeField]
    private MessagesDetailPanelView detailPanel;

    private bool initialized;

    /// <summary>
    /// Raised when the semantic chat command is requested.
    /// </summary>
    public event Action<MessagesWindowView> ChatRequested;

    /// <summary>
    /// Raised when the owning strategy window should close.
    /// </summary>
    public event Action<MessagesWindowView> CloseRequested;

    /// <summary>
    /// Raised when an index row requests the strategy context menu.
    /// </summary>
    public event Action<MessagesWindowView, PointerEventData> ContextRequested;

    /// <summary>
    /// Raised when a command control requests its configured press sound.
    /// </summary>
    public event Action ControlPressed;

    /// <summary>
    /// Raised when the view is destroyed so its controller can release subscriptions.
    /// </summary>
    public event Action<MessagesWindowView> Destroyed;

    /// <summary>
    /// Raised when the selected message should be displayed.
    /// </summary>
    public event Action<MessagesWindowView> DisplayRequested;

    /// <summary>
    /// Raised when the message index should be displayed.
    /// </summary>
    public event Action<MessagesWindowView> IndexRequested;

    /// <summary>
    /// Raised when selection should move toward newer source indexes.
    /// </summary>
    public event Action<MessagesWindowView> MessageNextRequested;

    /// <summary>
    /// Raised when selection should move toward older source indexes.
    /// </summary>
    public event Action<MessagesWindowView> MessagePreviousRequested;

    /// <summary>
    /// Raised when the selected messages should be removed.
    /// </summary>
    public event Action<MessagesWindowView> MessageRemovalRequested;

    /// <summary>
    /// Raised when a message row is activated.
    /// </summary>
    public event Action<MessagesWindowView, string> MessageRowActivated;

    /// <summary>
    /// Raised when a message row is selected.
    /// </summary>
    public event Action<MessagesWindowView, string> MessageRowSelected;

    /// <summary>
    /// Raised when every message in the active tab should be selected.
    /// </summary>
    public event Action<MessagesWindowView> MessageSelectAllRequested;

    /// <summary>
    /// Raised when the selected message target should be opened.
    /// </summary>
    public event Action<MessagesWindowView> MessageTargetRequested;

    /// <summary>
    /// Raised when notification policy should be toggled for the active tab.
    /// </summary>
    public event Action<MessagesWindowView> NotificationToggleRequested;

    /// <summary>
    /// Raised when another semantic Messages tab is requested.
    /// </summary>
    internal event Action<MessagesWindowView, MessagesTab> TabRequested;

    /// <summary>
    /// Applies a complete Messages presentation snapshot to the authored hierarchy.
    /// </summary>
    /// <param name="data">The projected Messages presentation.</param>
    public void Render(MessagesWindowRenderData data)
    {
        EnsureInitialized();
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        UILayout.SetSourcePosition(
            transform as RectTransform,
            data.FramePosition.x,
            data.FramePosition.y
        );
        UILayout.SetImageTexture(overlayFrameImage, data.OverlayFrameTexture);
        commandBar.Render(data.CommandBar);

        if (data.DetailVisible)
        {
            indexPanel.Hide();
            detailPanel.Render(data.DetailPanel);
        }
        else
        {
            detailPanel.Hide();
            indexPanel.Render(data.IndexPanel);
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Verifies authored references and binds focused child views.
    /// </summary>
    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>
    /// Notifies the controller that this view can release its subscriptions and state.
    /// </summary>
    private void OnDestroy()
    {
        UnbindChildViews();
        Destroyed?.Invoke(this);
    }

    /// <summary>
    /// Translates active-window keyboard input into semantic Messages gestures.
    /// </summary>
    private void Update()
    {
        if (!CanNavigateRows())
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.upArrowKey.wasPressedThisFrame)
        {
            MessageNextRequested?.Invoke(this);
            return;
        }

        if (keyboard.downArrowKey.wasPressedThisFrame)
        {
            MessagePreviousRequested?.Invoke(this);
            return;
        }

        bool commandPressed =
            IsPressed(keyboard.leftCtrlKey)
            || IsPressed(keyboard.rightCtrlKey)
            || IsPressed(keyboard.leftCommandKey)
            || IsPressed(keyboard.rightCommandKey);

        if (commandPressed && keyboard.aKey.wasPressedThisFrame)
            MessageSelectAllRequested?.Invoke(this);
        else if (
            keyboard.deleteKey.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame
        )
            MessageRemovalRequested?.Invoke(this);
    }

    /// <summary>
    /// Initializes child event routing exactly once.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized)
            return;

        VerifyReferences();
        indexPanel.Initialize(CanNavigateRows, transform);
        commandBar.ChatRequested += HandleChatRequested;
        commandBar.CloseRequested += HandleCloseRequested;
        commandBar.ControlPressed += HandleControlPressed;
        commandBar.DisplayRequested += HandleDisplayRequested;
        commandBar.IndexRequested += HandleIndexRequested;
        commandBar.SignalRequested += HandleSignalRequested;
        commandBar.TargetRequested += HandleTargetRequested;
        indexPanel.ContextRequested += HandleContextRequested;
        indexPanel.RemoveSelectedRequested += HandleRemoveSelectedRequested;
        indexPanel.RowClicked += HandleRowClicked;
        indexPanel.RowDoubleClicked += HandleRowDoubleClicked;
        indexPanel.SelectAllRequested += HandleSelectAllRequested;
        indexPanel.TabRequested += HandleTabRequested;
        detailPanel.NextRequested += HandleNextRequested;
        detailPanel.PreviousRequested += HandlePreviousRequested;
        initialized = true;
    }

    /// <summary>
    /// Detaches every child-view event owned by this window.
    /// </summary>
    private void UnbindChildViews()
    {
        if (!initialized)
            return;

        if (commandBar != null)
        {
            commandBar.ChatRequested -= HandleChatRequested;
            commandBar.CloseRequested -= HandleCloseRequested;
            commandBar.ControlPressed -= HandleControlPressed;
            commandBar.DisplayRequested -= HandleDisplayRequested;
            commandBar.IndexRequested -= HandleIndexRequested;
            commandBar.SignalRequested -= HandleSignalRequested;
            commandBar.TargetRequested -= HandleTargetRequested;
        }

        if (indexPanel != null)
        {
            indexPanel.ContextRequested -= HandleContextRequested;
            indexPanel.RemoveSelectedRequested -= HandleRemoveSelectedRequested;
            indexPanel.RowClicked -= HandleRowClicked;
            indexPanel.RowDoubleClicked -= HandleRowDoubleClicked;
            indexPanel.SelectAllRequested -= HandleSelectAllRequested;
            indexPanel.TabRequested -= HandleTabRequested;
        }

        if (detailPanel != null)
        {
            detailPanel.NextRequested -= HandleNextRequested;
            detailPanel.PreviousRequested -= HandlePreviousRequested;
        }
    }

    /// <summary>
    /// Emits the semantic chat command.
    /// </summary>
    private void HandleChatRequested()
    {
        ChatRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits the semantic close command.
    /// </summary>
    private void HandleCloseRequested()
    {
        CloseRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a row context-menu gesture.
    /// </summary>
    /// <param name="eventData">The pointer event that requested the context menu.</param>
    private void HandleContextRequested(PointerEventData eventData)
    {
        ContextRequested?.Invoke(this, eventData);
    }

    /// <summary>
    /// Forwards a command-control press gesture.
    /// </summary>
    private void HandleControlPressed()
    {
        ControlPressed?.Invoke();
    }

    /// <summary>
    /// Emits the semantic display command.
    /// </summary>
    private void HandleDisplayRequested()
    {
        DisplayRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits the semantic index command.
    /// </summary>
    private void HandleIndexRequested()
    {
        IndexRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits the semantic next-message command.
    /// </summary>
    private void HandleNextRequested()
    {
        MessageNextRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits the semantic previous-message command.
    /// </summary>
    private void HandlePreviousRequested()
    {
        MessagePreviousRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits the semantic remove-selected command.
    /// </summary>
    private void HandleRemoveSelectedRequested()
    {
        MessageRemovalRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a semantic row-selection gesture.
    /// </summary>
    /// <param name="messageId">The selected source message identifier.</param>
    private void HandleRowClicked(string messageId)
    {
        MessageRowSelected?.Invoke(this, messageId);
    }

    /// <summary>
    /// Emits a semantic row-activation gesture.
    /// </summary>
    /// <param name="messageId">The activated source message identifier.</param>
    private void HandleRowDoubleClicked(string messageId)
    {
        MessageRowActivated?.Invoke(this, messageId);
    }

    /// <summary>
    /// Emits the semantic select-all command.
    /// </summary>
    private void HandleSelectAllRequested()
    {
        MessageSelectAllRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits the semantic notification-toggle command.
    /// </summary>
    private void HandleSignalRequested()
    {
        NotificationToggleRequested?.Invoke(this);
    }

    /// <summary>
    /// Emits a semantic tab-selection gesture.
    /// </summary>
    /// <param name="tab">The requested semantic Messages tab.</param>
    private void HandleTabRequested(MessagesTab tab)
    {
        TabRequested?.Invoke(this, tab);
    }

    /// <summary>
    /// Emits the semantic target-navigation command.
    /// </summary>
    private void HandleTargetRequested()
    {
        MessageTargetRequested?.Invoke(this);
    }

    /// <summary>
    /// Returns whether an optional keyboard control is currently pressed.
    /// </summary>
    /// <param name="key">The optional keyboard control.</param>
    /// <returns>True when the key is pressed.</returns>
    private static bool IsPressed(KeyControl key)
    {
        return key != null && key.isPressed;
    }

    /// <summary>
    /// Returns whether the owning Messages window currently accepts keyboard navigation.
    /// </summary>
    /// <returns>True when the window is active.</returns>
    private bool CanNavigateRows()
    {
        return windowShell.ActiveWindow;
    }

    /// <summary>
    /// Verifies every authored root and child-view reference before use.
    /// </summary>
    private void VerifyReferences()
    {
        if (windowShell == null)
            throw new MissingReferenceException($"{name}/WindowShell is missing.");
        if (overlayFrameImage == null)
            throw new MissingReferenceException($"{name}/OverlayFrameImage is missing.");
        if (commandBar == null)
            throw new MissingReferenceException($"{name}/CommandBar is missing.");
        if (indexPanel == null)
            throw new MissingReferenceException($"{name}/IndexPanel is missing.");
        if (detailPanel == null)
            throw new MissingReferenceException($"{name}/DetailPanel is missing.");
    }
}
