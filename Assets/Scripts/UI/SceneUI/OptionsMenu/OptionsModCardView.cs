using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Presents one mod as a card and accepts pickup from its body or its outside grip.
/// </summary>
internal sealed class OptionsModCardView
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
{
    private Image _surface;
    private CanvasGroup _group;
    private Shadow _shadow;
    private Sprite _idle;
    private Sprite _active;
    private bool _hovered;
    private bool _pressed;
    private bool _lifted;

    internal RectTransform Rect => (RectTransform)transform;
    internal OptionsToggleRowView Row { get; private set; }
    internal OptionsModDragHandle Grip { get; set; }

    /// <summary>
    /// Applies the shared Options row artwork to the card and binds its toggle row.
    /// </summary>
    /// <param name="row">The card's editable row.</param>
    /// <param name="idle">The normal card surface.</param>
    /// <param name="active">The active and lifted card surface.</param>
    internal void Initialize(OptionsToggleRowView row, Sprite idle, Sprite active)
    {
        Row = row;
        _idle = idle;
        _active = active;
        _surface = GetComponent<Image>();
        _surface.type = Image.Type.Sliced;
        _group = GetComponent<CanvasGroup>();
        _shadow = gameObject.AddComponent<Shadow>();
        _shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        _shadow.effectDistance = new Vector2(0f, -4f);
        _shadow.enabled = false;
        RenderSurface();
    }

    /// <summary>
    /// Shows the lifted card above the list without letting it intercept drop targets.
    /// </summary>
    /// <param name="lifted">Whether this card is held by the pointer.</param>
    internal void SetLifted(bool lifted)
    {
        _lifted = lifted;
        _pressed = false;
        _group.blocksRaycasts = !lifted;
        _shadow.enabled = lifted;
        RenderSurface();
    }

    /// <summary>
    /// Highlights a card available for pickup when the pointer enters it.
    /// </summary>
    /// <param name="eventData">The entering pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        _hovered = true;
        RenderSurface();
    }

    /// <summary>
    /// Clears the highlight when the pointer leaves the card.
    /// </summary>
    /// <param name="eventData">The leaving pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        _hovered = false;
        RenderSurface();
    }

    /// <summary>
    /// Shows immediate pickup feedback for a press on the card or grip.
    /// </summary>
    /// <param name="eventData">The pointer press.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = CanPickUp(eventData);
        RenderSurface();
    }

    /// <summary>
    /// Clears press feedback on release.
    /// </summary>
    /// <param name="eventData">The pointer release.</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        RenderSurface();
    }

    /// <summary>
    /// Starts moving a card when a drag begins on its body.
    /// </summary>
    /// <param name="eventData">The pointer that began dragging.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (CanPickUp(eventData))
            Grip.OnBeginDrag(eventData);
    }

    /// <summary>
    /// Routes movement through the same handler used by the grip.
    /// </summary>
    /// <param name="eventData">The moving pointer.</param>
    public void OnDrag(PointerEventData eventData) => Grip?.OnDrag(eventData);

    /// <summary>
    /// Routes release through the same handler used by the grip.
    /// </summary>
    /// <param name="eventData">The released pointer.</param>
    public void OnEndDrag(PointerEventData eventData) => Grip?.OnEndDrag(eventData);

    /// <summary>
    /// Clears pointer feedback before this pooled card is reused.
    /// </summary>
    private void OnDisable()
    {
        _hovered = false;
        _pressed = false;
        if (_surface != null)
            RenderSurface();
    }

    /// <summary>
    /// Checks whether the pointer gesture belongs to this card's reorder surface.
    /// </summary>
    /// <param name="eventData">The originating pointer event.</param>
    /// <returns>Whether the card can be lifted by this gesture.</returns>
    private bool CanPickUp(PointerEventData eventData) =>
        eventData.button == PointerEventData.InputButton.Left
        && Grip?.gameObject.activeInHierarchy == true
        && !Row.IsModToggleTarget(eventData.pointerPressRaycast.gameObject);

    /// <summary>
    /// Updates the card surface to communicate hover, press, and pickup.
    /// </summary>
    private void RenderSurface()
    {
        bool held = _pressed || _lifted;
        bool highlighted = held || (_hovered && Grip?.gameObject.activeInHierarchy == true);
        _surface.sprite = held ? _active : _idle;
        _surface.color = new Color(1f, 1f, 1f, highlighted ? 1f : 0.78f);
    }
}
