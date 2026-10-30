using System;
using Rebellion.Persistence;
using UnityEngine;

/// <summary>
/// Presents application-owned storage failures using the shared acknowledgment dialog.
/// </summary>
public sealed class PersistenceFailureNoticeController : MonoBehaviour, ICancelable, IDisposable
{
    [SerializeField]
    private UIWindowManager windowManager;

    [SerializeField]
    private ConfirmationDialogView dialogTemplate;

    private PersistenceFailureNotice _notice;
    private UIWindow _window;
    private ConfirmationDialogView _dialog;
    private Func<IDisposable> _suppressShortcuts;
    private IDisposable _shortcutSuppression;
    private CancelStack _cancelStack;
    private bool _disposed;

    /// <summary>
    /// Binds this scene's shared dialog to the application's pending storage notice.
    /// </summary>
    private void Start()
    {
        if (_notice != null || _disposed)
            return;
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        Initialize(
            bootstrap.PersistenceNotice,
            windowManager,
            dialogTemplate,
            () => bootstrap.GetInputController().SuppressShortcuts(),
            bootstrap.GetCancelStack(),
            bootstrap.GetContentAssets()
        );
    }

    /// <summary>
    /// Connects scene presentation to explicit notice, artwork, window, and input owners.
    /// </summary>
    /// <param name="notice">The application-owned acknowledgment state.</param>
    /// <param name="manager">The scene's existing window registry.</param>
    /// <param name="template">The shared authored acknowledgment dialog.</param>
    /// <param name="suppressShortcuts">Acquires modal ownership of application shortcuts.</param>
    /// <param name="cancelStack">The application cancellation router.</param>
    /// <param name="contentAssets">The installed dialog artwork.</param>
    public void Initialize(
        PersistenceFailureNotice notice,
        UIWindowManager manager,
        ConfirmationDialogView template,
        Func<IDisposable> suppressShortcuts,
        CancelStack cancelStack,
        IContentAssetSource contentAssets
    )
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(PersistenceFailureNoticeController));
        if (_notice != null)
            throw new InvalidOperationException(
                "The persistence notice already has a scene owner."
            );
        _notice = notice ?? throw new ArgumentNullException(nameof(notice));
        windowManager = manager ?? throw new ArgumentNullException(nameof(manager));
        dialogTemplate = template ?? throw new ArgumentNullException(nameof(template));
        _suppressShortcuts =
            suppressShortcuts ?? throw new ArgumentNullException(nameof(suppressShortcuts));
        _cancelStack = cancelStack ?? throw new ArgumentNullException(nameof(cancelStack));
        windowManager.SetContentSource(
            contentAssets ?? throw new ArgumentNullException(nameof(contentAssets))
        );
        _notice.Changed += HandleNoticeChanged;
        HandleNoticeChanged();
    }

    /// <summary>
    /// Consumes cancellation while the acknowledgment dialog owns input.
    /// </summary>
    /// <returns>Whether the pending notice currently owns a modal.</returns>
    public bool TryCancel() => !_disposed && _window != null;

    /// <summary>
    /// Releases scene controls without acknowledging a notice retained by the application.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (_notice != null)
            _notice.Changed -= HandleNoticeChanged;
        CloseNotice();
    }

    /// <summary>
    /// Releases scene-owned subscriptions when the presenter is destroyed.
    /// </summary>
    private void OnDestroy() => Dispose();

    /// <summary>
    /// Creates or dismisses the shared modal only when acknowledgment state changes.
    /// </summary>
    private void HandleNoticeChanged()
    {
        if (_disposed)
            return;
        if (!_notice.IsPending)
        {
            CloseNotice();
            return;
        }
        if (_dialog == null)
        {
            _window = windowManager.CreateWindow(
                dialogTemplate,
                transform,
                "PersistenceFailureDialog",
                0,
                0,
                Vector2Int.RoundToInt(((RectTransform)dialogTemplate.transform).sizeDelta),
                true,
                true,
                false,
                false,
                out _dialog
            );
            RectTransform rectangle = (RectTransform)_dialog.transform;
            rectangle.anchorMin = Vector2.zero;
            rectangle.anchorMax = Vector2.one;
            rectangle.offsetMin = Vector2.zero;
            rectangle.offsetMax = Vector2.zero;
            _dialog.Confirmed += HandleAcknowledged;
            _shortcutSuppression = _suppressShortcuts();
            _cancelStack.Register(this);
        }
        _dialog.ShowAcknowledgment(
            _notice.Operation.Value switch
            {
                PersistenceOperation.Save => "The game could not be saved. Please try again.",
                PersistenceOperation.Load =>
                    "The saved game could not be loaded. Please try again.",
                _ => throw new ArgumentOutOfRangeException(nameof(_notice.Operation)),
            }
        );
    }

    /// <summary>
    /// Consumes the notice through its application owner after the user acknowledges it.
    /// </summary>
    private void HandleAcknowledged() => _notice.Acknowledge();

    /// <summary>
    /// Releases the modal, its input ownership, and its registered window together.
    /// </summary>
    private void CloseNotice()
    {
        _cancelStack?.Unregister(this);
        _shortcutSuppression?.Dispose();
        _shortcutSuppression = null;
        if (_dialog != null)
            _dialog.Confirmed -= HandleAcknowledged;
        if (_window != null && windowManager != null)
            windowManager.DestroyWindow(_window);
        _dialog = null;
        _window = null;
    }
}
