using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class SelectableListRowView
    : MonoBehaviour,
        IPointerDownHandler,
        IPointerClickHandler,
        IMoveHandler,
        ISubmitHandler
{
    public event Action<SelectableListRowView, PointerEventData> Selected;
    public event Action<SelectableListRowView, PointerEventData> Activated;
    public event Action<SelectableListRowView, PointerEventData> ContextRequested;

    private Func<bool> canNavigate = () => true;

    public int Index { get; private set; }

    public void SetNavigationGate(Func<bool> canNavigate)
    {
        this.canNavigate = canNavigate ?? (() => true);
    }

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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount >= 2)
            Activated?.Invoke(this, eventData);
    }

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

    public void OnSubmit(BaseEventData eventData)
    {
        if (!CanNavigate())
            return;

        Activated?.Invoke(this, null);
        eventData.Use();
    }

    private bool CanNavigate()
    {
        return canNavigate?.Invoke() != false;
    }

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
            if (row == null || !row.gameObject.activeInHierarchy)
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

    public void FocusForNavigation(BaseEventData eventData = null)
    {
        EventSystem.current?.SetSelectedGameObject(gameObject, eventData);
    }

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
            currentSelection != null
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
