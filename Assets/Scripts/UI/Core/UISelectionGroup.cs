using UnityEngine;

public sealed class UISelectionGroup : MonoBehaviour
{
    [SerializeField]
    private UISelectable defaultSelection;

    private UISelectable current;

    private void Start()
    {
        if (defaultSelection != null)
        {
            Select(defaultSelection);
        }
    }

    public void Select(UISelectable item)
    {
        if (current == item)
            return;

        if (current != null)
        {
            current.SetSelected(false);
        }

        current = item;

        if (current != null)
        {
            current.SetSelected(true);
        }
    }
}
