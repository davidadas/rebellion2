using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Exercises a gameplay control's pressed artwork while it is hovered in the content editor.
/// </summary>
public sealed class ContentEditorWindowControlHover
    : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler
{
    private RawImagePressVisual visual;
    private bool hovered;

    /// <summary>
    /// Connects the gameplay press visual exercised by this preview-only hover adapter.
    /// </summary>
    /// <param name="pressVisual">The authored gameplay press visual.</param>
    public void Bind(RawImagePressVisual pressVisual)
    {
        SetHovered(false);
        visual = pressVisual
            ? pressVisual
            : throw new MissingReferenceException("A gameplay press visual is required.");
    }

    /// <summary>
    /// Releases a gameplay visual that is not editable on the current preview surface.
    /// </summary>
    internal void Clear()
    {
        SetHovered(false);
        visual = null;
    }

    /// <summary>
    /// Shows the control's authored pressed image while the pointer is over it.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHovered(true);
    }

    /// <summary>
    /// Restores the control's authored normal image when the pointer leaves it.
    /// </summary>
    /// <param name="eventData">The pointer event.</param>
    public void OnPointerExit(PointerEventData eventData)
    {
        SetHovered(false);
    }

    /// <summary>
    /// Restores the normal image if the preview disappears while hovered.
    /// </summary>
    private void OnDisable()
    {
        SetHovered(false);
    }

    /// <summary>
    /// Applies preview-owned hover state after direct or surface-routed pointer detection.
    /// </summary>
    /// <param name="isHovered">Whether this control is the current pointer target.</param>
    internal void SetHovered(bool isHovered)
    {
        if (hovered == isHovered)
            return;

        hovered = isHovered;
        if (isHovered)
        {
            visual?.OnPointerDown(
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                }
            );
        }
        else
            visual?.OnPointerUp(null);
    }
}
