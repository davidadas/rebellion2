using System;
using System.Collections.Generic;
using Rebellion.Multiplayer;
using UnityEngine;

/// <summary>
/// Owns the modal session-end notice and its acknowledged return to the main menu.
/// </summary>
public sealed class MultiplayerSessionEndController : MonoBehaviour, ICancelable, IDisposable
{
    [SerializeField]
    private UIWindowManager windowManager;

    [SerializeField]
    private ConfirmationDialogView dialogTemplate;

    [SerializeField]
    private bool isMainMenu;

    private MultiplayerSessionEndNotice notice;
    private Action endGame;
    private Func<Action<Exception>, bool> requestMainMenu;
    private Func<IDisposable> acquireSuppression;
    private CancelStack cancelStack;
    private IDisposable shortcutSuppression;
    private UIWindow noticeWindow;
    private ConfirmationDialogView dialog;
    private bool noticeOpen;
    private bool transitionPending;
    private bool disposed;
    private int transitionVersion;

    /// <summary>
    /// Connects the scene's authored notice to persistent application state and navigation.
    /// </summary>
    private void Start()
    {
        if (notice != null || disposed)
            return;
        AppBootstrap bootstrap = AppBootstrap.EnsureExists();
        Initialize(
            MultiplayerCoordinator.EnsureExists().SessionEndNotice,
            windowManager,
            dialogTemplate,
            isMainMenu,
            () => bootstrap.GetRuntime()?.EndGame(),
            failure => bootstrap.LoadScene("MainMenu", failure),
            () => bootstrap.GetInputController().SuppressShortcuts(),
            bootstrap.GetCancelStack(),
            bootstrap.GetContentAssets()
        );
    }

    /// <summary>
    /// Binds one scene's notice presentation to explicit application and navigation owners.
    /// </summary>
    /// <param name="sessionEndNotice">The application-owned pending acknowledgment.</param>
    /// <param name="manager">The existing scene window registry.</param>
    /// <param name="template">The authored native dialog with its window shell.</param>
    /// <param name="alreadyInMainMenu">Whether acknowledgment only needs to dismiss the notice.</param>
    /// <param name="endActiveGame">Ends the current runtime before requesting navigation.</param>
    /// <param name="loadMainMenu">Requests navigation and receives any later loading failure.</param>
    /// <param name="suppressShortcuts">Acquires modal ownership of application shortcuts.</param>
    /// <param name="cancellation">The application cancellation router.</param>
    /// <param name="contentAssets">The installed artwork applied before the dialog becomes visible.</param>
    public void Initialize(
        MultiplayerSessionEndNotice sessionEndNotice,
        UIWindowManager manager,
        ConfirmationDialogView template,
        bool alreadyInMainMenu,
        Action endActiveGame,
        Func<Action<Exception>, bool> loadMainMenu,
        Func<IDisposable> suppressShortcuts,
        CancelStack cancellation,
        IContentAssetSource contentAssets
    )
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(MultiplayerSessionEndController));
        if (notice != null)
            throw new InvalidOperationException(
                "The session-end notice already has a scene owner."
            );
        notice = sessionEndNotice ?? throw new ArgumentNullException(nameof(sessionEndNotice));
        windowManager = manager ?? throw new ArgumentNullException(nameof(manager));
        dialogTemplate = template ?? throw new ArgumentNullException(nameof(template));
        endGame = endActiveGame ?? throw new ArgumentNullException(nameof(endActiveGame));
        requestMainMenu = loadMainMenu ?? throw new ArgumentNullException(nameof(loadMainMenu));
        acquireSuppression =
            suppressShortcuts ?? throw new ArgumentNullException(nameof(suppressShortcuts));
        cancelStack = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        if (contentAssets == null)
            throw new ArgumentNullException(nameof(contentAssets));
        isMainMenu = alreadyInMainMenu;
        windowManager.SetContentSource(contentAssets);
        notice.Changed += HandleNoticeChanged;
        windowManager.ModalOpened += HandleModalOpened;
        HandleNoticeChanged();
    }

    /// <summary>
    /// Consumes cancellation while the session-end modal owns the scene.
    /// </summary>
    /// <returns>True when the acknowledgment notice remains open.</returns>
    public bool TryCancel()
    {
        return !disposed && noticeOpen;
    }

    /// <summary>
    /// Releases scene-owned callbacks and controls without acknowledging persistent application state.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        transitionVersion++;
        if (notice != null)
            notice.Changed -= HandleNoticeChanged;
        if (windowManager != null)
            windowManager.ModalOpened -= HandleModalOpened;
        CloseNotice();
        if (dialog != null)
        {
            dialog.Confirmed -= HandleAcknowledged;
            if (Application.isPlaying)
                Destroy(dialog.gameObject);
            else
                DestroyImmediate(dialog.gameObject);
        }
        dialog = null;
        noticeWindow = null;
    }

    /// <summary>
    /// Releases the notice owner when its scene is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        Dispose();
    }

    /// <summary>
    /// Opens or releases the modal only when its persistent acknowledgment state changes.
    /// </summary>
    private void HandleNoticeChanged()
    {
        if (disposed)
            return;
        if (!notice.IsPending)
        {
            CloseNotice();
            return;
        }
        if (noticeOpen)
            return;

        if (dialog == null)
            CreateDialog();
        else
            windowManager.Register(noticeWindow, false);
        noticeOpen = true;
        shortcutSuppression = acquireSuppression();
        cancelStack.Register(this);
        dialog.ShowAcknowledgment(MultiplayerSessionEndNotice.Message);
    }

    /// <summary>
    /// Creates the scene's native dialog once and retains it for navigation failure recovery.
    /// </summary>
    private void CreateDialog()
    {
        RectTransform templateRect = (RectTransform)dialogTemplate.transform;
        noticeWindow = windowManager.CreateWindow(
            dialogTemplate,
            transform,
            "SessionEndDialog",
            0,
            0,
            Vector2Int.RoundToInt(templateRect.sizeDelta),
            true,
            true,
            false,
            false,
            out dialog
        );
        RectTransform rect = (RectTransform)dialog.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        dialog.Confirmed += HandleAcknowledged;
    }

    /// <summary>
    /// Keeps the pending session acknowledgment above modal windows opened by later scene events.
    /// </summary>
    /// <param name="opened">The newly registered modal window.</param>
    private void HandleModalOpened(UIWindow opened)
    {
        if (disposed || !noticeOpen || noticeWindow == null || opened == noticeWindow)
            return;
        List<UIWindow> ordered = new List<UIWindow>(windowManager.Windows);
        if (!ordered.Remove(noticeWindow))
            return;
        ordered.Add(noticeWindow);
        windowManager.SetStackOrder(ordered);
    }

    /// <summary>
    /// Returns to the menu once and keeps an unaccepted transition available for acknowledgment.
    /// </summary>
    private void HandleAcknowledged()
    {
        if (disposed || transitionPending || notice?.IsPending != true)
            return;
        if (isMainMenu)
        {
            notice.Acknowledge();
            return;
        }

        transitionPending = true;
        int version = ++transitionVersion;
        try
        {
            endGame();
            if (disposed || this == null || version != transitionVersion)
                return;
            bool accepted = requestMainMenu(failure => HandleTransitionFailure(version, failure));
            if (disposed || version != transitionVersion || !transitionPending)
                return;
            if (accepted)
                notice.Acknowledge();
            else
                transitionPending = false;
        }
        catch (Exception exception)
        {
            HandleTransitionFailure(version, exception);
        }
    }

    /// <summary>
    /// Restores a failed navigation attempt only while its originating scene still owns the callback.
    /// </summary>
    /// <param name="version">The navigation attempt that issued the callback.</param>
    /// <param name="exception">The failure that kept the current scene active.</param>
    private void HandleTransitionFailure(int version, Exception exception)
    {
        if (disposed || this == null || version != transitionVersion)
            return;
        transitionPending = false;
        notice.ShowOpponentDeparture();
        HandleNoticeChanged();
        Debug.LogException(exception);
    }

    /// <summary>
    /// Hides and unregisters the cached dialog while releasing its modal input ownership.
    /// </summary>
    private void CloseNotice()
    {
        noticeOpen = false;
        cancelStack?.Unregister(this);
        shortcutSuppression?.Dispose();
        shortcutSuppression = null;
        dialog?.Hide();
        if (windowManager != null && noticeWindow != null)
            windowManager.Unregister(noticeWindow);
    }
}
