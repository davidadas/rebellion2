using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Animates the loading-screen handoff control in response to pointer and navigation focus.
/// </summary>
[DisallowMultipleComponent]
public sealed class LoadingContinueButtonFeedback
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
{
    private static readonly Color _restingGlow = new Color(0.08f, 0.64f, 1f, 0.1f);
    private static readonly Color _focusedGlow = new Color(0.18f, 0.82f, 1f, 0.42f);
    private static readonly Color _restingFrame = new Color(0.12f, 0.55f, 0.75f, 0.75f);
    private static readonly Color _focusedFrame = new Color(0.45f, 0.95f, 1f, 1f);
    private static readonly Color _restingPanel = new Color(0.012f, 0.055f, 0.1f, 0.98f);
    private static readonly Color _focusedPanel = new Color(0.025f, 0.14f, 0.22f, 0.98f);
    private static readonly Color _restingLabel = new Color(0.78f, 0.91f, 0.96f, 1f);
    private static readonly Color _focusedLabel = Color.white;

    [SerializeField]
    private Image outerGlow;

    [SerializeField]
    private Image frame;

    [SerializeField]
    private Image panel;

    [SerializeField]
    private TextMeshProUGUI label;

    private bool pointerFocused;
    private bool navigationFocused;
    private bool pressed;
    private float focusAmount;

    /// <summary>
    /// Verifies the generated visual references and applies the resting appearance.
    /// </summary>
    private void Awake()
    {
        EnsureReferences();
        ApplyAppearance();
    }

    /// <summary>
    /// Verifies the generated visual references.
    /// </summary>
    private void EnsureReferences()
    {
        if (outerGlow == null || frame == null || panel == null || label == null)
        {
            throw new MissingReferenceException(
                $"{name} requires glow, frame, panel, and label references."
            );
        }
    }

    /// <summary>
    /// Marks the control as pointer-focused.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerFocused = true;
        focusAmount = 1f;
        ApplyAppearance();
    }

    /// <summary>
    /// Clears pointer focus and any active press.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        pointerFocused = false;
        pressed = false;
        focusAmount = navigationFocused ? 1f : 0f;
        ApplyAppearance();
    }

    /// <summary>
    /// Applies the pressed appearance while the pointer is held.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
        ApplyAppearance();
    }

    /// <summary>
    /// Releases the pressed appearance.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
        ApplyAppearance();
    }

    /// <summary>
    /// Applies the focused appearance for keyboard or controller navigation.
    /// </summary>
    /// <param name="eventData">The selection event.</param>
    public void OnSelect(BaseEventData eventData)
    {
        navigationFocused = true;
        focusAmount = 1f;
        ApplyAppearance();
    }

    /// <summary>
    /// Clears keyboard or controller focus.
    /// </summary>
    /// <param name="eventData">The selection event.</param>
    public void OnDeselect(BaseEventData eventData)
    {
        navigationFocused = false;
        focusAmount = pointerFocused ? 1f : 0f;
        ApplyAppearance();
    }

    /// <summary>
    /// Restores the authored appearance when the control is disabled or reused.
    /// </summary>
    private void OnDisable()
    {
        pointerFocused = false;
        navigationFocused = false;
        pressed = false;
        focusAmount = 0f;
        if (outerGlow != null && frame != null && panel != null && label != null)
            ApplyAppearance();
    }

    /// <summary>
    /// Applies the current glow, frame, panel, and label presentation.
    /// </summary>
    private void ApplyAppearance()
    {
        EnsureReferences();
        outerGlow.color = Color.Lerp(_restingGlow, _focusedGlow, focusAmount);
        frame.color = Color.Lerp(_restingFrame, _focusedFrame, focusAmount);
        panel.color = pressed
            ? Color.Lerp(_restingPanel, _focusedPanel, 0.6f)
            : Color.Lerp(_restingPanel, _focusedPanel, focusAmount);
        label.color = Color.Lerp(_restingLabel, _focusedLabel, focusAmount);
    }
}
