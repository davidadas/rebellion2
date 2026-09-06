using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Renders and selects one compact entity portrait in the idle bar.
/// </summary>
public sealed class IdleBarSlotView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler
{
    private const int _circleSize = 24;
    private const int _hoveredCircleSize = 26;
    private const float _hoveredPortraitScale = 1.08f;

    [SerializeField]
    private Button button;

    [SerializeField]
    private Image frameImage;

    [SerializeField]
    private RectTransform portraitMask;

    [SerializeField]
    private RawImage portraitBackground;

    [SerializeField]
    private RawImage portraitImage;

    private string instanceId;
    private bool hovered;
    private bool initialized;
    private int currentSlotSize;

    internal event Action<string> Selected;

    internal event Action<string> UntrackRequested;

    /// <summary>
    /// Renders one entity at its top-left source-space position.
    /// </summary>
    internal void Render(IdleBarEntry entry, int x, int y, int slotSize)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        Initialize();
        currentSlotSize = slotSize;
        instanceId = entry.Entity?.InstanceID;
        gameObject.name = entry.Name;
        SetSourceRect(transform as RectTransform, x, y, slotSize, slotSize);
        SetHovered(hovered);

        portraitImage.texture = entry.Texture;
        portraitImage.uvRect = GetCenteredSquareUv(entry.Texture);
        button.interactable = !string.IsNullOrEmpty(instanceId);
    }

    /// <summary>
    /// Enlarges the portrait under the pointer.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
    }

    /// <summary>
    /// Restores the portrait after the pointer leaves.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
    }

    /// <summary>
    /// Requests immediate removal from the idle bar on a secondary click.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (
            eventData?.button == PointerEventData.InputButton.Right
            && !string.IsNullOrEmpty(instanceId)
        )
        {
            eventData.Use();
            UntrackRequested?.Invoke(instanceId);
        }
    }

    /// <summary>
    /// Subscribes the authored button once the cloned slot becomes active.
    /// </summary>
    private void Awake()
    {
        Initialize();
    }

    /// <summary>
    /// Validates authored references and subscribes the button exactly once.
    /// </summary>
    private void Initialize()
    {
        if (initialized)
            return;

        if (
            button == null
            || frameImage == null
            || portraitMask == null
            || portraitBackground == null
            || portraitImage == null
        )
        {
            throw new MissingReferenceException(
                "IdleBarSlotView has incomplete authored references."
            );
        }

        button.onClick.AddListener(HandleSelected);
        initialized = true;
    }

    /// <summary>
    /// Releases the authored button listener.
    /// </summary>
    private void OnDestroy()
    {
        if (initialized && button != null)
            button.onClick.RemoveListener(HandleSelected);
    }

    /// <summary>
    /// Raises the stable identity represented by this slot.
    /// </summary>
    private void HandleSelected()
    {
        if (!string.IsNullOrEmpty(instanceId))
            Selected?.Invoke(instanceId);
    }

    /// <summary>
    /// Applies the compact normal or emphasized hover geometry.
    /// </summary>
    private void SetHovered(bool hovered)
    {
        this.hovered = hovered;
        int circleSize = hovered ? _hoveredCircleSize : _circleSize;
        int circlePosition = (currentSlotSize - circleSize) / 2;
        SetSourceRect(frameImage.rectTransform, 0, 0, currentSlotSize, currentSlotSize);
        SetSourceRect(portraitMask, circlePosition, circlePosition, circleSize, circleSize);
        portraitImage.rectTransform.localScale = hovered
            ? Vector3.one * _hoveredPortraitScale
            : Vector3.one;
    }

    /// <summary>
    /// Crops rectangular artwork into a centered square before circular masking.
    /// </summary>
    private static Rect GetCenteredSquareUv(Texture texture)
    {
        if (texture == null || texture.width <= 0 || texture.height <= 0)
            return new Rect(0f, 0f, 1f, 1f);

        if (texture.width > texture.height)
        {
            float width = (float)texture.height / texture.width;
            return new Rect((1f - width) / 2f, 0f, width, 1f);
        }

        float height = (float)texture.width / texture.height;
        return new Rect(0f, (1f - height) / 2f, 1f, height);
    }

    /// <summary>
    /// Applies a source-space rectangle using the strategy screen's top-left coordinates.
    /// </summary>
    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
