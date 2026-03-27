using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class UISelectable : MonoBehaviour
{
    [SerializeField]
    private GameObject selectionBorder;

    private UISelectionGroup group;
    private AnimatedButton animatedButton;
    private Button button;

    private bool isSelected;

    private void Awake()
    {
        group = GetComponentInParent<UISelectionGroup>();
        animatedButton = GetComponent<AnimatedButton>();
        button = GetComponent<Button>();

        if (selectionBorder != null)
        {
            selectionBorder.SetActive(false);
        }

        button.onClick.AddListener(OnClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClicked);
        }
    }

    private void OnClicked()
    {
        if (group != null)
        {
            group.Select(this);
        }
    }

    public void SetSelected(bool value)
    {
        if (isSelected == value)
            return;

        isSelected = value;

        if (selectionBorder != null)
        {
            selectionBorder.SetActive(value);
        }

        if (animatedButton != null)
        {
            animatedButton.SetFrozen(value);
        }
    }

    public bool IsSelected()
    {
        return isSelected;
    }
}
