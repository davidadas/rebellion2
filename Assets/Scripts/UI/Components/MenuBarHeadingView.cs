using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Routes menu-bar pointer movement while a desktop menu is open.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class MenuBarHeadingView : MonoBehaviour, IPointerEnterHandler
{
    public event Action<Button> Hovered;

    /// <summary>
    /// Switches between open menu headings without requiring another click.
    /// </summary>
    /// <param name="eventData">The entering pointer.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        Hovered?.Invoke(GetComponent<Button>());
    }
}
