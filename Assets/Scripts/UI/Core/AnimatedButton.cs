using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles animation-based visuals for UI buttons by listening to UIButton events.
/// Controls animator speed and visibility based on button state.
/// </summary>
[RequireComponent(typeof(UIButton))]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Image))]
public sealed class AnimatedButton : MonoBehaviour
{
    [SerializeField]
    private GameObject HoverVisual;

    [SerializeField]
    private GameObject PressedVisual;

    private UIButton uiButton;
    private Animator animator;
    private Button button;
    private Image image;

    private float defaultSpeed;
    private bool isExternallyFrozen;

    private void Awake()
    {
        uiButton = GetComponent<UIButton>();
        animator = GetComponent<Animator>();
        button = GetComponent<Button>();
        image = GetComponent<Image>();

        if (uiButton == null)
        {
            Debug.LogError(
                "AnimatedButton requires a UIButton component on the same GameObject.",
                this
            );
            return;
        }

        // Subscribe to UIButton events
        uiButton.OnHoverEnter.AddListener(HandleStateChange);
        uiButton.OnHoverExit.AddListener(HandleStateChange);
        uiButton.OnPressDown.AddListener(HandleStateChange);
        uiButton.OnPressUp.AddListener(HandleStateChange);

        if (HoverVisual != null)
            HoverVisual.SetActive(false);

        if (PressedVisual != null)
            PressedVisual.SetActive(false);
    }

    private void OnDestroy()
    {
        if (uiButton != null)
        {
            uiButton.OnHoverEnter.RemoveListener(HandleStateChange);
            uiButton.OnHoverExit.RemoveListener(HandleStateChange);
            uiButton.OnPressDown.RemoveListener(HandleStateChange);
            uiButton.OnPressUp.RemoveListener(HandleStateChange);
        }
    }

    private void HandleStateChange()
    {
        UpdateAnimator();
        UpdateVisuals();
    }

    private void Start()
    {
        float animatorSpeed = animator.speed;

        if (!Mathf.Approximately(animatorSpeed, 0f))
        {
            defaultSpeed = animatorSpeed;
        }
        else
        {
            defaultSpeed = 1f;
            animator.speed = 1f;
        }

        UpdateAnimator();
        UpdateVisuals();
    }

    public Button GetButton()
    {
        return button;
    }

    public void SetFrozen(bool value)
    {
        isExternallyFrozen = value;
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (animator == null || uiButton == null)
            return;

        if (uiButton.IsPressed)
        {
            animator.enabled = false;

            if (image != null)
                image.enabled = false;

            return;
        }

        if (!animator.enabled)
            animator.enabled = true;

        if (image != null && !image.enabled)
            image.enabled = true;

        bool shouldFreeze = uiButton.IsHovered || isExternallyFrozen;

        animator.speed = shouldFreeze ? 0f : defaultSpeed;
    }

    private void UpdateVisuals()
    {
        if (uiButton == null)
            return;

        if (HoverVisual != null)
            HoverVisual.SetActive(uiButton.IsHovered && !uiButton.IsPressed);

        if (PressedVisual != null)
            PressedVisual.SetActive(uiButton.IsPressed);
    }
}
