using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Exposes a captured mouse sample through Unity's shared input interface.
/// </summary>
[DisallowMultipleComponent]
public sealed class MouseSnapshotInput : BaseInput
{
    private Vector2 _position;
    private Vector2 _scroll;
    private byte _previousButtons;
    private byte _buttons;

    public override Vector2 mousePosition => _position;

    public override Vector2 mouseScrollDelta => _scroll;

    /// <summary>
    /// Supplies the mouse values consumed while Unity builds pointer event data.
    /// </summary>
    /// <param name="position">The captured position in screen pixels.</param>
    /// <param name="scroll">The scroll delta assigned to this sample.</param>
    /// <param name="previousButtons">The buttons held before this sample.</param>
    /// <param name="buttons">The buttons held in this sample.</param>
    internal void SetSnapshot(Vector2 position, Vector2 scroll, byte previousButtons, byte buttons)
    {
        _position = position;
        _scroll = scroll;
        _previousButtons = previousButtons;
        _buttons = buttons;
    }

    /// <summary>
    /// Reports a press edge from the captured button states.
    /// </summary>
    /// <param name="button">The zero-based mouse button index.</param>
    /// <returns>Whether the button became pressed in this sample.</returns>
    public override bool GetMouseButtonDown(int button)
    {
        int mask = 1 << button;
        return (_buttons & mask) != 0 && (_previousButtons & mask) == 0;
    }

    /// <summary>
    /// Reports a release edge from the captured button states.
    /// </summary>
    /// <param name="button">The zero-based mouse button index.</param>
    /// <returns>Whether the button became released in this sample.</returns>
    public override bool GetMouseButtonUp(int button)
    {
        int mask = 1 << button;
        return (_buttons & mask) == 0 && (_previousButtons & mask) != 0;
    }

    /// <summary>
    /// Reports whether a button is held in the captured sample.
    /// </summary>
    /// <param name="button">The zero-based mouse button index.</param>
    /// <returns>Whether the captured sample holds the button.</returns>
    public override bool GetMouseButton(int button)
    {
        return (_buttons & (1 << button)) != 0;
    }
}
