using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Draws the reorder grip and routes a drag from its hit area to the Mods page.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
internal sealed class OptionsModDragHandle
    : MaskableGraphic,
        IPointerDownHandler,
        IPointerUpHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
{
    private OptionsMenuView _view;

    internal string ModID { get; private set; }
    internal int LoadOrder { get; private set; }
    internal OptionsModCardView Card { get; private set; }

    /// <summary>
    /// Binds pointer and keyboard activation to the owning view for the lifetime of the grip.
    /// </summary>
    /// <param name="view">The Options view that handles ordering.</param>
    /// <param name="card">The card picked up by this grip.</param>
    /// <param name="button">The grip's selectable control.</param>
    internal void Initialize(OptionsMenuView view, OptionsModCardView card, Button button)
    {
        _view = view;
        Card = card;
        button.targetGraphic = this;
        button.onClick.AddListener(OpenMoveMenu);
    }

    /// <summary>
    /// Gives the card immediate press feedback when the grip is pressed.
    /// </summary>
    /// <param name="eventData">The pointer press.</param>
    public void OnPointerDown(PointerEventData eventData) => Card.OnPointerDown(eventData);

    /// <summary>
    /// Clears the card's press feedback when the grip is released.
    /// </summary>
    /// <param name="eventData">The pointer release.</param>
    public void OnPointerUp(PointerEventData eventData) => Card.OnPointerUp(eventData);

    /// <summary>
    /// Assigns the mod represented by this pooled grip.
    /// </summary>
    /// <param name="mod">The currently displayed mod.</param>
    internal void Bind(OptionsModRow mod)
    {
        ModID = mod.ID;
        LoadOrder = mod.LoadOrder;
    }

    /// <summary>
    /// Starts dragging after Unity recognizes a pointer move beyond its drag threshold.
    /// </summary>
    /// <param name="eventData">The pointer that started the drag.</param>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;
        eventData.eligibleForClick = false;
        _view.BeginModDrag(this, eventData);
    }

    /// <summary>
    /// Updates the drag preview for the current pointer.
    /// </summary>
    /// <param name="eventData">The pointer being moved.</param>
    public void OnDrag(PointerEventData eventData) => _view.UpdateModDrag(this, eventData);

    /// <summary>
    /// Completes the drag at its release position.
    /// </summary>
    /// <param name="eventData">The pointer being released.</param>
    public void OnEndDrag(PointerEventData eventData) => _view.EndModDrag(this, eventData);

    /// <summary>
    /// Draws two columns of three dots in the centre of the hit area.
    /// </summary>
    /// <param name="vertices">The UI mesh that receives the dots.</param>
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();
        Vector2 center = GetPixelAdjustedRect().center;
        for (int column = -1; column <= 1; column += 2)
        for (int row = -1; row <= 1; row++)
        {
            Vector2 dot = center + new Vector2(column * 2.5f, row * 4.5f);
            int start = vertices.currentVertCount;
            vertices.AddVert(dot, color, Vector2.zero);
            for (int point = 0; point < 8; point++)
            {
                float angle = -point * Mathf.PI / 4f;
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.4f;
                vertices.AddVert(dot + offset, color, Vector2.zero);
                vertices.AddTriangle(start, start + 1 + point, start + 1 + (point + 1) % 8);
            }
        }
    }

    /// <summary>
    /// Opens the movement commands on click or keyboard submit.
    /// </summary>
    private void OpenMoveMenu() => _view.ShowModMoveMenu(this);
}
