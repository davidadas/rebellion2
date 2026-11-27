using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class OptionsMenuView
{
    internal const int ModEnableWidth = 80;

    private int _sortableModCount;
    private SortableRowView _draggedMod;
    private CardReorderDrag _modDrag;
    private CardReorderMenu _modMoveMenu;
    private string _modToFocus;

    /// <summary>
    /// Gets or sets the controller's dependency check for a proposed move.
    /// </summary>
    internal Func<int, int, string> CheckModMove { get; set; }

    /// <summary>
    /// Starts a drag on an active mod and retains the pointer used for auto-scrolling.
    /// </summary>
    /// <param name="card">The card that began dragging.</param>
    /// <param name="eventData">The current drag pointer.</param>
    private void BeginModDrag(SortableRowView card, PointerEventData eventData)
    {
        if (_draggedMod != null)
            return;
        _modMoveMenu?.Hide();
        _draggedMod = card;
        _modDrag ??= new CardReorderDrag(
            _modsScrollArea,
            (RectTransform)_modsPage.transform.parent,
            _rowIdleSprite
        )
        {
            CheckMove = (origin, target) => CheckModMove?.Invoke(origin + 1, target + 1),
        };
        _modDrag.Accent = _accentColor;
        _modDrag.Error = _errorColor;
        _modDrag.Begin(_modCards.Take(_sortableModCount), _modCards.IndexOf(card), eventData);
    }

    /// <summary>
    /// Issues one move request on drop so the controller can validate and stage it.
    /// </summary>
    /// <param name="card">The card that owns the drag.</param>
    /// <param name="eventData">The final pointer event.</param>
    private void EndModDrag(SortableRowView card, PointerEventData eventData)
    {
        if (_draggedMod != card)
            return;
        int destination = _modDrag.Drop(card.Rect, eventData.position);
        CancelModDrag();
        if (destination >= 0)
            ModMoveRequested?.Invoke(card.ItemID, destination + 1);
    }

    /// <summary>
    /// Animates neighboring cards and scrolls while the pointer is near a viewport edge.
    /// </summary>
    private void Update() => _modDrag?.Update(Time.unscaledDeltaTime);

    /// <summary>
    /// Clears ordering interaction when the Options window becomes inactive.
    /// </summary>
    private void OnDisable() => CancelModOrdering();

    /// <summary>
    /// Restores both the card layout and the Options pickup styling.
    /// </summary>
    private void CancelModDrag()
    {
        if (_draggedMod == null)
            return;
        _modDrag.Cancel();
        _draggedMod = null;
    }

    /// <summary>
    /// Clears interaction when the list is rerendered or its page is left.
    /// </summary>
    private void CancelModOrdering()
    {
        CancelModDrag();
        _modMoveMenu?.Hide();
    }

    /// <summary>
    /// Handles Escape for the active mod drag or its movement menu.
    /// </summary>
    /// <returns>Whether a mod ordering interaction was dismissed.</returns>
    internal bool TryCancelModOrdering()
    {
        if (_draggedMod == null)
            return _modMoveMenu?.TryCancel() == true;
        CancelModDrag();
        return true;
    }

    /// <summary>
    /// Opens the shared movement commands beside the selected bar.
    /// </summary>
    /// <param name="source">The card requesting movement.</param>
    private void ShowModMoveMenu(SortableRowView source)
    {
        if (_draggedMod != null)
            return;
        _modMoveMenu ??= new CardReorderMenu(
            (RectTransform)_modsPage.transform.parent,
            _applyButton
        );
        _modMoveMenu.Show(
            source,
            _modCards.IndexOf(source),
            _sortableModCount,
            new Color(0.04f, 0.06f, 0.10f),
            destination =>
            {
                _modToFocus = source.ItemID;
                ModMoveRequested?.Invoke(source.ItemID, destination + 1);
            }
        );
    }

    /// <summary>
    /// Returns keyboard focus to the moved mod after the list has been rendered.
    /// </summary>
    private void RestoreModRowFocus()
    {
        if (_modToFocus == null)
            return;
        foreach (SortableRowView card in _modCards.Take(_sortableModCount))
        {
            if (card.ItemID != _modToFocus)
                continue;
            RectInt bounds = UILayout.GetSourceRect(card.Rect);
            _modsScrollArea.RevealContentRect(bounds.y, bounds.height);
            card.Select();
            break;
        }
        _modToFocus = null;
    }
}
