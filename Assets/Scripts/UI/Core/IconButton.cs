using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles sprite-based visuals for UI buttons by listening to UIButton events.
/// Swaps sprites based on button state (normal, hover, pressed, disabled, selected).
/// </summary>
[RequireComponent(typeof(UIButton))]
[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public sealed class IconButton : MonoBehaviour
{
    [SerializeField]
    private string displayText;

    [Header("Sprites")]
    [SerializeField]
    private Sprite normal;

    [SerializeField]
    private Sprite hover;

    [SerializeField]
    private Sprite pressed;

    [SerializeField]
    private Sprite disabled;

    [SerializeField]
    private Sprite selected;

    private UIButton uiButton;
    private Button button;
    private Image image;

    private Sprite defaultSprite;
    private Sprite desiredSprite;

    private bool isSelected;

    private void Awake()
    {
        uiButton = GetComponent<UIButton>();

        if (uiButton == null)
        {
            Debug.LogError(
                "IconButton requires a UIButton component on the same GameObject.",
                this
            );
            return;
        }

        // Subscribe to UIButton events
        uiButton.OnHoverEnter.AddListener(HandleStateChange);
        uiButton.OnHoverExit.AddListener(HandleStateChange);
        uiButton.OnPressDown.AddListener(HandleStateChange);
        uiButton.OnPressUp.AddListener(HandleStateChange);

        EnsureComponents();
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
        ApplyCurrentSprite();
    }

    private void EnsureComponents()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (image == null)
        {
            image = GetComponent<Image>();

            if (image != null)
            {
                image.preserveAspect = true;

                // Cache the original sprite as fallback
                if (defaultSprite == null)
                    defaultSprite = image.sprite;
            }
        }
    }

    public Button GetButton()
    {
        EnsureComponents();
        return button;
    }

    public string GetDisplayText()
    {
        return displayText;
    }

    public void SetSprites(
        Sprite normalSprite,
        Sprite hoverSprite,
        Sprite pressedSprite,
        Sprite disabledSprite,
        Sprite selectedSprite
    )
    {
        EnsureComponents();

        normal = normalSprite;
        hover = hoverSprite;
        pressed = pressedSprite;
        disabled = disabledSprite;
        selected = selectedSprite;

        ApplyCurrentSprite();
    }

    public void SetSelected(bool value)
    {
        EnsureComponents();

        isSelected = value;
        ApplyCurrentSprite();
    }

    public void SetDisabled(bool value)
    {
        EnsureComponents();

        button.interactable = !value;
        ApplyCurrentSprite();
    }

    public void SetEnabled(bool value)
    {
        EnsureComponents();

        button.interactable = value;
        ApplyCurrentSprite();
    }

    private void ApplyCurrentSprite()
    {
        EnsureComponents();

        // Always have a safe fallback
        Sprite fallback = normal != null ? normal : defaultSprite;

        if (!button.interactable)
        {
            desiredSprite = disabled != null ? disabled : fallback;
        }
        else if (uiButton != null && uiButton.IsPressed)
        {
            desiredSprite = pressed != null ? pressed : fallback;
        }
        else if (isSelected)
        {
            desiredSprite = selected != null ? selected : fallback;
        }
        else if (uiButton != null && uiButton.IsHovered)
        {
            desiredSprite = hover != null ? hover : fallback;
        }
        else
        {
            desiredSprite = fallback;
        }

        // Never assign null
        if (image != null && desiredSprite != null)
        {
            image.sprite = desiredSprite;
        }
    }
}
