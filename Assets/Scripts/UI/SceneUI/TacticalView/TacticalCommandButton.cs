using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A tactile command key; intentionally does not issue gameplay orders yet.
/// </summary>
public sealed class TacticalCommandButton : Button
{
    [SerializeField]
    private RectTransform keyFace;
    private Vector2 _releasedPosition;

    /// <summary>
    /// Captures the authored key face before input is enabled.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        if (!keyFace)
            throw new MissingReferenceException("Tactical command key face is missing.");
        _releasedPosition = keyFace.anchoredPosition;
    }

    /// <summary>
    /// Applies the standard button tint and a physical key depression.
    /// </summary>
    /// <param name="state">The current selectable state.</param>
    /// <param name="instant">Whether the tint should change immediately.</param>
    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        if (keyFace)
            keyFace.anchoredPosition =
                _releasedPosition
                + (state == SelectionState.Pressed ? new Vector2(0f, -2f) : Vector2.zero);
    }

    /// <summary>
    /// Restores the released key position when the dock or scene is deactivated.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        if (keyFace)
            keyFace.anchoredPosition = _releasedPosition;
    }
}
