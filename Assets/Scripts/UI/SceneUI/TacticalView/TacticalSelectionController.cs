using System;
using System.Collections.Generic;
using Rebellion.Game.Combat;
using Rebellion.SceneGraph;
using Rebellion.Tactical;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Owns hull picking, multiselection, and transient selection in the tactical scene.
/// </summary>
public sealed class TacticalSelectionController : MonoBehaviour
{
    public const int ControlGroupCount = 9;

    [SerializeField]
    private TacticalSelectionView view;

    private readonly List<RaycastResult> _interfaceHits = new List<RaycastResult>();
    private Camera _camera;
    private IReadOnlyDictionary<SpaceCombatUnit, Transform> _presentations;
    private PointerEventData _pointer;
    private EventSystem _pointerEventSystem;
    private readonly List<SpaceCombatUnit> _selected = new List<SpaceCombatUnit>();
    private IReadOnlyList<SpaceCombatUnit> _readOnlySelection;
    private Vector2 _pressPosition;
    private bool _selecting;
    private bool _choosingDestination;
    private bool _choosingAttack;
    private TacticalViewCameraController _cameraInput;
    private string _viewerFactionInstanceID;
    private readonly List<SpaceCombatUnit>[] _controlGroups = CreateControlGroups();
    private readonly int[] _controlGroupCounts = new int[ControlGroupCount];
    private int _activeControlGroup = -1;
    private TacticalCarrierOperations _carrierOperations;
    private readonly Dictionary<string, SpaceCombatUnit> _combatantsByID = new Dictionary<
        string,
        SpaceCombatUnit
    >(StringComparer.Ordinal);
    private readonly List<SpaceCombatUnit> _attackTargets = new List<SpaceCombatUnit>();
    private readonly HashSet<SpaceCombatUnit> _targetSeen = new HashSet<SpaceCombatUnit>();
    private SpaceCombatUnit _hovered;

    public event Action<Vector2> MoveRequested;
    public event Action<Vector2> MoveTogetherRequested;
    public event Action<Vector2, bool> AttackRequested;
    public event Action StopRequested;
    public event Action LaunchRequested;
    public event Action RecallRequested;
    public event Action DockRequested;
    public event Action<SpaceCombatUnit> DockAtRequested;

    /// <summary>
    /// Gets the ordered transient selection without allowing callers to modify it.
    /// </summary>
    public IReadOnlyList<SpaceCombatUnit> SelectedCombatants =>
        _readOnlySelection ??= _selected.AsReadOnly();

    /// <summary>
    /// Gets the inspected unit, regardless of its owning faction.
    /// </summary>
    public SpaceCombatUnit SelectedCombatant => _selected.Count > 0 ? _selected[0] : null;

    /// <summary>
    /// Clears the current tactical selection and any transient selection command.
    /// </summary>
    /// <returns>Whether there was selection state to clear.</returns>
    public bool TryDeselect()
    {
        bool wasFollowing = _cameraInput && _cameraInput.FollowTarget;
        bool handled =
            _selected.Count > 0
            || _selecting
            || _choosingDestination
            || _choosingAttack
            || wasFollowing;
        if (!handled)
            return false;

        _selected.Clear();
        _selecting = false;
        _choosingDestination = false;
        _choosingAttack = false;
        _activeControlGroup = -1;
        if (wasFollowing)
            _cameraInput.StopFollowing();
        if (view)
        {
            view.RenderMarquee(Vector2.zero, Vector2.zero, false);
            RefreshView();
        }
        return true;
    }

    /// <summary>
    /// Connects roster selection to the same selection owner as world input.
    /// </summary>
    private void Awake()
    {
        view.CardClicked += SelectCard;
        view.CardDoubleClicked += CenterOnCard;
        view.MoveButton.onClick.AddListener(BeginMove);
        view.AttackButton.onClick.AddListener(BeginAttack);
        view.StopButton.onClick.AddListener(RequestStop);
        view.LaunchButton.onClick.AddListener(RequestLaunch);
        view.RecallButton.onClick.AddListener(RequestRecall);
        view.DockButton.onClick.AddListener(RequestDock);
        view.FollowButton.onClick.AddListener(ToggleFollow);
        view.ControlGroupClicked += HandleControlGroupClicked;
        view.OfficerMarkerClicked += SelectOfficerMarker;
    }

    /// <summary>
    /// Binds picking to the loaded presentation roots of one battle.
    /// </summary>
    /// <param name="camera">The camera displaying the battle.</param>
    /// <param name="presentations">The loaded combatants and their presentation roots.</param>
    /// <param name="viewerFactionInstanceID">The faction controlled by the local player.</param>
    public void Initialize(
        Camera camera,
        IReadOnlyDictionary<SpaceCombatUnit, Transform> presentations,
        string viewerFactionInstanceID = null
    )
    {
        if (camera == null)
            throw new ArgumentNullException(nameof(camera));
        if (presentations == null)
            throw new ArgumentNullException(nameof(presentations));
        if (view == null)
            throw new MissingReferenceException("Tactical selection view is missing.");

        Clear();
        _camera = camera;
        _presentations = presentations;
        _viewerFactionInstanceID = viewerFactionInstanceID;
        foreach (SpaceCombatUnit combatant in presentations.Keys)
            _combatantsByID[combatant.InstanceID] = combatant;
        _cameraInput = camera.GetComponent<TacticalViewCameraController>();
        if (_cameraInput)
            _cameraInput.RightClicked += RequestPointerMove;
    }

    /// <summary>
    /// Binds hover and right-click recovery eligibility to the battle's indexed carrier state.
    /// </summary>
    /// <param name="operations">The active battle's carrier operations.</param>
    public void BindCarrierOperations(TacticalCarrierOperations operations)
    {
        _carrierOperations = operations;
    }

    /// <summary>
    /// Selects the nearest loaded hull under a screen point, or clears empty-space selection.
    /// </summary>
    /// <param name="screenPosition">The pointer position in screen pixels.</param>
    /// <param name="additive">Whether to toggle this hull without replacing the selection.</param>
    public void SelectAt(Vector2 screenPosition, bool additive = false)
    {
        if (_camera == null || _presentations == null || IsOverInterface(screenPosition))
            return;
        if (!_camera.pixelRect.Contains(screenPosition))
            return;
        TryGetCombatantAt(screenPosition, out SpaceCombatUnit nearest);

        if (!additive)
            _selected.Clear();
        if (IsControlled(nearest) && (!additive || !_selected.Remove(nearest)))
            _selected.Add(nearest);
        _activeControlGroup = -1;
        RefreshView();
    }

    /// <summary>
    /// Releases selection and all references to a previous battle presentation.
    /// </summary>
    public void Clear()
    {
        _selected.Clear();
        _selecting = false;
        _choosingDestination = false;
        _choosingAttack = false;
        if (_cameraInput)
        {
            _cameraInput.StopFollowing();
            _cameraInput.RightClicked -= RequestPointerMove;
        }
        _cameraInput = null;
        _viewerFactionInstanceID = null;
        _carrierOperations = null;
        _combatantsByID.Clear();
        _attackTargets.Clear();
        _targetSeen.Clear();
        _hovered = null;
        _activeControlGroup = -1;
        foreach (List<SpaceCombatUnit> group in _controlGroups)
            group.Clear();
        Array.Clear(_controlGroupCounts, 0, _controlGroupCounts.Length);
        _presentations = null;
        _camera = null;
        _interfaceHits.Clear();
        _pointer = null;
        _pointerEventSystem = null;
        if (view)
        {
            view.BindCarrierOperations(null, null);
            view.RenderSelection(_selected, null, null);
            view.BindBattle(null, null, null, null, _selected);
            view.RenderMarquee(Vector2.zero, Vector2.zero, false);
            view.RenderFollowState(false, false);
            view.RenderControlGroups(_controlGroupCounts, -1);
            view.RenderContextualCursor(Vector2.zero, TacticalPointerMode.None);
            view.RenderContacts(null, null, null, null);
        }
    }

    /// <summary>
    /// Selects visible hull centers inside a screen rectangle.
    /// </summary>
    /// <param name="screenRectangle">The drag-selection screen rectangle.</param>
    /// <param name="additive">Whether to retain the previous selection.</param>
    public void SelectWithin(Rect screenRectangle, bool additive = false)
    {
        if (_presentations == null || !_camera)
            return;
        if (!additive)
            _selected.Clear();
        foreach (KeyValuePair<SpaceCombatUnit, Transform> pair in _presentations)
        {
            if (
                !pair.Value
                || !pair.Value.gameObject.activeInHierarchy
                || pair.Key.HasRetreated
                || !IsControlled(pair.Key)
            )
                continue;
            Vector3 screen = _camera.WorldToScreenPoint(pair.Value.position);
            if (
                screen.z >= _camera.nearClipPlane
                && screen.z <= _camera.farClipPlane
                && _camera.pixelRect.Contains(screen)
                && screenRectangle.Contains(screen)
                && !_selected.Contains(pair.Key)
            )
                _selected.Add(pair.Key);
        }
        _activeControlGroup = -1;
        RefreshView();
    }

    /// <summary>
    /// Handles click, modifier-click, and drag selection after camera navigation.
    /// </summary>
    private void LateUpdate()
    {
        if (_presentations == null)
            return;
        HandleKeyboardShortcuts();
        Mouse mouse = Mouse.current;
        bool additive = IsAdditiveSelectionModifierPressed(Keyboard.current);
        if (mouse != null)
        {
            Vector2 point = mouse.position.ReadValue();
            _hovered =
                TryGetCombatantAt(point, out SpaceCombatUnit hovered) && !IsControlled(hovered)
                    ? hovered
                    : null;
            TacticalPointerMode pointerMode = GetPointerMode(point);
            view.RenderContextualCursor(point, pointerMode);
            if (
                mouse.leftButton.wasPressedThisFrame
                && !mouse.rightButton.isPressed
                && !IsOverInterface(point)
                && _camera.pixelRect.Contains(point)
            )
            {
                if (_choosingDestination)
                    RequestMove(point, false);
                else
                {
                    _pressPosition = point;
                    _selecting = true;
                }
            }
            if (mouse.rightButton.isPressed)
                _selecting = false;
            if (_selecting && mouse.leftButton.wasReleasedThisFrame)
            {
                if (Vector2.Distance(_pressPosition, point) > 5f)
                    SelectWithin(
                        Rect.MinMaxRect(
                            Mathf.Min(_pressPosition.x, point.x),
                            Mathf.Min(_pressPosition.y, point.y),
                            Mathf.Max(_pressPosition.x, point.x),
                            Mathf.Max(_pressPosition.y, point.y)
                        ),
                        additive
                    );
                else
                    SelectAt(point, additive);
                _selecting = false;
            }
            view.RenderMarquee(
                _pressPosition,
                point,
                _selecting && Vector2.Distance(_pressPosition, point) > 5f
            );
        }
        else
            view.RenderContextualCursor(Vector2.zero, TacticalPointerMode.None);
        for (int i = _selected.Count - 1; i >= 0; i--)
        {
            if (
                !_presentations.TryGetValue(_selected[i], out Transform root)
                || !root
                || (
                    !root.gameObject.activeInHierarchy
                    && _selected[i].Flight.Phase == FighterFlightState.Deployed
                )
                || _selected[i].HasRetreated
            )
                _selected.RemoveAt(i);
        }
        if (!_selecting)
            view.RenderMarquee(Vector2.zero, Vector2.zero, false);
        RefreshView();
    }

    /// <summary>
    /// Selects every deployed, active combatant controlled by the viewing faction.
    /// </summary>
    public void SelectAllOwnedCombatants()
    {
        if (_presentations == null || string.IsNullOrWhiteSpace(_viewerFactionInstanceID))
            return;

        _selected.Clear();
        foreach (KeyValuePair<SpaceCombatUnit, Transform> pair in _presentations)
        {
            if (
                !pair.Value
                || !pair.Value.gameObject.activeInHierarchy
                || pair.Key.HasRetreated
                || !string.Equals(
                    pair.Key.OwnerInstanceID,
                    _viewerFactionInstanceID,
                    StringComparison.Ordinal
                )
            )
                continue;
            _selected.Add(pair.Key);
        }
        _activeControlGroup = -1;
        RefreshView();
    }

    /// <summary>
    /// Dispatches tactical selection shortcuts without allowing camera navigation to consume them.
    /// </summary>
    private void HandleKeyboardShortcuts()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        bool primaryModifier = IsPrimaryShortcutModifierPressed(keyboard);
        if (keyboard.aKey.wasPressedThisFrame && primaryModifier)
        {
            SelectAllOwnedCombatants();
            return;
        }
        int groupIndex = GetPressedControlGroup(keyboard);
        if (groupIndex < 0)
            return;
        if (primaryModifier)
            AssignControlGroup(groupIndex);
        else
            SelectControlGroup(groupIndex);
    }

    /// <summary>
    /// Assigns the currently selected friendly battle units to one numbered control group.
    /// </summary>
    /// <param name="groupIndex">The zero-based group index.</param>
    public void AssignControlGroup(int groupIndex)
    {
        ValidateControlGroupIndex(groupIndex);
        List<SpaceCombatUnit> group = _controlGroups[groupIndex];
        group.Clear();
        foreach (SpaceCombatUnit unit in _selected)
            if (
                !unit.HasRetreated
                && string.Equals(
                    unit.OwnerInstanceID,
                    _viewerFactionInstanceID,
                    StringComparison.Ordinal
                )
            )
                group.Add(unit);
        _activeControlGroup = group.Count > 0 ? groupIndex : -1;
        RefreshView();
    }

    /// <summary>
    /// Replaces the current selection with the live members of one numbered control group.
    /// </summary>
    /// <param name="groupIndex">The zero-based group index.</param>
    public void SelectControlGroup(int groupIndex)
    {
        ValidateControlGroupIndex(groupIndex);
        PruneControlGroups();
        List<SpaceCombatUnit> group = _controlGroups[groupIndex];
        if (group.Count == 0)
            return;
        _selected.Clear();
        _selected.AddRange(group);
        _activeControlGroup = groupIndex;
        RefreshView();
    }

    /// <summary>
    /// Recalls a populated numbered group, or assigns the current friendly selection to an empty slot.
    /// </summary>
    /// <param name="groupIndex">The zero-based group index.</param>
    private void HandleControlGroupClicked(int groupIndex)
    {
        ValidateControlGroupIndex(groupIndex);
        PruneControlGroups();
        if (_controlGroups[groupIndex].Count == 0)
            AssignControlGroup(groupIndex);
        else
            SelectControlGroup(groupIndex);
    }

    /// <summary>
    /// Finds the numbered control-group key pressed during this frame.
    /// </summary>
    /// <param name="keyboard">The active keyboard.</param>
    /// <returns>The zero-based group index, or negative when no group key was pressed.</returns>
    private static int GetPressedControlGroup(Keyboard keyboard)
    {
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame)
            return 0;
        if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame)
            return 1;
        if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame)
            return 2;
        if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame)
            return 3;
        if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame)
            return 4;
        if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame)
            return 5;
        if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame)
            return 6;
        if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame)
            return 7;
        if (keyboard.digit9Key.wasPressedThisFrame || keyboard.numpad9Key.wasPressedThisFrame)
            return 8;
        return -1;
    }

    /// <summary>
    /// Returns whether the platform's Control or Command modifier is currently pressed.
    /// </summary>
    /// <param name="keyboard">The active keyboard.</param>
    /// <returns>True when a primary shortcut modifier is pressed.</returns>
    private static bool IsPrimaryShortcutModifierPressed(Keyboard keyboard)
    {
        return keyboard != null
            && (
                keyboard.leftCtrlKey.isPressed
                || keyboard.rightCtrlKey.isPressed
                || keyboard.leftMetaKey.isPressed
                || keyboard.rightMetaKey.isPressed
            );
    }

    /// <summary>
    /// Returns whether selection should preserve and toggle the current selected units.
    /// </summary>
    /// <param name="keyboard">The active keyboard.</param>
    /// <returns>True when Shift, Control, or Command is pressed.</returns>
    private static bool IsAdditiveSelectionModifierPressed(Keyboard keyboard) =>
        keyboard != null
        && (keyboard.shiftKey.isPressed || IsPrimaryShortcutModifierPressed(keyboard));

    /// <summary>
    /// Updates only selected presentation objects.
    /// </summary>
    private void RefreshView()
    {
        PruneControlGroups();
        view.RenderSelection(_selected, _presentations, _camera);
        view.RenderControlGroups(_controlGroupCounts, _activeControlGroup);
        view.RenderFollowState(
            TryGetFollowTarget(out _),
            _cameraInput && _cameraInput.FollowTarget
        );
        _attackTargets.Clear();
        _targetSeen.Clear();
        foreach (SpaceCombatUnit unit in _selected)
            if (
                IsControlled(unit)
                && unit.Weapons.AttackTargetInstanceID != null
                && _combatantsByID.TryGetValue(
                    unit.Weapons.AttackTargetInstanceID,
                    out SpaceCombatUnit target
                )
                && !target.HasRetreated
                && _targetSeen.Add(target)
            )
                _attackTargets.Add(target);
        view.RenderContacts(_attackTargets, _hovered, _presentations, _camera);
    }

    /// <summary>
    /// Toggles camera tracking for the primary selected unit without issuing a gameplay order.
    /// </summary>
    private void ToggleFollow()
    {
        if (!isActiveAndEnabled || !_cameraInput || !_cameraInput.IsConfigured)
            return;
        if (_cameraInput.FollowTarget)
            _cameraInput.StopFollowing();
        else if (TryGetFollowTarget(out Transform target))
            _cameraInput.Follow(target);
        RefreshView();
    }

    /// <summary>
    /// Resolves the primary selected unit through the existing scene presentation index.
    /// </summary>
    /// <param name="target">The visible presentation root when available.</param>
    /// <returns>Whether a configured camera can follow the selected unit.</returns>
    private bool TryGetFollowTarget(out Transform target)
    {
        target = null;
        return _cameraInput
            && _cameraInput.IsConfigured
            && SelectedCombatant?.HasRetreated == false
            && _presentations != null
            && _presentations.TryGetValue(SelectedCombatant, out target)
            && target
            && target.gameObject.activeInHierarchy;
    }

    /// <summary>
    /// Ends a pending drag when a modal takes input without discarding selected ships.
    /// </summary>
    private void OnDisable()
    {
        _selecting = false;
        _choosingDestination = false;
        _choosingAttack = false;
        if (view)
            view.RenderMarquee(Vector2.zero, Vector2.zero, false);
    }

    /// <summary>
    /// Arms the next unobstructed world click as a move destination instead of a selection.
    /// </summary>
    private void BeginMove()
    {
        _choosingAttack = false;
        if (isActiveAndEnabled && _presentations != null && _selected.Count > 0)
            _choosingDestination = true;
    }

    /// <summary>
    /// Arms the next hostile world click as an explicit attack order.
    /// </summary>
    private void BeginAttack()
    {
        if (isActiveAndEnabled && _presentations != null && _selected.Count > 0)
        {
            _choosingAttack = true;
            _choosingDestination = true;
        }
    }

    /// <summary>
    /// Requests movement without treating camera drags or interface clicks as orders.
    /// </summary>
    /// <param name="screenPosition">The requested destination in screen pixels.</param>
    /// <param name="moveTogether">Whether the selected formation should match its slowest speed.</param>
    private void RequestPointerMove(Vector2 screenPosition, bool moveTogether) =>
        RequestMove(screenPosition, moveTogether);

    /// <summary>
    /// Requests movement without treating camera drags or interface clicks as orders.
    /// </summary>
    /// <param name="screenPosition">The requested destination in screen pixels.</param>
    /// <param name="moveTogether">Whether the selected formation should match its slowest speed.</param>
    private void RequestMove(Vector2 screenPosition, bool moveTogether)
    {
        if (
            !isActiveAndEnabled
            || _camera == null
            || _presentations == null
            || !_camera.pixelRect.Contains(screenPosition)
            || IsOverInterface(screenPosition)
        )
            return;
        _choosingDestination = false;
        _selecting = false;
        if (CanAttackTargetAt(screenPosition))
        {
            AttackRequested?.Invoke(screenPosition, moveTogether);
            _choosingAttack = false;
            return;
        }
        if (_choosingAttack)
        {
            _choosingAttack = false;
            return;
        }
        if (!moveTogether && TryGetDockTarget(screenPosition, out SpaceCombatUnit carrier))
        {
            DockAtRequested?.Invoke(carrier);
            return;
        }
        if (moveTogether)
            MoveTogetherRequested?.Invoke(screenPosition);
        else
            MoveRequested?.Invoke(screenPosition);
    }

    /// <summary>
    /// Resolves the contextual pointer without changing selection or issuing an order.
    /// </summary>
    /// <param name="screenPosition">The pointer position in screen pixels.</param>
    /// <returns>The available action beneath the pointer.</returns>
    private TacticalPointerMode GetPointerMode(Vector2 screenPosition)
    {
        if (!TryGetCombatantAt(screenPosition, out SpaceCombatUnit target))
            return TacticalPointerMode.None;
        if (_carrierOperations?.CanDockAt(_selected, target, _viewerFactionInstanceID) == true)
            return TacticalPointerMode.Dock;
        return TacticalPointerMode.None;
    }

    /// <summary>
    /// Reports whether the pointer currently identifies a valid hostile attack target.
    /// </summary>
    /// <param name="screenPosition">The pointer position in screen pixels.</param>
    /// <returns>Whether an attack order can consume the pointer action.</returns>
    private bool CanAttackTargetAt(Vector2 screenPosition) =>
        TryGetCombatantAt(screenPosition, out SpaceCombatUnit target) && CanAttackTarget(target);

    /// <summary>
    /// Reports whether a combatant belongs to the locally controlled faction.
    /// </summary>
    /// <param name="combatant">The combatant to test.</param>
    /// <returns>Whether it can join the local selection.</returns>
    private bool IsControlled(SpaceCombatUnit combatant) =>
        combatant?.HasRetreated == false
        && (
            string.IsNullOrWhiteSpace(_viewerFactionInstanceID)
            || string.Equals(
                combatant.OwnerInstanceID,
                _viewerFactionInstanceID,
                StringComparison.Ordinal
            )
        );

    /// <summary>
    /// Determines whether friendly selected units can identify one visible combatant as hostile.
    /// </summary>
    /// <param name="target">The visible combatant beneath the pointer.</param>
    /// <returns>Whether the combatant should receive attack-target feedback.</returns>
    private bool CanAttackTarget(SpaceCombatUnit target)
    {
        bool hasFriendlySelection = false;
        foreach (SpaceCombatUnit selected in _selected)
            if (
                !selected.HasRetreated
                && selected.Flight.Phase == FighterFlightState.Deployed
                && string.Equals(
                    selected.OwnerInstanceID,
                    _viewerFactionInstanceID,
                    StringComparison.Ordinal
                )
            )
            {
                hasFriendlySelection = true;
                break;
            }
        return hasFriendlySelection
            && target != null
            && !string.Equals(
                target.OwnerInstanceID,
                _viewerFactionInstanceID,
                StringComparison.Ordinal
            );
    }

    /// <summary>
    /// Resolves an eligible friendly carrier beneath the pointer for the complete fighter selection.
    /// </summary>
    /// <param name="screenPosition">The pointer position in screen pixels.</param>
    /// <param name="carrier">The eligible carrier when found.</param>
    /// <returns>Whether targeted docking is currently available.</returns>
    private bool TryGetDockTarget(Vector2 screenPosition, out SpaceCombatUnit carrier)
    {
        carrier = null;
        return _carrierOperations != null
            && TryGetCombatantAt(screenPosition, out carrier)
            && _carrierOperations.CanDockAt(_selected, carrier, _viewerFactionInstanceID);
    }

    /// <summary>
    /// Resolves the nearest visible combatant beneath one unobstructed world-space pointer.
    /// </summary>
    /// <param name="screenPosition">The pointer position in screen pixels.</param>
    /// <param name="combatant">The nearest visible combatant when found.</param>
    /// <returns>Whether a combatant was hit.</returns>
    private bool TryGetCombatantAt(Vector2 screenPosition, out SpaceCombatUnit combatant)
    {
        combatant = null;
        if (
            _camera == null
            || _presentations == null
            || IsOverInterface(screenPosition)
            || !_camera.pixelRect.Contains(screenPosition)
        )
            return false;

        Ray worldRay = _camera.ScreenPointToRay(screenPosition);
        float nearestScreenDistance = float.PositiveInfinity;
        float nearestWorldDistance = float.PositiveInfinity;
        foreach (KeyValuePair<SpaceCombatUnit, Transform> presentation in _presentations)
        {
            Transform root = presentation.Value;
            if (!root || !root.gameObject.activeInHierarchy || presentation.Key.HasRetreated)
                continue;
            ModelDimensions size = presentation.Key.ModelSize;
            Bounds bounds = new Bounds(
                Vector3.zero,
                new Vector3(size.Width, size.Height, size.Depth)
            );
            Ray localRay = new Ray(
                root.InverseTransformPoint(worldRay.origin),
                root.InverseTransformVector(worldRay.direction).normalized
            );
            if (!bounds.IntersectRay(localRay, out float localDistance))
                continue;
            Vector3 hit = root.TransformPoint(localRay.GetPoint(localDistance));
            float depth = _camera.WorldToViewportPoint(hit).z;
            float worldDistance = Vector3.Distance(worldRay.origin, hit);
            Vector2 projectedCenter = _camera.WorldToScreenPoint(root.position);
            float screenDistance = (projectedCenter - screenPosition).sqrMagnitude;
            if (
                depth < _camera.nearClipPlane
                || depth > _camera.farClipPlane
                || screenDistance > nearestScreenDistance
                || (
                    Mathf.Approximately(screenDistance, nearestScreenDistance)
                    && worldDistance >= nearestWorldDistance
                )
            )
                continue;
            nearestScreenDistance = screenDistance;
            nearestWorldDistance = worldDistance;
            combatant = presentation.Key;
        }
        return combatant != null;
    }

    /// <summary>
    /// Returns the nearest visible hull beneath a screen point without changing selection.
    /// </summary>
    /// <param name="screenPosition">The pointer position in screen pixels.</param>
    /// <returns>The nearest visible combatant, or null.</returns>
    public SpaceCombatUnit PickAt(Vector2 screenPosition) =>
        TryGetCombatantAt(screenPosition, out SpaceCombatUnit combatant) ? combatant : null;

    /// <summary>
    /// Cancels destination placement and requests cancellation of the selected ships' move orders.
    /// </summary>
    private void RequestStop()
    {
        if (!isActiveAndEnabled)
            return;
        _choosingDestination = false;
        StopRequested?.Invoke();
    }

    /// <summary>
    /// Requests launch of selected carried squadrons or selected carriers' manifests.
    /// </summary>
    private void RequestLaunch()
    {
        if (isActiveAndEnabled)
            LaunchRequested?.Invoke();
    }

    /// <summary>
    /// Requests recovery of selected carriers' assigned fighters.
    /// </summary>
    private void RequestRecall()
    {
        if (isActiveAndEnabled)
            RecallRequested?.Invoke();
    }

    /// <summary>
    /// Requests return of selected fighter squadrons to their assigned carriers.
    /// </summary>
    private void RequestDock()
    {
        if (isActiveAndEnabled)
            DockRequested?.Invoke();
    }

    /// <summary>
    /// Inspects a roster group, or toggles its members when an additive modifier is held.
    /// </summary>
    /// <param name="combatants">The clicked card's combatants.</param>
    private void SelectCard(IReadOnlyList<SpaceCombatUnit> combatants)
    {
        if (_presentations == null || combatants.Count == 0)
            return;
        bool additive = IsAdditiveSelectionModifierPressed(Keyboard.current);
        if (!additive)
            _selected.Clear();
        foreach (SpaceCombatUnit combatant in combatants)
            if (
                IsControlled(combatant)
                && _presentations.ContainsKey(combatant)
                && (!additive || !_selected.Remove(combatant))
            )
                _selected.Add(combatant);
        _activeControlGroup = -1;
        RefreshView();
    }

    /// <summary>
    /// Selects the friendly ship represented by an officer portrait marker.
    /// </summary>
    /// <param name="combatant">The ship represented by the clicked portrait.</param>
    private void SelectOfficerMarker(SpaceCombatUnit combatant)
    {
        if (
            _presentations == null
            || !IsControlled(combatant)
            || !_presentations.ContainsKey(combatant)
        )
            return;
        bool additive = IsAdditiveSelectionModifierPressed(Keyboard.current);
        if (!additive)
            _selected.Clear();
        if (!additive || !_selected.Remove(combatant))
            _selected.Add(combatant);
        _activeControlGroup = -1;
        RefreshView();
    }

    /// <summary>
    /// Centers the tactical camera on the first visible member of a double-clicked roster card.
    /// </summary>
    /// <param name="combatants">The card's represented battle units.</param>
    private void CenterOnCard(IReadOnlyList<SpaceCombatUnit> combatants)
    {
        if (!_cameraInput || !_cameraInput.IsConfigured || _presentations == null)
            return;
        foreach (SpaceCombatUnit combatant in combatants)
            if (
                !combatant.HasRetreated
                && _presentations.TryGetValue(combatant, out Transform target)
                && target
                && target.gameObject.activeInHierarchy
            )
            {
                _cameraInput.CenterOn(target);
                RefreshView();
                return;
            }
    }

    /// <summary>
    /// Removes stale members from numbered groups and refreshes their displayed counts.
    /// </summary>
    private void PruneControlGroups()
    {
        for (int groupIndex = 0; groupIndex < _controlGroups.Length; groupIndex++)
        {
            List<SpaceCombatUnit> group = _controlGroups[groupIndex];
            for (int memberIndex = group.Count - 1; memberIndex >= 0; memberIndex--)
                if (
                    group[memberIndex].HasRetreated
                    || _presentations?.ContainsKey(group[memberIndex]) != true
                )
                    group.RemoveAt(memberIndex);
            _controlGroupCounts[groupIndex] = group.Count;
            if (groupIndex == _activeControlGroup && group.Count == 0)
                _activeControlGroup = -1;
        }
    }

    /// <summary>
    /// Validates one zero-based numbered-group index.
    /// </summary>
    /// <param name="groupIndex">The requested group index.</param>
    private static void ValidateControlGroupIndex(int groupIndex)
    {
        if (groupIndex < 0 || groupIndex >= ControlGroupCount)
            throw new ArgumentOutOfRangeException(nameof(groupIndex));
    }

    /// <summary>
    /// Creates the fixed numbered-group storage owned by one tactical selection controller.
    /// </summary>
    /// <returns>Nine independently mutable control groups.</returns>
    private static List<SpaceCombatUnit>[] CreateControlGroups()
    {
        List<SpaceCombatUnit>[] groups = new List<SpaceCombatUnit>[ControlGroupCount];
        for (int index = 0; index < groups.Length; index++)
            groups[index] = new List<SpaceCombatUnit>();
        return groups;
    }

    /// <summary>
    /// Prevents world selection through any raycastable interface element.
    /// </summary>
    /// <param name="screenPosition">The pointer position to test.</param>
    /// <returns>True when the active interface handles this point.</returns>
    private bool IsOverInterface(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;
        if (_pointer == null || _pointerEventSystem != eventSystem)
        {
            _pointer = new PointerEventData(eventSystem);
            _pointerEventSystem = eventSystem;
        }
        _pointer.Reset();
        _pointer.position = screenPosition;
        _interfaceHits.Clear();
        eventSystem.RaycastAll(_pointer, _interfaceHits);
        return _interfaceHits.Count > 0;
    }

    /// <summary>
    /// Discards scene-owned selection when the controller is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (view)
        {
            view.AttackButton.onClick.RemoveListener(BeginAttack);
            view.CardClicked -= SelectCard;
            view.CardDoubleClicked -= CenterOnCard;
            view.MoveButton.onClick.RemoveListener(BeginMove);
            view.StopButton.onClick.RemoveListener(RequestStop);
            view.LaunchButton.onClick.RemoveListener(RequestLaunch);
            view.RecallButton.onClick.RemoveListener(RequestRecall);
            view.DockButton.onClick.RemoveListener(RequestDock);
            view.FollowButton.onClick.RemoveListener(ToggleFollow);
            view.ControlGroupClicked -= HandleControlGroupClicked;
            view.OfficerMarkerClicked -= SelectOfficerMarker;
        }
        Clear();
    }
}
