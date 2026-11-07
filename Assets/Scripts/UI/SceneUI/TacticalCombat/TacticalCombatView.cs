using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Identifies a player action exposed by the tactical battle HUD.
/// </summary>
public enum TacticalCombatControl
{
    Pause,
    AttackFighters,
    AttackCapitalShips,
    HoldFire,
    Stop,
    Recover,
    Withdraw,
    CloseAttack,
    RangedAttack,
    LineFormation,
    SurroundFormation,
    LeftHook,
    RightHook,
    Hammer,
    Anvil,
    AttackBattleStation,
    Superlaser,
    Group,
    Continue,
    NavigationRing1,
    NavigationRing2,
    NavigationRing3,
    NavigationRing4,
}

/// <summary>
/// Exposes authored tactical controls and viewport input without owning battle rules.
/// </summary>
[DisallowMultipleComponent]
public sealed class TacticalCombatView
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler,
        IScrollHandler
{
    [Serializable]
    public sealed class ControlBinding
    {
        public TacticalCombatControl Control;
        public Button Button;
    }

    [SerializeField]
    private RawImage _viewport;

    [SerializeField]
    private RawImage _background;

    [SerializeField]
    private TextMeshProUGUI _status;

    [SerializeField]
    private TextMeshProUGUI _selection;

    [SerializeField]
    private TacticalCombatOverlayGraphic _overlay;

    [SerializeField]
    private TacticalCombatRenderer _battleRenderer;

    [SerializeField]
    private ControlBinding[] _controls;
    public RectTransform Viewport => _viewport.rectTransform;
    public Vector2Int PixelSize =>
        new Vector2Int(
            Mathf.RoundToInt(Viewport.rect.width * _viewport.canvas.scaleFactor),
            Mathf.RoundToInt(Viewport.rect.height * _viewport.canvas.scaleFactor)
        );
    public TacticalCombatOverlayGraphic Overlay => _overlay;
    public TacticalCombatRenderer Renderer => _battleRenderer;
    public event Action<TacticalCombatControl> ControlRequested;
    public event Action<PointerEventData> PointerPressed;
    public event Action<PointerEventData> PointerReleased;
    public event Action<PointerEventData> PointerDragged;
    public event Action<float> ZoomRequested;

    /// <summary>
    /// Verifies authored references and subscribes each semantic control exactly once.
    /// </summary>
    private void Awake()
    {
        if (
            _viewport == null
            || _status == null
            || _selection == null
            || _overlay == null
            || _battleRenderer == null
            || _controls == null
        )
            throw new MissingReferenceException(
                $"{name} is missing tactical presentation references."
            );
        foreach (ControlBinding binding in _controls)
        {
            if (binding.Button == null)
                throw new MissingReferenceException($"{name} has an unbound tactical control.");
            TacticalCombatControl control = binding.Control;
            binding.Button.onClick.AddListener(() => ControlRequested?.Invoke(control));
        }
    }

    /// <summary>
    /// Presents loading, battle progress, or completion without changing encounter state.
    /// </summary>
    /// <param name="value">The status text.</param>
    public void SetStatus(string value) => _status.text = value;

    /// <summary>
    /// Presents the selected units' current condition and orders.
    /// </summary>
    /// <param name="value">The selection text.</param>
    public void SetSelection(string value) => _selection.text = value;

    /// <summary>
    /// Enables one semantic control according to the controller's live encounter assessment.
    /// </summary>
    /// <param name="control">The authored control.</param>
    /// <param name="enabled">Whether the action is available.</param>
    public void SetControlEnabled(TacticalCombatControl control, bool enabled)
    {
        foreach (ControlBinding binding in _controls)
            if (binding.Control == control)
                binding.Button.interactable = enabled;
    }

    /// <summary>
    /// Assigns the camera's owned render target to the authored viewport.
    /// </summary>
    /// <param name="texture">The current render target.</param>
    public void SetViewportTexture(RenderTexture texture)
    {
        _viewport.texture = texture;
        _viewport.color = texture != null ? Color.white : Color.clear;
    }

    /// <summary>
    /// Displays the addressed starfield behind the transparent battle camera.
    /// </summary>
    /// <param name="texture">The selected pack's starfield.</param>
    public void SetBackgroundTexture(Texture2D texture)
    {
        _background.texture = texture;
        _background.color = texture != null ? Color.white : Color.black;
    }

    /// <summary>
    /// Routes a press originating inside the battle viewport.
    /// </summary>
    /// <param name="eventData">The viewport pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (
            RectTransformUtility.RectangleContainsScreenPoint(
                Viewport,
                eventData.position,
                eventData.pressEventCamera
            )
        )
            PointerPressed?.Invoke(eventData);
    }

    /// <summary>
    /// Routes the release of an ongoing viewport gesture.
    /// </summary>
    /// <param name="eventData">The viewport pointer event.</param>
    public void OnPointerUp(PointerEventData eventData) => PointerReleased?.Invoke(eventData);

    /// <summary>
    /// Routes viewport selection and camera gestures.
    /// </summary>
    /// <param name="eventData">The viewport pointer event.</param>
    public void OnDrag(PointerEventData eventData) => PointerDragged?.Invoke(eventData);

    /// <summary>
    /// Routes zoom input while the pointer is over the battle viewport.
    /// </summary>
    /// <param name="eventData">The viewport scroll event.</param>
    public void OnScroll(PointerEventData eventData) =>
        ZoomRequested?.Invoke(eventData.scrollDelta.y);
}
