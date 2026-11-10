using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Rebellion.Game.SpaceCombat;
using Rebellion.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Vector3N = System.Numerics.Vector3;

/// <summary>
/// Translates tactical presentation input into encounter commands and projects live battle state.
/// </summary>
public sealed class TacticalCombatController : IDisposable
{
    private readonly TacticalCombatSession _session;
    private readonly TacticalCombatView _view;
    private readonly Action<TacticalCombatSession> _complete;
    private readonly CancellationTokenSource _cancellation;
    private readonly CancellationToken _lifetime;
    private IReadOnlyList<TacticalCombatGroup> _selectedGroups => _session.Selection.Groups;
    private readonly TacticalCombatHudState _hud = new();
    private readonly bool[] _navigationSelected = new bool[4];
    private readonly bool[] _factionHighlighted = new bool[2];
    public event Action<TacticalCombatControl> MenuControlRequested;
    private ContentAssets _assets;
    private TacticalCombatCutsceneController _cutscenes;
    private TacticalCombatMoviePresentation _moviePresentation;
    private bool _ready;
    private bool _disposed;
    private bool _completing;
    private bool _stationControlsVisible;
    private bool _applicationMenuOpen;
    private float _hudRefresh;
    private readonly TacticalCombatInspection _inspection;
    private readonly TacticalCombatRenderSettings _videoSettings;

    /// <summary>
    /// Owns the authored view and its input subscriptions for one isolated battle.
    /// </summary>
    /// <param name="session">The deployed encounter.</param>
    /// <param name="view">The instantiated battle view owned by this controller.</param>
    /// <param name="complete">Commits the completed encounter.</param>
    /// <param name="cancellationToken">The campaign screen lifetime.</param>
    /// <param name="videoSettings">The active video settings, or null to use their authored defaults for an isolated encounter.</param>
    public TacticalCombatController(
        TacticalCombatSession session,
        TacticalCombatView view,
        Action<TacticalCombatSession> complete,
        CancellationToken cancellationToken,
        UserVideoSettings videoSettings = null
    )
    {
        this._session = session ?? throw new ArgumentNullException(nameof(session));
        _inspection = new TacticalCombatInspection(session);
        _videoSettings = new TacticalCombatRenderSettings(videoSettings ?? new UserVideoSettings());
        this._view = view != null ? view : throw new ArgumentNullException(nameof(view));
        this._complete = complete ?? throw new ArgumentNullException(nameof(complete));
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _lifetime = _cancellation.Token;
        view.ControlRequested += HandleControl;
        session.Selection.ContextReplaced += HandleContextReplaced;
        session.Selection.StationControlsClosed += HandleStationControlsClosed;
        session.Selection.WithdrawalStarting += _hud.BeginWithdrawal;
        session.Selection.WithdrawalDispatched += _hud.CompleteWithdrawal;
        session.UnitWithdrawn += HandleStationDeparture;
        _stationControlsVisible =
            session.GetSide(session.PlayerSide).FactionIndex == 1 && session.BattleStation != null;
        _hud.ReplaceContext(session.Selection.Kind);
        view.PointerPressed += HandlePointerDown;
        view.KeyboardRequested += HandleKeyboardEvent;
        view.Renderer.ArrivalStrobeChanged += HandleArrivalStrobe;
        session.ShotFired += HandleShot;
        session.Audio.Playback = PlayNotification;
        session.ImpactApplied += HandleImpact;
        session.DestructionEffectRequested += HandleDestructionEffect;
        session.SuperlaserFired += HandleSuperlaser;
        session.UnitDestroyed += HandleDestroyed;
        view.SetStatus("Preparing battle…");
        view.SetSelection(string.Empty);
        RefreshControls();
    }

    /// <summary>
    /// Loads required battle media before allowing any simulation progress.
    /// </summary>
    /// <param name="models">The application's shared model cache.</param>
    /// <param name="assets">The selected pack's media source.</param>
    /// <returns>The asynchronous preparation operation.</returns>
    public async Task PrepareAsync(ContentModelCache models, ContentAssets assets)
    {
        _assets = assets;
        if (_session.BattleStation != null)
        {
            if (
                string.IsNullOrWhiteSpace(_session.Definition?.AttackRunSuccessMoviePath)
                || string.IsNullOrWhiteSpace(_session.Definition?.AttackRunFailureMoviePath)
            )
                throw new InvalidDataException(
                    "Tactical combat requires success and failure movie addresses for battle-station attacks."
                );
            _ = assets.GetVideoUrl(_session.Definition.AttackRunSuccessMoviePath);
            _ = assets.GetVideoUrl(_session.Definition.AttackRunFailureMoviePath);
            _cutscenes = new TacticalCombatCutsceneController(
                _session,
                PlayAttackRunMovie,
                SetMoviePresentation,
                ReportMovieError,
                PostMovieCompletion
            );
        }
        await Task.WhenAll(
            _session
                .Audio.Clips.Select(clip => clip.Path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .Select(assets.LoadAudioAsync)
        );
        _lifetime.ThrowIfCancellationRequested();
        TacticalCombatFactionDefinition faction =
            _session.Definition.Factions.Find(value =>
                value.FactionIndex == _session.GetSide(_session.PlayerSide).FactionIndex
            )
            ?? throw new InvalidDataException(
                "Tactical combat HUD requires a definition for the player's faction."
            );
        _view.PrepareArtwork(_session.Definition, faction, assets);
        await _view.Renderer.PrepareAsync(_session, models, assets, _lifetime, _videoSettings);
        _lifetime.ThrowIfCancellationRequested();
        if (
            _videoSettings.ShowStarfield
            && !string.IsNullOrWhiteSpace(_session.Definition?.BackgroundImagePath)
        )
            _view.SetBackgroundTexture(assets.GetTexture(_session.Definition.BackgroundImagePath));
        System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
        _session.ConfigureClockedAdvance(() => unchecked((uint)clock.ElapsedMilliseconds));
        _session.BeginArrival(_view.Renderer);
        _ready = true;
        RefreshHud();
        RefreshControls();
    }

    /// <summary>
    /// Advances the encounter and projects one screen frame while campaign ticking remains suspended.
    /// </summary>
    /// <param name="elapsedSeconds">The unscaled presentation interval.</param>
    public void Advance(float elapsedSeconds)
    {
        if (_disposed || !_ready)
            return;
        _session.AdvanceClocked(elapsedSeconds * 1000);
        if (_session.IsComplete && !_completing)
        {
            _completing = true;
            _complete(_session);
            return;
        }
        if (_session.PendingAttackRunMovie != null)
            return;
        Vector2Int size = _view.PixelSize;
        _view.Renderer.Resize(size.x, size.y);
        _view.SetViewportTexture(_view.Renderer.Target);
        _view.Renderer.AdvanceEffects(elapsedSeconds);
        _view.Renderer.RenderUnits();
        RefreshNavigationSelection();
        _hudRefresh -= elapsedSeconds;
        if (_hudRefresh <= 0)
        {
            RefreshHud();
            RefreshControls();
            _hudRefresh = 0.1f;
        }
    }

    /// <summary>
    /// Transfers battle input ownership to or from the application menu without changing pause state.
    /// </summary>
    /// <param name="open">Whether an application menu currently owns user input.</param>
    public void SetApplicationMenuOpen(bool open)
    {
        _applicationMenuOpen = open;
        if (open)
            foreach (TacticalCombatHudControl definition in TacticalCombatHudLayout.Controls)
                if (TryCameraControl(definition.Control, out _))
                    _view.ApplyFrameKeyboardActivation(definition.Control, false);
        RefreshControls();
    }

    /// <summary>
    /// Routes native camera and direct-command transitions while preserving their distinct release semantics.
    /// </summary>
    /// <param name="key">The native keyboard key.</param>
    /// <param name="pressed">Whether this is a key-down event.</param>
    /// <returns>Whether the battle currently owns the addressed transition.</returns>
    private bool HandleKeyboardEvent(KeyCode key, bool pressed)
    {
        if (
            !_ready
            || _disposed
            || _completing
            || _applicationMenuOpen
            || _session.IsComplete
            || _session.PendingAttackRunMovie != null
        )
            return false;
        if (TacticalCombatKeyboard.TryGetCameraControl(key, out TacticalCombatControl control))
        {
            _view.ApplyFrameKeyboardActivation(control, pressed);
            return true;
        }
        if (key == KeyCode.Tab)
        {
            Keyboard keyboard = Keyboard.current;
            bool shift =
                keyboard != null
                && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            _view.HandleInspectionKey(Key.Tab, pressed, shift);
            if (pressed)
                _view.HandleInspectionKey(Key.Tab, false, shift);
            return true;
        }
        if (key == KeyCode.Space)
        {
            if (pressed)
                _session.Selection.QueueNearestTarget();
            return true;
        }
        if (TacticalCombatKeyboard.TryGetObjectCategoryControl(key, out control))
        {
            if (pressed)
            {
                _view.ClearCategoryKeyboardState();
                _view.ApplyFrameKeyboardActivation(control, true);
            }
            return true;
        }
        if (pressed && TacticalCombatKeyboard.TryGetCameraCategory(key, out int category))
        {
            _view.Renderer.ShowCategory(category);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Routes primary-camera controls while retaining the selected participant's live focus.
    /// </summary>
    /// <param name="command">The requested camera operation.</param>
    private void ApplyCameraCommand(TacticalCombatCameraCommand command)
    {
        if (command == TacticalCombatCameraCommand.Focus)
        {
            if (_session.Selection.PrimaryWaypoint is Vector3N point)
                _view.Renderer.Focus(point);
            else
                _view.Renderer.Focus(_session.Selection.PrimaryUnit);
        }
        else
            _view.Renderer.ApplyCameraCommand(command);
    }

    /// <summary>
    /// Translates a screen pointer into normalized battle viewport coordinates.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    /// <returns>The normalized point.</returns>
    private Vector2 GetViewportPoint(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _view.Viewport,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 local
        );
        Rect rect = _view.Viewport.rect;
        return new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
    }

    /// <summary>
    /// Queues pointer-down selection or contextual extension using ordered neighboring pixel picks.
    /// </summary>
    /// <param name="eventData">The source viewport press.</param>
    private void HandlePointerDown(PointerEventData eventData)
    {
        if (
            !_ready
            || _applicationMenuOpen
            || _session.IsArriving
            || _session.IsComplete
            || _session.PendingAttackRunMovie != null
        )
            return;
        bool right = eventData.button == PointerEventData.InputButton.Right;
        if (!right && eventData.button != PointerEventData.InputButton.Left)
            return;
        bool extend = Keyboard.current?.ctrlKey.isPressed == true;
        bool hit = _view.Renderer.TryPickNeighborhood(
            GetViewportPoint(eventData),
            right,
            out TacticalCombatUnit unit,
            out Vector3N waypoint
        );
        if (!right && !extend)
        {
            if (hit)
                _view.ClearCategoryKeyboardState();
            _session.Selection.QueueClear();
        }
        if (hit)
        {
            if (unit != null)
                _session.Selection.QueueObject(unit, extend);
            else
                _session.Selection.QueueWaypoint(waypoint, extend);
        }
        _hudRefresh = 0;
    }

    /// <summary>
    /// Applies a semantic HUD command to the current player selection.
    /// </summary>
    /// <param name="control">The requested control.</param>
    private void HandleControl(TacticalCombatControl control)
    {
        if (
            !_ready
            || _disposed
            || _completing
            || _applicationMenuOpen
            || _session.PendingAttackRunMovie != null
        )
            return;
        if (_session.IsComplete)
            return;
        if (control == TacticalCombatControl.Pause)
            _session.IsPaused = !_session.IsPaused;
        else if (TryCameraControl(control, out TacticalCombatCameraCommand camera))
            ApplyCameraCommand(camera);
        else if (
            control
            is >= TacticalCombatControl.NavigationRing1
                and <= TacticalCombatControl.NavigationRing4
        )
        {
            int index = (int)control - (int)TacticalCombatControl.NavigationRing1;
            _navigationSelected[index] = !_navigationSelected[index];
            _view.Renderer.ToggleNavigationRing(3 - index);
        }
        else if (control == TacticalCombatControl.OpenMenu)
            _hud.OpenMenu();
        else if (control == TacticalCombatControl.CloseMenu)
            _hud.CloseMenu();
        else if (control == TacticalCombatControl.Withdraw)
        {
            if (_session.PreparePlayerWithdrawal())
                _hud.OpenWithdrawalConfirmation();
        }
        else if (control == TacticalCombatControl.ConfirmWithdrawal)
        {
            _session.ConfirmPlayerWithdrawal();
            _hud.CloseWithdrawalConfirmation();
        }
        else if (control == TacticalCombatControl.CancelWithdrawal)
            _hud.CloseWithdrawalConfirmation();
        else if (control == TacticalCombatControl.OpenOrders)
            _hud.OpenOrders(SelectedRequestedGroup()?.Order ?? TacticalCombatOrder.None);
        else if (control == TacticalCombatControl.OpenTactics)
        {
            TacticalCombatGroup group = SelectedRequestedGroup();
            _hud.OpenTactics(group?.Order ?? TacticalCombatOrder.None, group?.CloseAttack == true);
        }
        else if (control == TacticalCombatControl.CancelPanel)
            _hud.CloseEditor();
        else if (control == TacticalCombatControl.ConfirmPanel)
            ConfirmPanel();
        else if (_hud.Panel is TacticalCombatHudPanel.Orders or TacticalCombatHudPanel.Tactics)
            _hud.SelectControl(control);
        else if (
            control is TacticalCombatControl.PreviousObject or TacticalCombatControl.NextObject
        )
            _session.Selection.QueueDisplayMode(control == TacticalCombatControl.NextObject);
        else if (
            control is TacticalCombatControl.ResolveImmediately or TacticalCombatControl.OpenOptions
        )
        {
            MenuControlRequested?.Invoke(control);
        }
        else if (
            control
            is TacticalCombatControl.HighlightAlliance
                or TacticalCombatControl.HighlightEmpire
        )
        {
            int faction = control == TacticalCombatControl.HighlightAlliance ? 0 : 1;
            _factionHighlighted[faction] = !_factionHighlighted[faction];
            _view.Renderer.SetFactionHighlight(faction, _factionHighlighted[faction]);
        }
        else if (control == TacticalCombatControl.Superlaser)
            _session.Selection.QueueStationTarget();
        else if (control == TacticalCombatControl.ToggleAutomation)
            _session.Selection.QueueToggleAutomation();
        else if (
            control
            is >= TacticalCombatControl.ObjectGroup1
                and <= TacticalCombatControl.ObjectGroup8
        )
            _session.Selection.QueueObjectCategory(
                (int)control - (int)TacticalCombatControl.ObjectGroup1
            );
        else if (
            control
            is >= TacticalCombatControl.FighterGroup1
                and <= TacticalCombatControl.FighterGroup4
        )
            _session.Selection.QueueFighterCategory(
                (int)control - (int)TacticalCombatControl.FighterGroup1
            );
        RefreshHud();
        RefreshControls();
        _hudRefresh = 0;
    }

    /// <summary>
    /// Resolves the current requested owner without promoting copied highlights into command membership.
    /// </summary>
    /// <returns>The context's requested owner, or null for a temporary multi-selection.</returns>
    private TacticalCombatGroup SelectedRequestedGroup() =>
        _session.Selection.Group
        ?? (
            _session.Selection.PrimaryUnit is TacticalCombatUnit unit
                ? _session.GetRequestedGroup(unit)
                : null
        );

    /// <summary>
    /// Commits only the editor's confirmed mission, maneuver, and engagement choices.
    /// </summary>
    private void ConfirmPanel()
    {
        if (_hud.Panel == TacticalCombatHudPanel.Orders)
            _session.Selection.RequestOrder(_hud.PendingOrder);
        else if (_hud.Panel == TacticalCombatHudPanel.Tactics)
        {
            TacticalCombatGroup group = SelectedRequestedGroup();
            TacticalCombatOrder order = _hud.GetPendingManeuver();
            if ((group?.Order ?? TacticalCombatOrder.None) != order)
                _session.Selection.RequestOrder(order);
            if (group == null || group.CloseAttack != _hud.PendingCloseAttack)
                _session.Selection.RequestAttackMode(_hud.PendingCloseAttack);
        }
        _hud.CloseEditor();
    }

    /// <summary>
    /// Installs the right-panel context only at the gameplay owner's actual replacement boundary.
    /// </summary>
    /// <param name="kind">The newly installed selection context.</param>
    private void HandleContextReplaced(TacticalCombatSelectionKind kind)
    {
        _view.ClearCategoryKeyboardState();
        _hud.ReplaceContext(kind);
        _hudRefresh = 0;
    }

    /// <summary>
    /// Resolves one camera image into the same command used by its keyboard shortcut.
    /// </summary>
    /// <param name="control">The image control.</param>
    /// <param name="command">Receives the camera command.</param>
    /// <returns>Whether the control addresses the camera.</returns>
    private static bool TryCameraControl(
        TacticalCombatControl control,
        out TacticalCombatCameraCommand command
    )
    {
        command = control switch
        {
            TacticalCombatControl.CameraZoomIn => TacticalCombatCameraCommand.ZoomIn,
            TacticalCombatControl.CameraZoomOut => TacticalCombatCameraCommand.ZoomOut,
            TacticalCombatControl.CameraRotateLeft => TacticalCombatCameraCommand.RotateLeft,
            TacticalCombatControl.CameraRotateRight => TacticalCombatCameraCommand.RotateRight,
            TacticalCombatControl.CameraTiltUp => TacticalCombatCameraCommand.TiltUp,
            TacticalCombatControl.CameraTiltDown => TacticalCombatCameraCommand.TiltDown,
            TacticalCombatControl.SaveCamera => TacticalCombatCameraCommand.Save,
            TacticalCombatControl.RestoreCamera => TacticalCombatCameraCommand.Restore,
            _ => TacticalCombatCameraCommand.Focus,
        };
        return control
            is TacticalCombatControl.CameraZoomIn
                or TacticalCombatControl.CameraZoomOut
                or TacticalCombatControl.CameraRotateLeft
                or TacticalCombatControl.CameraRotateRight
                or TacticalCombatControl.CameraTiltUp
                or TacticalCombatControl.CameraTiltDown
                or TacticalCombatControl.SaveCamera
                or TacticalCombatControl.RestoreCamera
                or TacticalCombatControl.FocusCamera;
    }

    /// <summary>
    /// Refreshes navigation markers for the current gameplay selection.
    /// </summary>
    private void RefreshNavigationSelection() =>
        _view.Renderer.UpdateNavigationSelection(_selectedGroups);

    /// <summary>
    /// Displays battle progress and selected-unit condition without allocating text every frame.
    /// </summary>
    private void RefreshHud()
    {
        _view.SetPanel(_hud.Panel);
        if (
            _hud.Panel is TacticalCombatHudPanel.SingleObject or TacticalCombatHudPanel.ObjectGroup
            && _session.Selection.PrimaryUnit is TacticalCombatUnit inspected
        )
            _view.SetInspection(inspected, _inspection.ReadUnit(inspected));
        if (_hud.Panel == TacticalCombatHudPanel.FighterGroup && _session.Selection.Group != null)
            _view.SetFighterInspection(
                _inspection.ReadFighters(
                    _session.Selection.Group,
                    _session.Selection.Category,
                    _session.GetSide(_session.PlayerSide).FactionIndex
                )
            );
        _view.SetDiagram(_hud.DiagramKey);
        _view.SetStationCharge(_stationControlsVisible, _session.BattleStation?.Charge ?? 0);
        _view.SetControlSelected(
            TacticalCombatControl.Superlaser,
            _session.Selection.Kind == TacticalCombatSelectionKind.StationTarget
        );
        for (int index = 0; index < 8; index++)
            _view.SetControlSelected(
                (TacticalCombatControl)((int)TacticalCombatControl.ObjectGroup1 + index),
                _session.Selection.Kind == TacticalCombatSelectionKind.ObjectGroup
                    && _session.Selection.Category == index,
                _session.GetObjectGroup(_session.PlayerSide, index) != null
            );
        for (int index = 0; index < 4; index++)
        {
            _view.SetControlSelected(
                (TacticalCombatControl)((int)TacticalCombatControl.FighterGroup1 + index),
                _session.Selection.Kind == TacticalCombatSelectionKind.FighterGroup
                    && _session.Selection.Category == index
            );
            _view.SetControlSelected(
                (TacticalCombatControl)((int)TacticalCombatControl.NavigationRing1 + index),
                _navigationSelected[index]
            );
        }
        _view.SetControlSelected(
            TacticalCombatControl.Pause,
            _session.IsPaused || _view.Renderer.IsPresentingArrival
        );
        _view.SetControlSelected(
            TacticalCombatControl.ToggleAutomation,
            _session.GetSide(_session.PlayerSide).IsAutomated
        );
        _view.SetControlSelected(TacticalCombatControl.HighlightAlliance, _factionHighlighted[0]);
        _view.SetControlSelected(TacticalCombatControl.HighlightEmpire, _factionHighlighted[1]);
        foreach (TacticalCombatHudControl definition in TacticalCombatHudLayout.Controls)
            if (definition.Panel is TacticalCombatHudPanel.Orders or TacticalCombatHudPanel.Tactics)
                _view.SetControlSelected(definition.Control, _hud.IsSelected(definition.Control));
    }

    /// <summary>
    /// Projects the arrival pause image independently of the user's current pause state.
    /// </summary>
    /// <param name="active">Whether the arrival sequence currently owns the pause strobe.</param>
    private void HandleArrivalStrobe(bool active) =>
        _view.SetControlSelected(TacticalCombatControl.Pause, active || _session.IsPaused);

    /// <summary>
    /// Projects context-specific controls without adding actions to unrelated inspection surfaces.
    /// </summary>
    private void RefreshControls()
    {
        bool active =
            _ready
            && !_applicationMenuOpen
            && !_session.IsComplete
            && _session.PendingAttackRunMovie == null;
        foreach (TacticalCombatHudControl definition in TacticalCombatHudLayout.Controls)
            _view.SetControlEnabled(definition.Control, active);
        for (int category = 0; category < 8; category++)
            _view.SetControlEnabled(
                (TacticalCombatControl)((int)TacticalCombatControl.ObjectGroup1 + category),
                active
                    && (
                        _session.GetObjectGroup(_session.PlayerSide, category) != null
                        || _session.Selection.CanIssueCommands
                    )
            );
        for (int category = 0; category < 4; category++)
            _view.SetControlEnabled(
                (TacticalCombatControl)((int)TacticalCombatControl.FighterGroup1 + category),
                active && _session.GetFighterGroup(_session.PlayerSide, category) != null
            );
        _view.SetControlEnabled(
            TacticalCombatControl.OpenOrders,
            active && _session.Selection.CanIssueCommands
        );
        _view.SetControlEnabled(
            TacticalCombatControl.OpenTactics,
            active && _session.Selection.CanIssueCommands
        );
        _view.SetControlEnabled(
            TacticalCombatControl.Recover,
            active && _session.Selection.Kind == TacticalCombatSelectionKind.FighterGroup
        );
        _view.SetControlEnabled(
            TacticalCombatControl.AttackBattleStation,
            active
                && _session.Selection.Kind == TacticalCombatSelectionKind.FighterGroup
                && _session.CanAttackBattleStation(_session.PlayerSide)
        );
        _view.SetControlEnabled(
            TacticalCombatControl.Withdraw,
            active && _session.CanWithdraw(_session.PlayerSide)
        );
        _view.SetControlEnabled(
            TacticalCombatControl.Superlaser,
            active && _session.IsDirectControlEnabled && _session.BattleStation?.Charge == 100
        );
        _view.SetControlEnabled(
            TacticalCombatControl.ToggleAutomation,
            active && !_session.GetSide(_session.PlayerSide).IsWithdrawing
        );
    }

    /// <summary>
    /// Starts an owned notification playback using its authored loop setting.
    /// </summary>
    /// <param name="clip">The selected notification variant.</param>
    /// <param name="loop">Whether the scheduler owns a repeating playback.</param>
    /// <returns>The playback's actual activity and cancellation boundary.</returns>
    private ITacticalCombatNotificationPlayback PlayNotification(
        TacticalCombatSoundClip clip,
        bool loop
    )
    {
        if (_disposed || AudioManager.Instance == null)
            return null;
        AudioPlaybackHandle playback = AudioManager.Instance.PlaySfxInstance(
            _assets.GetPreloadedAudio(clip.Path),
            loop
        );
        return playback == null ? null : new NotificationPlayback(playback);
    }

    /// <summary>
    /// Captures a fired weapon's visual trail.
    /// </summary>
    /// <param name="shot">The applied discharge.</param>
    private void HandleShot(TacticalCombatShot shot) =>
        _view.Renderer.ShowShot(shot, _session.GetSide(shot.Source.Side).FactionIndex);

    /// <summary>
    /// Projects the surface reached by an applied impact without repeating damage calculations.
    /// </summary>
    /// <param name="unit">The impacted encounter participant.</param>
    /// <param name="weapon">The impacting weapon.</param>
    /// <param name="impact">The affected surface or destruction.</param>
    private void HandleImpact(
        TacticalCombatUnit unit,
        TacticalWeaponType weapon,
        TacticalCombatImpactKind impact
    ) => _view.Renderer.ShowImpact(unit, weapon, impact);

    /// <summary>
    /// Displays the station's superlaser without changing its queued damage command.
    /// </summary>
    /// <param name="station">The firing station.</param>
    /// <param name="target">The enemy capital ship.</param>
    private void HandleSuperlaser(TacticalCombatUnit station, TacticalCombatUnit target) =>
        _view.Renderer.ShowSuperlaser(station, target, _session.BattleDepth);

    /// <summary>
    /// Begins the effect for a direct destruction command before its participant is deactivated.
    /// </summary>
    /// <param name="unit">The unit admitted for destruction.</param>
    private void HandleDestructionEffect(TacticalCombatUnit unit) =>
        _view.Renderer.ShowDestruction(unit);

    /// <summary>
    /// Closes station controls after their station is destroyed.
    /// </summary>
    /// <param name="unit">The destroyed encounter participant.</param>
    private void HandleDestroyed(TacticalCombatUnit unit)
    {
        if (unit.IsBattleStation)
            HandleStationControlsClosed();
    }

    /// <summary>
    /// Closes station controls when their owned station has departed.
    /// </summary>
    /// <param name="unit">The departed participant.</param>
    private void HandleStationDeparture(TacticalCombatUnit unit)
    {
        if (unit.IsBattleStation)
            HandleStationControlsClosed();
    }

    /// <summary>
    /// Releases the charge header without replacing an unrelated inspection context.
    /// </summary>
    private void HandleStationControlsClosed()
    {
        _stationControlsVisible = false;
        _hudRefresh = 0;
    }

    /// <summary>
    /// Begins the outcome movie with its own cancellable playback and restricted skip controls.
    /// </summary>
    /// <param name="path">The authored outcome video address.</param>
    /// <param name="completed">Acknowledges the battle's movie pause after playback.</param>
    /// <returns>Ownership that cancels only this movie.</returns>
    private IDisposable PlayAttackRunMovie(string path, Action completed)
    {
        return AppBootstrap
            .Instance.GetCutsceneManager()
            .PlayOwned(path, completed, CutsceneSkipInput.EscapeOrPrimaryPointer);
    }

    /// <summary>
    /// Transfers visibility and pointer focus between battle presentation and full-screen video.
    /// </summary>
    /// <param name="active">Whether the movie owns presentation.</param>
    private void SetMoviePresentation(bool active)
    {
        if (_moviePresentation == null)
        {
            AppInputController input = AppBootstrap.Instance.GetInputController();
            _moviePresentation = new TacticalCombatMoviePresentation(
                SetBattlePresentation,
                () => input.PushContext(InputContext.Cutscene),
                input.PopContext
            );
        }
        _moviePresentation.SetActive(active);
    }

    /// <summary>
    /// Releases battle sounds before hiding and permits new sounds after the battle reactivates.
    /// Its view-owned input context follows the same visibility transition.
    /// </summary>
    /// <param name="active">Whether battle presentation is visible.</param>
    private void SetBattlePresentation(bool active)
    {
        if (!active)
            _session.Audio.SetPlaybackAvailable(false);
        _view.gameObject.SetActive(active);
        if (active)
            _session.Audio.SetPlaybackAvailable(true);
    }

    /// <summary>
    /// Posts a failed movie's acknowledgement after the current battle command returns.
    /// </summary>
    /// <param name="completed">Releases the failed movie's simulation and presentation ownership.</param>
    private async void PostMovieCompletion(Action completed)
    {
        await Task.Yield();
        if (!_disposed)
            completed();
    }

    /// <summary>
    /// Reports a video that could not start before its deferred completion acknowledgement.
    /// </summary>
    /// <param name="exception">The playback failure.</param>
    private void ReportMovieError(Exception exception)
    {
        Debug.LogException(exception);
    }

    /// <summary>
    /// Adapts one application-owned playback to the notification scheduler's live status.
    /// </summary>
    private sealed class NotificationPlayback : ITacticalCombatNotificationPlayback
    {
        private readonly AudioPlaybackHandle playback;
        public bool IsPlaying =>
            playback.IsActive
            && (playback.Paused || AudioListener.pause || playback.Source?.isPlaying == true);

        /// <summary>
        /// Captures the independently controllable sound source.
        /// </summary>
        /// <param name="playback">The application's ownership handle.</param>
        public NotificationPlayback(AudioPlaybackHandle playback)
        {
            this.playback = playback;
        }

        /// <summary>
        /// Stops only the source owned by this notification.
        /// </summary>
        public void Stop() => playback.Stop();
    }

    /// <summary>
    /// Cancels pending preparation, removes subscriptions, and destroys the owned authored screen.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cutscenes?.Dispose();
        _cutscenes = null;
        _moviePresentation?.Dispose();
        _moviePresentation = null;
        _cancellation.Cancel();
        _cancellation.Dispose();
        _view.ControlRequested -= HandleControl;
        _session.Selection.ContextReplaced -= HandleContextReplaced;
        _session.Selection.StationControlsClosed -= HandleStationControlsClosed;
        _session.Selection.WithdrawalStarting -= _hud.BeginWithdrawal;
        _session.Selection.WithdrawalDispatched -= _hud.CompleteWithdrawal;
        _session.UnitWithdrawn -= HandleStationDeparture;
        _view.PointerPressed -= HandlePointerDown;
        _view.KeyboardRequested -= HandleKeyboardEvent;
        _view.Renderer.ArrivalStrobeChanged -= HandleArrivalStrobe;
        _session.ShotFired -= HandleShot;
        _session.Audio.Playback = null;
        _session.Selection.Dispose();
        _session.Audio.Dispose();
        _session.ImpactApplied -= HandleImpact;
        _session.DestructionEffectRequested -= HandleDestructionEffect;
        _session.SuperlaserFired -= HandleSuperlaser;
        _session.UnitDestroyed -= HandleDestroyed;
        UnityEngine.Object.Destroy(_view.gameObject);
    }
}
