using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Provides shared pointer and keyboard interaction for an authored selectable-list row.
/// </summary>
public abstract class SelectableListRowView
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerClickHandler,
        IMoveHandler,
        ISubmitHandler
{
    /// <summary>
    /// Occurs when the control is selected.
    /// </summary>
    public event Action<SelectableListRowView, PointerEventData> Selected;

    /// <summary>
    /// Occurs when the control is activated.
    /// </summary>
    public event Action<SelectableListRowView, PointerEventData> Activated;

    /// <summary>
    /// Occurs when a context request is raised.
    /// </summary>
    public event Action<SelectableListRowView, PointerEventData> ContextRequested;

    private Func<bool> canNavigate = () => true;

    public int Index { get; private set; }

    /// <summary>
    /// Assigns the feature-level gate for keyboard navigation.
    /// </summary>
    /// <param name="canNavigate">Determines whether navigation is currently allowed.</param>
    public void SetNavigationGate(Func<bool> canNavigate)
    {
        this.canNavigate = canNavigate ?? (() => true);
    }

    /// <summary>
    /// Configures this reusable row's source index and authored pointer hit area.
    /// </summary>
    /// <param name="index">The source index represented by the row.</param>
    /// <param name="hitAreaImage">The authored pointer hit area.</param>
    protected void ConfigureSelectableRow(int index, RawImage hitAreaImage)
    {
        if (hitAreaImage == null)
            throw new MissingReferenceException($"{name}/HitAreaImage is missing.");

        Index = index;
        enabled = true;
        hitAreaImage.enabled = true;
        hitAreaImage.raycastTarget = true;
        hitAreaImage.canvasRenderer.cullTransparentMesh = false;
    }

    /// <summary>
    /// Emits activation for a left-button double click.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            Activated?.Invoke(this, eventData);
    }

    /// <summary>
    /// Emits selection or a context request for a pointer press.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
            ContextRequested?.Invoke(this, eventData);
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            FocusForNavigation(eventData);
            Selected?.Invoke(this, eventData);
        }
    }

    /// <summary>
    /// Moves keyboard focus and selection to the next active sibling row.
    /// </summary>
    /// <param name="eventData">The navigation event.</param>
    public void OnMove(AxisEventData eventData)
    {
        if (!CanNavigate())
            return;

        int direction = GetMoveDirection(eventData);
        if (direction == 0)
            return;

        SelectableListRowView target = GetNavigatedRow(direction);
        if (target == null || target == this)
            return;

        target.FocusForNavigation(eventData);
        target.Selected?.Invoke(target, null);
        eventData.Use();
    }

    /// <summary>
    /// Emits activation for a permitted submit action.
    /// </summary>
    /// <param name="eventData">The submit event.</param>
    public void OnSubmit(BaseEventData eventData)
    {
        if (!CanNavigate())
            return;

        Activated?.Invoke(this, null);
        eventData.Use();
    }

    /// <summary>
    /// Evaluates the current keyboard-navigation gate.
    /// </summary>
    /// <returns>True when keyboard navigation is permitted.</returns>
    private bool CanNavigate()
    {
        return canNavigate?.Invoke() != false;
    }

    /// <summary>
    /// Finds the nearest active sibling row in a vertical direction.
    /// </summary>
    /// <param name="direction">A negative value for the previous row or positive for the next.</param>
    /// <returns>The nearest active row in that direction, or null.</returns>
    private SelectableListRowView GetNavigatedRow(int direction)
    {
        Transform parent = transform.parent;
        if (parent == null)
            return null;

        int currentSibling = transform.GetSiblingIndex();
        SelectableListRowView candidate = null;
        int candidateSibling = direction > 0 ? int.MaxValue : int.MinValue;
        for (int i = 0; i < parent.childCount; i++)
        {
            SelectableListRowView row = parent.GetChild(i).GetComponent<SelectableListRowView>();
            if (!row || !row.gameObject.activeInHierarchy)
                continue;

            int sibling = row.transform.GetSiblingIndex();
            if (direction > 0 && sibling > currentSibling && sibling < candidateSibling)
            {
                candidate = row;
                candidateSibling = sibling;
            }
            else if (direction < 0 && sibling < currentSibling && sibling > candidateSibling)
            {
                candidate = row;
                candidateSibling = sibling;
            }
        }

        return candidate;
    }

    /// <summary>
    /// Converts a Unity navigation direction into a signed vertical offset.
    /// </summary>
    /// <param name="eventData">The navigation event.</param>
    /// <returns>Negative one, positive one, or zero for unsupported movement.</returns>
    private static int GetMoveDirection(AxisEventData eventData)
    {
        if (eventData == null)
            return 0;

        return eventData.moveDir switch
        {
            MoveDirection.Up => -1,
            MoveDirection.Down => 1,
            _ => 0,
        };
    }

    /// <summary>
    /// Gives this row EventSystem focus for keyboard navigation.
    /// </summary>
    /// <param name="eventData">The event that initiated focus, when available.</param>
    public void FocusForNavigation(BaseEventData eventData = null)
    {
        EventSystem.current?.SetSelectedGameObject(gameObject, eventData);
    }

    /// <summary>
    /// Restores row focus when the current selection is outside a list's navigation scope.
    /// </summary>
    /// <param name="selectionScope">The list's navigation scope.</param>
    /// <param name="canFocus">Whether the feature currently accepts navigation.</param>
    /// <param name="row">The row that should receive focus.</param>
    public static void FocusRowForNavigation(
        Transform selectionScope,
        bool canFocus,
        SelectableListRowView row
    )
    {
        if (!canFocus || row == null)
            return;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        GameObject currentSelection = eventSystem.currentSelectedGameObject;
        if (
            currentSelection
            && currentSelection.activeInHierarchy
            && selectionScope != null
            && currentSelection.transform.IsChildOf(selectionScope)
        )
        {
            return;
        }

        row.FocusForNavigation();
    }
}
