using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Commits a live slider draft when pointer or keyboard interaction finishes.
/// </summary>
public sealed class ContentEditorSliderCommit
    : MonoBehaviour,
        IPointerUpHandler,
        ISubmitHandler,
        IDeselectHandler
{
    private Action commit;

    /// <summary>
    /// Assigns the completed-gesture callback.
    /// </summary>
    /// <param name="callback">The callback to invoke.</param>
    public void Configure(Action callback) => commit = callback;

    /// <summary>
    /// Commits after a pointer slider gesture.
    /// </summary>
    /// <param name="eventData">The completed pointer event.</param>
    public void OnPointerUp(PointerEventData eventData) => commit?.Invoke();

    /// <summary>
    /// Commits after keyboard submission.
    /// </summary>
    /// <param name="eventData">The submit event.</param>
    public void OnSubmit(BaseEventData eventData) => commit?.Invoke();

    /// <summary>
    /// Commits when keyboard focus leaves the slider.
    /// </summary>
    /// <param name="eventData">The deselection event.</param>
    public void OnDeselect(BaseEventData eventData) => commit?.Invoke();
}
