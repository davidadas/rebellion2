using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Routes menu-bar pointer movement while a desktop menu is open.
/// </summary>
public sealed class ContentEditorMenuHeading : MonoBehaviour, IPointerEnterHandler
{
    [SerializeField]
    private ContentPackEditorView owner;

    [SerializeField]
    private Button button;

    /// <summary>
    /// Switches between open menu headings without requiring another click.
    /// </summary>
    /// <param name="eventData">The entering pointer.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        owner.HoverMenu(button);
    }
}
