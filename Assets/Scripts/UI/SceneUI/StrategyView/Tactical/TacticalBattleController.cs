using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drives an interactive tactical battle: playback, inspection, selection, orders, sound, and
/// completion.
/// </summary>
internal sealed class TacticalBattleController : ICancelable
{
    private const float _completionDelaySeconds = 1.2f;
    private const float _maximumWheelSteps = 3f;
    private const float _clickDragThreshold = 4f;
    private const float _minimumPickRadius = 10f;
    private const float _keyboardPanRate = 0.6f;
    private const float _maximumEffectStep = 0.1f;
    private const float _maximumDeployHoldSeconds = 8f;
    private const float _maximumDeployHoldStep = 1f;
    private const float _keyboardYawDegreesPerSecond = 90f;
    private const float _dragYawDegreesPerPixel = 0.4f;
    private const float _dragPitchDegreesPerPixel = 0.3f;
    private const float _keyboardPitchDegreesPerSecond = 45f;
    private static readonly float[] _speeds = { 1f, 2f, 4f, 8f };

    private readonly TacticalBattleView _view;
    private readonly Func<UIContext> _getUIContext;
    private readonly Action<string> _playSfx;
    private readonly Action _markDirty;
    private readonly Func<ContentModelCache> _getModelCache;
    private readonly Action<string, Action> _playCutscene;
    private readonly TacticalBattleProjector _projector = new TacticalBattleProjector();
    private readonly TacticalBattleAudio _audio;
    private readonly List<TacticalBattleUnit> _selection = new List<TacticalBattleUnit>();
    private readonly List<TacticalBattleUnit> _highlighted = new List<TacticalBattleUnit>();
    private TacticalSpaceBattle _battle;
    private Planet _planet;
    private string _attackerFactionId;
    private string _defenderFactionId;
    private string _attackerName;
    private string _defenderName;
    private Action<TacticalSpaceBattle> _completed;
    private TacticalBattleUnit _inspected;
    private TacticalBattleUnit _followed;
    private TacticalBattleUnit _selectionAnchor;
    private bool _extendSelection;
    private bool _addToSelection;
    private float _deployHoldRemaining;
    private CombatSide _rosterSide;
    private bool _paused;
    private int _speedIndex;
    private bool _pressed;
    private bool _dragActive;
    private Vector2 _pressPoint;
    private Vector2 _dragPoint;
    private bool _rotatePressed;
    private bool _rotateDragActive;
    private Vector2 _rotatePressPoint;
    private Vector2 _rotateLastPoint;
    private bool _panPressed;
    private Vector2 _panLastPoint;
    private float _completionCountdown;
    private bool _completionReported;
    private bool _superlaserArmed;
    private bool _cutscenePlaying;

    internal bool IsActive => _battle != null;

    internal bool IsPaused => _paused;

    internal float Speed => _speeds[_speedIndex];

    internal IReadOnlyList<TacticalBattleUnit> Selection => _selection;

    internal TacticalBattleUnit Inspected => _inspected;

    /// <summary>
    /// Gets whether the player has armed the superlaser and is choosing its target.
    /// </summary>
    internal bool IsSuperlaserArmed => _superlaserArmed;

    /// <summary>
    /// Gets whether a trench-run movie is playing over the battle, which holds it.
    /// </summary>
    internal bool IsCutscenePlaying => _cutscenePlaying;

    /// <summary>
    /// Gets whether the battle is holding at its opening positions while hull models load.
    /// </summary>
    internal bool IsDeploying => IsActive && _deployHoldRemaining > 0f;

    /// <summary>
    /// Creates a tactical battle controller bound to an authored battle view.
    /// </summary>
    /// <param name="view">The authored battle view.</param>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    /// <param name="playSfx">Plays a strategy sound-effect path.</param>
    /// <param name="markDirty">Invalidates strategy presentation after the battle changes state.</param>
    /// <param name="isSoundAvailable">
    /// Reports whether a sound-effect content path exists, or null to assume every path exists.
    /// </param>
    /// <param name="getModelCache">
    /// Returns the cache that loads unit models, or null to present placeholder hulls only.
    /// </param>
    /// <param name="playCutscene">
    /// Plays a full-screen movie from a content address and invokes the callback when it ends,
    /// or null to show trench-run outcomes without a movie.
    /// </param>
    internal TacticalBattleController(
        TacticalBattleView view,
        Func<UIContext> getUIContext,
        Action<string> playSfx,
        Action markDirty,
        Func<string, bool> isSoundAvailable = null,
        Func<ContentModelCache> getModelCache = null,
        Action<string, Action> playCutscene = null
    )
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        _playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        _getModelCache = getModelCache;
        _playCutscene = playCutscene;
        _audio = new TacticalBattleAudio(_playSfx, isSoundAvailable);
        _view.RetreatRequested += HandleRetreatRequested;
        _view.AutoToggleRequested += HandleAutoToggleRequested;
        _view.PauseToggleRequested += HandlePauseToggleRequested;
        _view.SpeedCycleRequested += HandleSpeedCycleRequested;
        _view.ControlPressed += HandleControlPressed;
        _view.ArenaPressed += HandleArenaPressed;
        _view.ArenaDragged += HandleArenaDragged;
        _view.ArenaReleased += HandleArenaReleased;
        _view.ArenaScrolled += HandleArenaScrolled;
        _view.TargetPriorityToggleRequested += HandleTargetPriorityToggleRequested;
        _view.RecoverRequested += HandleRecoverRequested;
        _view.RosterSidePressed += HandleRosterSidePressed;
        _view.RosterShipPressed += HandleRosterShipPressed;
        _view.RosterSquadronPressed += HandleRosterSquadronPressed;
        _view.SuperlaserPressed += HandleSuperlaserPressed;
    }

    /// <summary>
    /// Starts presenting a battle.
    /// </summary>
    /// <param name="battle">The battle to present.</param>
    /// <param name="planet">The battle planet.</param>
    /// <param name="attackerFactionId">The attacking faction's instance identifier.</param>
    /// <param name="defenderFactionId">The defending faction's instance identifier.</param>
    /// <param name="attackerName">The attacking faction's display name.</param>
    /// <param name="defenderName">The defending faction's display name.</param>
    /// <param name="completed">Invoked once when the battle has ended and its result can be applied.</param>
    internal void Begin(
        TacticalSpaceBattle battle,
        Planet planet,
        string attackerFactionId,
        string defenderFactionId,
        string attackerName,
        string defenderName,
        Action<TacticalSpaceBattle> completed
    )
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        // Verify the authored view before the battle becomes active, so a stale prefab fails
        // once here instead of on every update.
        TacticalBattleScene scene = _view.Scene;
        _battle = battle;
        _planet = planet;
        _attackerFactionId = attackerFactionId;
        _defenderFactionId = defenderFactionId;
        _attackerName = attackerName;
        _defenderName = defenderName;
        _completed = completed;
        _selection.Clear();
        _inspected = null;
        _followed = null;
        _selectionAnchor = null;
        _extendSelection = false;
        _addToSelection = false;
        _paused = false;
        _speedIndex = 0;
        _pressed = false;
        _dragActive = false;
        _completionCountdown = _completionDelaySeconds;
        _completionReported = false;
        _superlaserArmed = false;
        _cutscenePlaying = false;
        _rosterSide = battle.PlayerSide ?? CombatSide.Attacker;
        _deployHoldRemaining = _maximumDeployHoldSeconds;
        scene.Begin(_getModelCache?.Invoke());
        _view.Show();
        _audio.Begin(battle.Units.Any(unit => unit.IsStarfighter));
        Render();
        _markDirty();
    }

    /// <summary>
    /// Advances playback and redraws the battle without keyboard camera input.
    /// </summary>
    /// <param name="deltaTime">The elapsed real seconds since the previous update.</param>
    internal void Update(float deltaTime)
    {
        Update(deltaTime, default);
    }

    /// <summary>
    /// Advances playback, applies keyboard camera input, and redraws the battle.
    /// </summary>
    /// <param name="deltaTime">The elapsed real seconds since the previous update.</param>
    /// <param name="cameraInput">This frame's keyboard camera input.</param>
    internal void Update(float deltaTime, TacticalCameraInput cameraInput)
    {
        if (!IsActive)
            return;

        // The fight holds at its opening positions while hull models are still loading, so it
        // never starts on placeholders or loses time to a loading hitch. A long frame counts for
        // at most one step, and the hold as a whole is capped.
        if (_deployHoldRemaining > 0f && (_battle.IsComplete || !_view.Scene.HasPendingModelLoads))
            _deployHoldRemaining = 0f;

        // A trench-run movie holds everything beneath it: the fight, its ending, and the
        // keys the player presses to skip the movie.
        if (_cutscenePlaying)
        {
            deltaTime = 0f;
            cameraInput = default;
        }

        // Clicks arrive between updates, so the selection keys are remembered from the frame.
        _extendSelection = cameraInput.ExtendSelection;
        _addToSelection = cameraInput.AddToSelection;
        if (_battle.IsComplete)
        {
            _battle.Advance(deltaTime);
            _completionCountdown -= deltaTime;
        }
        else if (_deployHoldRemaining > 0f)
        {
            _deployHoldRemaining -= Mathf.Min(deltaTime, _maximumDeployHoldStep);
        }
        else if (!_paused)
        {
            _battle.Advance(deltaTime * Speed);
        }

        _audio.Advance(deltaTime);
        // A loading hitch on the first frames must not swallow the hyperspace arrival.
        _view.Scene.Advance(Mathf.Min(deltaTime, _maximumEffectStep));
        IReadOnlyList<TacticalBattleEvent> events = _battle.TakeEvents();
        _audio.Handle(events);
        _view.Scene.Handle(events);
        HandleStationEvents(events);
        PruneSelection();
        FollowInspectedUnit();
        ApplyCameraInput(cameraInput, deltaTime);
        Render();

        if (
            _battle.IsComplete
            && _completionCountdown <= 0f
            && !_completionReported
            && !_cutscenePlaying
        )
        {
            _completionReported = true;
            _completed?.Invoke(_battle);
        }
    }

    /// <summary>
    /// Shows or hides the battle layer without ending the battle, for example while a menu is
    /// open above it.
    /// </summary>
    /// <param name="visible">Whether the battle layer is visible.</param>
    internal void SetVisible(bool visible)
    {
        if (!IsActive || _view.gameObject.activeSelf == visible)
            return;

        _view.SetVisible(visible);
    }

    /// <summary>
    /// Stops presenting the current battle.
    /// </summary>
    internal void End()
    {
        _battle = null;
        _planet = null;
        _completed = null;
        _selection.Clear();
        _inspected = null;
        _followed = null;
        _selectionAnchor = null;
        _extendSelection = false;
        _addToSelection = false;
        _pressed = false;
        _dragActive = false;
        _rotatePressed = false;
        _rotateDragActive = false;
        _panPressed = false;
        _superlaserArmed = false;
        _cutscenePlaying = false;
        _view.Scene.End();
        _view.Hide();
        _markDirty();
    }

    /// <summary>
    /// Toggles pause when the player cancels during a battle.
    /// </summary>
    /// <returns>True when a battle consumed the cancel request.</returns>
    public bool TryCancel()
    {
        if (!IsActive)
            return false;

        // Cancel does nothing under a movie, and backs out of superlaser targeting before it
        // pauses.
        if (_cutscenePlaying)
            return true;
        if (_superlaserArmed)
        {
            _superlaserArmed = false;
            return true;
        }

        HandlePauseToggleRequested();
        return true;
    }

    /// <summary>
    /// Arms the charged superlaser so the next click on an enemy capital ship fires it, or
    /// disarms it when it is already armed.
    /// </summary>
    private void HandleSuperlaserPressed()
    {
        if (!IsActive || _cutscenePlaying || _battle.PlayerSide == null)
            return;

        TacticalBattleStation station = _battle.GetStation(_battle.PlayerSide.Value);
        _superlaserArmed =
            !_superlaserArmed
            && station?.IsSuperlaserReady == true
            && _battle.CanCommandStation(station);
    }

    /// <summary>
    /// Fires the armed superlaser at a unit and disarms it when the shot is taken.
    /// </summary>
    /// <param name="target">The unit the player picked.</param>
    /// <returns>True when the superlaser fired.</returns>
    private bool TryFireSuperlaser(TacticalBattleUnit target)
    {
        TacticalBattleStation station =
            _battle.PlayerSide == null ? null : _battle.GetStation(_battle.PlayerSide.Value);
        if (station == null || !_battle.FireSuperlaser(station, target))
            return false;

        _superlaserArmed = false;
        return true;
    }

    /// <summary>
    /// Finds the enemy battle station drawn under a source-space point.
    /// </summary>
    /// <param name="point">The source-space position.</param>
    /// <returns>The station under the point, or null.</returns>
    private TacticalBattleStation FindEnemyStationAt(Vector2 point)
    {
        RectInt arena = _view.GetArenaSourceRect();
        foreach (TacticalBattleStation station in _battle.Stations)
        {
            if (!station.IsInBattle || station.Side == _battle.PlayerSide)
                continue;
            if (
                !_view.Scene.TryProjectStation(
                    station,
                    arena,
                    out Vector2 center,
                    out float diameter
                )
            )
                continue;

            if (Vector2.Distance(point, center) <= Mathf.Max(diameter * 0.5f, _minimumPickRadius))
                return station;
        }

        return null;
    }

    /// <summary>
    /// Reacts to trench-run outcomes: the camera turns to a station that is about to blow, and
    /// the outcome's movie plays.
    /// </summary>
    /// <param name="events">The events raised since the previous update.</param>
    private void HandleStationEvents(IReadOnlyList<TacticalBattleEvent> events)
    {
        foreach (TacticalBattleEvent battleEvent in events)
        {
            bool succeeded = battleEvent.Kind == TacticalBattleEventKind.TrenchRunSucceeded;
            if (!succeeded && battleEvent.Kind != TacticalBattleEventKind.TrenchRunFailed)
                continue;

            if (succeeded)
            {
                // Leave the camera on the station so its end is on screen when the movie closes.
                _followed = null;
                _view.Scene.LookToward(battleEvent.Station);
            }

            PlayTrenchRunCutscene(battleEvent.Station, succeeded);
        }
    }

    /// <summary>
    /// Plays the movie for a trench run's outcome over the battle, holding the battle until it
    /// ends. Without a movie for the station's ship type the battle simply carries on.
    /// </summary>
    /// <param name="station">The attacked station.</param>
    /// <param name="succeeded">Whether the run destroyed the station.</param>
    private void PlayTrenchRunCutscene(TacticalBattleStation station, bool succeeded)
    {
        string path = GetTrenchRunCutscenePath(
            _getUIContext()?.GetPlayerFactionTheme(),
            station.Ship.TypeID,
            succeeded
        );
        Debug.Log(
            $"Tactical battle: the trench run on {station.DisplayName} {(succeeded ? "succeeded" : "failed")}; movie '{path}'."
        );
        if (_playCutscene == null || string.IsNullOrWhiteSpace(path))
            return;

        _cutscenePlaying = true;
        _superlaserArmed = false;
        _pressed = false;
        _dragActive = false;
        _rotatePressed = false;
        _rotateDragActive = false;
        _panPressed = false;
        TacticalSpaceBattle battle = _battle;
        try
        {
            _playCutscene(
                path,
                () =>
                {
                    if (ReferenceEquals(_battle, battle))
                        _cutscenePlaying = false;
                }
            );
        }
        catch (Exception exception)
        {
            _cutscenePlaying = false;
            Debug.LogWarning(
                $"Tactical battle: movie '{path}' could not play. {exception.Message}"
            );
        }
    }

    /// <summary>
    /// Returns the movie a theme shows for a trench run on one ship type.
    /// </summary>
    /// <param name="theme">The viewing player's theme.</param>
    /// <param name="shipTypeId">The attacked station's ship type.</param>
    /// <param name="succeeded">Whether the run destroyed the station.</param>
    /// <returns>The movie's content address, or null when the theme has none.</returns>
    internal static string GetTrenchRunCutscenePath(
        FactionTheme theme,
        string shipTypeId,
        bool succeeded
    )
    {
        TrenchRunCutscene cutscene = theme?.TrenchRunCutscenes?.FirstOrDefault(entry =>
            entry?.ShipTypeID == shipTypeId
        );
        return succeeded ? cutscene?.SuccessPath : cutscene?.FailurePath;
    }

    /// <summary>
    /// Orders the player's side to withdraw.
    /// </summary>
    private void HandleRetreatRequested()
    {
        if (!IsActive || _battle.PlayerSide == null)
            return;

        if (_battle.OrderWithdrawal(_battle.PlayerSide.Value))
            _selection.Clear();
    }

    /// <summary>
    /// Toggles automatic control of the player's side.
    /// </summary>
    private void HandleAutoToggleRequested()
    {
        if (!IsActive || _battle.PlayerSide == null || _battle.IsComplete)
            return;

        CombatSide side = _battle.PlayerSide.Value;
        _battle.SetAutomatic(side, !_battle.IsAutomatic(side));
        _selection.Clear();
    }

    /// <summary>
    /// Toggles playback pause.
    /// </summary>
    private void HandlePauseToggleRequested()
    {
        if (!IsActive)
            return;

        _paused = !_paused;
    }

    /// <summary>
    /// Cycles the playback speed.
    /// </summary>
    private void HandleSpeedCycleRequested()
    {
        if (!IsActive)
            return;

        _speedIndex = (_speedIndex + 1) % _speeds.Length;
    }

    /// <summary>
    /// Plays the shared strategy control sound.
    /// </summary>
    private void HandleControlPressed()
    {
        _playSfx(StrategyUISoundPaths.ControlPress);
    }

    /// <summary>
    /// Starts a gesture: left selects, right orders or rotates, middle pans the camera.
    /// </summary>
    /// <param name="point">The source-space press position.</param>
    /// <param name="button">The pressed pointer button.</param>
    private void HandleArenaPressed(Vector2 point, PointerEventData.InputButton button)
    {
        if (!IsActive || _cutscenePlaying)
            return;

        switch (button)
        {
            case PointerEventData.InputButton.Left when !_battle.IsComplete:
                _pressed = true;
                _dragActive = false;
                _pressPoint = point;
                _dragPoint = point;
                break;
            case PointerEventData.InputButton.Right:
                _rotatePressed = true;
                _rotateDragActive = false;
                _rotatePressPoint = point;
                _rotateLastPoint = point;
                break;
            case PointerEventData.InputButton.Middle:
                _panPressed = true;
                _panLastPoint = point;
                break;
        }
    }

    /// <summary>
    /// Extends the drag-selection rectangle, rotates, or pans the camera.
    /// </summary>
    /// <param name="point">The source-space drag position.</param>
    /// <param name="button">The dragging pointer button.</param>
    private void HandleArenaDragged(Vector2 point, PointerEventData.InputButton button)
    {
        if (!IsActive)
            return;

        switch (button)
        {
            case PointerEventData.InputButton.Left when _pressed:
                _dragPoint = point;
                if ((point - _pressPoint).magnitude >= _clickDragThreshold)
                    _dragActive = true;
                break;
            case PointerEventData.InputButton.Right when _rotatePressed:
                if ((point - _rotatePressPoint).magnitude >= _clickDragThreshold)
                    _rotateDragActive = true;
                if (_rotateDragActive)
                    _view.Scene.Rotate(
                        (point.x - _rotateLastPoint.x) * _dragYawDegreesPerPixel,
                        (_rotateLastPoint.y - point.y) * _dragPitchDegreesPerPixel
                    );
                _rotateLastPoint = point;
                break;
            case PointerEventData.InputButton.Middle when _panPressed:
                _followed = null;
                _view.Scene.Pan(_view.GetArenaSourceRect(), _panLastPoint, point);
                _panLastPoint = point;
                break;
        }
    }

    /// <summary>
    /// Completes a selection, issues an order for an un-dragged right click, or ends a pan.
    /// </summary>
    /// <param name="point">The source-space release position.</param>
    /// <param name="button">The released pointer button.</param>
    private void HandleArenaReleased(Vector2 point, PointerEventData.InputButton button)
    {
        if (_cutscenePlaying)
            return;

        switch (button)
        {
            case PointerEventData.InputButton.Left when _pressed:
                _pressed = false;
                if (!IsActive)
                    return;
                // While the superlaser is armed a click picks its target and nothing else.
                if (_superlaserArmed)
                    TryFireSuperlaser(FindUnitAt(point));
                else if (_dragActive)
                    SelectInBox(_pressPoint, point);
                else
                    SelectAt(point);
                _dragActive = false;
                break;
            case PointerEventData.InputButton.Right when _rotatePressed:
                _rotatePressed = false;
                if (IsActive && !_rotateDragActive && !_battle.IsComplete)
                {
                    // A right-click backs out of superlaser targeting instead of giving an order.
                    if (_superlaserArmed)
                        _superlaserArmed = false;
                    else
                        IssueOrderAt(point);
                }

                _rotateDragActive = false;
                break;
            case PointerEventData.InputButton.Middle:
                _panPressed = false;
                break;
        }
    }

    /// <summary>
    /// Zooms the camera with the mouse wheel.
    /// </summary>
    /// <param name="delta">The wheel delta; positive turns zoom in.</param>
    private void HandleArenaScrolled(float delta)
    {
        if (!IsActive || delta == 0f)
            return;

        // A wheel notch is one step. A trackpad or a Magic Mouse reports one gesture as a run of
        // small deltas, so each of those zooms in proportion instead of a whole step.
        _view.Scene.Zoom(Mathf.Clamp(delta, -_maximumWheelSteps, _maximumWheelSteps));
    }

    /// <summary>
    /// Applies one frame of keyboard camera input.
    /// </summary>
    /// <param name="input">The camera input.</param>
    /// <param name="deltaTime">The elapsed real seconds since the previous update.</param>
    private void ApplyCameraInput(TacticalCameraInput input, float deltaTime)
    {
        if (input.IsEmpty)
            return;

        TacticalBattleScene scene = _view.Scene;
        if (input.ResetView)
        {
            _followed = null;
            scene.ResetView(_battle);
        }

        if (input.CycleShips)
            CycleFriendlyShip();
        if (input.TogglePause)
            HandlePauseToggleRequested();
        if (input.Pan != Vector2.zero)
        {
            // Panning hands the camera back to the player.
            _followed = null;
            scene.PanBy(input.Pan * (_keyboardPanRate * deltaTime));
        }

        if (input.Yaw != 0f || input.Pitch != 0f)
            scene.Rotate(
                input.Yaw * _keyboardYawDegreesPerSecond * deltaTime,
                input.Pitch * _keyboardPitchDegreesPerSecond * deltaTime
            );
        if (input.ZoomSteps != 0f)
            scene.Zoom(input.ZoomSteps);
    }

    /// <summary>
    /// Picks the unit under a point, or clears the selection when there is none. With a
    /// selection key held the click adds that unit to the selection, or takes it out again, and
    /// a click on empty space leaves the selection as it is.
    /// </summary>
    /// <param name="point">The source-space position.</param>
    private void SelectAt(Vector2 point)
    {
        TacticalBattleUnit unit = FindUnitAt(point);
        if (_extendSelection || _addToSelection)
        {
            if (unit != null)
                AddOrRemove(unit);
            return;
        }

        if (unit != null)
        {
            Pick(unit);
            return;
        }

        _selection.Clear();
        _inspected = null;
        _followed = null;
    }

    /// <summary>
    /// Picks one unit with a plain click, in the arena or on its roster slot. The first click
    /// inspects it, and selects it alone when the player can command it, without moving the
    /// camera. A click on the unit that is already picked is the second click of a pair: it
    /// zooms the camera in on the unit and follows it.
    /// </summary>
    /// <param name="unit">The clicked unit.</param>
    private void Pick(TacticalBattleUnit unit)
    {
        if (IsPickedAlone(unit))
        {
            FocusUnit(unit);
            return;
        }

        _inspected = unit;
        _selection.Clear();
        if (_battle.CanCommand(unit))
        {
            _selection.Add(unit);
            _selectionAnchor = unit;
        }
    }

    /// <summary>
    /// Returns whether a unit is already the one unit picked: it is the inspected unit and the
    /// selection holds nothing else.
    /// </summary>
    /// <param name="unit">The unit to check.</param>
    /// <returns>True when another plain click on the unit should zoom to it.</returns>
    private bool IsPickedAlone(TacticalBattleUnit unit)
    {
        return _inspected == unit
            && (_selection.Count == 0 || (_selection.Count == 1 && _selection[0] == unit));
    }

    /// <summary>
    /// Adds a unit the player can command to the selection, or takes it out when it is already
    /// there. Any other unit is only inspected, and the selection stays as it is.
    /// </summary>
    /// <param name="unit">The unit clicked with a selection key held.</param>
    private void AddOrRemove(TacticalBattleUnit unit)
    {
        if (!_battle.CanCommand(unit))
        {
            _inspected = unit;
            return;
        }

        if (_selection.Remove(unit))
        {
            if (_inspected == unit)
                _inspected = _selection.Count > 0 ? _selection[_selection.Count - 1] : null;
            return;
        }

        _selection.Add(unit);
        _inspected = unit;
        _selectionAnchor = unit;
    }

    /// <summary>
    /// Handles a click on a roster slot. A plain click picks that unit alone, and a second one
    /// zooms to it. With the extend key held it selects every unit of the roster from the last
    /// unit picked through this one; with the add key held it adds the unit to the selection
    /// or takes it out.
    /// </summary>
    /// <param name="unit">The unit in the clicked slot.</param>
    private void PickFromRoster(TacticalBattleUnit unit)
    {
        if (_extendSelection && SelectRosterRun(unit))
            return;

        if (_extendSelection || _addToSelection)
            AddOrRemove(unit);
        else
            Pick(unit);
    }

    /// <summary>
    /// Selects every unit the player can command in the listed roster from the last unit picked
    /// through the one clicked: ships first, then squadrons, in the order the bar shows them.
    /// The run replaces the selection, or joins it when the add key is held as well.
    /// </summary>
    /// <param name="unit">The unit that ends the run.</param>
    /// <returns>
    /// False when the unit cannot be commanded or no earlier pick in this roster starts a run.
    /// </returns>
    private bool SelectRosterRun(TacticalBattleUnit unit)
    {
        if (_selectionAnchor == null || !_battle.CanCommand(unit))
            return false;

        List<TacticalBattleUnit> roster = new List<TacticalBattleUnit>();
        roster.AddRange(TacticalBattleProjector.GetRosterShips(_battle, _rosterSide));
        roster.AddRange(TacticalBattleProjector.GetRosterSquadrons(_battle, _rosterSide));
        int from = roster.IndexOf(_selectionAnchor);
        int to = roster.IndexOf(unit);
        if (from < 0 || to < 0)
            return false;

        if (!_addToSelection)
            _selection.Clear();
        for (int index = Math.Min(from, to); index <= Math.Max(from, to); index++)
        {
            TacticalBattleUnit member = roster[index];
            if (_battle.CanCommand(member) && !_selection.Contains(member))
                _selection.Add(member);
        }

        _inspected = unit;
        return true;
    }

    /// <summary>
    /// Selects every commandable ship inside a drag rectangle, adding them to the selection
    /// instead of replacing it when a selection key is held.
    /// </summary>
    /// <param name="first">One source-space corner.</param>
    /// <param name="second">The opposite source-space corner.</param>
    private void SelectInBox(Vector2 first, Vector2 second)
    {
        bool adding = _extendSelection || _addToSelection;
        if (!adding)
            _selection.Clear();
        Vector2 min = Vector2.Min(first, second);
        Vector2 max = Vector2.Max(first, second);
        RectInt arena = _view.GetArenaSourceRect();
        TacticalBattleScene scene = _view.Scene;
        foreach (TacticalBattleUnit unit in _battle.Units)
        {
            if (!_battle.CanCommand(unit))
                continue;
            if (!scene.TryProject(unit.X, unit.Y, arena, out Vector2 point, unit.Altitude))
                continue;

            if (
                point.x >= min.x
                && point.x <= max.x
                && point.y >= min.y
                && point.y <= max.y
                && !_selection.Contains(unit)
            )
                _selection.Add(unit);
        }

        if (!adding || _inspected == null)
            _inspected = _selection.Count > 0 ? _selection[0] : null;
    }

    /// <summary>
    /// Issues an attack or move order to the selection at a point.
    /// </summary>
    /// <param name="point">The source-space position.</param>
    private void IssueOrderAt(Vector2 point)
    {
        if (_selection.Count == 0)
            return;

        TacticalBattleUnit target = FindUnitAt(point);
        if (target != null && target.Side != _battle.PlayerSide)
        {
            _battle.IssueAttackOrder(_selection, target);
            return;
        }

        // A click on one of the player's own capital ships posts the selected squadrons around
        // it as a screen. Ships selected with them take the click as the move order it always
        // was.
        if (target is TacticalCapitalShipUnit)
            _battle.IssueScreenOrder(_selection, target);

        // An enemy battle station stands beyond the arena: a click on it sends the selected
        // squadrons to fly its trench, and never moves the ships selected with them.
        TacticalBattleStation station = target == null ? FindEnemyStationAt(point) : null;
        if (station != null)
        {
            _battle.IssueTrenchRunOrder(_selection, station);
            return;
        }

        // The click is cast onto the deck the selected ships stand on, so they end up under the
        // pointer rather than above or below it.
        if (
            _view.Scene.TryPickGround(
                _view.GetArenaSourceRect(),
                point,
                out Vector2 battlePoint,
                GetSelectionAltitude()
            )
        )
            _battle.IssueMoveOrder(_selection, battlePoint.x, battlePoint.y);
    }

    /// <summary>
    /// Returns the height a move order is aimed at: the mean height of the selected capital
    /// ships, which is one ship's own deck when one is selected.
    /// </summary>
    /// <returns>The height above the battle plane, or zero with no ship selected.</returns>
    private double GetSelectionAltitude()
    {
        double total = 0;
        int ships = 0;
        foreach (TacticalBattleUnit unit in _selection)
        {
            if (unit.IsStarfighter)
                continue;

            total += unit.Altitude;
            ships++;
        }

        return ships > 0 ? total / ships : 0;
    }

    /// <summary>
    /// Removes selected units that can no longer be commanded and clears a lost inspection.
    /// </summary>
    private void PruneSelection()
    {
        for (int index = _selection.Count - 1; index >= 0; index--)
        {
            if (!_battle.CanCommand(_selection[index]))
                _selection.RemoveAt(index);
        }

        if (_inspected != null && (!_inspected.IsAlive || _inspected.HasWithdrawn))
        {
            _inspected = null;
            _followed = null;
        }

        if (_selectionAnchor != null && !_battle.CanCommand(_selectionAnchor))
            _selectionAnchor = null;

        // The camera lets go of a squadron that has flown off the battlefield, into a trench or
        // aboard a ship.
        if (_followed?.IsOffField == true)
            _followed = null;

        if (_superlaserArmed)
        {
            TacticalBattleStation station =
                _battle.PlayerSide == null ? null : _battle.GetStation(_battle.PlayerSide.Value);
            if (station?.IsSuperlaserReady != true || !_battle.CanCommandStation(station))
                _superlaserArmed = false;
        }
    }

    /// <summary>
    /// Finds the unit under a source-space point by casting onto the battle plane.
    /// </summary>
    /// <param name="point">The source-space position.</param>
    /// <returns>The unit under the point, or null.</returns>
    private TacticalBattleUnit FindUnitAt(Vector2 point)
    {
        RectInt arena = _view.GetArenaSourceRect();
        TacticalBattleScene scene = _view.Scene;
        TacticalBattleUnit best = null;
        float bestScore = float.MaxValue;
        foreach (TacticalBattleUnit unit in _battle.Units)
        {
            if (!unit.IsAlive || unit.HasWithdrawn || unit.IsOffField)
                continue;
            if (!scene.TryProjectUnit(unit, arena, out Vector2 center, out float diameter))
                continue;

            // Pick against the drawn hull, so clicks anywhere on a ship land on it.
            float radius = Mathf.Max(diameter * 0.5f, _minimumPickRadius);
            float score = Vector2.Distance(point, center) / radius;
            if (score <= 1f && score < bestScore)
            {
                best = unit;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>
    /// Selects, inspects, and frames the next of the player's ships after the current one.
    /// </summary>
    private void CycleFriendlyShip()
    {
        if (_battle.PlayerSide == null)
            return;

        List<TacticalCapitalShipUnit> ships = new List<TacticalCapitalShipUnit>();
        foreach (
            TacticalCapitalShipUnit ship in TacticalBattleProjector.GetRosterShips(
                _battle,
                _battle.PlayerSide.Value
            )
        )
        {
            if (ship.IsAlive && !ship.HasWithdrawn)
                ships.Add(ship);
        }

        if (ships.Count == 0)
            return;

        TacticalBattleUnit current = _selection.Count == 1 ? _selection[0] : _inspected;
        int index = current is TacticalCapitalShipUnit currentShip
            ? ships.IndexOf(currentShip)
            : -1;
        FocusUnit(ships[(index + 1) % ships.Count]);
    }

    /// <summary>
    /// Keeps the camera centered on the followed unit while it is still the inspected unit and
    /// still in the battle; a fighter would otherwise fly out of view within a second.
    /// </summary>
    private void FollowInspectedUnit()
    {
        if (_followed == null)
            return;
        if (_followed != _inspected || !_followed.IsAlive || _followed.HasWithdrawn)
        {
            _followed = null;
            return;
        }

        _view.Scene.TrackUnit(_followed);
    }

    /// <summary>
    /// Inspects a unit, selects it when the player can command it, zooms the camera in on it,
    /// and follows it until the player pans or reframes.
    /// </summary>
    /// <param name="unit">The unit to focus.</param>
    private void FocusUnit(TacticalBattleUnit unit)
    {
        _inspected = unit;
        _selection.Clear();
        if (_battle.CanCommand(unit))
        {
            _selection.Add(unit);
            _selectionAnchor = unit;
        }

        _followed = unit.IsAlive && !unit.HasWithdrawn ? unit : null;
        if (_followed != null)
            _view.Scene.FocusOn(unit);
    }

    /// <summary>
    /// Flips the inspected unit's target priority, and the whole selection's when the inspected
    /// unit is part of it.
    /// </summary>
    private void HandleTargetPriorityToggleRequested()
    {
        if (!IsActive)
            return;

        TacticalBattleUnit focus = _inspected ?? (_selection.Count > 0 ? _selection[0] : null);
        if (focus == null || !_battle.CanSetTargetPriority(focus))
            return;

        TacticalTargetPriority priority =
            focus.TargetPriority == TacticalTargetPriority.Starfighters
                ? TacticalTargetPriority.CapitalShips
                : TacticalTargetPriority.Starfighters;
        IEnumerable<TacticalBattleUnit> affected = _selection.Contains(focus)
            ? _selection
            : new[] { focus };
        _battle.SetTargetPriority(affected, priority);
    }

    /// <summary>
    /// Recalls the selected squadrons to a hangar: each to the ship it is screening, else to its
    /// own carrier, or to the nearest friendly ship with a bay free when that has none.
    /// </summary>
    private void HandleRecoverRequested()
    {
        if (!IsActive || _battle.IsComplete)
            return;

        _battle.IssueRecoverOrder(_selection);
    }

    /// <summary>
    /// Switches the roster bar to the other side.
    /// </summary>
    private void HandleRosterSidePressed()
    {
        if (!IsActive)
            return;

        _rosterSide =
            _rosterSide == CombatSide.Attacker ? CombatSide.Defender : CombatSide.Attacker;
    }

    /// <summary>
    /// Picks the ship in a roster slot, alone or into the selection as the selection keys say.
    /// </summary>
    /// <param name="index">The ship slot index.</param>
    private void HandleRosterShipPressed(int index)
    {
        if (!IsActive)
            return;

        List<TacticalCapitalShipUnit> ships = TacticalBattleProjector.GetRosterShips(
            _battle,
            _rosterSide
        );
        if (index < 0 || index >= ships.Count)
            return;

        HandleControlPressed();
        // While the superlaser is armed, an enemy ship's roster slot picks its target.
        if (_superlaserArmed && TryFireSuperlaser(ships[index]))
            return;

        PickFromRoster(ships[index]);
    }

    /// <summary>
    /// Picks the squadron in a roster slot, alone or into the selection as the selection keys
    /// say.
    /// </summary>
    /// <param name="index">The squadron slot index, counted across every fighter type.</param>
    private void HandleRosterSquadronPressed(int index)
    {
        if (!IsActive)
            return;

        List<TacticalStarfighterUnit> squadrons = TacticalBattleProjector.GetRosterSquadrons(
            _battle,
            _rosterSide
        );
        if (index < 0 || index >= squadrons.Count)
            return;

        HandleControlPressed();
        PickFromRoster(squadrons[index]);
    }

    /// <summary>
    /// Lists the units that carry a selection box: the player's selection, and the inspected
    /// unit when it is not part of that, such as an enemy ship the player clicked.
    /// </summary>
    /// <returns>The units to box, without repeats.</returns>
    private IReadOnlyList<TacticalBattleUnit> GetHighlightedUnits()
    {
        _highlighted.Clear();
        _highlighted.AddRange(_selection);
        if (_inspected != null && !_selection.Contains(_inspected))
            _highlighted.Add(_inspected);
        return _highlighted;
    }

    /// <summary>
    /// Projects and draws the current frame.
    /// </summary>
    private void Render()
    {
        RectInt? selectionBox = null;
        if (_pressed && _dragActive)
        {
            Vector2 min = Vector2.Min(_pressPoint, _dragPoint);
            Vector2 max = Vector2.Max(_pressPoint, _dragPoint);
            selectionBox = new RectInt(
                Mathf.RoundToInt(min.x),
                Mathf.RoundToInt(min.y),
                Mathf.RoundToInt(max.x - min.x),
                Mathf.RoundToInt(max.y - min.y)
            );
        }

        RectInt arena = _view.GetArenaSourceRect();
        TacticalBattleScene scene = _view.Scene;
        UIContext uiContext = _getUIContext();
        Color attackerColor = TacticalBattleProjector.GetSideColor(
            _battle,
            CombatSide.Attacker,
            uiContext,
            _attackerFactionId
        );
        Color defenderColor = TacticalBattleProjector.GetSideColor(
            _battle,
            CombatSide.Defender,
            uiContext,
            _defenderFactionId
        );
        scene.Sync(_battle, arena);
        scene.ShowSelection(GetHighlightedUnits(), attackerColor, defenderColor);
        scene.ShowDeckMarkers(_battle, attackerColor, defenderColor);
        _view.Render(
            _projector.Project(
                _battle,
                arena,
                scene,
                uiContext,
                _planet,
                _attackerFactionId,
                _defenderFactionId,
                _attackerName,
                _defenderName,
                _selection,
                _inspected ?? (_selection.Count > 0 ? _selection[0] : null),
                selectionBox,
                _paused,
                Speed,
                _rosterSide,
                IsDeploying,
                _superlaserArmed
            )
        );
    }
}
