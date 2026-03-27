using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Core UI button input handler that emits events for visual and audio components to listen to.
/// This component handles all pointer interactions and maintains the button state.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButton
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
{
    [Header("Events")]
    public UnityEvent OnHoverEnter;
    public UnityEvent OnHoverExit;
    public UnityEvent OnPressDown;
    public UnityEvent OnPressUp;
    public UnityEvent OnClick;
    public UnityEvent OnDoubleClick;

    [Header("Double Click Settings")]
    [SerializeField]
    private float doubleClickWindow = 0.3f;

    /// <summary>
    /// Is the pointer currently hovering over this button?
    /// </summary>
    public bool IsHovered { get; private set; }

    /// <summary>
    /// Is the button currently pressed down?
    /// </summary>
    public bool IsPressed { get; private set; }

    /// <summary>
    /// Is this button currently interactable?
    /// </summary>
    public bool IsInteractable => button != null && button.interactable;

    private Button button;
    private float lastClickTime = -1f;

    private void Awake()
    {
        button = GetComponent<Button>();

        // Initialize events if null
        if (OnHoverEnter == null)
            OnHoverEnter = new UnityEvent();
        if (OnHoverExit == null)
            OnHoverExit = new UnityEvent();
        if (OnPressDown == null)
            OnPressDown = new UnityEvent();
        if (OnPressUp == null)
            OnPressUp = new UnityEvent();
        if (OnClick == null)
            OnClick = new UnityEvent();
        if (OnDoubleClick == null)
            OnDoubleClick = new UnityEvent();
    }

    /// <summary>
    /// Gets the Unity Button component attached to this GameObject.
    /// </summary>
    public Button GetButton()
    {
        if (button == null)
            button = GetComponent<Button>();
        return button;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovered = true;
        OnHoverEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovered = false;
        IsPressed = false;
        OnHoverExit?.Invoke();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsPressed = true;
        OnPressDown?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        bool wasPressed = IsPressed;
        IsPressed = false;
        OnPressUp?.Invoke();

        // Only fire click events if this was a valid click (pressed and released while hovering)
        if (wasPressed && IsHovered && IsInteractable)
        {
            float currentTime = Time.unscaledTime;
            bool isDoubleClick = (currentTime - lastClickTime) <= doubleClickWindow;

            OnClick?.Invoke();

            if (isDoubleClick)
            {
                OnDoubleClick?.Invoke();
                lastClickTime = -1f; // Reset to prevent triple-click from triggering double-click
            }
            else
            {
                lastClickTime = currentTime;
            }
        }
    }
}
