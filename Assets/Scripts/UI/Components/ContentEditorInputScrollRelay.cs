using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Keeps the content editor's outer form scrollable while a text input owns pointer focus.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public sealed class ContentEditorInputScrollRelay : MonoBehaviour, IScrollHandler
{
    private ScrollAreaView scrollArea;
    private TMP_InputField input;

    /// <summary>
    /// Relays a pointer-wheel gesture to the nearest containing editor scroll area.
    /// </summary>
    /// <param name="eventData">The pointer-wheel event.</param>
    public void OnScroll(PointerEventData eventData)
    {
        input ??= GetComponent<TMP_InputField>();
        if (
            input.lineType != TMP_InputField.LineType.SingleLine
            && input.textComponent.preferredHeight > input.textViewport.rect.height
        )
            return;
        if (scrollArea == null)
            scrollArea = GetComponentInParent<ScrollAreaView>();
        scrollArea?.RelayScroll(eventData);
    }
}
