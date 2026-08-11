using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Session-level actions the Options overlay delegates to its host scene.
/// </summary>
public interface IOptionsMenuActions
{
    /// <summary>Gets whether saving is currently available.</summary>
    bool CanSave { get; }

    /// <summary>Gets whether loading is currently available.</summary>
    bool CanLoad { get; }

    /// <summary>Gets whether a running game exists to return to.</summary>
    bool CanReturnToGame { get; }

    /// <summary>Gets whether returning to the Main Menu is possible (false when already there).</summary>
    bool CanReturnToMainMenu { get; }

    /// <summary>Pauses the host while the Options overlay is open.</summary>
    void PauseForOptions();

    /// <summary>Resumes the host after the Options overlay closes.</summary>
    void ResumeFromOptions();

    /// <summary>Enumerates the Save/Load rows: the create-new row followed by existing saves.</summary>
    /// <returns>The current save rows.</returns>
    IReadOnlyList<OptionsSaveSlot> GetSaveSlots();

    /// <summary>Creates a new save from the running game.</summary>
    void CreateNewSave();

    /// <summary>Creates a new save from the running game with a player-chosen display name.</summary>
    /// <param name="displayName">The display name for the new save.</param>
    void CreateNamedSave(string displayName);

    /// <summary>Overwrites an existing save file with the running game.</summary>
    /// <param name="fileName">The save file to overwrite.</param>
    /// <param name="displayName">The display name to preserve or apply.</param>
    void OverwriteSave(string fileName, string displayName);

    /// <summary>Loads the game held in a save file.</summary>
    /// <param name="fileName">The save file to load.</param>
    /// <returns>True when a game was loaded.</returns>
    bool LoadSave(string fileName);

    /// <summary>Deletes a save file.</summary>
    /// <param name="fileName">The save file to delete.</param>
    void DeleteSave(string fileName);

    /// <summary>Renames a save's display name.</summary>
    /// <param name="fileName">The save file to rename.</param>
    /// <param name="displayName">The new display name.</param>
    void RenameSave(string fileName, string displayName);

    /// <summary>Returns to the main menu.</summary>
    void ReturnToMainMenu();

    /// <summary>Quits the application.</summary>
    void QuitApplication();
}

/// <summary>
/// Owns the in-game Options overlay: modal lifetime, live settings edits, and the
/// read-only key-binding listing. Registered with the cancel stack to close on Escape.
/// </summary>
public sealed class OptionsMenuController : ICancelable
{
    private const int MasterChannel = 0;
    private const int MusicChannel = 1;
    private const int SfxChannel = 2;
    private const int AmbienceChannel = 3;
    private const int VideoChannel = 4;

    private static readonly int[] FullScreenModes =
    {
        (int)FullScreenMode.ExclusiveFullScreen,
        (int)FullScreenMode.FullScreenWindow,
        (int)FullScreenMode.Windowed,
    };

    private readonly OptionsMenuView prefab;
    private readonly Transform windowParent;
    private readonly UIWindowManager windowManager;
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Action<UIWindow> closeWindow;
    private readonly Action markDirty;
    private readonly UserSettingsManager userSettings;
    private readonly AudioManager audioManager;
    private readonly InputManager inputManager;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private readonly List<OptionsBindingRow> bindings = new List<OptionsBindingRow>();
    private readonly List<(InputAction action, int primaryIndex, int secondaryIndex)> rebindTargets =
        new List<(InputAction, int, int)>();
    private readonly List<OptionsSaveSlot> saveSlots = new List<OptionsSaveSlot>();
    private readonly Dictionary<UserTacticalOption, bool> snapshotTactical =
        new Dictionary<UserTacticalOption, bool>();

    private float[] snapshotVolumes;
    private int snapshotResolutionWidth;
    private int snapshotResolutionHeight;
    private int snapshotFullScreenMode;

    private IOptionsMenuActions actions;
    private Action pendingConfirmAction;
    private bool keepConfirmVisibleOnAccept;
    private InputActionRebindingExtensions.RebindingOperation activeRebind;
    private bool rebindingActive;
    private InputAction conflictOldAction;
    private int conflictOldIndex;
    private InputAction conflictNewAction;
    private int conflictNewIndex;
    private bool awaitingConflict;
    private int listeningRow = -1;
    private bool listeningSecondary;
    private OptionsMenuView view;
    private UIWindow window;
    private OptionsMenuTab activeTab = OptionsMenuTab.Graphics;
    private int resolutionIndex;
    private int selectedSlot = -1;
    private bool saveSlotsLoaded;
    private bool settingsDirty;
    private int capturedRebindModifiers;

    /// <summary>
    /// Creates an Options overlay controller. The overlay is scene-agnostic: it hosts the same
    /// authored prefab under any window manager, so StrategyView, the Main Menu, and other scenes
    /// can all present it.
    /// </summary>
    /// <param name="prefab">The authored Options overlay prefab to instantiate.</param>
    /// <param name="windowParent">The modal layer that hosts the created overlay window.</param>
    /// <param name="windowManager">Owns overlay-window creation and registration.</param>
    /// <param name="getWindowPosition">Returns the source-space Options placement.</param>
    /// <param name="closeWindow">Closes a registered window.</param>
    /// <param name="userSettings">The user-settings store.</param>
    /// <param name="audioManager">The audio manager for live volume changes.</param>
    /// <param name="inputManager">The input manager exposing key bindings.</param>
    /// <param name="markDirty">Invalidates host presentation after changes.</param>
    public OptionsMenuController(
        OptionsMenuView prefab,
        Transform windowParent,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
        UserSettingsManager userSettings,
        AudioManager audioManager,
        InputManager inputManager,
        Action markDirty
    )
    {
        this.prefab = prefab;
        this.windowParent = windowParent ?? throw new ArgumentNullException(nameof(windowParent));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
        this.audioManager = audioManager ?? throw new ArgumentNullException(nameof(audioManager));
        this.inputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>Gets whether the overlay is currently open.</summary>
    public bool IsOpen => window != null;

    /// <summary>
    /// Supplies host actions after the strategy controller graph is constructed.
    /// </summary>
    /// <param name="menuActions">The host-specific Options actions.</param>
    public void Initialize(IOptionsMenuActions menuActions)
    {
        actions = menuActions ?? throw new ArgumentNullException(nameof(menuActions));
    }

    /// <summary>
    /// Opens the Options overlay, or focuses it when already open.
    /// </summary>
    public void Open()
    {
        EnsureInitialized();
        if (window != null)
        {
            windowManager.Focus(window);
            return;
        }

        if (prefab == null)
        {
            Debug.LogWarning(
                "OptionsMenu prefab is not assigned; run Build Options Menu UI to generate it."
            );
            return;
        }

        RebuildResolutions();
        RebuildBindings();
        CaptureSnapshot();
        activeTab = OptionsMenuTab.Graphics;
        selectedSlot = -1;
        saveSlotsLoaded = false;
        settingsDirty = false;

        Vector2Int position = getWindowPosition();
        window = windowManager.CreateWindow(
            prefab,
            windowParent,
            "OptionsMenu",
            position.x,
            position.y,
            GetPrefabSize(),
            true,
            true,
            false,
            false,
            out view
        );
        BindView(view);
        actions.PauseForOptions();
        markDirty();
    }

    /// <summary>
    /// Closes the Options overlay. Settings are committed only through Apply, so any unsaved
    /// changes are resolved (kept-and-applied or discarded) before this is reached.
    /// </summary>
    public void Close()
    {
        if (window == null)
            return;

        pendingConfirmAction = null;
        keepConfirmVisibleOnAccept = false;
        // Safety: ensure gameplay bindings are re-enabled if the overlay is closed mid-edit.
        HandleRenameEditingChanged(false);
        UIWindow closing = window;
        window = null;
        view = null;
        actions.ResumeFromOptions();
        closeWindow(closing);
        markDirty();
    }

    /// <summary>
    /// Renders the open Options overlay, if any.
    /// </summary>
    public void RenderWindows()
    {
        if (window == null || view == null)
            return;

        view.Render(BuildRenderData(window));
    }

    /// <summary>
    /// Closes the overlay in response to a cancel request.
    /// </summary>
    /// <returns>True when the overlay was open and closed.</returns>
    public bool TryCancel()
    {
        if (window == null)
            return false;

        // Escape first dismisses an open confirmation prompt, then behaves like Back to Game.
        if (pendingConfirmAction != null)
        {
            HandleConfirmDeclined();
            return true;
        }

        HandleBackToGame();
        return true;
    }

    /// <summary>
    /// Builds the current presentation snapshot from live settings and cached bindings.
    /// </summary>
    /// <param name="shell">The window shell supplying source-space position.</param>
    /// <returns>The immutable presentation snapshot.</returns>
    private OptionsMenuRenderData BuildRenderData(UIWindow shell)
    {
        UserVideoSettings video = userSettings.Settings.Video;
        UserAudioSettings audio = userSettings.Settings.Audio;

        Dictionary<UserTacticalOption, bool> tactical =
            new Dictionary<UserTacticalOption, bool>();
        foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
            tactical[option] = video.IsEnabled(option);

        float[] volumes =
        {
            audio.MasterVolume,
            audio.MusicVolume,
            audio.SfxVolume,
            audio.AmbienceVolume,
            audio.VideoVolume,
        };

        return new OptionsMenuRenderData(
            shell.X,
            shell.Y,
            activeTab,
            GetResolutionLabel(video),
            GetFullScreenLabel(video.FullScreenMode),
            tactical,
            volumes,
            bindings,
            saveSlots,
            selectedSlot,
            actions.CanSave,
            actions.CanLoad,
            actions.CanReturnToGame,
            actions.CanReturnToMainMenu,
            listeningRow,
            listeningSecondary
        );
    }

    /// <summary>
    /// Subscribes the controller to one Options view exactly once.
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
    /// Selects a page. When the current page has uncommitted changes, the player is warned and must
    /// discard them before leaving; declining keeps them on the current page.
    /// </summary>
    /// <param name="tab">The selected page.</param>
    private void HandleTabSelected(OptionsMenuTab tab)
    {
        if (tab == activeTab)
            return;

        if (settingsDirty)
        {
            RequestConfirm(
                "Discard unsaved changes?",
                () =>
                {
                    RevertToSnapshot();
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
        activeTab = tab;
        if (tab == OptionsMenuTab.SaveLoad && !saveSlotsLoaded)
        {
            RefreshSaveSlots();
            saveSlotsLoaded = true;
        }

        markDirty();
    }

    /// <summary>
    /// Handles a single Save/Load row click by selecting it. The create-new row is not committed
    /// here; the save is written only when the player confirms the typed name.
    /// </summary>
    /// <param name="slot">The clicked row index.</param>
    private void HandleSlotSelected(int slot)
    {
        if (slot < 0 || slot >= saveSlots.Count)
            return;

        selectedSlot = saveSlots[slot].IsCreateNew ? -1 : slot;
        markDirty();
    }

    /// <summary>
    /// Commits an inline text edit: the create-new row writes a new save with the typed name; an
    /// existing row is renamed, and Return also overwrites it in a running game.
    /// </summary>
    /// <param name="slot">The edited row index.</param>
    /// <param name="newName">The typed name.</param>
    /// <param name="submitted">Whether Return submitted the edit.</param>
    private void HandleSlotRenamed(int slot, string newName, bool submitted)
    {
        if (slot < 0 || slot >= saveSlots.Count || string.IsNullOrWhiteSpace(newName))
            return;

        if (saveSlots[slot].IsCreateNew)
        {
            if (actions.CanSave)
                actions.CreateNamedSave(newName.Trim());
            selectedSlot = -1;
        }
        else
        {
            string displayName = newName.Trim();
            if (submitted && actions.CanSave)
                actions.OverwriteSave(saveSlots[slot].FileName, displayName);
            else
                actions.RenameSave(saveSlots[slot].FileName, displayName);
            selectedSlot = slot;
        }

        RefreshSaveSlots();
        markDirty();
    }

    /// <summary>
    /// Confirms and deletes an existing save.
    /// </summary>
    /// <param name="slot">The row index to delete.</param>
    private void HandleSlotDeleteRequested(int slot)
    {
        if (slot < 0 || slot >= saveSlots.Count || saveSlots[slot].IsCreateNew)
            return;

        string fileName = saveSlots[slot].FileName;
        string label = saveSlots[slot].Name;
        RequestConfirm(
            $"Delete \"{label}\"?",
            () =>
            {
                actions.DeleteSave(fileName);
                selectedSlot = -1;
                RefreshSaveSlots();
                markDirty();
            }
        );
    }

    /// <summary>
    /// Overwrites the selected existing save with the running game.
    /// </summary>
    private void HandleSaveRequested()
    {
        if (!actions.CanSave || !IsSelectedExistingSave())
            return;

        actions.OverwriteSave(
            saveSlots[selectedSlot].FileName,
            saveSlots[selectedSlot].Name
        );
        RefreshSaveSlots();
        markDirty();
    }

    /// <summary>
    /// Loads the selected existing save, then closes the overlay on success.
    /// </summary>
    private void HandleLoadRequested()
    {
        if (!actions.CanLoad || !IsSelectedExistingSave())
            return;

        if (actions.LoadSave(saveSlots[selectedSlot].FileName))
            Close();
    }

    /// <summary>
    /// Gets whether the selected row is an existing save (not the create-new row).
    /// </summary>
    /// <returns>True when an existing save is selected.</returns>
    private bool IsSelectedExistingSave()
    {
        return selectedSlot >= 0
            && selectedSlot < saveSlots.Count
            && !saveSlots[selectedSlot].IsCreateNew;
    }

    /// <summary>
    /// Suppresses gameplay key bindings while inline text is being edited, so typed keys (P, F, 1-9,
    /// etc.) enter the text field instead of triggering their bound commands.
    /// </summary>
    /// <param name="editing">True while a text field is focused for editing.</param>
    private void HandleRenameEditingChanged(bool editing)
    {
        InputActionAsset asset = inputManager?.Asset;
        if (asset == null)
            return;

        foreach (InputActionMap map in asset.actionMaps)
        {
            if (!IsBindableMap(map.name))
                continue;
            if (editing)
                map.Disable();
            else
                map.Enable();
        }
    }

    /// <summary>
    /// Rebuilds the cached save-slot list from the host.
    /// </summary>
    private void RefreshSaveSlots()
    {
        saveSlots.Clear();
        saveSlots.AddRange(actions.GetSaveSlots());
    }

    /// <summary>
    /// Persists the currently previewed settings to disk.
    /// </summary>
    private void HandleApply()
    {
        // Commit through the application settings boundary as well as writing the file. This keeps
        // Apply authoritative if a live preview was ignored or deferred by the current platform.
        userSettings.Apply();
        userSettings.Save();
        settingsDirty = false;
        CaptureSnapshot();
    }

    /// <summary>
    /// Reverts every setting to the snapshot captured when the overlay opened. Used when the
    /// player discards unsaved changes on the way out.
    /// </summary>
    private void RevertToSnapshot()
    {
        UserAudioSettings audio = userSettings.Settings.Audio;
        UserVideoSettings video = userSettings.Settings.Video;
        audio.MasterVolume = snapshotVolumes[MasterChannel];
        audio.MusicVolume = snapshotVolumes[MusicChannel];
        audio.SfxVolume = snapshotVolumes[SfxChannel];
        audio.AmbienceVolume = snapshotVolumes[AmbienceChannel];
        audio.VideoVolume = snapshotVolumes[VideoChannel];
        video.ResolutionWidth = snapshotResolutionWidth;
        video.ResolutionHeight = snapshotResolutionHeight;
        video.FullScreenMode = snapshotFullScreenMode;
        foreach (KeyValuePair<UserTacticalOption, bool> entry in snapshotTactical)
            video.SetEnabled(entry.Key, entry.Value);
        ApplyAllVolumes();
        ApplyResolution();
        RebuildResolutions();
        settingsDirty = false;
        markDirty();
    }

    /// <summary>
    /// Asks the player to confirm resetting the active page's settings to defaults. Defaults are
    /// per-page: each page's button restores only that page's values.
    /// </summary>
    private void HandleDefaults()
    {
        switch (activeTab)
        {
            case OptionsMenuTab.Graphics:
                RequestConfirm("Reset display settings to their defaults?", ApplyGraphicsDefaults);
                break;
            case OptionsMenuTab.Audio:
                RequestConfirm("Reset volume to defaults?", ApplyAudioDefaults);
                break;
            case OptionsMenuTab.Controls:
                RequestConfirm("Reset all key bindings to their defaults?", ApplyControlsDefaults);
                break;
        }
    }

    /// <summary>
    /// Resets the Graphics page (resolution, display mode, and detail toggles) to defaults and
    /// previews them live.
    /// </summary>
    private void ApplyGraphicsDefaults()
    {
        UserVideoSettings video = userSettings.Settings.Video;
        video.ResolutionWidth = 0;
        video.ResolutionHeight = 0;
        video.FullScreenMode = (int)FullScreenMode.ExclusiveFullScreen;
        video.RestoreTacticalDefaults();
        ApplyResolution();
        RebuildResolutions();
        settingsDirty = true;
        markDirty();
    }

    /// <summary>
    /// Resets the Audio page volumes to defaults and previews them live.
    /// </summary>
    private void ApplyAudioDefaults()
    {
        UserAudioSettings audio = userSettings.Settings.Audio;
        audio.MasterVolume = 1f;
        audio.MusicVolume = 1f;
        audio.SfxVolume = 1f;
        audio.AmbienceVolume = 1f;
        audio.VideoVolume = 1f;
        ApplyAllVolumes();
        settingsDirty = true;
        markDirty();
    }

    /// <summary>
    /// Clears every key-binding override, restoring the authored defaults, and persists the result.
    /// </summary>
    private void ApplyControlsDefaults()
    {
        InputActionAsset asset = inputManager?.Asset;
        if (asset == null)
            return;

        asset.RemoveAllBindingOverrides();
        PersistBindings();
        RebuildBindings();
        markDirty();
    }

    /// <summary>
    /// Captures the current settings so Cancel can restore them.
    /// </summary>
    private void CaptureSnapshot()
    {
        UserAudioSettings audio = userSettings.Settings.Audio;
        UserVideoSettings video = userSettings.Settings.Video;
        snapshotVolumes = new[]
        {
            audio.MasterVolume,
            audio.MusicVolume,
            audio.SfxVolume,
            audio.AmbienceVolume,
            audio.VideoVolume,
        };
        snapshotResolutionWidth = video.ResolutionWidth;
        snapshotResolutionHeight = video.ResolutionHeight;
        snapshotFullScreenMode = video.FullScreenMode;
        snapshotTactical.Clear();
        foreach (UserTacticalOption option in Enum.GetValues(typeof(UserTacticalOption)))
            snapshotTactical[option] = video.IsEnabled(option);
    }

    /// <summary>
    /// Pushes the stored volumes to the audio manager.
    /// </summary>
    private void ApplyAllVolumes()
    {
        UserAudioSettings audio = userSettings.Settings.Audio;
        audioManager.SetMasterVolume(audio.MasterVolume);
        audioManager.SetMusicVolume(audio.MusicVolume);
        audioManager.SetSfxVolume(audio.SfxVolume);
        audioManager.SetAmbienceVolume(audio.AmbienceVolume);
        audioManager.SetVideoVolume(audio.VideoVolume);
    }

    /// <summary>
    /// Applies the stored resolution and display mode to the screen.
    /// </summary>
    private void ApplyResolution()
    {
        UserVideoSettings video = userSettings.Settings.Video;
        int width =
            video.ResolutionWidth > 0 ? video.ResolutionWidth : Display.main.systemWidth;
        int height =
            video.ResolutionHeight > 0 ? video.ResolutionHeight : Display.main.systemHeight;
        Screen.SetResolution(width, height, (FullScreenMode)video.FullScreenMode);
    }

    /// <summary>
    /// Closes the overlay in response to Back to Game, confirming first when settings are unsaved.
    /// </summary>
    private void HandleBackToGame()
    {
        if (!settingsDirty)
        {
            Close();
            return;
        }

        RequestConfirm(
            "Discard unsaved settings and return to the game?",
            () =>
            {
                RevertToSnapshot();
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
            settingsDirty
                ? "Return to the Main Menu? Unsaved settings will be lost."
                : "Return to the Main Menu?",
            () => actions.ReturnToMainMenu(),
            true
        );
    }

    /// <summary>
    /// Confirms quitting to desktop, warning when settings are unsaved.
    /// </summary>
    private void HandleQuitRequested()
    {
        RequestConfirm(
            settingsDirty
                ? "Quit to desktop? Unsaved settings will be lost."
                : "Quit to desktop?",
            () => actions.QuitApplication()
        );
    }

    /// <summary>
    /// Shows the confirmation prompt and stores the action to run if the player accepts.
    /// </summary>
    /// <param name="message">The prompt text.</param>
    /// <param name="onConfirmed">The action to run on acceptance.</param>
    private void RequestConfirm(
        string message,
        Action onConfirmed,
        bool keepVisibleOnAccept = false
    )
    {
        pendingConfirmAction = onConfirmed;
        keepConfirmVisibleOnAccept = keepVisibleOnAccept;
        view?.ShowConfirm(message);
    }

    /// <summary>
    /// Runs and clears the pending confirmed action.
    /// </summary>
    private void HandleConfirmAccepted()
    {
        if (awaitingConflict)
        {
            view?.HideConfirm();
            ResolveConflict(true);
            return;
        }

        Action confirmed = pendingConfirmAction;
        pendingConfirmAction = null;
        bool holdForTransition = keepConfirmVisibleOnAccept;
        keepConfirmVisibleOnAccept = false;
        if (holdForTransition)
            view?.HoldConfirmForTransition();
        else
            view?.HideConfirm();
        confirmed?.Invoke();
    }

    /// <summary>
    /// Dismisses the confirmation prompt without acting.
    /// </summary>
    private void HandleConfirmDeclined()
    {
        view?.HideConfirm();
        keepConfirmVisibleOnAccept = false;
        if (awaitingConflict)
        {
            ResolveConflict(false);
            return;
        }

        pendingConfirmAction = null;
    }

    /// <summary>
    /// Begins an interactive rebind of the clicked binding (double-click), or adds a binding when
    /// the slot is empty.
    /// </summary>
    /// <param name="row">The binding row index.</param>
    /// <param name="secondary">Whether the secondary column was clicked.</param>
    private void HandleRebindRequested(int row, bool secondary)
    {
        if (rebindingActive || awaitingConflict || row < 0 || row >= rebindTargets.Count)
            return;

        (InputAction action, int primaryIndex, int secondaryIndex) = rebindTargets[row];
        if (action == null)
            return;

        int bindingIndex = secondary ? secondaryIndex : primaryIndex;
        if (bindingIndex < 0)
        {
            // An empty slot has no binding to rebind; add one (bindings can only be added while the
            // owning map is disabled).
            InputActionMap map = action.actionMap;
            bool wasEnabled = map.enabled;
            map.Disable();
            action.AddBinding(string.Empty, groups: "Keyboard&Mouse");
            bindingIndex = action.bindings.Count - 1;
            if (wasEnabled)
                map.Enable();
        }

        listeningRow = row;
        listeningSecondary = secondary;
        markDirty();
        StartRebind(action, bindingIndex);
    }

    /// <summary>
    /// Starts the interactive rebinding operation, canceling on Escape.
    /// </summary>
    /// <param name="action">The action to rebind.</param>
    /// <param name="bindingIndex">The binding index within the action.</param>
    private void StartRebind(InputAction action, int bindingIndex)
    {
        activeRebind?.Dispose();
        KeyboardChordProcessor.EnsureRegistered();
        capturedRebindModifiers = 0;
        rebindingActive = true;
        action.Disable();
        activeRebind = action
            .PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsExcluding("<Mouse>")
            .OnPotentialMatch(operation => HandlePotentialRebindMatch(operation, action))
            .OnApplyBinding(
                (operation, path) => ApplyRebindPath(action, bindingIndex, path)
            )
            .OnCancel(_ => FinishRebind(action, bindingIndex, false))
            .OnComplete(_ => FinishRebind(action, bindingIndex, true))
            .Start();
    }

    /// <summary>
    /// Ignores a held modifier for command actions and waits until the chord's main key arrives.
    /// Modifier actions themselves may still be rebound directly to Shift, Ctrl, Alt, or Meta.
    /// </summary>
    private static void HandlePotentialRebindMatch(
        InputActionRebindingExtensions.RebindingOperation operation,
        InputAction action
    )
    {
        InputControl control = operation.selectedControl;
        if (KeyboardChordProcessor.IsModifier(control) && !IsModifierAction(action?.name))
        {
            operation.RemoveCandidate(control);
            return;
        }

        operation.Complete();
    }

    /// <summary>Applies the main key and the modifiers held when it was pressed.</summary>
    private void ApplyRebindPath(InputAction action, int bindingIndex, string path)
    {
        InputControl selectedControl = activeRebind?.selectedControl;
        capturedRebindModifiers = KeyboardChordProcessor.IsModifier(selectedControl)
            ? 0
            : KeyboardChordProcessor.GetPressedModifiers(Keyboard.current);

        InputBinding target = action.bindings[bindingIndex];
        if (target.isComposite)
        {
            TryApplyCompositeChord(action, bindingIndex, path);
            return;
        }

        action.ApplyBindingOverride(
            bindingIndex,
            new InputBinding
            {
                overridePath = path,
                overrideProcessors = KeyboardChordProcessor.GetProcessorOverride(
                    capturedRebindModifiers
                ),
            }
        );
    }

    /// <summary>Updates the modifier and button parts of an existing one-modifier composite.</summary>
    private bool TryApplyCompositeChord(InputAction action, int compositeIndex, string buttonPath)
    {
        string modifierPath = GetSingleModifierPath(capturedRebindModifiers);
        if (string.IsNullOrEmpty(modifierPath))
            return false;

        int modifierIndex = -1;
        int buttonIndex = -1;
        for (int i = compositeIndex + 1; i < action.bindings.Count; i++)
        {
            InputBinding part = action.bindings[i];
            if (!part.isPartOfComposite)
                break;
            if (string.Equals(part.name, "Modifier", StringComparison.OrdinalIgnoreCase))
                modifierIndex = i;
            else if (string.Equals(part.name, "Button", StringComparison.OrdinalIgnoreCase))
                buttonIndex = i;
        }

        if (modifierIndex < 0 || buttonIndex < 0)
            return false;

        action.ApplyBindingOverride(modifierIndex, modifierPath);
        action.ApplyBindingOverride(buttonIndex, buttonPath);
        return true;
    }

    private static string GetSingleModifierPath(int modifierMask)
    {
        return modifierMask switch
        {
            KeyboardChordProcessor.Shift => "<Keyboard>/shift",
            KeyboardChordProcessor.Control => "<Keyboard>/ctrl",
            KeyboardChordProcessor.Alt => "<Keyboard>/alt",
            KeyboardChordProcessor.Meta => "<Keyboard>/leftMeta",
            _ => null,
        };
    }

    private static bool IsModifierAction(string actionName)
    {
        return actionName
            is "MultiSelectModifier" or "RangeSelectModifier" or "AlternateSelectModifier";
    }

    /// <summary>
    /// Completes or cancels a rebind, warning on a conflict and persisting on success.
    /// </summary>
    /// <param name="action">The rebound action.</param>
    /// <param name="bindingIndex">The rebound binding index.</param>
    /// <param name="completed">Whether the rebind completed (vs. canceled by Escape).</param>
    private void FinishRebind(InputAction action, int bindingIndex, bool completed)
    {
        activeRebind?.Dispose();
        activeRebind = null;
        action.Enable();
        rebindingActive = false;
        listeningRow = -1;
        listeningSecondary = false;

        if (!completed)
        {
            RebuildBindings();
            markDirty();
            return;
        }

        (InputAction other, int otherIndex) = FindConflict(action, bindingIndex);
        if (other != null)
        {
            conflictOldAction = other;
            conflictOldIndex = otherIndex;
            conflictNewAction = action;
            conflictNewIndex = bindingIndex;
            awaitingConflict = true;
            view?.ShowConfirm(
                $"That input is already bound to \"{Humanize(other.name)}\". Move it here and clear the old binding?"
            );
            return;
        }

        PersistBindings();
        RebuildBindings();
        markDirty();
    }

    /// <summary>
    /// Resolves a binding conflict: clearing the old binding, or reverting the new one.
    /// </summary>
    /// <param name="clearOld">True to clear the old conflicting binding; false to revert the new.</param>
    private void ResolveConflict(bool clearOld)
    {
        awaitingConflict = false;
        if (clearOld)
            conflictOldAction?.ApplyBindingOverride(conflictOldIndex, string.Empty);
        else
            conflictNewAction?.RemoveBindingOverride(conflictNewIndex);

        conflictOldAction = null;
        conflictNewAction = null;
        PersistBindings();
        RebuildBindings();
        markDirty();
    }

    /// <summary>
    /// Finds another bindable action already using the rebound binding's path.
    /// </summary>
    /// <param name="rebound">The rebound action.</param>
    /// <param name="reboundIndex">The rebound binding index.</param>
    /// <returns>The conflicting action and binding index, or (null, -1).</returns>
    private (InputAction action, int index) FindConflict(InputAction rebound, int reboundIndex)
    {
        string path = rebound.bindings[reboundIndex].effectivePath;
        if (string.IsNullOrEmpty(path))
            return (null, -1);

        InputActionAsset asset = inputManager.Asset;
        foreach (InputActionMap map in asset.actionMaps)
        {
            if (!IsBindableMap(map.name))
                continue;
            foreach (InputAction action in map.actions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action.bindings[i].isPartOfComposite)
                        continue;
                    if (action == rebound && i == reboundIndex)
                        continue;
                    if (action.bindings[i].effectivePath == path)
                        return (action, i);
                }
            }
        }

        return (null, -1);
    }

    /// <summary>
    /// Persists the current binding overrides to user settings.
    /// </summary>
    private void PersistBindings()
    {
        InputManager manager = inputManager;
        if (manager == null)
            return;

        userSettings.Settings.Input.BindingOverridesJson = manager.SaveBindingOverrides();
        userSettings.Save();
    }

    /// <summary>
    /// Toggles a detail option and marks settings dirty.
    /// </summary>
    /// <param name="option">The toggled option.</param>
    private void HandleTacticalToggle(UserTacticalOption option)
    {
        UserVideoSettings video = userSettings.Settings.Video;
        video.SetEnabled(option, !video.IsEnabled(option));
        settingsDirty = true;
        markDirty();
    }

    /// <summary>
    /// Steps the selected resolution and applies it immediately.
    /// </summary>
    /// <param name="delta">The step direction.</param>
    private void HandleResolutionStep(int delta)
    {
        if (resolutions.Count == 0)
            return;

        resolutionIndex = ((resolutionIndex + delta) % resolutions.Count + resolutions.Count)
            % resolutions.Count;
        Vector2Int resolution = resolutions[resolutionIndex];
        UserVideoSettings video = userSettings.Settings.Video;
        video.ResolutionWidth = resolution.x;
        video.ResolutionHeight = resolution.y;
        Screen.SetResolution(resolution.x, resolution.y, (FullScreenMode)video.FullScreenMode);
        settingsDirty = true;
        markDirty();
    }

    /// <summary>
    /// Steps the display mode and applies it immediately.
    /// </summary>
    /// <param name="delta">The step direction.</param>
    private void HandleFullScreenStep(int delta)
    {
        UserVideoSettings video = userSettings.Settings.Video;
        int current = Array.IndexOf(FullScreenModes, video.FullScreenMode);
        if (current < 0)
            current = 0;
        int next = ((current + delta) % FullScreenModes.Length + FullScreenModes.Length)
            % FullScreenModes.Length;
        video.FullScreenMode = FullScreenModes[next];
        int width =
            video.ResolutionWidth > 0 ? video.ResolutionWidth : Display.main.systemWidth;
        int height =
            video.ResolutionHeight > 0 ? video.ResolutionHeight : Display.main.systemHeight;
        Screen.SetResolution(width, height, (FullScreenMode)video.FullScreenMode);
        settingsDirty = true;
        markDirty();
    }

    /// <summary>
    /// Applies a live volume change for one channel.
    /// </summary>
    /// <param name="channel">The channel index (0..4).</param>
    /// <param name="value">The normalized volume.</param>
    private void HandleVolumeChanged(int channel, float value)
    {
        UserAudioSettings audio = userSettings.Settings.Audio;
        switch (channel)
        {
            case MasterChannel:
                audio.MasterVolume = value;
                audioManager.SetMasterVolume(value);
                break;
            case MusicChannel:
                audio.MusicVolume = value;
                audioManager.SetMusicVolume(value);
                break;
            case SfxChannel:
                audio.SfxVolume = value;
                audioManager.SetSfxVolume(value);
                break;
            case AmbienceChannel:
                audio.AmbienceVolume = value;
                audioManager.SetAmbienceVolume(value);
                break;
            case VideoChannel:
                audio.VideoVolume = value;
                audioManager.SetVideoVolume(value);
                break;
            default:
                return;
        }

        settingsDirty = true;
        markDirty();
    }

    /// <summary>
    /// Releases subscriptions and state for a destroyed Options view.
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
        if (ReferenceEquals(destroyed, view))
        {
            view = null;
            window = null;
        }
    }

    /// <summary>
    /// Persists settings to disk when a change is pending.
    /// </summary>
    private void PersistIfDirty()
    {
        if (!settingsDirty)
            return;

        userSettings.Save();
        settingsDirty = false;
    }

    /// <summary>
    /// Rebuilds the available-resolution list and selects the current entry.
    /// </summary>
    private void RebuildResolutions()
    {
        resolutions.Clear();
        foreach (Resolution resolution in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(resolution.width, resolution.height);
            if (!resolutions.Contains(size))
                resolutions.Add(size);
        }

        if (resolutions.Count == 0)
            resolutions.Add(new Vector2Int(Screen.width, Screen.height));

        UserVideoSettings video = userSettings.Settings.Video;
        Vector2Int current = new Vector2Int(
            video.ResolutionWidth > 0 ? video.ResolutionWidth : Screen.width,
            video.ResolutionHeight > 0 ? video.ResolutionHeight : Screen.height
        );
        resolutionIndex = Mathf.Max(0, resolutions.IndexOf(current));
    }

    /// <summary>
    /// Rebuilds the key-binding listing from the input asset, showing only the player-bindable
    /// gameplay maps and using short key names.
    /// </summary>
    private void RebuildBindings()
    {
        bindings.Clear();
        rebindTargets.Clear();
        InputActionAsset asset = inputManager.Asset;
        if (asset == null)
            return;

        foreach (InputActionMap map in asset.actionMaps)
        {
            if (!IsBindableMap(map.name))
                continue;

            List<OptionsBindingRow> mapRows = new List<OptionsBindingRow>();
            List<(InputAction, int, int)> mapTargets = new List<(InputAction, int, int)>();
            foreach (InputAction action in map.actions)
            {
                if (!IsBindableAction(action.name))
                    continue;
                (string primary, string secondary) = FormatKeys(action);
                mapRows.Add(new OptionsBindingRow(Humanize(action.name), primary, secondary));
                (int primaryIndex, int secondaryIndex) = GetTopLevelBindingIndices(action);
                mapTargets.Add((action, primaryIndex, secondaryIndex));
            }

            if (mapRows.Count == 0)
                continue;

            bindings.Add(
                new OptionsBindingRow(map.name.ToUpperInvariant(), string.Empty, string.Empty, true)
            );
            bindings.AddRange(mapRows);
            rebindTargets.AddRange(mapTargets);
        }
    }

    /// <summary>
    /// Gets the binding indices of an action's first two top-level bindings (or -1 when absent).
    /// </summary>
    /// <param name="action">The input action.</param>
    /// <returns>The primary and secondary binding indices.</returns>
    private static (int primary, int secondary) GetTopLevelBindingIndices(InputAction action)
    {
        int primary = -1;
        int secondary = -1;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (action.bindings[i].isPartOfComposite)
                continue;
            if (primary < 0)
                primary = i;
            else if (secondary < 0)
            {
                secondary = i;
                break;
            }
        }

        return (primary, secondary);
    }

    /// <summary>
    /// Gets whether an action map holds player-bindable gameplay actions. The UI, Window, and
    /// Cutscene maps are internal and not shown in Controls.
    /// </summary>
    /// <param name="map">The action-map name.</param>
    /// <returns>True when the map is bindable.</returns>
    private static bool IsBindableMap(string map)
    {
        return map == "Global" || map == "Strategy";
    }

    /// <summary>
    /// Gets whether an action is player-bindable. Cancel/Settings (Escape) is reserved.
    /// </summary>
    /// <param name="action">The action name.</param>
    /// <returns>True when the action is bindable.</returns>
    private static bool IsBindableAction(string action)
    {
        return action != "CancelOrSettings";
    }

    /// <summary>
    /// Formats an action's first two top-level bindings as short, upper-cased key names
    /// (e.g. CTRL, SHIFT, F4) for the primary and secondary columns.
    /// </summary>
    /// <param name="action">The input action.</param>
    /// <returns>The primary and secondary key text.</returns>
    private static (string primary, string secondary) FormatKeys(InputAction action)
    {
        List<string> parts = new List<string>();
        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (action.bindings[i].isPartOfComposite)
                continue;
            string display = action.GetBindingDisplayString(i);
            if (!string.IsNullOrEmpty(display))
            {
                int modifierMask = KeyboardChordProcessor.ParseModifierMask(
                    action.bindings[i].effectiveProcessors
                );
                parts.Add(
                    KeyboardChordProcessor.GetDisplayPrefix(modifierMask) + ShortenKey(display)
                );
            }
        }

        return (
            parts.Count > 0 ? parts[0] : string.Empty,
            parts.Count > 1 ? parts[1] : string.Empty
        );
    }

    /// <summary>
    /// Upper-cases and abbreviates a key display string so it fits the narrow badges
    /// (e.g. "Left Windows" becomes "L WIN", "Control" becomes "CTRL").
    /// </summary>
    /// <param name="display">The input system's key display string.</param>
    /// <returns>The short, upper-cased key name.</returns>
    private static string ShortenKey(string display)
    {
        string key = display.ToUpperInvariant();
        key = key.Replace("LEFT ", "L ").Replace("RIGHT ", "R ");
        key = key
            .Replace("WINDOWS KEY", "WIN")
            .Replace("WINDOWS", "WIN")
            .Replace("SYSTEM", "WIN")
            .Replace("META", "WIN")
            .Replace("COMMAND", "WIN")
            .Replace("CONTROL", "CTRL")
            .Replace("ESCAPE", "ESC")
            .Replace("DELETE", "DEL")
            .Replace("INSERT", "INS")
            .Replace("BACKSPACE", "BKSP")
            .Replace("PAGE UP", "PG UP")
            .Replace("PAGE DOWN", "PG DN")
            .Replace("NUMPAD ", "NUM ");
        return key;
    }

    /// <summary>
    /// Resolves the display label for the current resolution.
    /// </summary>
    /// <param name="video">The current video settings.</param>
    /// <returns>The resolution label.</returns>
    private string GetResolutionLabel(UserVideoSettings video)
    {
        if (video.ResolutionWidth > 0 && video.ResolutionHeight > 0)
            return $"{video.ResolutionWidth} x {video.ResolutionHeight}";

        return resolutions.Count > 0
            ? $"{resolutions[resolutionIndex].x} x {resolutions[resolutionIndex].y}"
            : $"{Screen.width} x {Screen.height}";
    }

    /// <summary>
    /// Resolves the display label for a full-screen mode value.
    /// </summary>
    /// <param name="mode">The stored full-screen mode.</param>
    /// <returns>The display label.</returns>
    private static string GetFullScreenLabel(int mode)
    {
        return (FullScreenMode)mode switch
        {
            FullScreenMode.ExclusiveFullScreen => "Fullscreen",
            FullScreenMode.FullScreenWindow => "Borderless",
            FullScreenMode.MaximizedWindow => "Maximized",
            FullScreenMode.Windowed => "Windowed",
            _ => "Fullscreen",
        };
    }

    /// <summary>
    /// Inserts spaces between camel-cased words for display.
    /// </summary>
    /// <param name="name">The raw action name.</param>
    /// <returns>The spaced display name.</returns>
    private static string Humanize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            char previous = i > 0 ? name[i - 1] : '\0';
            bool boundary =
                i > 0
                && (
                    (char.IsUpper(c) && !char.IsUpper(previous))
                    || (char.IsDigit(c) && !char.IsDigit(previous))
                    || (char.IsLetter(c) && char.IsDigit(previous))
                );
            if (boundary)
                builder.Append(' ');
            builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads the overlay prefab's authored source-space size.
    /// </summary>
    /// <returns>The rounded source-space window size.</returns>
    private Vector2Int GetPrefabSize()
    {
        RectTransform rect = (RectTransform)prefab.transform;
        return new Vector2Int(
            Mathf.RoundToInt(rect.sizeDelta.x),
            Mathf.RoundToInt(rect.sizeDelta.y)
        );
    }

    /// <summary>
    /// Verifies that host actions are available before use.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                $"{nameof(OptionsMenuController)} must be initialized before use."
            );
    }
}
