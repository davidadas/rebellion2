using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    private readonly HashSet<TacticalCombatUnit> _selected = new();
    private readonly List<TacticalCombatGroup> _selectedGroups = new();
    private readonly StringBuilder _selectionText = new();
    private readonly List<AudioPlaybackHandle> _audioPlaybacks = new();
    private ContentAssets _assets;
    private Vector2 _pressPoint;
    private PointerEventData.InputButton? _gesture;
    private bool _dragged;
    private bool _ready;
    private bool _disposed;
    private bool _completing;
    private bool _targetingSuperlaser;
    private float _hudRefresh;
    private string _notice;

    /// <summary>
    /// Owns the authored view and its input subscriptions for one isolated battle.
    /// </summary>
    /// <param name="session">The deployed encounter.</param>
    /// <param name="view">The instantiated battle view owned by this controller.</param>
    /// <param name="complete">Commits the completed encounter.</param>
    /// <param name="cancellationToken">The campaign screen lifetime.</param>
    public TacticalCombatController(
        TacticalCombatSession session,
        TacticalCombatView view,
        Action<TacticalCombatSession> complete,
        CancellationToken cancellationToken
    )
    {
        this._session = session ?? throw new ArgumentNullException(nameof(session));
        this._view = view != null ? view : throw new ArgumentNullException(nameof(view));
        this._complete = complete ?? throw new ArgumentNullException(nameof(complete));
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _lifetime = _cancellation.Token;
        view.ControlRequested += HandleControl;
        view.PointerPressed += HandlePointerDown;
        view.PointerReleased += HandlePointerUp;
        view.PointerDragged += HandleDrag;
        view.ZoomRequested += HandleZoom;
        session.ShotFired += HandleShot;
        session.Audio.SoundRequested += HandleSound;
        session.ShotApplied += HandleImpact;
        session.SuperlaserFired += HandleSuperlaser;
        session.UnitDestroyed += HandleDestroyed;
        session.UnitWithdrawn += HandleWithdrawn;
        session.FighterRecovered += HandleRecovered;
        session.RecoveryUnavailable += HandleRecoveryUnavailable;
        session.SystemChanged += HandleSystemChanged;
        session.AttackRunChanged += HandleAttackRun;
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
        await Task.WhenAll(
            (_session.Definition?.Sounds ?? new List<TacticalCombatSoundDefinition>())
                .SelectMany(sound => sound.Variants)
                .Select(clip => clip.Path)
                .Distinct(StringComparer.Ordinal)
                .Select(assets.LoadAudioAsync)
        );
        _lifetime.ThrowIfCancellationRequested();
        await _view.Renderer.PrepareAsync(_session, models, assets, _lifetime);
        _lifetime.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(_session.Definition?.BackgroundImagePath))
            _view.SetBackgroundTexture(assets.GetTexture(_session.Definition.BackgroundImagePath));
        _ready = true;
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
        ReadKeyboard();
        _session.Advance(elapsedSeconds * 1000);
        Vector2Int size = _view.PixelSize;
        _view.Renderer.Resize(size.x, size.y);
        _view.SetViewportTexture(_view.Renderer.Target);
        _view.Renderer.AdvanceEffects(elapsedSeconds);
        _view.Renderer.RenderUnits();
        ProjectOverlay();
        _hudRefresh -= elapsedSeconds;
        if (_hudRefresh <= 0)
        {
            RefreshHud();
            RefreshControls();
            _hudRefresh = 0.1f;
        }
    }

    /// <summary>
    /// Handles screen-local shortcuts without creating or leaking input-action maps.
    /// </summary>
    private void ReadKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            HandleControl(TacticalCombatControl.Pause);
        if (keyboard.gKey.wasPressedThisFrame)
            HandleControl(TacticalCombatControl.Group);
        if (keyboard.fKey.wasPressedThisFrame && _selected.Count > 0)
            _view.Renderer.Focus(_selected.First().Position);
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
    /// Starts a selection or camera gesture inside the battle viewport.
    /// </summary>
    /// <param name="eventData">The viewport pointer event.</param>
    private void HandlePointerDown(PointerEventData eventData)
    {
        if (!_ready || _session.IsComplete)
            return;
        _gesture = eventData.button;
        _pressPoint = GetViewportPoint(eventData);
        _dragged = false;
    }

    /// <summary>
    /// Updates a selection rectangle, camera pan, or camera orbit.
    /// </summary>
    /// <param name="eventData">The active gesture.</param>
    private void HandleDrag(PointerEventData eventData)
    {
        if (!_gesture.HasValue)
            return;
        _dragged = true;
        if (_gesture == PointerEventData.InputButton.Left)
            _view.Overlay.SelectionRectangle = GetRectangle(
                _pressPoint,
                GetViewportPoint(eventData)
            );
        else if (_gesture == PointerEventData.InputButton.Middle)
            _view.Renderer.Pan(eventData.delta);
        else
            _view.Renderer.Orbit(eventData.delta);
    }

    /// <summary>
    /// Completes a selection gesture or issues a target or waypoint order.
    /// </summary>
    /// <param name="eventData">The completed gesture.</param>
    private void HandlePointerUp(PointerEventData eventData)
    {
        if (!_gesture.HasValue)
            return;
        Vector2 point = GetViewportPoint(eventData);
        if (_gesture == PointerEventData.InputButton.Left)
            Select(point, _dragged);
        else if (_gesture == PointerEventData.InputButton.Right && !_dragged)
            IssuePointerOrder(point);
        _gesture = null;
        _view.Overlay.SelectionRectangle = null;
        _hudRefresh = 0;
    }

    /// <summary>
    /// Selects friendly participants by click or rectangle, with Shift extending the selection.
    /// </summary>
    /// <param name="point">The normalized release point.</param>
    /// <param name="rectangle">Whether the gesture covered an area.</param>
    private void Select(Vector2 point, bool rectangle)
    {
        bool extend = Keyboard.current?.shiftKey.isPressed == true;
        if (!extend)
            _selected.Clear();
        if (!rectangle)
        {
            _view.Renderer.TryPick(point, out TacticalCombatUnit unit, out _);
            if (unit?.Side == _session.PlayerSide)
                foreach (TacticalCombatUnit member in _session.GetGroup(unit).Members)
                    _selected.Add(member);
            return;
        }
        Rect area = GetRectangle(_pressPoint, point);
        foreach (TacticalCombatUnit unit in _session.Units)
        {
            Vector3 projected = _view.Renderer.Project(unit.Position);
            if (
                unit.Side == _session.PlayerSide
                && unit.IsTargetable
                && projected.z > 0
                && area.Contains(projected)
            )
                _selected.Add(unit);
        }
    }

    /// <summary>
    /// Assigns a hostile target or a snapped three-dimensional navigation point to selected groups.
    /// </summary>
    /// <param name="point">The normalized pointer position.</param>
    private void IssuePointerOrder(Vector2 point)
    {
        CollectSelectedGroups();
        bool hit = _view.Renderer.TryPick(
            point,
            out TacticalCombatUnit target,
            out Vector3N waypoint
        );
        bool append = Keyboard.current?.shiftKey.isPressed == true;
        if (_targetingSuperlaser)
        {
            _targetingSuperlaser = false;
            if (target != null && target.Side != _session.PlayerSide)
                _session.FireSuperlaser(target);
            return;
        }
        if (!hit || target?.Side == _session.PlayerSide)
            return;
        bool moving = target == null;
        foreach (TacticalCombatGroup group in _selectedGroups)
        {
            if (!append || group.Order == TacticalCombatOrder.HoldFire)
                _session.SetOrder(group, TacticalCombatOrder.None);
            if (target?.IsTargetable == true)
            {
                if (append && group.RequestedTargets.Contains(target))
                    _session.RemoveTarget(group, target);
                else
                    _session.SetTarget(group, target, append);
            }
            else if (moving)
            {
                if (append && group.RequestedWaypoints.Contains(waypoint))
                    _session.RemoveWaypoint(group, waypoint);
                else
                    _session.SetWaypoint(group, waypoint, append);
            }
        }
    }

    /// <summary>
    /// Applies a semantic HUD command to the current player selection.
    /// </summary>
    /// <param name="control">The requested control.</param>
    private void HandleControl(TacticalCombatControl control)
    {
        if (!_ready || _disposed || _completing)
            return;
        if (control == TacticalCombatControl.Continue)
        {
            if (_session.IsComplete)
            {
                _completing = true;
                _complete(_session);
            }
            return;
        }
        if (_session.IsComplete)
            return;
        if (control == TacticalCombatControl.Pause)
        {
            _session.IsPaused = !_session.IsPaused;
            return;
        }
        if (
            control >= TacticalCombatControl.NavigationRing1
            && control <= TacticalCombatControl.NavigationRing4
        )
        {
            _view.Renderer.ToggleNavigationRing(
                (int)control - (int)TacticalCombatControl.NavigationRing1
            );
            return;
        }
        if (control == TacticalCombatControl.Withdraw)
        {
            _session.BeginWithdrawal(_session.PlayerSide);
            return;
        }
        CollectSelectedGroups();
        if (control == TacticalCombatControl.Group)
            RegroupSelection();
        else if (control == TacticalCombatControl.Superlaser)
        {
            _notice = "Right-click an enemy unit to fire the superlaser.";
            _targetingSuperlaser = true;
        }
        else
            foreach (TacticalCombatGroup group in _selectedGroups)
                ApplyGroupControl(group, control);
        _hudRefresh = 0;
    }

    /// <summary>
    /// Applies one formation, attack-style, or behavior order to a selected group.
    /// </summary>
    /// <param name="group">The player's selected group.</param>
    /// <param name="control">The requested control.</param>
    private void ApplyGroupControl(TacticalCombatGroup group, TacticalCombatControl control)
    {
        switch (control)
        {
            case TacticalCombatControl.CloseAttack:
                group.SetAttackMode(true);
                return;
            case TacticalCombatControl.RangedAttack:
                group.SetAttackMode(false);
                return;
            case TacticalCombatControl.LineFormation:
                if (!group.IsFighterGroup)
                    _session.SetFormation(group, TacticalCombatFormationKind.Line);
                return;
            case TacticalCombatControl.SurroundFormation:
                if (!group.IsFighterGroup)
                    _session.SetFormation(group, TacticalCombatFormationKind.Surround);
                return;
        }
        TacticalCombatOrder order = control switch
        {
            TacticalCombatControl.AttackFighters => TacticalCombatOrder.AttackFighters,
            TacticalCombatControl.AttackCapitalShips => TacticalCombatOrder.AttackCapitalShips,
            TacticalCombatControl.HoldFire => TacticalCombatOrder.HoldFire,
            TacticalCombatControl.Stop => TacticalCombatOrder.Stop,
            TacticalCombatControl.Recover => TacticalCombatOrder.Recover,
            TacticalCombatControl.LeftHook => TacticalCombatOrder.LeftHook,
            TacticalCombatControl.RightHook => TacticalCombatOrder.RightHook,
            TacticalCombatControl.Hammer => TacticalCombatOrder.Hammer,
            TacticalCombatControl.Anvil => TacticalCombatOrder.Anvil,
            TacticalCombatControl.AttackBattleStation => TacticalCombatOrder.AttackBattleStation,
            _ => TacticalCombatOrder.None,
        };
        if (
            (
                order == TacticalCombatOrder.Recover
                || order == TacticalCombatOrder.AttackBattleStation
            ) && !group.IsFighterGroup
        )
            return;
        _session.SetOrder(group, order);
    }

    /// <summary>
    /// Collects each selected formation once in encounter registration order.
    /// </summary>
    private void CollectSelectedGroups()
    {
        _selectedGroups.Clear();
        foreach (TacticalCombatGroup group in _session.Groups)
            if (group.Side == _session.PlayerSide && group.Members.Any(_selected.Contains))
                _selectedGroups.Add(group);
    }

    /// <summary>
    /// Creates independent capital-ship and fighter formations from the selected active units.
    /// </summary>
    private void RegroupSelection()
    {
        foreach (bool fighters in new[] { false, true })
        {
            List<TacticalCombatUnit> members = _session
                .Units.Where(unit =>
                    _selected.Contains(unit)
                    && unit.IsEngaged
                    && unit.IsFighter == fighters
                    && !unit.IsBattleStation
                )
                .ToList();
            if (members.Count > 0)
                _session.CreateGroup(members);
        }
    }

    /// <summary>
    /// Updates markers and weapon trails using the current camera projection.
    /// </summary>
    private void ProjectOverlay()
    {
        TacticalCombatOverlayGraphic overlay = _view.Overlay;
        overlay.BeginFrame();
        foreach (TacticalCombatUnit unit in _session.Units)
        {
            if (!unit.IsEngaged || !unit.Systems.IsAlive || unit.IsBattleStation)
                continue;
            Vector3 point = _view.Renderer.Project(unit.Position);
            if (point.z <= 0)
                continue;
            Color tint =
                _selected.Contains(unit) ? Color.yellow
                : unit.Side == _session.PlayerSide ? Color.green
                : Color.red;
            overlay.AddMarker(
                point,
                tint,
                _selected.Contains(unit),
                unit.Systems.HullFraction,
                unit.Systems.MaximumShields > 0
                    ? unit.Systems.Shields / unit.Systems.MaximumShields
                    : 0
            );
        }
        CollectSelectedGroups();
        _view.Renderer.UpdateNavigationSelection(_selectedGroups);
        overlay.EndFrame();
    }

    /// <summary>
    /// Displays battle progress and selected-unit condition without allocating text every frame.
    /// </summary>
    private void RefreshHud()
    {
        string state =
            _session.IsComplete ? "Battle complete. Select Return to campaign."
            : _session.IsPaused ? "Paused"
            : $"Battle time: {Math.Max(_session.SystemTime - 3, 0):0} s";
        _view.SetStatus(string.IsNullOrEmpty(_notice) ? state : state + "  •  " + _notice);
        _selectionText.Clear();
        foreach (TacticalCombatUnit unit in _session.Units)
        {
            if (!_selected.Contains(unit))
                continue;
            if (_selectionText.Length > 0)
                _selectionText.AppendLine();
            _selectionText
                .Append(unit.DisplayName)
                .Append("  Hull ")
                .Append((int)unit.Systems.Hull)
                .Append('/')
                .Append((int)unit.Systems.MaximumHull)
                .Append("  Shields ")
                .Append((int)unit.Systems.Shields);
            if (unit.IsFighter)
                _selectionText.Append("  Fighters ").Append(unit.SquadronSize);
            else
                _selectionText
                    .Append("  Component damage ")
                    .Append(
                        (
                            unit.Systems.GetDamage(TacticalShipSystem.Shields)
                            + unit.Systems.GetDamage(TacticalShipSystem.Weapons)
                            + unit.Systems.GetDamage(TacticalShipSystem.TractorBeam)
                            + unit.Systems.GetDamage(TacticalShipSystem.Engines)
                            + unit.Systems.GetDamage(TacticalShipSystem.Hyperdrive)
                        )
                    );
        }
        _view.SetSelection(
            _selectionText.Length == 0
                ? "Select a group. Right-click a target or navigation point. Shift toggles queued orders.\nShow navigation rings to issue movement orders. Middle-drag pans; right-drag orbits; scroll zooms. F focuses; G groups."
                : _selectionText.ToString()
        );
    }

    /// <summary>
    /// Projects action availability from the current encounter and selection.
    /// </summary>
    private void RefreshControls()
    {
        bool active = _ready && !_session.IsComplete;
        foreach (TacticalCombatControl control in Enum.GetValues(typeof(TacticalCombatControl)))
            _view.SetControlEnabled(control, active && _selected.Count > 0);
        _view.SetControlEnabled(TacticalCombatControl.Pause, active);
        for (
            TacticalCombatControl control = TacticalCombatControl.NavigationRing1;
            control <= TacticalCombatControl.NavigationRing4;
            control++
        )
            _view.SetControlEnabled(control, active);
        _view.SetControlEnabled(
            TacticalCombatControl.Withdraw,
            active && _session.CanWithdraw(_session.PlayerSide)
        );
        _view.SetControlEnabled(
            TacticalCombatControl.AttackBattleStation,
            active
                && _selected.Any(unit => unit.IsFighter)
                && _session.CanAttackBattleStation(_session.PlayerSide)
        );
        _view.SetControlEnabled(
            TacticalCombatControl.Superlaser,
            active
                && _session.BattleStation?.Unit.Side == _session.PlayerSide
                && _session.BattleStation.Charge >= 100
        );
        _view.SetControlEnabled(TacticalCombatControl.Continue, _session.IsComplete);
    }

    /// <summary>
    /// Routes viewport wheel input to camera zoom.
    /// </summary>
    /// <param name="steps">The signed wheel displacement.</param>
    private void HandleZoom(float steps)
    {
        if (_ready)
            _view.Renderer.Zoom(steps);
    }

    /// <summary>
    /// Plays an already selected combat sound through the application's audio ownership boundary.
    /// </summary>
    /// <param name="clip">The sound chosen by the battle's bounded audio queue.</param>
    private void HandleSound(TacticalCombatSoundClip clip)
    {
        if (_disposed || AudioManager.Instance == null)
            return;
        _audioPlaybacks.RemoveAll(playback => !playback.IsActive);
        AudioPlaybackHandle playback = AudioManager.Instance.PlaySfxInstance(
            _assets.GetPreloadedAudio(clip.Path)
        );
        if (playback != null)
            _audioPlaybacks.Add(playback);
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
    /// <param name="shot">The applied discharge.</param>
    /// <param name="impact">The affected surface or destruction.</param>
    private void HandleImpact(TacticalCombatShot shot, TacticalCombatImpactKind impact) =>
        _view.Renderer.ShowImpact(shot, impact);

    /// <summary>
    /// Displays the station's superlaser without changing its queued damage command.
    /// </summary>
    /// <param name="station">The firing station.</param>
    /// <param name="target">The enemy capital ship.</param>
    private void HandleSuperlaser(TacticalCombatUnit station, TacticalCombatUnit target) =>
        _view.Renderer.ShowSuperlaser(station, target, _session.BattleDepth);

    /// <summary>
    /// Displays a unit destruction notice.
    /// </summary>
    /// <param name="unit">The destroyed unit.</param>
    private void HandleDestroyed(TacticalCombatUnit unit)
    {
        _selected.Remove(unit);
        _view.Renderer.ShowDestruction(unit);
        _notice = $"{unit.DisplayName} destroyed.";
    }

    /// <summary>
    /// Displays a unit departure notice.
    /// </summary>
    /// <param name="unit">The departed unit.</param>
    private void HandleWithdrawn(TacticalCombatUnit unit)
    {
        _selected.Remove(unit);
        _notice = $"{unit.DisplayName} entered hyperspace.";
    }

    /// <summary>
    /// Removes a recovered squadron from the active command selection.
    /// </summary>
    /// <param name="unit">The recovered squadron.</param>
    private void HandleRecovered(TacticalCombatUnit unit)
    {
        _selected.Remove(unit);
        _notice = $"{unit.DisplayName} recovered.";
    }

    /// <summary>
    /// Displays unavailable fighter recovery capacity.
    /// </summary>
    /// <param name="group">The affected fighter group.</param>
    private void HandleRecoveryUnavailable(TacticalCombatGroup group) =>
        _notice = "No carrier bay is available for fighter recovery.";

    /// <summary>
    /// Displays a component change without changing its simulation state.
    /// </summary>
    /// <param name="unit">The affected unit.</param>
    /// <param name="change">The applied component event.</param>
    private void HandleSystemChanged(TacticalCombatUnit unit, TacticalSystemEvent change) =>
        _notice =
            $"{unit.DisplayName}: {(change.IsRepair ? "component repaired" : "systems damaged")}.";

    /// <summary>
    /// Displays the current battle-station attack phase.
    /// </summary>
    /// <param name="run">The staged fighter attack.</param>
    private void HandleAttackRun(TacticalCombatAttackRun run) =>
        _notice = run.State switch
        {
            TacticalCombatAttackRunState.Succeeded => "The battle station was destroyed.",
            TacticalCombatAttackRunState.Failed => "The battle-station attack failed.",
            TacticalCombatAttackRunState.Cancelled => "The battle-station attack was cancelled.",
            _ => "Fighters are attacking the battle station.",
        };

    /// <summary>
    /// Constructs a direction-independent selection rectangle.
    /// </summary>
    /// <param name="first">The first point.</param>
    /// <param name="second">The second point.</param>
    /// <returns>The normalized rectangle.</returns>
    private static Rect GetRectangle(Vector2 first, Vector2 second) =>
        Rect.MinMaxRect(
            Mathf.Min(first.x, second.x),
            Mathf.Min(first.y, second.y),
            Mathf.Max(first.x, second.x),
            Mathf.Max(first.y, second.y)
        );

    /// <summary>
    /// Cancels pending preparation, removes subscriptions, and destroys the owned authored screen.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _cancellation.Cancel();
        _cancellation.Dispose();
        _view.ControlRequested -= HandleControl;
        _view.PointerPressed -= HandlePointerDown;
        _view.PointerReleased -= HandlePointerUp;
        _view.PointerDragged -= HandleDrag;
        _view.ZoomRequested -= HandleZoom;
        _session.ShotFired -= HandleShot;
        _session.Audio.SoundRequested -= HandleSound;
        foreach (AudioPlaybackHandle playback in _audioPlaybacks)
            playback.Stop();
        _audioPlaybacks.Clear();
        _session.ShotApplied -= HandleImpact;
        _session.SuperlaserFired -= HandleSuperlaser;
        _session.UnitDestroyed -= HandleDestroyed;
        _session.UnitWithdrawn -= HandleWithdrawn;
        _session.FighterRecovered -= HandleRecovered;
        _session.RecoveryUnavailable -= HandleRecoveryUnavailable;
        _session.SystemChanged -= HandleSystemChanged;
        _session.AttackRunChanged -= HandleAttackRun;
        UnityEngine.Object.Destroy(_view.gameObject);
    }
}
