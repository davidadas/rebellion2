using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Previews a card reorder in a scroll area and returns the destination when it is dropped.
/// </summary>
public sealed class CardReorderDrag
{
    private readonly ScrollAreaView _scroll;
    private readonly RectTransform _overlay;
    private readonly List<SortableRowView> _cards = new();
    private readonly List<RectInt> _slots = new();
    private readonly Sprite _slotSprite;
    private RectTransform _held;
    private int _origin;
    private int _destination;
    private int _siblingIndex;
    private Vector2 _pointer;
    private Camera _camera;
    private Vector2 _grabOffset;
    private Image _placeholder;
    private Image _line;
    private CanvasGroup _group;
    private Shadow _shadow;
    private bool _blockedRaycasts;
    private bool _shadowWasEnabled;

    public bool IsDragging => _held != null;

    /// <summary>Called when the preview moves to another slot.</summary>
    public Func<int, int, string> CheckMove { get; set; }

    /// <summary>Reports a restriction for the current preview slot.</summary>
    public Action<string> ShowRestriction { get; set; }

    public Color Accent { get; set; }
    public Color Error { get; set; }

    /// <summary>
    /// Creates the drag interaction for cards laid out from the top of the scroll content.
    /// </summary>
    /// <param name="scroll">The scroll area containing the cards.</param>
    /// <param name="overlay">The surface used to lift the card above the viewport mask.</param>
    /// <param name="slotSprite">The optional placeholder artwork.</param>
    public CardReorderDrag(ScrollAreaView scroll, RectTransform overlay, Sprite slotSprite = null)
    {
        _scroll = scroll;
        _overlay = overlay;
        _slotSprite = slotSprite;
    }

    /// <summary>
    /// Lifts the card while leaving a placeholder in the list.
    /// </summary>
    /// <param name="cards">The cards in display order.</param>
    /// <param name="origin">The index of the card that was picked up.</param>
    /// <param name="input">The pointer that started the drag.</param>
    public void Begin(IEnumerable<SortableRowView> cards, int origin, PointerEventData input)
    {
        if (IsDragging)
            return;

        _cards.AddRange(cards);
        foreach (SortableRowView card in _cards)
            _slots.Add(UILayout.GetSourceRect(card.Rect));
        _origin = _destination = origin;
        _held = _cards[origin].Rect;
        _cards[origin].SetLifted(true);
        _siblingIndex = _held.GetSiblingIndex();
        _camera = input.pressEventCamera;
        Vector2 corner = _overlay.InverseTransformPoint(
            _held.TransformPoint(new Vector2(_held.rect.xMin, _held.rect.yMax))
        );
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _overlay,
            input.pressPosition == Vector2.zero ? input.position : input.pressPosition,
            _camera,
            out Vector2 press
        );
        _grabOffset = corner - press;
        _placeholder ??= CreateMarker("Card pickup position");
        _placeholder.sprite = _slotSprite;
        _placeholder.type = _slotSprite == null ? Image.Type.Simple : Image.Type.Sliced;
        _placeholder.color = new Color(1f, 1f, 1f, _slotSprite == null ? 0.12f : 0.26f);
        RectInt slot = _slots[origin];
        UILayout.SetSourceRect(_placeholder.rectTransform, slot.x, slot.y, slot.width, slot.height);
        _placeholder.gameObject.SetActive(true);

        _held.SetParent(_overlay, worldPositionStays: true);
        _held.SetAsLastSibling();
        if (!_held.TryGetComponent(out _group))
            _group = _held.gameObject.AddComponent<CanvasGroup>();
        _blockedRaycasts = _group.blocksRaycasts;
        _group.blocksRaycasts = false;
        _shadow = _held.GetComponent<Shadow>();
        if (_shadow == null)
        {
            _shadow = _held.gameObject.AddComponent<Shadow>();
            _shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            _shadow.effectDistance = new Vector2(0f, -4f);
            _shadow.enabled = false;
        }
        _shadowWasEnabled = _shadow.enabled;
        _shadow.enabled = true;
        input.eligibleForClick = false;
        PointAt(_held, input.position);
    }

    /// <summary>
    /// Updates the preview for the current pointer.
    /// </summary>
    /// <param name="card">The card reporting pointer movement.</param>
    /// <param name="pointer">The pointer in screen space.</param>
    public void PointAt(RectTransform card, Vector2 pointer)
    {
        if (card != _held)
            return;
        _pointer = pointer;
        Update(0f);
    }

    /// <summary>
    /// Animates neighboring cards and scrolls the list when the pointer nears an edge.
    /// </summary>
    /// <param name="elapsed">The elapsed unscaled time in seconds.</param>
    public void Update(float elapsed)
    {
        if (!IsDragging)
            return;

        FollowPointer();
        RectTransform viewport = _scroll.ViewportRoot;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                _pointer,
                _camera,
                out Vector2 point
            ) || !viewport.rect.Contains(point)
        )
        {
            PlacePreview(-1, elapsed);
            return;
        }

        float viewportTop = viewport.rect.yMax - point.y;
        RectTransform content = _scroll.ContentRoot;
        float maxScroll = content.rect.height - viewport.rect.height;
        const float edge = 16f;
        if (elapsed > 0f && maxScroll > 0f)
        {
            float direction =
                Mathf.Clamp01(1f - viewportTop / edge)
                - Mathf.Clamp01(1f - (viewport.rect.height - viewportTop) / edge);
            if (direction != 0f)
                _scroll.VerticalNormalizedPosition += direction * elapsed * 160f / maxScroll;
        }

        float pointerTop = viewportTop + content.anchoredPosition.y;
        PlacePreview(ResolveDestination(pointerTop), elapsed);
    }

    /// <summary>
    /// Releases the held card and restores the layout before the caller applies the move.
    /// </summary>
    /// <param name="card">The card reporting the release.</param>
    /// <param name="pointer">The release position in screen space.</param>
    /// <returns>The new index, or negative one if the drop did not move the card.</returns>
    public int Drop(RectTransform card, Vector2 pointer)
    {
        if (card != _held)
            return -1;
        PointAt(card, pointer);
        int destination = _destination == _origin ? -1 : _destination;
        Cancel();
        return destination;
    }

    /// <summary>
    /// Restores the card and its neighbors to their original slots immediately.
    /// </summary>
    public void Cancel()
    {
        if (!IsDragging)
            return;

        AnimateCards(_origin, 1f);
        _held.SetParent(_scroll.ContentRoot, false);
        _held.SetSiblingIndex(_siblingIndex);
        RectInt slot = _slots[_origin];
        UILayout.SetSourceRect(_held, slot.x, slot.y, slot.width, slot.height);
        _group.blocksRaycasts = _blockedRaycasts;
        _shadow.enabled = _shadowWasEnabled;
        _group = null;
        _shadow = null;
        _cards[_origin].SetLifted(false);
        _held = null;
        _camera = null;
        _cards.Clear();
        _slots.Clear();
        _placeholder.gameObject.SetActive(false);
        _line?.gameObject.SetActive(false);
        ShowRestriction?.Invoke(null);
    }

    /// <summary>
    /// Places the lifted card at its original grab offset from the current pointer.
    /// </summary>
    private void FollowPointer()
    {
        if (
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _overlay,
                _pointer,
                _camera,
                out Vector2 point
            )
        )
        {
            Vector2 corner = point + _grabOffset;
            _held.anchoredPosition = corner - new Vector2(_overlay.rect.xMin, _overlay.rect.yMax);
        }
    }

    /// <summary>
    /// Advances the slot when the pointer crosses a neighboring card's midpoint.
    /// </summary>
    /// <param name="pointerTop">The pointer's vertical offset within the list.</param>
    /// <returns>The index of the preview slot.</returns>
    private int ResolveDestination(float pointerTop)
    {
        int current = _destination < 0 ? _origin : _destination;
        int destination = current;
        while (destination < _cards.Count - 1)
        {
            int neighbor = destination >= _origin ? destination + 1 : destination;
            if (pointerTop < GetPreviewMidpoint(neighbor, destination))
                break;
            destination++;
        }

        if (destination != current)
            return destination;

        while (destination > 0)
        {
            int neighbor = destination <= _origin ? destination - 1 : destination;
            if (pointerTop >= GetPreviewMidpoint(neighbor, destination))
                break;
            destination--;
        }
        return destination;
    }

    /// <summary>
    /// Measures a neighboring card's midpoint in the target layout.
    /// </summary>
    /// <param name="index">The neighboring card's original index.</param>
    /// <param name="destination">The held card's preview slot.</param>
    /// <returns>The midpoint within the scroll content.</returns>
    private float GetPreviewMidpoint(int index, int destination) =>
        GetPreviewTop(index, destination) + _slots[index].height * 0.5f;

    /// <summary>
    /// Calculates the positions of cards that move to make room for the held card.
    /// </summary>
    /// <param name="index">The card's original index.</param>
    /// <param name="destination">The held card's preview slot.</param>
    /// <returns>The top of the card in the preview layout.</returns>
    private int GetPreviewTop(int index, int destination)
    {
        int top = _slots[index].y;
        int span = _slots[_origin].height + SortableRowView.Gap;
        if (index == _origin)
            return destination > _origin
                ? _slots[destination].yMax + SortableRowView.Gap - span
                : _slots[destination].y;
        if (index > _origin && index <= destination)
            return top - span;
        if (index < _origin && index >= destination)
            return top + span;
        return top;
    }

    /// <summary>
    /// Moves the preview gap and marks valid or blocked insertion slots.
    /// </summary>
    /// <param name="destination">The requested slot, or negative one outside the viewport.</param>
    /// <param name="elapsed">The time available to animate the neighboring cards.</param>
    private void PlacePreview(int destination, float elapsed)
    {
        int preview = destination < 0 ? _origin : destination;
        if (elapsed > 0f)
            AnimateCards(preview, 1f - Mathf.Exp(-26f * elapsed));

        if (destination == _destination)
            return;
        _destination = destination;
        RectInt slot = _slots[_origin];
        int top = GetPreviewTop(_origin, preview);
        UILayout.SetSourcePosition(_placeholder.rectTransform, slot.x, top);
        if (preview == _origin)
        {
            _line?.gameObject.SetActive(false);
            ShowRestriction?.Invoke(null);
            return;
        }

        string restriction = CheckMove?.Invoke(_origin, destination);
        ShowRestriction?.Invoke(restriction);
        _line ??= CreateMarker("Card drop position");
        _line.color = string.IsNullOrEmpty(restriction) ? Accent : Error;
        UILayout.SetSourceRect(_line.rectTransform, slot.x, Mathf.Max(0, top - 2), slot.width, 2);
        _line.transform.SetAsLastSibling();
        _line.gameObject.SetActive(true);
    }

    /// <summary>
    /// Slides neighboring cards toward their preview slots.
    /// </summary>
    /// <param name="destination">The slot reserved for the held card.</param>
    /// <param name="progress">The fraction of remaining distance to move.</param>
    private void AnimateCards(int destination, float progress)
    {
        for (int index = 0; index < _cards.Count; index++)
        {
            if (index == _origin)
                continue;
            RectTransform card = _cards[index].Rect;
            Vector2 position = card.anchoredPosition;
            float target = -GetPreviewTop(index, destination);
            if (position.y == target)
                continue;
            position.y =
                Mathf.Abs(position.y - target) < 0.25f
                    ? target
                    : Mathf.Lerp(position.y, target, progress);
            card.anchoredPosition = position;
        }
    }

    /// <summary>
    /// Adds a visual marker to the scroll content.
    /// </summary>
    /// <param name="name">The marker's name in the hierarchy.</param>
    /// <returns>The marker image.</returns>
    private Image CreateMarker(string name)
    {
        var marker = new GameObject(name, typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(_scroll.ContentRoot, false);
        Image image = marker.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
}
