using System;
using Rebellion.Gameplay;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Owns application Options above an isolated battle while retaining the battle's pause state.
/// </summary>
public sealed class TacticalCombatOptionsHost : IDisposable
{
    private readonly TacticalCombatController _combat;
    private readonly TacticalCombatSession _session;
    private readonly Canvas _battleCanvas;
    private readonly GameObject _root;
    private readonly UIWindowManager _windows;
    private readonly IOptionsMenuPresenter _options;
    private bool _dirty;
    private bool _presenting;
    private bool _battleWasVisible;
    private bool _disposed;

    /// <summary>
    /// Reports when the application menu takes or releases modal input ownership.
    /// </summary>
    public event Action<bool> VisibilityChanged;

    /// <summary>
    /// Builds an independent modal desktop using the application's existing window scale and bounds.
    /// </summary>
    /// <param name="combat">The battle's input owner.</param>
    /// <param name="session">The encounter whose pause state is retained.</param>
    /// <param name="view">The battle view hidden while Options is open.</param>
    /// <param name="sourceDesktop">The application's authored window desktop.</param>
    /// <param name="contentAssets">The content source for the Options window.</param>
    /// <param name="createOptions">Creates the application menu against this host's desktop and invalidation callback.</param>
    public TacticalCombatOptionsHost(
        TacticalCombatController combat,
        TacticalCombatSession session,
        TacticalCombatView view,
        RectTransform sourceDesktop,
        IContentAssetSource contentAssets,
        Func<Transform, UIWindowManager, Action, IOptionsMenuPresenter> createOptions
    )
    {
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sourceDesktop == null)
            throw new ArgumentNullException(nameof(sourceDesktop));
        if (createOptions == null)
            throw new ArgumentNullException(nameof(createOptions));
        CanvasScaler sourceScaler = sourceDesktop.GetComponentInParent<CanvasScaler>();
        if (sourceScaler == null)
            throw new MissingReferenceException(
                "The application window desktop has no canvas scaler."
            );
        _battleCanvas = view.Viewport.GetComponentInParent<Canvas>();
        _root = new GameObject(
            "Combat application menu",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        _root.transform.SetParent(view.transform, false);
        try
        {
            Canvas canvas = _root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _battleCanvas.sortingOrder + 1;
            CanvasScaler scaler = _root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = sourceScaler.uiScaleMode;
            scaler.referenceResolution = sourceScaler.referenceResolution;
            scaler.screenMatchMode = sourceScaler.screenMatchMode;
            scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
            scaler.dynamicPixelsPerUnit = sourceScaler.dynamicPixelsPerUnit;
            scaler.scaleFactor = sourceScaler.scaleFactor;

            GameObject blocker = new GameObject(
                "Modal input blocker",
                typeof(RectTransform),
                typeof(Image)
            );
            blocker.transform.SetParent(_root.transform, false);
            RectTransform blockerRect = blocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = Color.clear;

            GameObject desktop = new GameObject(
                "Window desktop",
                typeof(RectTransform),
                typeof(UIWindowManager)
            );
            desktop.transform.SetParent(_root.transform, false);
            RectTransform desktopRect = desktop.GetComponent<RectTransform>();
            desktopRect.anchorMin =
                desktopRect.anchorMax =
                desktopRect.pivot =
                    new Vector2(0.5f, 0.5f);
            desktopRect.sizeDelta = sourceDesktop.rect.size;
            _windows = desktop.GetComponent<UIWindowManager>();
            _windows.SetContentSource(contentAssets);
            _options =
                createOptions(desktop.transform, _windows, MarkDirty)
                ?? throw new InvalidOperationException(
                    "The application Options presenter was not created."
                );
            _windows.WindowClosed += HandleWindowClosed;
            _combat.MenuControlRequested += HandleControl;
            _root.SetActive(false);
        }
        catch
        {
            ReleaseRoot();
            throw;
        }
    }

    /// <summary>
    /// Opens Options with input isolated from both battle and campaign controls.
    /// </summary>
    public void Open()
    {
        if (_disposed)
            return;
        if (!_presenting)
        {
            _battleWasVisible = _battleCanvas.enabled;
            _presenting = true;
            VisibilityChanged?.Invoke(true);
        }
        if (!_session.IsPaused)
            _session.IsPaused = true;
        _combat.SetApplicationMenuOpen(true);
        _battleCanvas.enabled = false;
        _root.SetActive(true);
        try
        {
            _options.Open();
            _dirty = true;
            Render();
        }
        catch
        {
            RestoreBattle();
            throw;
        }
    }

    /// <summary>
    /// Renders invalidated menu state and restores the battle after the menu closes.
    /// </summary>
    public void Render()
    {
        if (_disposed || !_presenting)
            return;
        if (!_options.IsOpen)
        {
            RestoreBattle();
            return;
        }
        if (!_dirty)
            return;
        _dirty = false;
        _options.RenderWindows();
    }

    /// <summary>
    /// Routes only application Options requests to this modal owner.
    /// </summary>
    /// <param name="control">The requested battle-menu control.</param>
    private void HandleControl(TacticalCombatControl control)
    {
        if (control == TacticalCombatControl.OpenOptions)
            Open();
    }

    /// <summary>
    /// Invalidates the menu after a settings or binding change.
    /// </summary>
    private void MarkDirty() => _dirty = true;

    /// <summary>
    /// Releases modal presentation after the registered Options window closes.
    /// </summary>
    /// <param name="_">The window removed from this desktop.</param>
    private void HandleWindowClosed(UIWindow _)
    {
        if (!_options.IsOpen)
            RestoreBattle();
    }

    /// <summary>
    /// Re-shows the retained battle without changing its pause state.
    /// </summary>
    private void RestoreBattle()
    {
        if (!_presenting)
            return;
        _presenting = false;
        _root.SetActive(false);
        if (_battleCanvas != null)
            _battleCanvas.enabled = _battleWasVisible;
        _combat.SetApplicationMenuOpen(false);
        VisibilityChanged?.Invoke(false);
    }

    /// <summary>
    /// Releases the owned modal canvas in both runtime and authoring contexts.
    /// </summary>
    private void ReleaseRoot()
    {
        if (Application.isPlaying)
            UnityEngine.Object.Destroy(_root);
        else
            UnityEngine.Object.DestroyImmediate(_root);
    }

    /// <summary>
    /// Unsubscribes modal routing and releases the application Options surface and desktop.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _combat.MenuControlRequested -= HandleControl;
        _windows.WindowClosed -= HandleWindowClosed;
        _options.Dispose();
        RestoreBattle();
        _disposed = true;
        ReleaseRoot();
    }
}
