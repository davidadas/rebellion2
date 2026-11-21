using System.Diagnostics.CodeAnalysis;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Keeps the content editor's outer form scrollable while a text input owns pointer focus.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public sealed class ContentEditorInputScrollRelay : MonoBehaviour, IScrollHandler
{
    private ScrollAreaView scrollArea;
    private TMP_InputField input;
    private bool thumbLayoutDirty;
    private EventTrigger thumbEvents;
    private readonly EventTrigger.Entry thumbPress = new()
    {
        eventID = EventTriggerType.PointerDown,
    };

    /// <summary>
    /// Observes text geometry so a pooled multiline input can size its thumb after layout.
    /// </summary>
    private void OnEnable()
    {
        input = GetComponent<TMP_InputField>();
        input.textComponent.OnPreRenderText += MarkThumbLayoutDirty;
        thumbPress.callback.AddListener(FocusFromThumb);
        thumbLayoutDirty = true;
    }

    /// <summary>
    /// Releases subscriptions when the field is hidden or reused.
    /// </summary>
    private void OnDisable()
    {
        input.textComponent.OnPreRenderText -= MarkThumbLayoutDirty;
        ObserveThumb(null);
        thumbPress.callback.RemoveListener(FocusFromThumb);
    }

    /// <summary>
    /// Requests a thumb update after the input's viewport changes size.
    /// </summary>
    [SuppressMessage("Roslynator", "RCS1213", Justification = "Invoked by Unity.")]
    private void OnRectTransformDimensionsChange() => thumbLayoutDirty = true;

    /// <summary>
    /// Requests a thumb update after text is laid out or assigned without notification.
    /// </summary>
    /// <param name="layout">The regenerated input text layout.</param>
    private void MarkThumbLayoutDirty(TMP_TextInfo layout) => thumbLayoutDirty = true;

    /// <summary>
    /// Sizes the native scroll thumb to the portion of text visible in the input.
    /// </summary>
    private void LateUpdate()
    {
        if (!thumbLayoutDirty)
            return;
        thumbLayoutDirty = false;
        Scrollbar bar = input.verticalScrollbar;
        ObserveThumb(bar);
        if (bar == null)
            return;

        float viewport = input.textViewport.rect.height;
        float content = input.textComponent.preferredHeight;
        bar.size = viewport < content ? viewport / content : 1f;
    }

    /// <summary>
    /// Adds input activation to the scrollbar used by this multiline field.
    /// </summary>
    /// <param name="bar">The current native scrollbar, or null for a single-line field.</param>
    private void ObserveThumb(Scrollbar bar)
    {
        if (thumbEvents != null && thumbEvents.gameObject == bar?.gameObject)
            return;
        if (thumbEvents != null)
            thumbEvents.triggers.Remove(thumbPress);
        thumbEvents = null;
        if (bar == null)
            return;
        thumbEvents =
            bar.GetComponent<EventTrigger>() ?? bar.gameObject.AddComponent<EventTrigger>();
        thumbEvents.triggers.Add(thumbPress);
    }

    /// <summary>
    /// Activates the text editor when its thumb receives a direct primary-button press.
    /// </summary>
    /// <param name="eventData">The scrollbar press.</param>
    private void FocusFromThumb(BaseEventData eventData)
    {
        if (eventData is PointerEventData { button: PointerEventData.InputButton.Left })
            input.ActivateInputField();
    }

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
