using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class OptionsMenuView
{
    private const int _moveMenuWidth = 104;
    private const int _moveMenuHeight = 54;
    private const int _modCardGap = 6;

    private readonly List<OptionsModDragHandle> _modGrips = new();
    private readonly List<int> _modDropBoundaries = new();
    private OptionsModDragHandle _draggedMod;
    private Vector2 _modDragPointer;
    private Camera _modDragCamera;
    private RectInt _modCardOrigin;
    private Vector2 _modCardGrabOffset;
    private Image _modCardPlaceholder;
    private Image _modInsertionLine;
    private int _modDropOrder;
    private GameObject _modMoveMenu;
    private RectTransform _modMoveMenuPanel;
    private Button _modMoveEarlier;
    private Button _modMoveLater;
    private OptionsModDragHandle _modMoveMenuSource;
    private string _modGripToFocus;

    /// <summary>
    /// Gets or sets the controller's dependency check for a proposed move.
    /// </summary>
    internal Func<int, int, string> CheckModMove { get; set; }

    /// <summary>
    /// Places a selectable grip in the gutter and records the row's drop boundaries.
    /// </summary>
    /// <param name="card">The displayed draggable card.</param>
    /// <param name="mod">The active mod represented by the row.</param>
    /// <param name="count">The number of active mods.</param>
    /// <param name="top">The row's top coordinate.</param>
    /// <param name="bottom">The following row's top coordinate, including this row's details.</param>
    /// <param name="gutter">The space outside the row reserved for its grip.</param>
    private void RenderModGrip(
        OptionsModCardView card,
        OptionsModRow mod,
        int count,
        int top,
        int bottom,
        int gutter
    )
    {
        int index = mod.LoadOrder - 1;
        if (index == _modGrips.Count)
        {
            var root = new GameObject(
                "Reorder mod",
                typeof(RectTransform),
                typeof(OptionsModDragHandle),
                typeof(Button)
            );
            root.transform.SetParent(card.transform, false);
            var grip = root.GetComponent<OptionsModDragHandle>();
            grip.Initialize(this, card, root.GetComponent<Button>());
            card.Grip = grip;
            grip.color = _metaColor;
            root.AddComponent<HelpTooltipView>()
                .Configure(
                    "Drag to reorder. Click for move commands.",
                    captionFont: _modsStatusTextField.font
                );
            _modGrips.Add(grip);
        }

        OptionsModDragHandle handle = _modGrips[index];
        handle.Bind(mod);
        RectInt row = UILayout.GetSourceRect((RectTransform)card.Row.transform);
        UILayout.SetSourceRect(handle.rectTransform, -gutter, row.y, gutter - 2, row.height);
        handle.gameObject.SetActive(true);
        _modDropBoundaries.Add(top);
        if (mod.LoadOrder == count)
            _modDropBoundaries.Add(bottom);
    }

    /// <summary>
    /// Starts a drag on an active mod and retains the pointer used for auto-scrolling.
    /// </summary>
    /// <param name="handle">The grip that began dragging.</param>
    /// <param name="eventData">The current drag pointer.</param>
    internal void BeginModDrag(OptionsModDragHandle handle, PointerEventData eventData)
    {
        if (_draggedMod != null)
            return;
        HideModMoveMenu();
        _draggedMod = handle;
        _draggedMod.color = _accentColor;
        _modDropOrder = handle.LoadOrder;
        _modDragCamera = eventData.pressEventCamera;
        LiftModCard(
            handle.Card,
            eventData.pressPosition == Vector2.zero ? eventData.position : eventData.pressPosition
        );
        UpdateModDrag(handle, eventData);
    }

    /// <summary>
    /// Updates the insertion marker for a drag from the active handle.
    /// </summary>
    /// <param name="handle">The grip reporting the pointer move.</param>
    /// <param name="eventData">The current pointer event.</param>
    internal void UpdateModDrag(OptionsModDragHandle handle, PointerEventData eventData)
    {
        if (_draggedMod != handle)
            return;
        _modDragPointer = eventData.position;
        UpdateModDrop(0f);
    }

    /// <summary>
    /// Issues one move request on drop so the controller can validate and stage it.
    /// </summary>
    /// <param name="handle">The grip that owns the drag.</param>
    /// <param name="eventData">The final pointer event.</param>
    internal void EndModDrag(OptionsModDragHandle handle, PointerEventData eventData)
    {
        if (_draggedMod != handle)
            return;
        UpdateModDrag(handle, eventData);
        int destination = _modDropOrder;
        CancelModDrag();
        if (destination != 0 && destination != handle.LoadOrder)
            ModMoveRequested?.Invoke(handle.ModID, destination);
    }

    /// <summary>
    /// Animates neighboring cards and scrolls while the pointer is near a viewport edge.
    /// </summary>
    private void Update()
    {
        if (_draggedMod != null)
            UpdateModDrop(Time.unscaledDeltaTime);
    }

    /// <summary>
    /// Clears ordering interaction when the Options window becomes inactive.
    /// </summary>
    private void OnDisable() => CancelModOrdering();

    /// <summary>
    /// Resolves the current pointer to a row boundary and previews valid or blocked drops.
    /// </summary>
    /// <param name="elapsed">The unscaled time available for edge scrolling.</param>
    private void UpdateModDrop(float elapsed)
    {
        FollowModPointer();
        RectTransform viewport = _modsScrollArea.ViewportRoot;
        if (
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport,
                _modDragPointer,
                _modDragCamera,
                out Vector2 point
            ) || !viewport.rect.Contains(point)
        )
        {
            RenderModPlacement(0, elapsed);
            return;
        }

        RectTransform content = _modsScrollArea.ContentRoot;
        float viewportTop = viewport.rect.yMax - point.y;
        float maxScroll = content.rect.height - viewport.rect.height;
        const float edge = 16f;
        if (elapsed > 0f && maxScroll > 0f)
        {
            float direction =
                Mathf.Clamp01(1f - viewportTop / edge)
                - Mathf.Clamp01(1f - (viewport.rect.height - viewportTop) / edge);
            if (direction != 0f)
                _modsScrollArea.VerticalNormalizedPosition +=
                    direction * elapsed * 160f / maxScroll;
        }

        float pointerTop = viewportTop + content.anchoredPosition.y;
        RenderModPlacement(ResolveModDestination(pointerTop), elapsed);
    }

    /// <summary>
    /// Advances the insertion slot when the pointer crosses a neighboring card's midpoint.
    /// </summary>
    /// <param name="pointerTop">The pointer's vertical position inside the scrolling content.</param>
    /// <returns>The new one-based insertion position.</returns>
    private int ResolveModDestination(float pointerTop)
    {
        int origin = _draggedMod.LoadOrder;
        int current = _modDropOrder == 0 ? origin : _modDropOrder;
        int destination = current;
        int count = _modDropBoundaries.Count - 1;
        while (destination < count)
        {
            int neighbor = destination >= origin ? destination + 1 : destination;
            if (pointerTop < GetModPreviewMidpoint(neighbor, destination))
                break;
            destination++;
        }

        if (destination != current)
            return destination;

        while (destination > 1)
        {
            int neighbor = destination <= origin ? destination - 1 : destination;
            if (pointerTop >= GetModPreviewMidpoint(neighbor, destination))
                break;
            destination--;
        }
        return destination;
    }

    /// <summary>
    /// Measures the midpoint of a card in the preview layout so animations do not change its hit test.
    /// </summary>
    /// <param name="order">The neighboring card's original load position.</param>
    /// <param name="destination">The current insertion position of the held card.</param>
    /// <returns>The midpoint in scroll content coordinates.</returns>
    private float GetModPreviewMidpoint(int order, int destination) =>
        GetModPreviewTop(order, destination) + _modGrips[order - 1].Card.Rect.rect.height * 0.5f;

    /// <summary>
    /// Calculates where a card sits while the held card opens a gap in the list.
    /// </summary>
    /// <param name="order">The card's original one-based position.</param>
    /// <param name="destination">The held card's preview position.</param>
    /// <returns>The card's top coordinate in the preview layout.</returns>
    private int GetModPreviewTop(int order, int destination)
    {
        int origin = _draggedMod.LoadOrder;
        int top = _modDropBoundaries[order - 1];
        int span = _modCardOrigin.height + _modCardGap;
        if (order == origin)
            return destination > origin
                ? _modDropBoundaries[destination] - span
                : _modDropBoundaries[destination - 1];
        if (order > origin && order <= destination)
            return top - span;
        if (order < origin && order >= destination)
            return top + span;
        return top;
    }

    /// <summary>
    /// Slides the other cards aside and places the drop marker beside the open slot.
    /// </summary>
    /// <param name="destination">The pointer's destination, or zero outside the list.</param>
    /// <param name="elapsed">Elapsed time for the neighboring cards' movement.</param>
    private void RenderModPlacement(int destination, float elapsed)
    {
        int origin = _draggedMod.LoadOrder;
        int preview = destination == 0 ? origin : destination;
        if (elapsed > 0f)
            AnimateModCards(preview, 1f - Mathf.Exp(-26f * elapsed));

        if (destination == _modDropOrder)
            return;

        _modDropOrder = destination;
        int top = GetModPreviewTop(origin, preview);
        UILayout.SetSourcePosition(_modCardPlaceholder.rectTransform, _modCardOrigin.x, top);
        if (preview == origin)
        {
            _modInsertionLine?.gameObject.SetActive(false);
            return;
        }
        if (_modInsertionLine == null)
        {
            var line = new GameObject("Mod drop position", typeof(RectTransform), typeof(Image));
            line.transform.SetParent(_modsScrollArea.ContentRoot, false);
            _modInsertionLine = line.GetComponent<Image>();
            _modInsertionLine.raycastTarget = false;
        }
        int left = _modCardOrigin.x - 10;
        _modInsertionLine.color = string.IsNullOrEmpty(
            CheckModMove?.Invoke(_draggedMod.LoadOrder, destination)
        )
            ? _accentColor
            : _errorColor;
        UILayout.SetSourceRect(
            _modInsertionLine.rectTransform,
            left,
            top - 2,
            _modCardOrigin.width + 10,
            2
        );
        _modInsertionLine.transform.SetAsLastSibling();
        _modInsertionLine.gameObject.SetActive(true);
    }

    /// <summary>
    /// Slides the cards toward their preview positions while leaving the held card under the pointer.
    /// </summary>
    /// <param name="destination">The current insertion position.</param>
    /// <param name="progress">The fraction of remaining distance to move this frame.</param>
    private void AnimateModCards(int destination, float progress)
    {
        for (int order = 1; order < _modDropBoundaries.Count; order++)
        {
            if (order == _draggedMod.LoadOrder)
                continue;
            RectTransform card = _modGrips[order - 1].Card.Rect;
            Vector2 position = card.anchoredPosition;
            float target = -GetModPreviewTop(order, destination);
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
    /// Moves the real card into the overlay and leaves a placeholder where it was picked up.
    /// </summary>
    /// <param name="card">The selected card.</param>
    /// <param name="pressPosition">The screen coordinate where the pointer grabbed the card.</param>
    private void LiftModCard(OptionsModCardView card, Vector2 pressPosition)
    {
        RectTransform rect = card.Rect;
        RectTransform surface = (RectTransform)_modsPage.transform.parent;
        _modCardOrigin = UILayout.GetSourceRect(rect);
        Vector2 corner = surface.InverseTransformPoint(
            rect.TransformPoint(new Vector2(rect.rect.xMin, rect.rect.yMax))
        );
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            surface,
            pressPosition,
            _modDragCamera,
            out Vector2 press
        );
        _modCardGrabOffset = corner - press;

        if (_modCardPlaceholder == null)
        {
            var slot = new GameObject("Mod pickup position", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(_modsScrollArea.ContentRoot, false);
            _modCardPlaceholder = slot.GetComponent<Image>();
            _modCardPlaceholder.sprite = _rowIdleSprite;
            _modCardPlaceholder.type = Image.Type.Sliced;
            _modCardPlaceholder.color = new Color(1f, 1f, 1f, 0.26f);
            _modCardPlaceholder.raycastTarget = false;
        }
        UILayout.SetSourceRect(
            _modCardPlaceholder.rectTransform,
            _modCardOrigin.x,
            _modCardOrigin.y,
            _modCardOrigin.width,
            _modCardOrigin.height
        );
        _modCardPlaceholder.gameObject.SetActive(true);
        rect.SetParent(surface, false);
        rect.SetAsLastSibling();
        card.SetLifted(true);
    }

    /// <summary>
    /// Positions the lifted card at its original grab offset from the current pointer.
    /// </summary>
    private void FollowModPointer()
    {
        RectTransform card = _draggedMod.Card.Rect;
        RectTransform surface = (RectTransform)card.parent;
        if (
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                surface,
                _modDragPointer,
                _modDragCamera,
                out Vector2 point
            )
        )
        {
            Vector2 corner = point + _modCardGrabOffset;
            card.anchoredPosition = corner - new Vector2(surface.rect.xMin, surface.rect.yMax);
        }
    }

    /// <summary>
    /// Clears the dragged grip and its marker without changing the order.
    /// </summary>
    private void CancelModDrag()
    {
        if (_draggedMod != null)
        {
            _draggedMod.color = _metaColor;
            AnimateModCards(_draggedMod.LoadOrder, 1f);
            OptionsModCardView card = _draggedMod.Card;
            card.Rect.SetParent(_modsScrollArea.ContentRoot, false);
            UILayout.SetSourceRect(
                card.Rect,
                _modCardOrigin.x,
                _modCardOrigin.y,
                _modCardOrigin.width,
                _modCardOrigin.height
            );
            card.SetLifted(false);
        }
        _draggedMod = null;
        _modDragCamera = null;
        _modDropOrder = 0;
        _modInsertionLine?.gameObject.SetActive(false);
        _modCardPlaceholder?.gameObject.SetActive(false);
    }

    /// <summary>
    /// Clears interaction when the list is rerendered or its page is left.
    /// </summary>
    private void CancelModOrdering()
    {
        CancelModDrag();
        HideModMoveMenu();
    }

    /// <summary>
    /// Handles Escape for the active mod drag or its movement menu.
    /// </summary>
    /// <returns>Whether a mod ordering interaction was dismissed.</returns>
    internal bool TryCancelModOrdering()
    {
        if (_draggedMod != null)
        {
            CancelModDrag();
            return true;
        }
        if (_modMoveMenuSource == null)
            return false;

        _modMoveMenuSource.GetComponent<Button>().Select();
        HideModMoveMenu();
        return true;
    }

    /// <summary>
    /// Opens the native button menu beside the clicked or keyboard-selected grip.
    /// </summary>
    /// <param name="source">The grip requesting movement.</param>
    internal void ShowModMoveMenu(OptionsModDragHandle source)
    {
        if (_draggedMod != null)
            return;
        if (_modMoveMenu == null)
            CreateModMoveMenu();

        RectTransform surface = (RectTransform)_modsPage.transform.parent;
        RectTransform anchor = source.rectTransform;
        Vector2 bottom = surface.InverseTransformPoint(anchor.TransformPoint(anchor.rect.min));
        int x = Mathf.Clamp(
            Mathf.RoundToInt(bottom.x - surface.rect.xMin),
            0,
            Mathf.Max(0, Mathf.FloorToInt(surface.rect.width) - _moveMenuWidth)
        );
        int y = Mathf.RoundToInt(surface.rect.yMax - bottom.y);
        if (y + _moveMenuHeight > surface.rect.height)
            y = Mathf.Max(0, y - Mathf.RoundToInt(anchor.rect.height) - _moveMenuHeight);
        UILayout.SetSourceRect(_modMoveMenuPanel, x, y, _moveMenuWidth, _moveMenuHeight);
        _modMoveMenuSource = source;
        _modMoveEarlier.interactable = source.LoadOrder > 1;
        _modMoveLater.interactable = source.LoadOrder < _modDropBoundaries.Count - 1;
        _modMoveMenu.transform.SetAsLastSibling();
        _modMoveMenu.SetActive(true);
        (_modMoveEarlier.interactable ? _modMoveEarlier : _modMoveLater).Select();
    }

    /// <summary>
    /// Builds one dismissible movement menu using the Options button artwork.
    /// </summary>
    private void CreateModMoveMenu()
    {
        _modMoveMenu = new GameObject(
            "Mod move menu",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );
        RectTransform overlay = (RectTransform)_modMoveMenu.transform;
        overlay.SetParent(_modsPage.transform.parent, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;
        Image blocker = _modMoveMenu.GetComponent<Image>();
        blocker.color = Color.clear;
        blocker.canvasRenderer.cullTransparentMesh = false;
        Button dismiss = _modMoveMenu.GetComponent<Button>();
        dismiss.navigation = new Navigation { mode = Navigation.Mode.None };
        dismiss.onClick.AddListener(DismissModMoveMenu);

        var panel = new GameObject("Move commands", typeof(RectTransform), typeof(Image));
        _modMoveMenuPanel = (RectTransform)panel.transform;
        panel.transform.SetParent(overlay, false);
        panel.GetComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f);
        _modMoveEarlier = CreateModMoveCommand("Move earlier", 4, -1);
        _modMoveLater = CreateModMoveCommand("Move later", 28, 1);
        _modMoveEarlier.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnDown = _modMoveLater,
        };
        _modMoveLater.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = _modMoveEarlier,
        };
        _modMoveMenu.SetActive(false);
    }

    /// <summary>
    /// Creates a focusable movement command using the same button as other Options actions.
    /// </summary>
    /// <param name="label">The command label.</param>
    /// <param name="y">The command's top coordinate inside the popup.</param>
    /// <param name="direction">The one-row direction for this command.</param>
    /// <returns>The command button.</returns>
    private Button CreateModMoveCommand(string label, int y, int direction)
    {
        Button button = Instantiate(_applyButton, _modMoveMenuPanel, false);
        button.name = label;
        UILayout.SetSourceRect((RectTransform)button.transform, 4, y, _moveMenuWidth - 8, 22);
        UILayout.SetTextContent(button.GetComponentInChildren<TextMeshProUGUI>(true), label);
        button.onClick.AddListener(() => MoveModFromMenu(direction));
        return button;
    }

    /// <summary>
    /// Closes the menu and stages a one-row move for its mod.
    /// </summary>
    /// <param name="direction">The chosen movement direction.</param>
    private void MoveModFromMenu(int direction)
    {
        OptionsModDragHandle source = _modMoveMenuSource;
        HideModMoveMenu();
        _modGripToFocus = source.ModID;
        ModMoveRequested?.Invoke(source.ModID, source.LoadOrder + direction);
    }

    /// <summary>
    /// Closes a menu after an outside click.
    /// </summary>
    private void DismissModMoveMenu()
    {
        TryCancelModOrdering();
    }

    /// <summary>
    /// Closes the popup without changing the current mod order.
    /// </summary>
    private void HideModMoveMenu()
    {
        _modMoveMenuSource = null;
        _modMoveMenu?.SetActive(false);
    }

    /// <summary>
    /// Returns keyboard focus to the moved mod after the list has been rendered.
    /// </summary>
    private void RestoreModGripFocus()
    {
        if (_modGripToFocus == null)
            return;
        foreach (OptionsModDragHandle grip in _modGrips)
        {
            if (!grip.gameObject.activeInHierarchy || grip.ModID != _modGripToFocus)
                continue;
            RectInt bounds = UILayout.GetSourceRect((RectTransform)grip.transform.parent);
            _modsScrollArea.RevealContentRect(bounds.y, bounds.height);
            grip.GetComponent<Button>().Select();
            break;
        }
        _modGripToFocus = null;
    }
}
