using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the Options menu.
/// </summary>
public sealed class OptionsMenuController : ICancelable, IDisposable
{
    // Window.
    private readonly OptionsMenuView _prefab;
    private readonly Transform _windowParent;
    private readonly UIWindowManager _windowManager;
    private readonly Func<Vector2Int> _getWindowPosition;
    private readonly Action<UIWindow> _closeWindow;
    private readonly Action _markDirty;

    // Settings.
    private readonly OptionsSettingsSession _settingsSession;
    private readonly OptionsBindingEditor _bindingEditor;

    // Save Games.
    private readonly List<OptionsSaveSlot> _saveSlots = new List<OptionsSaveSlot>();
    private IOptionsSaveStore _saveStore;
    private IOptionsSaveWriter _saveWriter;
    private int _selectedSlot = -1;
    private bool _saveSlotsLoaded;

    // Menu State.
    private IOptionsMenuHostActions _hostActions;
    private Action _pendingConfirmAction;
    private bool _pendingConfirmKeepsVisible;
    private OptionsMenuView _view;
    private UIWindow _window;
    private OptionsMenuTab _activeTab = OptionsMenuTab.Graphics;
    private bool _disposed;

    /// <summary>
    /// Creates an Options menu controller.
    /// </summary>
    /// <param name="prefab">The Options menu prefab.</param>
    /// <param name="windowParent">The parent for the Options menu.</param>
    /// <param name="windowManager">The window manager.</param>
    /// <param name="getWindowPosition">Returns the Options menu position.</param>
    /// <param name="closeWindow">Closes a registered window.</param>
    /// <param name="userSettings">The user-settings store.</param>
    /// <param name="displayManager">The display manager.</param>
    /// <param name="audioManager">The audio manager.</param>
    /// <param name="inputManager">The input manager.</param>
    /// <param name="markDirty">Marks the menu data as changed.</param>
    public OptionsMenuController(
        OptionsMenuView prefab,
        Transform windowParent,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
        UserSettingsManager userSettings,
        DisplayManager displayManager,
        AudioManager audioManager,
        InputManager inputManager,
        Action markDirty
    )
    {
        _prefab = prefab;
        _windowParent = windowParent ?? throw new ArgumentNullException(nameof(windowParent));
        _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        _getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        _closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        InputManager bindings =
            inputManager ?? throw new ArgumentNullException(nameof(inputManager));
        _settingsSession = new OptionsSettingsSession(
            userSettings,
            displayManager,
            audioManager,
            bindings
        );
        _bindingEditor = new OptionsBindingEditor(bindings);
        _bindingEditor.Changed += _settingsSession.MarkInputChanged;
        _bindingEditor.PresentationChanged += _markDirty;
        _bindingEditor.ConflictRequested += HandleBindingConflictRequested;
    }

    /// <summary>
    /// Returns whether the Options menu is open.
    /// </summary>
    public bool IsOpen => _window != null;

    /// <summary>
    /// Sets the game and save actions used by the Options menu.
    /// </summary>
    /// <param name="menuActions">The game actions.</param>
    /// <param name="optionsSaveStore">The save game actions.</param>
    /// <param name="optionsSaveWriter">The save writing actions.</param>
    public void Initialize(
        IOptionsMenuHostActions menuActions,
        IOptionsSaveStore optionsSaveStore,
        IOptionsSaveWriter optionsSaveWriter = null
    )
    {
        _hostActions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
        _saveStore = optionsSaveStore ?? throw new ArgumentNullException(nameof(optionsSaveStore));
        _saveWriter = optionsSaveWriter;
    }

    /// <summary>
    /// Opens or focuses the Options menu.
    /// </summary>
    public void Open()
    {
        EnsureInitialized();
        if (_window != null)
        {
            _windowManager.Focus(_window);
            return;
        }

        if (_prefab == null)
        {
            Debug.LogWarning(
                "OptionsMenu prefab is not assigned; run Build Options Menu UI to generate it."
            );
            return;
        }

        _settingsSession.Begin();
        _bindingEditor.Rebuild();
        _activeTab = OptionsMenuTab.Graphics;
        _selectedSlot = -1;
        _saveSlotsLoaded = false;

        Vector2Int position = _getWindowPosition();
        _window = _windowManager.CreateWindow(
            _prefab,
            _windowParent,
            "OptionsMenu",
            position.x,
            position.y,
            GetPrefabSize(),
            true,
            true,
            false,
            false,
            out _view
        );
        BindView(_view);
        _hostActions.PauseForOptions();
        _markDirty();
    }

    /// <summary>
    /// Closes the Options menu.
    /// </summary>
    public void Close()
    {
        if (_window == null)
            return;

        _pendingConfirmAction = null;
        _pendingConfirmKeepsVisible = false;
        _bindingEditor.CancelRebind();
        if (_bindingEditor.HasPendingConflict)
            _bindingEditor.ResolveConflict(false);
        _bindingEditor.SetTextEntryActive(false);
        UIWindow closing = _window;
        _window = null;
        _view = null;
        _hostActions.ResumeFromOptions();
        _closeWindow(closing);
        _markDirty();
    }

    /// <summary>
    /// Closes the Options menu and restores unapplied settings.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_settingsSession.IsDirty)
            _settingsSession.Revert();
        Close();
        _bindingEditor.Changed -= _settingsSession.MarkInputChanged;
        _bindingEditor.PresentationChanged -= _markDirty;
        _bindingEditor.ConflictRequested -= HandleBindingConflictRequested;
        _bindingEditor.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Updates the open Options menu.
    /// </summary>
    public void RenderWindows()
    {
        if (_window == null || _view == null)
            return;

        _view.Render(BuildRenderData(_window));
    }

    /// <summary>
    /// Closes the Options menu from a cancel request.
    /// </summary>
    /// <returns>True when the overlay was open and closed.</returns>
    public bool TryCancel()
    {
        if (_window == null)
            return false;

        // Close the confirmation dialog first.
        if (_pendingConfirmAction != null || _bindingEditor.HasPendingConflict)
        {
            HandleConfirmDeclined();
            return true;
        }

        HandleBackToGame();
        return true;
    }

    /// <summary>
    /// Creates the data displayed by the Options menu.
    /// </summary>
    /// <param name="shell">The Options menu window.</param>
    /// <returns>The Options menu data.</returns>
    private OptionsMenuRenderData BuildRenderData(UIWindow shell)
    {
        return new OptionsMenuRenderData(
            shell.X,
            shell.Y,
            _activeTab,
            _settingsSession.ResolutionLabel,
            _settingsSession.FullScreenLabel,
            _settingsSession.GetTacticalStates(),
            _settingsSession.GetVolumes(),
            _bindingEditor.Rows,
            _saveSlots,
            _selectedSlot,
            _saveWriter != null,
            _saveStore != null,
            _hostActions.CanReturnToGame,
            _hostActions.CanReturnToMainMenu,
            _bindingEditor.ListeningRow,
            _bindingEditor.ListeningSecondary
        );
    }

    /// <summary>
    /// Adds the Options menu listeners.
    /// </summary>
    /// <param name="target">The view to bind.</param>
    private void BindView(OptionsMenuView target)
    {
        target.TabSelected += HandleTabSelected;
        target.ResumeRequested += HandleBackToGame;
        target.SaveRequested += HandleSaveRequested;
        target.LoadRequested += HandleLoadRequested;
        target.SlotSelected += HandleSlotSelected;
        target.SlotRenamed += HandleSlotRenamed;
        target.SlotDeleteRequested += HandleSlotDeleteRequested;
        target.RenameEditingChanged += HandleRenameEditingChanged;
        target.ApplyRequested += HandleApply;
        target.DefaultsRequested += HandleDefaults;
        target.ConfirmAccepted += HandleConfirmAccepted;
        target.ConfirmDeclined += HandleConfirmDeclined;
        target.RebindRequested += HandleRebindRequested;
        target.MainMenuRequested += HandleMainMenuRequested;
        target.QuitRequested += HandleQuitRequested;
        target.TacticalToggleRequested += HandleTacticalToggle;
        target.ResolutionStepRequested += HandleResolutionStep;
        target.FullScreenStepRequested += HandleFullScreenStep;
        target.VolumeChanged += HandleVolumeChanged;
        target.Destroyed += HandleViewDestroyed;
    }

    /// <summary>
    /// Selects an Options menu page.
    /// </summary>
    /// <param name="tab">The selected page.</param>
    private void HandleTabSelected(OptionsMenuTab tab)
    {
        if (tab == _activeTab)
            return;

        if (_settingsSession.IsDirty)
        {
            RequestConfirm(
                "Discard unsaved changes?",
                () =>
                {
                    _settingsSession.Revert();
                    _bindingEditor.Rebuild();
                    SwitchTab(tab);
                }
            );
            return;
        }

        SwitchTab(tab);
    }

    /// <summary>
    /// Activates a page and lazily loads its data.
    /// </summary>
    /// <param name="tab">The page to show.</param>
    private void SwitchTab(OptionsMenuTab tab)
    {
        _activeTab = tab;
        if (tab == OptionsMenuTab.SaveLoad && !_saveSlotsLoaded)
        {
            RefreshSaveSlots();
            _saveSlotsLoaded = true;
        }

        _markDirty();
    }

    /// <summary>
    /// Selects a save game row.
    /// </summary>
    /// <param name="slot">The clicked row index.</param>
    private void HandleSlotSelected(int slot)
    {
        if (slot < 0 || slot >= _saveSlots.Count)
            return;

        _selectedSlot = _saveSlots[slot].IsCreateNew ? -1 : slot;
        _markDirty();
    }

    /// <summary>
    /// Saves a change to a save game name.
    /// </summary>
    /// <param name="slot">The edited row index.</param>
    /// <param name="newName">The typed name.</param>
    /// <param name="submitted">Whether Return submitted the edit.</param>
    private void HandleSlotRenamed(int slot, string newName, bool submitted)
    {
        if (slot < 0 || slot >= _saveSlots.Count || string.IsNullOrWhiteSpace(newName))
            return;

        if (_saveSlots[slot].IsCreateNew)
        {
            _saveWriter?.CreateNamedSave(newName);
            _selectedSlot = -1;
        }
        else
        {
            string fileName = _saveSlots[slot].FileName;
            if (submitted && _saveWriter != null)
                _saveWriter.OverwriteSave(fileName, newName);
            else
                _saveStore.RenameSave(fileName, newName);
            RefreshSaveSlots(fileName);
            _markDirty();
            return;
        }

        RefreshSaveSlots();
        _markDirty();
    }

    /// <summary>
    /// Confirms and deletes an existing save.
    /// </summary>
    /// <param name="slot">The row index to delete.</param>
    private void HandleSlotDeleteRequested(int slot)
    {
        if (slot < 0 || slot >= _saveSlots.Count || _saveSlots[slot].IsCreateNew)
            return;

        string fileName = _saveSlots[slot].FileName;
        string label = _saveSlots[slot].Name;
        RequestConfirm(
            $"Delete \"{label}\"?",
            () =>
            {
                _saveStore.DeleteSave(fileName);
                _selectedSlot = -1;
                RefreshSaveSlots();
                _markDirty();
            }
        );
    }

    /// <summary>
    /// Overwrites the selected existing save with the running game.
    /// </summary>
    private void HandleSaveRequested()
    {
        if (_saveWriter == null || !IsSelectedExistingSave())
            return;

        string fileName = _saveSlots[_selectedSlot].FileName;
        _saveWriter.OverwriteSave(fileName, _saveSlots[_selectedSlot].Name);
        RefreshSaveSlots(fileName);
        _markDirty();
    }

    /// <summary>
    /// Loads the selected existing save, then closes the overlay on success.
    /// </summary>
    private void HandleLoadRequested()
    {
        if (_saveStore == null || !IsSelectedExistingSave())
            return;

        if (_saveStore.LoadSave(_saveSlots[_selectedSlot].FileName))
            Close();
    }

    /// <summary>
    /// Gets whether an existing save game is selected.
    /// </summary>
    /// <returns>True when an existing save is selected.</returns>
    private bool IsSelectedExistingSave()
    {
        return _selectedSlot >= 0
            && _selectedSlot < _saveSlots.Count
            && !_saveSlots[_selectedSlot].IsCreateNew;
    }

    /// <summary>
    /// Disables game shortcuts while a save name is being edited.
    /// </summary>
    /// <param name="editing">True while a text field is focused for editing.</param>
    private void HandleRenameEditingChanged(bool editing)
    {
        _bindingEditor.SetTextEntryActive(editing);
    }

    /// <summary>
    /// Rebuilds the cached save-slot list from the host.
    /// </summary>
    private void RefreshSaveSlots(string selectedFileName = null)
    {
        _saveSlots.Clear();
        _saveSlots.AddRange(_saveStore.GetSaveSlots());
        _selectedSlot = -1;
        if (string.IsNullOrEmpty(selectedFileName))
            return;

        for (int index = 0; index < _saveSlots.Count; index++)
        {
            if (
                string.Equals(
                    _saveSlots[index].FileName,
                    selectedFileName,
                    StringComparison.Ordinal
                )
            )
            {
                _selectedSlot = index;
                return;
            }
        }
    }

    /// <summary>
    /// Persists the currently previewed settings to disk.
    /// </summary>
    private void HandleApply()
    {
        _settingsSession.Commit();
        _markDirty();
    }

    /// <summary>
    /// Confirms before restoring the current page to its default settings.
    /// </summary>
    private void HandleDefaults()
    {
        switch (_activeTab)
        {
            case OptionsMenuTab.Graphics:
                RequestConfirm("Reset display settings to their defaults?", ApplyActiveDefaults);
                break;
            case OptionsMenuTab.Audio:
                RequestConfirm("Reset volume to defaults?", ApplyActiveDefaults);
                break;
            case OptionsMenuTab.Controls:
                RequestConfirm("Reset all key bindings to their defaults?", ApplyActiveDefaults);
                break;
        }
    }

    /// <summary>
    /// Restores the default settings for the current page.
    /// </summary>
    private void ApplyActiveDefaults()
    {
        _settingsSession.RestoreDefaults(_activeTab);
        _bindingEditor.Rebuild();
        _markDirty();
    }

    /// <summary>
    /// Returns to the game after resolving any unsaved settings.
    /// </summary>
    private void HandleBackToGame()
    {
        if (!_settingsSession.IsDirty)
        {
            Close();
            return;
        }

        RequestConfirm(
            "Discard unsaved settings and return to the game?",
            () =>
            {
                _settingsSession.Revert();
                _bindingEditor.Rebuild();
                Close();
            }
        );
    }

    /// <summary>
    /// Confirms returning to the main menu, warning when settings are unsaved.
    /// </summary>
    private void HandleMainMenuRequested()
    {
        RequestConfirm(
            _settingsSession.IsDirty
                ? "Return to the Main Menu? Unsaved settings will be lost."
                : "Return to the Main Menu?",
            () => ExitWithoutSaving(_hostActions.ReturnToMainMenu),
            true
        );
    }

    /// <summary>
    /// Confirms quitting to desktop, warning when settings are unsaved.
    /// </summary>
    private void HandleQuitRequested()
    {
        RequestConfirm(
            _settingsSession.IsDirty
                ? "Quit to desktop? Unsaved settings will be lost."
                : "Quit to desktop?",
            () => ExitWithoutSaving(_hostActions.QuitApplication),
            true
        );
    }

    /// <summary>
    /// Reverts pending settings before invoking a scene or application exit.
    /// </summary>
    /// <param name="exit">The exit operation to invoke.</param>
    private void ExitWithoutSaving(Action exit)
    {
        if (_settingsSession.IsDirty)
            _settingsSession.Revert();
        exit?.Invoke();
    }

    /// <summary>
    /// Opens a confirmation dialog for an action.
    /// </summary>
    /// <param name="message">The prompt text.</param>
    /// <param name="onConfirmed">The action to run on acceptance.</param>
    /// <param name="keepVisibleWhileExecuting">Whether the prompt remains during a scene exit.</param>
    private void RequestConfirm(
        string message,
        Action onConfirmed,
        bool keepVisibleWhileExecuting = false
    )
    {
        _pendingConfirmAction = onConfirmed;
        _pendingConfirmKeepsVisible = keepVisibleWhileExecuting;
        _view?.ShowConfirm(message);
    }

    /// <summary>
    /// Runs and clears the pending confirmed action.
    /// </summary>
    private void HandleConfirmAccepted()
    {
        if (_bindingEditor.HasPendingConflict)
        {
            _view?.HideConfirm();
            _bindingEditor.ResolveConflict(true);
            return;
        }

        Action confirmed = _pendingConfirmAction;
        bool keepVisible = _pendingConfirmKeepsVisible;
        _pendingConfirmAction = null;
        _pendingConfirmKeepsVisible = false;
        if (!keepVisible)
            _view?.HideConfirm();
        confirmed?.Invoke();
    }

    /// <summary>
    /// Dismisses the confirmation prompt without acting.
    /// </summary>
    private void HandleConfirmDeclined()
    {
        _view?.HideConfirm();
        if (_bindingEditor.HasPendingConflict)
        {
            _bindingEditor.ResolveConflict(false);
            return;
        }

        _pendingConfirmAction = null;
        _pendingConfirmKeepsVisible = false;
    }

    /// <summary>
    /// Starts changing a key binding.
    /// </summary>
    /// <param name="row">The binding row index.</param>
    /// <param name="secondary">Whether the secondary column was clicked.</param>
    private void HandleRebindRequested(int row, bool secondary)
    {
        _bindingEditor.BeginRebind(row, secondary);
    }

    /// <summary>
    /// Displays the confirmation prompt for a conflicting binding assignment.
    /// </summary>
    /// <param name="message">The conflict prompt to display.</param>
    private void HandleBindingConflictRequested(string message)
    {
        _view?.ShowConfirm(message);
    }

    /// <summary>
    /// Toggles a detail option and marks settings dirty.
    /// </summary>
    /// <param name="option">The toggled option.</param>
    private void HandleTacticalToggle(UserTacticalOption option)
    {
        _settingsSession.ToggleTactical(option);
        _markDirty();
    }

    /// <summary>
    /// Steps the selected resolution and applies it immediately.
    /// </summary>
    /// <param name="delta">The step direction.</param>
    private void HandleResolutionStep(int delta)
    {
        _settingsSession.StepResolution(delta);
        _markDirty();
    }

    /// <summary>
    /// Steps the display mode and applies it immediately.
    /// </summary>
    /// <param name="delta">The step direction.</param>
    private void HandleFullScreenStep(int delta)
    {
        _settingsSession.StepFullScreen(delta);
        _markDirty();
    }

    /// <summary>
    /// Applies a live volume change for one channel.
    /// </summary>
    /// <param name="channel">The channel index (0..4).</param>
    /// <param name="value">The normalized volume.</param>
    private void HandleVolumeChanged(int channel, float value)
    {
        _settingsSession.SetVolume(channel, value);
        _markDirty();
    }

    /// <summary>
    /// Clears the destroyed Options menu view.
    /// </summary>
    /// <param name="destroyed">The destroyed view.</param>
    private void HandleViewDestroyed(OptionsMenuView destroyed)
    {
        if (destroyed == null)
            return;

        destroyed.TabSelected -= HandleTabSelected;
        destroyed.ResumeRequested -= HandleBackToGame;
        destroyed.SaveRequested -= HandleSaveRequested;
        destroyed.LoadRequested -= HandleLoadRequested;
        destroyed.SlotSelected -= HandleSlotSelected;
        destroyed.SlotRenamed -= HandleSlotRenamed;
        destroyed.SlotDeleteRequested -= HandleSlotDeleteRequested;
        destroyed.RenameEditingChanged -= HandleRenameEditingChanged;
        destroyed.ApplyRequested -= HandleApply;
        destroyed.DefaultsRequested -= HandleDefaults;
        destroyed.ConfirmAccepted -= HandleConfirmAccepted;
        destroyed.ConfirmDeclined -= HandleConfirmDeclined;
        destroyed.RebindRequested -= HandleRebindRequested;
        destroyed.MainMenuRequested -= HandleMainMenuRequested;
        destroyed.QuitRequested -= HandleQuitRequested;
        destroyed.TacticalToggleRequested -= HandleTacticalToggle;
        destroyed.ResolutionStepRequested -= HandleResolutionStep;
        destroyed.FullScreenStepRequested -= HandleFullScreenStep;
        destroyed.VolumeChanged -= HandleVolumeChanged;
        destroyed.Destroyed -= HandleViewDestroyed;
        if (ReferenceEquals(destroyed, _view))
        {
            bool wasOpen = _window != null;
            _view = null;
            _window = null;
            if (wasOpen)
                _hostActions?.ResumeFromOptions();
        }
    }

    /// <summary>
    /// Returns the size of the Options menu prefab.
    /// </summary>
    /// <returns>The Options menu size.</returns>
    private Vector2Int GetPrefabSize()
    {
        RectTransform rect = (RectTransform)_prefab.transform;
        return new Vector2Int(
            Mathf.RoundToInt(rect.sizeDelta.x),
            Mathf.RoundToInt(rect.sizeDelta.y)
        );
    }

    /// <summary>
    /// Checks that the Options menu has been initialized.
    /// </summary>
    private void EnsureInitialized()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OptionsMenuController));
        if (_hostActions == null || _saveStore == null)
            throw new InvalidOperationException(
                $"{nameof(OptionsMenuController)} must be initialized before use."
            );
    }
}
