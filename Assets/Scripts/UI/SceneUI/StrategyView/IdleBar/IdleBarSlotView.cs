using System;
using TMPro;
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
        IPointerDownHandler,
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

    [SerializeField]
    private TextMeshProUGUI overflowTextField;

    private string instanceId;
    private bool hovered;
    private bool initialized;
    private int currentSlotSize;

    /// <summary>Raised when the player selects this entry.</summary>
    internal event Action<string> Selected;

    /// <summary>Raised when the player requests that this entry stop being tracked.</summary>
    internal event Action<string> UntrackRequested;

    /// <summary>Raised when the pointer begins hovering this entity.</summary>
    internal event Action<string> Hovered;

    /// <summary>Raised when the pointer stops hovering this entity.</summary>
    internal event Action<string> HoverCleared;

    /// <summary>Raised when a primary press may begin dragging this entity.</summary>
    internal event Action<string, DragPreview, PointerEventData> DragCandidateRequested;

    /// <summary>Raised when a primary click releases a pending drag candidate.</summary>
    internal event Action<PointerEventData> DragCandidateReleased;

    /// <summary>
    /// Renders one entity at its top-left source-space position.
    /// </summary>
    /// <param name="entry">The entry to render.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="slotSize">The source-space square slot size.</param>
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

        portraitMask.gameObject.SetActive(true);
        overflowTextField.gameObject.SetActive(false);
        portraitImage.texture = entry.Texture;
        portraitImage.uvRect = GetCenteredSquareUv(entry.Texture);
        button.interactable = !string.IsNullOrEmpty(instanceId);
    }

    /// <summary>
    /// Renders the number of entries hidden by the compact shelf.
    /// </summary>
    /// <param name="hiddenCount">The number of hidden entries.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="slotSize">The source-space square slot size.</param>
    internal void RenderOverflow(int hiddenCount, int x, int y, int slotSize)
    {
        if (hiddenCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(hiddenCount));

        Initialize();
        currentSlotSize = slotSize;
        instanceId = null;
        gameObject.name = $"+{hiddenCount}";
        SetSourceRect(transform as RectTransform, x, y, slotSize, slotSize);
        portraitMask.gameObject.SetActive(false);
        overflowTextField.text = $"+{hiddenCount}";
        overflowTextField.gameObject.SetActive(true);
        button.interactable = false;
    }

    /// <summary>
    /// Enlarges the portrait under the pointer.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
        if (!string.IsNullOrEmpty(instanceId))
            Hovered?.Invoke(instanceId);
    }

    /// <summary>
    /// Restores the portrait after the pointer leaves.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
        if (!string.IsNullOrEmpty(instanceId))
            HoverCleared?.Invoke(instanceId);
    }

    /// <summary>
    /// Captures a primary press as a possible direct entity drag.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (
            eventData?.button != PointerEventData.InputButton.Left
            || string.IsNullOrEmpty(instanceId)
        )
            return;

        int previewSize = hovered ? _hoveredCircleSize : _circleSize;
        DragCandidateRequested?.Invoke(
            instanceId,
            new DragPreview(
                new[]
                {
                    new DragPreviewImage(
                        portraitImage.texture,
                        new RectInt(-previewSize / 2, -previewSize / 2, previewSize, previewSize),
                        portraitImage.uvRect
                    ),
                },
                0,
                0
            ),
            eventData
        );
    }

    /// <summary>
    /// Requests immediate removal from the idle bar on a secondary click.
    /// </summary>
    /// <param name="eventData">The source pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData?.button == PointerEventData.InputButton.Left)
        {
            DragCandidateReleased?.Invoke(eventData);
            return;
        }

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
            || overflowTextField == null
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
    /// Clears hover state when shelf layout hides a reused slot beneath the pointer.
    /// </summary>
    private void OnDisable()
    {
        if (!hovered)
            return;

        SetHovered(false);
        if (!string.IsNullOrEmpty(instanceId))
            HoverCleared?.Invoke(instanceId);
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
    /// <param name="hovered">Whether the pointer is over the slot.</param>
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
    /// <param name="texture">The source portrait texture.</param>
    /// <returns>The centered square UV rectangle.</returns>
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
    /// <param name="rect">The rectangle to position.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="width">The source-space width.</param>
    /// <param name="height">The source-space height.</param>
    private static void SetSourceRect(RectTransform rect, int x, int y, int width, int height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
