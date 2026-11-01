using TMPro;
using UnityEngine;

/// <summary>
/// Keeps unfocused text inputs at the beginning of their value while preserving normal editing.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public sealed class TextInputOverflowView : MonoBehaviour
{
    private TMP_InputField input;
    private bool pending;

    /// <summary>
    /// Observes text layout, including values assigned without input notifications.
    /// </summary>
    private void OnEnable()
    {
        input = GetComponent<TMP_InputField>();
        input.onDeselect.AddListener(HandleDeselect);
        input.textComponent.OnPreRenderText += HandleTextLayout;
        pending = true;
    }

    /// <summary>
    /// Releases listeners when a pooled input is hidden or destroyed.
    /// </summary>
    private void OnDisable()
    {
        input.onDeselect.RemoveListener(HandleDeselect);
        input.textComponent.OnPreRenderText -= HandleTextLayout;
    }

    /// <summary>
    /// Restores the prefix after the input has completed its own focus and layout changes.
    /// </summary>
    private void LateUpdate()
    {
        if (pending && !input.isFocused)
            ShowBeginning();
    }

    /// <summary>
    /// Requests a presentation update when a value's text geometry changes.
    /// </summary>
    /// <param name="text">The newly generated text geometry.</param>
    private void HandleTextLayout(TMP_TextInfo text) => pending = true;

    /// <summary>
    /// Defers scrolling until the input has finished deselecting.
    /// </summary>
    /// <param name="value">The committed text.</param>
    private void HandleDeselect(string value) => pending = true;

    /// <summary>
    /// Shows the start of an unfocused value without changing its text or fitting-value alignment.
    /// </summary>
    public void ShowBeginning()
    {
        input ??= GetComponent<TMP_InputField>();
        if (input.isFocused)
            return;
        if (input.verticalScrollbar != null)
        {
            pending = false;
            return;
        }
        input.MoveTextStart(false);
        TMP_Text text = input.textComponent;
        text.rectTransform.anchoredPosition = Vector2.zero;
        input.ForceLabelUpdate();
        text.ForceMeshUpdate();
        if (
            input.lineType == TMP_InputField.LineType.SingleLine
            && text.preferredWidth > input.textViewport.rect.width
            && text.textInfo.characterCount > 0
        )
        {
            RectTransform viewport = input.textViewport;
            Vector3 first = text.rectTransform.TransformPoint(
                new Vector3(text.textInfo.characterInfo[0].origin, 0)
            );
            float offset = viewport.rect.xMin - viewport.InverseTransformPoint(first).x;
            Vector3 shift = text.rectTransform.parent.InverseTransformVector(
                viewport.TransformVector(new Vector3(offset, 0))
            );
            text.rectTransform.anchoredPosition += new Vector2(shift.x, shift.y);
        }
        pending = false;
    }
}
