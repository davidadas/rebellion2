using System;
using System.Collections.Generic;
using Rebellion.Game.Combat;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Owns hull picking, multiselection, and transient selection in the tactical scene.
/// </summary>
public sealed class TacticalSelectionController : MonoBehaviour
{
    [SerializeField]
    private TacticalSelectionView view;

    private readonly List<RaycastResult> _interfaceHits = new List<RaycastResult>();
    private Camera _camera;
    private IReadOnlyDictionary<CombatUnit, Transform> _presentations;
    private PointerEventData _pointer;
    private EventSystem _pointerEventSystem;
    private readonly List<CombatUnit> _selected = new List<CombatUnit>();
    private IReadOnlyList<CombatUnit> _readOnlySelection;
    private Vector2 _pressPosition;
    private bool _selecting;
    private bool _choosingDestination;
    private TacticalViewCameraController _cameraInput;

    public event Action<Vector2> MoveRequested;
    public event Action StopRequested;

    /// <summary>
    /// Gets the ordered transient selection without allowing callers to modify it.
    /// </summary>
    public IReadOnlyList<CombatUnit> SelectedCombatants =>
        _readOnlySelection ??= _selected.AsReadOnly();

    /// <summary>
    /// Gets the inspected unit, regardless of its owning faction.
    /// </summary>
    public CombatUnit SelectedCombatant => _selected.Count > 0 ? _selected[0] : null;

    /// <summary>
    /// Connects roster selection to the same selection owner as world input.
    /// </summary>
    private void Awake()
    {
        view.CardClicked += SelectCard;
        view.MoveButton.onClick.AddListener(BeginMove);
        view.StopButton.onClick.AddListener(RequestStop);
    }

    /// <summary>
    /// Binds picking to the loaded presentation roots of one battle.
    /// </summary>
    /// <param name="camera">The camera displaying the battle.</param>
    /// <param name="presentations">The loaded combatants and their presentation roots.</param>
    public void Initialize(Camera camera, IReadOnlyDictionary<CombatUnit, Transform> presentations)
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
        _cameraInput = camera.GetComponent<TacticalViewCameraController>();
        if (_cameraInput)
            _cameraInput.RightClicked += RequestMove;
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

        Ray worldRay = _camera.ScreenPointToRay(screenPosition);
        float nearestDistance = float.PositiveInfinity;
        CombatUnit nearest = null;
        foreach (KeyValuePair<CombatUnit, Transform> presentation in _presentations)
        {
            Transform root = presentation.Value;
            if (!root || !root.gameObject.activeInHierarchy || presentation.Key.HasRetreated)
                continue;
            ModelDimensions size = presentation.Key.GetUnit().ModelSize;
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
            float distance = Vector3.Distance(worldRay.origin, hit);
            if (
                depth < _camera.nearClipPlane
                || depth > _camera.farClipPlane
                || distance >= nearestDistance
            )
                continue;

            nearestDistance = distance;
            nearest = presentation.Key;
        }

        if (!additive)
            _selected.Clear();
        if (nearest != null && (!additive || !_selected.Remove(nearest)))
            _selected.Add(nearest);
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
        if (_cameraInput)
            _cameraInput.RightClicked -= RequestMove;
        _cameraInput = null;
        _presentations = null;
        _camera = null;
        _interfaceHits.Clear();
        _pointer = null;
        _pointerEventSystem = null;
        if (view)
        {
            view.RenderSelection(_selected, null, null);
            view.BindBattle(null, null, null, null, _selected);
            view.RenderMarquee(Vector2.zero, Vector2.zero, false);
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
        foreach (KeyValuePair<CombatUnit, Transform> pair in _presentations)
        {
            if (!pair.Value || !pair.Value.gameObject.activeInHierarchy || pair.Key.HasRetreated)
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
        RefreshView();
    }

    /// <summary>
    /// Handles click, shift-click, and drag selection after camera navigation.
    /// </summary>
    private void LateUpdate()
    {
        if (_presentations == null)
            return;
        Mouse mouse = Mouse.current;
        bool additive = Keyboard.current?.shiftKey.isPressed == true;
        if (mouse != null)
        {
            Vector2 point = mouse.position.ReadValue();
            if (
                mouse.leftButton.wasPressedThisFrame
                && !mouse.rightButton.isPressed
                && !IsOverInterface(point)
                && _camera.pixelRect.Contains(point)
            )
            {
                if (_choosingDestination)
                    RequestMove(point);
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
        for (int i = _selected.Count - 1; i >= 0; i--)
        {
            if (
                !_presentations.TryGetValue(_selected[i], out Transform root)
                || !root
                || !root.gameObject.activeInHierarchy
                || _selected[i].HasRetreated
            )
                _selected.RemoveAt(i);
        }
        if (!_selecting)
            view.RenderMarquee(Vector2.zero, Vector2.zero, false);
        RefreshView();
    }

    /// <summary>
    /// Updates only selected presentation objects.
    /// </summary>
    private void RefreshView() => view.RenderSelection(_selected, _presentations, _camera);

    /// <summary>
    /// Ends a pending drag when a modal takes input without discarding selected ships.
    /// </summary>
    private void OnDisable()
    {
        _selecting = false;
        _choosingDestination = false;
        if (view)
            view.RenderMarquee(Vector2.zero, Vector2.zero, false);
    }

    /// <summary>
    /// Arms the next unobstructed world click as a move destination instead of a selection.
    /// </summary>
    private void BeginMove()
    {
        if (isActiveAndEnabled && _presentations != null && _selected.Count > 0)
            _choosingDestination = true;
    }

    /// <summary>
    /// Requests movement without treating camera drags or interface clicks as orders.
    /// </summary>
    /// <param name="screenPosition">The requested destination in screen pixels.</param>
    private void RequestMove(Vector2 screenPosition)
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
        MoveRequested?.Invoke(screenPosition);
    }

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
    /// Inspects a roster group, or toggles its members when shift is held.
    /// </summary>
    /// <param name="combatants">The clicked card's combatants.</param>
    private void SelectCard(IReadOnlyList<CombatUnit> combatants)
    {
        if (_presentations == null || combatants.Count == 0)
            return;
        bool additive = Keyboard.current?.shiftKey.isPressed == true;
        if (!additive)
            _selected.Clear();
        foreach (CombatUnit combatant in combatants)
            if (
                _presentations.ContainsKey(combatant) && (!additive || !_selected.Remove(combatant))
            )
                _selected.Add(combatant);
        RefreshView();
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
            view.CardClicked -= SelectCard;
            view.MoveButton.onClick.RemoveListener(BeginMove);
            view.StopButton.onClick.RemoveListener(RequestStop);
        }
        Clear();
    }
}
